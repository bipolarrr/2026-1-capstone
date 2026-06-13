using System.Collections.Generic;
using UnityEngine;

public enum AnimationDebugEntityKind
{
	Mob,
	Boss,
}

public sealed class AnimationDebugCatalogEntry
{
	public AnimationDebugCatalogEntry(StageData stage, MobDef mob, int mobIndex)
	{
		Stage = stage;
		MobDefinition = mob;
		Kind = AnimationDebugEntityKind.Mob;
		MobIndex = mobIndex;
	}

	public AnimationDebugCatalogEntry(StageData stage, BossDef boss)
	{
		Stage = stage;
		BossDefinition = boss;
		Kind = AnimationDebugEntityKind.Boss;
		MobIndex = -1;
	}

	public StageData Stage { get; }
	public MobDef MobDefinition { get; }
	public BossDef BossDefinition { get; }
	public AnimationDebugEntityKind Kind { get; }
	public int MobIndex { get; }
	public bool IsBoss => Kind == AnimationDebugEntityKind.Boss;
	public string StageId => Stage != null ? Stage.id : "";
	public string StageDisplayName => Stage != null ? Stage.displayName : "";
	public string EntityName => IsBoss ? BossDefinition?.name : MobDefinition?.name;
	public int Rank => IsBoss ? BossDefinition?.rank ?? 0 : MobDefinition?.rank ?? 0;
	public int HpMin => IsBoss ? BossDefinition?.hp ?? 0 : MobDefinition?.hpMin ?? 0;
	public int HpMax => IsBoss ? BossDefinition?.hp ?? 0 : MobDefinition?.hpMax ?? 0;
	public Color ThemeColor => IsBoss ? BossDefinition?.themeColor ?? Color.gray : MobDefinition?.themeColor ?? Color.gray;
	public string SpritePath => IsBoss ? BossDefinition?.spritePath : MobDefinition?.spritePath;
	public string IdleSpriteFolderPath => IsBoss ? BossDefinition?.idleSpriteFolderPath : MobDefinition?.idleSpriteFolderPath;
	public string AttackSpriteFolderPath => IsBoss ? BossDefinition?.attackSpriteFolderPath : MobDefinition?.attackSpriteFolderPath;
	public string HitSpriteFolderPath => IsBoss ? BossDefinition?.hitSpriteFolderPath : MobDefinition?.hitSpriteFolderPath;
	public string DeathSpriteFolderPath => IsBoss ? BossDefinition?.deathSpriteFolderPath : MobDefinition?.deathSpriteFolderPath;
	public string DeathAnimationClipPath => IsBoss ? BossDefinition?.deathAnimationClipPath : MobDefinition?.deathAnimationClipPath;
	public string ProjectileSpritePath => IsBoss ? null : MobDefinition?.projectileSpritePath;
	public string AttackVfxSpritePath => IsBoss ? null : MobDefinition?.attackVfxSpritePath;

	public string Key
	{
		get
		{
			string kind = IsBoss ? "boss" : "mob";
			string index = IsBoss ? "0" : MobIndex.ToString();
			return $"{StageId}:{kind}:{index}:{EntityName}";
		}
	}

	public EnemyInfo CreateEnemyInfo(Sprite sprite)
	{
		int hp = Mathf.Max(1, HpMax);
		return new EnemyInfo(EntityName, hp, Mathf.Max(1, Rank), ThemeColor, sprite);
	}
}

public static class AnimationDebugCatalog
{
	public static List<AnimationDebugCatalogEntry> BuildFromRegisteredStages()
	{
		var entries = new List<AnimationDebugCatalogEntry>();
		foreach (var stage in StageRegistry.AllStages)
		{
			if (stage == null)
				continue;

			if (stage.mobPool != null)
			{
				for (int i = 0; i < stage.mobPool.Count; i++)
				{
					var mob = stage.mobPool[i];
					if (mob != null)
						entries.Add(new AnimationDebugCatalogEntry(stage, mob, i));
				}
			}

			if (stage.boss != null)
				entries.Add(new AnimationDebugCatalogEntry(stage, stage.boss));
		}
		return entries;
	}
}
