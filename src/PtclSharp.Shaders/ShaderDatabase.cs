using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PtclSharp.Shaders;

public enum ShaderGame
{
    BotW,
    TotK
}

/// <summary>
/// What one shader program (vertex, fragment and compute stages together) looks like from outside: which samplers it samples,
/// which vertex attributes it consumes, and which bytes of the emitter static uniform block (a verbatim copy of the first bytes of the
/// emitter data) it reads. The reads are what the program does with an emitter: a program that never reads the UV scroll fields
/// cannot scroll a texture, whatever the emitter says.
/// </summary>
/// <param name="Id">SHA-1 of the stage byte code and control code, in hex. Identical programs in different effect files share an id.</param>
/// <param name="Stages">Letters of the stages present: V, F, C.</param>
/// <param name="VertexInputs">Vertex attribute names (<c>sysPosAttr</c>, <c>sysEmtMat0Attr</c>, <c>gl_VertexID</c>, ...); the geometry class of the program.</param>
/// <param name="FragmentSamplers">Fragment samplers, for example <c>sysTextureSampler0</c> or <c>sysDepthBufferTexture</c>.</param>
/// <param name="VertexSamplers">Vertex-stage samplers (custom shaders sample textures there too).</param>
/// <param name="FragmentBlocks">Uniform blocks the fragment stage declares.</param>
/// <param name="VertexBlocks">Uniform blocks the vertex stage declares.</param>
/// <param name="VertexReads">Byte offsets of the static block that the vertex stage reads (4-byte granularity).</param>
/// <param name="FragmentReads">Byte offsets of the static block that the fragment stage reads.</param>
/// <param name="ComputeReads">Byte offsets of the static block that the compute stage reads.</param>
public sealed record ProgramSignature(
    string Id,
    string Stages,
    string[] VertexInputs,
    string[] FragmentSamplers,
    string[] VertexSamplers,
    string[] FragmentBlocks,
    string[] VertexBlocks,
    int[] VertexReads,
    int[] FragmentReads,
    int[] ComputeReads)
{
    /// <summary>The texture slots (0 to 5) the fragment stage samples, as a bit mask.</summary>
    [JsonIgnore]
    public int TextureSlotMask => FragmentSamplers.Aggregate(0, (mask, s) =>
        s.StartsWith("sysTextureSampler", StringComparison.Ordinal) && int.TryParse(s.AsSpan("sysTextureSampler".Length), out int slot) ? mask | (1 << slot) : mask);

    [JsonIgnore]
    public bool UsesDepthBuffer => FragmentSamplers.Contains("sysDepthBufferTexture");

    /// <summary>True when the program depends on a custom shader (its own uniform block, samplers or reserved parameters).</summary>
    [JsonIgnore]
    public bool IsCustom => FragmentSamplers.Concat(VertexSamplers).Concat(FragmentBlocks).Concat(VertexBlocks)
        .Any(n => n.StartsWith("sysCustomShader", StringComparison.Ordinal) && n != "sysCustomShaderReservedUniformBlockParam");

    [JsonIgnore]
    public IEnumerable<int> AllReads => VertexReads.Concat(FragmentReads).Concat(ComputeReads);
}

/// <summary>An emitter of an effect file and the shader programs it selects (indices into the file's variation lists).</summary>
/// <param name="TextureSlots">One character per texture slot: '1' when the emitter has a texture there.</param>
public sealed record ShaderEmitterUse(string Name, int Normal, int Pass1, int Pass2, int Compute0, int Compute1, int Compute2, int CustomShader, string TextureSlots);

/// <summary>An effect file's shader archive and the emitters that use it.</summary>
/// <param name="Programs">Program id of every graphics variation, in archive order (an emitter's <c>shader_idx_*</c> indexes this).</param>
/// <param name="ComputePrograms">Program id of every compute variation, in archive order (<c>compute_shader*</c> indexes this).</param>
public sealed record ShaderFileEntry(ShaderGame Game, string Name, string[] Programs, string[] ComputePrograms, ShaderEmitterUse[] Emitters);

/// <summary>A catalogue of the shader programs in the shipped BotW and TotK effect files.</summary>
public sealed class ShaderDatabase
{
    public const int CurrentVersion = 1;

    private static readonly JsonSerializerOptions Json = new() { WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.Never };

    public int Version { get; init; } = CurrentVersion;
    public Dictionary<string, ProgramSignature> Programs { get; init; } = [];
    public List<ShaderFileEntry> Files { get; init; } = [];

    public IEnumerable<ShaderFileEntry> FilesOf(ShaderGame game) => Files.Where(f => f.Game == game);

    public ProgramSignature? Find(string id) => Programs.GetValueOrDefault(id);

    /// <summary>The program an emitter's graphics variation index selects, or null for an out-of-range or unused (-1) index.</summary>
    public ProgramSignature? Resolve(ShaderFileEntry file, int variation) =>
        variation >= 0 && variation < file.Programs.Length ? Find(file.Programs[variation]) : null;

    public void Save(string path)
    {
        using var file = File.Create(path);
        using var gzip = new GZipStream(file, CompressionLevel.Optimal);
        JsonSerializer.Serialize(gzip, this, Json);
    }

    public static ShaderDatabase Load(string path)
    {
        using var file = File.OpenRead(path);
        using var gzip = new GZipStream(file, CompressionMode.Decompress);
        ShaderDatabase db = JsonSerializer.Deserialize<ShaderDatabase>(gzip, Json) ?? throw new InvalidDataException("Empty shader database.");
        if (db.Version != CurrentVersion) throw new InvalidDataException($"Shader database version {db.Version} is not supported (expected {CurrentVersion}).");
        return db;
    }
}
