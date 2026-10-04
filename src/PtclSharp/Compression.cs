namespace PtclSharp;
using Yaz0Sharp;
using ZstdSharp;

public class Compression
{
    public byte[] CompressSet(EmitterSet emitterSet, ReadOnlySpan<byte> zstdDictionary = default)
    {
        if (emitterSet.Version == PtclVersion.BotW_NintendoWareVfx_4_4_0)
        {
            return Yaz0.Compress(emitterSet.Data, Yaz0.GetAlignment(emitterSet.Data));
        }
        else if (emitterSet.Version == PtclVersion.TotK_NintendoWareVfx2_15_3_1)
        {
            using var compressor = new ZstdSharp.Compressor();
            if (!zstdDictionary.IsEmpty)
                compressor.LoadDictionary(zstdDictionary);
            System.Span<byte> compressedData = compressor.Wrap(emitterSet.Data);
            return compressedData.ToArray();
        }
        else
        {
            throw new PtclVersionException("PtclSharp does not recognize that emitter set version!");
        }
    }

    public byte[] DecompressSet(EmitterSet emitterSet, ReadOnlySpan<byte> zstdDictionary = default)
    {
        if (emitterSet.Version == PtclVersion.BotW_NintendoWareVfx_4_4_0)
        {
            return Yaz0.Decompress(emitterSet.Data);
        }
        else if (emitterSet.Version == PtclVersion.TotK_NintendoWareVfx2_15_3_1)
        {
            using var decompressor = new ZstdSharp.Decompressor();
            if (!zstdDictionary.IsEmpty)
                decompressor.LoadDictionary(zstdDictionary);
            System.Span<byte> decompressedData = decompressor.Unwrap(emitterSet.Data);
            return decompressedData.ToArray();
        }
        else
        {
            throw new PtclVersionException("PtclSharp does not recognize that emitter set version!");
        }
    }
}
