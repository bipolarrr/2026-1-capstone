using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace BattleTests
{
	public class AnimationDebugSceneTests
	{
		[Test]
		public void Catalog_IncludesAllRegisteredStageMobsAndBosses()
		{
			var entries = AnimationDebugCatalog.BuildFromRegisteredStages();
			int expectedCount = 0;

			foreach (var stage in StageRegistry.AllStages)
			{
				expectedCount += stage.mobPool != null ? stage.mobPool.Count : 0;
				expectedCount += stage.boss != null ? 1 : 0;

				if (stage.mobPool != null)
				{
					for (int i = 0; i < stage.mobPool.Count; i++)
					{
						int mobIndex = i;
						Assert.That(entries.Count(e =>
								!e.IsBoss
								&& e.StageId == stage.id
								&& e.MobIndex == mobIndex
								&& e.MobDefinition == stage.mobPool[mobIndex]),
							Is.EqualTo(1),
							$"Missing mob entry {stage.id}[{mobIndex}]");
					}
				}

				if (stage.boss != null)
				{
					Assert.That(entries.Count(e =>
							e.IsBoss
							&& e.StageId == stage.id
							&& e.BossDefinition == stage.boss),
						Is.EqualTo(1),
						$"Missing boss entry {stage.id}");
				}
			}

			Assert.That(entries, Has.Count.EqualTo(expectedCount));
		}

		[Test]
		public void Catalog_KeepsSameMobNamesInDifferentStagesAsSeparateEntries()
		{
			var entries = AnimationDebugCatalog.BuildFromRegisteredStages();
			var duplicateNameGroup = entries
				.Where(e => !e.IsBoss)
				.GroupBy(e => e.EntityName)
				.FirstOrDefault(g => g.Select(e => e.StageId).Distinct().Count() > 1);

			Assert.That(duplicateNameGroup, Is.Not.Null, "Expected at least one mob name shared by multiple stages.");
			Assert.That(duplicateNameGroup.Select(e => e.Key).Distinct().Count(),
				Is.EqualTo(duplicateNameGroup.Count()));
			Assert.That(duplicateNameGroup.Select(e => e.StageId).Distinct().Count(),
				Is.GreaterThan(1));
		}

		[Test]
		public void Catalog_EntriesCarryStageSpecificTuningForDuplicateMobNames()
		{
			var entries = AnimationDebugCatalog.BuildFromRegisteredStages();
			var forestGoblin = entries.Single(e =>
				!e.IsBoss && e.StageId == Stage1Forest.Id && e.EntityName == "고블린");
			var caveGoblin = entries.Single(e =>
				!e.IsBoss && e.StageId == Stage2Cave.Id && e.EntityName == "고블린");

			Assert.That(forestGoblin.MobDefinition, Is.Not.SameAs(caveGoblin.MobDefinition));
			Assert.That(forestGoblin.MobDefinition.bodyYMin, Is.Not.EqualTo(caveGoblin.MobDefinition.bodyYMin));
			Assert.That(forestGoblin.MobDefinition.enemyDiceProfileId, Is.EqualTo(caveGoblin.MobDefinition.enemyDiceProfileId));
		}

		[Test]
		public void Stage2Golem_AttackFramesMatchInGameFolder()
		{
			var stage = Stage2Cave.Build();
			var golem = stage.FindMob("골렘");
			Assert.That(golem, Is.Not.Null);

			const string attackFolder = "Assets/Mobs/Sprites/Golem/InGame/Attack";
			Assert.That(golem.attackSpriteFolderPath, Is.EqualTo(attackFolder));
			Assert.That(Directory.Exists(attackFolder), Is.True);

			int directPngCount = Directory.GetFiles(attackFolder, "*.png", SearchOption.TopDirectoryOnly).Length;
			Assert.That(directPngCount, Is.GreaterThan(0));
			Assert.That(golem.attackSpriteFrameCount, Is.EqualTo(directPngCount));
		}

		[Test]
		public void Stage2Golem_AttackVisualOverride_IsDefined()
		{
			var stage = Stage2Cave.Build();
			var golem = stage.FindMob("골렘");
			Assert.That(golem, Is.Not.Null);

			Assert.That(golem.attackVisualScaleMultiplier, Is.EqualTo(1f).Within(0.0001f));
			Assert.That(golem.attackVisualOffset.x, Is.EqualTo(0f).Within(0.0001f));
			Assert.That(golem.attackVisualOffset.y, Is.EqualTo(-23f).Within(0.0001f));
			Assert.That(golem.attackUseFullTextureFrames, Is.True);
		}

		[Test]
		public void Stage2Golem_ImportSettingsMatchPpuAndPivotButAttackUsesTightMultipleRect()
		{
			const string idlePath = "Assets/Mobs/Sprites/Golem/InGame/Idle/0000.png";
			const string attackPath = "Assets/Mobs/Sprites/Golem/InGame/Attack/0000.png";
			var idleImporter = AssetImporter.GetAtPath(idlePath) as TextureImporter;
			var attackImporter = AssetImporter.GetAtPath(attackPath) as TextureImporter;
			Assert.That(idleImporter, Is.Not.Null);
			Assert.That(attackImporter, Is.Not.Null);

			Assert.That(idleImporter.textureType, Is.EqualTo(TextureImporterType.Sprite));
			Assert.That(attackImporter.textureType, Is.EqualTo(TextureImporterType.Sprite));
			Assert.That(attackImporter.spritePixelsPerUnit, Is.EqualTo(idleImporter.spritePixelsPerUnit).Within(0.001f));
			Assert.That(Vector2.Distance(attackImporter.spritePivot, idleImporter.spritePivot), Is.LessThan(0.0001f));

			var idleSettings = new TextureImporterSettings();
			var attackSettings = new TextureImporterSettings();
			idleImporter.ReadTextureSettings(idleSettings);
			attackImporter.ReadTextureSettings(attackSettings);
			Assert.That(idleImporter.spriteImportMode, Is.EqualTo(SpriteImportMode.Single));
			Assert.That(attackImporter.spriteImportMode, Is.EqualTo(SpriteImportMode.Multiple));
			Assert.That(idleSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.FullRect));
			Assert.That(attackSettings.spriteMeshType, Is.EqualTo(SpriteMeshType.Tight));
		}

		[Test]
		public void SharedAnimationSet_LoadsGolemAttackFrames()
		{
			var stage = Stage2Cave.Build();
			var golem = stage.FindMob("골렘");
			Assert.That(golem, Is.Not.Null);
			var method = typeof(SceneBuilderUtility).GetMethod("BuildEnemyAnimationSet",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null);

			var set = (EnemySpriteAnimationSet)method.Invoke(null, new object[]
			{
				golem.idleSpriteFolderPath,
				golem.attackSpriteFolderPath,
				golem.hitSpriteFolderPath,
				golem.deathSpriteFolderPath,
				golem.deathAnimationClipPath,
				golem.deathFrameRateMultiplier,
				golem.attackFrameRate,
				golem.attackSpriteFrameCount,
				golem.hitSpriteFrameCount,
			});

			Assert.That(set.attackSprites, Is.Not.Null);
			Assert.That(set.attackSprites, Has.Length.EqualTo(golem.attackSpriteFrameCount));
			Assert.That(set.attackSprites.Count(sprite => sprite != null), Is.EqualTo(golem.attackSpriteFrameCount));
			Assert.That(set.attackPingPong, Is.False);
		}

		[Test]
		public void SharedAnimationSet_CopiesGolemFullTextureAttackOverride()
		{
			var stage = Stage2Cave.Build();
			var golem = stage.FindMob("골렘");
			Assert.That(golem, Is.Not.Null);
			var method = typeof(SceneBuilderUtility).GetMethod("BuildMobAnimationSet",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null);

			var set = (EnemySpriteAnimationSet)method.Invoke(null, new object[] { golem });

			Assert.That(set.attackVisualScaleMultiplier, Is.EqualTo(1f).Within(0.0001f));
			Assert.That(set.attackVisualOffset.x, Is.EqualTo(0f).Within(0.0001f));
			Assert.That(set.attackVisualOffset.y, Is.EqualTo(-23f).Within(0.0001f));
			Assert.That(set.attackUseFullTextureFrames, Is.True);
		}

		[Test]
		public void BossAttackPreview_UsesDraculaLaserOnlyForStage1Boss()
		{
			var entries = AnimationDebugCatalog.BuildFromRegisteredStages();
			var stage1Boss = entries.Single(e => e.IsBoss && e.StageId == Stage1Forest.Id);
			var stage2Boss = entries.Single(e => e.IsBoss && e.StageId == Stage2Cave.Id);
			var stage1Mob = entries.First(e => !e.IsBoss && e.StageId == Stage1Forest.Id);

			Assert.That(AnimationDebugSceneController.ShouldPlayDraculaLaserPreview(
				stage1Boss,
				stage1Boss.CreateEnemyInfo(null)), Is.True);
			Assert.That(AnimationDebugSceneController.ShouldPlayDraculaLaserPreview(
				stage2Boss,
				stage2Boss.CreateEnemyInfo(null)), Is.False);
			Assert.That(AnimationDebugSceneController.ShouldPlayDraculaLaserPreview(
				stage1Mob,
				stage1Mob.CreateEnemyInfo(null)), Is.False);
			Assert.That(AnimationDebugSceneController.ShouldPlayDraculaLaserPreview(
				null,
				stage1Boss.CreateEnemyInfo(null)), Is.False);
		}

		[Test]
		public void MissingAttackAndHitSprites_FallBackToIdleLikeBattleSceneBundles()
		{
			var method = typeof(SceneBuilderUtility).GetMethod("BuildEnemyAnimationSet",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null);

			var set = (EnemySpriteAnimationSet)method.Invoke(null, new object[]
			{
				"Assets/__AnimationDebugMissingIdle",
				"Assets/__AnimationDebugMissingAttack",
				"Assets/__AnimationDebugMissingHit",
				"Assets/__AnimationDebugMissingDeath",
				null,
				1f,
				0f,
				0,
				0,
			});

			Assert.That(set.idleSprites, Is.Not.Null);
			Assert.That(set.attackSprites, Is.SameAs(set.idleSprites));
			Assert.That(set.hitSprites, Is.SameAs(set.idleSprites));
			Assert.That(set.attackPingPong, Is.True);
			Assert.That(set.hitPingPong, Is.True);
		}

		[Test]
		public void AnimationDebugScenePath_IsNotInBuildSettings()
		{
			Assert.That(EditorBuildSettings.scenes.Select(s => s.path),
				Does.Not.Contain(AnimationDebugSceneBuilder.ScenePath));
		}
	}
}
