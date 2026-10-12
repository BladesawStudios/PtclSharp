using System.Numerics;
using ImGuiNET;
using PtclSharp;
using PtclSharp.Simulation.Resources;
using PtclSharp.Simulation.Runtime;
using PtclSharp.Rendering.Assets;
using PtclSharp.Rendering.Drawing;
using PtclSharp.Rendering.Imaging;
using PtclSharp.Rendering.Post;
using Silk.NET.Input;
using Silk.NET.OpenGL;
using Silk.NET.OpenGL.Extensions.ImGui;
using Silk.NET.Windowing;

namespace PtclViewer;

public sealed class ViewerOptions
{
    public string RomRoot = Environment.GetEnvironmentVariable("PTCL_BOTW_ROM")
        ?? @"C:\Users\dylan\shenanigans\Personal\Nintendo\Switch\Zelda BotW\RE\ROM";
    public string? Effect;
    public string? Set;
    public string? ShotPath;
    public int ShotFrames = 60;
    public float[] ShotAtFrames = [];
    public int Width = 1280, Height = 720;
    public float Exposure = 1f;
    public float[]? Camera;
    public float Fps = 30f;
    public string[]? Survey;      // effect names, or ["all"]
    public int SurveyMaxSets = 3;
    public bool AutoFrame = true;
    public string[]? Only;
    public int[]? Probe;
    public float OrbitRadius, OrbitSpeed = 0.05f;
    /// <summary>Emitter set colour the game would set at runtime (guardians, enemies recolour their effects); default white.</summary>
    public System.Numerics.Vector4 SetColor = System.Numerics.Vector4.One;
    public bool Bloom = true;
    public float BloomIntensity = 0.35f, BloomThreshold = 1.2f;
}

/// <summary>The interactive effect viewer: browse a ROM's effects, spawn their emitter sets, watch the simulation run.</summary>
public sealed unsafe class ViewerApp
{
    private readonly ViewerOptions _opt;
    private IWindow _window = null!;
    private GL _gl = null!;
    private ImGuiController? _imgui;
    private IInputContext? _input;
    private ParticleRenderer _renderer = null!;
    private GridRenderer _grid = null!;
    private HdrTarget _hdr = null!;
    private Bloom _bloom = null!;
    private Tonemap _tonemap = null!;
    private readonly BloomSettings _bloomSettings = new();
    private readonly Camera _cam = new();

    private readonly EffectSystem _system = new();
    private EffectAssets? _assets;
    private EmitterSetInstance? _instance;
    private EmitterSetResource? _currentSet;
    private string[] _files = [];
    private string _filter = "";
    private string _status = "";
    private bool _paused;
    private float _speed = 1f;
    private float _frameAccumulator;
    private float _exposure = 1f;
    private bool _showGrid = true, _loop = true, _cycleSets;
    private float _loopSeconds = 8f;
    private double _totalFrames;
    private readonly HashSet<string> _hidden = [];
    private Vector2 _lastMouse;
    private bool _dragging, _panning;
    private int _setIndex;
    private float _orbitRadius, _orbitSpeed = 0.05f;

    public ViewerApp(ViewerOptions options)
    {
        _opt = options;
        _exposure = options.Exposure;
        _orbitRadius = options.OrbitRadius;
        _orbitSpeed = options.OrbitSpeed;
    }

    public int Run()
    {
        var wo = WindowOptions.Default;
        wo.Size = new Silk.NET.Maths.Vector2D<int>(_opt.Width, _opt.Height);
        wo.Title = "PtclViewer";
        wo.API = new GraphicsAPI(ContextAPI.OpenGL, ContextProfile.Core, ContextFlags.Default, new APIVersion(4, 5));
        wo.IsVisible = _opt.ShotPath is null;
        wo.VSync = true;
        _window = Window.Create(wo);
        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Closing += () => { _imgui?.Dispose(); };
        _window.Run();
        return _exitCode;
    }

    private int _exitCode;

    private void OnLoad()
    {
        _gl = GL.GetApi(_window);
        Console.WriteLine($"GL: {_gl.GetStringS(StringName.Renderer)} / {_gl.GetStringS(StringName.Version)}");
        _renderer = new ParticleRenderer(_gl);
        _grid = new GridRenderer(_gl);
        _hdr = new HdrTarget(_gl);
        _bloom = new Bloom(_gl);
        _tonemap = new Tonemap(_gl);
        _bloomSettings.Enabled = _opt.Bloom;
        _bloomSettings.Intensity = _opt.BloomIntensity;
        _bloomSettings.Threshold = _opt.BloomThreshold;

        string effectDir = Path.Combine(_opt.RomRoot, "Effect");
        _files = Directory.Exists(effectDir)
            ? Directory.GetFiles(effectDir, "*.sesetlist").Select(Path.GetFileNameWithoutExtension).Order(StringComparer.OrdinalIgnoreCase).ToArray()!
            : [];

        if (_opt.ShotPath is null)
        {
            _input = _window.CreateInput();
            _imgui = new ImGuiController(_gl, _window, _input);
            foreach (IMouse m in _input.Mice)
            {
                m.MouseDown += (_, b) => { if (!ImGui.GetIO().WantCaptureMouse) { _dragging = b == MouseButton.Left; _panning = b is MouseButton.Right or MouseButton.Middle; } };
                m.MouseUp += (_, b) => { _dragging = false; _panning = false; };
                m.MouseMove += (_, pos) =>
                {
                    Vector2 d = pos - _lastMouse;
                    _lastMouse = pos;
                    if (ImGui.GetIO().WantCaptureMouse) return;
                    if (_dragging) _cam.Orbit(d.X, d.Y);
                    else if (_panning) _cam.Pan(d.X, d.Y);
                };
                m.Scroll += (_, w) => { if (!ImGui.GetIO().WantCaptureMouse) _cam.Zoom(w.Y); };
            }
        }

        if (_opt.Camera is { Length: >= 3 } c)
        {
            _cam.Yaw = c[0]; _cam.Pitch = c[1]; _cam.Distance = c[2];
            if (c.Length >= 6) _cam.Target = new Vector3(c[3], c[4], c[5]);
        }
        if (_opt.Effect is not null) LoadEffect(_opt.Effect, _opt.Set);
    }

    // ------------------------------------------------------------------ loading

    private void LoadEffect(string name, string? setName = null)
    {
        try
        {
            string path = Path.Combine(_opt.RomRoot, "Effect", name + ".sesetlist");
            PtclFile file = PtclFile.ReadSesetlist(File.ReadAllBytes(path));
            _system.KillAll();
            _assets = _renderer.LoadEffect(name, file, Path.Combine(_opt.RomRoot, "Pack", "Bootup.pack"), path);
            _system.Primitives = id => _assets.Meshes.Primitive(id);
            int index = setName is null ? 0 : Math.Max(0, _assets.Sets.ToList().FindIndex(s => s.Name.Equals(setName, StringComparison.OrdinalIgnoreCase)));
            if (setName is not null && int.TryParse(setName, out int n) && n < _assets.Sets.Count) index = n;
            _status = $"{name}: {_assets.Sets.Count} emitter sets";
            Spawn(index);
        }
        catch (Exception e)
        {
            _status = $"failed to load {name}: {e.Message}";
            Console.Error.WriteLine(e);
            _exitCode = 2;
        }
    }

    private void Spawn(int index)
    {
        if (_assets is null || index < 0 || index >= _assets.Sets.Count) return;
        _system.KillAll();
        _setIndex = index;
        _currentSet = _assets.Sets[index];
        _instance = _system.CreateEmitterSet(_currentSet);
        _instance.Color = _opt.SetColor;
        _totalFrames = 0;
        _hidden.Clear();
    }

    // ------------------------------------------------------------------ frame

    private void Step(float frames)
    {
        if (frames <= 0) return;
        if (_instance is not null && _orbitRadius > 0f)
        {
            float a = (float)_totalFrames * _orbitSpeed;
            _instance.SetPos(new Vector3(MathF.Cos(a) * _orbitRadius, 0f, MathF.Sin(a) * _orbitRadius));
        }
        _system.Calculate(frames);
        _totalFrames += frames;
        if (_loop && _instance is not null && _assets is not null && _opt.ShotPath is null)
        {
            // restart when the effect ends, or after the loop length for effects that never end (looping effects, emitters with infinite life)
            bool finished = !_instance.IsAlive || (_loopSeconds > 0f && _totalFrames >= _loopSeconds * _opt.Fps);
            if (finished) Spawn(_cycleSets ? (_setIndex + 1) % Math.Max(_assets.Sets.Count, 1) : _setIndex);
        }
    }

    private void OnRender(double dt)
    {
        if (_opt.ShotPath is not null) { RunShot(); return; }

        if (!_paused)
        {
            _frameAccumulator += (float)dt * _opt.Fps * _speed;
            int steps = 0;
            while (_frameAccumulator >= 1f && steps < 8) { Step(1f); _frameAccumulator -= 1f; steps++; }
            if (steps == 8) _frameAccumulator = 0;
        }

        _imgui!.Update((float)dt);
        int w = _window.FramebufferSize.X, h = _window.FramebufferSize.Y;
        if (w <= 0 || h <= 0) return;
        RenderScene(w, h);

        _presentTarget = 0;
        (_presentWidth, _presentHeight) = (w, h);
        Present();
        DrawUi();
        _imgui.Render();
    }

    private void RenderScene(int w, int h)
    {
        _hdr.Resize(w, h);
        _hdr.Bind();
        _gl.DepthMask(true);
        _gl.ClearColor(0.02f, 0.025f, 0.035f, 1f);
        _gl.ClearDepth(1.0);
        _gl.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit);

        var view = new ParticleView
        {
            View = _cam.View, Projection = _cam.Projection(w / (float)h), Near = _cam.Near, Far = _cam.Far, Width = w, Height = h
        };
        _renderer.Begin(view);
        if (_showGrid) _grid.Draw(view.View * view.Projection);
        if (_instance is not null && _assets is not null)
        {
            _renderer.DrawSet(_instance, _assets, e => !_hidden.Contains(e.Res.Name) && (_opt.Only is null || _opt.Only.Contains(e.Res.Name)));
            _renderer.Prune(_system.Sets.SelectMany(s => s.AllEmitters));
        }
        _renderer.End();
    }

    /// <summary>Tonemaps the HDR target (with bloom) into the bound framebuffer. The target is in the game's orientation, so it is flipped on the way out.</summary>
    private void Present()
    {
        uint glow = _bloomSettings.Enabled ? _bloom.Render(_hdr.ColorTexture, _hdr.Width, _hdr.Height, _bloomSettings) : 0;
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _presentTarget);
        _gl.Viewport(0, 0, (uint)_presentWidth, (uint)_presentHeight);
        _tonemap.Draw(_hdr.ColorTexture, glow, _exposure, _bloomSettings.Intensity, flipVertical: true);
    }

    private uint _presentTarget;
    private int _presentWidth, _presentHeight;

    // ------------------------------------------------------------------ screenshots

    private int _shotStep;

    /// <summary>Frames the camera on the live particles (their bounds in world space).</summary>
    private void FrameParticles()
    {
        if (_instance is null) return;
        Vector3 min = new(float.MaxValue), max = new(float.MinValue);
        int n = 0;
        foreach (Emitter e in _instance.AllEmitters)
        {
            if (e.Res.CustomActionIndex > 0 && !e.Res.Textures.Any(t => t.IsValid)) continue;
            for (int i = 0; i < e.ParticleCount; i++)
            {
                if (e.Particles.CreateId[i] == 0) continue;
                Vector4 lp = e.Particles.LocalPos[i];
                Vector3 w = e.TransformToWorldPos(new Vector3(lp.X, lp.Y, lp.Z), i);
                if (!float.IsFinite(w.X + w.Y + w.Z)) continue;
                min = Vector3.Min(min, w); max = Vector3.Max(max, w); n++;
            }
        }
        if (n == 0) return;
        Vector3 center = (min + max) / 2, ext = max - min;
        _cam.Target = center;
        // stay beyond every emitter's near fade, or the effect is faded out (they are meant to be seen from a distance)
        float nearFade = _instance.AllEmitters.Where(e => e.ParticleCount > 0).Select(e => e.Res.NearFade.End).DefaultIfEmpty(0f).Max();
        _cam.Distance = Math.Clamp(Math.Max((Math.Max(ext.X, Math.Max(ext.Y, ext.Z)) * 1.6f) + 2.5f, (nearFade * 1.3f) + 1f), 3f, 80f);
    }

    private void RunSurvey()
    {
        string dir = _opt.ShotPath!;
        Directory.CreateDirectory(dir);
        string[] names = _opt.Survey!.Length == 1 && _opt.Survey[0] == "all" ? _files : _opt.Survey;
        int w = _opt.Width, h = _opt.Height;
        float[] marks = _opt.ShotAtFrames.Length > 0 ? _opt.ShotAtFrames : [30f];
        int shaderFails = 0, total = 0;
        foreach (string name in names)
        {
            LoadEffect(name);
            if (_assets is null) continue;
            for (int si = 0; si < Math.Min(_assets.Sets.Count, _opt.SurveyMaxSets); si++)
            {
                Spawn(si);
                _loop = false;
                foreach (float mark in marks.Order())
                {
                    while (_totalFrames + 0.001 < mark) Step(1f);
                    if (_opt.AutoFrame) FrameParticles();
                    RenderScene(w, h);
                    string safe = _assets.Sets[si].Name;
                    SaveColor(Path.Combine(dir, $"{name}__{safe}_{(int)mark:D3}.png"), w, h);
                }
                total++;
            }
            int bad = 0;
            for (int v = 0; v < _assets.VariationCount; v++) if (_assets.GetProgram(v) is null) bad++;
            shaderFails += bad;
            Console.WriteLine($"{name}: sets={_assets.Sets.Count} variations={_assets.VariationCount} shaderFailures={bad} {string.Join("; ", _assets.Messages.Distinct().Take(4))}");
        }
        Console.WriteLine($"survey done: {names.Length} effects, {total} sets shot, {shaderFails} shader failures");
        _window.Close();
    }

    private void RunShot()
    {
        if (_opt.Survey is not null) { RunSurvey(); return; }
        // advance the simulation, then render and save one frame (or one per requested frame)
        int w = _opt.Width, h = _opt.Height;
        float[] marks = _opt.ShotAtFrames.Length > 0 ? _opt.ShotAtFrames : [_opt.ShotFrames];
        int doneFrames = 0;
        foreach (float mark in marks.Order())
        {
            while (_totalFrames + 0.001 < mark) Step(1f);
            if (_opt.Camera is null && _opt.AutoFrame) FrameParticles();
            RenderScene(w, h);
            string path = marks.Length == 1 ? _opt.ShotPath! : Path.Combine(Path.GetDirectoryName(_opt.ShotPath!) ?? ".",
                $"{Path.GetFileNameWithoutExtension(_opt.ShotPath)}_{(int)mark:D4}.png");
            SaveColor(path, w, h);
            if (_opt.Probe is { Length: 2 }) ProbePixel(_opt.Probe[0], _opt.Probe[1]);
            int live = _instance?.AllEmitters.Sum(e => e.Particles.CreateId.Take(e.ParticleCount).Count(c => c != 0)) ?? 0;
            Console.WriteLine($"saved {path}  frame={_totalFrames:F0} live={live} draws={_renderer.DrawCalls} drawn={_renderer.ParticlesDrawn}");
            doneFrames++;
        }
        if (_instance is not null && _assets is not null)
            foreach (Emitter e in _instance.AllEmitters)
            {
                EmitterResource r = e.Res;
                string tex = string.Join(",", r.Textures.Select(t => t.IsValid ? _assets.Textures.NameOf(t.Guid) : "-"));
                int live = 0;
                for (int i = 0; i < e.ParticleCount; i++) if (e.Particles.CreateId[i] != 0) live++;
                Console.WriteLine($"  {r.Name,-24} calc={r.Calc} shape={r.Volume} follow={r.Follow} shader={r.ShaderIndex} blend={r.Render.BlendMode}/{(r.Render.BlendEnable ? "on" : "off")} depth={r.Render.DepthTest}/{r.Render.DepthWrite} cull={r.Render.CullMode} path={r.DrawPath} act={r.CustomActionIndex} cs={r.CustomShaderIndex} sort={r.SortType} tex=[{tex}] live={live}/{e.ParticleCount}");
            }
        foreach (string m in (_assets?.Messages ?? []).Concat(_renderer.Warnings).Distinct()) Console.WriteLine("note: " + m);
        _shotStep++;
        _window.Close();
    }

    private void ProbePixel(int x, int y)
    {
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, _hdr.Framebuffer);
        float[] px = new float[4];
        // the target is in the game's orientation: memory row 0 is the top of the picture
        fixed (float* p = px) _gl.ReadPixels(x, y, 1, 1, PixelFormat.Rgba, PixelType.Float, p);
        Console.WriteLine($"probe ({x},{y}) = {px[0]:G9} {px[1]:G9} {px[2]:G9} {px[3]:G9}");
    }

    private void SaveColor(string path, int w, int h)
    {
        // tonemap into an RGBA8 target and read it back
        uint fbo = _gl.GenFramebuffer(), tex = _gl.GenTexture();
        _gl.BindTexture(TextureTarget.Texture2D, tex);
        _gl.TexImage2D(TextureTarget.Texture2D, 0, InternalFormat.Rgba8, (uint)w, (uint)h, 0, PixelFormat.Rgba, PixelType.UnsignedByte, null);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, fbo);
        _gl.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2D, tex, 0);
        (_presentTarget, _presentWidth, _presentHeight) = (fbo, w, h);
        Present();
        byte[] px = new byte[w * h * 4];
        fixed (byte* p = px) _gl.ReadPixels(0, 0, (uint)w, (uint)h, PixelFormat.Rgba, PixelType.UnsignedByte, p);
        _gl.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
        _gl.DeleteFramebuffer(fbo);
        _gl.DeleteTexture(tex);
        // flip rows
        byte[] flipped = new byte[px.Length];
        for (int y = 0; y < h; y++) System.Buffer.BlockCopy(px, y * w * 4, flipped, (h - 1 - y) * w * 4, w * 4);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
        PngWriter.Write(path, flipped, w, h);
    }

    // ------------------------------------------------------------------ UI

    private void DrawUi()
    {
        ImGui.SetNextWindowPos(new Vector2(8, 8), ImGuiCond.FirstUseEver);
        ImGui.SetNextWindowSize(new Vector2(330, 620), ImGuiCond.FirstUseEver);
        ImGui.Begin("Effects");
        ImGui.InputText("filter", ref _filter, 64);
        if (ImGui.BeginListBox("##files", new Vector2(-1, 200)))
        {
            foreach (string f in _files)
            {
                if (_filter.Length > 0 && !f.Contains(_filter, StringComparison.OrdinalIgnoreCase)) continue;
                if (ImGui.Selectable(f, _assets?.Name == f)) LoadEffect(f);
            }
            ImGui.EndListBox();
        }
        if (_assets is not null)
        {
            ImGui.Separator();
            ImGui.Text($"{_assets.Name} - sets ({_assets.Sets.Count})");
            if (ImGui.BeginListBox("##sets", new Vector2(-1, 120)))
            {
                for (int i = 0; i < _assets.Sets.Count; i++)
                    if (ImGui.Selectable(_assets.Sets[i].Name, i == _setIndex)) Spawn(i);
                ImGui.EndListBox();
            }
        }
        ImGui.Separator();
        if (ImGui.Button(_paused ? "Play" : "Pause")) _paused = !_paused;
        ImGui.SameLine();
        if (ImGui.Button("Step")) Step(1f);
        ImGui.SameLine();
        if (ImGui.Button("Restart")) Spawn(_setIndex);
        ImGui.SliderFloat("speed", ref _speed, 0.05f, 4f);
        ImGui.Checkbox("loop", ref _loop);
        ImGui.SameLine();
        ImGui.Checkbox("next set when looping", ref _cycleSets);
        ImGui.SliderFloat("loop length (s, 0 = until it ends)", ref _loopSeconds, 0f, 30f);
        ImGui.SameLine();
        ImGui.Checkbox("grid", ref _showGrid);
        ImGui.SliderFloat("exposure", ref _exposure, 0.1f, 8f);
        bool bloomOn = _bloomSettings.Enabled;
        if (ImGui.Checkbox("bloom", ref bloomOn)) _bloomSettings.Enabled = bloomOn;
        if (bloomOn)
        {
            float threshold = _bloomSettings.Threshold, intensity = _bloomSettings.Intensity, radius = _bloomSettings.Radius;
            if (ImGui.SliderFloat("bloom threshold", ref threshold, 0f, 4f)) _bloomSettings.Threshold = threshold;
            if (ImGui.SliderFloat("bloom intensity", ref intensity, 0f, 2f)) _bloomSettings.Intensity = intensity;
            if (ImGui.SliderFloat("bloom radius", ref radius, 0.5f, 3f)) _bloomSettings.Radius = radius;
        }
        ImGui.SliderFloat("orbit radius", ref _orbitRadius, 0f, 10f);
        ImGui.SliderFloat("orbit speed", ref _orbitSpeed, 0.01f, 0.3f);
        ImGui.Text($"frame {_totalFrames:F0}   draws {_renderer.DrawCalls}   particles {_renderer.ParticlesDrawn}");

        if (_instance is not null)
        {
            ImGui.Separator();
            ImGui.Text("emitters");
            foreach (Emitter e in _instance.AllEmitters)
            {
                string name = e.Res.Name;
                bool visible = !_hidden.Contains(name);
                int live = 0;
                for (int i = 0; i < e.ParticleCount; i++) if (e.Particles.CreateId[i] != 0) live++;
                if (ImGui.Checkbox($"{(e.IsChild ? "  └ " : "")}{name} [{live}] ##{e.EmitterCreateId}", ref visible))
                { if (visible) _hidden.Remove(name); else _hidden.Add(name); }
            }
        }
        ImGui.Separator();
        ImGui.TextWrapped(_status);
        if (_assets is not null) foreach (string m in _assets.Messages.Concat(_renderer.Warnings).Distinct().Take(8)) ImGui.TextWrapped(m);
        ImGui.End();
    }
}
