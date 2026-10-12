using PtclSharp.Layout;

namespace PtclSharp.Simulation.Resources;

/// <summary>Turns the EMTR/ESET nodes of one game's effect file into the neutral resource model. TotK adds its own implementation.</summary>
public interface IEmitterResourceReader
{
    PtclVersion Version { get; }
    EmitterSetResource ReadSet(VfxbFile file, VfxbEmitterSet set);
}

public static class EmitterResourceReaders
{
    public static IEmitterResourceReader For(PtclVersion version) => version switch
    {
        PtclVersion.BotW_NintendoWareVfx_4_4_0 => new BotwEmitterResourceReader(),
        _ => throw new NotSupportedException($"No simulation reader for {version} yet.")
    };
}
