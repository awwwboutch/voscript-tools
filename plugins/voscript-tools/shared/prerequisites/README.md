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

## Revision numbers do not compare across tenants

PRO configuration is not centralized. A tenant is assembled from a starter export plus deltas
taken from whichever Voicebrook resource's configuration, so **`@1P` in one tenant is not the same
code as `@1P` in another**, and a higher number does not mean newer. `pro\sentara` carries
`_Premium@3P-15292-3` while `demo\sales` carries `@1P-2154-3` — unrelated lineages, not two points
on one history.

So the check ignores revision numbers and compares the code. It derives the set of members the
templates actually call — `FindElementOnPage`, `WaitForElement`, `IsVisiblyRendered`, `GetParent`
and the rest — by scanning `../templates/` at run time, then checks each is declared in the
tenant's copy. Four outcomes:

| Status | Meaning | Do |
|---|---|---|
| `IDENTICAL` | Same code as the bundled copy. | Nothing. |
| `DIFFERS-OK` | Different code, but every called member is present. | **Nothing.** Leave the tenant's copy alone — overwriting it could break scripts already there. |
| `INCOMPATIBLE` | Present, but missing members the templates call. The absent ones are listed. | Create the bundled copy, or rewrite the generated scripts to avoid those members. |
| `MISSING` | No published revision at all. | Create the bundled copy. |

`DIFFERS-OK` is the common case in a customer tenant and is not a problem to fix. Divergence only
matters when it removes something the scripts need.

Because ordering is meaningless, the check examines *every* published revision in the tenant and
keeps whichever satisfies the most of what the templates call, rather than assuming the
highest-numbered one is best.

Do not edit these files. The source of truth is whatever is published in PRO; this is a snapshot
for seeding a tenant that lacks one.
