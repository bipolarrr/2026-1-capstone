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

- `Assets/Mobs/DiceGambler_sample.png`
- `Assets/Mobs/Slime_sample.png`
- `Assets/Mobs/Goblin_sample.png`
- `Assets/Mobs/Bat_sample.png`
- `Assets/Mobs/Skeleton_sample.png`
- `Assets/Mobs/DiceGambler_explanation_sample.png`
