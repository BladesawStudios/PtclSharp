namespace PtclSharp;

/// <summary>Legacy byte-oriented wrapper. New code should use <see cref="PtclFile"/>.</summary>
public class EmitterSet
{
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public EmitterSet(PtclVersion version)
    {
        Version = version;
    }

    public PtclVersion Version { get; }
}
