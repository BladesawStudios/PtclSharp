namespace PtclSharp;
using Yaz0Sharp;
using ZstdSharp;

public class Compression
{
    public byte[] CompressSet(EmitterSet emitterSet)
    {
        if (emitterSet.Version == Version.BotW)
        {
            return Yaz0.Compress(emitterSet.Data, Yaz0.GetAlignment(emitterSet.Data));
        }
        else if (emitterSet.Version == Version.TotK)
        {
            using var compressor = new ZstdSharp.Compressor();
            System.Span<byte> compressedData = compressor.Wrap(emitterSet.Data);
            return compressedData.ToArray();
        }
        else
        {
            throw new VersionException("PtclSharp does not recognize that emitter set version!");
        }
    }

    public byte[] DecompressSet(EmitterSet emitterSet)
    {
        if (emitterSet.Version == Version.BotW)
        {
            return Yaz0.Decompress(emitterSet.Data);
        }
        else if (emitterSet.Version == Version.TotK)
        {
            using var decompressor = new ZstdSharp.Decompressor();
            System.Span<byte> decompressedData = decompressor.Unwrap(emitterSet.Data);
            return decompressedData.ToArray();
        }
        else
        {
            throw new VersionException("PtclSharp does not recognize that emitter set version!");
        }
    }
}