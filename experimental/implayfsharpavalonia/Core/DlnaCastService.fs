namespace ImPlay.Core.Services

open System
open System.Collections.Concurrent
open System.Collections.Generic
open System.Globalization
open System.IO
open System.Linq
open System.Net
open System.Net.Http
open System.Net.NetworkInformation
open System.Net.Sockets
open System.Security
open System.Text
open System.Threading
open System.Threading.Tasks
open System.Xml.Linq
open ImPlay.Core.Models

type DlnaPositionInfo = { Position : TimeSpan; Duration : TimeSpan }

type DlnaCastService() =
    let AvTransportService = "urn:schemas-upnp-org:service:AVTransport:1"
    let RenderingControlService = "urn:schemas-upnp-org:service:RenderingControl:1"

    let http = new HttpClient(Timeout = TimeSpan.FromSeconds(4.0))
    let devices = ConcurrentDictionary<string, DlnaCastDevice>()
    let serverLock = obj()
    let mutable serverCts : CancellationTokenSource option = None
    let mutable listener : TcpListener option = None
    let mutable servedFilePath : string option = None
    let mutable servedSubtitlePath : string option = None
    let mutable servedFileUri : Uri option = None
    let mutable servedSubtitleUri : Uri option = None
    let mutable activeDevice : DlnaCastDevice option = None

    let isHttpUri (uri: Uri) =
        uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)

    let resolveBaseUrl (descriptionUrl: Uri) (urlBase: string) =
        match Uri.TryCreate(urlBase, UriKind.Absolute) with
        | true, parsed when isHttpUri parsed -> parsed
        | _ -> descriptionUrl

    let resolveDeviceUrl (baseUrl: Uri) (path: string) =
        if String.IsNullOrWhiteSpace(path) then None
        else
            let trimmed = path.Trim()
            let mutable resolved = 
                match Uri.TryCreate(trimmed, UriKind.Absolute) with
                | true, absolute -> absolute
                | _ -> Uri(baseUrl, trimmed)
            
            if not (isHttpUri resolved) && not (String.IsNullOrWhiteSpace(resolved.AbsolutePath)) then
                resolved <- Uri(baseUrl, resolved.PathAndQuery)
            
            if isHttpUri resolved then Some resolved else None

    let extractSoapFault (xml: string) =
        if String.IsNullOrWhiteSpace(xml) then None
        else
            try
                let doc = XDocument.Parse(xml)
                let errorDescription = 
                    doc.Descendants()
                    |> Seq.tryFind (fun e -> e.Name.LocalName.Equals("errorDescription", StringComparison.OrdinalIgnoreCase))
                    |> Option.map (fun e -> e.Value.Trim())
                
                match errorDescription with
                | Some d when not (String.IsNullOrWhiteSpace(d)) -> Some d
                | _ ->
                    let fs = 
                        doc.Descendants()
                        |> Seq.tryFind (fun e -> e.Name.LocalName.Equals("faultstring", StringComparison.OrdinalIgnoreCase))
                        |> Option.map (fun e -> e.Value.Trim())
                    match fs with
                    | Some f when not (String.IsNullOrWhiteSpace(f)) -> Some f
                    | _ -> None
            with _ ->
                if xml.Length > 180 then Some $"{xml.Substring(0, 180)}..." else Some xml

    let soapAsync (controlUrl: Uri) (serviceType: string) (action: string) (body: string) (ct: CancellationToken) =
        task {
            let envelope = 
                "<?xml version=\"1.0\"?>\n" +
                "<s:Envelope xmlns:s=\"http://schemas.xmlsoap.org/soap/envelope/\" s:encodingStyle=\"http://schemas.xmlsoap.org/soap/encoding/\">\n" +
                "  <s:Body>\n" +
                $"    <u:{action} xmlns:u=\"{serviceType}\">\n" +
                $"      {body}\n" +
                $"    </u:{action}>\n" +
                "  </s:Body>\n" +
                "</s:Envelope>"
            
            use request = new HttpRequestMessage(HttpMethod.Post, controlUrl)
            request.Headers.TryAddWithoutValidation("SOAPACTION", $"\"{serviceType}#{action}\"") |> ignore
            request.Content <- new StringContent(envelope, Encoding.UTF8, "text/xml")
            
            use! response = http.SendAsync(request, ct)
            let! text = response.Content.ReadAsStringAsync(ct)
            
            if not response.IsSuccessStatusCode then
                let detail = extractSoapFault text
                let msg = 
                    match detail with
                    | Some d -> $"{action} failed: {d}"
                    | None -> $"{action} failed: HTTP {int response.StatusCode} {response.ReasonPhrase}"
                raise (InvalidOperationException(msg))
            return text
        }

    let readSoapString (xml: string) (elementName: string) =
        try
            let doc = XDocument.Parse(xml)
            doc.Descendants()
            |> Seq.tryFind (fun e -> e.Name.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase))
            |> Option.map (fun e -> e.Value.Trim())
        with _ -> None

    let readSoapTime (xml: string) (elementName: string) =
        match readSoapString xml elementName with
        | Some v when not (v.StartsWith("NOT_IMPLEMENTED", StringComparison.OrdinalIgnoreCase)) ->
            match TimeSpan.TryParse(v, CultureInfo.InvariantCulture) with
            | true, parsed -> Some parsed
            | _ -> None
        | _ -> None

    let formatDlnaTime (time: TimeSpan) =
        $"{int time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}"

    let getMimeType (path: string) =
        match Path.GetExtension(path).ToLowerInvariant() with
        | ".mp4" | ".m4v" -> "video/mp4"
        | ".mkv" -> "video/x-matroska"
        | ".webm" -> "video/webm"
        | ".avi" -> "video/x-msvideo"
        | ".mov" -> "video/quicktime"
        | ".mp3" -> "audio/mpeg"
        | ".flac" -> "audio/flac"
        | ".ogg" | ".oga" -> "audio/ogg"
        | ".opus" -> "audio/opus"
        | ".wav" -> "audio/wav"
        | ".m4a" | ".aac" -> "audio/aac"
        | ".srt" -> "application/x-subrip"
        | ".vtt" -> "text/vtt"
        | _ -> "application/octet-stream"

    let getLanAddress() =
        NetworkInterface.GetAllNetworkInterfaces()
        |> Seq.filter (fun ni -> ni.OperationalStatus = OperationalStatus.Up && ni.NetworkInterfaceType <> NetworkInterfaceType.Loopback && ni.NetworkInterfaceType <> NetworkInterfaceType.Tunnel)
        |> Seq.collect (fun ni -> ni.GetIPProperties().UnicastAddresses)
        |> Seq.tryFind (fun unicast -> unicast.Address.AddressFamily = AddressFamily.InterNetwork && not (IPAddress.IsLoopback(unicast.Address)))
        |> Option.map (fun unicast -> unicast.Address)

    let getFreePort() =
        let l = new TcpListener(IPAddress.Loopback, 0)
        l.Start()
        let port = (l.LocalEndpoint :?> IPEndPoint).Port
        l.Stop()
        port

    let buildDidlLite (filePath: string) (mediaUri: Uri) (subtitleUri: Uri option) =
        let title = SecurityElement.Escape(Path.GetFileNameWithoutExtension(filePath))
        let mime = getMimeType filePath
        let subMetadata = 
            match subtitleUri with
            | Some uri -> 
                let u = SecurityElement.Escape(uri.ToString())
                $"<res protocolInfo=\"http-get:*:text/srt:*\">{u}</res>\n" +
                $"<res protocolInfo=\"http-get:*:application/x-subrip:*\">{u}</res>\n" +
                $"<upnp:subtitle>{u}</upnp:subtitle>\n" +
                $"<sec:CaptionInfo sec:type=\"srt\">{u}</sec:CaptionInfo>\n" +
                $"<sec:CaptionInfoEx sec:type=\"srt\">{u}</sec:CaptionInfoEx>"
            | None -> ""
        
        let classType = if mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) then "object.item.audioItem.musicTrack" else "object.item.videoItem.movie"
        "<DIDL-Lite xmlns=\"urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\" xmlns:upnp=\"urn:schemas-upnp-org:metadata-1-0/upnp/\" xmlns:sec=\"http://www.sec.co.kr/\">\n" +
        "  <item id=\"0\" parentID=\"0\" restricted=\"1\">\n" +
        $"    <dc:title>{title}</dc:title>\n" +
        $"    <upnp:class>{classType}</upnp:class>\n" +
        $"    <res protocolInfo=\"http-get:*:{mime}:*\">{SecurityElement.Escape(mediaUri.ToString())}</res>\n" +
        (if String.IsNullOrEmpty(subMetadata) then "" else $"    {subMetadata}\n") +
        "  </item>\n" +
        "</DIDL-Lite>"

    let parseHeaders (response: string) =
        let d = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        let lines = response.Split([| "\r\n"; "\n" |], StringSplitOptions.RemoveEmptyEntries)
        for line in lines.Skip(1) do
            let idx = line.IndexOf(':')
            if idx > 0 then
                d.[line.Substring(0, idx).Trim()] <- line.Substring(idx + 1).Trim()
        d

    let writeResponseHeadersAsync (stream: NetworkStream) (status: int) (reason: string) (contentType: string) (contentLength: int64) (contentRange: string option) (ct: CancellationToken) =
        task {
            let sb = StringBuilder()
            sb.Append($"HTTP/1.1 {status} {reason}\r\n") |> ignore
            sb.Append($"Content-Type: {contentType}\r\n") |> ignore
            sb.Append($"Content-Length: {contentLength}\r\n") |> ignore
            sb.Append("Accept-Ranges: bytes\r\n") |> ignore
            sb.Append("Connection: close\r\n") |> ignore
            match contentRange with
            | Some r -> sb.Append($"Content-Range: {r}\r\n") |> ignore
            | None -> ()
            sb.Append("\r\n") |> ignore
            
            let bytes = Encoding.ASCII.GetBytes(sb.ToString())
            do! stream.WriteAsync(bytes, ct)
        }

    let readHttpRequestAsync (stream: NetworkStream) (ct: CancellationToken) =
        task {
            let buffer : byte[] = Array.zeroCreate 8192
            let mutable total = 0
            let mutable result = None
            while total < buffer.Length && result.IsNone do
                let! read = stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), ct)
                if read = 0 then total <- buffer.Length // exit loop
                else
                    total <- total + read
                    let text = Encoding.ASCII.GetString(buffer, 0, total)
                    let endIdx = text.IndexOf("\r\n\r\n")
                    if endIdx >= 0 then
                        let lines = text.Substring(0, endIdx).Split("\r\n")
                        let requestLine = lines.[0].Split(' ')
                        if requestLine.Length >= 2 then
                            let headers = Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                            for line in lines.Skip(1) do
                                let idx = line.IndexOf(':')
                                if idx > 0 then
                                    headers.[line.Substring(0, idx).Trim()] <- line.Substring(idx + 1).Trim()
                            result <- Some (requestLine.[0], requestLine.[1], headers)
            return result
        }

    let serveClientAsync (client: TcpClient) (ct: CancellationToken) =
        task {
            use _ = client
            use network = client.GetStream()
            let! request = readHttpRequestAsync network ct
            match request with
            | None -> ()
            | Some (method, requestPath, headers) ->
                let path = 
                    if requestPath.Contains("/subtitle/", StringComparison.OrdinalIgnoreCase) then servedSubtitlePath
                    else servedFilePath
                
                match path with
                | Some p when File.Exists(p) ->
                    try
                        let info = FileInfo(p)
                        let mutable start = 0L
                        let mutable endPos = info.Length - 1L
                        let status, contentRange = 
                            match headers.TryGetValue("range") with
                            | true, range when range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase) ->
                                let parts = range.Substring(6).Split('-', 2)
                                match Int64.TryParse(parts.[0]) with
                                | true, s -> start <- Math.Clamp(s, 0L, endPos)
                                | _ -> ()
                                if parts.Length > 1 then
                                    match Int64.TryParse(parts.[1]) with
                                    | true, e -> endPos <- Math.Clamp(e, start, info.Length - 1L)
                                    | _ -> ()
                                206, Some $"bytes {start}-{endPos}/{info.Length}"
                            | _ -> 200, None
                        
                        let length = endPos - start + 1L
                        do! writeResponseHeadersAsync network status (if status = 206 then "Partial Content" else "OK") (getMimeType p) length contentRange ct
                        
                        if not (method.Equals("HEAD", StringComparison.OrdinalIgnoreCase)) then
                            use stream = File.OpenRead(p)
                            stream.Seek(start, SeekOrigin.Begin) |> ignore
                            let buffer : byte[] = Array.zeroCreate (128 * 1024)
                            let mutable remaining = length
                            while remaining > 0L && not ct.IsCancellationRequested do
                                let! read = stream.ReadAsync(buffer.AsMemory(0, int(Math.Min(int64 buffer.Length, remaining))), ct)
                                if read = 0 then remaining <- 0L
                                else
                                    do! network.WriteAsync(buffer.AsMemory(0, read), ct)
                                    remaining <- remaining - int64 read
                    with _ -> ()
                | _ ->
                    do! writeResponseHeadersAsync network 404 "Not Found" "text/plain" 0L None ct
        }

    let serverLoopAsync (l: TcpListener) (ct: CancellationToken) =
        task {
            while not ct.IsCancellationRequested do
                try
                    let! client = l.AcceptTcpClientAsync(ct)
                    let _ = Task.Run((fun () -> serveClientAsync client ct :> Task), ct)
                    ()
                with
                | :? OperationCanceledException -> ()
                | _ when not ct.IsCancellationRequested -> do! Task.Delay(100, ct)
                | _ -> ()
        }

    let stopServer() =
        lock serverLock (fun () ->
            activeDevice <- None
            servedFilePath <- None
            servedSubtitlePath <- None
            servedFileUri <- None
            servedSubtitleUri <- None
            serverCts |> Option.iter (fun c -> c.Cancel(); c.Dispose())
            serverCts <- None
            listener |> Option.iter (fun l -> l.Stop())
            listener <- None
        )

    let playAsync (device: DlnaCastDevice) (ct: CancellationToken) =
        soapAsync device.ControlUrl AvTransportService "Play" "<InstanceID>0</InstanceID><Speed>1</Speed>" ct

    let pauseAsync (device: DlnaCastDevice) (ct: CancellationToken) =
        soapAsync device.ControlUrl AvTransportService "Pause" "<InstanceID>0</InstanceID>" ct

    let stopAsync (device: DlnaCastDevice) (ct: CancellationToken) =
        soapAsync device.ControlUrl AvTransportService "Stop" "<InstanceID>0</InstanceID>" ct

    let setTransportUriAsync (device: DlnaCastDevice) (mediaUri: Uri) (metadata: string) (ct: CancellationToken) =
        let body = 
            "<InstanceID>0</InstanceID>\n" +
            $"<CurrentURI>{SecurityElement.Escape(mediaUri.ToString())}</CurrentURI>\n" +
            $"<CurrentURIMetaData>{SecurityElement.Escape(metadata)}</CurrentURIMetaData>"
        soapAsync device.ControlUrl AvTransportService "SetAVTransportURI" body ct

    let setVolumeAsync (device: DlnaCastDevice) (volume: int) (ct: CancellationToken) =
        match device.RenderingControlUrl with
        | Some url ->
            soapAsync url RenderingControlService "SetVolume"
                $"<InstanceID>0</InstanceID><Channel>Master</Channel><DesiredVolume>{Math.Clamp(volume, 0, 100)}</DesiredVolume>" ct
        | None -> Task.FromResult("")

    let seekAsync (device: DlnaCastDevice) (position: TimeSpan) (ct: CancellationToken) =
        soapAsync device.ControlUrl AvTransportService "Seek"
            $"<InstanceID>0</InstanceID><Unit>REL_TIME</Unit><Target>{formatDlnaTime position}</Target>" ct

    let getPositionInfoAsync (device: DlnaCastDevice) (ct: CancellationToken) =
        task {
            let! response = soapAsync device.ControlUrl AvTransportService "GetPositionInfo" "<InstanceID>0</InstanceID>" ct
            let pos = readSoapTime response "RelTime"
            let dur = readSoapTime response "TrackDuration"
            match pos, dur with
            | None, None -> return None
            | _ -> return Some { Position = pos |> Option.defaultValue TimeSpan.Zero; Duration = dur |> Option.defaultValue TimeSpan.Zero }
        }

    let getIsPlayingAsync (device: DlnaCastDevice) (ct: CancellationToken) =
        task {
            let! response = soapAsync device.ControlUrl AvTransportService "GetTransportInfo" "<InstanceID>0</InstanceID>" ct
            match readSoapString response "CurrentTransportState" with
            | Some state when state.Equals("PLAYING", StringComparison.OrdinalIgnoreCase) || state.Equals("TRANSITIONING", StringComparison.OrdinalIgnoreCase) -> return Some true
            | Some _ -> return Some false
            | None -> return None
        }

    let addDeviceFromDescriptionAsync (descriptionUrl: Uri) (ct: CancellationToken) =
        task {
            try
                let! xml = http.GetStringAsync(descriptionUrl, ct)
                let doc = XDocument.Parse(xml)
                let ns = 
                    if isNull doc.Root then XNamespace.None
                    else doc.Root.Name.Namespace
                
                let baseUrl = resolveBaseUrl descriptionUrl (if isNull doc.Root then "" else doc.Root.Element(ns + "URLBase") |> fun e -> if isNull e then "" else e.Value)
                let deviceNode = 
                    doc.Descendants(ns + "device")
                    |> Seq.tryFind (fun d -> d.Elements(ns + "deviceType") |> Seq.exists (fun e -> e.Value.Contains("MediaRenderer", StringComparison.OrdinalIgnoreCase)))
                
                match deviceNode with
                | Some d ->
                    let name = d.Element(ns + "friendlyName") |> fun e -> if isNull e then "" else e.Value.Trim()
                    if not (String.IsNullOrWhiteSpace(name)) then
                        let udn = d.Element(ns + "UDN") |> fun e -> if isNull e then descriptionUrl.ToString() else e.Value.Trim()
                        let services = d.Descendants(ns + "service") |> Seq.toList
                        let av = services |> List.tryFind (fun s -> s.Element(ns + "serviceType") |> fun e -> not (isNull e) && e.Value.Trim() = AvTransportService)
                        match av with
                        | Some s ->
                            let rc = services |> List.tryFind (fun s -> s.Element(ns + "serviceType") |> fun e -> not (isNull e) && e.Value.Trim() = RenderingControlService)
                            let control = 
                                resolveDeviceUrl baseUrl (s.Element(ns + "controlURL") |> fun e -> if isNull e then "" else e.Value)
                                |> Option.orElse (resolveDeviceUrl descriptionUrl (s.Element(ns + "controlURL") |> fun e -> if isNull e then "" else e.Value))
                            
                            match control with
                            | Some c ->
                                let rendering = 
                                    rc |> Option.bind (fun r -> resolveDeviceUrl baseUrl (r.Element(ns + "controlURL") |> fun e -> if isNull e then "" else e.Value))
                                    |> Option.orElse (rc |> Option.bind (fun r -> resolveDeviceUrl descriptionUrl (r.Element(ns + "controlURL") |> fun e -> if isNull e then "" else e.Value)))
                                
                                devices.[udn] <- { Id = udn; Name = name; DescriptionUrl = descriptionUrl; ControlUrl = c; RenderingControlUrl = rendering }
                            | None -> ()
                        | None -> ()
                | None -> ()
            with _ -> ()
        }

    let startServer (filePath: string) (subtitlePath: string option) =
        lock serverLock (fun () ->
            let normalizedSub = 
                match subtitlePath with
                | Some p when not (String.IsNullOrWhiteSpace(p)) && File.Exists(p) -> Some p
                | _ -> None
            
            match listener, servedFilePath, servedSubtitlePath with
            | Some _, Some f, Some s when f = filePath && Some s = normalizedSub -> servedFileUri.Value
            | Some _, Some f, None when f = filePath && normalizedSub.IsNone -> servedFileUri.Value
            | _ ->
                stopServer()
                let address = getLanAddress() |> Option.defaultValue IPAddress.Loopback
                let port = getFreePort()
                let fileUri = Uri($"http://{address}:{port}/implay-cast/media/{Uri.EscapeDataString(Path.GetFileName(filePath))}")
                let subUri = normalizedSub |> Option.map (fun p -> Uri($"http://{address}:{port}/implay-cast/subtitle/{Uri.EscapeDataString(Path.GetFileName(p))}"))
                
                let l = new TcpListener(IPAddress.Any, port)
                l.Start()
                let cts = new CancellationTokenSource()
                listener <- Some l
                serverCts <- Some cts
                servedFilePath <- Some filePath
                servedSubtitlePath <- normalizedSub
                servedFileUri <- Some fileUri
                servedSubtitleUri <- subUri
                Task.Run((fun () -> serverLoopAsync l cts.Token :> Task), cts.Token) |> ignore
                fileUri
        )

    member _.ActiveDevice = activeDevice
    member _.ServedFileUri = servedFileUri
    member _.Devices = devices.Values

    member _.DiscoverAsync(timeout: TimeSpan, ct: CancellationToken) =
        task {
            devices.Clear()
            use linked = CancellationTokenSource.CreateLinkedTokenSource(ct)
            linked.CancelAfter(timeout)
            let descriptionTasks = List<Task>()
            let seenLocations = HashSet<string>(StringComparer.OrdinalIgnoreCase)
            
            use udp = new UdpClient(AddressFamily.InterNetwork)
            udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true)
            udp.Client.ReceiveTimeout <- 500
            
            let endpoint = IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900)
            let search = String.Join("\r\n", "M-SEARCH * HTTP/1.1", "HOST: 239.255.255.250:1900", "MAN: \"ssdp:discover\"", "MX: 2", "ST: urn:schemas-upnp-org:device:MediaRenderer:1", "", "")
            let bytes = Encoding.ASCII.GetBytes(search)
            
            for i in 0 .. 2 do
                let! _ = udp.SendAsync(bytes, bytes.Length, endpoint)
                ()
            
            while not linked.IsCancellationRequested do
                try
                    let! result = udp.ReceiveAsync(linked.Token)
                    let response = Encoding.UTF8.GetString(result.Buffer)
                    let headers = parseHeaders response
                    match headers.TryGetValue("location") with
                    | true, location ->
                        match Uri.TryCreate(location, UriKind.Absolute) with
                        | true, url when isHttpUri url && seenLocations.Add(url.ToString()) ->
                            descriptionTasks.Add(addDeviceFromDescriptionAsync url ct)
                        | _ -> ()
                    | _ -> ()
                with
                | :? OperationCanceledException -> ()
                | _ -> ()
            
            try
                let! _ = Task.WhenAll(descriptionTasks).WaitAsync(TimeSpan.FromSeconds(3.0), ct)
                ()
            with _ -> ()
            
            return devices.Values.OrderBy((fun d -> d.Name), StringComparer.OrdinalIgnoreCase).ToList() :> IReadOnlyList<DlnaCastDevice>
        }

    member self.CastAsync(device: DlnaCastDevice, filePath: string, subtitlePath: string option, startPosition: TimeSpan, volume: int, ct: CancellationToken) =
        task {
            let mediaUri = startServer filePath subtitlePath
            let didl = buildDidlLite filePath mediaUri servedSubtitleUri
            
            try
                let! _ = setTransportUriAsync device mediaUri didl ct
                ()
            with _ ->
                let! _ = setTransportUriAsync device mediaUri "" ct
                ()
            
            if device.RenderingControlUrl.IsSome then
                try let! _ = setVolumeAsync device volume ct in () with _ -> ()
            
            if startPosition > TimeSpan.Zero then
                try let! _ = seekAsync device startPosition ct in () with _ -> ()
            
            let! _ = playAsync device ct
            activeDevice <- Some device
        }

    member _.PlayAsync(ct) = match activeDevice with Some d -> playAsync d ct | None -> Task.FromResult("")
    member _.PauseAsync(ct) = match activeDevice with Some d -> pauseAsync d ct | None -> Task.FromResult("")
    member _.StopAsync(ct) = match activeDevice with Some d -> stopAsync d ct | None -> Task.FromResult("")
    member _.SetVolumeAsync(v, ct) = match activeDevice with Some d -> setVolumeAsync d v ct | None -> Task.FromResult("")
    member _.SeekAsync(p, ct) = match activeDevice with Some d -> seekAsync d p ct | None -> Task.FromResult("")
    member _.GetPositionInfoAsync(ct) = match activeDevice with Some d -> getPositionInfoAsync d ct | None -> Task.FromResult(None)
    member _.GetIsPlayingAsync(ct) = match activeDevice with Some d -> getIsPlayingAsync d ct | None -> Task.FromResult(None)

    member _.StopServer() = stopServer()

    interface IDisposable with
        member _.Dispose() = stopServer()
