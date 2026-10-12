// PtclMeshDump <effect.sesetlist> <Bootup.pack> <out.pmesh>
//
// Writes the draw meshes (G3D models) and volume primitives an effect file's emitters use, in a flat binary the viewer reads without
// BfresLibrary (the viewer's shader decompiler brings a different BfresLibrary build, so the two cannot share a process).
//
//   "PMSH" u32 version(2) u32 modelCount u32 primitiveCount
//   model:     u64 id, u32 vertexCount, u32 indexCount, u32 topology (0 triangles, 1 strip, 2 lines), then vertexCount x
//              (pos, normal, tangent, color, uv0, uv1) as 6 float4, then indexCount u32
//   primitive: u64 id, u32 vertexCount, then vertexCount x (position float3, normal float3)
using System.Text;
using BfresLibrary;
using BfresLibrary.Helpers;
using PtclSharp;
using PtclSharp.Models;
using PtclSharp.Simulation.Resources;

if (args.Length < 3) { Console.Error.WriteLine("usage: PtclMeshDump <effect.sesetlist> <Bootup.pack> <out.pmesh>"); return 1; }

PtclFile file = PtclFile.ReadSesetlist(File.ReadAllBytes(args[0]));
PtclFile? resident = null;
if (File.Exists(args[1]))
{
    byte[]? bytes = Sarc(File.ReadAllBytes(args[1]), "Effect/GameResident.sesetlist");
    if (bytes is not null) resident = PtclFile.ReadSesetlist(bytes);
}
var catalog = new BotwModelCatalog(file, resident);

var reader = EmitterResourceReaders.For(file.Version);
var models = new Dictionary<ulong, BotwModelLocation>();
var prims = new HashSet<ulong>();
foreach (VfxbEmitterSet set in file.Vfxb.EmitterSets)
    foreach (EmitterResource e in reader.ReadSet(file.Vfxb, set).AllEmitters)
    {
        if (e.G3dPrimitiveId != ulong.MaxValue && catalog.Find((uint)e.G3dPrimitiveId) is { } loc) models[e.G3dPrimitiveId] = loc;
        if (e.VolumePrimitiveId != ulong.MaxValue) prims.Add(e.VolumePrimitiveId);
    }

using var output = new MemoryStream();
using var w = new BinaryWriter(output);
w.Write(Encoding.ASCII.GetBytes("PMSH"));
w.Write(2u);
long countsAt = output.Position;
w.Write(0u); w.Write(0u);

uint modelCount = 0;
foreach (var (id, loc) in models)
{
    try
    {
        ResFile fres = loc.Content.Open();
        Model model = fres.Models[loc.Entry.Index];
        Shape shape = model.Shapes[0];
        VertexBufferHelper helper = new(model.VertexBuffers[shape.VertexBufferIndex], Syroot.BinaryData.ByteOrder.LittleEndian);
        Mesh mesh = shape.Meshes[0];
        uint[] indices = mesh.GetIndices().ToArray();
        int count = (int)model.VertexBuffers[shape.VertexBufferIndex].VertexCount;
        VertexBufferHelperAttrib? Find(params string[] names) => names.Select(n => helper.Attributes.FirstOrDefault(a => a.Name == n)).FirstOrDefault(a => a is not null);
        var pos = Find("_p0"); var nrm = Find("_n0"); var tan = Find("_t0"); var col = Find("_c0"); var uv0 = Find("_u0"); var uv1 = Find("_u1");
        w.Write(id); w.Write((uint)count); w.Write((uint)indices.Length);
        w.Write(mesh.PrimitiveType.ToString().Contains("Strip") ? 1u : mesh.PrimitiveType.ToString().Contains("Line") ? 2u : 0u);
        for (int i = 0; i < count; i++)
            foreach (var a in new[] { pos, nrm, tan, col, uv0, uv1 })
            {
                if (a is null) { w.Write(0f); w.Write(0f); w.Write(0f); w.Write(1f); continue; }
                var v = a.Data[i];
                w.Write(v.X); w.Write(v.Y); w.Write(v.Z); w.Write(v.W);
            }
        foreach (uint idx in indices) w.Write(idx);
        modelCount++;
        Console.WriteLine($"model {id:X}: {model.Name} vtx={count} idx={indices.Length} {mesh.PrimitiveType} attrs={string.Join(",", helper.Attributes.Select(a => a.Name))}");
    }
    catch (Exception e) { Console.Error.WriteLine($"model {id:X}: {e.Message}"); }
}

uint primCount = 0;
foreach (ulong id in prims)
{
    if (catalog.FindPrimitive(id) is not { } found) continue;
    byte[] node = found.Node; // header (0x20) + data
    int data = 0x20;
    int vtx = BitConverter.ToInt32(node, data + 8);
    int posOff = BitConverter.ToInt32(node, data + 0x3c), nrmOff = BitConverter.ToInt32(node, data + 0x40);
    if (vtx <= 0 || data + posOff + (vtx * 12) > node.Length) continue;
    w.Write(id); w.Write((uint)vtx);
    for (int i = 0; i < vtx; i++)
    {
        for (int k = 0; k < 3; k++) w.Write(BitConverter.ToSingle(node, data + posOff + (i * 12) + (k * 4)));
        for (int k = 0; k < 3; k++) w.Write(nrmOff > 0 && data + nrmOff + (vtx * 12) <= node.Length ? BitConverter.ToSingle(node, data + nrmOff + (i * 12) + (k * 4)) : 0f);
    }
    primCount++;
}

output.Position = countsAt;
w.Write(modelCount); w.Write(primCount);
w.Flush();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllBytes(args[2], output.ToArray());
Console.WriteLine($"{modelCount} models, {primCount} primitives -> {args[2]}");
return 0;

static byte[]? Sarc(byte[] sarc, string path)
{
    bool little = BitConverter.ToUInt16(sarc, 6) == 0xFEFF;
    ushort U16(int o) => little ? BitConverter.ToUInt16(sarc, o) : System.Buffers.Binary.BinaryPrimitives.ReadUInt16BigEndian(sarc.AsSpan(o));
    uint U32(int o) => little ? BitConverter.ToUInt32(sarc, o) : System.Buffers.Binary.BinaryPrimitives.ReadUInt32BigEndian(sarc.AsSpan(o));
    int dataStart = (int)U32(0x0C), sfat = U16(4), count = U16(sfat + 6), nodes = sfat + 12, names = nodes + (count * 16) + 8;
    for (int i = 0; i < count; i++)
    {
        int node = nodes + (i * 16), nameOffset = names + ((int)(U32(node + 4) & 0xFFFFFF) * 4);
        int end = Array.IndexOf(sarc, (byte)0, nameOffset);
        if (Encoding.UTF8.GetString(sarc, nameOffset, end - nameOffset) != path) continue;
        int start = dataStart + (int)U32(node + 8);
        return sarc.AsSpan(start, (int)U32(node + 12) - (int)U32(node + 8)).ToArray();
    }
    return null;
}
