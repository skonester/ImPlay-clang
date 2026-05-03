namespace ImPlay.Fs

open System
open System.IO
open System.IO.Pipes
open System.Text
open System.Collections.Generic

module Ipc =
    let getIpcPipeName () = "mpv-ipc-socket"

    let sendIpc (paths: string list) =
        try
            let pipeName = getIpcPipeName()
            use client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out)
            client.Connect(100) // 100ms timeout
            
            for path in paths do
                let cmd = sprintf "{\"command\": [\"loadfile\", \"%s\", \"append-play\"]}\n" (path.Replace("\\", "\\\\"))
                let bytes = Encoding.UTF8.GetBytes(cmd)
                client.Write(bytes, 0, bytes.Length)
            
            client.Flush()
            true
        with _ ->
            false

    // This is handled by libmpv if we set "input-ipc-server"
    // But for a pure F# implementation without libmpv (e.g. just to check if it's running), 
    // we use the client to probe.
