using System.Collections.Concurrent;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Security;
using System.Text;
using System.Xml.Linq;

namespace ImPlay.Core.Services;

public sealed record DlnaCastDevice(
    string Id,
    string Name,
    Uri DescriptionUrl,
    Uri ControlUrl,
    Uri? RenderingControlUrl);

public sealed record DlnaPositionInfo(TimeSpan Position, TimeSpan Duration);

public sealed class DlnaCastService : IDisposable
{
    private const string AvTransportService = "urn:schemas-upnp-org:service:AVTransport:1";
    private const string RenderingControlService = "urn:schemas-upnp-org:service:RenderingControl:1";

    private static readonly HttpClient Http = new()
    {
        Timeout = TimeSpan.FromSeconds(4)
    };

    private readonly ConcurrentDictionary<string, DlnaCastDevice> _devices = new();
    private readonly object _serverLock = new();
    private CancellationTokenSource? _serverCts;
    private TcpListener? _listener;
    private string? _servedFilePath;
    private string? _servedSubtitlePath;
    private Uri? _servedFileUri;
    private Uri? _servedSubtitleUri;
    private DlnaCastDevice? _activeDevice;

    public DlnaCastDevice? ActiveDevice => _activeDevice;
    public Uri? ServedFileUri => _servedFileUri;

    public async Task<IReadOnlyList<DlnaCastDevice>> DiscoverAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        _devices.Clear();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        linked.CancelAfter(timeout);
        var descriptionTasks = new List<Task>();
        var seenLocations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
        udp.Client.ReceiveTimeout = 500;

        var endpoint = new IPEndPoint(IPAddress.Parse("239.255.255.250"), 1900);
        var search = string.Join("\r\n",
            "M-SEARCH * HTTP/1.1",
            "HOST: 239.255.255.250:1900",
            "MAN: \"ssdp:discover\"",
            "MX: 2",
            "ST: urn:schemas-upnp-org:device:MediaRenderer:1",
            "", "");
        var bytes = Encoding.ASCII.GetBytes(search);

        for (var i = 0; i < 3; i++)
            await udp.SendAsync(bytes, bytes.Length, endpoint).ConfigureAwait(false);

        while (!linked.IsCancellationRequested)
        {
            try
            {
                var result = await udp.ReceiveAsync(linked.Token).ConfigureAwait(false);
                var response = Encoding.UTF8.GetString(result.Buffer);
                var headers = ParseHeaders(response);
                if (!headers.TryGetValue("location", out var location) ||
                    !Uri.TryCreate(location, UriKind.Absolute, out var descriptionUrl))
                    continue;

                if (!IsHttpUri(descriptionUrl) || !seenLocations.Add(descriptionUrl.ToString()))
                    continue;

                descriptionTasks.Add(AddDeviceFromDescriptionAsync(descriptionUrl, cancellationToken));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                // Ignore malformed devices and keep listening during the discovery window.
            }
        }

        try
        {
            await Task.WhenAll(descriptionTasks).WaitAsync(TimeSpan.FromSeconds(3), cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Return whatever discovery completed within the window.
        }
        return _devices.Values.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public async Task CastAsync(DlnaCastDevice device, string filePath, string? subtitlePath, TimeSpan startPosition, int volume, CancellationToken cancellationToken = default)
    {
        var mediaUri = StartServer(filePath, subtitlePath);
        var didl = BuildDidlLite(filePath, mediaUri, _servedSubtitleUri);

        try
        {
            await SetTransportUriAsync(device, mediaUri, didl, cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            // Several renderers reject otherwise valid DIDL-Lite metadata. Retry with
            // an empty metadata field before reporting the cast as failed.
            await SetTransportUriAsync(device, mediaUri, "", cancellationToken).ConfigureAwait(false);
        }

        if (device.RenderingControlUrl is not null)
            await TrySoapAsync(() => SetVolumeAsync(device, volume, cancellationToken)).ConfigureAwait(false);

        if (startPosition > TimeSpan.Zero)
            await TrySoapAsync(() => SeekAsync(device, startPosition, cancellationToken)).ConfigureAwait(false);

        await PlayAsync(device, cancellationToken).ConfigureAwait(false);
        _activeDevice = device;
    }

    public Task PlayAsync(CancellationToken cancellationToken = default)
        => _activeDevice is null ? Task.CompletedTask : PlayAsync(_activeDevice, cancellationToken);

    public Task PauseAsync(CancellationToken cancellationToken = default)
        => _activeDevice is null ? Task.CompletedTask : PauseAsync(_activeDevice, cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken = default)
        => _activeDevice is null ? Task.CompletedTask : StopAsync(_activeDevice, cancellationToken);

    public Task SetVolumeAsync(int volume, CancellationToken cancellationToken = default)
        => _activeDevice is null ? Task.CompletedTask : SetVolumeAsync(_activeDevice, volume, cancellationToken);

    public Task SeekAsync(TimeSpan position, CancellationToken cancellationToken = default)
        => _activeDevice is null ? Task.CompletedTask : SeekAsync(_activeDevice, position, cancellationToken);

    public Task<DlnaPositionInfo?> GetPositionInfoAsync(CancellationToken cancellationToken = default)
        => _activeDevice is null ? Task.FromResult<DlnaPositionInfo?>(null) : GetPositionInfoAsync(_activeDevice, cancellationToken);

    public Task<bool?> GetIsPlayingAsync(CancellationToken cancellationToken = default)
        => _activeDevice is null ? Task.FromResult<bool?>(null) : GetIsPlayingAsync(_activeDevice, cancellationToken);

    public void StopServer()
    {
        lock (_serverLock)
        {
            _activeDevice = null;
            _servedFilePath = null;
            _servedSubtitlePath = null;
            _servedFileUri = null;
            _servedSubtitleUri = null;
            _serverCts?.Cancel();
            _listener?.Stop();
            _listener = null;
            _serverCts?.Dispose();
            _serverCts = null;
        }
    }

    public void Dispose()
    {
        StopServer();
    }

    private static Task PlayAsync(DlnaCastDevice device, CancellationToken cancellationToken)
        => SoapAsync(device.ControlUrl, AvTransportService, "Play",
            "<InstanceID>0</InstanceID><Speed>1</Speed>", cancellationToken);

    private static Task PauseAsync(DlnaCastDevice device, CancellationToken cancellationToken)
        => SoapAsync(device.ControlUrl, AvTransportService, "Pause",
            "<InstanceID>0</InstanceID>", cancellationToken);

    private static Task StopAsync(DlnaCastDevice device, CancellationToken cancellationToken)
        => SoapAsync(device.ControlUrl, AvTransportService, "Stop",
            "<InstanceID>0</InstanceID>", cancellationToken);

    private static Task SetTransportUriAsync(DlnaCastDevice device, Uri mediaUri, string metadata, CancellationToken cancellationToken)
        => SoapAsync(device.ControlUrl, AvTransportService, "SetAVTransportURI",
            $"""
            <InstanceID>0</InstanceID>
            <CurrentURI>{SecurityElement.Escape(mediaUri.ToString())}</CurrentURI>
            <CurrentURIMetaData>{SecurityElement.Escape(metadata)}</CurrentURIMetaData>
            """, cancellationToken);

    private static async Task<DlnaPositionInfo?> GetPositionInfoAsync(DlnaCastDevice device, CancellationToken cancellationToken)
    {
        var response = await SoapAsync(device.ControlUrl, AvTransportService, "GetPositionInfo",
            "<InstanceID>0</InstanceID>", cancellationToken).ConfigureAwait(false);
        var position = ReadSoapTime(response, "RelTime");
        var duration = ReadSoapTime(response, "TrackDuration");
        return position is null && duration is null
            ? null
            : new DlnaPositionInfo(position ?? TimeSpan.Zero, duration ?? TimeSpan.Zero);
    }

    private static async Task<bool?> GetIsPlayingAsync(DlnaCastDevice device, CancellationToken cancellationToken)
    {
        var response = await SoapAsync(device.ControlUrl, AvTransportService, "GetTransportInfo",
            "<InstanceID>0</InstanceID>", cancellationToken).ConfigureAwait(false);
        var state = ReadSoapString(response, "CurrentTransportState");
        if (string.IsNullOrWhiteSpace(state)) return null;
        return state.Equals("PLAYING", StringComparison.OrdinalIgnoreCase) ||
               state.Equals("TRANSITIONING", StringComparison.OrdinalIgnoreCase);
    }

    private static Task SeekAsync(DlnaCastDevice device, TimeSpan position, CancellationToken cancellationToken)
        => SoapAsync(device.ControlUrl, AvTransportService, "Seek",
            $"<InstanceID>0</InstanceID><Unit>REL_TIME</Unit><Target>{FormatDlnaTime(position)}</Target>", cancellationToken);

    private static Task SetVolumeAsync(DlnaCastDevice device, int volume, CancellationToken cancellationToken)
        => device.RenderingControlUrl is null
            ? Task.CompletedTask
            : SoapAsync(device.RenderingControlUrl, RenderingControlService, "SetVolume",
                $"<InstanceID>0</InstanceID><Channel>Master</Channel><DesiredVolume>{Math.Clamp(volume, 0, 100)}</DesiredVolume>",
                cancellationToken);

    private Uri StartServer(string filePath, string? subtitlePath)
    {
        lock (_serverLock)
        {
            var normalizedSubtitle = !string.IsNullOrWhiteSpace(subtitlePath) && File.Exists(subtitlePath)
                ? subtitlePath
                : null;

            if (_listener is not null &&
                string.Equals(_servedFilePath, filePath, StringComparison.Ordinal) &&
                string.Equals(_servedSubtitlePath, normalizedSubtitle, StringComparison.Ordinal))
                return _servedFileUri!;

            StopServer();

            var address = GetLanAddress() ?? IPAddress.Loopback;
            var port = GetFreePort();
            var publicUri = new Uri($"http://{address}:{port}/implay-cast/media/{Uri.EscapeDataString(Path.GetFileName(filePath))}");
            var subtitleUri = normalizedSubtitle is null
                ? null
                : new Uri($"http://{address}:{port}/implay-cast/subtitle/{Uri.EscapeDataString(Path.GetFileName(normalizedSubtitle))}");

            _listener = new TcpListener(IPAddress.Any, port);
            _listener.Start();
            _serverCts = new CancellationTokenSource();
            _servedFilePath = filePath;
            _servedSubtitlePath = normalizedSubtitle;
            _servedFileUri = publicUri;
            _servedSubtitleUri = subtitleUri;
            _ = Task.Run(() => ServerLoopAsync(_listener, _serverCts.Token));
            return publicUri;
        }
    }

    private async Task ServerLoopAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                _ = Task.Run(() => ServeClientAsync(client, cancellationToken), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                if (!cancellationToken.IsCancellationRequested)
                    await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ServeClientAsync(TcpClient client, CancellationToken cancellationToken)
    {
        using var _ = client;
        using var network = client.GetStream();

        var request = await ReadHttpRequestAsync(network, cancellationToken).ConfigureAwait(false);
        if (request is null) return;

        var requestPath = request.Value.Path;
        var path = requestPath.Contains("/subtitle/", StringComparison.OrdinalIgnoreCase)
            ? _servedSubtitlePath
            : _servedFilePath;
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            await WriteResponseHeadersAsync(network, 404, "Not Found", "text/plain", 0, null, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            var info = new FileInfo(path);
            var start = 0L;
            var end = info.Length - 1;
            request.Value.Headers.TryGetValue("range", out var range);
            var status = 200;
            string? contentRange = null;
            if (!string.IsNullOrWhiteSpace(range) && range.StartsWith("bytes=", StringComparison.OrdinalIgnoreCase))
            {
                var parts = range["bytes=".Length..].Split('-', 2);
                if (long.TryParse(parts[0], out var parsedStart))
                    start = Math.Clamp(parsedStart, 0, end);
                if (parts.Length > 1 && long.TryParse(parts[1], out var parsedEnd))
                    end = Math.Clamp(parsedEnd, start, info.Length - 1);
                status = 206;
                contentRange = $"bytes {start}-{end}/{info.Length}";
            }

            var length = end - start + 1;
            await WriteResponseHeadersAsync(network, status, status == 206 ? "Partial Content" : "OK",
                GetMimeType(path), length, contentRange, cancellationToken).ConfigureAwait(false);

            if (string.Equals(request.Value.Method, "HEAD", StringComparison.OrdinalIgnoreCase))
                return;

            await using var stream = File.OpenRead(path);
            stream.Seek(start, SeekOrigin.Begin);
            var buffer = new byte[128 * 1024];
            var remaining = length;
            while (remaining > 0 && !cancellationToken.IsCancellationRequested)
            {
                var read = await stream.ReadAsync(buffer.AsMemory(0, (int)Math.Min(buffer.Length, remaining)), cancellationToken)
                    .ConfigureAwait(false);
                if (read == 0) break;
                await network.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                remaining -= read;
            }
        }
        catch
        {
            // Renderers often close probe/range connections early. Treat that as a normal client disconnect.
        }
    }

    private static async Task TrySoapAsync(Func<Task> action)
    {
        try
        {
            await action().ConfigureAwait(false);
        }
        catch
        {
            // Some renderers do not support optional controls such as volume or seek-before-play.
        }
    }

    private static async Task<(string Method, string Path, Dictionary<string, string> Headers)?> ReadHttpRequestAsync(
        NetworkStream stream,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
        var total = 0;
        while (total < buffer.Length)
        {
            var read = await stream.ReadAsync(buffer.AsMemory(total, buffer.Length - total), cancellationToken).ConfigureAwait(false);
            if (read == 0) return null;
            total += read;
            var text = Encoding.ASCII.GetString(buffer, 0, total);
            var end = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
            if (end < 0) continue;

            var lines = text[..end].Split("\r\n", StringSplitOptions.None);
            var requestLine = lines.FirstOrDefault()?.Split(' ', 3);
            if (requestLine is null || requestLine.Length < 2) return null;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in lines.Skip(1))
            {
                var idx = line.IndexOf(':');
                if (idx <= 0) continue;
                headers[line[..idx].Trim()] = line[(idx + 1)..].Trim();
            }

            return (requestLine[0], requestLine[1], headers);
        }
        return null;
    }

    private static async Task WriteResponseHeadersAsync(
        NetworkStream stream,
        int status,
        string reason,
        string contentType,
        long contentLength,
        string? contentRange,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"HTTP/1.1 {status} {reason}\r\n")
            .Append(CultureInfo.InvariantCulture, $"Content-Type: {contentType}\r\n")
            .Append(CultureInfo.InvariantCulture, $"Content-Length: {contentLength}\r\n")
            .Append("Accept-Ranges: bytes\r\n")
            .Append("Connection: close\r\n");
        if (!string.IsNullOrWhiteSpace(contentRange))
            builder.Append(CultureInfo.InvariantCulture, $"Content-Range: {contentRange}\r\n");
        builder.Append("\r\n");

        var bytes = Encoding.ASCII.GetBytes(builder.ToString());
        await stream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
    }

    private async Task AddDeviceFromDescriptionAsync(Uri descriptionUrl, CancellationToken cancellationToken)
    {
        try
        {
            var xml = await Http.GetStringAsync(descriptionUrl, cancellationToken).ConfigureAwait(false);
            var doc = XDocument.Parse(xml);
            XNamespace ns = doc.Root?.Name.Namespace ?? "";
            var baseUrl = ResolveBaseUrl(descriptionUrl, doc.Root?.Element(ns + "URLBase")?.Value);
            var device = doc.Descendants(ns + "device").FirstOrDefault(d =>
                d.Elements(ns + "deviceType").Any(e => e.Value.Contains("MediaRenderer", StringComparison.OrdinalIgnoreCase)));
            if (device is null) return;

            var name = device.Element(ns + "friendlyName")?.Value.Trim();
            if (string.IsNullOrWhiteSpace(name)) return;

            var udn = device.Element(ns + "UDN")?.Value.Trim() ?? descriptionUrl.ToString();
            var services = device.Descendants(ns + "service").ToList();
            var av = services.FirstOrDefault(s => s.Element(ns + "serviceType")?.Value.Trim() == AvTransportService);
            if (av is null) return;

            var rc = services.FirstOrDefault(s => s.Element(ns + "serviceType")?.Value.Trim() == RenderingControlService);
            var control = ResolveDeviceUrl(baseUrl, av.Element(ns + "controlURL")?.Value) ??
                ResolveDeviceUrl(descriptionUrl, av.Element(ns + "controlURL")?.Value);
            if (control is null) return;
            var rendering = ResolveDeviceUrl(baseUrl, rc?.Element(ns + "controlURL")?.Value) ??
                ResolveDeviceUrl(descriptionUrl, rc?.Element(ns + "controlURL")?.Value);

            _devices[udn] = new DlnaCastDevice(udn, name, descriptionUrl, control, rendering);
        }
        catch
        {
            // Discovery should be best-effort; one bad device must not cancel the scan.
        }
    }

    private static async Task<string> SoapAsync(Uri controlUrl, string serviceType, string action, string body, CancellationToken cancellationToken)
    {
        var envelope =
            $$"""
            <?xml version="1.0"?>
            <s:Envelope xmlns:s="http://schemas.xmlsoap.org/soap/envelope/" s:encodingStyle="http://schemas.xmlsoap.org/soap/encoding/">
              <s:Body>
                <u:{{action}} xmlns:u="{{serviceType}}">
                  {{body}}
                </u:{{action}}>
              </s:Body>
            </s:Envelope>
            """;
        using var request = new HttpRequestMessage(HttpMethod.Post, controlUrl);
        request.Headers.TryAddWithoutValidation("SOAPACTION", $"\"{serviceType}#{action}\"");
        request.Content = new StringContent(envelope, Encoding.UTF8, "text/xml");
        using var response = await Http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (!response.IsSuccessStatusCode)
        {
            var detail = ExtractSoapFault(text);
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(detail)
                ? $"{action} failed: HTTP {(int)response.StatusCode} {response.ReasonPhrase}"
                : $"{action} failed: {detail}");
        }
        return text;
    }

    private static string? ExtractSoapFault(string xml)
    {
        if (string.IsNullOrWhiteSpace(xml)) return null;
        try
        {
            var doc = XDocument.Parse(xml);
            var errorDescription = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("errorDescription", StringComparison.OrdinalIgnoreCase))
                ?.Value;
            if (!string.IsNullOrWhiteSpace(errorDescription))
                return errorDescription.Trim();

            var faultString = doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals("faultstring", StringComparison.OrdinalIgnoreCase))
                ?.Value;
            return string.IsNullOrWhiteSpace(faultString) ? null : faultString.Trim();
        }
        catch
        {
            return xml.Length > 180 ? $"{xml[..180]}..." : xml;
        }
    }

    private static string? ReadSoapString(string xml, string elementName)
    {
        try
        {
            var doc = XDocument.Parse(xml);
            return doc.Descendants()
                .FirstOrDefault(e => e.Name.LocalName.Equals(elementName, StringComparison.OrdinalIgnoreCase))
                ?.Value
                ?.Trim();
        }
        catch
        {
            return null;
        }
    }

    private static TimeSpan? ReadSoapTime(string xml, string elementName)
    {
        var value = ReadSoapString(xml, elementName);
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("NOT_IMPLEMENTED", StringComparison.OrdinalIgnoreCase))
            return null;
        return TimeSpan.TryParse(value, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static Uri ResolveBaseUrl(Uri descriptionUrl, string? urlBase)
    {
        if (Uri.TryCreate(urlBase, UriKind.Absolute, out var parsed) && IsHttpUri(parsed))
            return parsed;
        return descriptionUrl;
    }

    private static Uri? ResolveDeviceUrl(Uri baseUrl, string? path)
    {
        if (string.IsNullOrWhiteSpace(path)) return null;
        var trimmed = path.Trim();
        var resolved = Uri.TryCreate(trimmed, UriKind.Absolute, out var absolute)
            ? absolute
            : new Uri(baseUrl, trimmed);

        if (!IsHttpUri(resolved) && !string.IsNullOrWhiteSpace(resolved.AbsolutePath))
            resolved = new Uri(baseUrl, resolved.PathAndQuery);

        return IsHttpUri(resolved) ? resolved : null;
    }

    private static bool IsHttpUri(Uri uri)
        => uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
           uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);

    private static Dictionary<string, string> ParseHeaders(string response)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in response.Split(["\r\n", "\n"], StringSplitOptions.RemoveEmptyEntries).Skip(1))
        {
            var idx = line.IndexOf(':');
            if (idx <= 0) continue;
            headers[line[..idx].Trim()] = line[(idx + 1)..].Trim();
        }
        return headers;
    }

    private static string BuildDidlLite(string filePath, Uri mediaUri, Uri? subtitleUri)
    {
        var title = SecurityElement.Escape(Path.GetFileNameWithoutExtension(filePath));
        var mime = GetMimeType(filePath);
        var subtitleMetadata = subtitleUri is null
            ? ""
            : $"""
              
                            <res protocolInfo="http-get:*:text/srt:*">{SecurityElement.Escape(subtitleUri.ToString())}</res>
                            <res protocolInfo="http-get:*:application/x-subrip:*">{SecurityElement.Escape(subtitleUri.ToString())}</res>
                            <upnp:subtitle>{SecurityElement.Escape(subtitleUri.ToString())}</upnp:subtitle>
                            <sec:CaptionInfo sec:type="srt">{SecurityElement.Escape(subtitleUri.ToString())}</sec:CaptionInfo>
                            <sec:CaptionInfoEx sec:type="srt">{SecurityElement.Escape(subtitleUri.ToString())}</sec:CaptionInfoEx>
              """;
        return
            $$"""
            <DIDL-Lite xmlns="urn:schemas-upnp-org:metadata-1-0/DIDL-Lite/" xmlns:dc="http://purl.org/dc/elements/1.1/" xmlns:upnp="urn:schemas-upnp-org:metadata-1-0/upnp/" xmlns:sec="http://www.sec.co.kr/">
              <item id="0" parentID="0" restricted="1">
                <dc:title>{{title}}</dc:title>
                <upnp:class>{{(mime.StartsWith("audio/", StringComparison.OrdinalIgnoreCase) ? "object.item.audioItem.musicTrack" : "object.item.videoItem.movie")}}</upnp:class>
                <res protocolInfo="http-get:*:{{mime}}:*">{{SecurityElement.Escape(mediaUri.ToString())}}</res>
                {{subtitleMetadata}}
              </item>
            </DIDL-Lite>
            """;
    }

    private static string GetMimeType(string path)
        => Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".mp4" or ".m4v" => "video/mp4",
            ".mkv" => "video/x-matroska",
            ".webm" => "video/webm",
            ".avi" => "video/x-msvideo",
            ".mov" => "video/quicktime",
            ".mp3" => "audio/mpeg",
            ".flac" => "audio/flac",
            ".ogg" or ".oga" => "audio/ogg",
            ".opus" => "audio/opus",
            ".wav" => "audio/wav",
            ".m4a" or ".aac" => "audio/aac",
            ".srt" => "application/x-subrip",
            ".vtt" => "text/vtt",
            _ => "application/octet-stream"
        };

    private static string FormatDlnaTime(TimeSpan time)
        => $"{(int)time.TotalHours:D2}:{time.Minutes:D2}:{time.Seconds:D2}";

    private static int GetFreePort()
    {
        var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        return port;
    }

    private static IPAddress? GetLanAddress()
    {
        foreach (var ni in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (ni.OperationalStatus != OperationalStatus.Up ||
                ni.NetworkInterfaceType is NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                continue;

            foreach (var unicast in ni.GetIPProperties().UnicastAddresses)
            {
                if (unicast.Address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(unicast.Address))
                    return unicast.Address;
            }
        }
        return null;
    }
}
