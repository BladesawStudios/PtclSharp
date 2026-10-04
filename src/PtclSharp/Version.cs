namespace PtclSharp;

/// <summary>The game/runtime family that owns a particle resource.</summary>
public enum PtclVersion
{
    BotW_NintendoWareVfx_4_4_0 = 440,
    TotK_NintendoWareVfx2_15_3_1 = 1531
}

public sealed class PtclVersionException : Exception
{
    public PtclVersionException(string message) : base(message)
    {
    }
}
