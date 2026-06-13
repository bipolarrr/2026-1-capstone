# Assets And Dependencies

This file owns asset and dependency expectations for the current prototype.

## Dependencies

- Universal Render Pipeline (URP)
- TextMesh Pro with `Assets/TextMesh Pro/Fonts/Mona12.asset`
- Dice asset pack with `Assets/Dices/Prefabs/Dice_d6.prefab`
- Unity Input System

## Generated Assets

Scene builders are expected to create these if missing:

- `Assets/Textures/DiceRenderTexture.renderTexture` (`960x540`)
- `Assets/Textures/VaultRenderTexture.renderTexture` (`960x180`)
- `Assets/Physics/DiceBouncy.asset`
- `Assets/Physics/WallBouncy.asset`
- `Assets/Materials/DiceOutline.mat`

## Referenced Sprites

Mob animation folders follow `Assets/Mobs/Sprites/<Mob>/<State>/*.png`.
`Idle` is required for animated mobs. `Attack` and `Hit` fall back to `Idle` when empty.
`Dead` is optional and may be either a PNG frame folder or a `.anim` clip path in the mob definition; when present, the enemy death animation plays once and holds the last frame.
Normal enemies and bosses use the same animation-set structure.

- `Assets/Player/Sprites/Idle/*.png`
- `Assets/Player/Sprites/LowHp/*.png`
- `Assets/Player/Sprites/Jump/*.png`
- `Assets/Player/Sprites/JumpBelow/*.png`
- `Assets/Player/Sprites/Defense/*.png`
- `Assets/Player/Sprites/SmallHit/*.png`
- `Assets/Player/Sprites/StrongHit/*.png`
- `Assets/Player/Sprites/Debuff/*.png`
- `Assets/Player/Sprites/Die/*.png`
- `Assets/Player/Sprites/DiceRoll/*.png`
- `Assets/Player/Sprites/Attack/*.png`
- `Assets/Player/Sprites/Weapon/Player_Weapon.png`
- `Assets/Backgrounds/Fight_Background_0_Forest.png`
- `Assets/Backgrounds/Fight_Background_1_Cave.png`
- `Assets/UI/MainScreen_Logo.png`
- `Assets/UI/UI_Background.png`
- `Assets/UI/UI_Heart.png`
- `Assets/Story/Story_CutScene_*.png`
- `Assets/Mobs/DiceGambler_sample.png`
- `Assets/Mobs/Sprites/Slime/Idle/*.png`
- `Assets/Mobs/Sprites/Slime/Dead/*.png`
- `Assets/Mobs/Sprites/Goblin/Idle/*.png`
- `Assets/Mobs/Sprites/Goblin/Dead/*.png`
- `Assets/Mobs/Sprites/Bat/Idle/*.png`
- `Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent_clean/*.png`
- `Assets/Mobs/Sprites/Bat/Dead/*.png`
- `Assets/Mobs/Sprites/Skeleton/Idle/*.png`
- `Assets/Mobs/Sprites/Skeleton/Attack/*.png`
- `Assets/Mobs/Sprites/Skeleton/Hit/*.png`
- `Assets/Mobs/Sprites/Skeleton/Dead/*.png`
- `Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png`
- `Assets/Mobs/Sprites/Elemental/WaterCannon/WaterCannon.png`
- `Assets/Mobs/DiceGambler_explanation_sample.png`

## Stage 2 Runtime Sprite QA Notes

- Golem runtime references use `Assets/Mobs/Sprites/Golem/InGame/{Idle,Attack,Hit,Dead}`. Automated tests cover path existence and direct PNG counts only; scale, feet anchor, attack offset, hit alignment, and death hold quality remain manual QA.
- Water Elemental keeps `Assets/Mobs/Water_Elemental.png` as the static body while no runtime-ready idle PNG folder exists under `Assets/Mobs/Sprites/Elemental`.
- Water Elemental attack uses only `Assets/Mobs/Sprites/Elemental/WaterCannon/WaterCannon.png`; do not create or wire a separate body attack animation for it.
- Water Elemental hit/dead use fallback when an actual sprite sequence is absent: hit uses the static body with flash feedback, and dead uses the no-sequence death fallback. Visual readability of those fallbacks remains manual QA.
