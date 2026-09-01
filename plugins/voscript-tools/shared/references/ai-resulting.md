# AI resulting: the CAP biomarker pipeline

When a system publishes AI biomarker results, the integration pulls them into the active CAP
checklist in Report Builder instead of making the pathologist retype them.

## How the pieces fit

```
PullBiomarkerResults                       (per vendor, VOScript.Starter.<System>)
  |
  +-- _TemplateDetector.GetActivePartKey(SpeechBox)          shared
  |     -> which CAP template is open, keyed by the template's own "filename" field
  |
  +-- _TemplateDetector.GetMappingForPartKey(partKey)        shared, vendor-agnostic
  |     -> CAP questions, answer CKeys, range maps
  |
  +-- _<System>LabelRegistry.GetLabelsForPartKey(partKey)    per vendor
  |     -> Dictionary<biomarkerKey, on-screen label>, built by
  |        <Namespace>.Labels._<System><Template>Labels.Build()
  |
  +-- new <System>Adapter(manager)                           per vendor
  |     -> IAiResultSource: string ReadValueByLabel(string label)
  |
  +-- _CapEngine.Run(adapter, SpeechBox, mapping, labels)    shared
```

Only three of these are per-vendor: the entry-point command, the label overlay (plus its registry
entry), and the adapter. The mapping and the engine are shared and should not be forked.

`IAiResultSource` is a single method:

```csharp
/// Returns the raw string value associated with the given page label, or null if the label
/// isn't present. Implementations handle their own exceptions and log warnings, not throw -
/// a missing label is expected behavior, not an error.
string ReadValueByLabel(string label);
```

## Adding a vendor

1. `PullBiomarkerResults.cs` — copy the generated one. It only wires things together.
2. `_<System>Adapter.cs` — the only real work. Scrape the results panel once into a cache, serve
   `ReadValueByLabel` from it. Start from `_HaloAdapter.cs`, which is the most complete reference
   implementation, rather than from scratch.
3. `_<System>LabelRegistry.cs` — one dictionary entry per supported template.
4. `<Namespace>.Labels._<System><Template>Labels.cs` — the overlay, one entry per biomarker field.

## PartKey: get this right first

The registry key **must** be the value of the CAP template XML's own hidden `filename` field
(read-only, present on every CAP template), for example
`Breast.Bmk.169_1.010.001.REL_sdcFDF.xml`.

It is **not** the local export filename (`za25CAP-Jun25-BreastBmk169_2P-4831-1`). Those two
conventions differ and the mismatch has already broken three registries. Open the template XML,
read the `filename` field, paste it verbatim.

## Diagnosing "no values scraped"

The engine logs `No values scraped ... skipping biomarker` for two very different causes:

- The adapter genuinely could not read the panel. Its own summary line will show zero or few
  values scraped.
- The adapter scraped fine but **the label overlay has no entry for that biomarker.** The
  adapter's summary line will look healthy while one marker is skipped.

Check the adapter's own log line before debugging the scraper. A one-marker gap next to a healthy
scrape is almost always a missing dictionary entry.

## Things that are easy to get wrong

- **Window lookup.** `PullBiomarkerResults` runs against an open case, so it needs
  `FindCurrentTitlePageByRegex`, not `FindCurrentTitlePage`. Using the worklist lookup here has
  already shipped once.
- **Numeric scales.** A vendor's raw `1/2/3` intensity is not automatically Weak/Moderate/Strong.
  Verify against a real case before trusting the mapping.
- **Range maps.** A `RangeMap` with too few buckets silently maps values to the wrong CAP answer —
  it does not fail. Check every bucket against the CAP template XML, not against another
  biomarker's map.
- **Values the vendor reports with no CAP field to hold them** (e.g. a HER2 "Grade"). Do not
  invent a field. Flag it and leave it unmapped until a CKey is agreed.
- **Antibody / assay assumptions.** Where the vendor does not expose which antibody was used,
  record the hardcoded assumption in a comment rather than leaving it implicit.
