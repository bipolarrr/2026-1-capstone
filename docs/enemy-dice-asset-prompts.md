# Enemy Dice Asset Prompts And Import Plan

Scope: prompt and planning document only. This task does not create Unity asset folders, images, materials, prefabs, `.meta` files, scenes, or runtime code changes.

## Repo Investigation Summary

### Existing Dice Assets

Current d6 prefab paths:

- `Assets/Dices/Prefabs/Dice_d6_mine.prefab`
- `Assets/Dices/Prefabs/Dice_d6.prefab`

Current generated d6 source/build paths:

- Source image: `Assets/Dices/D6_mine.png`
- Generated atlas: `Assets/Dices/Generated/D6MineAtlas.png`
- Generated mesh: `Assets/Dices/Generated/D6MineMesh.asset`
- Generated material: `Assets/Dices/Generated/D6Mine.mat`
- Generated face sprites: `Assets/Textures/DiceFaces/face1.png` through `face6.png`

`Assets/Editor/DicePrefabBuilder.cs` builds `Dice_d6_mine.prefab` from `Assets/Dices/D6_mine.png`, writes a 3x2 face atlas, creates a mesh, creates/updates a material, and saves the prefab. It is a generated-asset path, so any enemy dice import workflow that uses this builder pattern is high risk and should be handled as a separate task.

### Enemy Dice Runtime And Builder Hooks

Relevant code:

- `Assets/Scripts/Battle/EnemyDiceRoller.cs`
- `Assets/Scripts/Battle/EnemyDiceResult.cs`
- `Assets/Scripts/Battle/EnemyDiceProfile.cs`
- `Assets/Scripts/Battle/EnemyCounterAttackDirector.cs`
- `Assets/Scripts/Battle/BattleControllerBase.cs`
- `Assets/Editor/DiceBattleSceneBuilder.cs`
- `Assets/Editor/DicePrefabBuilder.cs`
- `Assets/Scripts/Dice/Dice.cs`
- `Assets/Scripts/Stages/Stage1Forest.cs`
- `Assets/Scripts/Stages/StageData.cs`

Current flow:

1. `DiceBattleSceneBuilder` loads `Assets/Dices/Prefabs/Dice_d6_mine.prefab`, falling back to `Assets/Dices/Prefabs/Dice_d6.prefab`.
2. It creates five player dice and five enemy dice by instantiating the same prefab through `CreateDiceSet`.
3. It creates an `EnemyDiceRoller` and injects `enemyDice`, `vaultCenter`, `diceCamera`, a default `EnemyDiceProfileCatalog`, and `diceSpacing`.
4. `EnemyCounterAttackDirector` resolves an enemy dice profile ID per enemy, places the enemy dice, shows either the viewport overlay or center popup, and calls `EnemyDiceRoller.RollForEnemy(rank, diceProfileId, ...)`.
5. `BattleControllerBase.ResolveEnemyDiceProfileId(enemyName)` can return `MobDef.enemyDiceProfileId` or `BossDef.enemyDiceProfileId`.
6. `StageData.MobDef` and `StageData.BossDef` already have `enemyDiceProfileId` fields.

### Current Support Level

The code has partial data-path support for enemy dice profile IDs, but it does not currently support enemy-specific dice visual assets end to end.

Supported now:

- Enemy-specific profile ID lookup exists through stage data.
- `EnemyDiceProfileCatalog.Resolve(profileId)` exists.
- `EnemyDiceProfile` has fields for dice scale, spacing, arena, launch force, bounce, camera, and overlay sizing.
- `EnemyDiceRoller` applies scale, roll force, bounce, and camera settings from a resolved profile.

Not supported yet:

- `Stage1Forest` does not assign `enemyDiceProfileId` for Slime, Goblin, Bat, Skeleton, or the Stage 1 boss.
- `DiceBattleSceneBuilder` injects only `EnemyDiceProfileCatalog.CreateDefault(dicePrefab)`.
- `EnemyDiceProfile.prefab` exists, but `EnemyDiceRoller` does not instantiate or swap dice prefabs by profile.
- There is no enemy-specific material or texture field.
- There is no runtime material replacement path for enemy dice renderers.
- The enemy dice objects are created once by the builder from the default d6 prefab.

Conclusion: this document only prepares review-ready image prompts and a future import plan. Actual Unity connection requires a separate implementation task.

### Stage 1 Naming Note

`Stage1Forest` currently defines the Stage 1 boss with:

- Stage ID: `forest_1`
- Stage display name: `어둠의 숲`
- Boss name: `어둠의 지배자`
- Boss sprite path: `Assets/Mobs/Boss_Dracula_example.png`

For this document, `Dracula` means the Stage 1 boss art direction and candidate display concept. Do not assume the runtime key/name is `Dracula` unless a separate data task changes it.

### Asset Pipeline Safety

The sprite pipeline docs and tools are review-first. They write review artifacts under `SpritePipelineWork/<asset_id>/` and intentionally avoid writing into `Assets/` by default. The same policy should be used for enemy dice concept generation:

- First generate images outside `Assets/`.
- Build a review packet/contact sheet.
- Only after human approval, create/import Unity runtime assets in a separate task.
- Do not manually edit `.meta` files.
- Do not promote generated files into `Assets/` without an explicit import task and isolated Unity validation.

## Common Prompt Building Blocks

Use these as base text when generating any enemy dice image. Keep scale, camera, and readability consistent across all enemies.

### Single Dice Concept Render Base Prompt

```text
Create a stylized 3D d6 dice game asset, orthographic view, three-quarter view, readable pips, clean silhouette, transparent background, consistent scale, not a full scene, no text, no watermark. The dice must preserve a clear cube shape for a physics simulation object and should be readable from a small battle camera viewport.
```

### Six-Face Texture Reference Base Prompt

```text
Create a six-face reference sheet for a stylized 3D d6 dice game asset. Show all six dice faces from 1 to 6 as separate square face designs in flat orthographic view, consistent scale, readable pips, clean silhouette, transparent or plain background, not a full scene, no labels, no text, no watermark. Each face must keep the pip count clear at small size.
```

### Unity Material/Albedo Texture Base Prompt

```text
Create a Unity-ready albedo texture concept for a stylized 3D d6 dice game asset. Focus on readable material surface, clear pip marks, low visual noise, consistent scale, not a full scene, no labels, no text, no watermark. The texture should be suitable as a material or atlas reference for a cube dice mesh.
```

### Review Sheet Base Prompt

```text
Create a review sheet for a stylized 3D d6 dice game asset. Include one large orthographic three-quarter render, one front view, one side view, one top view, and six small face samples for pips 1 through 6. Use readable pips, clean silhouette, transparent or plain background, consistent scale, not a full scene, no text labels, no watermark.
```

## Enemy Prompts

Each enemy has four prompts:

- A. Single Concept Render Prompt
- B. Six-Face Reference Prompt
- C. Albedo/Texture Prompt
- D. Review Sheet Prompt

### Slime Dice

#### A. Single Concept Render Prompt

```text
Create a stylized 3D d6 dice game asset themed after a green slime enemy. The dice is a translucent jelly cube with rounded edges, soft elastic material, internal bubbles, glossy highlights, subtle surface tension, and readable bright pips embedded inside the slime. It should look bouncy, squishy, and slightly wobble-ready while still clearly preserving a cube dice silhouette for physics rolling. Orthographic view, three-quarter view, transparent background, consistent scale, clean silhouette, readable pips, not a full scene, no text, no watermark.
```

#### B. Six-Face Reference Prompt

```text
Create a six-face reference sheet for a stylized green slime d6 dice game asset. Show all six faces from 1 to 6 as separate square dice-face designs. Each face is translucent green jelly with rounded corners, internal bubbles, subtle slime streaks, surface-tension highlights, and clear readable pips embedded inside the slime. Use high contrast pale green or white pips with a darker inner rim so each count is readable. Flat orthographic face view, consistent scale, transparent or plain background, not a full scene, no labels, no text, no watermark.
```

#### C. Albedo/Texture Prompt

```text
Create a Unity-ready albedo texture concept for a green translucent slime d6 dice material. Glossy jelly surface, soft green gradients, internal bubbles, subtle slime streaks, rounded-edge highlight suggestions, and clear readable embedded pip marks or pip cutouts. Keep the texture clean and low-noise so pip counts remain readable on a small rolling dice. Game asset texture reference, consistent scale, not a full scene, no labels, no text, no watermark.
```

#### D. Review Sheet Prompt

```text
Create a review sheet for a stylized green slime d6 dice game asset. Include one large orthographic three-quarter render, one front view, one side view, one top view, and six small face samples for pips 1 through 6. The dice must look like a translucent bouncy jelly cube with rounded edges, internal bubbles, glossy highlights, and readable embedded pips. Clean silhouette, consistent scale, transparent or plain background, not a full scene, no text labels, no watermark.
```

### Skeleton Dice

#### A. Single Concept Render Prompt

```text
Create a stylized 3D d6 dice game asset themed after a skeleton enemy. The dice is assembled from interlocking bone plates and small bone fragments, aged ivory color, cracked bone surface, dark seams between pieces, and readable black recessed pips. It should feel like loose bones clattering on the ground when rolled while still clearly preserving a cube dice silhouette for physics simulation. Orthographic view, three-quarter view, transparent background, consistent scale, clean silhouette, readable pips, not a full scene, no text, no watermark.
```

#### B. Six-Face Reference Prompt

```text
Create a six-face reference sheet for a stylized skeleton bone d6 dice game asset. Show all six faces from 1 to 6 as separate square dice-face designs. Each face is made from aged ivory bone plates with small cracks, darker seams, subtle porous bone grain, and dark recessed pip holes. Add minimal skull, rib, or bone-joint motifs only if they do not reduce pip readability. Flat orthographic face view, consistent scale, transparent or plain background, not a full scene, no labels, no text, no watermark.
```

#### C. Albedo/Texture Prompt

```text
Create a Unity-ready albedo texture concept for a skeleton bone d6 dice material. Aged ivory bone, small cracks, darker seams, subtle porous grain, slightly dirty edges, and clear dark recessed pip marks. Keep the surface readable and not too noisy, with no gore and no blood. Game asset texture reference, consistent scale, not a full scene, no labels, no text, no watermark.
```

#### D. Review Sheet Prompt

```text
Create a review sheet for a stylized skeleton bone d6 dice game asset. Include one large orthographic three-quarter render, one front view, one side view, one top view, and six small face samples for pips 1 through 6. The dice should look assembled from old interlocking bone pieces with cracks, dark seams, and readable black recessed pips, like it would clatter when rolled. Clean silhouette, consistent scale, transparent or plain background, not a full scene, no text labels, no watermark.
```

### Bat Dice

#### A. Single Concept Render Prompt

```text
Create a stylized 3D d6 dice game asset themed after a purple bat enemy. The dice is lightweight and hollow-looking, with dark purple leather-like surfaces, subtle bat wing membrane patterns, low-profile wing motifs along the edges, pale moonlit readable pips, and tiny fang-like corner accents. It should feel light and quick when rolled while still clearly preserving a cube dice silhouette for physics simulation. Orthographic view, three-quarter view, transparent background, consistent scale, clean silhouette, readable pips, not a full scene, no text, no watermark.
```

#### B. Six-Face Reference Prompt

```text
Create a six-face reference sheet for a stylized purple bat d6 dice game asset. Show all six faces from 1 to 6 as separate square dice-face designs. Each face has dark violet leather texture, subtle bat wing membrane lines, faint midnight blue gradients, pale lavender or moonlight pips, and minimal gothic bat motifs. Keep the pips highly readable and avoid large protruding wings. Flat orthographic face view, consistent scale, transparent or plain background, not a full scene, no labels, no text, no watermark.
```

#### C. Albedo/Texture Prompt

```text
Create a Unity-ready albedo texture concept for a purple bat d6 dice material. Dark violet leather, subtle wing membrane veins, faint midnight blue gradients, pale lavender pip marks, small fang-like decorative scratches, and a lightweight hollow fantasy dice feeling. Keep the texture clean and readable at small camera size. Game asset texture reference, consistent scale, not a full scene, no labels, no text, no watermark.
```

#### D. Review Sheet Prompt

```text
Create a review sheet for a stylized purple bat d6 dice game asset. Include one large orthographic three-quarter render, one front view, one side view, one top view, and six small face samples for pips 1 through 6. The dice should look lightweight, dark purple, bat-themed, with wing membrane texture, low-profile edge motifs, and readable moonlit pips. Clean silhouette, consistent scale, transparent or plain background, not a full scene, no text labels, no watermark.
```

### Goblin Dice

#### A. Single Concept Render Prompt

```text
Create a stylized 3D d6 dice game asset themed after a goblin cheater gambler. The dice looks suspicious and rigged, with dirty green-brown material, worn edges, grime, brass inlays, small coin dents, card-suit scratches, slightly crooked but readable pips, and one subtle metal weight plug on a corner. It should feel like a dishonest loaded dice used by a goblin gambler while still clearly preserving a cube dice silhouette for physics simulation. Orthographic view, three-quarter view, transparent background, consistent scale, clean silhouette, readable pips, not a full scene, no text, no watermark.
```

#### B. Six-Face Reference Prompt

```text
Create a six-face reference sheet for a stylized goblin gambler d6 dice game asset. Show all six faces from 1 to 6 as separate square dice-face designs. Each face has dirty green-brown worn material, brass corner marks, grime, tiny card-suit scratches, coin-like dents, and crooked but readable pips. The dice should look rigged and dishonest, but the pip count must remain clear. Flat orthographic face view, consistent scale, transparent or plain background, not a full scene, no labels, no text, no watermark.
```

#### C. Albedo/Texture Prompt

```text
Create a Unity-ready albedo texture concept for a goblin cheater d6 dice material. Dirty green-brown surface, worn grime, brass inlays, small scratches, coin dents, card-suit markings, slightly uneven readable pip marks, and subtle metal weight plug detail. Keep the texture readable and not too noisy, suitable for a small rolling dice in a battle viewport. Game asset texture reference, consistent scale, not a full scene, no labels, no text, no watermark.
```

#### D. Review Sheet Prompt

```text
Create a review sheet for a stylized goblin gambler d6 dice game asset. Include one large orthographic three-quarter render, one front view, one side view, one top view, and six small face samples for pips 1 through 6. The dice should look like a dirty rigged gambling dice with brass details, crooked readable pips, grime, coin dents, and suspicious weighted construction. Clean silhouette, consistent scale, transparent or plain background, not a full scene, no text labels, no watermark.
```

### Dracula Dice

#### A. Single Concept Render Prompt

```text
Create a stylized 3D d6 dice game asset themed after a noble vampire boss, Dracula. The dice is luxurious and gothic, made of glossy black obsidian and deep burgundy enamel, with polished gold trim, red gemstone inlaid pips, elegant aristocratic crest details, and sharp but refined bevels. It should feel expensive, heavy, and boss-like, like a custom high-roller casino dice owned by a vampire count, while still clearly preserving a cube dice silhouette for physics simulation. Orthographic view, three-quarter view, transparent background, consistent scale, clean silhouette, readable pips, not a full scene, no text, no watermark.
```

#### B. Six-Face Reference Prompt

```text
Create a six-face reference sheet for a luxurious Dracula vampire boss d6 dice game asset. Show all six faces from 1 to 6 as separate square dice-face designs. Each face uses glossy black obsidian, deep burgundy enamel panels, polished gold trim, red gemstone pips, and subtle gothic noble crest motifs. Keep the pips highly readable and make the design more premium than normal enemy dice. Flat orthographic face view, consistent scale, transparent or plain background, not a full scene, no labels, no text, no watermark.
```

#### C. Albedo/Texture Prompt

```text
Create a Unity-ready albedo texture concept for a Dracula vampire boss d6 dice material. Glossy black obsidian, burgundy enamel panels, polished gold trim, red gemstone pip inlays, subtle gothic filigree, and aristocratic luxury object feeling. Keep strong contrast around the pips and avoid excessive ornament density. Game asset texture reference, consistent scale, not a full scene, no labels, no text, no watermark.
```

#### D. Review Sheet Prompt

```text
Create a review sheet for a luxurious Dracula vampire boss d6 dice game asset. Include one large orthographic three-quarter render, one front view, one side view, one top view, and six small face samples for pips 1 through 6. The dice should look expensive, heavy, gothic, vampire-themed, black and burgundy with gold trim and red gemstone pips, more premium than normal enemy dice. Clean silhouette, consistent scale, transparent or plain background, not a full scene, no text labels, no watermark.
```

## Negative Prompts

### Common Negative Prompt

```text
no unreadable pips, no extra dice, no hands, no character body, no background scene, no motion blur, no labels, no numbers unless explicitly requested, no watermark, no logo, no distorted cube shape, no overly complex decorations, no tiny illegible details, no realistic gore, no blood splatter, no non-dice object, no flat 2D icon unless requested, no perspective that hides the top face completely
```

### Slime Additional Negative Prompt

```text
no amorphous blob without cube silhouette, no puddle shape, no face or character expression, no opaque plastic cube, no pips hidden by bubbles, no excessive slime drips that change the collision silhouette
```

### Skeleton Additional Negative Prompt

```text
no gore, no blood, no full skeleton body, no skull-only object, no chaotic bone pile, no hollow lattice that loses cube readability, no pips lost inside cracks
```

### Bat Additional Negative Prompt

```text
no full bat creature, no giant wings, no feathers, no wings extending far beyond cube bounds, no pips hidden by dark purple texture, no vampire character face
```

### Goblin Additional Negative Prompt

```text
no goblin body, no hand holding dice, no casino table scene, no labels or written graffiti, no pips so crooked they become unreadable, no excessive grime covering pip count
```

### Dracula Additional Negative Prompt

```text
no character portrait, no castle scene, no cape shape replacing cube silhouette, no blood splatter, no overly busy filigree, no gemstones obscuring pip count, no unreadable dark-on-dark pips
```

## Recommended File Names And Folder Structure

Do not create these folders or files in this task. This is a proposed import structure only.

```text
Assets/Dices/EnemyStyles/
  Slime/
    slime_d6_concept.png
    slime_d6_faces_ref.png
    slime_d6_albedo.png
    slime_d6_review_sheet.png
    slime_d6_atlas.png
    slime_d6.mat
    slime_d6.prefab
  Skeleton/
    skeleton_d6_concept.png
    skeleton_d6_faces_ref.png
    skeleton_d6_albedo.png
    skeleton_d6_review_sheet.png
    skeleton_d6_atlas.png
    skeleton_d6.mat
    skeleton_d6.prefab
  Bat/
    bat_d6_concept.png
    bat_d6_faces_ref.png
    bat_d6_albedo.png
    bat_d6_review_sheet.png
    bat_d6_atlas.png
    bat_d6.mat
    bat_d6.prefab
  Goblin/
    goblin_d6_concept.png
    goblin_d6_faces_ref.png
    goblin_d6_albedo.png
    goblin_d6_review_sheet.png
    goblin_d6_atlas.png
    goblin_d6.mat
    goblin_d6.prefab
  Dracula/
    dracula_d6_concept.png
    dracula_d6_faces_ref.png
    dracula_d6_albedo.png
    dracula_d6_review_sheet.png
    dracula_d6_atlas.png
    dracula_d6.mat
    dracula_d6.prefab
```

Recommended review-work structure before Unity import:

```text
SpritePipelineWork/enemy_dice_slime/
SpritePipelineWork/enemy_dice_skeleton/
SpritePipelineWork/enemy_dice_bat/
SpritePipelineWork/enemy_dice_goblin/
SpritePipelineWork/enemy_dice_dracula/
```

Recommended profile IDs for a later implementation task:

```text
slime_d6
skeleton_d6
bat_d6
goblin_d6
dracula_d6
```

Stage 1 boss mapping note:

- Art folder/profile can use `Dracula`/`dracula_d6`.
- Runtime boss data currently uses `어둠의 지배자`.
- A later implementation should set `BossDef.enemyDiceProfileId = "dracula_d6"` without renaming the boss unless the task explicitly authorizes display-name/data changes.

## Review Criteria

### Readability

- Pips are readable at enemy dice overlay size: currently fixed around `298.7 x 168 px`.
- Pips remain readable when the dice is rotated and lit.
- The top face is never fully hidden in the concept render.
- Six-face reference clearly distinguishes counts 1 through 6 without labels.

### Theme

- Slime reads as green translucent jelly, not green plastic.
- Skeleton reads as assembled bone, not stone or plain ivory plastic.
- Bat reads as dark purple lightweight bat/leather/wing membrane, not a full bat creature.
- Goblin reads as rigged gambler dice with grime/brass/loaded-dice cues.
- Dracula reads as premium gothic vampire boss dice with black, burgundy, gold, and red gemstone contrast.

### Cube Silhouette

- The dice remains a cube first.
- Rounded or beveled edges are allowed, but face planes must remain clear.
- Decorations should be low-profile and must not visually contradict the physics cube/collider.

### Existing Game Tone

- Stylized 3D game asset look should fit DiceBattleScene's simple physics dice presentation.
- Boss dice can be more ornate, but not so ornate that the dice reads as jewelry instead of a gameplay object.
- Normal enemy dice should share consistent scale, camera angle, and face layout.

### Unity Import Feasibility

- The concept should be reducible to either a 3x2 face atlas or a material/albedo texture for a cube mesh.
- Avoid designs requiring complex geometry, rigging, transparency sorting tricks, or non-cube colliders for v0.1.
- If transparency is used for slime, review whether URP material settings and render texture sorting are acceptable before runtime use.

### Battle Camera Identification

- Enemy type should be identifiable in the small overlay or center popup.
- Dracula should read as boss-quality even at reduced size.
- High-contrast pip treatment is more important than decorative detail.

### Physics Simulation Believability

- The dice should look like a rollable object.
- Slime can imply bounce through material and rounded edges, not by losing cube form.
- Bat edge motifs should be shallow enough that the object still seems physically rollable.
- Goblin loaded-dice detail should be subtle, not enough to suggest an actual unbalanced collider unless a later gameplay task wants that behavior.

## Future Unity Application Plans

### Plan A: Review-Only Asset Generation

Scope:

- Generate concepts and review sheets outside `Assets/`.
- Build contact sheets in `SpritePipelineWork/enemy_dice_*`.
- No Unity import and no runtime connection.

Files likely touched:

- `SpritePipelineWork/enemy_dice_*/**`
- Optional review result doc under `docs/`

Risk: Low.

Validation:

- Confirm no files were added under `Assets/`.
- Confirm no `.meta`, `.unity`, `.prefab`, `.asset`, `ProjectSettings`, or `Packages` changes.
- Visual review against criteria above.

Rollback:

- Delete or archive only the generated `SpritePipelineWork/enemy_dice_*` review folders if rejected.

### Plan B: Import Static Candidate Textures Only

Scope:

- Import approved PNGs under `Assets/Dices/EnemyStyles/<Enemy>/`.
- Keep them as references/material source images only.
- Do not wire them to dice runtime.

Files likely touched:

- `Assets/Dices/EnemyStyles/<Enemy>/*.png`
- Unity-generated `.meta` files
- Optional `docs/assets.md` update

Risk: Medium, because new Unity assets and `.meta` files are introduced.

Validation:

- Use isolated Unity validation worktree only.
- Check import settings for texture type, alpha, compression, filter mode, and readability where needed.
- Run `git status --porcelain` in validation worktree after Unity import.
- Treat unexpected tracked file changes as failure/risk.

Rollback:

- Remove only the newly imported `Assets/Dices/EnemyStyles/<Enemy>/` assets and associated Unity-generated `.meta` files if the import task explicitly allowed cleanup.
- Do not touch existing dice prefabs or generated d6 assets.

### Plan C: Minimal Runtime Mapping With Existing Profile IDs

Scope:

- Keep enemy dice as the same existing d6 prefab.
- Add enemy profile IDs to `Stage1Forest`.
- Add profile entries to the builder/catalog for per-enemy scale, spacing, camera, bounce, or overlay tuning only.
- No material or prefab swap yet.

Files likely touched:

- `Assets/Scripts/Stages/Stage1Forest.cs`
- `Assets/Editor/DiceBattleSceneBuilder.cs`
- Existing tests under `Assets/Editor/Tests/Battle/`
- Generated scene only through scene builder, in a separate validation task

Risk: High, because `DiceBattleSceneBuilder` and generated scene wiring are involved.

Validation:

- Add/extend pure EditMode tests for profile resolution first.
- Rebuild DiceBattleScene only in an isolated validation worktree if scene regeneration is part of the task.
- Run Unity EditMode tests from the validation worktree.
- Verify `git status --porcelain` in validation worktree.
- Manual viewport check: rank 1-3 overlay, rank 4-5 center popup, boss profile fallback.

Rollback:

- Revert only the mapping/profile-code changes from that task.
- Regenerate scene from previous builder state if a scene artifact was generated in validation.

### Plan D: Material Swap On Existing Dice Mesh

Scope:

- Use a shared cube dice mesh and swap renderer materials by resolved enemy profile.
- Add material/texture fields to `EnemyDiceProfile`.
- Add runtime-safe material assignment in `EnemyDiceRoller.ApplyDieProfile`.
- Assign `MobDef.enemyDiceProfileId` and `BossDef.enemyDiceProfileId`.

Files likely touched:

- `Assets/Scripts/Battle/EnemyDiceProfile.cs`
- `Assets/Scripts/Battle/EnemyDiceRoller.cs`
- `Assets/Scripts/Stages/Stage1Forest.cs`
- `Assets/Editor/DiceBattleSceneBuilder.cs`
- `Assets/Editor/Tests/Battle/EnemyDiceProfileCatalogTests.cs`
- Imported material/texture assets under `Assets/Dices/EnemyStyles/**`

Risk: High. This touches serialized profile data, builder wiring, imported assets, and runtime rendering.

Validation:

- Unit tests for profile fallback and material field resolution where possible.
- Isolated Unity EditMode tests.
- Scene build validation in a separate worktree.
- Manual visual check in DiceBattleScene for Slime, Skeleton, Bat, Goblin, and Stage 1 boss.
- Confirm no runtime `Shader.Find()` dependency is introduced.

Rollback:

- Clear profile IDs in stage data or return them to `default_d6`.
- Revert `EnemyDiceProfile`/`EnemyDiceRoller` material assignment changes.
- Leave imported assets untouched unless the rollback task explicitly authorizes asset cleanup.

### Plan E: Enemy-Specific Dice Prefabs

Scope:

- Generate one prefab per enemy dice style.
- Make `EnemyDiceRoller` or the builder instantiate/swap dice prefabs based on the resolved profile.
- Potentially create separate mesh/material/atlas assets per enemy.

Files likely touched:

- `Assets/Editor/DicePrefabBuilder.cs` or a new enemy dice prefab builder
- `Assets/Editor/DiceBattleSceneBuilder.cs`
- `Assets/Scripts/Battle/EnemyDiceProfile.cs`
- `Assets/Scripts/Battle/EnemyDiceRoller.cs`
- `Assets/Scripts/Stages/Stage1Forest.cs`
- `Assets/Dices/EnemyStyles/**`
- Existing tests and new tests

Risk: High. This is the broadest option and affects prefab generation, scene generation, serialized fields, physics/collider behavior, and asset GUIDs.

Validation:

- Separate implementation plan before editing.
- Preserve existing `Dice_d6_mine.prefab` and generated assets.
- Isolated Unity EditMode tests and scene build validation.
- Manual QA for rolling stability, top-face detection, enemy overlay framing, and material visibility.
- Check collider/mesh consistency for every enemy style.

Rollback:

- Return all stage profile IDs to `default_d6`.
- Revert runtime/builder prefab selection changes.
- Do not delete existing generated dice assets.
- Remove newly generated enemy prefabs/assets only if the rollback task explicitly authorizes it and `.meta` handling is planned.

## Suggested Generation Workflow

1. Generate review images outside `Assets/`, using `SpritePipelineWork/enemy_dice_<enemy>/source/`.
2. For each enemy, generate at least:
   - 3 single concept variants
   - 2 six-face references
   - 2 albedo/texture references
   - 1 review sheet from the strongest concept
3. Build a manual review packet with:
   - enemy identity comparison sheet
   - pip readability sheet at full size, 50%, 25%, and approximate overlay size
   - light/dark background contrast sheet
   - selected candidate notes
4. Approve one direction per enemy.
5. Only then start a separate Unity import task.

## Prompt Summary Table

| Enemy | Visual identity | Material feel | Pip style | Risk |
|---|---|---|---|---|
| Slime | Green slime cube | Translucent jelly, bubbles, glossy surface tension | Pale green or white embedded pips with dark rim | Medium for final import because transparency may need URP/material validation |
| Skeleton | Bone-built cube | Aged ivory bone, cracks, dark seams | Black recessed holes or skull-like pips | Low to medium visually; material is mostly opaque |
| Bat | Purple bat-themed cube | Dark violet leather, wing membrane texture, hollow-light feel | Pale lavender or moonlit pips | Medium because wing motifs can break cube silhouette if overdone |
| Goblin | Rigged gambler dice | Dirty green-brown, brass, grime, loaded detail | Slightly crooked but readable dark/light pips | Low to medium; detail density must be controlled |
| Dracula | Stage 1 boss luxury dice | Black obsidian, burgundy enamel, gold trim, red gemstone | Gold or red gemstone inlay pips with high contrast | Medium; ornament density and dark-on-dark contrast are the main risks |

## Unity Application Summary Table

| Plan | Scope | Files likely touched | Risk | Validation | Rollback |
|---|---|---|---|---|---|
| A | Review-only generation outside Unity assets | `SpritePipelineWork/enemy_dice_*/**`, optional docs | Low | No `Assets/` changes, visual review packet | Delete/archive review work folders |
| B | Import static candidate textures only | `Assets/Dices/EnemyStyles/**/*.png`, `.meta`, optional docs | Medium | Isolated Unity import validation, import settings check | Remove only newly imported assets if authorized |
| C | Profile IDs with tuning only | `Stage1Forest.cs`, `DiceBattleSceneBuilder.cs`, tests | High | EditMode tests, isolated scene build, overlay check | Revert mapping/profile tuning |
| D | Material swap on existing mesh | `EnemyDiceProfile.cs`, `EnemyDiceRoller.cs`, `Stage1Forest.cs`, `DiceBattleSceneBuilder.cs`, tests, materials | High | Tests, isolated Unity validation, manual visual QA | Return to `default_d6`, revert material assignment |
| E | Enemy-specific prefabs | Dice prefab/builder code, scene builder, runtime roller, stage data, tests, assets | High | Full isolated Unity validation plus manual roll/top-face QA | Revert prefab selection, return profiles to default |

## Manual Review Checklist

- [ ] Every concept is a d6 dice, not a character prop or scene object.
- [ ] Top/front/side faces are visible enough to judge pips.
- [ ] The six-face reference has readable counts 1 through 6 without labels.
- [ ] Pips remain readable when the image is scaled to approximate enemy overlay size.
- [ ] Cube silhouette is intact.
- [ ] Decorations do not imply a collider shape that differs strongly from a cube.
- [ ] Slime transparency does not hide pips.
- [ ] Skeleton cracks do not compete with pip holes.
- [ ] Bat wing motifs stay low-profile.
- [ ] Goblin crooked pips remain countable.
- [ ] Dracula ornament and dark material do not bury red/gold pips.
- [ ] Candidate files are still outside `Assets/` during review.
- [ ] No `.meta`, `.prefab`, `.asset`, `.unity`, `ProjectSettings`, or `Packages` files are changed during prompt/review work.

## Next Work Options

1. Asset generation only: generate candidates outside `Assets/` using the prompts above.
2. Review packet: build comparison sheets and select one approved direction per enemy.
3. Static texture import: import approved PNGs into `Assets/Dices/EnemyStyles/**` without runtime wiring.
4. Runtime design task: specify the material/profile mapping API and tests before touching builders.
5. Runtime implementation task: add enemy profile IDs and material/prefab application in the smallest separate step.
