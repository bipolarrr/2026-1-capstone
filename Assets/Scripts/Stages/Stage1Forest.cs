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
			mapTitle    = "숲",
			themeColor  = new Color(0.30f, 0.50f, 0.35f),
			backgroundSpritePath = "Assets/Backgrounds/Fight_Background_0_Forest.png",

			mobPool = new List<MobDef>
			{
				new MobDef
				{
					name       = "슬라임",
					hpMin      = 30, hpMax = 40, rank = 1,
					themeColor = new Color(0.60f, 0.90f, 0.60f),
					spritePath = "Assets/Mobs/Sprites/Slime/Idle/0.png",
					idleSpriteFolderPath = "Assets/Mobs/Sprites/Slime/Idle",
					attackSpriteFolderPath = "Assets/Mobs/Sprites/Slime/Attack",
					hitSpriteFolderPath = "Assets/Mobs/Sprites/Slime/Hit",
					deathSpriteFolderPath = "Assets/Mobs/Sprites/Slime/Dead",
					deathFrameRateMultiplier = 2f,
					attackRangeType = EnemyAttackRangeType.Unique,
					enemyDiceProfileId = EnemyDiceProfile.SlimeId,
					bodyYMin   = 0.00f, bodyYMax = 0.40f,
				},
				new MobDef
				{
					name       = "고블린",
					hpMin      = 18, hpMax = 25, rank = 2,
					themeColor = new Color(0.95f, 0.75f, 0.50f),
					spritePath = "Assets/Mobs/Sprites/Goblin/Idle/0.png",
					idleSpriteFolderPath = "Assets/Mobs/Sprites/Goblin/Idle",
					attackSpriteFolderPath = "Assets/Mobs/Sprites/Goblin/Attack",
					attackSpriteFrameCount = 50,
					hitSpriteFolderPath = "Assets/Mobs/Sprites/Goblin/Hit",
					hitSpriteFrameCount = 29,
					deathSpriteFolderPath = "Assets/Mobs/Sprites/Goblin/Dead",
					attackRangeType = EnemyAttackRangeType.Melee,
					attackFrameRate = 30f,
					attackTimingProfile = new EnemyAttackTimingProfile
					{
						enemyId = EnemyAttackTiming.GoblinEnemyId,
						impactNormalizedTime = EnemyAttackTiming.GoblinMeleeImpactNormalizedTime,
						playerSmallHitReactionNormalizedTime = EnemyAttackTiming.PlayerSmallHitReactionNormalizedTime,
						playerStrongHitReactionNormalizedTime = EnemyAttackTiming.PlayerStrongHitReactionNormalizedTime,
					},
					enemyDiceProfileId = EnemyDiceProfile.GoblinId,
					bodyYMin   = 0.00f, bodyYMax = 0.82f,
					bodyXMin   = -0.08f, bodyXMax = 1.08f,
				},
				new MobDef
				{
					name       = "박쥐",
					hpMin      = 10, hpMax = 15, rank = 3,
					themeColor = new Color(0.75f, 0.60f, 0.90f),
					spritePath = "Assets/Mobs/Sprites/Bat/Idle/0.png",
					idleSpriteFolderPath = "Assets/Mobs/Sprites/Bat/Idle",
					attackSpriteFolderPath = "Assets/Mobs/Sprites/Bat/Attack/Bat_Attack_transparent_clean",
					hitSpriteFolderPath = "Assets/Mobs/Sprites/Bat/Hit",
					hitSpriteFrameCount = 53,
					deathSpriteFolderPath = "Assets/Mobs/Sprites/Bat/Dead",
					deathFallToGround = true,
					attackRangeType = EnemyAttackRangeType.Melee,
					uniqueAttackProfileId = "bat",
					attackFrameRate = 30f,
					attackApproachDuration = 0.6f,
					attackRetreatDuration = 0.5f,
					enemyDiceProfileId = EnemyDiceProfile.BatId,
					bodyYMin   = 0.30f, bodyYMax = 0.80f,
				},
				new MobDef
				{
					name       = "해골",
					hpMin      = 22, hpMax = 30, rank = 2,
					themeColor = new Color(0.85f, 0.85f, 0.85f),
					spritePath = "Assets/Mobs/Sprites/Skeleton/Idle/0.png",
					idleSpriteFolderPath = "Assets/Mobs/Sprites/Skeleton/Idle",
					deathSpriteFolderPath = "Assets/Mobs/Sprites/Skeleton/Dead",
					projectileSpritePath = "Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png",
					attackRangeType = EnemyAttackRangeType.Ranged,
					enemyDiceProfileId = EnemyDiceProfile.SkeletonId,
					bodyYMin   = 0.00f, bodyYMax = 0.75f,
				},
			},

			rounds = new List<StageRoundType>
			{
				StageRoundType.NormalCombat,
				StageRoundType.NormalCombat,
				StageRoundType.ItemBox,
				StageRoundType.NormalCombat,
				StageRoundType.ItemBox,
				StageRoundType.NormalCombat,
				StageRoundType.ItemBox,
				StageRoundType.NormalCombat,
				StageRoundType.BossCombat,
			},

			boss = new BossDef
			{
				name       = "어둠의 지배자",
				hp         = GameSessionManager.BossHp,
				rank       = 5,
				themeColor = new Color(0.95f, 0.45f, 0.45f),
				spritePath = "Assets/Mobs/Boss_Dracula_example.png",
				enemyDiceProfileId = EnemyDiceProfile.DraculaId,
			},

			normalEnemyCountMin = 2,
			normalEnemyCountMax = 4,
		};
	}
}
