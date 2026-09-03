# Prerequisites

Shared libraries that generated starter scripts **call**. These are not examples — nothing the
scaffolder produces compiles until they exist in the target tenant.

Not every tenant has them. A fresh QA or customer tenant often has none, and an older one can hold
a revision predating helpers the templates call. This bundle exists so a missing library is a
two-minute fix rather than a request to whoever has a tenant that does have it.

Exported 2026-09-01 from `demo\sales`, highest published (`P`) revision of each.

## Check before generating

```powershell
pwsh -File "${CLAUDE_PLUGIN_ROOT}/shared/scripts/Test-Prerequisites.ps1" -IncludeReporting -IncludeAi -OutDir .\prereqs
```

It resolves the tenant from the registry, reports `OK` / `OLDER` / `MISSING` per library, and with
`-OutDir` stages the source of anything missing so it can be created in PRO. Add `-IncludeStale` to
stage older ones too. Pass `-CachePath` or `-Tenant` to target a specific tenant.

## What is here

| Library | Tier | Declare as | Needed by |
|---|---|---|---|
| `_Browser` | core | `ExtensionScript` | Every browser-driving script. ~98 KB. |
| `Browser.Manager.BrowserManager` | core | `IDisposable` | Every browser-driving script. |
| `_Premium` | reporting | `ExtensionScript` | `DictateSection`, `ReturnTo{System}` — `GetWritelock`, `SetupDocument`. |
| `CapResultParser._IAiResultSource` | ai | `ExtensionScript` | The adapter contract. |
| `CapResultParser._CapTypes` | ai | `ExtensionScript` | Shared CAP types. |
| `CapResultParser._TemplateDetector` | ai | `ExtensionScript` | `GetActivePartKey`, `GetMappingForPartKey`. |
| `CapResultParser._CapEngine` | ai | `ExtensionScript` | `_CapEngine.Run`. |

Core is always required. Reporting is required only with `-Reporting`, AI only with `-AiResulting`.

`WindowTools` is not here — it ships with PRO rather than being a starter script.

## The declaration matters

`BrowserManager` declares `IDisposable`, **not** `ExtensionScript`. Everything else here is an
`ExtensionScript`. If you are creating these through the PRO scripting MCP, `pro_script_create`
requires the exact base-class declaration rather than a category, so take it from the table above
or from each file's header comment.

## Staleness is not always benign

`OLDER` means the tenant has the library at a lower revision than this bundle. It may be fine, but
the templates were written against the bundled revision and call helpers that may not exist in an
older one — `IsVisiblyRendered`, `WaitForElement`, `Invoke` and `FindByPath` were all added over
time. Check the helpers your scripts actually call before assuming an older copy will do.

Do not edit these files. The source of truth is whatever is published in PRO; this is a snapshot
for seeding a tenant that lacks one.
