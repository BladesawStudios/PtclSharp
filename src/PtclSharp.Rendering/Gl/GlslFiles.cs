using System.Reflection;

namespace PtclSharp.Rendering.Gl;

/// <summary>The library's own GLSL, embedded as <c>Glsl/&lt;name&gt;.glsl</c>.</summary>
internal static class GlslFiles
{
    public static string Load(string name)
    {
        string resource = $"Glsl/{name}.glsl";
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resource)
            ?? throw new FileNotFoundException($"Embedded shader '{resource}' is missing.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
