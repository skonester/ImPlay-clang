namespace ImPlay.Fs

open System

module Cli =

    type ParsedArgs = {
        Options: (string * string) list
        Paths: string list
        PlaylistFiles: string list
        SubtitleFiles: string list
        HelpRequested: bool
    }

    let empty = {
        Options = []
        Paths = []
        PlaylistFiles = []
        SubtitleFiles = []
        HelpRequested = false
    }

    let private addOption key value parsed =
        { parsed with Options = parsed.Options @ [ key, value ] }

    let private addPath path parsed =
        { parsed with Paths = parsed.Paths @ [ path ] }

    let private addPlaylist path parsed =
        { parsed with PlaylistFiles = parsed.PlaylistFiles @ [ path ] }

    let private addSubtitle path parsed =
        { parsed with SubtitleFiles = parsed.SubtitleFiles @ [ path ] }

    let private applyOption key value parsed =
        match key with
        | "help" | "h" -> { parsed with HelpRequested = true }
        | "playlist" -> addPlaylist value parsed
        | "sub-file" -> addSubtitle value parsed
        | _ -> addOption key value parsed

    let parse (args: string list) =
        let rec loop optEnd parsed remaining =
            match remaining with
            | [] -> parsed
            | arg :: rest when String.IsNullOrWhiteSpace(arg) ->
                loop optEnd parsed rest
            | "--" :: rest ->
                loop true parsed rest
            | "-" :: rest ->
                loop optEnd (addPath "-" parsed) rest
            | arg :: rest when not optEnd && arg.StartsWith("--") ->
                let body = arg.Substring(2)
                if body.StartsWith("no-") && body.Length > 3 then
                    let key = body.Substring(3)
                    loop optEnd (applyOption key "no" parsed) rest
                else
                    match body.IndexOf('=') with
                    | -1 -> loop optEnd (applyOption body "yes" parsed) rest
                    | idx ->
                        let key = body.Substring(0, idx)
                        let value = body.Substring(idx + 1)
                        loop optEnd (applyOption key value parsed) rest
            | arg :: rest when not optEnd && arg.StartsWith("-") && arg.Length > 1 ->
                let body = arg.Substring(1)
                match body.IndexOf('=') with
                | -1 -> loop optEnd (applyOption body "yes" parsed) rest
                | idx ->
                    let key = body.Substring(0, idx)
                    let value = body.Substring(idx + 1)
                    loop optEnd (applyOption key value parsed) rest
            | arg :: rest ->
                loop optEnd (addPath arg parsed) rest

        loop false empty args
