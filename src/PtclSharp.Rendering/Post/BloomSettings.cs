namespace PtclSharp.Rendering.Post;

/// <summary>What the bloom pass keeps and how strongly it adds it back.</summary>
public sealed class BloomSettings
{
    public bool Enabled { get; set; } = true;

    /// <summary>Brightness (HDR, before exposure) above which a pixel starts to glow.</summary>
    public float Threshold { get; set; } = 1.2f;

    /// <summary>Width of the soft transition below the threshold.</summary>
    public float Knee { get; set; } = 0.5f;

    /// <summary>How much of the blurred image is added to the scene.</summary>
    public float Intensity { get; set; } = 0.35f;

    /// <summary>Spread of the upsampling filter, in texels of the level being read; larger is a wider, softer glow.</summary>
    public float Radius { get; set; } = 1.0f;

    /// <summary>Number of blur levels, each half the size of the one before.</summary>
    public int Levels { get; set; } = 6;
}
