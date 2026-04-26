using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 스테이지 2 — 끝없는 동굴.
/// 몹 풀: 박쥐 · 골렘 · 물의 정령 · 고블린. 배경은 Assets/Mobs/Fight_Background_1_Cave.png.
/// 보스 스프라이트는 아직 없으므로 themeColor로 폴백 생성.
/// 새 스테이지를 만들 때 이 파일을 복사해 시작하면 됨.
/// </summary>
public static class Stage2Cave
{
	public const string Id = "cave_2";

	public static StageData Build()
	{
		return new StageData
		{
			id          = Id,
			displayName = "끝없는 동굴",
			themeColor  = new Color(0.32f, 0.28f, 0.38f),
			backgroundSpritePath = "Assets/Mobs/Fight_Background_1_Cave.png",

			mobPool = new List<MobDef>
			{
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
					name       = "골렘",
					hpMin      = 38, hpMax = 52, rank = 1,
					themeColor = new Color(0.55f, 0.55f, 0.60f),
					spritePath = "Assets/Mobs/Enemy_Golem.png",
					// 슬롯을 넘치도록 크게 — 거대한 거구감 연출.
					bodyYMin   = 0.00f, bodyYMax = 1.30f,
					bodyXMin   = -0.10f, bodyXMax = 1.10f,
				},
				new MobDef
				{
					name       = "물의 정령",
					hpMin      = 22, hpMax = 28, rank = 2,
					themeColor = new Color(0.45f, 0.70f, 0.95f),
					spritePath = "Assets/Mobs/Water_Elemental.png",
					bodyYMin   = 0.05f, bodyYMax = 0.80f,
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
					// 동굴 지형 기준 조금 더 띄움.
					bodyYMin   = 0.20f, bodyYMax = 0.95f,
				},
			},

			rounds = new List<StageRoundType>
			{
				StageRoundType.NormalCombat,
				StageRoundType.NormalCombat,
				StageRoundType.ItemBox,
				StageRoundType.BossCombat,
			},

			boss = new BossDef
			{
				name       = "동굴의 수호자",
				hp         = GameSessionManager.BossHp,
				rank       = 5,
				themeColor = new Color(0.45f, 0.42f, 0.55f),
				spritePath = null, // 에셋 없음 → themeColor 폴백으로 자동 생성
			},

			normalEnemyCountMin = 2,
			normalEnemyCountMax = 4,

			// 동굴 바닥이 기본 GroundY보다 높게 그려지므로 플레이어 앵커를 살짝 끌어올림.
			playerGroundYOffset = 0.08f,
		};
	}
}
