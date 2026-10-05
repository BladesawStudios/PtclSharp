using System.Security.Cryptography;
using PtclSharp.Conversion;
using PtclSharp.Layout;
using PtclSharp.Writer;

namespace PtclSharp.Shaders;

/// <summary>Turns a <see cref="DonorPlan"/> into the shader selection the converter needs.</summary>
public static class DonorBinding
{
    private static readonly PtclLayoutSet Totk = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);

    /// <summary>
    /// Builds <see cref="ConverterOptions.ShaderBinder"/> for the plan: each source emitter (found by the hash of its data block) gets the
    /// shader fields and custom-shader parameters of its donor emitter inside <paramref name="donorDocument"/>, which must be the plan's file.
    /// Emitters the plan has no donor for get no binding, so the converter reports them as errors.
    /// </summary>
    public static Func<byte[], ShaderBinding?> CreateBinder(DonorPlan plan, VfxbDocument donorDocument)
    {
        var all = new List<VfxbTreeNode>();
        foreach (VfxbTreeNode set in donorDocument.Sets) Collect(set, all);

        var bindings = new Dictionary<string, ShaderBinding?>();
        foreach (EmitterDonor choice in plan.Emitters)
        {
            string key = Key(choice.Source.Data);
            if (bindings.ContainsKey(key) || choice.Donor is null) continue;
            VfxbTreeNode? node = Find(all, choice.Donor.Emitter);
            if (node is null) continue;
            byte[]? csdp = node.Attributes.FirstOrDefault(a => a.Kind == "CSDP")?.Data;
            bindings[key] = ShaderBinding.FromDonorEmitter(node.Data!, csdp);
        }
        return data => bindings.GetValueOrDefault(Key(data));
    }

    /// <summary>The donor emitter node of <paramref name="use"/>: same name and same normal-pass shader index.</summary>
    private static VfxbTreeNode? Find(List<VfxbTreeNode> emitters, ShaderEmitterUse use)
    {
        foreach (VfxbTreeNode node in emitters)
        {
            if (node.Data is not { } data) continue;
            var view = new StructView(Totk.Emitter, data);
            if (view.GetString("emitter_name") == use.Name && view.GetInt32("shader_idx_normal") == use.Normal) return node;
        }
        return null;
    }

    private static void Collect(VfxbTreeNode node, List<VfxbTreeNode> into)
    {
        foreach (VfxbTreeNode child in node.Children)
        {
            if (child.Kind == "EMTR") into.Add(child);
            Collect(child, into);
        }
    }

    // The emitter data block is the identity of a source emitter: the converter hands the binder exactly those bytes.
    private static string Key(byte[] data) => Convert.ToHexString(SHA1.HashData(data));
}
