# v0.1 Manual QA Pass 2 Result

Source checklist: `docs/16_v0_1_manual_qa_checklist.md`

QA date: 2026-05-27

Historical note: this QA pass predates the first-pass Hold'em implementation. Current Hold'em validation expectations are in `docs/16_v0_1_manual_qa_checklist.md`.

## Resume After BLOCKER-002 Font Fix

Manual QA Pass 2 was resumed after the reported BLOCKER-001 and BLOCKER-002 fixes. Interactive gameplay QA did not continue past the startup gate because the standalone player reproduced the TextMeshPro font/settings failure immediately on launch.

Per the task's special handling, this pass stops here and does not start a broad font-system refactor.

## Environment

| Field | Value |
|---|---|
| Foreground project | `C:\Users\song\desktop\Capstone` |
| Player build tested | `C:\Users\song\Desktop\Capstone_validation_outputs\blocker001-fix-player\CapstoneQA.exe` |
| Player log | `C:\Users\song\Desktop\Capstone_validation_outputs\manual-qa-pass-2-player.log` |
| Screenshot | `C:\Users\song\Desktop\Capstone_validation_outputs\manual-qa-pass-2\blocker002-font-repro-startup.png` |
| Unity version | `6000.3.11f1` |
| Build target | Windows Standalone x64 |

Artifact note:

- The only standalone player executable found under `C:\Users\song\Desktop\Capstone_validation_outputs` was `blocker001-fix-player\CapstoneQA.exe` with timestamp `2026-05-27 19:46:01`.
- If BLOCKER-002 produced a newer standalone build in another location, that artifact was not visible from this QA pass.

## Required Order Results

| Order | Item | Status | Notes |
|---:|---|---|---|
| 1 | Confirm build/font gate is now clear. | BLOCKED | Player build launches, but the font/text gate is not clear. Startup log reproduced `TMPro.TMP_Settings.get_defaultFontAsset()` null reference errors. |
| 2 | Smoke-test `MainMenu -> CharacterSelect -> Dice -> GameExploreScene`. | BLOCKED | Not executed. Stopped at startup font/text blocker. |
| 3 | Complete one Dice normal combat and verify return to explore. | BLOCKED | Not executed. Stopped at startup font/text blocker. |
| 4 | Advance to item box, choose a power-up, and verify the next encounter remains playable. | BLOCKED | Not executed. Stopped at startup font/text blocker. |
| 5 | Complete Dice boss combat and record victory or stage-advance behavior. | BLOCKED | Not executed. Stopped at startup font/text blocker. |
| 6 | Trigger Dice defeat and verify return/new-game reset. | BLOCKED | Not executed. Stopped at startup font/text blocker. |
| 7 | Smoke-test `MainMenu -> CharacterSelect -> Mahjong -> GameExploreScene`. | BLOCKED | Not executed. Stopped at startup font/text blocker. |
| 8 | Complete one Mahjong normal combat and verify return to explore. | BLOCKED | Not executed. Stopped at startup font/text blocker. |
| 9 | Advance Mahjong through item box and boss if time allows. | BLOCKED | Not executed. Stopped at startup font/text blocker. |
| 10 | Test Hold'em route/behavior and record the actual route. | BLOCKED | Not executed. Stopped at startup font/text blocker. |
| 11 | Run cross-scene UI, sprite/animation, audio, and debug-console regression passes. | BLOCKED | Not executed. Stopped at startup font/text blocker. |

## BLOCKER-002 Reproduced: Standalone Player TextMeshPro Default Font Failure

Status: BLOCKED

Exact reproduction steps:

1. Launch the standalone player:

```powershell
Start-Process -FilePath 'C:\Users\song\Desktop\Capstone_validation_outputs\blocker001-fix-player\CapstoneQA.exe' -ArgumentList @(
  '-screen-width','1280',
  '-screen-height','720',
  '-screen-fullscreen','0',
  '-logFile','C:\Users\song\Desktop\Capstone_validation_outputs\manual-qa-pass-2-player.log'
)
```

2. Wait for the first runtime scene to load.
3. Inspect the player log and startup screen.

Actual behavior:

- The player process launches and displays the main-menu background/logo image.
- The player log immediately records repeated TextMeshPro null reference exceptions:

```text
NullReferenceException: Object reference not set to an instance of an object
  at TMPro.TMP_Settings.get_defaultFontAsset ()
  at TMPro.TextMeshProUGUI.LoadFontAsset ()
  at TMPro.TextMeshProUGUI.Awake ()
```

- The affected area is the startup `MainMenu` scene UI text layer, before the Play/CharacterSelect route could be tested.

Expected behavior:

- The standalone player should initialize TextMeshPro UI without null reference exceptions.
- Main menu UI text/buttons should render reliably so QA can continue through `MainMenu -> CharacterSelect -> Dice -> GameExploreScene`.

Screenshot/log path:

- Screenshot: `C:\Users\song\Desktop\Capstone_validation_outputs\manual-qa-pass-2\blocker002-font-repro-startup.png`
- Player log: `C:\Users\song\Desktop\Capstone_validation_outputs\manual-qa-pass-2-player.log`

Suspected file/class if obvious:

- `TMPro.TMP_Settings`
- `TMPro.TextMeshProUGUI`
- Project TextMeshPro settings/default font asset wiring for the player build

Issue type:

- UI/font/visual issue
- scene/build wiring issue
- possible asset missing issue

Proposed next task:

- Reopen BLOCKER-002 as a focused player-build font/settings task.
- Verify the player build includes a valid TextMeshPro settings asset and default font asset.
- Rebuild Windows Standalone x64, then relaunch the player and confirm no `TMP_Settings.get_defaultFontAsset()` exceptions occur before resuming gameplay QA.

Stop condition:

- Stop this QA pass immediately because the font/text issue reproduced.
- Do not diagnose or refactor the broader font system during this manual QA pass.
- Do not continue to gameplay checklist items until the standalone startup text gate is clear.

## Resume After Validation Base Correction

Manual QA Pass 2 resumed after the validation/execution base problem was corrected. The prior BLOCKER-002 reproduction in this document is retained as useful history, but is now classified as a stale artifact / wrong base issue unless it reproduces from the corrected build.

### Corrected Gate Status

| Field | Value |
|---|---|
| Corrected source commit / snapshot commit | `8b55edfd624dc487ec34cb0948a655c2a1b983bf` |
| Corrected validation worktree | `C:\Users\song\Desktop\Capstone_blocker002_integrated_validation` |
| Build artifact used | `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player\CapstoneQA.exe` |
| Artifact rebuilt after BLOCKER-001 | Yes. Corrected artifact timestamp observed as `2026-05-27 20:39:58`. |
| Unity version | `6000.3.11f1` |
| Build target | Windows Standalone x64 |
| Visible text/font confirmed in `MainMenu` | Yes, confirmed by user direct validation on corrected build. |
| Fresh build or previously built artifact | Previously built corrected artifact, not the older `blocker001-fix-player` artifact. |
| QA execution owner | User direct gameplay validation; Codex records results and does not launch stale artifacts. |

### Stale Artifact Reclassification

Status: PASS for procedure correction.

Exact reproduction / review steps:

1. Compare the earlier failed player artifact with the corrected artifact.
2. Earlier run used `C:\Users\song\Desktop\Capstone_validation_outputs\blocker001-fix-player\CapstoneQA.exe`.
3. Corrected run uses `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player\CapstoneQA.exe`.
4. User directly verified that visible `MainMenu` text/font is normal on the corrected build.

Actual behavior:

- The earlier TextMeshPro startup issue is treated as caused by stale validation base / wrong build artifact selection.
- Corrected build proceeds past the font/text gate by user verification.

Expected behavior:

- Manual QA must use only the corrected validation base and corrected build artifact.
- Previously observed bugs from stale artifacts must not generate gameplay or font-system fixes.

Screenshot/log path:

- Corrected artifact startup evidence from prior investigation: `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player-startup.png`
- Corrected player logs from prior investigation: `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player.log`, `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player-live.log`

Suspected file/class if obvious:

- Not a current gameplay or font defect.
- Procedure issue: build artifact/base selection.

Issue type:

- stale artifact / wrong base issue
- validation environment issue

Proposed next task:

- Continue gameplay checklist from corrected build, starting with `MainMenu -> CharacterSelect -> Dice -> GameExploreScene`.

Stop condition:

- Stop only if the same font/text issue reproduces from `blocker002-integrated-player\CapstoneQA.exe`; if so, record it as a focused font/TMP follow-up.

### Corrected Required Order Results

| Order | Item | Status | Notes |
|---:|---|---|---|
| 1 | Confirm the corrected build artifact launches. | PASS | User confirmed corrected artifact launches. Corrected artifact path: `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player\CapstoneQA.exe`. |
| 2 | Confirm visible text/font in `MainMenu`. | PASS | User directly confirmed normal visible text/font in `MainMenu` on the corrected build. Prior font issue is reclassified as stale artifact / wrong base unless it reproduces again here. |

### Item 1 Detail: Corrected Build Artifact Launch

Status: PASS

Exact reproduction steps:

1. Use corrected build artifact only: `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player\CapstoneQA.exe`.
2. Launch the player from the corrected validation base output.
3. Do not reuse `blocker001-fix-player\CapstoneQA.exe`.

Actual behavior:

- Corrected player artifact launches successfully by user direct validation.

Expected behavior:

- Windows Standalone x64 player starts from the corrected BLOCKER-001/BLOCKER-002 integrated base.

Screenshot/log path if available:

- `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player-startup.png`
- `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player.log`

Suspected file/class if obvious:

- None.

Issue type:

- Not an active issue.
- Prior failed run classified as stale artifact / wrong base issue.

Proposed next task:

- Continue to Dice smoke path.

Stop condition:

- Stop if the launched artifact path is not the corrected `blocker002-integrated-player` artifact.

### Item 2 Detail: MainMenu Visible Text/Font

Status: PASS

Exact reproduction steps:

1. Launch corrected build artifact.
2. Observe `MainMenu`.
3. Confirm visible UI text/font renders normally.

Actual behavior:

- User directly confirmed `MainMenu` visible text/font is normal on the corrected build.
- The previous TextMeshPro/default-font failure did not reproduce on the corrected build per user verification.

Expected behavior:

- `MainMenu` text and buttons render visibly with no font/TMP startup blocker.

Screenshot/log path if available:

- `C:\Users\song\Desktop\Capstone_validation_outputs\blocker002-integrated-player-startup.png`

Suspected file/class if obvious:

- None for the corrected build.

Issue type:

- Not an active UI/font/visual issue.
- Prior failure classified as stale artifact / wrong base issue.

Proposed next task:

- Continue with `MainMenu -> CharacterSelect -> Dice -> GameExploreScene`.

Stop condition:

- If font/text failure reproduces again on the corrected artifact, stop and propose focused font/TMP follow-up.
