# VOScript Tools

Plugin for automatic building of IMS / LIS integrations.

A Claude Code plugin for scaffolding VoiceOver PRO starter script sets.

## Install

**These are Claude Code slash commands — type them at the Claude Code prompt, not in a shell.**
PowerShell will just tell you `/plugin` is not a recognized cmdlet.

Once hosted:

    /plugin marketplace add <org>/voscript-tools
    /plugin install voscript-tools@voicebrook

If the install summary says `Run /reload-plugins to activate`, run that.

For local testing before it is hosted anywhere, point the marketplace at this directory — the one
containing `.claude-plugin/marketplace.json`. An absolute path avoids depending on the session's
working directory:

    /plugin marketplace add C:\path\to\voscript-tools
    /plugin install voscript-tools@voicebrook

`/plugin` opens an interactive panel, so it needs an interactive `claude` terminal session; some
surfaces do not host that panel.

## What is in it

| Skill | Use for |
|---|---|
| `voscript-ims` | IMS / slide viewer integrations — Halo, AISight, Concentriq, Corista, Fusion, BXLink. Slide navigation, magnification, annotations, AI biomarker resulting. |
| `voscript-lis` | LIS integrations where reporting is the main surface — Epic, PowerPath, CoPath, PathFlow. **Stub — not yet exercised on a real job.** |

Invoke as `/voscript-ims` or `/voscript-lis`, or let Claude pick based on what you describe.

## Layout

```
.claude-plugin/marketplace.json      marketplace definition
plugins/voscript-tools/
  .claude-plugin/plugin.json         plugin manifest
  skills/
    voscript-ims/SKILL.md
    voscript-lis/SKILL.md
  shared/
    references/                      house conventions, browser API, script catalog, AI resulting
    templates/                       the .cs templates and the named-list worksheet
    scripts/New-StarterScripts.ps1   the scaffolder
```

Both skills read the same `shared/` material through `${CLAUDE_PLUGIN_ROOT}`. That is the point of
one plugin rather than two: `conventions.md` changes often, and two copies would drift.

## Requirements

Windows, PowerShell, and a local PRO client install. The scaffolder writes `.cs` files to disk and
the skills read the PRO local cache, so this only works in Claude Code — not in claude.ai chat.

## Using it? Add yourself

Please add a line to [USERS.md](USERS.md) when you start using this, and open an issue or ping
Andrew if something in it is wrong.

This is not a vanity metric. The conventions in `shared/references/` change as we learn things —
the Dragon spoken-form rules, the named-list translation rule and the Material ligature addressing
all changed after scripts had already been written against the old version. When that happens, the
list is how anyone knows who to tell. Without it the only signal is GitHub clone traffic, which is
anonymous and only covers 14 days.

## Editing it

The scaffolder and templates are plain files; change them in place and the next run picks them up.
When something is learned on a real integration — a new addressing shape, a vendor gotcha, a
convention correction — put it in `shared/references/` rather than leaving it in a chat log. That
is what keeps this worth installing.

If the change alters a convention rather than adding to one, say so in the commit message and tell
whoever is on `USERS.md` — scripts already generated against the old rule will not fix themselves.
