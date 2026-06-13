using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleTests
{
	public class SceneBuilderUtilityTests
	{
		class DummyTarget
		{
			int value;
			public int Value => value;
		}

		[Test]
		public void SetField_SetsPrivateFieldWithoutFailure()
		{
			SceneBuilderUtility.BeginSceneBuildValidation("test");
			var target = new DummyTarget();

			SceneBuilderUtility.SetField(target, "value", 7);

			Assert.AreEqual(7, target.Value);
			Assert.AreEqual(0, SceneBuilderUtility.FieldBindingFailureCount);
		}

		[Test]
		public void SetField_RecordsMissingField()
		{
			SceneBuilderUtility.BeginSceneBuildValidation("test");

			SceneBuilderUtility.SetField(new DummyTarget(), "missing", 7);

			Assert.AreEqual(1, SceneBuilderUtility.FieldBindingFailureCount);
		}

		[Test]
		public void BuildSceneShell_CreatesCameraCanvasAndEventSystem()
		{
			var shell = SceneBuilderUtility.BuildSceneShell("TestCamera", Color.black,
				includeAudioListener: true);

			try
			{
				Assert.NotNull(shell.camera);
				Assert.NotNull(shell.canvas);
				Assert.NotNull(shell.canvasRoot);
				Assert.NotNull(shell.eventSystem);
				Assert.NotNull(shell.camera.GetComponent<AudioListener>());
				Assert.NotNull(shell.canvas.GetComponent<CanvasScaler>());
				Assert.NotNull(shell.canvas.GetComponent<GraphicRaycaster>());
				Assert.NotNull(shell.eventSystem.GetComponent<EventSystem>());
			}
			finally
			{
				if (shell.camera != null)
					Object.DestroyImmediate(shell.camera.gameObject);
				if (shell.canvas != null)
					Object.DestroyImmediate(shell.canvas.gameObject);
				if (shell.eventSystem != null)
					Object.DestroyImmediate(shell.eventSystem);
			}
		}

		[Test]
		public void ExploreMapCircleGraphic_CreatesRequiredCanvasRenderer()
		{
			var go = new GameObject("ExploreMapCircleGraphicTest");

			try
			{
				var graphic = go.AddComponent<ExploreMapCircleGraphic>();

				Assert.NotNull(graphic.GetComponent<RectTransform>());
				Assert.NotNull(graphic.GetComponent<CanvasRenderer>());
			}
			finally
			{
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void ExploreMapEdgeGraphic_CreatesRequiredCanvasRenderer()
		{
			var go = new GameObject("ExploreMapEdgeGraphicTest");

			try
			{
				var graphic = go.AddComponent<ExploreMapEdgeGraphic>();

				Assert.NotNull(graphic.GetComponent<RectTransform>());
				Assert.NotNull(graphic.GetComponent<CanvasRenderer>());
			}
			finally
			{
				Object.DestroyImmediate(go);
			}
		}

		[Test]
		public void BuildExploreEnemySlots_ReturnsFourBindableSlots()
		{
			var rootGo = new GameObject("ExploreEnemySlotsTest", typeof(RectTransform));

			try
			{
				var handles = SceneBuilderUtility.BuildExploreEnemySlots(
					rootGo.transform, 0.12f, Color.black, Color.red, null);

				Assert.AreEqual(4, handles.panels.Length);
				Assert.AreEqual(4, handles.bodies.Length);
				Assert.AreEqual(4, handles.idleProjectiles.Length);
				Assert.AreEqual(4, handles.names.Length);
				Assert.AreEqual(4, handles.hpFills.Length);
				Assert.AreEqual(4, handles.hpTexts.Length);
				Assert.NotNull(handles.root);
				Assert.NotNull(handles.panels[0]);
				Assert.NotNull(handles.bodies[0]);
				Assert.NotNull(handles.idleProjectiles[0]);
				Assert.NotNull(handles.names[0]);
				Assert.NotNull(handles.hpFills[0]);
				Assert.NotNull(handles.hpTexts[0]);
			}
			finally
			{
				Object.DestroyImmediate(rootGo);
			}
		}

		[Test]
		public void BuildBattleRootBase_BindsControllerWithoutFieldFailures()
		{
			SceneBuilderUtility.BeginSceneBuildValidation("test");
			var canvasGo = new GameObject("Canvas", typeof(RectTransform));
			var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
			var playerGo = new GameObject("Player", typeof(RectTransform), typeof(Image), typeof(PlayerBodyAnimator));
			var heartGo = new GameObject("Heart", typeof(RectTransform), typeof(HeartDisplay));
			var logGo = new GameObject("Log", typeof(RectTransform), typeof(BattleLog));
			var focusGo = new GameObject("Focus", typeof(RectTransform), typeof(BattleBottomFocusController));
			var damageGo = new GameObject("DamageSpawn", typeof(RectTransform));

			backgroundGo.transform.SetParent(canvasGo.transform, false);
			playerGo.transform.SetParent(canvasGo.transform, false);
			heartGo.transform.SetParent(canvasGo.transform, false);
			logGo.transform.SetParent(canvasGo.transform, false);
			focusGo.transform.SetParent(canvasGo.transform, false);
			damageGo.transform.SetParent(canvasGo.transform, false);

			GameObject[] enemyPanels = null;
			try
			{
				enemyPanels = new GameObject[4];
				var enemyBodies = new Image[4];
				var enemyNames = new TMPro.TMP_Text[4];
				var enemyHpFills = new Image[4];
				var enemyHpTexts = new TMPro.TMP_Text[4];
				var targetMarkers = new Image[4];
				var deadOverlays = new TMPro.TMP_Text[4];
				var enemyAnimators = new EnemySpriteAnimator[4];
				var idleProjectiles = new Image[4];
				var buttons = new Button[4];
				for (int i = 0; i < 4; i++)
				{
					var enemyGo = new GameObject($"Enemy{i}", typeof(RectTransform), typeof(Image), typeof(Button));
					enemyGo.transform.SetParent(canvasGo.transform, false);
					enemyPanels[i] = enemyGo;
					enemyBodies[i] = enemyGo.GetComponent<Image>();
					enemyHpFills[i] = enemyGo.GetComponent<Image>();
					targetMarkers[i] = enemyGo.GetComponent<Image>();
					idleProjectiles[i] = enemyGo.GetComponent<Image>();
					buttons[i] = enemyGo.GetComponent<Button>();
				}

				var handles = SceneBuilderUtility.BuildBattleRootBase<BattleSceneController>(
					new SceneBuilderUtility.StageBackgroundHandles
					{
						image = backgroundGo.GetComponent<Image>(),
					},
					new StageSpriteBundle[0],
					new SceneBuilderUtility.BattlePlayerRigHandles
					{
						bodyImage = playerGo.GetComponent<Image>(),
						bodyAnimator = playerGo.GetComponent<PlayerBodyAnimator>(),
					},
					new SceneBuilderUtility.EnemySlotStripHandles
					{
						panels = enemyPanels,
						bodies = enemyBodies,
						names = enemyNames,
						hpFills = enemyHpFills,
						hpTexts = enemyHpTexts,
						targetMarkers = targetMarkers,
						deadOverlays = deadOverlays,
						animators = enemyAnimators,
						idleProjectiles = idleProjectiles,
						buttons = buttons,
					},
					0.44f,
					heartGo.GetComponent<HeartDisplay>(),
					logGo.GetComponent<BattleLog>(),
					focusGo.GetComponent<BattleBottomFocusController>(),
					damageGo.GetComponent<RectTransform>());

				Assert.NotNull(handles.root);
				Assert.NotNull(handles.controller);
				Assert.NotNull(handles.vfx);
				Assert.NotNull(handles.animations);
				Assert.AreEqual(0, SceneBuilderUtility.FieldBindingFailureCount);
				Object.DestroyImmediate(handles.root);
			}
			finally
			{
				Object.DestroyImmediate(canvasGo);
			}
		}

		[Test]
		public void ResolveEnemyBodyDisplayScale_FlipsOnlyBossSlot()
		{
			Assert.AreEqual(new Vector3(-2f, 3f, 1f),
				BattleControllerBase.ResolveEnemyBodyDisplayScale(true, 0, new Vector3(2f, 3f, 1f)));
			Assert.AreEqual(new Vector3(2f, 3f, 1f),
				BattleControllerBase.ResolveEnemyBodyDisplayScale(false, 0, new Vector3(-2f, 3f, 1f)));
			Assert.AreEqual(new Vector3(2f, 3f, 1f),
				BattleControllerBase.ResolveEnemyBodyDisplayScale(true, 1, new Vector3(-2f, 3f, 1f)));
		}

		[Test]
		public void Stage2Cave_WaterElementalOnlyHasWaterCannonAttackVfx()
		{
			var stage = Stage2Cave.Build();
			const string waterCannonPath = "Assets/Mobs/Sprites/Elemental/WaterCannon/WaterCannon.png";
			const string staticBodyPath = "Assets/Mobs/Water_Elemental.png";
			int waterElementalCount = 0;

			foreach (var def in stage.mobPool)
			{
				if (def.name == "물의 정령")
				{
					waterElementalCount++;
					Assert.AreEqual(EnemyAttackRangeType.Unique, def.attackRangeType);
					Assert.AreEqual(staticBodyPath, def.spritePath);
					Assert.IsTrue(string.IsNullOrEmpty(def.idleSpriteFolderPath), "Idle은 runtime-ready frame folder 없음: static body fallback.");
					Assert.IsTrue(string.IsNullOrEmpty(def.attackSpriteFolderPath), "Attack은 body animation 없이 WaterCannon VFX만 사용.");
					Assert.AreEqual(waterCannonPath, def.attackVfxSpritePath);
					Assert.IsTrue(string.IsNullOrEmpty(def.projectileSpritePath));
					Assert.IsTrue(string.IsNullOrEmpty(def.hitSpriteFolderPath), "Hit은 static body + flash fallback.");
					Assert.IsTrue(string.IsNullOrEmpty(def.deathSpriteFolderPath), "Dead는 실제 sequence 없음: no-sequence death fallback.");
					Assert.IsTrue(string.IsNullOrEmpty(def.deathAnimationClipPath));
				}
				else
				{
					Assert.IsTrue(string.IsNullOrEmpty(def.attackVfxSpritePath), def.name);
				}
			}

			Assert.AreEqual(1, waterElementalCount);
		}

		[Test]
		public void Stage2Cave_GolemRuntimeFramesUseInGameFolders()
		{
			var stage = Stage2Cave.Build();
			var golem = stage.FindMob("골렘");

			Assert.NotNull(golem);
			Assert.AreEqual("Assets/Mobs/Sprites/Golem/InGame/Idle/0000.png", golem.spritePath);
			Assert.AreEqual("Assets/Mobs/Sprites/Golem/InGame/Idle", golem.idleSpriteFolderPath);
			Assert.AreEqual("Assets/Mobs/Sprites/Golem/InGame/Attack", golem.attackSpriteFolderPath);
			Assert.AreEqual("Assets/Mobs/Sprites/Golem/InGame/Hit", golem.hitSpriteFolderPath);
			Assert.AreEqual("Assets/Mobs/Sprites/Golem/InGame/Dead", golem.deathSpriteFolderPath);
			AssertFolderHasAtLeastDirectPngs(golem.idleSpriteFolderPath, 1);
			AssertFolderHasAtLeastDirectPngs(golem.attackSpriteFolderPath, golem.attackSpriteFrameCount);
			AssertFolderHasAtLeastDirectPngs(golem.hitSpriteFolderPath, golem.hitSpriteFrameCount);
			AssertFolderHasAtLeastDirectPngs(golem.deathSpriteFolderPath, 1);
		}

		[Test]
		public void Stage2Cave_MobRoster_IsLockedToCaveMobs()
		{
			var stage = Stage2Cave.Build();

			AssertStage2CaveLockedRoster(stage);

			StringAssert.Contains("Bat", stage.FindMob("박쥐").spritePath);
			StringAssert.Contains("Goblin", stage.FindMob("고블린").spritePath);
			StringAssert.Contains("Water_Elemental", stage.FindMob("물의 정령").spritePath);
			StringAssert.Contains("Golem", stage.FindMob("골렘").spritePath);
			Assert.AreEqual(29, stage.FindMob("고블린").hitSpriteFrameCount);
		}

		[Test]
		public void Stage2Cave_NormalCombatRounds_UseLockedMobRoster()
		{
			var stage = Stage2Cave.Build();
			var normalCombatRoundIndexes = new List<int>();

			for (int i = 0; i < stage.rounds.Count; i++)
			{
				if (stage.rounds[i] == StageRoundType.NormalCombat)
					normalCombatRoundIndexes.Add(i);
			}

			CollectionAssert.AreEqual(new[] { 0, 1, 3, 5, 7 }, normalCombatRoundIndexes);
			Assert.AreEqual(stage.mobPool.Count, stage.normalEnemyCountMax);
			Assert.LessOrEqual(stage.normalEnemyCountMin, stage.normalEnemyCountMax);
			AssertStage2CaveLockedRoster(stage);
		}

		[Test]
		public void Stage2Cave_BossRound_IsFinalePlaceholderWithoutNewBossMobDefinition()
		{
			var stage = Stage2Cave.Build();

			Assert.NotNull(stage.boss);
			Assert.AreEqual(StageRoundType.BossCombat, stage.rounds[stage.rounds.Count - 1]);
			Assert.AreEqual("동굴의 수호자", stage.boss.name);
			Assert.AreEqual(GameSessionManager.BossHp, stage.boss.hp);
			Assert.AreEqual(5, stage.boss.rank);
			Assert.IsNull(stage.FindMob(stage.boss.name));
			Assert.IsTrue(string.IsNullOrEmpty(stage.boss.spritePath));
			Assert.IsTrue(string.IsNullOrEmpty(stage.boss.idleSpriteFolderPath));
			Assert.IsTrue(string.IsNullOrEmpty(stage.boss.attackSpriteFolderPath));
			Assert.IsTrue(string.IsNullOrEmpty(stage.boss.hitSpriteFolderPath));
			Assert.IsTrue(string.IsNullOrEmpty(stage.boss.deathSpriteFolderPath));
			Assert.IsTrue(string.IsNullOrEmpty(stage.boss.enemyDiceProfileId));
		}

		[Test]
		public void Stage1Forest_BossCombatStillUsesDraculaDefinition()
		{
			var stage = Stage1Forest.Build();

			Assert.NotNull(stage.boss);
			Assert.AreEqual(StageRoundType.BossCombat, stage.rounds[stage.rounds.Count - 1]);
			Assert.AreEqual("어둠의 지배자", stage.boss.name);
			Assert.AreEqual(GameSessionManager.BossHp, stage.boss.hp);
			Assert.AreEqual(5, stage.boss.rank);
			Assert.AreEqual("Assets/Mobs/Boss_Dracula_example.png", stage.boss.spritePath);
			Assert.AreEqual(EnemyDiceProfile.DraculaId, stage.boss.enemyDiceProfileId);
		}

		[Test]
		public void StageRuntimeSpriteReferences_PointToExistingAssetsOrIntentionalFallbacks()
		{
			var failures = new List<string>();

			foreach (var stage in StageRegistry.AllStages)
			{
				string stageLabel = stage.id;
				CheckFileReference(failures, stageLabel, "backgroundSpritePath", stage.backgroundSpritePath,
					"stage themeColor background fallback");

				if (stage.mobPool != null)
				{
					foreach (var mob in stage.mobPool)
						CheckMobSpriteReferences(failures, stageLabel, mob);
				}

				if (stage.boss != null)
					CheckBossSpriteReferences(failures, stageLabel, stage.boss);
			}

			if (failures.Count > 0)
				Assert.Fail(string.Join("\n", failures));
		}

		static void AssertStage2CaveLockedRoster(StageData stage)
		{
			Assert.NotNull(stage.mobPool);
			Assert.AreEqual(4, stage.mobPool.Count);

			var names = stage.mobPool.ConvertAll(mob => mob.name);
			CollectionAssert.AreEquivalent(
				new[] { "박쥐", "고블린", "물의 정령", "골렘" },
				names);
		}

		static void CheckMobSpriteReferences(List<string> failures, string stageLabel, MobDef mob)
		{
			string mobLabel = $"{stageLabel}/mob/{mob.name}";
			CheckFileReference(failures, mobLabel, "spritePath", mob.spritePath,
				!string.IsNullOrEmpty(mob.idleSpriteFolderPath) ? "first idle frame supplies body sprite" : null);
			CheckFileReference(failures, mobLabel, "projectileSpritePath", mob.projectileSpritePath,
				ResolveMobProjectileFallbackReason(mob));
			CheckFileReference(failures, mobLabel, "attackVfxSpritePath", mob.attackVfxSpritePath,
				"mob does not use a separate attack VFX");
			CheckFileReference(failures, mobLabel, "deathAnimationClipPath", mob.deathAnimationClipPath,
				ResolveMobDeathClipFallbackReason(mob));
			CheckFolderReference(failures, mobLabel, "idleSpriteFolderPath", mob.idleSpriteFolderPath, 0,
				!string.IsNullOrEmpty(mob.spritePath) ? "static body sprite supplies idle fallback" : null);
			CheckFolderReference(failures, mobLabel, "attackSpriteFolderPath", mob.attackSpriteFolderPath, mob.attackSpriteFrameCount,
				ResolveMobAttackFallbackReason(mob));
			CheckFolderReference(failures, mobLabel, "hitSpriteFolderPath", mob.hitSpriteFolderPath, mob.hitSpriteFrameCount,
				HasMobBodyFallback(mob) ? "static body or idle sequence supplies hit fallback with flash" : null);
			CheckFolderReference(failures, mobLabel, "deathSpriteFolderPath", mob.deathSpriteFolderPath, 0,
				ResolveMobDeathFolderFallbackReason(mob));
		}

		static void CheckBossSpriteReferences(List<string> failures, string stageLabel, BossDef boss)
		{
			string bossLabel = $"{stageLabel}/boss/{boss.name}";
			string staticBossFallback = !string.IsNullOrEmpty(boss.spritePath)
				? "static boss sprite fallback"
				: "boss themeColor placeholder fallback";

			CheckFileReference(failures, bossLabel, "spritePath", boss.spritePath, staticBossFallback);
			CheckFileReference(failures, bossLabel, "deathAnimationClipPath", boss.deathAnimationClipPath,
				"boss no-sequence death fallback");
			CheckFolderReference(failures, bossLabel, "idleSpriteFolderPath", boss.idleSpriteFolderPath, 0, staticBossFallback);
			CheckFolderReference(failures, bossLabel, "attackSpriteFolderPath", boss.attackSpriteFolderPath, 0, staticBossFallback);
			CheckFolderReference(failures, bossLabel, "hitSpriteFolderPath", boss.hitSpriteFolderPath, 0,
				"static boss body supplies hit fallback with flash");
			CheckFolderReference(failures, bossLabel, "deathSpriteFolderPath", boss.deathSpriteFolderPath, 0,
				"boss no-sequence death fallback");
		}

		static string ResolveMobProjectileFallbackReason(MobDef mob)
		{
			if (mob == null)
				return null;
			if (!string.IsNullOrEmpty(mob.attackVfxSpritePath))
				return "attack VFX supplies the separate attack visual";
			if (EnemyAttackPositionResolver.ResolveRangeType(mob) != EnemyAttackRangeType.Ranged)
				return "mob does not use an idle projectile";
			return null;
		}

		static string ResolveMobAttackFallbackReason(MobDef mob)
		{
			if (mob == null)
				return null;
			if (!string.IsNullOrEmpty(mob.projectileSpritePath))
				return "projectile attack uses projectile sprite instead of body attack sequence";
			if (!string.IsNullOrEmpty(mob.attackVfxSpritePath))
				return "attack VFX supplies the attack visual";
			return null;
		}

		static string ResolveMobDeathClipFallbackReason(MobDef mob)
		{
			if (mob == null)
				return null;
			if (!string.IsNullOrEmpty(mob.deathSpriteFolderPath))
				return "death frame folder supplies death sequence";
			return HasMobBodyFallback(mob) ? "no-sequence death fallback" : null;
		}

		static string ResolveMobDeathFolderFallbackReason(MobDef mob)
		{
			if (mob == null)
				return null;
			if (!string.IsNullOrEmpty(mob.deathAnimationClipPath))
				return "death animation clip supplies death sequence";
			return HasMobBodyFallback(mob) ? "no-sequence death fallback" : null;
		}

		static bool HasMobBodyFallback(MobDef mob)
		{
			return mob != null
				&& (!string.IsNullOrEmpty(mob.spritePath) || !string.IsNullOrEmpty(mob.idleSpriteFolderPath));
		}

		static void CheckFileReference(List<string> failures, string owner, string fieldName, string assetPath,
			string emptyFallbackReason)
		{
			if (string.IsNullOrEmpty(assetPath))
			{
				if (string.IsNullOrEmpty(emptyFallbackReason))
					failures.Add($"{owner}: {fieldName} is empty without an intentional fallback");
				return;
			}

			if (!File.Exists(assetPath))
				failures.Add($"{owner}: {fieldName} missing file '{assetPath}'");
		}

		static void CheckFolderReference(List<string> failures, string owner, string fieldName, string folderPath,
			int expectedFrameCount, string emptyFallbackReason)
		{
			if (string.IsNullOrEmpty(folderPath))
			{
				if (string.IsNullOrEmpty(emptyFallbackReason))
					failures.Add($"{owner}: {fieldName} is empty without an intentional fallback");
				return;
			}

			if (!Directory.Exists(folderPath))
			{
				failures.Add($"{owner}: {fieldName} missing folder '{folderPath}'");
				return;
			}

			int pngCount = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly).Length;
			if (pngCount == 0)
			{
				failures.Add($"{owner}: {fieldName} has no direct PNG frames in '{folderPath}'");
				return;
			}

			if (expectedFrameCount > 0 && pngCount < expectedFrameCount)
				failures.Add($"{owner}: {fieldName} expected {expectedFrameCount} frames but found {pngCount} in '{folderPath}'");
		}

		static void AssertFolderHasAtLeastDirectPngs(string folderPath, int expectedFrameCount)
		{
			Assert.That(Directory.Exists(folderPath), Is.True, folderPath);
			int pngCount = Directory.GetFiles(folderPath, "*.png", SearchOption.TopDirectoryOnly).Length;
			Assert.That(pngCount, Is.GreaterThanOrEqualTo(expectedFrameCount), folderPath);
		}

		[Test]
		public void IncrementalBuild_NoManifest_MarksAllTargetsStale()
		{
			WithIncrementalFixture((root, utilityPath, manifestPath, targets) =>
			{
				var changes = SceneBuilderIncrementalBuild.FindChangedScenes(
					targets, manifestPath, "test-unity", utilityPath);

				Assert.AreEqual(targets.Length, changes.Count);
				Assert.AreEqual("manifest missing", changes[0].Reason);
			});
		}

		[Test]
		public void IncrementalBuild_DefaultTargets_IncludeAnimationDebugScene()
		{
			var targets = SceneBuilderIncrementalBuild.DefaultTargets
				.Where(t => t.SceneName == "AnimationDebugScene")
				.ToArray();

			Assert.AreEqual(1, targets.Length);
			Assert.AreEqual("Assets/Editor/AnimationDebugSceneBuilder.cs", targets[0].BuilderPath);
			Assert.AreEqual(AnimationDebugSceneBuilder.ScenePath, targets[0].OutputScenePath);
			Assert.NotNull(targets[0].BuildAction);
		}

		[Test]
		public void IncrementalBuild_BuilderContentChange_MarksOnlyThatSceneStale()
		{
			WithIncrementalFixture((root, utilityPath, manifestPath, targets) =>
			{
				SaveMatchingManifest(manifestPath, utilityPath, targets, "test-unity");
				File.AppendAllText(targets[0].BuilderPath, "\nchanged");

				var changes = SceneBuilderIncrementalBuild.FindChangedScenes(
					targets, manifestPath, "test-unity", utilityPath);

				Assert.AreEqual(1, changes.Count);
				Assert.AreEqual(targets[0].SceneName, changes[0].Target.SceneName);
				Assert.AreEqual("builder input changed", changes[0].Reason);
			});
		}

		[Test]
		public void IncrementalBuild_SharedUtilityChange_MarksAllScenesStale()
		{
			WithIncrementalFixture((root, utilityPath, manifestPath, targets) =>
			{
				SaveMatchingManifest(manifestPath, utilityPath, targets, "test-unity");
				File.AppendAllText(utilityPath, "\nchanged");

				var changes = SceneBuilderIncrementalBuild.FindChangedScenes(
					targets, manifestPath, "test-unity", utilityPath);

				Assert.AreEqual(targets.Length, changes.Count);
				Assert.AreEqual("builder input changed", changes[0].Reason);
			});
		}

		[Test]
		public void IncrementalBuild_MissingOutputScene_MarksSceneStale()
		{
			WithIncrementalFixture((root, utilityPath, manifestPath, targets) =>
			{
				SaveMatchingManifest(manifestPath, utilityPath, targets, "test-unity");
				File.Delete(targets[0].OutputScenePath);

				var changes = SceneBuilderIncrementalBuild.FindChangedScenes(
					targets, manifestPath, "test-unity", utilityPath);

				Assert.AreEqual(1, changes.Count);
				Assert.AreEqual("output scene missing", changes[0].Reason);
			});
		}

		[Test]
		public void IncrementalBuild_OutputSceneDrift_MarksSceneStale()
		{
			WithIncrementalFixture((root, utilityPath, manifestPath, targets) =>
			{
				SaveMatchingManifest(manifestPath, utilityPath, targets, "test-unity");
				File.AppendAllText(targets[0].OutputScenePath, "\ndrift");

				var changes = SceneBuilderIncrementalBuild.FindChangedScenes(
					targets, manifestPath, "test-unity", utilityPath);

				Assert.AreEqual(1, changes.Count);
				Assert.AreEqual("output scene drifted", changes[0].Reason);
			});
		}

		[Test]
		public void IncrementalBuild_SetFieldFailure_DoesNotUpdateManifest()
		{
			WithIncrementalFixture((root, utilityPath, manifestPath, targets) =>
			{
				var target = new SceneBuilderIncrementalBuild.SceneBuildTarget(
					targets[0].SceneName,
					targets[0].BuilderPath,
					targets[0].OutputScenePath,
					() =>
					{
						SceneBuilderUtility.BeginSceneBuildValidation("incremental-test");
						SceneBuilderUtility.SetField(new DummyTarget(), "missing", 1);
						File.WriteAllText(targets[0].OutputScenePath, "new scene");
						return true;
					});

				bool result = SceneBuilderIncrementalBuild.BuildChangedScenes(
					new[] { target }, manifestPath, "test-unity", utilityPath,
					promptBeforeBuild: false, showResultDialog: false);

				Assert.IsFalse(result);
				Assert.IsFalse(File.Exists(manifestPath));
			});
		}

		[Test]
		public void IncrementalBuild_BuildFailure_DoesNotUpdateManifest()
		{
			WithIncrementalFixture((root, utilityPath, manifestPath, targets) =>
			{
				var target = new SceneBuilderIncrementalBuild.SceneBuildTarget(
					targets[0].SceneName,
					targets[0].BuilderPath,
					targets[0].OutputScenePath,
					() =>
					{
						SceneBuilderUtility.BeginSceneBuildValidation("incremental-test");
						File.WriteAllText(targets[0].OutputScenePath, "failed scene");
						return false;
					});

				bool result = SceneBuilderIncrementalBuild.BuildChangedScenes(
					new[] { target }, manifestPath, "test-unity", utilityPath,
					promptBeforeBuild: false, showResultDialog: false);

				Assert.IsFalse(result);
				Assert.IsFalse(File.Exists(manifestPath));
			});
		}

		[Test]
		public void IncrementalBuild_SuccessfulBuild_UpdatesManifest()
		{
			WithIncrementalFixture((root, utilityPath, manifestPath, targets) =>
			{
				var target = new SceneBuilderIncrementalBuild.SceneBuildTarget(
					targets[0].SceneName,
					targets[0].BuilderPath,
					targets[0].OutputScenePath,
					() =>
					{
						SceneBuilderUtility.BeginSceneBuildValidation("incremental-test");
						File.WriteAllText(targets[0].OutputScenePath, "rebuilt scene");
						return true;
					});

				bool result = SceneBuilderIncrementalBuild.BuildChangedScenes(
					new[] { target }, manifestPath, "test-unity", utilityPath,
					promptBeforeBuild: false, showResultDialog: false);

				Assert.IsTrue(result);
				Assert.NotNull(SceneBuilderIncrementalBuild.LoadManifest(manifestPath));

				var changes = SceneBuilderIncrementalBuild.FindChangedScenes(
					new[] { target }, manifestPath, "test-unity", utilityPath);
				Assert.AreEqual(0, changes.Count);
			});
		}

		static void WithIncrementalFixture(
			System.Action<string, string, string, SceneBuilderIncrementalBuild.SceneBuildTarget[]> test)
		{
			string root = Path.Combine(Path.GetTempPath(), "CapstoneSceneBuilderIncrementalTests", System.Guid.NewGuid().ToString("N"));
			Directory.CreateDirectory(root);
			try
			{
				string utilityPath = Path.Combine(root, "SceneBuilderUtility.cs");
				string builderA = Path.Combine(root, "BuilderA.cs");
				string builderB = Path.Combine(root, "BuilderB.cs");
				string outputA = Path.Combine(root, "SceneA.unity");
				string outputB = Path.Combine(root, "SceneB.unity");
				string manifestPath = Path.Combine(root, "Library", "SceneBuilderIncremental", "manifest.json");

				File.WriteAllText(utilityPath, "utility");
				File.WriteAllText(builderA, "builder-a");
				File.WriteAllText(builderB, "builder-b");
				File.WriteAllText(outputA, "scene-a");
				File.WriteAllText(outputB, "scene-b");

				var targets = new[]
				{
					new SceneBuilderIncrementalBuild.SceneBuildTarget("SceneA", builderA, outputA),
					new SceneBuilderIncrementalBuild.SceneBuildTarget("SceneB", builderB, outputB),
				};

				test(root, utilityPath, manifestPath, targets);
			}
			finally
			{
				if (Directory.Exists(root))
					Directory.Delete(root, true);
			}
		}

		static void SaveMatchingManifest(
			string manifestPath,
			string utilityPath,
			SceneBuilderIncrementalBuild.SceneBuildTarget[] targets,
			string unityVersion)
		{
			var manifest = new SceneBuilderIncrementalBuild.Manifest
			{
				version = SceneBuilderIncrementalBuild.ManifestVersion,
				unityVersion = unityVersion,
			};

			for (int i = 0; i < targets.Length; i++)
			{
				manifest.scenes.Add(new SceneBuilderIncrementalBuild.ManifestSceneEntry
				{
					sceneName = targets[i].SceneName,
					inputHash = SceneBuilderIncrementalBuild.ComputeInputHash(targets[i], unityVersion, utilityPath),
					outputHash = SceneBuilderIncrementalBuild.ComputeFileHash(targets[i].OutputScenePath),
					outputScenePath = targets[i].OutputScenePath,
					builtAtUtc = System.DateTime.UtcNow.ToString("O"),
				});
			}

			SceneBuilderIncrementalBuild.SaveManifest(manifestPath, manifest);
		}
	}
}
