# PtclSharp normalized API design (draft)

## Principles

1. **Lossless first.** The original VFXB bytes are the source of truth. Typed access is a view over bytes, and the tree writer carries unknown node payloads forward unchanged.
2. **Evidence-gated fields.** A field only becomes a typed property when it has a `Confirmed` entry in the research docs. Everything else stays raw and is reachable by offset.
3. **One normalized model, two layouts.** Game differences live in `EmitterLayout` tables, not in model classes.
4. **Conversion is explicit and reports loss.** `Convert(source, target)` returns the document plus a `ConversionReport`.

## Layers

```text
Container (Yaz0 / Zstd+BYML)         exists: Container.cs
  -> VfxbFile / VfxbNode tree         exists: Vfxb.cs (lossless)
    -> EmitterView  (typed span view, layout-driven)       NEW
      -> EmitterModel (normalized, game-agnostic)          NEW
        -> VfxbTreeWriter(target layout)                   NEW
```

## Layout tables (the only game-specific code)

```csharp
public enum FieldStatus { Confirmed, Paired, Unverified }   // Paired = offset derived by delta, not decompiled in both games

public sealed record FieldDef(
    string Name, int Offset, FieldType Type, int Count, FieldStatus Status, string Evidence);

public sealed class EmitterLayout
{
    public VfxbLayout Vfxb { get; }
    public int TextureSlotCount { get; }        // BotW 3, TotK 6
    public int KeyframeTrackCount { get; }      // BotW 5 (6th TBD), TotK 10
    public IReadOnlyDictionary<string, FieldDef> Fields { get; }
    public bool Supports(EmitterFeature f);
}
```

A field absent from a layout means "not representable in this game". That is the single source for capability checks and conversion warnings.

## Typed view (zero-copy)

```csharp
public readonly ref struct EmitterView
{
    public EmitterLayout Layout { get; }
    public Span<byte> Data { get; }              // EMTR data block
    public string Name { get; set; }
    public float EmitRate { get; set; }
    public ShapeType Shape { get; set; }
    // ... property per Confirmed field, each does Layout.Fields[...] lookup
    public bool TryGet<T>(string field, out T value);   // by-name, includes Paired/Unverified
    public Span<byte> Raw(int offset, int length);      // escape hatch
}
```

## Normalized model (game-agnostic)

```csharp
public sealed class EmitterModel
{
    public string Name;
    public EmissionSettings Emission;       // start, duration, rate, rateRandom, interval, loopMode, infinite
    public ShapeSettings Shape;             // type, arc/elevation/phase, hollowRatio, radii, divisions
    public TransformSettings Transform;     // translation/rotation/scale (+random ranges)
    public ParticleSettings Particle;       // lifespan(+random), fadeIn/Out, scale(+random), gravity, spreadCone
    public ColorSettings Color;             // Color0/1, Alpha0/1: mode, constant, 8-key track, loop
    public KeyTrack<Vector3> Scale;         // plus Rotation, extra TotK tracks in ExtraTracks
    public RenderState Render;              // blend, depth, alpha test, cull
    public IList<TextureSlot> Textures;     // 0..N, N from layout
    public ShaderRefs Shaders;              // indices; target-specific, never auto-converted
    public RawPayload Unmapped;             // preserved bytes + attribute chunks
}

public sealed record KeyFrame<T>(float Time, T Value);
public sealed class KeyTrack<T> { public Mode Mode; public T Constant; public IList<KeyFrame<T>> Keys; /* max 8 */ public Loop Loop; }
```

## Document API

```csharp
var doc = PtclDocument.Load(path);              // detects game from VFXB version (20 / 51)
foreach (var set in doc.EmitterSets)
    foreach (var emtr in set.Emitters) { emtr.Emission.Rate *= 2; }
doc.Save(path);                                  // same-game: rebuilds the node tree, preserving raw payloads

var result = PtclConverter.Convert(doc, PtclGame.TotK);
result.Document.Save(outPath);
foreach (var w in result.Report.Warnings) Console.WriteLine(w);
```

## Conversion rules

| Situation | Behavior |
|---|---|
| Field exists in both layouts | Copy through model |
| Target has extra slots/tracks | Left at neutral defaults |
| Source has more than target | Dropped, `Warning: DroppedTexture(slot 4)` |
| Shader indices, primitive indices, texture GUIDs | Never auto-remapped; report requires a caller-supplied resolver |
| `Unverified` or unmapped bytes | Not converted; reported |

## Decided and open questions

The writer will rebuild the VFXB node tree. Unknown node payloads and
unverified byte ranges are retained as opaque data so rebuilding does not imply
semantic reconstruction.

Still open:

1. **Attribute chunks (`EA*`, `FR*`, `CSDP`)**: keep as opaque `RawChunk` for now, or trace them next?
2. **Strictness:** should `Unverified`/`Paired` fields be hidden from the typed API, or exposed with a flag?

## Prerequisite work before coding the model

- Reconcile the TotK loop-field contradiction (`0xE28..` vs `0xE78..`) with a direct decompile.
- Verify the BotW track count (5 vs 6) against `0x74`/`0x680`.
- Decompile the BotW counterpart for every `Paired` field.
