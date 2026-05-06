namespace ImPlay.Core

open System
open System.Runtime.InteropServices

[<Struct; StructLayout(LayoutKind.Sequential)>]
type MpvEvent =
    val event_id: MpvEventId
    val error: int
    val reply_userdata: uint64
    val data: IntPtr

and MpvEventId =
    | None = 0
    | Shutdown = 1
    | LogMessage = 2
    | GetPropertyReply = 3
    | SetPropertyReply = 4
    | CommandReply = 5
    | StartFile = 6
    | EndFile = 7
    | FileLoaded = 8
    | TracksChanged = 9
    | TrackSwitched = 10
    | Idle = 11
    | Pause = 12
    | Unpause = 13
    | Tick = 14
    | ScriptInputDispatch = 15
    | ClientMessage = 16
    | VideoReconfig = 17
    | AudioReconfig = 18
    | MetadataUpdate = 19
    | Seek = 20
    | PlaybackRestart = 21
    | PropertyChange = 22
    | ChapterChange = 23
    | Hook = 24

type MpvFormat =
    | None = 0
    | String = 1
    | OsdString = 2
    | Flag = 3
    | Int64 = 4
    | Double = 5
    | Node = 6
    | NodeArray = 7
    | NodeMap = 8
    | ByteArray = 9

[<Struct; StructLayout(LayoutKind.Sequential)>]
type MpvEventProperty =
    val name: IntPtr
    val format: MpvFormat
    val data: IntPtr

[<Struct; StructLayout(LayoutKind.Sequential)>]
type MpvEventEndFile =
    val reason: int
    val error: int
    val playlist_entry_id: int64
    val playlist_insert_id: int64
    val playlist_insert_num_entries: int

[<Struct; StructLayout(LayoutKind.Sequential)>]
type MpvEventLogMessage =
    val prefix: IntPtr
    val level: IntPtr
    val text: IntPtr
    val log_level: int

type MpvRenderParamType =
    | Invalid = 0
    | ApiType = 1
    | OpenGlInitParams = 2
    | OpenGlFbo = 3
    | FlipY = 4
    | ExternalGlCtx = 5
    | SwSize = 6
    | SwFormat = 7
    | SwStride = 8
    | SwPointer = 9

[<Struct; StructLayout(LayoutKind.Sequential)>]
type MpvRenderParam =
    val type_: MpvRenderParamType
    val data: IntPtr
    new(t, d) = { type_ = t; data = d }

[<Struct; StructLayout(LayoutKind.Sequential)>]
type MpvOpenGlInitParams =
    val mutable get_proc_address: IntPtr
    val mutable get_proc_address_ctx: IntPtr

[<Struct; StructLayout(LayoutKind.Sequential)>]
type MpvOpenGlFbo =
    val mutable fbo: int
    val mutable w: int
    val mutable h: int
    val mutable internal_format: int
    new(f, width, height, fmt) = { fbo = f; w = width; h = height; internal_format = fmt }

type MpvRenderUpdateCallback = delegate of IntPtr -> unit
type MpvOpenGlGetProcAddress = delegate of IntPtr * IntPtr -> IntPtr

module MpvNative =
    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern IntPtr mpv_create()

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_initialize(IntPtr ctx)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern void mpv_terminate_destroy(IntPtr ctx)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_set_option_string(IntPtr ctx, [<MarshalAs(UnmanagedType.LPStr)>] string name, [<MarshalAs(UnmanagedType.LPStr)>] string value)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_command(IntPtr ctx, IntPtr args)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_set_property(IntPtr ctx, [<MarshalAs(UnmanagedType.LPStr)>] string name, MpvFormat format, IntPtr data)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_get_property(IntPtr ctx, [<MarshalAs(UnmanagedType.LPStr)>] string name, MpvFormat format, IntPtr data)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_set_property_string(IntPtr ctx, [<MarshalAs(UnmanagedType.LPStr)>] string name, [<MarshalAs(UnmanagedType.LPStr)>] string value)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern IntPtr mpv_get_property_string(IntPtr ctx, [<MarshalAs(UnmanagedType.LPStr)>] string name)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_observe_property(IntPtr ctx, uint64 reply_userdata, [<MarshalAs(UnmanagedType.LPStr)>] string name, MpvFormat format)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern IntPtr mpv_wait_event(IntPtr ctx, double timeout)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_request_log_messages(IntPtr ctx, [<MarshalAs(UnmanagedType.LPStr)>] string min_level)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern void mpv_free(IntPtr data)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_render_context_create(IntPtr& res, IntPtr ctx, IntPtr parameters)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern void mpv_render_context_set_update_callback(IntPtr ctx, MpvRenderUpdateCallback callback, IntPtr callback_ctx)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern uint64 mpv_render_context_update(IntPtr ctx)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern int mpv_render_context_render(IntPtr ctx, IntPtr parameters)

    [<DllImport("libmpv-2.dll", CallingConvention = CallingConvention.Cdecl)>]
    extern void mpv_render_context_free(IntPtr ctx)
