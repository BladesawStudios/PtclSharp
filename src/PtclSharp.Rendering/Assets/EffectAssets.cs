using EffectLibraryTest;
using PtclSharp.Rendering.Gl;
using PtclSharp.Rendering.Lighting;
using PtclSharp.Simulation.Resources;
using ShaderLibrary;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Assets;

/// <summary>
/// Everything one effect file needs on the GPU: its emitter sets, its decompiled shader programs and its textures, built on first use. The simulation
/// resources are game-neutral; this class is where BotW's file layout (BNSH shader archive, BNTX textures, resident pack) is handled.
/// </summary>
public sealed class EffectAssets
{
    readonly GL _gl;
    readonly GameEnvironment _environment;
    readonly BnshFile? _shaders;
    readonly Dictionary<int, GlProgram?> _programs = [];

    public EffectAssets(GL gl, ProceduralTextures fallbacks, GameEnvironment environment, string name, PtclFile file, string? bootupPack, string effectPath)
    {
        _gl = gl;
        _environment = environment;
        Name = name;
        File = file;
        IEmitterResourceReader reader = EmitterResourceReaders.For(file.Version);
        Sets = file.Vfxb.EmitterSets.Select(set => reader.ReadSet(file.Vfxb, set)).ToArray();

        Meshes = new MeshLibrary(gl, effectPath, bootupPack ?? "");
        if (Meshes.Error is not null)
            Messages.Add("meshes: " + Meshes.Error);
        Textures = new TextureCatalog(gl, fallbacks, file, bootupPack, Messages);
        _shaders = OpenShaderArchive(file);
    }

    public string Name { get; }

    public PtclFile File { get; }

    public IReadOnlyList<EmitterSetResource> Sets { get; }

    public List<string> Messages { get; } = [];

    public MeshLibrary Meshes { get; }

    public TextureCatalog Textures { get; }

    public int VariationCount => _shaders?.Variations.Count ?? 0;

    /// <summary>The program for a shader variation index, or null (with a message) when it cannot be built.</summary>
    public GlProgram? GetProgram(int variation)
    {
        if (variation < 0 || _shaders is null || variation >= _shaders.Variations.Count)
            return null;
        if (_programs.TryGetValue(variation, out GlProgram? known))
            return known;
        GlProgram? program = null;
        try { program = Build(variation); }
        catch (Exception e)
        {
            Messages.Add($"shader {variation}: {e.Message.Split('\n')[0]}");
            Console.Error.WriteLine($"[{Name}] shader {variation}: {e.Message}");
        }
        return _programs[variation] = program;
    }

    GlProgram Build(int variation)
    {
        var binary = _shaders!.Variations[variation].BinaryProgram;
        if (binary.VertexShader?.ByteCode is null || binary.FragmentShader?.ByteCode is null)
            throw new InvalidOperationException("variation has no vertex/fragment stage");

        string vertex = EffectDebug.DebugVertex(GlslClean.Apply(ShaderExtract.GetCode(binary.VertexShader, binary.VertexShaderReflection)));
        string fragment = GlslClean.Apply(ShaderExtract.GetCode(binary.FragmentShader, binary.FragmentShaderReflection));
        fragment = GlslClean.ForwardizeGBuffer(fragment, GlslClean.SunInViewSpace(_environment.SunDirection));
        fragment = EffectDebug.DebugFragment(fragment);

        if (EffectDebug.DumpGlslDirectory is { } directory)
        {
            Directory.CreateDirectory(directory);
            System.IO.File.WriteAllText(Path.Combine(directory, $"{Name}_{variation}.vert"), vertex);
            System.IO.File.WriteAllText(Path.Combine(directory, $"{Name}_{variation}.frag"), fragment);
        }
        return new GlProgram(_gl, vertex, fragment, $"{Name}#{variation}");
    }

    BnshFile? OpenShaderArchive(PtclFile file)
    {
        VfxbNode? node = file.Vfxb.Roots.FirstOrDefault(root => root.Kind == "GRSN");
        if (node?.DataOffset is not int at || at + 4 > file.Vfxb.Data.Length || !file.Vfxb.Data.AsSpan(at, 4).SequenceEqual("BNSH"u8))
            return null;
        try { return new BnshFile(new MemoryStream(file.Vfxb.Data, at, file.Vfxb.Data.Length - at)); }
        catch (Exception e)
        {
            Messages.Add($"shader archive: {e.Message}");
            return null;
        }
    }
}
