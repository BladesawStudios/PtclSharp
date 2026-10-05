using BntxSharp;
using TxtgSharp;

namespace PtclSharp.Textures;

/// <summary>A converted texture: the <c>.txtg</c> bytes and anything about the conversion the caller should know.</summary>
public sealed record TxtgConversion(byte[] Bytes, IReadOnlyList<string> Notes);

/// <summary>Turns a BotW (BNTX) texture into a TotK TexToGo (<c>.txtg</c>) file.</summary>
public static class BntxToTxtg
{
    /// <summary>
    /// Format mapping, chosen from the 289 textures BotW and TotK share by name (the raw codes TotK files use for them):
    /// BC1 (either colour space) to 0x202, BC3 to 0x505, BC4 to 0x606, BC5 (unorm or snorm) to 0x707, R8 to 0xC0C, RG8 to
    /// 0xB0B, RGBA8 to 0xA0A.
    /// </summary>
    public static bool TryMapFormat(SurfaceFormat format, out TxtgFormat target, out bool signedToUnsigned)
    {
        signedToUnsigned = format is SurfaceFormat.BC4_SNORM or SurfaceFormat.BC5_SNORM;
        target = format switch
        {
            SurfaceFormat.BC1_UNORM or SurfaceFormat.BC1_SRGB => TxtgFormat.Bc1Unorm,
            SurfaceFormat.BC3_UNORM or SurfaceFormat.BC3_SRGB => TxtgFormat.Bc3UnormSrgb,
            SurfaceFormat.BC4_UNORM or SurfaceFormat.BC4_SNORM => TxtgFormat.Bc4Unorm,
            SurfaceFormat.BC5_UNORM or SurfaceFormat.BC5_SNORM => TxtgFormat.Bc5Unorm,
            SurfaceFormat.BC7_UNORM or SurfaceFormat.BC7_SRGB => TxtgFormat.Bc7Unorm,
            SurfaceFormat.R8_UNORM => TxtgFormat.R8Unorm,
            SurfaceFormat.R8_G8_UNORM => TxtgFormat.R8G8Unorm,
            SurfaceFormat.R8_G8_B8_A8_UNORM or SurfaceFormat.R8_G8_B8_A8_SRGB => TxtgFormat.R8G8B8A8Unorm,
            _ => TxtgFormat.Unknown
        };
        return target != TxtgFormat.Unknown;
    }

    public static TxtgConversion Convert(BntxTexture texture, int compressionLevel = 12)
    {
        if (!TryMapFormat(texture.Format, out TxtgFormat format, out bool snorm))
            throw new NotSupportedException($"BNTX format {texture.Format} has no TexToGo counterpart.");
        if (texture.Depth > 1 || texture.SurfaceDim is not (SurfaceDim.Dim2D or SurfaceDim.Dim2DArray))
            throw new NotSupportedException($"'{texture.Name}' is not a plain 2D texture ({texture.SurfaceDim}).");

        var notes = new List<string>();
        int layers = Math.Max(1, texture.ArrayLength);
        var surfaces = new List<TxtgSurfaceData>();
        for (int layer = 0; layer < layers; layer++)
            for (int mip = 0; mip < texture.MipCount; mip++)
            {
                byte[] data = texture.GetDeswizzledData(mip, layer);
                if (snorm) SignedBlocksToUnsigned(data, texture.Format == SurfaceFormat.BC5_SNORM ? 16 : 8);
                surfaces.Add(new TxtgSurfaceData(layer, mip, data));
            }
        if (snorm)
            notes.Add($"'{texture.Name}' is {texture.Format}; its block endpoints were remapped to unsigned. Not verified in game: TotK's own normal maps use the same 0x707 code, but how its shaders sample them was not checked.");

        TxtgFile file = TxtgFile.Create(texture.Width, texture.Height, format, surfaces);
        byte[] bytes = file.ToBytes(compressionLevel);
        // Header bytes 0x18..0x1B are the channel selectors: 0..3 pick R, G, B, A, 4 is zero, 5 is one. TotK's own R/G normal
        // maps carry 0 1 4 5, which is the BNTX channel order Red, Green, Zero, One that BotW's carry too.
        for (int c = 0; c < 4; c++)
            bytes[0x18 + c] = texture.ChannelTypes[c] switch
            {
                ChannelType.Red => 0,
                ChannelType.Green => 1,
                ChannelType.Blue => 2,
                ChannelType.Alpha => 3,
                ChannelType.Zero => 4,
                _ => 5
            };
        return new TxtgConversion(bytes, notes);
    }

    /// <summary>
    /// BC4/BC5 store one or two 8-value channels as two endpoints and 3-bit indices per 4x4 block. The signed variants read the
    /// endpoints as int8 (-128 clamps to -127) and the decoded values in [-1, 1]; interpolation is linear, so mapping each
    /// endpoint with (v + 127) * 255 / 254 turns the block into the unsigned encoding of (v + 1) / 2.
    /// </summary>
    private static void SignedBlocksToUnsigned(byte[] data, int blockSize)
    {
        for (int block = 0; block + blockSize <= data.Length; block += blockSize)
            for (int channel = 0; channel < blockSize; channel += 8)
            {
                int at = block + channel;
                data[at] = Remap((sbyte)data[at]);
                data[at + 1] = Remap((sbyte)data[at + 1]);
            }
    }

    private static byte Remap(sbyte value)
    {
        int v = Math.Max((int)value, -127);
        return (byte)Math.Clamp((int)Math.Round((v + 127) * 255.0 / 254.0), 0, 255);
    }
}
