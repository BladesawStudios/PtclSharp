namespace PtclSharp;

public class EmitterSet
{
    public byte[] Data { get; set; } = Array.Empty<byte>();

    public EmitterSet(Version version)
    {
        Version = version;
    }

    public Version Version { get; }
}