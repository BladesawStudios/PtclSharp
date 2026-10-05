using PtclSharp.Conversion;
using PtclSharp.Layout;

namespace PtclSharp.Shaders;

/// <summary>A BotW emitter and the shader programs it selects, which is what the donor search compares against.</summary>
/// <param name="Data">The emitter's data block (used to tell which of the fields a program reads are actually set).</param>
public sealed record BotwEmitterShaders(
    string Name,
    byte[] Data,
    ProgramSignature? Normal,
    ProgramSignature? Pass1,
    ProgramSignature? Pass2,
    ProgramSignature? Compute0);

/// <summary>A TotK emitter that could donate its shader selection.</summary>
/// <param name="Score">0 to 1. Zero means the donor cannot work (the texture slots it samples differ from the source's).</param>
/// <param name="Notes">What differs, in words: lost features, extra reads, custom-shader mismatches.</param>
public sealed record DonorCandidate(ShaderFileEntry File, int EmitterIndex, ShaderEmitterUse Emitter, double Score, IReadOnlyList<string> Notes);

/// <summary>The best donor for one source emitter inside the chosen donor file (null when nothing in that file can work).</summary>
public sealed record EmitterDonor(BotwEmitterShaders Source, DonorCandidate? Donor);

/// <summary>A donor file (its shader archive is reused wholesale) and the donor emitter chosen for every source emitter.</summary>
/// <param name="Score">Mean score over the source emitters.</param>
/// <param name="Ceiling">Mean of the best score each source emitter could get from any TotK file; the gap to <paramref name="Score"/> is what using one archive costs.</param>
public sealed record DonorPlan(ShaderFileEntry File, double Score, double Ceiling, IReadOnlyList<EmitterDonor> Emitters);

public sealed class DonorOptions
{
    /// <summary>Source emitters whose static-block fields are all zero count a read of that field this much (the effect does not use it).</summary>
    public double UnsetFieldWeight { get; init; } = 0.25;

    /// <summary>Recall counts this many times as much as precision: a donor that drops a feature the source uses is worse than one that reads extra fields.</summary>
    public double RecallBias { get; init; } = 2.0;
}

/// <summary>
/// Finds TotK shader programs that behave like a BotW program. Samplers are a hard requirement (a program that samples texture slot 2
/// needs a texture there, and one that does not will ignore it); the rest is a weighted comparison of which emitter fields the
/// programs read, after translating BotW field names to TotK ones with <see cref="BotwToTotkConverter.TotkFieldFor"/>. Fields that
/// nearly every program reads (colour, size) weigh little; rare ones (scrolling, flipbooks, soft-particle fade) decide the match.
/// </summary>
public sealed class DonorFinder
{
    private static readonly PtclLayoutSet Botw = PtclLayouts.For(PtclVersion.BotW_NintendoWareVfx_4_4_0);
    private static readonly PtclLayoutSet Totk = PtclLayouts.For(PtclVersion.TotK_NintendoWareVfx2_15_3_1);

    private sealed record Donor(ShaderFileEntry File, int Index, ShaderEmitterUse Emitter, ProgramSignature? Normal, ProgramSignature? Pass1, ProgramSignature? Pass2, ProgramSignature? Compute0);

    private readonly ShaderDatabase _database;
    private readonly DonorOptions _options;
    private readonly Dictionary<string, int> _fieldIds = [];
    private readonly Dictionary<string, int[]> _programFields = [];
    private readonly List<Donor> _donors = [];
    private double[] _idf = [];

    public DonorFinder(ShaderDatabase database, DonorOptions? options = null)
    {
        _database = database;
        _options = options ?? new DonorOptions();

        foreach (ShaderFileEntry file in database.FilesOf(ShaderGame.TotK))
            for (int i = 0; i < file.Emitters.Length; i++)
            {
                ShaderEmitterUse e = file.Emitters[i];
                ProgramSignature? normal = database.Resolve(file, e.Normal);
                if (normal is null) continue; // compute-only or unused emitters cannot donate a drawing shader
                _donors.Add(new Donor(file, i, e, normal, database.Resolve(file, e.Pass1), database.Resolve(file, e.Pass2), ResolveCompute(file, e.Compute0)));
            }

        // Field sets of every distinct TotK program, and how common each field is among them.
        var documentFrequency = new Dictionary<int, int>();
        var distinct = _donors.SelectMany(d => new[] { d.Normal, d.Pass1, d.Pass2, d.Compute0 }).OfType<ProgramSignature>().DistinctBy(p => p.Id).ToList();
        foreach (ProgramSignature p in distinct)
        {
            int[] fields = p.AllReads.Select(o => FieldIdAt(Totk, o)).Where(id => id >= 0).Distinct().Order().ToArray();
            _programFields[p.Id] = fields;
            foreach (int f in fields) documentFrequency[f] = documentFrequency.GetValueOrDefault(f) + 1;
        }
        _idf = new double[_fieldIds.Count];
        foreach ((int field, int count) in documentFrequency)
            _idf[field] = Math.Log((distinct.Count + 1.0) / (count + 1.0)) + 0.05;
    }

    public ShaderDatabase Database => _database;

    private ProgramSignature? ResolveCompute(ShaderFileEntry file, int index) =>
        index >= 0 && index < file.ComputePrograms.Length ? _database.Find(file.ComputePrograms[index]) : null;

    private int FieldIdAt(PtclLayoutSet layouts, int offset)
    {
        FieldDef? def = layouts.Emitter.FieldAt(offset);
        if (def is null) return -1;
        return Intern(def.Name);
    }

    private int Intern(string totkFieldName)
    {
        if (!_fieldIds.TryGetValue(totkFieldName, out int id)) _fieldIds[totkFieldName] = id = _fieldIds.Count;
        return id;
    }

    /// <summary>Reads the shader selection of every emitter of a BotW effect file from the database; null when the file is not in it.</summary>
    public IReadOnlyList<BotwEmitterShaders>? SourceEmitters(string botwFileName, PtclFile botw)
    {
        ShaderFileEntry? file = _database.FilesOf(ShaderGame.BotW).FirstOrDefault(f => string.Equals(f.Name, botwFileName, StringComparison.OrdinalIgnoreCase));
        if (file is null) return null;
        VfxbEmitter[] emitters = botw.Vfxb.EmitterSets.SelectMany(s => s.Emitters).ToArray();
        if (emitters.Length != file.Emitters.Length) return null;

        var result = new List<BotwEmitterShaders>();
        for (int i = 0; i < emitters.Length; i++)
        {
            ShaderEmitterUse use = file.Emitters[i];
            int offset = emitters[i].Node.DataOffset ?? throw new InvalidDataException("EMTR without data.");
            byte[] data = botw.Vfxb.Data.AsSpan(offset, Botw.Emitter.Size).ToArray();
            result.Add(new BotwEmitterShaders(emitters[i].Name, data, _database.Resolve(file, use.Normal), _database.Resolve(file, use.Pass1),
                _database.Resolve(file, use.Pass2), ResolveCompute(file, use.Compute0)));
        }
        return result;
    }

    /// <summary>The best donors for one source emitter across all of TotK, best first.</summary>
    public IReadOnlyList<DonorCandidate> Rank(BotwEmitterShaders source, int top = 10)
    {
        var scorer = new Scorer(this, source);
        return _donors.Select(d => scorer.Score(d)).Where(c => c.Score > 0)
            .OrderByDescending(c => c.Score).ThenBy(c => c.File.Name, StringComparer.Ordinal).ThenBy(c => c.EmitterIndex)
            .Take(top).ToList();
    }

    /// <summary>
    /// Chooses the TotK file whose shader archive serves the whole effect best, and the donor emitter inside it for every source emitter.
    /// An effect has to live in one archive, so this maximises the mean over all emitters rather than each emitter alone.
    /// </summary>
    public IReadOnlyList<DonorPlan> Plan(IReadOnlyList<BotwEmitterShaders> sources, int top = 5)
    {
        if (sources.Count == 0) return [];
        var scorers = sources.Select(s => new Scorer(this, s)).ToArray();
        var perFile = new Dictionary<ShaderFileEntry, DonorCandidate?[]>();
        var ceiling = new double[sources.Count];

        foreach (Donor donor in _donors)
        {
            if (!perFile.TryGetValue(donor.File, out DonorCandidate?[]? best)) perFile[donor.File] = best = new DonorCandidate?[sources.Count];
            for (int i = 0; i < scorers.Length; i++)
            {
                DonorCandidate candidate = scorers[i].Score(donor);
                if (candidate.Score <= 0) continue;
                if (best[i] is null || candidate.Score > best[i]!.Score) best[i] = candidate;
                ceiling[i] = Math.Max(ceiling[i], candidate.Score);
            }
        }

        double ceilingMean = ceiling.Average();
        return perFile
            .Select(kv => new DonorPlan(kv.Key, kv.Value.Average(c => c?.Score ?? 0), ceilingMean,
                sources.Select((s, i) => new EmitterDonor(s, kv.Value[i])).ToList()))
            .OrderByDescending(p => p.Score).ThenBy(p => p.File.Name, StringComparer.Ordinal)
            .Take(top).ToList();
    }

    /// <summary>Scores donors for one source emitter. Program-pair results are cached, since donors share programs heavily.</summary>
    private sealed class Scorer
    {
        private readonly DonorFinder _finder;
        private readonly BotwEmitterShaders _source;
        private readonly Dictionary<(string Source, string Donor), (double Score, string[] Notes)> _cache = [];
        private readonly Dictionary<string, SourceFields> _sourceFields = [];

        private sealed record SourceFields(Dictionary<int, double> Weights, List<string> Untranslated);

        internal Scorer(DonorFinder finder, BotwEmitterShaders source)
        {
            _finder = finder;
            _source = source;
        }

        internal DonorCandidate Score(Donor donor)
        {
            var notes = new List<string>();
            double total = 0, weight = 0;

            // The normal pass decides everything: no match on texture slots means no donor.
            (double normal, string[] normalNotes) = Compare(_source.Normal, donor.Normal, requireSlots: true);
            if (normal <= 0) return new DonorCandidate(donor.File, donor.Index, donor.Emitter, 0, normalNotes);
            total += normal;
            weight += 1;
            notes.AddRange(normalNotes);

            foreach ((string role, ProgramSignature? from, ProgramSignature? to) in new[]
                     {
                         ("pass1", _source.Pass1, donor.Pass1), ("pass2", _source.Pass2, donor.Pass2), ("compute", _source.Compute0, donor.Compute0)
                     })
            {
                if (from is null && to is null) continue;
                weight += 0.5;
                if (from is null) { notes.Add($"donor adds a {role} program the source does not have"); continue; }
                if (to is null) { notes.Add($"donor has no {role} program"); continue; }
                (double s, string[] n) = Compare(from, to, requireSlots: false);
                total += 0.5 * s;
                notes.AddRange(n.Select(x => $"{role}: {x}"));
            }
            return new DonorCandidate(donor.File, donor.Index, donor.Emitter, total / weight, notes);
        }

        private (double Score, string[] Notes) Compare(ProgramSignature? from, ProgramSignature? to, bool requireSlots)
        {
            if (from is null) return (0.5, ["source program unknown; matched on nothing but the donor's existence"]);
            if (to is null) return (0, []);
            if (_cache.TryGetValue((from.Id, to.Id), out var cached)) return cached;
            return _cache[(from.Id, to.Id)] = Evaluate(from, to, requireSlots);
        }

        private (double, string[]) Evaluate(ProgramSignature from, ProgramSignature to, bool requireSlots)
        {
            if (requireSlots && from.TextureSlotMask != to.TextureSlotMask)
                return (0, [$"samples texture slots {Slots(to.TextureSlotMask)} but the source samples {Slots(from.TextureSlotMask)}"]);

            var notes = new List<string>();
            SourceFields source = FieldsOf(from);
            int[] donor = _finder._programFields.GetValueOrDefault(to.Id) ?? [];
            double covered = 0, sourceTotal = source.Weights.Values.Sum(), donorTotal = 0, sharedUnweighted = 0;
            var lost = new List<(string Name, double Weight)>();
            foreach (int f in donor) donorTotal += _finder._idf[f];
            foreach ((int f, double w) in source.Weights)
            {
                if (Array.BinarySearch(donor, f) >= 0)
                {
                    covered += w;
                    sharedUnweighted += _finder._idf[f];
                }
                else lost.Add((_finder._fieldIds.First(kv => kv.Value == f).Key, w));
            }
            double recall = sourceTotal <= 0 ? 1 : covered / sourceTotal;
            double precision = donorTotal <= 0 ? 1 : sharedUnweighted / donorTotal;
            double b2 = _finder._options.RecallBias * _finder._options.RecallBias;
            double reads = recall + precision <= 0 ? 0 : (1 + b2) * precision * recall / ((b2 * precision) + recall);

            foreach ((string name, double w) in lost.OrderByDescending(l => l.Weight).Take(4))
                notes.Add($"donor never reads '{name}'");
            if (source.Untranslated.Count > 0)
                notes.Add($"source reads {source.Untranslated.Count} field(s) TotK does not have ({string.Join(", ", source.Untranslated.Take(3))}{(source.Untranslated.Count > 3 ? ", ..." : "")})");

            double inputs = Jaccard(from.VertexInputs, to.VertexInputs);
            double depth = from.UsesDepthBuffer == to.UsesDepthBuffer ? 1 : 0;
            if (depth == 0) notes.Add(to.UsesDepthBuffer ? "donor fades against the depth buffer (soft particles), the source does not" : "source fades against the depth buffer (soft particles), the donor does not");
            double custom = from.IsCustom == to.IsCustom ? 1 : from.IsCustom ? 0.5 : 0;
            if (to.IsCustom) notes.Add("donor is a custom shader; its custom parameters (CSDP) come along unchanged");
            if (from.IsCustom) notes.Add("source is a custom shader whose behaviour cannot be carried over");

            double score = (0.55 * reads) + (0.15 * inputs) + (0.15 * depth) + (0.15 * custom);
            return (score, notes.ToArray());
        }

        private SourceFields FieldsOf(ProgramSignature program)
        {
            if (_sourceFields.TryGetValue(program.Id, out SourceFields? known)) return known;
            var weights = new Dictionary<int, double>();
            var untranslated = new SortedSet<string>(StringComparer.Ordinal);
            foreach (int offset in program.AllReads.Distinct())
            {
                FieldDef? def = Botw.Emitter.FieldAt(offset);
                if (def is null) continue;
                bool set = _source.Data.AsSpan(def.Offset, def.ByteLength).ContainsAnyExcept((byte)0);
                string? target = BotwToTotkConverter.TotkFieldFor(def.Name);
                if (target is null)
                {
                    if (set) untranslated.Add(def.Name);
                    continue;
                }
                int id = _finder.Intern(target);
                if (id >= _finder._idf.Length) Array.Resize(ref _finder._idf, id + 1); // a field no TotK program reads: weight stays at its floor
                double w = Math.Max(_finder._idf[id], 0.05) * (set ? 1 : _finder._options.UnsetFieldWeight);
                weights[id] = Math.Max(weights.GetValueOrDefault(id), w);
            }
            return _sourceFields[program.Id] = new SourceFields(weights, untranslated.ToList());
        }

        private static double Jaccard(string[] a, string[] b)
        {
            if (a.Length == 0 && b.Length == 0) return 1;
            int common = a.Intersect(b).Count();
            return (double)common / (a.Length + b.Length - common);
        }

        private static string Slots(int mask) => mask == 0 ? "none" : string.Join(",", Enumerable.Range(0, 8).Where(i => (mask & (1 << i)) != 0));
    }
}
