using BfresLibrary;
using PtclSharp.Layout;
using PtclSharp.Writer;

namespace PtclSharp.Models;

public enum ModelDisposition
{
    /// <summary>TotK lists a model with this id: its own copy is embedded (the same asset, already in BFRES 10).</summary>
    ReuseTotk,

    /// <summary>TotK does not have it: the BotW model is converted.</summary>
    Convert,

    /// <summary>No table names the id (own or resident), or the model cannot be read.</summary>
    Unresolved
}

public sealed record ModelPlanItem(uint Id, ModelDisposition Disposition, TotkModelLocation? Totk, BotwModelLocation? Botw, string? Problem = null)
{
    public bool FromResident => Botw?.Resident == true;
}

/// <summary>The models one BotW effect file needs in TotK.</summary>
public sealed class ModelPlan
{
    public required IReadOnlyList<ModelPlanItem> Items { get; init; }

    /// <summary>Ids that end up in the converted file (pass to <c>ConverterOptions.CarriedModelIds</c>).</summary>
    public IReadOnlySet<ulong> CarriedIds => Items.Where(i => i.Disposition != ModelDisposition.Unresolved).Select(i => (ulong)i.Id).ToHashSet();

    public IEnumerable<ModelPlanItem> Unresolved => Items.Where(i => i.Disposition == ModelDisposition.Unresolved);
}

public sealed class ModelPlanOptions
{
    /// <summary>Use TotK's own copy of a model when it has one (default) instead of converting the BotW model.</summary>
    public bool PreferTotkModels { get; init; } = true;
}

public static class ModelPlanner
{
    /// <summary>Plans every model id the emitters of <paramref name="botw"/> use, in order of first use.</summary>
    public static ModelPlan Plan(PtclFile botw, BotwModelCatalog catalog, TotkModelIndex totk, ModelPlanOptions? options = null)
    {
        options ??= new ModelPlanOptions();
        var items = new List<ModelPlanItem>();
        var seen = new HashSet<uint>();
        foreach (VfxbEmitter emitter in botw.Vfxb.EmitterSets.SelectMany(s => s.Emitters))
        {
            ulong id = botw.Vfxb.EmitterView(emitter).GetUInt64("g3d_primitive_idx");
            if (id == ulong.MaxValue) continue;
            if (id > uint.MaxValue) { items.Add(new ModelPlanItem(unchecked((uint)id), ModelDisposition.Unresolved, null, null, "The id does not fit 32 bits.")); continue; }
            if (seen.Add((uint)id)) items.Add(PlanOne((uint)id, catalog, totk, options));
        }
        return new ModelPlan { Items = items };
    }

    public static ModelPlanItem PlanOne(uint id, BotwModelCatalog catalog, TotkModelIndex totk, ModelPlanOptions options)
    {
        BotwModelLocation? source = catalog.Find(id);
        if (options.PreferTotkModels && totk.TryFind(id, out TotkModelLocation? location))
            return new ModelPlanItem(id, ModelDisposition.ReuseTotk, location, source);
        if (source is null)
        {
            // No BotW copy: TotK's own is still better than nothing.
            return totk.TryFind(id, out location)
                ? new ModelPlanItem(id, ModelDisposition.ReuseTotk, location, null)
                : new ModelPlanItem(id, ModelDisposition.Unresolved, null, null, "No G3NT table (own or resident) names this id.");
        }
        return new ModelPlanItem(id, ModelDisposition.Convert, null, source);
    }
}

/// <summary>The G3D resource built for a plan: the FRES bytes and a G3NT entry per model, ready for <see cref="VfxbTail.BuildG3d"/>.</summary>
public sealed class G3dBuild
{
    public required byte[]? Resource { get; init; }
    public required IReadOnlyList<byte[]> Entries { get; init; }
    public required IReadOnlyList<string> Notes { get; init; }

    /// <summary>The model names in table order.</summary>
    public required IReadOnlyList<string> ModelNames { get; init; }

    /// <summary>Replaces the document's <c>G3PR</c> root with this resource.</summary>
    public void ApplyTo(VfxbDocument document) =>
        document.Tail = document.ParseTail().Replace(VfxbTail.BuildG3d(Resource, Entries)).ToBytes();
}

/// <summary>The mesh primitives (<c>PRMA</c>) one BotW effect file needs: TotK's own copy of a primitive where it has one, else BotW's node unchanged.</summary>
public sealed class PrimitivePlan
{
    public required IReadOnlyList<(ulong Id, byte[]? Node, string? Problem)> Items { get; init; }

    public IReadOnlySet<ulong> CarriedIds => Items.Where(i => i.Node is not null).Select(i => i.Id).ToHashSet();

    /// <summary>Replaces the document's <c>PRMA</c> root with the carried primitives.</summary>
    public void ApplyTo(VfxbDocument document) =>
        document.Tail = document.ParseTail().Replace(VfxbTail.BuildPrimitives(Items.Where(i => i.Node is not null).Select(i => i.Node!).ToList())).ToBytes();

    public static PrimitivePlan Plan(PtclFile botw, BotwModelCatalog catalog, TotkModelIndex totk, ModelPlanOptions? options = null)
    {
        options ??= new ModelPlanOptions();
        var items = new List<(ulong, byte[]?, string?)>();
        var seen = new HashSet<ulong>();
        foreach (VfxbEmitter emitter in botw.Vfxb.EmitterSets.SelectMany(s => s.Emitters))
        {
            StructView view = botw.Vfxb.EmitterView(emitter);
            foreach (ulong id in new[] { view.GetUInt64("mesh_primitive_idx"), view.GetUInt64("trim_primitive_idx") })
            {
                if (id == ulong.MaxValue || !seen.Add(id)) continue;
                if (options.PreferTotkModels && totk.TryFindPrimitive(id, out TotkModelLocation? location))
                {
                    items.Add((id, totk.OpenPrimitives(location.File).Primitives[location.Index].Node, null));
                    continue;
                }
                (ulong Id, byte[] Node)? own = catalog.FindPrimitive(id);
                items.Add(own is { } found ? (id, found.Node, null) : (id, null, "No PRMA (own or resident) holds this primitive."));
            }
        }
        return new PrimitivePlan { Items = items };
    }
}

public static class G3dBuilder
{
    /// <summary>
    /// Builds a TotK G3D resource holding every carried model of <paramref name="plan"/>, in plan order. TotK models are copied from the
    /// TotK files that have them; BotW models are converted with <see cref="BotwModelConverter"/>. The FRES header comes from a TotK
    /// file (<see cref="TotkModelIndex.TemplateFile"/>), so version and alignment are the shipped ones.
    /// </summary>
    public static G3dBuild Build(ModelPlan plan, TotkModelIndex totk)
    {
        var notes = new List<string>();
        var entries = new List<byte[]>();
        var names = new List<string>();
        var contents = new Dictionary<string, G3dContent>();
        G3dContent Content(string file) => contents.TryGetValue(file, out G3dContent? c) ? c : contents[file] = totk.OpenContent(file);

        string templateFile = totk.TemplateFile ?? throw new InvalidOperationException("The TotK index has no template file (no model with a dummy material).");
        ResFile output = Content(templateFile).Open();
        output.Models = new ResDict<Model>();

        foreach (ModelPlanItem item in plan.Items.Where(i => i.Disposition != ModelDisposition.Unresolved))
        {
            Model model;
            byte[] entry;
            if (item.Disposition == ModelDisposition.ReuseTotk)
            {
                G3dContent source = Content(item.Totk!.File);
                model = source.Open().Models[item.Totk.Index];
                entry = source.Entries[item.Totk.Index].Raw;
            }
            else
            {
                BotwModelLocation botw = item.Botw!;
                model = botw.Content.Open().Models[botw.Entry.Index];
                entry = botw.Entry.Raw;
                BotwModelConverter.Convert(model, FindDummy(Content(templateFile)), notes);
            }

            string name = model.Name;
            if (output.Models.ContainsKey(name)) model.Name = name = $"{name}_{item.Id:X8}";
            output.Models.Add(name, model);
            entries.Add(entry);
            names.Add(name);
        }

        if (entries.Count == 0)
            return new G3dBuild { Resource = null, Entries = [], Notes = notes, ModelNames = [] };

        using var ms = new MemoryStream();
        output.Save(ms);
        return new G3dBuild { Resource = ms.ToArray(), Entries = entries, Notes = notes, ModelNames = names };
    }

    private static Model FindDummy(G3dContent template) =>
        template.Open().Models.Values.FirstOrDefault(TotkModelIndex.IsDummyMaterial)
        ?? throw new InvalidOperationException("The template file has no model with a dummy material.");
}
