using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 1 — 어둠의 숲.
/// 기존 컨텐츠(슬라임·고블린·박쥐·해골 + 드라큘라 보스 + Fight_Background_0_Forest)를 그대로 보존.
/// 수정 시 밸런스 변경에 주의.
/// </summary>
public static class Stage1Forest
{
	public const string Id = "forest_1";

	public static StageData Build()
	{
		return new StageData
		{
			id          = Id,
			displayName = "어둠의 숲",
			themeColor  = new Color(0.30f, 0.50f, 0.35f),
			backgroundSpritePath = "Assets/Mobs/Fight_Background_0_Forest.png",

			mobPool = new List<MobDef>
			{
				new MobDef
				{
					name       = "슬라임",
					hpMin      = 30, hpMax = 40, rank = 1,
					themeColor = new Color(0.60f, 0.90f, 0.60f),
					spritePath = "Assets/Mobs/Slime_sample.png",
					idleSpriteFolderPath = "Assets/Mobs/Sprites/Slime/Idle",
					attackSpriteFolderPath = "Assets/Mobs/Sprites/Slime/Attack",
					hitSpriteFolderPath = "Assets/Mobs/Sprites/Slime/Hit",
					bodyYMin   = 0.00f, bodyYMax = 0.40f,
				},
				new MobDef
				{
					name       = "고블린",
					hpMin      = 18, hpMax = 25, rank = 2,
					themeColor = new Color(0.95f, 0.75f, 0.50f),
					spritePath = "Assets/Mobs/Goblin.png",
					idleSpriteFolderPath = "Assets/Mobs/Sprites/Goblin/Idle",
					attackSpriteFolderPath = "Assets/Mobs/Sprites/Goblin/Attack",
					hitSpriteFolderPath = "Assets/Mobs/Sprites/Goblin/Hit",
					bodyYMin   = 0.00f, bodyYMax = 0.75f,
				},
				new MobDef
				{
					name       = "박쥐",
					hpMin      = 10, hpMax = 15, rank = 3,
					themeColor = new Color(0.75f, 0.60f, 0.90f),
					spritePath = "Assets/Mobs/Bat_sample.png",
					idleSpriteFolderPath = "Assets/Mobs/Sprites/Bat/Idle",
					attackSpriteFolderPath = "Assets/Mobs/Sprites/Bat/Attack",
					hitSpriteFolderPath = "Assets/Mobs/Sprites/Bat/Hit",
					bodyYMin   = 0.30f, bodyYMax = 0.80f,
				},
				new MobDef
				{
					name       = "해골",
					hpMin      = 22, hpMax = 30, rank = 2,
					themeColor = new Color(0.85f, 0.85f, 0.85f),
					spritePath = "Assets/Mobs/Skeleton_sample.png",
					bodyYMin   = 0.00f, bodyYMax = 0.75f,
				},
			},

			rounds = new List<StageRoundType>
			{
				StageRoundType.NormalCombat,
				StageRoundType.ItemBox,
				StageRoundType.BossCombat,
			},

			boss = new BossDef
			{
				name       = "어둠의 지배자",
				hp         = GameSessionManager.BossHp,
				rank       = 5,
				themeColor = new Color(0.95f, 0.45f, 0.45f),
				spritePath = "Assets/Mobs/Boss_Dracula_example.png",
			},

			normalEnemyCountMin = 2,
			normalEnemyCountMax = 4,
		};
	}
}
