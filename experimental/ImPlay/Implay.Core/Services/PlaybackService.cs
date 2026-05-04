using System.Reflection;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

using ImPlay.Core.Models;

namespace ImPlay.Core.Services;

public sealed class PlaybackService : IDisposable
{
    private const ulong MpvRenderUpdateFrame = 1UL << 0;

    private static readonly Dictionary<string, string> LanguageNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ["ara"] = "Arabic",
        ["chi"] = "Chinese",
        ["zho"] = "Chinese",
        ["dan"] = "Danish",
        ["dut"] = "Dutch",
        ["nld"] = "Dutch",
        ["eng"] = "English",
        ["fin"] = "Finnish",
        ["fre"] = "French",
        ["fra"] = "French",
        ["ger"] = "German",
        ["deu"] = "German",
        ["hin"] = "Hindi",
        ["ita"] = "Italian",
        ["jpn"] = "Japanese",
        ["kor"] = "Korean",
        ["nor"] = "Norwegian",
        ["pol"] = "Polish",
        ["por"] = "Portuguese",
        ["rus"] = "Russian",
        ["spa"] = "Spanish",
        ["swe"] = "Swedish",
        ["tur"] = "Turkish"
    };

    private readonly MediaState _state = new();
    private readonly Thread? _eventThread;
    private readonly object _stateLock = new();

    private readonly IntPtr _mpv;
    private bool _disposed;
    private bool _loop;
    private bool _pause = true;
    private long _trackRevision;

    private IntPtr _renderContext;
    private MpvNative.MpvRenderUpdateCallback? _renderUpdateCallback;
    private MpvNative.MpvOpenGlGetProcAddress? _getProcAddressCallback;
    private Func<string, IntPtr>? _getProcAddress;
    private Action? _requestRender;
    private bool _rendererReady;
    private int _lastRenderFramebuffer = -1;
    private int _lastRenderWidth;
    private int _lastRenderHeight;
    private IntPtr _nativeVideoWindow;

    public PlaybackService()
    {
        _mpv = MpvNative.mpv_create();

        try
        {
            MpvNative.mpv_set_option_string(_mpv, "config", "no");
            MpvNative.mpv_set_option_string(_mpv, "terminal", "no");
            MpvNative.mpv_set_option_string(_mpv, "idle", "yes");
            MpvNative.mpv_set_option_string(_mpv, "vo", "libmpv");
            MpvNative.mpv_set_option_string(_mpv, "ao", "wasapi");
            MpvNative.mpv_set_option_string(_mpv, "hwdec", "auto");
            MpvNative.mpv_set_option_string(_mpv, "osd-level", "1");

            MpvNative.mpv_initialize(_mpv);

            Observe("time-pos", MpvFormat.Double);
            Observe("duration", MpvFormat.Double);
            Observe("pause", MpvFormat.Flag);
            Observe("mute", MpvFormat.Flag);
            Observe("volume", MpvFormat.Double);
            Observe("speed", MpvFormat.Double);
            Observe("aid", MpvFormat.String);
            Observe("sid", MpvFormat.String);

            SetVolume(_state.Volume);

            _eventThread = new Thread(EventLoop)
            {
                IsBackground = true,
                Name = "ImPlay mpv events"
            };
            _eventThread.Start();
        }
        catch (DllNotFoundException)
        {
            InitializationError = "libmpv is not installed. Install mpv/libmpv and restart ImPlay.";
        }
        catch (Exception ex)
        {
            InitializationError = $"mpv could not be initialized: {ex.Message}";
        }
    }

    public string? InitializationError { get; }
    public string? CurrentFilePath => _state.FilePath;
    public TimeSpan Position => Snapshot().Position;
    public TimeSpan Duration => Snapshot().Duration;
    public bool IsPlaying => Snapshot().IsPlaying;
    public bool IsMuted => Snapshot().IsMuted;
    public int Volume => Snapshot().Volume;
    public float Speed => Snapshot().Speed;
    public bool IsLooping => _loop;
    public long TrackRevision => Interlocked.Read(ref _trackRevision);
    public IntPtr MpvHandle => _mpv;
    public IntPtr Context => _mpv;

    public event EventHandler<VideoFrameData>? VideoFrameCaptured;

    public event EventHandler<MediaState>? StateChanged;
    public event EventHandler? EndReached;
    public event EventHandler<string>? ErrorOccurred;

    public async Task OpenAsync(string filePath, TimeSpan resumePosition)
    {
        if (_mpv == IntPtr.Zero)
        {
            ErrorOccurred?.Invoke(this, InitializationError ?? "mpv is not available.");
            return;
        }

        if (!File.Exists(filePath))
        {
            ErrorOccurred?.Invoke(this, "The selected file does not exist.");
            return;
        }

        lock (_stateLock)
        {
            _state.FilePath = filePath;
            _state.Position = TimeSpan.Zero;
            _state.Duration = TimeSpan.Zero;
            _state.IsPlaying = true;
            _pause = false;
        }

        await Task.Run(() => Command("loadfile", filePath, "replace")).ConfigureAwait(false);

        if (resumePosition > TimeSpan.Zero)
        {
            _ = Task.Run(async () =>
            {
                await Task.Delay(250).ConfigureAwait(false);
                Seek(resumePosition);
            });
        }

        RaiseStateChanged();
    }

    public void Stop()
    {
        if (_mpv == IntPtr.Zero) return;
        Command("stop");
        lock (_stateLock)
        {
            _state.FilePath = null;
            _state.Position = TimeSpan.Zero;
            _state.Duration = TimeSpan.Zero;
            _state.IsPlaying = false;
            _pause = true;
        }
        RaiseStateChanged();
    }

    public void TogglePlayPause()
    {
        if (_mpv == IntPtr.Zero || string.IsNullOrWhiteSpace(_state.FilePath)) return;
        SetFlag("pause", !_pause);
    }

    public void Play()
    {
        if (_mpv == IntPtr.Zero || string.IsNullOrWhiteSpace(_state.FilePath)) return;
        SetFlag("pause", false);
    }

    public void Pause()
    {
        if (_mpv == IntPtr.Zero || string.IsNullOrWhiteSpace(_state.FilePath)) return;
        SetFlag("pause", true);
    }

    public void Seek(TimeSpan position)
    {
        if (_mpv == IntPtr.Zero || string.IsNullOrWhiteSpace(_state.FilePath)) return;
        SetDouble("time-pos", Math.Max(0, position.TotalSeconds));
        lock (_stateLock)
            _state.Position = position < TimeSpan.Zero ? TimeSpan.Zero : position;
        RaiseStateChanged();
    }

    public void SeekRelative(TimeSpan offset)
    {
        var seconds = offset.TotalSeconds.ToString("0.###", System.Globalization.CultureInfo.InvariantCulture);
        Command("seek", seconds, "relative", "exact");
    }

    public void StepFrame()
    {
        if (_mpv == IntPtr.Zero || string.IsNullOrWhiteSpace(_state.FilePath)) return;
        SetFlag("pause", true);
        Command("frame-step");
    }

    public void StepFrameBack()
    {
        if (_mpv == IntPtr.Zero || string.IsNullOrWhiteSpace(_state.FilePath)) return;
        SetFlag("pause", true);
        Command("frame-back-step");
    }

    public void SetVolume(int volume)
    {
        var clamped = Math.Clamp(volume, 0, 150);
        lock (_stateLock)
            _state.Volume = clamped;
        if (_mpv != IntPtr.Zero)
            SetDouble("volume", clamped);
        RaiseStateChanged();
    }

    public void ChangeVolume(int delta) => SetVolume(Volume + delta);

    private static bool IsSubtitleFile(string path)
    {
        var ext = Path.GetExtension(path).ToLowerInvariant();
        return ext is ".srt" or ".vtt" or ".ass" or ".ssa" or ".sub" or ".idx";
    }

    public void ToggleMute()
    {
        if (_mpv == IntPtr.Zero) return;
        SetFlag("mute", !IsMuted);
    }

    public void SetSpeed(float rate)
    {
        var clamped = Math.Clamp(rate, 0.25f, 4.0f);
        lock (_stateLock)
            _state.Speed = clamped;
        if (_mpv != IntPtr.Zero)
            SetDouble("speed", clamped);
        RaiseStateChanged();
    }

    // ── Video adjustments ──────────────────────────────────────────────────

    public void SetBrightness(int value)  => SetInt("brightness",  Math.Clamp(value, -100, 100));
    public void SetContrast(int value)    => SetInt("contrast",    Math.Clamp(value, -100, 100));
    public void SetSaturation(int value)  => SetInt("saturation",  Math.Clamp(value, -100, 100));
    public void SetVideoRotation(int deg) => SetInt("video-rotate", ((deg % 360) + 360) % 360);
    public void SetVideoZoom(double zoom) => SetDouble("video-zoom", Math.Clamp(zoom, -2.0, 2.0));
    public void SetVideoAspect(string aspect) => SetPropertyString("video-aspect-override", aspect);

    // ── Metadata / track info ──────────────────────────────────────────────

    /// <summary>Reads a metadata tag by key (e.g. "title", "artist", "album"). Returns null if unavailable.</summary>
    public string? GetMetadata(string key) => GetString($"metadata/by-key/{key}");

    /// <summary>Returns true if the loaded file has at least one video track with actual dimensions.</summary>
    public bool HasVideoTrack => GetTracks("video").Length > 0;

    public void ToggleLoop()
    {
        _loop = !_loop;
        SetOption("loop-file", _loop ? "inf" : "no");
        lock (_stateLock)
            _state.IsLooping = _loop;
        RaiseStateChanged();
    }

    public bool TakeSnapshot(string outputPath)
    {
        if (_mpv == IntPtr.Zero || string.IsNullOrWhiteSpace(_state.FilePath)) return false;
        return Command("screenshot-to-file", outputPath, "video") >= 0;
    }

    public void SetWindowHandle(IntPtr handle)
    {
        if (handle != IntPtr.Zero)
            SetPropertyString("wid", handle.ToInt64().ToString());
    }

    public bool UseNativeVideoWindow(IntPtr handle, bool preferVulkan)
    {
        if (_mpv == IntPtr.Zero || handle == IntPtr.Zero)
            return false;

        ShutdownRenderer();
        _nativeVideoWindow = handle;

        var vo = preferVulkan ? "gpu-next" : "gpu";
        SetOption("vo", vo);
        if (preferVulkan)
            SetOption("gpu-api", "vulkan");
        SetOption("hwdec", "auto-safe");
        SetOption("wid", handle.ToInt64().ToString(CultureInfo.InvariantCulture));

        StartupLogger.Log($"mpv native video window attached: hwnd=0x{handle.ToInt64():X}, vo={vo}, gpu-api={(preferVulkan ? "vulkan" : "auto")}.");
        return true;
    }

    public void DetachNativeVideoWindow(IntPtr handle)
    {
        if (_nativeVideoWindow != handle)
            return;

        _nativeVideoWindow = IntPtr.Zero;
        if (_mpv == IntPtr.Zero)
            return;

        SetOption("wid", "0");
        StartupLogger.Log($"mpv native video window detached: hwnd=0x{handle.ToInt64():X}.");
    }

    public void Load(string path)
    {
        if (IsSubtitleFile(path))
            LoadSubtitleFile(path);
        else
            _ = OpenAsync(path, TimeSpan.Zero);
    }

    public void LoadSubtitleFile(string path)
    {
        if (_mpv == IntPtr.Zero || string.IsNullOrWhiteSpace(_state.FilePath)) return;
        Command("sub-add", path, "cached");
        MarkTracksChanged();
    }

    public MediaTrack[] GetAudioTracks() => GetTracks("audio");

    public MediaTrack[] GetSubtitleTracks() =>
    [
        new(-1, "Off", CurrentSubtitleTrack < 0),
        .. GetTracks("sub")
    ];

    public int CurrentAudioTrack => GetSelectedTrackId("audio");
    public int CurrentSubtitleTrack => GetSelectedTrackId("sub");

    public void SetAudioTrack(int id)
    {
        if (id >= 0)
            SetPropertyString("aid", id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        MarkTracksChanged();
    }

    public void SetSubtitleTrack(int id)
    {
        if (id < 0)
            SetPropertyString("sid", "no");
        else
            SetPropertyString("sid", id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        MarkTracksChanged();
    }

    public long SubtitleDelayMs
    {
        get => (long)(GetDouble("sub-delay") * 1000);
        set => SetDouble("sub-delay", value / 1000.0);
    }

    public void CycleAudioTrack()
    {
        var tracks = GetAudioTracks();
        if (tracks.Length == 0) return;

        var current = CurrentAudioTrack;
        var idx = Array.FindIndex(tracks, t => t.Id == current);
        var next = tracks[(idx + 1 + tracks.Length) % tracks.Length];
        SetAudioTrack(next.Id);
    }

    public void CycleSubtitleTrack()
    {
        var tracks = GetSubtitleTracks();
        if (tracks.Length == 0) return;

        var current = CurrentSubtitleTrack;
        var idx = Array.FindIndex(tracks, t => t.Id == current);
        var next = tracks[(idx + 1 + tracks.Length) % tracks.Length];
        SetSubtitleTrack(next.Id);
    }

    public bool InitializeRenderer(Func<string, IntPtr> getProcAddress, Action requestRender)
    {
        if (_mpv == IntPtr.Zero) return false;
        if (_renderContext != IntPtr.Zero) return true;

        _nativeVideoWindow = IntPtr.Zero;
        SetOption("wid", "0");
        SetOption("vo", "libmpv");
        ResetRenderTarget();
        _getProcAddress = getProcAddress;
        _requestRender = requestRender;
        _getProcAddressCallback = (_, name) =>
        {
            var proc = Marshal.PtrToStringAnsi(name);
            return string.IsNullOrEmpty(proc) ? IntPtr.Zero : _getProcAddress?.Invoke(proc) ?? IntPtr.Zero;
        };

        unsafe
        {
            var apiType = Marshal.StringToHGlobalAnsi("opengl");
            var initParams = new MpvNative.MpvOpenGlInitParams
            {
                get_proc_address = Marshal.GetFunctionPointerForDelegate(_getProcAddressCallback),
                get_proc_address_ctx = IntPtr.Zero
            };
            var initPtr = Marshal.AllocHGlobal(Marshal.SizeOf<MpvNative.MpvOpenGlInitParams>());
            Marshal.StructureToPtr(initParams, initPtr, false);

            try
            {
                var parameters = stackalloc MpvNative.MpvRenderParam[3];
                parameters[0] = new MpvNative.MpvRenderParam(MpvRenderParamType.ApiType, apiType);
                parameters[1] = new MpvNative.MpvRenderParam(MpvRenderParamType.OpenGlInitParams, initPtr);
                parameters[2] = new MpvNative.MpvRenderParam(MpvRenderParamType.Invalid, IntPtr.Zero);

                var result = MpvNative.mpv_render_context_create(out _renderContext, _mpv, parameters);
                if (result < 0 || _renderContext == IntPtr.Zero)
                {
                    ErrorOccurred?.Invoke(this, $"mpv OpenGL renderer failed to initialize: {result}.");
                    StartupLogger.Log($"mpv OpenGL renderer failed to initialize: {result}.");
                    ClearRendererCallbacks();
                    return false;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(apiType);
                Marshal.FreeHGlobal(initPtr);
            }
        }

        _renderUpdateCallback = _ => _requestRender?.Invoke();
        MpvNative.mpv_render_context_set_update_callback(_renderContext, _renderUpdateCallback, IntPtr.Zero);
        _rendererReady = true;
        StartupLogger.Log("mpv OpenGL renderer initialized.");
        return true;
    }

    public void RenderVideo(int framebuffer, int width, int height)
    {
        if (!_rendererReady || _renderContext == IntPtr.Zero || width <= 0 || height <= 0) return;

        var updateFlags = MpvNative.mpv_render_context_update(_renderContext);
        var targetChanged = framebuffer != _lastRenderFramebuffer ||
                            width != _lastRenderWidth ||
                            height != _lastRenderHeight;

        if ((updateFlags & MpvRenderUpdateFrame) == 0 && !targetChanged)
            return;

        unsafe
        {
            var fbo = new MpvNative.MpvOpenGlFbo(framebuffer, width, height, 0);
            var flip = 1;
            var block = 0;
            var parameters = stackalloc MpvNative.MpvRenderParam[4];
            parameters[0] = new MpvNative.MpvRenderParam(MpvRenderParamType.OpenGlFbo, (IntPtr)(&fbo));
            parameters[1] = new MpvNative.MpvRenderParam(MpvRenderParamType.FlipY, (IntPtr)(&flip));
            parameters[2] = new MpvNative.MpvRenderParam(MpvRenderParamType.BlockForTargetTime, (IntPtr)(&block));
            parameters[3] = new MpvNative.MpvRenderParam(MpvRenderParamType.Invalid, IntPtr.Zero);
            MpvNative.mpv_render_context_render(_renderContext, parameters);
        }

        _lastRenderFramebuffer = framebuffer;
        _lastRenderWidth = width;
        _lastRenderHeight = height;
    }

    public void ShutdownRenderer()
    {
        if (_renderContext != IntPtr.Zero)
        {
            MpvNative.mpv_render_context_free(_renderContext);
            _renderContext = IntPtr.Zero;
        }

        _rendererReady = false;
        ResetRenderTarget();
        ClearRendererCallbacks();
        StartupLogger.Log("mpv renderer shut down.");
    }

    public MediaState Snapshot()
    {
        lock (_stateLock)
        {
            _state.IsPlaying = !_pause && !string.IsNullOrWhiteSpace(_state.FilePath);
            _state.IsLooping = _loop;
            return new MediaState
            {
                FilePath = _state.FilePath,
                Position = _state.Position,
                Duration = _state.Duration,
                IsPlaying = _state.IsPlaying,
                IsMuted = _state.IsMuted,
                Volume = _state.Volume,
                Speed = _state.Speed,
                IsLooping = _state.IsLooping
            };
        }
    }

    private void EventLoop()
    {
        while (!_disposed && _mpv != IntPtr.Zero)
        {
            var evt = Marshal.PtrToStructure<MpvNative.MpvEvent>(MpvNative.mpv_wait_event(_mpv, 0.25));
            if (evt.event_id == MpvEventId.None) continue;

            switch (evt.event_id)
            {
                case MpvEventId.Shutdown:
                    return;
                case MpvEventId.FileLoaded:
                    lock (_stateLock) _pause = GetFlag("pause");
                    MarkTracksChanged();
                    RaiseStateChanged();
                    break;
                case MpvEventId.TracksChanged:
                case MpvEventId.TrackSwitched:
                    MarkTracksChanged();
                    RaiseStateChanged();
                    break;
                case MpvEventId.EndFile:
                    // reason 0 = MPV_END_FILE_REASON_EOF (natural end).
                    // Any other reason (stop=2, quit=3, error=4, redirect=5) means the file
                    // was replaced or stopped externally — do NOT auto-advance in those cases.
                    if (!_loop && evt.data != IntPtr.Zero)
                    {
                        var endFileEvt = Marshal.PtrToStructure<MpvNative.MpvEventEndFile>(evt.data);
                        if (endFileEvt.reason == 0)
                            EndReached?.Invoke(this, EventArgs.Empty);
                    }
                    RaiseStateChanged();
                    break;
                case MpvEventId.Pause:
                    lock (_stateLock) _pause = true;
                    RaiseStateChanged();
                    break;
                case MpvEventId.Unpause:
                    lock (_stateLock) _pause = false;
                    RaiseStateChanged();
                    break;
                case MpvEventId.PropertyChange:
                    if (ApplyPropertyChange(evt.data))
                        RaiseStateChanged();
                    break;
            }
        }
    }

    private bool ApplyPropertyChange(IntPtr data)
    {
        if (data == IntPtr.Zero) return false;
        var property = Marshal.PtrToStructure<MpvNative.MpvEventProperty>(data);
        var name = Marshal.PtrToStringAnsi(property.name);
        if (string.IsNullOrWhiteSpace(name) || property.data == IntPtr.Zero) return false;
        var shouldRaise = true;

        lock (_stateLock)
        {
            switch (name)
            {
                case "time-pos" when property.format == MpvFormat.Double:
                    _state.Position = TimeSpan.FromSeconds(Math.Max(0, Marshal.PtrToStructure<double>(property.data)));
                    shouldRaise = false;
                    break;
                case "duration" when property.format == MpvFormat.Double:
                    _state.Duration = TimeSpan.FromSeconds(Math.Max(0, Marshal.PtrToStructure<double>(property.data)));
                    shouldRaise = false;
                    break;
                case "pause" when property.format == MpvFormat.Flag:
                    _pause = Marshal.PtrToStructure<int>(property.data) != 0;
                    break;
                case "mute" when property.format == MpvFormat.Flag:
                    _state.IsMuted = Marshal.PtrToStructure<int>(property.data) != 0;
                    break;
                case "volume" when property.format == MpvFormat.Double:
                    _state.Volume = (int)Math.Round(Math.Clamp(Marshal.PtrToStructure<double>(property.data), 0, 150));
                    break;
                case "speed" when property.format == MpvFormat.Double:
                    _state.Speed = (float)Math.Clamp(Marshal.PtrToStructure<double>(property.data), 0.25, 4.0);
                    break;
                case "aid" or "sid":
                    MarkTracksChanged();
                    break;
                default:
                    shouldRaise = false;
                    break;
            }
        }

        return shouldRaise;
    }

    private void RaiseStateChanged() => StateChanged?.Invoke(this, Snapshot());

    private void MarkTracksChanged() => Interlocked.Increment(ref _trackRevision);

    private int Command(params string[] args)
    {
        if (_mpv == IntPtr.Zero) return -1;

        var ptrs = new IntPtr[args.Length + 1];
        try
        {
            for (var i = 0; i < args.Length; i++)
                ptrs[i] = StringToUtf8(args[i]);
            ptrs[^1] = IntPtr.Zero;

            unsafe
            {
                fixed (IntPtr* p = ptrs)
                    return MpvNative.mpv_command(_mpv, (IntPtr)p);
            }
        }
        finally
        {
            foreach (var ptr in ptrs)
                if (ptr != IntPtr.Zero)
                    Marshal.FreeHGlobal(ptr);
        }
    }

    private void SetOption(string name, string value)
    {
        if (_mpv != IntPtr.Zero)
            MpvNative.mpv_set_option_string(_mpv, name, value);
    }

    private void SetPropertyString(string name, string value)
    {
        if (_mpv != IntPtr.Zero)
            MpvNative.mpv_set_property_string(_mpv, name, value);
        RaiseStateChanged();
    }

    private void Observe(string name, MpvFormat format)
    {
        if (_mpv != IntPtr.Zero)
            MpvNative.mpv_observe_property(_mpv, 0, name, format);
    }

    private void SetFlag(string name, bool value)
    {
        if (_mpv == IntPtr.Zero) return;
        var raw = value ? 1 : 0;
        unsafe
        {
            MpvNative.mpv_set_property(_mpv, name, MpvFormat.Flag, (IntPtr)(&raw));
        }
    }

    public double[] GetChapterPositions()
    {
        if (_mpv == IntPtr.Zero) return [];
        var count = GetInt64("chapter-list/count");
        if (count <= 0) return [];
        var positions = new List<double>((int)count);
        for (var i = 0; i < count; i++)
        {
            var t = GetDouble($"chapter-list/{i}/time");
            if (!double.IsNaN(t))
                positions.Add(t);
        }
        return [.. positions];
    }

    public void SeekToChapter(int direction)
    {
        if (_mpv == IntPtr.Zero || string.IsNullOrWhiteSpace(_state.FilePath)) return;
        Command("add", "chapter", direction > 0 ? "1" : "-1");
    }

    private bool GetFlag(string name)    {
        if (_mpv == IntPtr.Zero) return false;
        var raw = 0;
        unsafe
        {
            return MpvNative.mpv_get_property(_mpv, name, MpvFormat.Flag, (IntPtr)(&raw)) >= 0 && raw != 0;
        }
    }

    private void SetDouble(string name, double value)
    {
        if (_mpv == IntPtr.Zero) return;
        unsafe
        {
            MpvNative.mpv_set_property(_mpv, name, MpvFormat.Double, (IntPtr)(&value));
        }
    }

    private void SetInt(string name, long value)
    {
        if (_mpv == IntPtr.Zero) return;
        unsafe
        {
            MpvNative.mpv_set_property(_mpv, name, MpvFormat.Int64, (IntPtr)(&value));
        }
    }

    private double GetDouble(string name)
    {
        if (_mpv == IntPtr.Zero) return double.NaN;
        double value = double.NaN;
        unsafe
        {
            MpvNative.mpv_get_property(_mpv, name, MpvFormat.Double, (IntPtr)(&value));
        }
        return value;
    }

    private long GetInt64(string name, long fallback = 0)
    {
        if (_mpv == IntPtr.Zero) return fallback;
        long value = fallback;
        unsafe
        {
            return MpvNative.mpv_get_property(_mpv, name, MpvFormat.Int64, (IntPtr)(&value)) >= 0
                ? value
                : fallback;
        }
    }

    private string? GetString(string name)
    {
        if (_mpv == IntPtr.Zero) return null;
        var ptr = MpvNative.mpv_get_property_string(_mpv, name);
        if (ptr == IntPtr.Zero) return null;
        try
        {
            return Marshal.PtrToStringUTF8(ptr);
        }
        finally
        {
            MpvNative.mpv_free(ptr);
        }
    }

    private MediaTrack[] GetTracks(string type)
    {
        if (_mpv == IntPtr.Zero) return [];

        var count = GetInt64("track-list/count");
        if (count <= 0) return [];

        var tracks = new List<MediaTrack>();
        for (var i = 0; i < count; i++)
        {
            var prefix = $"track-list/{i}";
            if (!string.Equals(GetString($"{prefix}/type"), type, StringComparison.OrdinalIgnoreCase))
                continue;

            var id = (int)GetInt64($"{prefix}/id", -1);
            if (id < 0) continue;

            var selected = GetFlag($"{prefix}/selected");
            tracks.Add(new MediaTrack(id, BuildTrackName(prefix, type, id), selected));
        }

        return [.. tracks];
    }

    private int GetSelectedTrackId(string type)
    {
        foreach (var track in GetTracks(type))
        {
            if (track.IsSelected)
                return track.Id;
        }

        return -1;
    }

    private string BuildTrackName(string prefix, string type, int id)
    {
        var title = GetString($"{prefix}/title");
        var lang = GetString($"{prefix}/lang");
        var codec = GetString($"{prefix}/codec");
        var external = GetFlag($"{prefix}/external");

        var fallback = type == "audio" ? $"Audio {id}" : $"Subtitle {id}";
        var main = !string.IsNullOrWhiteSpace(title) ? title! : fallback;
        var details = new List<string>();

        if (!string.IsNullOrWhiteSpace(lang))
            details.Add(FormatLanguageName(lang!));
        if (!string.IsNullOrWhiteSpace(codec))
            details.Add(codec!);
        if (external)
            details.Add("external");

        return details.Count == 0 ? main : $"{main} ({string.Join(", ", details)})";
    }

    private static string FormatLanguageName(string language)
    {
        var code = language.Trim();
        if (LanguageNames.TryGetValue(code, out var name))
            return name;

        try
        {
            if (code.Length == 2)
                return CultureInfo.GetCultureInfo(code).EnglishName;
        }
        catch (CultureNotFoundException)
        {
        }

        return code;
    }

    private static IntPtr StringToUtf8(string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        var ptr = Marshal.AllocHGlobal(bytes.Length + 1);
        Marshal.Copy(bytes, 0, ptr, bytes.Length);
        Marshal.WriteByte(ptr, bytes.Length, 0);
        return ptr;
    }

    private void ResetRenderTarget()
    {
        _lastRenderFramebuffer = -1;
        _lastRenderWidth = 0;
        _lastRenderHeight = 0;
    }

    private void ClearRendererCallbacks()
    {
        _requestRender = null;
        _getProcAddress = null;
        _renderUpdateCallback = null;
        _getProcAddressCallback = null;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        ShutdownRenderer();

        if (_mpv != IntPtr.Zero)
            MpvNative.mpv_terminate_destroy(_mpv);

        if (_eventThread is { IsAlive: true })
            _eventThread.Join(TimeSpan.FromMilliseconds(500));
    }


}

public sealed record MediaTrack(int Id, string Name, bool IsSelected = false);

public enum MpvFormat
{
    None = 0,
    String = 1,
    OsdString = 2,
    Flag = 3,
    Int64 = 4,
    Double = 5
}

public enum MpvEventId
{
    None = 0,
    Shutdown = 1,
    StartFile = 6,
    EndFile = 7,
    FileLoaded = 8,
    TracksChanged = 9,
    TrackSwitched = 10,
    Idle = 11,
    Pause = 12,
    Unpause = 13,
    VideoReconfig = 17,
    AudioReconfig = 18,
    Seek = 20,
    PlaybackRestart = 21,
    PropertyChange = 22
}

public enum MpvRenderParamType
{
    Invalid = 0,
    ApiType = 1,
    OpenGlInitParams = 2,
    OpenGlFbo = 3,
    FlipY = 4,
    SwSize = 6,
    SwFormat = 7,
    SwStride = 8,
    SwPointer = 9,
    BlockForTargetTime = 12
}

public static partial class MpvNative
{
    private const string Library = "mpv";

    static MpvNative()
    {
        NativeLibrary.SetDllImportResolver(typeof(MpvNative).Assembly, ResolveLibrary);
    }

    private static IntPtr ResolveLibrary(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, Library, StringComparison.Ordinal))
            return IntPtr.Zero;

        var candidates = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? new[] { "mpv-2.dll", "libmpv-2.dll", "mpv.dll" }
            : RuntimeInformation.IsOSPlatform(OSPlatform.OSX)
                ? new[] { "libmpv.2.dylib", "libmpv.dylib" }
                : new[] { "libmpv.so.2", "libmpv.so" };

        foreach (var candidate in candidates)
        {
            if (NativeLibrary.TryLoad(candidate, assembly, searchPath, out var handle))
                return handle;

            var localPath = Path.Combine(AppContext.BaseDirectory, candidate);
            if (NativeLibrary.TryLoad(localPath, out handle))
                return handle;
        }

        return IntPtr.Zero;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate IntPtr MpvOpenGlGetProcAddress(IntPtr ctx, IntPtr name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    public delegate void MpvRenderUpdateCallback(IntPtr ctx);

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MpvEvent
    {
        public readonly MpvEventId event_id;
        public readonly int error;
        public readonly ulong reply_userdata;
        public readonly IntPtr data;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MpvEventProperty
    {
        public readonly IntPtr name;
        public readonly MpvFormat format;
        public readonly IntPtr data;
    }

    /// <summary>Maps to <c>mpv_event_end_file</c>.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MpvEventEndFile
    {
        /// <summary>0 = EOF (natural end), 2 = stop, 3 = quit, 4 = error, 5 = redirect.</summary>
        public readonly int reason;
        public readonly int error;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct MpvOpenGlInitParams
    {
        public IntPtr get_proc_address;
        public IntPtr get_proc_address_ctx;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MpvOpenGlFbo(int fbo, int w, int h, int internalFormat)
    {
        public readonly int fbo = fbo;
        public readonly int w = w;
        public readonly int h = h;
        public readonly int internal_format = internalFormat;
    }

    [StructLayout(LayoutKind.Sequential)]
    public readonly struct MpvRenderParam(MpvRenderParamType type, IntPtr data)
    {
        public readonly MpvRenderParamType type = type;
        public readonly IntPtr data = data;
    }

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_create();

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_terminate_destroy(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_initialize(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_set_option_string(IntPtr ctx, string name, string value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_observe_property(IntPtr ctx, ulong replyUserData, string name, MpvFormat format);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_set_property(IntPtr ctx, string name, MpvFormat format, IntPtr data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_set_property_string(IntPtr ctx, string name, string value);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern int mpv_get_property(IntPtr ctx, string name, MpvFormat format, IntPtr data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
    public static extern IntPtr mpv_get_property_string(IntPtr ctx, string name);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_free(IntPtr data);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern int mpv_command(IntPtr ctx, IntPtr args);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern IntPtr mpv_wait_event(IntPtr ctx, double timeout);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static unsafe extern int mpv_render_context_create(out IntPtr res, IntPtr mpv, MpvRenderParam* parameters);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static unsafe extern void mpv_render_context_set_update_callback(
        IntPtr ctx,
        MpvRenderUpdateCallback callback,
        IntPtr callbackContext);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern ulong mpv_render_context_update(IntPtr ctx);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static unsafe extern int mpv_render_context_render(IntPtr ctx, MpvRenderParam* parameters);

    [DllImport(Library, CallingConvention = CallingConvention.Cdecl)]
    public static extern void mpv_render_context_free(IntPtr ctx);
}
