using System.Diagnostics;

namespace PtclSharp.Rendering.Assets;

/// <summary>
/// Runs <c>PtclMeshDump</c> to turn an effect's G3D models and volume primitives into a .pmesh cache. The dump runs out of process because its BfresLibrary
/// cannot share a process with the one the shader decompiler brings.
/// </summary>
/// <remarks>
/// The tool is found at <see cref="ToolPath"/>, or in <c>ptcl-meshdump/</c> beside the host, or in a repository checkout's <c>tools/PtclMeshDump</c> build
/// output above the host. Caches are kept under the temp folder and rebuilt when the effect file is newer.
/// </remarks>
public static class MeshDumpTool
{
    /// <summary>The PtclMeshDump.dll to run; null looks for it.</summary>
    public static string? ToolPath { get; set; } = Environment.GetEnvironmentVariable("PTCL_MESHDUMP");

    /// <summary>Where caches are written.</summary>
    public static string CacheDirectory { get; set; } = Path.Combine(Path.GetTempPath(), "PtclSharp", "meshes");

    /// <summary>The cache for <paramref name="effectPath"/>, dumping it first when it is missing or stale.</summary>
    public static string EnsureCache(string effectPath, string bootupPack)
    {
        string cache = Path.Combine(CacheDirectory, Path.GetFileNameWithoutExtension(effectPath) + ".pmesh");
        if (File.Exists(cache) && File.GetLastWriteTimeUtc(cache) >= File.GetLastWriteTimeUtc(effectPath))
            return cache;

        string tool = Locate() ?? throw new FileNotFoundException("PtclMeshDump has not been built; set MeshDumpTool.ToolPath or PTCL_MESHDUMP");
        Directory.CreateDirectory(CacheDirectory);
        var start = new ProcessStartInfo("dotnet") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        foreach (string argument in new[] { tool, effectPath, bootupPack, cache })
            start.ArgumentList.Add(argument);
        using Process process = Process.Start(start)!;
        process.StandardOutput.ReadToEnd();
        process.StandardError.ReadToEnd();
        process.WaitForExit();
        if (process.ExitCode != 0 || !File.Exists(cache))
            throw new InvalidOperationException("mesh dump failed");
        return cache;
    }

    static string? Locate()
    {
        if (ToolPath is not null && File.Exists(ToolPath))
            return ToolPath;
        string beside = Path.Combine(AppContext.BaseDirectory, "ptcl-meshdump", "PtclMeshDump.dll");
        if (File.Exists(beside))
            return beside;

        // A checkout: tools/PtclMeshDump/bin/<config>/net10.0 in any folder above the host's.
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            string? built = new[] { "Debug", "Release" }
                .Select(config => Path.Combine(directory.FullName, "tools", "PtclMeshDump", "bin", config, "net10.0", "PtclMeshDump.dll"))
                .Where(File.Exists)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .FirstOrDefault();
            if (built is not null)
                return built;
        }
        return null;
    }
}
