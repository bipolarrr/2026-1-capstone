# Existing Markdown Drift Matrix

- Post-audit closure date: 2026-06-12 KST
- Current HEAD at closure: `311ae2e6e5ccb653d8b6df606bcdb2896bfebdb2`
- Initial audit baseline commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`

## Post-Audit Master Delta Drift Update

This section updates the drift classification after `e1337d16`, `d007d473`, and `311ae2e6`. The original matrix below remains the 2026-06-11 baseline at `e6de7c9`.

| Topic | Initial drift finding | Latest master status | Current documentation action |
|---|---|---|---|
| Hold'em fallback/prototype claims | Old audit said Hold'em docs/prototype conflicted with tracked route because tracked route fell back to Dice. | Superseded. Hold'em is now promoted in tracked runtime, assets, builder, scene, tests, build settings, and route. | Non-audit docs should be updated to say Hold'em is implemented/validated with builder churn risk, not fallback-only. |
| Player build blocked by invalid scene path | Old validation docs said player build failed because BuildPlayer received an invalid empty scene path. | Historical baseline. Latest runtime and Hold'em player builds pass and produce executables. | Keep old failure only when explicitly labeled initial baseline at `e6de7c9`. |
| No tracked scene files | Old audit said HEAD contained no tracked `Assets/Scenes/*.unity`. | Historical baseline. Latest `HEAD` tracks six runtime scene files and `.meta` files. | Update scene/source-of-truth docs to distinguish tracked scenes from remaining builder churn risk. |
| Runtime asset missing/untracked claims | Old manifest listed several missing/untracked/count mismatched runtime references. | Partially mitigated. `311ae2e6` tracks selected runtime source-of-truth assets and adds a passing reference test. | Keep original manifest as baseline; use `05_runtime_asset_reference_manifest.md` delta for current planning. |
| Mahjong tile back expectation | Old repair result noted generated back-sprite expectation disagreed with `tile_back_acorn`. | Superseded. `tile_back_acorn` is current intended database-backed hidden tile back and tests pass. | Use `d007d473` and Mahjong validation artifacts as current evidence. |
| Runtime validation result docs | Older dirty validation note reported failures against a broad dirty aggregate. | Superseded by narrower commits and validation. Runtime asset commit `311ae2e6` passed 132/132 and player build, with hygiene risk. | Prefer `13_runtime_asset_source_of_truth_validation_result.md` over `11_runtime_asset_reference_repair_result.md` for latest runtime asset status. |
| Validation hygiene | Old audit reported dirty validation worktrees. | Still current. Latest runtime validation also dirtied tracked URP/project settings. | Keep as open risk; do not treat validation-worktree settings churn as intended foreground edits. |
| Manual QA | Old manual QA docs are historical. | Still unknown for latest committed build behavior. | Manual visual/playthrough QA remains a next action. |

- Audit date: 2026-06-11 KST
- Repository commit: `e6de7c933ddf8e7c89317e291da72f6c998ef799`
- Evidence scope: all `.md` files listed by `rg --files -g "*.md"`, compared against runtime code, asset references, build settings, and Unity validation.
- Unity validation run: yes
- Uses old `.md` as factual evidence: no. Exceptions: old markdown is used only as claim source for drift comparison, plus AGENTS safety rules and Grok/provenance docs.

## Markdown Inventory

Inventory command: `rg --files -g "*.md"`.

Current audit files below `docs/audit_current/**` were created by this task and are excluded from stale-doc classification.

```text
CLAUDE.md
README.md
tools\sprite_pipeline\README.md
docs\tuning.md
docs\task-000c-approved-files.md
tools\external\waifu2x-ncnn-vulkan\README.md
docs\holdem_image_prompts.md
SpritePipelineWork\Slime_Normalized_Candidate\README.md
docs\holdem_battle.md
docs\grok_source_still_checklist.md
docs\grok_manual_generation_packet.md
docs\grok_generation_queue.md
docs\grok-imagine-sprite-prompts.md
docs\enemy-dice-asset-prompts.md
docs\audit_current\06_grok_and_sprite_pipeline_current.md
docs\audit_current\05_runtime_asset_reference_manifest.md
docs\audit_current\04_validation_current.md
docs\audit_current\03_scene_and_build_current.md
docs\audit_current\02_runtime_architecture_current.md
docs\audit_current\01_repo_inventory_current.md
docs\audit_current\00_index.md
docs\audit_current\00_audit_run_record.md
docs\asset_postprocess_plan.md
docs\asset_audit_manifest.md
docs\asset_acceptance_contract.md
docs\assets.md
docs\20_sprite_promotion_policy_draft.md
docs\19_sprite_upscale_pipeline_plan.md
docs\18_v0_1_manual_qa_pass_2_result.md
docs\17_v0_1_manual_qa_pass_1_result.md
docs\16_v0_1_manual_qa_checklist.md
docs\15_validation_baseline_result.md
docs\14_scene_build_hygiene_result.md
docs\13_next_backlog.md
docs\12_v0_1_scope.md
docs\11_project_decisions.md
docs\10_validation_triage.md
docs\09_scene_source_of_truth_decision.md
docs\08_open_questions.md
docs\02_unity_scene_and_object_construction.md
docs\01_architecture_map.md
docs\00_project_brief.md
docs\00_actual_project_audit.md
CLAUDE.original.md
SPRITE_GENERATION_GUIDE.md
AGENTS.md
SpritePipelineWork\golem_attack_normalized_candidate_v2\normalization_report.md
SpritePipelineWork\slime_hit\selected\unity_copy_plan.md
SpritePipelineWork\golem_hit\review_diagnostics\automated_review_notes.md
SpritePipelineWork\golem_attack_normalized_candidate\normalization_report.md
SpritePipelineWork\golem_idle\review_diagnostics\alpha_opacity_notes.md
SpritePipelineWork\Golem_Regeneration_Candidate\Reports\README.md
SpritePipelineWork\Golem_Regeneration_Candidate\Reports\golem_regeneration_work_plan.md
SpritePipelineWork\Golem_Regeneration_Candidate\Reports\golem_regeneration_frame_prompts.md
SpritePipelineWork\Golem_Regeneration_Candidate\Reports\golem_ingame_application.md
SpritePipelineWork\golem_dead_normalized_candidate_v3\repair_frame_report.md
SpritePipelineWork\golem_attack\review_diagnostics\automated_review_notes.md
SpritePipelineWork\slime_hit\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\upscale_candidate_report.md
Assets\TextMesh Pro\Fonts\decimal_font_numbers.md
SpritePipelineWork\slime_hit\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\review_diagnostics\alpha_policy_diagnostic.md
SpritePipelineWork\golem_hit_normalized_candidate_v3\normalization_report.md
SpritePipelineWork\golem_dead_normalized_candidate_v3\normalization_report.md
SpritePipelineWork\golem_dead_normalized_candidate_v3\image_repair_blocked.md
SpritePipelineWork\golem_normalization_experiments\normalization_strategy.md
SpritePipelineWork\slime_hit\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\review\ai_upscale_review_report.md
SpritePipelineWork\golem_idle_normalized_candidate_v3\normalization_report.md
SpritePipelineWork\golem_dead_normalized_candidate\normalization_report.md
SpritePipelineWork\golem_hit_normalized_candidate\normalization_report.md
SpritePipelineWork\golem_idle_normalized_candidate\normalization_report.md
SpritePipelineWork\goblin_hit\selected_alpha_repair_v2\repair_notes.md
SpritePipelineWork\Slime_Attack_AlphaRepair_Candidate\Reports\repaired_frames.md
SpritePipelineWork\Slime_Attack_AlphaRepair_Candidate\README.md
SpritePipelineWork\slime_hit\batch_480p_assetization\assetization_report.md
SpritePipelineWork\slime_hit\batch_480p_assetization\review\ai_assetization_review_report.md
SpritePipelineWork\bat_attack\selected\unity_copy_plan.md
SpritePipelineWork\golem_dead\review_diagnostics\automated_review_notes.md
SpritePipelineWork\goblin_hit\selected_alpha_repair\repair_notes.md
SpritePipelineWork\bat_hit\selected\unity_copy_plan.md
SpritePipelineWork\batch_480p_ai_assetization\skipped_existing_assets.md
SpritePipelineWork\batch_480p_ai_assetization\skipped_approved_assets.md
SpritePipelineWork\batch_480p_ai_assetization\inventory_480p_videos.md
SpritePipelineWork\batch_480p_ai_assetization\failed_or_ambiguous_assets.md
SpritePipelineWork\batch_480p_ai_assetization\existing_but_unapproved_assets.md
SpritePipelineWork\batch_480p_ai_assetization\created_missing_assets.md
SpritePipelineWork\batch_480p_ai_assetization\batch_asset_records.md
SpritePipelineWork\batch_480p_ai_assetization\batch_assetization_report.md
REFACTOR_BACKLOG.md
SpritePipelineWork\slime_attack\selected\unity_copy_plan.md
SpritePipelineWork\golem_attack_normalized_candidate_v3\repair_frame_report.md
SpritePipelineWork\goblin_hit\selected\unity_copy_plan.md
SpritePipelineWork\goblin_hit\review_diagnostics\club_opacity_diagnostic.md
SpritePipelineWork\bat_dead\selected\unity_copy_plan.md
SpritePipelineWork\golem_attack_normalized_candidate_v3\normalization_report.md
SpritePipelineWork\golem_attack_normalized_candidate_v3\image_repair_blocked.md
SpritePipelineWork\slime_attack\batch_480p_assetization\assetization_report.md
SpritePipelineWork\slime_attack\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\upscale_candidate_report.md
SpritePipelineWork\slime_attack\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\review_diagnostics\alpha_policy_diagnostic.md
SpritePipelineWork\goblin_attack\upscaled_runtime_candidates_from_lowres\nearest\upscale_candidate_report.md
SpritePipelineWork\slime_attack\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\review\ai_upscale_review_report.md
SpritePipelineWork\golem_regeneration_packet\regeneration_summary.md
SpritePipelineWork\golem_regeneration_packet\golem_dead_regeneration_prompt.md
SpritePipelineWork\golem_regeneration_packet\golem_attack_regeneration_prompt.md
SpritePipelineWork\slime_attack\batch_480p_assetization\review\ai_assetization_review_report.md
SpritePipelineWork\goblin_attack\upscaled_runtime_candidates_from_lowres\lanczos\upscale_candidate_report.md
SpritePipelineWork\goblin_attack\lowres_selected_source\lowres_selected_source_report.md
SpritePipelineWork\goblin_dead\selected\unity_copy_plan.md
SpritePipelineWork\goblin_attack\upscaled_runtime_candidates_from_lowres\human_review_packet\human_review_report.md
SpritePipelineWork\goblin_attack\upscaled_runtime_candidates_from_lowres\comparison\lowres_upscale_comparison_report.md
SpritePipelineWork\goblin_attack\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\upscale_candidate_report.md
SpritePipelineWork\goblin_attack\selected\unity_copy_plan.md
SpritePipelineWork\goblin_attack\selected\destination_resolve_report.md
SpritePipelineWork\goblin_attack\upscaled_runtime_candidates_from_lowres\bicubic\upscale_candidate_report.md
SpritePipelineWork\goblin_attack\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\review\ai_upscale_review_report.md
SpritePipelineWork\goblin_attack\promotion_result.md
SpritePipelineWork\goblin_dead\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\upscale_candidate_report.md
SpritePipelineWork\goblin_dead\batch_480p_assetization\upscaled_runtime_candidate\waifu2x\review\ai_upscale_review_report.md
SpritePipelineWork\goblin_attack\upscaled_runtime_candidates\nearest\upscale_candidate_report.md
SpritePipelineWork\goblin_attack\batch_480p_assetization\assetization_report.md
SpritePipelineWork\goblin_attack\batch_480p_assetization\review\ai_assetization_review_report.md
SpritePipelineWork\goblin_attack\upscaled_runtime_candidates\lanczos\upscale_candidate_report.md
SpritePipelineWork\goblin_attack\upscaled_runtime_candidates\bicubic\upscale_candidate_report.md
SpritePipelineWork\goblin_dead\batch_480p_assetization\assetization_report.md
SpritePipelineWork\goblin_attack\upscaled_runtime_candidates\comparison\upscale_comparison_report.md
SpritePipelineWork\goblin_dead\batch_480p_assetization\review\ai_assetization_review_report.md
SpritePipelineWork\Goblin_Attack_Normalized_Candidate\README.md
```

## Document Type Summary

| Category | Files |
|---|---|
| operating rule | `AGENTS.md`, `CLAUDE.md`, `CLAUDE.original.md`, `.claude`-derived rule references mentioned by AGENTS |
| project overview / historical architecture | `README.md`, `docs/00_project_brief.md`, `docs/01_architecture_map.md`, `docs/02_unity_scene_and_object_construction.md`, `docs/00_actual_project_audit.md` |
| design decision / scope | `docs/09_scene_source_of_truth_decision.md`, `docs/11_project_decisions.md`, `docs/12_v0_1_scope.md`, `docs/13_next_backlog.md` |
| QA / validation result | `docs/10_validation_triage.md`, `docs/14_scene_build_hygiene_result.md`, `docs/15_validation_baseline_result.md`, `docs/16_v0_1_manual_qa_checklist.md`, `docs/17_v0_1_manual_qa_pass_1_result.md`, `docs/18_v0_1_manual_qa_pass_2_result.md` |
| backlog | `REFACTOR_BACKLOG.md`, `docs/task-000c-approved-files.md` |
| asset provenance / Grok prompt | `docs/grok-*.md`, `docs/*asset*.md`, `SPRITE_GENERATION_GUIDE.md`, `docs/enemy-dice-asset-prompts.md`, `docs/holdem_image_prompts.md` |
| pipeline doc/report | `tools/sprite_pipeline/README.md`, `SpritePipelineWork/**/*.md`, `tools/external/waifu2x-ncnn-vulkan/README.md` |
| current audit | `docs/audit_current/*.md` |

## Drift Matrix

| Markdown file | Claimed topic | Claim summary | Claim source quote or heading | Actual evidence used | Current status | Impact | Recommended action |
|---|---|---|---|---|---|---|---|
| `docs/00_actual_project_audit.md` | Holdem runtime | Claims or implies first-pass Holdem battle is part of current runtime. | Holdem-related audit sections | `GameExploreController.ResolveBattleSceneName` routes Holdem to `DiceBattleScene`; Holdem files are foreground untracked. | contradicted-by-current-evidence | High: can cause planning to assume a battle loop is shipped. | supersede with current audit |
| `docs/holdem_battle.md` | Holdem implementation | Describes Holdem battle source and scene routing as implemented. | Holdem battle doc heading/content | Tracked route sends Holdem to Dice; `Assets/Scripts/Holdem/**` and builder/scene are untracked foreground files. | contradicted-by-current-evidence | High: misstates actual validated route. | mark historical or rewrite later after explicit Holdem task |
| `REFACTOR_BACKLOG.md` | Holdem route update | Says first-pass Holdem supersedes placeholder and should route to Holdem scene. | recent update notes | Runtime still routes Holdem to Dice in tracked code. | contradicted-by-current-evidence | High: backlog status can direct wrong next work. | supersede with current audit |
| `README.md` | Weapon status | Says Holdem UI slot exists but is not implemented. | overview / status section | Tracked runtime route agrees with not implemented; foreground prototype exists untracked. | partially-current | Medium: does not explain dirty foreground prototype. | rewrite later |
| `docs/12_v0_1_scope.md` | Scope limits | Says node map/shop/relic/joker/Balatro-like systems are out of scope, but also contains some old feature state. | v0.1 scope headings | Source scan found no relic/joker/chips/mult system; foreground map presentation exists partially. | partially-current | Medium: scope rule remains useful; feature state needs update. | keep scope rule, supersede implementation claims |
| `docs/15_validation_baseline_result.md` | EditMode baseline | Records an older passing count. | validation baseline heading | Current validation is 82/82 passing on 2026-06-11. | historical | Medium: old pass count is obsolete. | supersede with `04_validation_current.md` |
| `docs/17_v0_1_manual_qa_pass_1_result.md` | Build failure | Records older player build failure details. | manual QA result heading | Current player build fails with invalid empty scene path, not the older captured error. | historical | Medium: wrong failure root can waste triage. | supersede with `04_validation_current.md` |
| `docs/18_v0_1_manual_qa_pass_2_result.md` | Manual QA | Records old manual QA state. | manual QA result heading | Current audit did not perform manual playthrough; player build is blocked. | historical | Medium: should not be used as current QA. | mark historical |
| `docs/09_scene_source_of_truth_decision.md` | Scene policy | Establishes generated scenes/source-of-truth decision. | scene source of truth heading | Clean HEAD tracks no scenes; foreground has dirty build settings and untracked/ignored scenes. | partially-current | High: policy and actual repo state are misaligned. | rewrite later after scene policy decision |
| `docs/assets.md` | Asset inventory | Describes asset dependencies and current/missing art. | asset dependency headings | Current manifest finds changed counts/status: Slime Attack/Hit foreground untracked, Goblin Hit count mismatch, Stage2 assets untracked. | partially-current | High: asset tasks may target stale state. | supersede with `05_runtime_asset_reference_manifest.md` and rewrite later |
| `docs/asset_audit_manifest.md` | Asset audit | Lists asset status claims. | asset audit headings | Current asset scan differs for several generated/runtime paths and tracking states. | partially-current / stale | High: can misclassify runtime art readiness. | rewrite later |
| `docs/grok_generation_queue.md` | Grok queue | Lists manual generation queue and "do not write Assets" policy. | queue headings | Policy is useful; actual foreground contains some newly present untracked runtime folders. | current-provenance with stale items | Medium: useful as provenance, not current state. | keep as provenance, refresh queue later |
| `docs/grok_manual_generation_packet.md` | Grok prompt handoff | Provides manual prompt packet. | packet heading | Used only as provenance; not runtime evidence. | current-provenance | Low | keep as provenance |
| `docs/grok_source_still_checklist.md` | Source still checklist | Provides acceptance checklist. | checklist heading | Used only as provenance/policy. | current-provenance | Low | keep as provenance |
| `tools/sprite_pipeline/README.md` | Pipeline operation | Describes review-first pipeline and no default `Assets/**` writes. | README workflow headings | Script help/source mostly matches; promotion/write flags can mutate `Assets/**`. | current-operational | Medium: must distinguish default review from promotion. | keep as operational pipeline doc |
| `SpritePipelineWork/goblin_attack/promotion_result.md` | Prior promotion | Records promoted Goblin Attack frames. | promotion result heading | Foreground Goblin Attack has 50 modified tracked PNGs matching stage frame count. | current-provenance | Medium: proves prior work, not current approval. | keep as provenance |
| `SpritePipelineWork/*/unity_copy_plan.md` | Selected frame copy plans | Says selected frames are review-only until approved import. | unity copy plan headings | No promotion was run during this audit; some runtime folders are already changed/untracked from previous work. | current-provenance | Medium | keep as provenance |
| `docs/19_sprite_upscale_pipeline_plan.md` | Future upscale pipeline | Describes planned pipeline behavior. | plan heading | Actual scripts exist foreground untracked and support candidate/upscale flows. | partially-current | Medium | rewrite later after pipeline stabilization |
| `docs/20_sprite_promotion_policy_draft.md` | Promotion policy | Draft promotion rules. | policy draft heading | Actual `promote_selected_asset.py` supports destructive commit mode. | partially-current | Medium | keep as draft, supersede with current pipeline audit |

## Replacement Rule

For current implementation, validation, scene/build, and runtime asset status, use the new `docs/audit_current/**` files instead of historical markdown claims. Existing markdown may still be useful as operating policy or provenance when explicitly marked that way.
