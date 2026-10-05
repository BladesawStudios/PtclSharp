namespace PtclSharp.Conversion;

public enum ConversionSeverity
{
    /// <summary>A mapping decision worth knowing about (value clamped, field assumed equal).</summary>
    Info,

    /// <summary>Behavior will differ in the target game (a field or chunk has no counterpart).</summary>
    Warning,

    /// <summary>The result will not render correctly until the caller supplies more (shaders, meshes, textures).</summary>
    Error
}

public sealed record ConversionNote(ConversionSeverity Severity, string Scope, string Message)
{
    public override string ToString() => $"{Severity}: {Scope}: {Message}";
}

/// <summary>What a conversion did and what it could not carry over.</summary>
public sealed class ConversionReport
{
    private readonly List<ConversionNote> _notes = [];

    public IReadOnlyList<ConversionNote> Notes => _notes;
    public int EmittersConverted { get; internal set; }
    public int SetsConverted { get; internal set; }
    public int ChunksConverted { get; internal set; }
    public int ChunksDropped { get; internal set; }
    public int FieldsCopied { get; internal set; }

    public bool HasErrors => _notes.Any(n => n.Severity == ConversionSeverity.Error);

    internal void Add(ConversionSeverity severity, string scope, string message) => _notes.Add(new ConversionNote(severity, scope, message));

    public IEnumerable<ConversionNote> Of(ConversionSeverity severity) => _notes.Where(n => n.Severity == severity);

    /// <summary>The distinct messages with how many times each occurred (conversions repeat the same note per emitter).</summary>
    public IEnumerable<(ConversionSeverity Severity, string Message, int Count)> Summary() =>
        _notes.GroupBy(n => (n.Severity, n.Message)).Select(g => (g.Key.Severity, g.Key.Message, g.Count()))
            .OrderByDescending(x => x.Severity).ThenByDescending(x => x.Item3);
}
