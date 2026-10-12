using BntxSharp;
using PtclSharp.Rendering.Imaging;
using Silk.NET.OpenGL;

namespace PtclSharp.Rendering.Assets;

/// <summary>
/// The textures an effect file names by GUID: its own BNTX archive first, then the game's resident effect resource, uploaded to GL the first time
/// something asks for them. The channel mapping each BNTX carries (a single-channel image is stored as red, then swizzled) is applied as a GL swizzle.
/// </summary>
public sealed unsafe class TextureCatalog
{
    readonly GL _gl;
    readonly ProceduralTextures _fallbacks;
    readonly Dictionary<ulong, (string Name, BntxTexture? Pixels)> _entries = [];
    readonly Dictionary<ulong, uint> _uploaded = [];

    public TextureCatalog(GL gl, ProceduralTextures fallbacks, PtclFile file, string? bootupPack, List<string> messages)
    {
        _gl = gl;
        _fallbacks = fallbacks;
        Messages = messages;
        Add(file, overwrite: true);
        if (bootupPack is null || !File.Exists(bootupPack))
            return;
        try
        {
            byte[]? resident = SarcReader.Extract(File.ReadAllBytes(bootupPack), "Effect/GameResident.sesetlist");
            if (resident is not null)
                Add(PtclFile.ReadSesetlist(resident), overwrite: false);
        }
        catch (Exception e) { Messages.Add($"resident: {e.Message}"); }
    }

    public List<string> Messages { get; }

    public string NameOf(ulong guid) => _entries.TryGetValue(guid, out var entry) ? entry.Name : $"0x{guid:X}";

    /// <summary>The GL texture for <paramref name="guid"/>, or a white one when the image is missing.</summary>
    public uint Get(ulong guid)
    {
        if (guid == ulong.MaxValue)
            return _fallbacks.White;
        if (_uploaded.TryGetValue(guid, out uint known))
            return known;
        uint handle = _fallbacks.White;
        if (_entries.TryGetValue(guid, out var entry) && entry.Pixels is { } bntx)
        {
            try { handle = Upload(bntx); }
            catch (Exception e) { Messages.Add($"texture {entry.Name}: {e.Message}"); }
        }
        else
            Messages.Add($"texture 0x{guid:X} is not in the catalog");
        return _uploaded[guid] = handle;
    }

    void Add(PtclFile file, bool overwrite)
    {
        BotwTextureTable table = BotwTextureTable.Read(file.Vfxb);
        var pixels = new Dictionary<string, BntxTexture>(StringComparer.OrdinalIgnoreCase);
        if (!table.Archive.IsEmpty)
        {
            try
            {
                foreach (BntxTexture texture in BntxFile.Load(table.Archive.Span).Textures)
                    pixels[texture.Name] = texture;
            }
            catch (Exception e) { Messages.Add($"textures: {e.Message}"); }
        }
        foreach (BotwTextureName name in table.Names)
            if (overwrite || !_entries.ContainsKey(name.Guid))
                _entries[name.Guid] = (name.Name, pixels.GetValueOrDefault(name.Name));
    }

    uint Upload(BntxTexture texture)
    {
        byte[] rgba = texture.ToRgba8(0, 0);
        bool srgb = texture.Format.ToString().EndsWith("SRGB", StringComparison.Ordinal);
        if (EffectDebug.DumpTextureDirectory is { } directory)
        {
            Directory.CreateDirectory(directory);
            PngWriter.Write(Path.Combine(directory, texture.Name + ".png"), rgba, texture.Width, texture.Height);
            Console.WriteLine($"tex {texture.Name} {texture.Format} {texture.Width}x{texture.Height} ch={string.Join(',', texture.ChannelTypes)}");
        }
        uint handle = _gl.GenTexture();
        _gl.ActiveTexture(TextureUnit.Texture15); // uploads must not disturb the units a draw has set up
        _gl.BindTexture(TextureTarget.Texture2D, handle);
        fixed (byte* p = rgba)
            _gl.TexImage2D(TextureTarget.Texture2D, 0, srgb ? InternalFormat.Srgb8Alpha8 : InternalFormat.Rgba8, (uint)texture.Width, (uint)texture.Height, 0,
                PixelFormat.Rgba, PixelType.UnsignedByte, p);
        _gl.GenerateMipmap(TextureTarget.Texture2D);

        var channels = texture.ChannelTypes;
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleR, (int)Swizzle(channels[0]));
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleG, (int)Swizzle(channels[1]));
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleB, (int)Swizzle(channels[2]));
        _gl.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureSwizzleA, (int)Swizzle(channels[3]));
        return handle;
    }

    static GLEnum Swizzle(ChannelType channel) => channel switch
    {
        ChannelType.Red => GLEnum.Red,
        ChannelType.Green => GLEnum.Green,
        ChannelType.Blue => GLEnum.Blue,
        ChannelType.Alpha => GLEnum.Alpha,
        ChannelType.Zero => GLEnum.Zero,
        ChannelType.One => GLEnum.One,
        _ => GLEnum.Red
    };
}
