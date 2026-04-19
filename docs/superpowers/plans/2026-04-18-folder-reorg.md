# Folder Reorganization Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the Unity project's Assets folder into a `_Project/` + `ThirdParty/` structure, remove URP template samples, and migrate Nature Forest + InputSystem assets per spec.

**Architecture:** Pure filesystem + git operations — no code changes. Work in small focused commits. Use `git mv`/`git rm` for tracked files (preserves history), plain `mv`/`rm` for untracked files. Close Unity Editor during operations so it doesn't regenerate `.meta` files mid-move.

**Tech Stack:** Unity 6 (URP), bash, git. Paths relative to repo root `/Users/nopbard/Desktop/Unity/Modak-Golf/`.

**Spec reference:** [`docs/superpowers/specs/2026-04-18-folder-reorg-design.md`](../specs/2026-04-18-folder-reorg-design.md)

---

## Prerequisites & Current State

**Uncommitted changes on `main` (from prior MCP/package installation) — DO NOT touch:**
- `M Packages/manifest.json`, `Packages/packages-lock.json`
- `M ProjectSettings/QualitySettings.asset`, `ProjectSettings/VFXManager.asset`
- `?? .claude/`, `?? Packages/Coplay/`, `?? ProjectSettings/Packages/`, `?? ProjectSettings/UModeler-Hub.json`
- `?? Assets/Plugins/` (Roslyn), `?? Assets/Plugins.meta`

These remain in the working tree across this plan. The reorg only touches:
- `Assets/Assets/` (untracked Nature Forest)
- `Assets/InputSystem_Actions.inputactions` (tracked)
- `Assets/Scenes/`, `Assets/TutorialInfo/`, `Assets/Readme.asset` (tracked URP samples)

**Unity Editor MUST be closed for Tasks 2–7.** File operations while Unity is open will trigger meta regeneration and can corrupt references.

---

### Task 1: Pre-flight verification

**Files:** none (inspection only)

- [ ] **Step 1: Confirm Unity Editor is not running**

Run:
```bash
pgrep -fl "Unity.app/Contents/MacOS/Unity" || echo "Unity not running — OK to proceed"
```
Expected: `Unity not running — OK to proceed`. If Unity is running, close it via the Unity Hub / menu and rerun.

- [ ] **Step 2: Snapshot current git status**

Run:
```bash
git status --short
```
Expected output contains exactly these reorg-relevant lines (ignore unrelated MCP/package lines listed in Prerequisites):
```
?? Assets/Assets.meta
?? Assets/Assets/
```
Plus the tracked items that will be moved/removed (they show up later when we touch them).

- [ ] **Step 3: Create a feature branch**

Run:
```bash
git checkout -b chore/folder-reorg
```
Expected: `Switched to a new branch 'chore/folder-reorg'`.

---

### Task 2: Create `_Project/` skeleton

**Files:**
- Create: `Assets/_Project/Art/{Materials,Models,Textures,VFX,UI}/.gitkeep`
- Create: `Assets/_Project/Audio/{BGM,SFX}/.gitkeep`
- Create: `Assets/_Project/Prefabs/{Gameplay,Stages,UI}/.gitkeep`
- Create: `Assets/_Project/Scenes/.gitkeep`
- Create: `Assets/_Project/ScriptableObjects/{Stages,Settings}/.gitkeep`
- Create: `Assets/_Project/Scripts/{Core,Gameplay,Stages,UI,Audio,Input,Utils}/.gitkeep`
- Create: `Assets/_Project/Scripts/Gameplay/{Ball,Club,Hole,Camera}/.gitkeep`
- Create: `Assets/_Project/Input/.gitkeep` (InputSystem asset lands here in Task 4)
- Create: `Assets/_Project/Settings/.gitkeep`

**Why `.gitkeep`:** Unity ignores dot-prefixed files (hidden), so no `.meta` gets generated for them — they're purely a git mechanism to commit empty folders. Once real content arrives, `.gitkeep` can be deleted.

- [ ] **Step 1: Create directory tree**

Run:
```bash
cd /Users/nopbard/Desktop/Unity/Modak-Golf
mkdir -p \
  Assets/_Project/Art/Materials \
  Assets/_Project/Art/Models \
  Assets/_Project/Art/Textures \
  Assets/_Project/Art/VFX \
  Assets/_Project/Art/UI \
  Assets/_Project/Audio/BGM \
  Assets/_Project/Audio/SFX \
  Assets/_Project/Prefabs/Gameplay \
  Assets/_Project/Prefabs/Stages \
  Assets/_Project/Prefabs/UI \
  Assets/_Project/Scenes \
  Assets/_Project/ScriptableObjects/Stages \
  Assets/_Project/ScriptableObjects/Settings \
  Assets/_Project/Scripts/Core \
  Assets/_Project/Scripts/Gameplay/Ball \
  Assets/_Project/Scripts/Gameplay/Club \
  Assets/_Project/Scripts/Gameplay/Hole \
  Assets/_Project/Scripts/Gameplay/Camera \
  Assets/_Project/Scripts/Stages \
  Assets/_Project/Scripts/UI \
  Assets/_Project/Scripts/Audio \
  Assets/_Project/Scripts/Input \
  Assets/_Project/Scripts/Utils \
  Assets/_Project/Input \
  Assets/_Project/Settings
```
Expected: no output, exit code 0.

- [ ] **Step 2: Add `.gitkeep` placeholders**

Run:
```bash
find Assets/_Project -type d -empty -exec touch {}/.gitkeep \;
```
Expected: no output.

- [ ] **Step 3: Verify tree**

Run:
```bash
find Assets/_Project -type f | sort
```
Expected: 25 `.gitkeep` lines (one per leaf folder). If the count differs, inspect and re-run Step 1.

- [ ] **Step 4: Stage and commit**

Run:
```bash
git add Assets/_Project
git commit -m "$(cat <<'EOF'
chore: scaffold _Project/ folder skeleton

Pre-create the target folder tree (Art/Audio/Prefabs/Scenes/
ScriptableObjects/Scripts/Input/Settings) with .gitkeep placeholders
so subsequent moves have destinations. Per folder-reorg design spec.
EOF
)"
```
Expected: `1 file changed` style output, 25 files committed.

---

### Task 3: Create `ThirdParty/` directory

**Files:**
- Create: `Assets/ThirdParty/.gitkeep` (will be deleted in Task 4 when NatureForest arrives)

- [ ] **Step 1: Create directory**

Run:
```bash
mkdir -p Assets/ThirdParty
touch Assets/ThirdParty/.gitkeep
git add Assets/ThirdParty/.gitkeep
git commit -m "chore: add ThirdParty/ folder for external assets"
```
Expected: commit succeeds.

---

### Task 4: Migrate Nature Forest to `Assets/ThirdParty/NatureForest/`

**Files:**
- Move: `Assets/Assets/Environment/Nature Forest/` → `Assets/ThirdParty/NatureForest/`
- Move: `Assets/Assets/Environment/Nature Forest.meta` → `Assets/ThirdParty/NatureForest.meta`
- Delete: `Assets/Assets/` (nested wrapper, including its `.meta` files for `Assets` and `Environment`)
- Delete: `Assets/Assets.meta` (at repo-Assets root, meta for the nested wrapper)

**Note:** Nature Forest is **untracked** in git, so `mv` is used (not `git mv`). Git will record the new files on `git add`.

- [ ] **Step 1: Remove ThirdParty placeholder (NatureForest will populate it)**

Run:
```bash
rm Assets/ThirdParty/.gitkeep
```

- [ ] **Step 2: Move Nature Forest folder**

Run:
```bash
mv "Assets/Assets/Environment/Nature Forest" "Assets/ThirdParty/NatureForest"
```
Expected: no output.

- [ ] **Step 3: Move its meta file**

Run:
```bash
mv "Assets/Assets/Environment/Nature Forest.meta" "Assets/ThirdParty/NatureForest.meta"
```

- [ ] **Step 4: Verify destination**

Run:
```bash
ls Assets/ThirdParty/NatureForest/
```
Expected: `Demo  Materials  Models  Prefabs  Textures` (directories, possibly with their `.meta` siblings).

- [ ] **Step 5: Delete the now-empty nested `Assets/Assets/` wrapper**

Run:
```bash
rm -rf Assets/Assets
rm -f Assets/Assets.meta
```

- [ ] **Step 6: Verify nothing remains at old location**

Run:
```bash
test ! -e Assets/Assets && echo "OK: removed"
```
Expected: `OK: removed`.

- [ ] **Step 7: Stage changes**

Run:
```bash
git add -A Assets/ThirdParty
git status --short | grep -E "Assets/(Assets|ThirdParty)" | head -10
```
Expected: lines beginning with `A  Assets/ThirdParty/NatureForest/...` (the placeholder `.gitkeep` deletion + new NatureForest files). **No** `?? Assets/Assets` lines — `Assets/Assets/` was untracked and has been deleted from the working tree, so git has nothing to record about it.

- [ ] **Step 8: Commit**

Run:
```bash
git commit -m "$(cat <<'EOF'
chore: move Nature Forest asset to ThirdParty/

Relocate from nested Assets/Assets/Environment/Nature Forest/ to
Assets/ThirdParty/NatureForest/ so external assets are clearly
separated. Collapse the redundant Assets/Assets/ wrapper.
EOF
)"
```
Expected: commit succeeds.

---

### Task 5: Migrate InputSystem actions into `_Project/Input/`

**Files:**
- Move: `Assets/InputSystem_Actions.inputactions` → `Assets/_Project/Input/InputSystem_Actions.inputactions`
- Move: `Assets/InputSystem_Actions.inputactions.meta` → `Assets/_Project/Input/InputSystem_Actions.inputactions.meta`
- Delete: `Assets/_Project/Input/.gitkeep`

**Note:** InputSystem asset is **tracked**, so `git mv` preserves rename history.

- [ ] **Step 1: Remove the Input placeholder**

Run:
```bash
git rm Assets/_Project/Input/.gitkeep
```
Expected: `rm 'Assets/_Project/Input/.gitkeep'`.

- [ ] **Step 2: Move the input actions file and its meta**

Run:
```bash
git mv Assets/InputSystem_Actions.inputactions Assets/_Project/Input/InputSystem_Actions.inputactions
git mv Assets/InputSystem_Actions.inputactions.meta Assets/_Project/Input/InputSystem_Actions.inputactions.meta
```
Expected: no output; git registers renames.

- [ ] **Step 3: Verify**

Run:
```bash
ls Assets/_Project/Input/
git status --short Assets/_Project/Input Assets/InputSystem_Actions.inputactions
```
Expected: listing shows `InputSystem_Actions.inputactions` and its `.meta`; status shows renames `R  Assets/InputSystem_Actions.inputactions -> Assets/_Project/Input/...`.

- [ ] **Step 4: Commit**

Run:
```bash
git commit -m "$(cat <<'EOF'
chore: move InputSystem_Actions into _Project/Input/

Relocate InputSystem_Actions.inputactions (+meta) from Assets root
to Assets/_Project/Input/ so input configuration lives alongside
other project assets.
EOF
)"
```
Expected: commit succeeds.

---

### Task 6: Delete URP template samples

**Files:**
- Delete: `Assets/Scenes/SampleScene.unity` (+ `.meta`)
- Delete: `Assets/Scenes.meta` (parent folder meta)
- Delete: `Assets/Scenes/` (folder itself, after contents gone)
- Delete: `Assets/TutorialInfo/` entirely (+ its own `.meta`)
- Delete: `Assets/Readme.asset` (+ `.meta`)

**Note:** All tracked — use `git rm`.

- [ ] **Step 1: Remove SampleScene**

Run:
```bash
git rm -r Assets/Scenes Assets/Scenes.meta
```
Expected: lists `SampleScene.unity`, `SampleScene.unity.meta`, `Scenes.meta` as removed.

- [ ] **Step 2: Remove TutorialInfo**

Run:
```bash
git rm -r Assets/TutorialInfo Assets/TutorialInfo.meta
```
Expected: lists `TutorialInfo/` contents (Icons/URP.png, Layout.wlt, Scripts/Readme.cs, Scripts/Editor/ReadmeEditor.cs, all `.meta`s) + parent `.meta` as removed.

- [ ] **Step 3: Remove root Readme asset**

Run:
```bash
git rm Assets/Readme.asset Assets/Readme.asset.meta
```

- [ ] **Step 4: Verify nothing of the template remains at Assets root**

Run:
```bash
ls Assets/
```
Expected top-level listing contains exactly: `_Project`, `Plugins`, `Plugins.meta`, `Settings`, `Settings.meta`, `ThirdParty`. No `Scenes`, `TutorialInfo`, `Readme.asset`, `InputSystem_Actions*`, `Assets`.

- [ ] **Step 5: Commit**

Run:
```bash
git commit -m "$(cat <<'EOF'
chore: remove URP template samples

Delete SampleScene, TutorialInfo/ (URP welcome), and the Readme
asset. Project now starts from a clean slate for gameplay content.
EOF
)"
```
Expected: commit succeeds.

---

### Task 7: Intermediate git-state sanity check

**Files:** none

- [ ] **Step 1: Summarize branch state**

Run:
```bash
git log --oneline chore/folder-reorg ^main
```
Expected: 5 commits in this order (most recent first):
1. `chore: remove URP template samples`
2. `chore: move InputSystem_Actions into _Project/Input/`
3. `chore: move Nature Forest asset to ThirdParty/`
4. `chore: add ThirdParty/ folder for external assets`
5. `chore: scaffold _Project/ folder skeleton`

- [ ] **Step 2: Confirm Assets root is now clean**

Run:
```bash
ls Assets/ | sort
```
Expected exact output (one per line):
```
Plugins
Plugins.meta
Settings
Settings.meta
ThirdParty
_Project
```

If anything else appears, stop and investigate before opening Unity.

---

### Task 8: Open Unity and verify no errors

**Files:** none directly; Unity will generate `.meta` files for newly created folders.

- [ ] **Step 1: Open the project in Unity**

Action: Open Unity Hub → open `/Users/nopbard/Desktop/Unity/Modak-Golf`.

Expected: Unity loads. It will scan `Assets/` and generate `.meta` files for each new folder it sees (`_Project.meta`, `_Project/Art.meta`, `_Project/Art/Materials.meta`, etc.). Initial import may take up to ~1 minute because `ThirdParty/NatureForest/` contains ~300 models/prefabs.

- [ ] **Step 2: Verify Console is clean**

In Unity, open `Window → General → Console`. Expected: zero red (error) messages. Yellow warnings (e.g., deprecated shader variants from NatureForest) are acceptable. If any red errors appear, capture their text and stop.

- [ ] **Step 3: Verify generated `.meta` count in shell**

Run in terminal:
```bash
find Assets/_Project -name "*.meta" | wc -l
```
Expected: a number ≥ 25 (one per folder you created; Unity may add a few more if it sees subdirs).

- [ ] **Step 4: Smoke-test URP pipeline**

In Unity: `File → New Scene → Basic (URP)`. Expected: scene opens, Game view renders with a camera + directional light, no pink shaders. Do not save this scene; close it without saving (we only want to confirm URP settings still resolve).

---

### Task 9: Commit Unity-generated meta files

**Files:** all newly generated `.meta` files under `Assets/_Project/` and `Assets/ThirdParty/`.

- [ ] **Step 1: Stage meta files**

Run:
```bash
git add Assets/_Project Assets/ThirdParty
```

- [ ] **Step 2: Review what is being added**

Run:
```bash
git status --short | head -40
```
Expected: many `A  Assets/_Project/**/*.meta` lines (and any newly settled NatureForest state).

- [ ] **Step 3: Commit**

Run:
```bash
git commit -m "$(cat <<'EOF'
chore: add Unity-generated .meta files for _Project/ skeleton

Capture .meta files Unity emitted on first import after the folder
reorg so GUIDs stay stable across clones.
EOF
)"
```
Expected: commit succeeds.

- [ ] **Step 4: Final acceptance check**

Run:
```bash
test ! -e Assets/Assets && \
test ! -e Assets/Scenes && \
test ! -e Assets/TutorialInfo && \
test ! -e Assets/Readme.asset && \
test -d Assets/_Project/Art && \
test -d Assets/ThirdParty/NatureForest && \
test -f Assets/_Project/Input/InputSystem_Actions.inputactions && \
echo "ALL ACCEPTANCE CHECKS PASS"
```
Expected: `ALL ACCEPTANCE CHECKS PASS`.

---

## Rollback

If any task fails irrecoverably:
```bash
git checkout main
git branch -D chore/folder-reorg
```
This discards all reorg commits and returns to the pre-reorg state. The MCP-related uncommitted changes in the working tree remain untouched.

## Out of Scope (do NOT do in this plan)

- Creating new scenes (`Boot`, `MainMenu`, `Gameplay`) — next plan
- Writing gameplay scripts — next plan
- Setting up Addressables, asmdef, or Editor tooling — future
- Committing the MCP-related uncommitted changes (`Packages/Coplay/`, etc.) — user's call
- Opening a PR or merging to `main` — defer until user confirms Unity Editor verification
