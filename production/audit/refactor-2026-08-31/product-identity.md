# Product identity migration inventory — `ResonanceHunter` → `IDLExIDLE` / `IdleXIdle`

- **Audit date**: 2026-08-31 · **Branch**: `feat/hunter-cutout-rig` (clean) · **Read-only audit**; nothing outside this file was modified.
- **Scope**: every occurrence of the obsolete product identity (`ResonanceHunter`, `Resonance Hunter`, the `RH_` env-var prefix, old assembly/namespace/folder names), the mechanics of finishing the rename, what must be preserved for save compatibility, and the gameplay uses of the word *resonance* that must NOT be touched.
- **Method**: `grep -rn` / `find` over the repo excluding `bin/ obj/ .git/ docs/art-reference/ build/ *.png lfs-trace.txt`; targeted `sed -n` reads of every file cited; `dotnet`, `git log`, `gh run view` for the toolchain facts. Every count below is from a command that was actually run; the exact commands are in the Appendix.

---

## 0. Headline

The rename was **started on 2026-08-24 and stopped at the player-visible skin**: the exe is `IDLExIDLE.exe` (`src/ResonanceHunter.Game/ResonanceHunter.Game.csproj:8` `<AssemblyName>IDLExIDLE</AssemblyName>`), the window title is `"IDLExIDLE — pre-alpha"` (`Game1.cs:435`), the title lockup, controls header, store kit (`docs/store/*`), marketing scripts (`tools/marketing/*.py`) and publish script (`tools/publish/itch_push.sh:33,40`) all say IDLExIDLE. **Zero in-game strings still say "Resonance Hunter"** (grep of `src/` for the two-word form: 0 hits). Everything *under* the skin still carries the old identity: **1,025 `ResonanceHunter` occurrences** outside build outputs and the session log (src 329, tests 618, tools 26, production 19, docs 11, design 5, slnx 4, README 3, CI 3, PLAY.bat 2, .claude 2, assets 2, .gitignore 1), of which **227 are `namespace` declarations across 228 `.cs` files** and **620 are `using` lines**. The csproj comment at `ResonanceHunter.Game.csproj:6-7` records the deliberate choice to keep the folder name "so every path in the tools and the docs stays true" — that is the debt this phase pays.

Three facts change the plan from "sed everything" to something more careful:

1. **The save folder is `%LOCALAPPDATA%\ResonanceHunter\`** (`SaveFile.cs:31-35`, and independently `DisplaySettings.cs:145-147` for `display.txt`), and the 2026-08-24 rename *explicitly* decided to leave it (`production/session-logs/session-log.md:12155` — "save dir unchanged ResonanceHunter — old saves intact"). A blind sed of the literal `"ResonanceHunter"` would orphan every existing player save. No migration/fallback reader exists today.
2. **`ResonanceHunter.slnx` is unusable locally**: SDK `8.0.423` gives `MSB4068: <Solution> öğesi tanınmıyor` on `dotnet build ResonanceHunter.slnx` and `MSB1003` on a bare `dotnet restore` at the repo root. CI is green *only* because the GitHub runner has a newer SDK on PATH (`.NET ... 10.0.11 is already installed`, `DOTNET_ROOT: /usr/share/dotnet`, no `global.json` to pin 8.x) and that SDK parses `.slnx`. Rename the solution *and* fix the format (classic `.sln`, or add `global.json` + bump SDK) in the same step.
3. **The word "resonance" is a live gameplay term** (159 lines in `src/`+`tests/` after stripping the product identifier): `Branch.Resonance` mastery branch, `HunterStat.ResonanceAffinity`, `SkillShape.ResonanceWorth`, the STATS screen's "RESONANCE is your skills' damage", RITUAL NEST's "Listen to the resonance", six `*_resonance*` asset keys. The rename must be an **identifier rename** (`ResonanceHunter` → `IdleXIdle`, `Resonance Hunter` → `IDLExIDLE`), never a word replace.

Nothing in the codebase resolves types or resources by namespace/assembly string (grep for `GetName()`, `.Namespace`, `GetManifestResourceStream`, `AssemblyQualifiedName`, `JsonDerivedType`, `$type`: 0 hits in `src/`), and the save JSON carries no type names (`SaveGame.cs:444-448` — plain `JsonSerializerOptions`, `WriteIndented` + `WhenWritingNull`). **The C# rename is therefore mechanically safe for saves**; only the two folder-name literals and the one path-literal test need hand care.

---

## 1. What already says IDLExIDLE (finish, don't restart)

| Where | Evidence | Note |
|---|---|---|
| Executable name | `src/ResonanceHunter.Game/ResonanceHunter.Game.csproj:8` `<AssemblyName>IDLExIDLE</AssemblyName>`; comment at `:6-7` dates it 2026-08-24 | `RootNamespace` at `:5` is still `ResonanceHunter.Client` |
| Window title | `Game1.cs:435` `Window.Title = "IDLExIDLE — pre-alpha";` | |
| Title screen lockup + fallback text | `Game1.cs:4870-4880` (`logo_idlexidle_full`, fallback `"IDLExIDLE"`) | logo assets: `assets/art/BrandingSymbols/branding/logo_idlexidle_full.png`, `…/preview/emblem_idlexidle.png` (per `tools/marketing/make_capsules.py:26-27`) |
| Controls modal header | `Game1.cs:4998` `"IDLExIDLE — CONTROLS"` | |
| Publish script | `tools/publish/itch_push.sh:33` `OUT="build/release/IDLExIDLE-win64"`, `:40` runs `./IDLExIDLE.exe` under `RH_SHOT` | still publishes from `src/ResonanceHunter.Game` (`:35`) |
| Boot check kill-switch | `tools/check_boot.sh:65` `Get-Process IDLExIDLE` | but `:32` save path and `:12,34,54` project path are old |
| Store kit | `docs/store/store-page.md:1,18,106,109,113`; `docs/store/itch/README.md:1`, `description.html:2`, `custom.css:1` | 0 hits for "Resonance" in `docs/store/` |
| Marketing tooling | `tools/marketing/make_capsules.py:1,4,5,17,26,27`; `make_itch_page.py:1,30,31` | |
| Release artefacts | `build/release/IDLExIDLE-prealpha-{32a9630,5391cc7,885f579,e339bae}-win64.zip`, `IDLExIDLE-win64/` | untracked (`.gitignore:205` `build/`); one stale `ResonanceHunter-prealpha-9111f59-win64.zip` + `ResonanceHunter-win64/` remain from 2026-08-24 |
| Repo / itch identity | remote `github.com/abdullahamidi/idleXidle`; itch target `abdullahamidi/idlexidle` (user memory `itch-publishing.md`) | |
| Session state | `production/session-state/active.md:1,4,10` names the epic "ResonanceHunter -> IdleXIdle" | |
| Rename commits | `fe2038e` (2026-08-24) "post-pre-alpha round one — IDLExIDLE…"; `e42a63f` (2026-08-27) "the exe is IDLExIDLE…" | `git log -S"IDLExIDLE"` |

**Not yet anywhere as a C# identifier**: `IdleXIdle` appears only in prose (`assets/art/idlexidle_ux_screen_guide_standard.md:1,3`, `docs/art-reference/…`, `active.md`). There is no `namespace IdleXIdle…` and no `IdleXIdle.*.csproj` yet.

---

## 2. Categorised inventory

Counts exclude `bin/ obj/ .git/ build/ docs/art-reference/ *.png lfs-trace.txt` and the session log unless stated.

| # | Category | Count | Representative `file:line` |
|---|---|---|---|
| 1 | C# `namespace` declarations | **227** lines in 228 `.cs` files (Core 82, Game 25, unit tests 119, integration 1); 35 distinct namespaces | `src/ResonanceHunter.Game/Game1.cs:22` `namespace ResonanceHunter.Client;` · `src/ResonanceHunter.Core/Weaving/ResonanceWeaving.cs:6` `namespace ResonanceHunter.Core.Abilities;` |
| 2 | `using ResonanceHunter.*` statements | **620** (18 distinct; top: `.Core.Economy` 94, `.Core.Automation` 86, `.Core.Builds` 76, `.Core.Abilities` 75, `.Core.Loot` 74) | `Game1.cs:7-20` (14 usings) |
| 3 | Fully-qualified in-body references | **≈90** lines — 7 in `src/`, rest in tests | `src/…/Expeditions/WaveModel.cs:223`; `src/…/UiKit.cs:96-101,1083-1085`; `src/…/WeaveScreen.cs:747-750`; `src/…/Program.cs:1` `new ResonanceHunter.Client.Game1()`; tests e.g. `power_scale_test.cs:96-109`, `EnchantmentsTests.cs:121-234`; `global::ResonanceHunter` at `PassiveTreeTests.cs:69`, `TriggerLivenessTests.cs:38-39`; alias `roster_parity_test.cs:15` |
| 4 | XML-doc `cref="ResonanceHunter…"` | **6** | `Combat/AttackBias.cs:5`; `Expeditions/WaveModel.cs:11,216,346`; `Progression/EfficiencyContract.cs:33`; `Game/UiKit.cs:1080` |
| 5 | csproj / slnx / project refs / manifests | **14** lines in 8 files | `ResonanceHunter.slnx:3,4,8,11`; `Game.csproj:5` RootNamespace, `:36` ProjectReference; `unit .csproj:24`; `integration .csproj:24`; `app.manifest:3` `assemblyIdentity name="ResonanceHunter.Game"`; `.vscode/launch.json:8,11`; `.github/workflows/ci.yml:33,39` (+ comment `:30`) |
| 6 | Directory / file names | 4 project dirs, 4 csproj names, 1 slnx, 1 orphan mgcb, 10 agent-memory files | `src/ResonanceHunter.Core/`, `src/ResonanceHunter.Game/`, `tests/unit/ResonanceHunter.Core.Tests/`, `tests/integration/ResonanceHunter.Integration.Tests/`, `ResonanceHunter.slnx`, `assets/art/ResonanceHunterAssets.mgcb` (21,816 lines, **unreferenced** — see §3.4), `.claude/agent-memory/*/project_resonance-hunter-*.md` (10) |
| 7 | Tool / script hardcoded paths | **26** lines in 14 files (+ hook + PLAY.bat) | `tools/run.sh:16,22`; `check_boot.sh:12,32,34,54`; `asset-pipeline/capture.sh:74,87`; `capture_seq.sh:18,22`; `finalize.sh:5,32,35`; `finish_hunter.sh:11`; `publish/itch_push.sh:35`; `asset-pipeline/audit.py:35,89`; `check_asset_keys.py:28,118,143`; `check_font_coverage.py:37`; `check_init_order.py:37`; `check_mouse_space.py:25`; `check_nav_gates.py:17-18`; `check_ui_type.py:50`; `.claude/hooks/generate-assets.sh:33`; `PLAY.bat:14,23` |
| 8 | Env-var prefix `RH_*` | **22 distinct tokens**; code reads in 5 Game files (Game1.cs 67 lines, ForgeScreen.cs 12, StatsScreen.cs 5, SaveFile.cs 2, SoloExpeditionScreen.cs 2 comments); tools 7 files / 38 lines; docs 17 lines; user memory 4 lines | `Game1.cs:459` `RH_DEV`, `:462` `RH_SHOT`, `:477` `RH_SHOT_MODE`, `:871-872` `RH_BOOTCHECK`/`RH_SAVE_DIR`; `SaveFile.cs:31`; `tools/shellenv.sh:19,102-103,107,112` (`RH_ENV`, `_RH_PY`); `capture.sh` (14 lines); `check_boot.sh:11,18,25,27,51-53`; `docs/PROJECT-NOTES.md:297,313,323,342-344` |
| 9 | UI strings / window title (old name) | **0** in `src/`; **1** user-visible in `PLAY.bat:22` `echo Starting Resonance Hunter...` | new name already at `Game1.cs:435,4880,4998` |
| 10 | Save / prefs path | **2** code literals + 1 script + 3 docs | `Game/SaveFile.cs:35` `"ResonanceHunter"` → `%LOCALAPPDATA%\ResonanceHunter\save.json|save.bak|save.corrupt-*|save.newer-*` (`SaveStore.cs:30,33,121,133,158`); `Game/DisplaySettings.cs:147` `"ResonanceHunter", "display.txt"`; `tools/check_boot.sh:32`; `production/ROADMAP-TO-RELEASE.md:129`; `design/gdd/save-load-persistence.md:224` (says `%AppData%` — doc drift, code uses LocalApplicationData); `design/gdd/accessibility-settings-system.md:113` |
| 11 | Docs titles / prose "Resonance Hunter" | **80** lines / 46 files: design 52, .claude/agent-memory 20, production 3, assets 2, README 1, docs 1, PLAY.bat 1 | 25 GDD/art title lines of the form `# <System>: Resonance Hunter` (e.g. `design/gdd/combat-encounter-system.md:1`, `design/art/art-bible.md:1`, `design/gdd/systems-index.md:1`); `README.md:1`; `production/ROADMAP-TO-RELEASE.md:1`; `production/qa/playtest-guide.md:1`; `docs/release-notes-v1.0.md:1`; fiction: `design/gdd/game-concept.md:34,76` "You are a Resonance Hunter who tames chaos" |
| 12 | Docs prose `ResonanceHunter` (paths/namespaces) | docs 11, production 19 (13 in `systems-map.json`), design 5 | `docs/architecture/ADR-001-pure-logic-core-separation.md:34,38,39,42,45,88`; `ADR-002:112`; `ADR-004:24`; `docs/PROJECT-NOTES.md:313,481,1337`; `design/gdd/warren-facilities.md:4`; `production/qa/playtest-guide.md:7`; `ROADMAP-TO-RELEASE.md:129` |
| 13 | Test fixtures | **1** path literal; 119 namespace decls; ≈70 FQ lines | `tests/unit/ResonanceHunter.Core.Tests/Presentation/hunt_screen_feedback_test.cs:35` `Path.Combine(dir, "src", "ResonanceHunter.Game", "SoloExpeditionScreen.cs")` — **breaks on folder rename** |
| 14 | Comments (prose, not code) | 7 | `Game1.cs:34`; `Game.csproj:6-7`; `.github/workflows/ci.yml:30`; `.gitignore:166`; `tools/asset-pipeline/finalize.sh:5`; `EfficiencyContract.cs:33` |
| 15 | Historical — leave as-is | session-log 143, systems-map.json 13, dated audits/reports 4, untracked builds | `production/session-logs/session-log.md` (audit trail; 38 of 143 are bare file paths); `production/audit/systems-map.json` (2026-08-12 snapshot); `design/system-audit-2026-08-12.md`; `docs/release-notes-v1.0.md`; `production/OVERNIGHT-REPORT.md`; `build/release/ResonanceHunter-win64/` + `.zip` (untracked) |
| 16 | Agent / user memory | 10 files named `project_resonance-hunter-*`, 20 prose lines; user memory 2 lines | `.claude/agent-memory/ux-designer/MEMORY.md` (6); user `MEMORY.md:1` "Memory Index — Resonance Hunter (idleXidle)"; `open-decision-core-combat-orphans.md:32`; `dormant-features-are-the-failure-mode.md:37,45-48` (RH_ vars) |

Raw total including build binaries and the session log: 1,199 `ResonanceHunter` lines (`grep -rn … | wc -l`).

---

## 3. Category detail and evidence

### 3.1 Namespaces (227 declarations, 35 distinct)

```
35 ResonanceHunter.Core.Tests.Builds      17 ResonanceHunter.Core.Builds        4 ResonanceHunter.Core.Progression
25 ResonanceHunter.Client                 13 ResonanceHunter.Core.Encounters    4 ResonanceHunter.Core.Persistence
23 ResonanceHunter.Core.Tests.Economy     13 ResonanceHunter.Core.Economy       4 ResonanceHunter.Core.Characters
 9 ResonanceHunter.Core.Tests.Persistence  9 ResonanceHunter.Core.Expeditions   3 ResonanceHunter.Core.Forging
 9 ResonanceHunter.Core.Tests.Expeditions  5 ResonanceHunter.Core.Prestige      2 ResonanceHunter.Core.Automation
 8 ResonanceHunter.Core.Tests.Progression  … (full list in Appendix)            1 ResonanceHunter.Core.Abilities
```

Three folder/namespace mismatches will survive a pure token rename and are worth fixing in the same pass because the sed touches every one of those lines anyway:

- `src/ResonanceHunter.Core/Weaving/ResonanceWeaving.cs:6` → `namespace ResonanceHunter.Core.Abilities` (folder *Weaving*, namespace *Abilities*; 75 `using …Abilities` consumers). This is the LEGACY Source×Form file the refactor deletes, but it also hosts **Vows** (`VowKind` at `:15`), which are current. Vows must move out before the file dies.
- `src/ResonanceHunter.Core/Forge/*.cs` → `namespace ResonanceHunter.Core.Forging` (3 files) and `tests/unit/…/Forge/*.cs` → `…Tests.Forging` (7 files).
- `src/ResonanceHunter.Game/*.cs` → `namespace ResonanceHunter.Client` (25 files) while the folder and csproj say `.Game`. Decision needed (see §9 Q1).

### 3.2 `using`s, fully-qualified references, `cref`s

620 `using ResonanceHunter.*` lines and ≈90 fully-qualified in-body references; a `sed 's/\bResonanceHunter\b/IdleXIdle/g'` over `*.cs` handles all of them, including `global::ResonanceHunter.Core.Automation.Source` (`PassiveTreeTests.cs:69`, `TriggerLivenessTests.cs:38-39`), the alias `using CoreSource = ResonanceHunter.Core.Automation.Source;` (`roster_parity_test.cs:15`), the 6 `cref`s, and `Program.cs:1`. Nothing constructs a namespace string at runtime (see §0 reflection grep).

### 3.3 Project files, manifests, IDE, CI

- `ResonanceHunter.slnx:3,4,8,11` — four project paths. File created in `ec4f234` (2026-07-24, "Recreate repository without Git LFS"); **no `.sln` has ever existed** (`git log --all -- "*.sln"`: empty).
- `src/ResonanceHunter.Game/ResonanceHunter.Game.csproj:5` `RootNamespace ResonanceHunter.Client`; `:36` `..\ResonanceHunter.Core\ResonanceHunter.Core.csproj`.
- `tests/unit/…/ResonanceHunter.Core.Tests.csproj:24`, `tests/integration/…/ResonanceHunter.Integration.Tests.csproj:24` — ProjectReference to Core.
- `src/ResonanceHunter.Game/app.manifest:3` `<assemblyIdentity … name="ResonanceHunter.Game"/>` — cosmetic (Win32 manifest identity), but it is the last place the exe calls itself the old name.
- `src/ResonanceHunter.Game/.vscode/launch.json:8,11`.
- `.github/workflows/ci.yml:33,39` — test project paths; `:30` comment. CI runs `dotnet restore` / `dotnet build` at the root with no argument (`ci.yml:22,27`), i.e. it depends on the solution file being parseable — see §8.
- `src/ResonanceHunter.Game/.config/dotnet-tools.json` — clean (0 hits).
- `src/ResonanceHunter.Game/Content/Content.mgcb` — clean, and **empty** of content entries (the csproj copies PNG/WAV/TTF directly, `Game.csproj:38-73`).

### 3.4 Directory and file names

- Four project directories + their csproj files (above).
- `assets/art/ResonanceHunterAssets.mgcb` — 21,816 lines of `TextureProcessor` entries. **Orphan**: `grep -rn ResonanceHunterAssets .` (excl. build/bin/obj) finds only `docs/art-reference/package_09/{IMPORT_CHECKLIST,MASTER_IMPLEMENTATION_GUIDE,MONOGAME_CONTENT_PIPELINE}.md`, i.e. the art vendor's import instructions, never a csproj or script. The Game csproj explicitly sidesteps MGCB (`Game.csproj:39-42`) and MonoGame's `**/*.mgcb` auto-glob only sees files under the project directory, which `assets/art/` is not. Delete candidate (A).
- `src/ResonanceHunter.Core/Weaving/ResonanceWeaving.cs` and `design/gdd/resonance-weaving-system.md` — "Resonance Weaving" is the *legacy system's* name, not the product's; it goes away with the legacy sweep, not with the identity rename.
- `.claude/agent-memory/{art-director,economy-designer,systems-designer,ux-designer}/project_resonance-hunter*.md` — 10 files (agent memory; retitle on touch, not blocking).

### 3.5 Tools and scripts (26 lines, 14 files)

All are literal `src/ResonanceHunter.Game` / `src/ResonanceHunter.Core` / `tests/unit/ResonanceHunter.Core.Tests` path segments — every one of `tools/run.sh`, `check_boot.sh`, `asset-pipeline/{capture,capture_seq,finalize,finish_hunter}.sh`, `publish/itch_push.sh`, `asset-pipeline/audit.py`, `check_{asset_keys,font_coverage,init_order,mouse_space,nav_gates,ui_type}.py`, plus `.claude/hooks/generate-assets.sh:33` (a `case` glob `*src/ResonanceHunter.Game/*.cs`) and `PLAY.bat:14,23`. `tools/shellenv.sh` and `tools/check_all.sh` carry no path (0 hits). A single sed over `tools/ .claude/hooks/ PLAY.bat` covers them; **except `check_boot.sh:32`** which is the save path and must stay `ResonanceHunter` (or read one shared constant — §6).

### 3.6 Env-var prefix `RH_*` (22 tokens)

```
RH_SHOT(36) RH_SHOT_T(17) RH_SHOT_MODE(16) RH_BOOTCHECK(16) RH_ENV(15) RH_SAVE_DIR(11) RH_SHOT_MOUSE(8)
RH_SHOT_DROPDOWN(7) RH_SHOT_ZOOM(6) RH_SHOT_TAB(5) RH_SHOT_STEP(5) RH_SHOT_SEQ(4) RH_SHOT_HOVER(3)
RH_SHOT_EXPLAIN(3) RH_DEV(3) RH_SHOT_SWING(2) RH_SHOT_POSE(2) RH_SHOT_OPEN(2) RH_SHOT_CHARTS(2)
RH_SHOT_ASK(2) RH_SHOT_ARM(2) RH_PY(2)          (occurrence counts across repo, excl. session log)
```

- **Every read is a raw `Environment.GetEnvironmentVariable("RH_…")`** — ~40 call sites in `Game1.cs` (`:459,462,466,477,519,555,861,871-872,1145,1240-1249,1322,1333,1489,1589,1616,1748,1806,1822,1874,2006,2109,2181,2188,3051-3052,3843,3876,4033,4111,4117`), 6 in `ForgeScreen.cs` (`:390,395,401,406,2461-2462`), 2 in `StatsScreen.cs` (`:147-148`), 1 in `SaveFile.cs:31`. There is no helper; the prefix is spelled out ~50 times.
- `RH_ENV` / `_RH_PY` in `tools/shellenv.sh:19,39,51,102-103,107,112` are **shell variables**, not game env vars — the `dn` function applies `RH_ENV` entries to the dotnet process.
- The vars are a **developer/CI contract** (screenshot rig, boot check, save redirection), never player-facing. They are documented in `docs/PROJECT-NOTES.md:297-344`, `tools/check_boot.sh:11-27`, and the owner's memory file `dormant-features-are-the-failure-mode.md:37,45-48`.
- Recommendation in §7.

### 3.7 UI strings / window title

Old name in shipped UI: **none** (`grep -rn "Resonance Hunter" src/` → 0). The only user-visible old string is the console `echo Starting Resonance Hunter...` in `PLAY.bat:22`. New name already at `Game1.cs:435` (title bar), `:4880` (title fallback), `:4998` (controls header).

### 3.8 Save / prefs path (the one thing sed must not touch)

- `src/ResonanceHunter.Game/SaveFile.cs:28-38`: `Dir` = `RH_SAVE_DIR` if set, else `Path.Combine(LocalApplicationData, "ResonanceHunter")`. Files inside: `save.json`, `save.bak`, `save.corrupt-<ts>`, `save.newer-<ts>`, `save.bak-pre-reset-<ts>` (`Core/Persistence/SaveStore.cs:30,33,121,133,158`).
- `src/ResonanceHunter.Game/DisplaySettings.cs:145-147`: `PrefsPath` = `LocalApplicationData/ResonanceHunter/display.txt` — **a second, independent computation of the same folder** (duplicated responsibility; hoist to one `SaveLocation`/`AppDataDir` constant).
- `tools/check_boot.sh:32` `SAVE="${LOCALAPPDATA:-$HOME/.local/share}/ResonanceHunter/save.json"` — hashes the real save before/after a boot to prove the boot check cannot write.
- Docs: `production/ROADMAP-TO-RELEASE.md:129` (`rm -f "$LOCALAPPDATA/ResonanceHunter/save.json"`); `design/gdd/save-load-persistence.md:224` says `%AppData%/ResonanceHunter/save.json` (**drift**: code uses `LocalApplicationData` = `%LOCALAPPDATA%`); `design/gdd/accessibility-settings-system.md:113` names `accessibility_settings.json` (code has `display.txt`).
- Prior decision on record: `production/session-logs/session-log.md:12155` "renamed IDLExIDLE (window title, help header, title fallback; **save dir unchanged ResonanceHunter — old saves intact**)".

### 3.9 Docs — living vs historical

**Living (update):** `README.md:1,9,61-62`; `PLAY.bat:14,22,23`; `docs/architecture/ADR-001-pure-logic-core-separation.md:34-45,88` (the architecture contract names both projects and the test project); `ADR-002:112`; `ADR-004:24`; `design/gdd/warren-facilities.md:4` (`System: ResonanceHunter.Core.Warrens.Warren`); `docs/PROJECT-NOTES.md:313,481,1337` + 11 `RH_` lines; `production/qa/playtest-guide.md:1,7`; `production/ROADMAP-TO-RELEASE.md:1,129`; the 25 GDD/art-bible title lines `# …: Resonance Hunter` and the ~27 body-prose uses in `design/` (list in §2 row 11); `assets/art/idlexidle_ux_screen_guide_standard.md:3` ("IdleXIdle / Resonance Hunter"); `assets/art/package_10_asset_placement_guide/README.md:3`; `design/monetization.md:11`; `design/references/reference-games.md:3`.

**Historical (leave, or a one-line "was Resonance Hunter" note at the top):** `production/session-logs/session-log.md` (143; it is the audit trail), `production/audit/systems-map.json` (13; dated 2026-08-12 snapshot with line numbers that are already stale), `design/system-audit-2026-08-12.md`, `docs/release-notes-v1.0.md`, `production/OVERNIGHT-REPORT.md`.

**Fiction, needs a design call (Q4):** `design/gdd/game-concept.md:34` "You are a Resonance Hunter who tames chaos" and `:76` "The Resonance Hunter identity" use the old product name as the *player's in-world role*. The code never does (the player is `Hunter` — `Economy/HunterProgression.cs`, UI "HUNTER LEVEL"), so this is a docs-only question.

### 3.10 Test fixtures

- `tests/unit/ResonanceHunter.Core.Tests/Presentation/hunt_screen_feedback_test.cs:30-40` walks up from `AppContext.BaseDirectory` looking for `src/ResonanceHunter.Game/SoloExpeditionScreen.cs` and reads the **Game project's source text** to assert on it. This is (a) the one string literal that breaks on `git mv`, and (b) a Core test project reaching into presentation source by path — note under core/presentation coupling.
- 119 `namespace ResonanceHunter.Core.Tests.*` declarations and 1 `ResonanceHunter.Integration.Tests`; ≈70 fully-qualified lines; all sed-safe.
- `legacy_trait_migration_test.cs` is about the Trait→Dust migration, not the product name — unrelated to this rename.

### 3.11 Comments

`Game1.cs:34` ("all rules live in ResonanceHunter.Core (ADR-001)"), `Game.csproj:6-7` (the rename rationale — rewrite, don't delete: it becomes the record that the folder rename happened), `ci.yml:30`, `.gitignore:166` (explains why `src/ResonanceHunter.Core/Builds/` must not be swallowed by the Unity `[Bb]uilds/` rule — **the path in that comment must be updated or a future reader will look for a folder that no longer exists**), `tools/asset-pipeline/finalize.sh:5`, `EfficiencyContract.cs:33`.

---

## 4. "Resonance" as a gameplay term — KEEP list

After stripping the identifier tokens (`sed 's/ResonanceHunter//g; s/ResonanceWeaving//g'`) **159 lines** in `src/` + `tests/` still contain *resonan*. These are gameplay, and a word-level replace would destroy them:

| Use | Evidence | Status |
|---|---|---|
| Mastery branch `Branch.Resonance` — "STRONGER SKILLS, BEFORE YOU PICK ONE" | `Core/Builds/MasteryCatalog.cs:14-28,137,152-194,406,415,434-436,460-463`; `MasteryLayout.cs:135`; `Game/BuildScreen.cs:434,1091,1213,1588,1619`; `RosterScreen.cs:93,100`; `Game1.cs:1600` | **current** — the four branches are RESONANCE / LOOT / TEMPO / ENDURE |
| Stat `HunterStat.ResonanceAffinity` ("RESONANCE is your skills' damage") | `Core/Economy/HunterProgression.cs:11,55,72,372,385`; `Game/StatsScreen.cs:254,310,315-324`; `SoloBattle.cs:486` | **current** |
| `SkillShape.ResonanceWorth` (DEEP notable: "a point of resonance is worth 20% more") | `Core/Builds/SkillShape.cs:58-79,494`; `SoloBattle.cs:484-487` | **current** |
| BUILD screen "PASSIVES & RESONANCE" panel, "<SOURCE> RESONANCE" share rows | `Game/BuildScreen.cs:845,892-926` | **current** |
| Class WARDEN "Built for the RESONANCE road" | `Core/Economy/ItemClasses.cs:96-98`; champion `Lean = Branch.Resonance` `Characters/CharacterRoster.cs:73,132` | **current** |
| RITUAL NEST — "Listen to the resonance. Slow, deep Insight." | `Core/Warrens/Warren.cs:53` | **current** (flavour) |
| Asset keys `icon_branch_resonance`, `icon_status_resonance`, `state_resonance_128`, `affix_resonance`, `currency_resonance_shard`, `stat_resonance` | `Game/AssetLibrary.cs:144,151`; `BuildScreen.cs:1619`; `Game1.cs:5118`; `SoloExpeditionScreen.cs:2343`; `StatsScreen.cs:231`; `tools/asset-pipeline/manifest.json:1002,1062,1104,1796,2298`; PNGs under `assets/art/**` | **current**; renaming a key renames a file on disk and a manifest row |
| `WeavingTuning.SourceScalingCoefficient` × `resonanceAffinity` in `Weaving.BasePower` | `Core/Weaving/ResonanceWeaving.cs:187-191,333-349`; `FormBehaviour.cs:293-296`; `SoloBattle.cs:1420,1573,1707,1926` | **legacy formula** (Form-based) — dies with Source×Form, but the *stat* it reads survives |
| "Resonance Weaving" (system name), `design/gdd/resonance-weaving-system.md`, `DustEffects.cs:93` comment | | **legacy system name** — retire with the system, not with the product rename |
| Test names `mastery_node_liveness_test.cs:38,124-125` `EveryResonanceNode`, `test_taking_a_resonance_node_changes_the_fight` | | **current** |

Rule for the sed: match the identifier `\bResonanceHunter\b` and the two-word phrase `Resonance Hunter` only. Never `Resonance` alone.

---

## 5. Rename plan (ordered)

Target identity (proposed; see §9 Q1 for the one open naming choice):

| Old | New |
|---|---|
| `src/ResonanceHunter.Core/` · `ResonanceHunter.Core.csproj` · `namespace ResonanceHunter.Core.*` · `ResonanceHunter.Core.dll` | `src/IdleXIdle.Core/` · `IdleXIdle.Core.csproj` · `IdleXIdle.Core.*` · `IdleXIdle.Core.dll` |
| `src/ResonanceHunter.Game/` · `ResonanceHunter.Game.csproj` · `namespace ResonanceHunter.Client` · `RootNamespace ResonanceHunter.Client` | `src/IdleXIdle.Game/` · `IdleXIdle.Game.csproj` · `namespace IdleXIdle.Game` (recommended) · `RootNamespace IdleXIdle.Game`; **AssemblyName stays `IDLExIDLE`** |
| `tests/unit/ResonanceHunter.Core.Tests/` · `…Tests.csproj` · `namespace ResonanceHunter.Core.Tests.*` | `tests/unit/IdleXIdle.Core.Tests/` · `IdleXIdle.Core.Tests.csproj` · `IdleXIdle.Core.Tests.*` |
| `tests/integration/ResonanceHunter.Integration.Tests/` | `tests/integration/IdleXIdle.Integration.Tests/` |
| `ResonanceHunter.slnx` | `IdleXIdle.sln` (classic format) — or keep `.slnx` and add `global.json` pinning an SDK ≥ 9.0.200 |
| `%LOCALAPPDATA%\ResonanceHunter\` | **unchanged** (§6) |
| `RH_*` env vars | §7 |

Steps — each one leaves the tree building and the tests green, so they can be separate commits:

0. **Preconditions.** Stop the running game (`Get-Process IDLExIDLE`), delete every `bin/` and `obj/` (they are untracked and `git mv` will leave them behind in the old folders, where the stale `obj/**/ResonanceHunter.*.AssemblyInfo.cs` would confuse greps). Consider doing the directory moves in a worktree **outside the OneDrive-synced path** — `C:\Users\sanan\OneDrive\Masaüstü\idleXidle` is live-synced and directory renames under sync are a known source of locked-file failures.
1. **Hoist the save folder name first** (hand edit, 3 places): introduce one constant — e.g. `IdleXIdle.Game.AppData.FolderName = "ResonanceHunter"` with a comment "the pre-rename product name; the save has lived here since 2026-07 and every player's file is under it — do NOT rename without a migration (see PLAN)". Point `SaveFile.cs:35` and `DisplaySettings.cs:147` at it. Leave `tools/check_boot.sh:32` literal but add the same comment. This step exists so step 4's sed can be verified to have touched exactly zero save-path literals.
2. **`git mv` the four project directories and rename the four csproj files** (`git mv src/ResonanceHunter.Core src/IdleXIdle.Core && git mv src/IdleXIdle.Core/ResonanceHunter.Core.csproj src/IdleXIdle.Core/IdleXIdle.Core.csproj`, ×4). Windows FS is case-insensitive but these are not case-only renames, so plain `git mv` is fine.
3. **Solution + csproj + manifests.** Write `IdleXIdle.sln` (`dotnet new sln -n IdleXIdle && dotnet sln add …` ×4 — works on SDK 8) and `git rm ResonanceHunter.slnx`; or keep `.slnx` + `global.json`. Update `Game.csproj:5,36`, both test csproj `:24`, `app.manifest:3` (`name="IDLExIDLE"` or `IdleXIdle.Game`), `.vscode/launch.json:8,11`, `.github/workflows/ci.yml:30,33,39`. Verify `dotnet build IdleXIdle.sln -warnaserror` locally — this is also the moment the local build and CI first run the *same* command.
4. **Namespaces/usings via sed** over tracked `*.cs` only, excluding the file from step 1 if it still holds the literal: `git ls-files 'src/*.cs' 'tests/*.cs' | xargs sed -i 's/\bResonanceHunter\.Client\b/IdleXIdle.Game/g; s/\bResonanceHunter\b/IdleXIdle/g'`. Then `grep -rn "ResonanceHunter" src tests` must return **exactly** the step-1 constant (1 line) — any other hit is a bug in the sed. Optionally fold the folder/namespace mismatches (`Forging`→`Forge`, `Abilities`→ wherever Vows land) here, since every affected line is already in the diff.
5. **The one test fixture**: `hunt_screen_feedback_test.cs:35` is covered by the sed (path segment `ResonanceHunter.Game` → `IdleXIdle.Game`) — confirm the test still finds the file.
6. **Tools/scripts**: sed over `tools/**/*.sh tools/**/*.py .claude/hooks/*.sh PLAY.bat` for the path segments (`src/ResonanceHunter.Game`, `src/ResonanceHunter.Core`, `tests/unit/ResonanceHunter.Core.Tests`) and `PLAY.bat:22` → `Starting IDLExIDLE...`. **Skip `check_boot.sh:32`.** Re-run `tools/check_all.sh`, `tools/check_boot.sh`, one `tools/asset-pipeline/capture.sh` mode and `tools/publish/itch_push.sh` up to (not including) the butler push — these are the only proof the scripts still find the project.
7. **Living docs**: `README.md`, ADR-001/002/004, `design/gdd/warren-facilities.md:4`, `docs/PROJECT-NOTES.md`, `production/qa/playtest-guide.md`, `production/ROADMAP-TO-RELEASE.md`, `.gitignore:166`, the 25 GDD title lines (`sed -i 's/: Resonance Hunter$/: IDLExIDLE/'` on `design/gdd/*.md design/art/*.md` first lines) and the body-prose lines; fix the `%AppData%` → `%LOCALAPPDATA%` drift in `save-load-persistence.md:224` while there. Add a one-line "Formerly *Resonance Hunter* (renamed 2026-08-24)" to README so the historical docs stay legible.
8. **Memory & agent notes**: user `MEMORY.md:1` header; `dormant-features-are-the-failure-mode.md` (env vars, if §7 renames them); `.claude/agent-memory/**/project_resonance-hunter*.md` retitle on next touch.
9. **Housekeeping**: `rm -rf build/release/ResonanceHunter-win64 build/release/ResonanceHunter-prealpha-9111f59-win64.zip` (untracked); `git rm assets/art/ResonanceHunterAssets.mgcb` (orphan, §3.4) — or keep it if the owner wants the vendor's MGCB list as reference, but then move it under `docs/art-reference/` where its only readers live.
10. **Definition of done**: `grep -rn "ResonanceHunter\|Resonance Hunter" . --exclude-dir={bin,obj,.git,build,art-reference} --exclude=session-log.md` returns only (a) the save-folder constant + `check_boot.sh:32`, (b) the dated historical docs listed in §3.9, (c) the README "formerly" line; `dotnet build IdleXIdle.sln -warnaserror` green locally and in CI; 1225 + 2 tests green; `check_boot.sh` reports BOOT OK against the real save (proves the folder literal survived).

---

## 6. Save compatibility — what must be preserved

- **The folder `%LOCALAPPDATA%\ResonanceHunter\` must keep being read.** Every existing player (the four itch pre-alpha builds since 2026-08-24, plus the owner) has `save.json` / `save.bak` / `display.txt` there. `SaveStore` has quarantine and backup logic keyed to that directory (`SaveStore.cs:121,133,158`).
- Two acceptable end states: (a) **keep the folder name forever** (cheapest; matches the 2026-08-24 decision; the name is invisible to players except in Explorer); or (b) **migrate**: new folder `%LOCALAPPDATA%\IDLExIDLE\`; on first run, if the new dir has no `save.json` and the old dir does, copy `save.json`, `save.bak`, `display.txt` (never move — leave the old tree so a downgrade still works), then read from the new dir. (b) needs a `SaveStore`-level unit test against two throwaway dirs and a `check_boot.sh` variant. Recommendation: **(a) now**, revisit (b) only if a Steam/Epic path requires a clean app-data identity.
- **Nothing in the JSON is namespace-bound**: `SaveSystem.Options` (`SaveGame.cs:444-448`) has no polymorphism/type discriminators; enum values serialise as ints by default (no `JsonStringEnumConverter` — grep 0 hits in `Persistence/`), so even enum *renames* would be save-safe as long as ordinal positions hold. Share codes (`ShareCodes.cs:101-210`) likewise serialise plain records. **The C# rename cannot break a save.**
- `RH_SAVE_DIR` (`SaveFile.cs:31`) is the only redirection and is dev-only; if §7 renames it, keep `RH_SAVE_DIR` as an alias for one release so `tools/check_boot.sh` and the memory file's recipe keep working.
- `BuildStamp` (`Core/Persistence/BuildStamp.cs:20`) reads `AssemblyInformationalVersion` (commit hash), not the assembly *name* — the newer-build quarantine (`save.newer-*`) is unaffected by the Core assembly changing name.

---

## 7. Env-var prefix policy (`RH_*`)

Facts: 22 tokens; ~50 raw `GetEnvironmentVariable("RH_…")` call sites (Game1.cs 40+, ForgeScreen 6, StatsScreen 2, SaveFile 1); 7 tool files; documented in `PROJECT-NOTES.md`, `check_boot.sh` header, and the owner's memory. Dev/CI contract only — no player ever sets one.

Options:

- **A — Keep `RH_*`, document it as a legacy prefix.** Zero churn, zero risk to the rig or the owner's muscle memory. Cost: the abbreviation is now meaningless ("RH" = a game that no longer exists) and violates the project's own no-unexplained-abbreviations rule for anything a reader meets.
- **B — Rename to `IXI_*` with a one-release alias.** Introduce a single `DevEnv.Get("SHOT")` helper that tries `IXI_SHOT` then `RH_SHOT`; replace the ~50 raw reads with it (this is a real abstraction opportunity regardless of the rename — the prefix is spelled ~50 times and `Game1.cs` is 5k lines); update `tools/shellenv.sh` (`RH_ENV` → `IXI_ENV`), `capture.sh`, `capture_seq.sh`, `check_boot.sh`, `itch_push.sh`, `run.sh`, `check_init_order.py`, `PROJECT-NOTES.md`, and the memory file. Drop the alias after one publish.

**Recommendation: B, but as the last, non-blocking step of the identity phase**, and only if the `DevEnv` helper is done at the same time — a rename-only sed of 50 string literals without the helper is churn with no structural gain. If the owner prefers to leave the rig alone, A is fine; either way write the policy down in `PROJECT-NOTES.md` so the next reader does not re-open it.

---

## 8. Solution file (`.slnx`) — verified

- `dotnet --version` → `8.0.423`; no `global.json` in the repo.
- `dotnet sln ResonanceHunter.slnx list` → "`ResonanceHunter.slnx` çözümü geçersiz. Beklenen dosya üst bilgisi bulunamadı."
- `dotnet build ResonanceHunter.slnx --no-restore` → `error MSB4068: <Solution> öğesi tanınmıyor veya bu bağlamda desteklenmiyor.`
- `dotnet restore` (bare, at root — the exact CI command, `ci.yml:22`) → `MSB1003: Bir proje veya çözüm dosyası belirtin.`
- CI run `33390896201` (2026-08-31, green) log: `dotnet-install: .NET Core SDK with version '8.0.424' is already installed`, **`.NET Core Runtime with version '10.0.11' is already installed`**, `DOTNET_ROOT: /usr/share/dotnet`, and all four csproj restored from the root. With no `global.json`, the muxer picks the newest SDK on the runner, which understands `.slnx`. **Local and CI are building with different toolchains; the local one cannot open the solution at all.** Every tool script works around this by building `src/ResonanceHunter.Game` directly (`run.sh:16`, `check_boot.sh:34`, `capture.sh:74`, `itch_push.sh:35`).
- Fix options: (i) classic `IdleXIdle.sln` — works on every SDK, no other change; (ii) keep `.slnx`, add `global.json` with `"sdk": { "version": "9.0.200", "rollForward": "latestFeature" }` and install that SDK locally — also pins CI to a known SDK, which it currently is not. (i) is the smaller change; (ii) is the more honest one because the project would then build with the same SDK everywhere. Either is fine; doing neither leaves the solution file as dead weight.

---

## 9. Risks and open questions

**Q1 — Game namespace: `IdleXIdle.Game` or `IdleXIdle.Client`?** Today folder/csproj say `.Game`, namespace/RootNamespace say `.Client` (`Game.csproj:5`, 25 files). Recommendation: `IdleXIdle.Game` so folder = csproj = namespace; needs a separate sed rule (`ResonanceHunter.Client` → `IdleXIdle.Game`) *before* the generic one.

**Q2 — Keep or migrate the save folder?** See §6; recommendation keep. Needs the owner's yes because it means the product's app-data folder carries the old name indefinitely.

**Q3 — `.sln` vs `global.json`+SDK bump?** See §8.

**Q4 — In-fiction role.** `game-concept.md:34,76` calls the player "a Resonance Hunter". Is that still the fantasy, or is the player just "the Hunter" (as the code says)? Docs-only; no code consequence.

**Q5 — Env prefix** (§7): A or B, and whether B waits for the `DevEnv` helper.

**Q6 — `ResonanceHunter.Core.Abilities` namespace.** It is the legacy Weaving file's namespace and 75 files `using` it. Vows (`VowKind`, `Weaving.Catalog` used by `DustEffects.cs` / `DustEffectsTests.cs:285,294`) live inside it. The identity rename will carry it to `IdleXIdle.Core.Abilities`; the legacy sweep then has to move Vows out and delete the rest. Decide the Vows' destination namespace before step 4 so those lines are touched once.

**Q7 — OneDrive.** The working copy is inside a synced folder. Directory moves + a 228-file sed while sync is running have a real chance of "file in use" failures mid-`git mv`. Pause sync or use a worktree outside OneDrive for the move commit.

**Q8 — Agent memory files.** Ten `.claude/agent-memory/**/project_resonance-hunter*.md` files: rename now (cheap, cosmetic) or on touch? They are not code; recommend on touch.

**Not an open question — verified safe**: no reflection-by-name, no `InternalsVisibleTo` (grep 0 in `src/ tests/` excluding `obj/`), embedded resources use fixed `LogicalName`s (`Game.csproj:23-27`), assets load from `AppContext.BaseDirectory/assets/**` (`AssetLibrary.cs:35`, `SmoothFont.cs:148`, `SoundBank.cs:60`) — none of it keys on the product or assembly name.

---

## Appendix — commands run (all from repo root, Git Bash)

```
grep -rn "ResonanceHunter" . --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git --exclude-dir=art-reference --exclude="*.png" --exclude="lfs-trace.txt" | wc -l            # 1199
grep -rc "ResonanceHunter" . <same excludes> | grep -v ":0$" | sort -t: -k2 -nr                                                                               # per-file table
grep -rn "Resonance Hunter" . <same excludes> --exclude-dir=build | wc -l                                                                                      # 80
grep -rhoE "RH_[A-Z0-9_]+" . <excludes, --exclude=session-log.md> | sort | uniq -c | sort -nr                                                                  # 22 tokens
grep -rn "RH_" src/ --include="*.cs"                                                                                                                           # every code read
grep -rni "resonance" tools/ --exclude-dir=__pycache__
find . -iname "*resonance*" -not -path "*/bin/*" -not -path "*/obj/*" -not -path "./.git/*"
grep -rh "^namespace" src/ tests/ --include="*.cs" | sed 's/;$//' | sort | uniq -c | sort -nr                                                                  # 35 namespaces
grep -rh "^using ResonanceHunter" src/ tests/ --include="*.cs" | wc -l                                                                                         # 620
grep -rn "ResonanceHunter\." src/ tests/ --include="*.cs" | grep -v ":using \|:namespace "                                                                     # FQ refs
grep -rn 'cref="ResonanceHunter' src/ tests/ --include="*.cs" --exclude-dir=obj | wc -l                                                                        # 6
grep -rniE "resonan" src/ tests/ --include="*.cs" --exclude-dir=bin --exclude-dir=obj | sed -E 's/ResonanceHunter//g; s/ResonanceWeaving//g' | grep -ic "resonan"   # 159 gameplay uses
grep -rn "Window.Title" src/ResonanceHunter.Game/Game1.cs ; grep -rn "ApplicationData\|GetFolderPath\|SpecialFolder" src/ --include="*.cs"
grep -rn "InternalsVisibleTo\|GetName()\|\.Namespace\b\|GetManifestResourceStream\|AssemblyQualifiedName\|JsonDerivedType\|TypeInfoResolver\|\$type" src/ --include="*.cs"   # 0
grep -rn "ResonanceHunterAssets" . <excludes>                                                                                                                  # only docs/art-reference
grep -rn '"[^"]*ResonanceHunter[^"]*"' tests/ src/ --include="*.cs" --exclude-dir=obj                                                                          # 3 literals
grep -rni "idlexidle" . <excludes> ; grep -rn "IdleXIdle" . <excludes>
cat ResonanceHunter.slnx ; cat src/*/*.csproj tests/*/*/*.csproj ; cat src/ResonanceHunter.Game/app.manifest src/ResonanceHunter.Game/.vscode/launch.json
dotnet --version ; dotnet sln ResonanceHunter.slnx list ; dotnet build ResonanceHunter.slnx --no-restore ; dotnet restore
git log --oneline -S"IDLExIDLE" -- src/ResonanceHunter.Game/ResonanceHunter.Game.csproj src/ResonanceHunter.Game/Game1.cs ; git log --all --oneline -- "*.sln"
gh run list --limit 4 ; gh run view 33390896201 --log | grep -iE "installed|DOTNET_ROOT|Restored"
grep -n "old saves intact" production/session-logs/session-log.md
```
