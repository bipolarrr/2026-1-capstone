using NUnit.Framework;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

namespace Battle.Tests
{
	public class EnemyDiceProfileCatalogTests
	{
		[Test]
		public void Resolve_EmptyId_ReturnsDefaultD6()
		{
			var catalog = EnemyDiceProfileCatalog.CreateDefault();

			var profile = catalog.Resolve("");

			Assert.That(profile.id, Is.EqualTo(EnemyDiceProfile.DefaultId));
		}

		[Test]
		public void Resolve_UnknownId_FallsBackToDefaultD6()
		{
			var catalog = EnemyDiceProfileCatalog.CreateDefault();

			var profile = catalog.Resolve("skeleton_yut");

			Assert.That(profile.id, Is.EqualTo(EnemyDiceProfile.DefaultId));
		}

		[TestCase("Bat", EnemyDiceStyleKind.Bat, EnemyDiceProfile.BatId)]
		[TestCase("박쥐", EnemyDiceStyleKind.Bat, EnemyDiceProfile.BatId)]
		[TestCase("Skeleton Archer", EnemyDiceStyleKind.Skeleton, EnemyDiceProfile.SkeletonId)]
		[TestCase("스켈레톤", EnemyDiceStyleKind.Skeleton, EnemyDiceProfile.SkeletonId)]
		[TestCase("해골", EnemyDiceStyleKind.Skeleton, EnemyDiceProfile.SkeletonId)]
		[TestCase("Goblin", EnemyDiceStyleKind.Goblin, EnemyDiceProfile.GoblinId)]
		[TestCase("고블린", EnemyDiceStyleKind.Goblin, EnemyDiceProfile.GoblinId)]
		[TestCase("Slime", EnemyDiceStyleKind.Slime, EnemyDiceProfile.SlimeId)]
		[TestCase("슬라임", EnemyDiceStyleKind.Slime, EnemyDiceProfile.SlimeId)]
		[TestCase("Dracula", EnemyDiceStyleKind.Dracula, EnemyDiceProfile.DraculaId)]
		[TestCase("드라큘라", EnemyDiceStyleKind.Dracula, EnemyDiceProfile.DraculaId)]
		[TestCase("골렘", EnemyDiceStyleKind.Default, EnemyDiceProfile.DefaultId)]
		public void PrototypeResolver_MapsEnemyNamesToDiceStyle(string enemyName, EnemyDiceStyleKind expectedStyle, string expectedProfileId)
		{
			Assert.That(EnemyDiceStyleResolver.ResolveStyle(enemyName), Is.EqualTo(expectedStyle));
			Assert.That(EnemyDiceStyleResolver.ResolveProfileId(enemyName), Is.EqualTo(expectedProfileId));
		}

		[Test]
		public void CreateDefault_IncludesPrototypeEnemyProfiles()
		{
			var catalog = EnemyDiceProfileCatalog.CreateDefault();

			Assert.That(catalog.Profiles, Has.Length.EqualTo(6));
			Assert.That(catalog.Resolve(EnemyDiceProfile.BatId).id, Is.EqualTo(EnemyDiceProfile.BatId));
			Assert.That(catalog.Resolve(EnemyDiceProfile.SkeletonId).id, Is.EqualTo(EnemyDiceProfile.SkeletonId));
			Assert.That(catalog.Resolve(EnemyDiceProfile.GoblinId).id, Is.EqualTo(EnemyDiceProfile.GoblinId));
			Assert.That(catalog.Resolve(EnemyDiceProfile.SlimeId).id, Is.EqualTo(EnemyDiceProfile.SlimeId));
			Assert.That(catalog.Resolve(EnemyDiceProfile.DraculaId).id, Is.EqualTo(EnemyDiceProfile.DraculaId));
		}

		[Test, Category("EnemyDicePhysicsSafety")]
		public void EnemyDiceArenaCapacity_FitsEveryPrototypeProfile()
		{
			var catalog = EnemyDiceProfileCatalog.CreateDefault();

			foreach (var sourceProfile in catalog.Profiles)
			{
				var profile = catalog.Resolve(sourceProfile.id);
				AssertProfileFitsArena(profile, EnemyDiceProfile.MaxEnemyDiceCount);
			}
		}

		[Test, Category("EnemyDicePhysicsSafety")]
		public void EnemyDiceSpawnPositions_StayInsideArenaAndDoNotOverlap()
		{
			var catalog = EnemyDiceProfileCatalog.CreateDefault();
			var center = new Vector3(0f, 0.2625f, 100f);

			foreach (var sourceProfile in catalog.Profiles)
			{
				var profile = catalog.Resolve(sourceProfile.id);
				var positions = profile.ComputeSpawnPositions(center, EnemyDiceProfile.MaxEnemyDiceCount);
				float diceWorldSize = profile.DiceWorldSize;
				float halfDice = diceWorldSize * 0.5f;
				float requiredWallMargin = EnemyDiceProfile.ComputeWallMargin(diceWorldSize);
				float minCenterGap = Mathf.Max(diceWorldSize + profile.positionJitter * 2f,
					diceWorldSize * EnemyDiceProfile.MinimumDiceSpacingMultiplier);

				for (int i = 0; i < positions.Length; i++)
				{
					float xClearance = profile.arenaSize.x * 0.5f
						- Mathf.Abs(positions[i].x - center.x)
						- halfDice
						- profile.positionJitter;
					float zClearance = profile.arenaSize.z * 0.5f
						- Mathf.Abs(positions[i].z - center.z)
						- halfDice
						- profile.positionJitter;

					Assert.That(xClearance + 0.0001f, Is.GreaterThanOrEqualTo(requiredWallMargin),
						$"{profile.id} die {i} x wall clearance");
					Assert.That(zClearance + 0.0001f, Is.GreaterThanOrEqualTo(requiredWallMargin),
						$"{profile.id} die {i} z wall clearance");
				}

				for (int i = 1; i < positions.Length; i++)
				{
					float centerGap = Vector2.Distance(
						new Vector2(positions[i - 1].x, positions[i - 1].z),
						new Vector2(positions[i].x, positions[i].z));
					Assert.That(centerGap + 0.0001f, Is.GreaterThanOrEqualTo(minCenterGap),
						$"{profile.id} spawn center gap {i - 1}->{i}");
				}
			}
		}

		[Test, Category("EnemyDicePhysicsSafety")]
		public void GoblinProfile_DiceScaleIsBelowArenaDerivedSafeLimit()
		{
			var goblin = EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Goblin);
			float safeScaleLimit = EnemyDiceProfile.ComputeMaxSafeDiceScale(
				goblin.arenaSize, EnemyDiceProfile.MaxEnemyDiceCount);

			Assert.That(goblin.diceScale, Is.EqualTo(EnemyDiceProfile.DefaultDiceScale).Within(0.0001f));
			Assert.That(goblin.diceScale + 0.0001f, Is.LessThanOrEqualTo(safeScaleLimit));
		}

		[UnityTest, Category("EnemyDicePhysicsSafety")]
		public IEnumerator EnemyDiceRoller_SettleTimeoutReturnsFallbackResult()
		{
			var dieGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var rollerGo = new GameObject("EnemyDiceRollerTimeoutTest");
			try
			{
				dieGo.AddComponent<Rigidbody>();
				var die = dieGo.AddComponent<Dice>();
				var roller = rollerGo.AddComponent<EnemyDiceRoller>();
				var profile = EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Goblin);
				profile.settleTimeoutSeconds = 0.02f;
				profile.NormalizeSafetySizing(1);

				SetPrivateField(roller, "enemyDice", new[] { die });
				SetPrivateField(roller, "vaultCenter", Vector3.zero);
				SetPrivateField(roller, "profileCatalog", CreateCatalog(profile));

				EnemyDiceResult result = null;
				bool done = false;
				yield return InvokeRollRoutine(roller, 1, profile.id, r =>
				{
					result = r;
					done = true;
				});

				int expectedFallback = EnemyDiceRoller.ComputeDeterministicFallbackFace(profile.id, 0, 1);
				Assert.That(done, Is.True);
				Assert.That(result, Is.Not.Null);
				Assert.That(result.values, Is.EqualTo(new[] { expectedFallback }));
				Assert.That(die.IsSpinning, Is.False);
			}
			finally
			{
				Object.DestroyImmediate(rollerGo);
				Object.DestroyImmediate(dieGo);
			}
		}

		[Test]
		public void PrototypeProfiles_CarryDistinctVisualAndPhysicsSettings()
		{
			var catalog = EnemyDiceProfileCatalog.CreateDefault();
			var bat = catalog.Resolve(EnemyDiceProfile.BatId);
			var skeleton = catalog.Resolve(EnemyDiceProfile.SkeletonId);
			var goblin = catalog.Resolve(EnemyDiceProfile.GoblinId);
			var slime = catalog.Resolve(EnemyDiceProfile.SlimeId);
			var dracula = catalog.Resolve(EnemyDiceProfile.DraculaId);

			Assert.That(bat.visualBaseColor, Is.EqualTo(new Color(0.55f, 0.25f, 0.92f, 1f)));
			Assert.That(bat.rigidbodyMass, Is.LessThan(EnemyDiceProfile.CreateDefault().rigidbodyMass));
			Assert.That(bat.torqueMax, Is.GreaterThan(EnemyDiceProfile.CreateDefault().torqueMax));

			Assert.That(skeleton.visualBaseColor, Is.EqualTo(new Color(0.86f, 0.78f, 0.58f, 1f)));
			Assert.That(skeleton.bounce, Is.LessThan(bat.bounce));

			Assert.That(goblin.visualBaseColor, Is.EqualTo(new Color(0.07f, 0.35f, 0.16f, 1f)));
			Assert.That(goblin.rigidbodyMass, Is.GreaterThan(EnemyDiceProfile.CreateDefault().rigidbodyMass));
			Assert.That(goblin.staticFriction, Is.GreaterThan(slime.staticFriction));

			Assert.That(slime.visualBaseColor, Is.EqualTo(new Color(0.48f, 1.00f, 0.18f, 0.78f)));
			Assert.That(slime.visualDetailColor.grayscale, Is.LessThan(0.04f));
			Assert.That(slime.materialTransparent, Is.True);
			Assert.That(slime.bounce, Is.LessThan(goblin.bounce));
			Assert.That(slime.bounceCombine, Is.EqualTo(PhysicsMaterialCombine.Minimum));
			Assert.That(slime.staticFriction, Is.GreaterThan(0.25f));
			Assert.That(slime.rigidbodyLinearDamping, Is.GreaterThan(EnemyDiceProfile.CreateDefault().rigidbodyLinearDamping));
			Assert.That(slime.rigidbodyAngularDamping, Is.GreaterThan(EnemyDiceProfile.CreateDefault().rigidbodyAngularDamping));

			var defaultProfile = EnemyDiceProfile.CreateDefault();
			Assert.That(dracula.id, Is.EqualTo(EnemyDiceProfile.DraculaId));
			Assert.That(dracula.diceScale, Is.EqualTo(defaultProfile.diceScale));
			Assert.That(dracula.rigidbodyMass, Is.EqualTo(defaultProfile.rigidbodyMass));
			Assert.That(dracula.bounce, Is.EqualTo(defaultProfile.bounce));
		}

		static void AssertProfileFitsArena(EnemyDiceProfile profile, int diceCount)
		{
			float diceWorldSize = profile.DiceWorldSize;
			float requiredSpacing = diceWorldSize * EnemyDiceProfile.MinimumDiceSpacingMultiplier;
			float requiredWidth = EnemyDiceProfile.ComputeRequiredArenaWidth(
				diceCount, diceWorldSize, profile.diceSpacing, profile.positionJitter);
			float requiredDepth = EnemyDiceProfile.ComputeRequiredArenaDepth(
				diceWorldSize, profile.positionJitter);

			Assert.That(profile.diceSpacing + 0.0001f, Is.GreaterThanOrEqualTo(requiredSpacing),
				$"{profile.id} spacing");
			Assert.That(profile.positionJitter + 0.0001f,
				Is.LessThanOrEqualTo(diceWorldSize * EnemyDiceProfile.MaxSpawnJitterMultiplier),
				$"{profile.id} jitter");
			Assert.That(profile.arenaSize.x + 0.0001f, Is.GreaterThanOrEqualTo(requiredWidth),
				$"{profile.id} arena width");
			Assert.That(profile.arenaSize.z + 0.0001f, Is.GreaterThanOrEqualTo(requiredDepth),
				$"{profile.id} arena depth");
			Assert.That(profile.cameraOrthographicSize + 0.0001f,
				Is.GreaterThanOrEqualTo(EnemyDiceProfile.ComputeCameraOrthographicSize(profile.arenaSize)),
				$"{profile.id} camera frame");
			Assert.That(profile.settleTimeoutSeconds + 0.0001f,
				Is.GreaterThanOrEqualTo(EnemyDiceProfile.DefaultSettleTimeoutSeconds),
				$"{profile.id} timeout");
		}

		[Test]
		public void PrototypeProfiles_EnableJellyOnlyForSlime()
		{
			var catalog = EnemyDiceProfileCatalog.CreateDefault();

			Assert.That(catalog.Resolve(EnemyDiceProfile.DefaultId).jellyEnabled, Is.False);
			Assert.That(catalog.Resolve(EnemyDiceProfile.BatId).jellyEnabled, Is.False);
			Assert.That(catalog.Resolve(EnemyDiceProfile.SkeletonId).jellyEnabled, Is.False);
			Assert.That(catalog.Resolve(EnemyDiceProfile.GoblinId).jellyEnabled, Is.False);
			Assert.That(catalog.Resolve(EnemyDiceProfile.DraculaId).jellyEnabled, Is.False);

			var slime = catalog.Resolve(EnemyDiceProfile.SlimeId);
			Assert.That(slime.jellyEnabled, Is.True);
			Assert.That(slime.jellyCompressionMax, Is.GreaterThan(0f));
			Assert.That(slime.jellyShearMax, Is.GreaterThan(0f));
			Assert.That(slime.jellyImpulseScale, Is.GreaterThan(0f));
			Assert.That(slime.jellyStiffness, Is.GreaterThan(0f));
			Assert.That(slime.jellyDamping, Is.GreaterThan(0f));
			Assert.That(slime.jellySettleEpsilon, Is.GreaterThan(0f));
			Assert.That(slime.jellyDentRadius, Is.GreaterThan(0f));
			Assert.That(slime.jellyBulgeScale, Is.GreaterThan(0f));
			Assert.That(slime.jellyWobbleMax, Is.GreaterThan(0f));
			Assert.That(slime.jellyCompressionMax, Is.GreaterThanOrEqualTo(0.28f));
			Assert.That(slime.jellyShearMax, Is.GreaterThanOrEqualTo(0.14f));
		}

		[Test]
		public void PrototypeProfiles_DefaultToRuntimeRecolorWhenAtlasMissing()
		{
			var catalog = EnemyDiceProfileCatalog.CreateDefault();

			Assert.That(catalog.Resolve(EnemyDiceProfile.BatId).faceAtlasTexture, Is.Null);
			Assert.That(catalog.Resolve(EnemyDiceProfile.SkeletonId).faceAtlasTexture, Is.Null);
			Assert.That(catalog.Resolve(EnemyDiceProfile.GoblinId).faceAtlasTexture, Is.Null);
			Assert.That(catalog.Resolve(EnemyDiceProfile.SlimeId).faceAtlasTexture, Is.Null);
			Assert.That(catalog.Resolve(EnemyDiceProfile.DraculaId).faceAtlasTexture, Is.Null);
		}

		[Test]
		public void StageData_AssignsDiceProfilesOnlyWhereAtlasesExist()
		{
			var stage1 = Stage1Forest.Build();
			var stage2 = Stage2Cave.Build();

			Assert.That(stage1.FindMob("슬라임").enemyDiceProfileId, Is.EqualTo(EnemyDiceProfile.SlimeId));
			Assert.That(stage1.FindMob("해골").enemyDiceProfileId, Is.EqualTo(EnemyDiceProfile.SkeletonId));
			Assert.That(stage1.FindMob("박쥐").enemyDiceProfileId, Is.EqualTo(EnemyDiceProfile.BatId));
			Assert.That(stage1.FindMob("고블린").enemyDiceProfileId, Is.EqualTo(EnemyDiceProfile.GoblinId));
			Assert.That(stage1.boss.enemyDiceProfileId, Is.EqualTo(EnemyDiceProfile.DraculaId));

			Assert.That(stage2.FindMob("박쥐").enemyDiceProfileId, Is.EqualTo(EnemyDiceProfile.BatId));
			Assert.That(stage2.FindMob("고블린").enemyDiceProfileId, Is.EqualTo(EnemyDiceProfile.GoblinId));
			Assert.That(stage2.FindMob("골렘").enemyDiceProfileId, Is.Null);
			Assert.That(stage2.FindMob("물의 정령").enemyDiceProfileId, Is.Null);
			Assert.That(stage2.boss.enemyDiceProfileId, Is.Null);
		}

		[Test]
		public void DraculaLaserAttack_RequiresBossBattle()
		{
			var stage1 = Stage1Forest.Build();
			var dracula = new EnemyInfo(stage1.boss.name, stage1.boss.hp, stage1.boss.rank, stage1.boss.themeColor);

			bool shouldPlay = DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
				false,
				stage1.id,
				dracula,
				stage1.boss,
				stage1.boss.enemyDiceProfileId);

			Assert.That(shouldPlay, Is.False);
		}

		[Test]
		public void DraculaLaserAttack_RequiresStage1()
		{
			var stage1 = Stage1Forest.Build();
			var dracula = new EnemyInfo(stage1.boss.name, stage1.boss.hp, stage1.boss.rank, stage1.boss.themeColor);

			bool shouldPlay = DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
				true,
				Stage2Cave.Id,
				dracula,
				stage1.boss,
				stage1.boss.enemyDiceProfileId);

			Assert.That(shouldPlay, Is.False);
		}

		[Test]
		public void DraculaLaserAttack_PlaysForStage1DraculaBoss()
		{
			var stage1 = Stage1Forest.Build();
			var dracula = new EnemyInfo(stage1.boss.name, stage1.boss.hp, stage1.boss.rank, stage1.boss.themeColor);

			bool shouldPlay = DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
				true,
				stage1.id,
				dracula,
				stage1.boss,
				stage1.boss.enemyDiceProfileId);

			Assert.That(shouldPlay, Is.True);
		}

		[Test]
		public void DraculaLaserAttack_SkipsStage1NormalMob()
		{
			var stage1 = Stage1Forest.Build();
			var slimeDef = stage1.FindMob("슬라임");
			var slime = new EnemyInfo(slimeDef.name, slimeDef.hpMax, slimeDef.rank, slimeDef.themeColor);

			bool shouldPlay = DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
				true,
				stage1.id,
				slime,
				stage1.boss,
				slimeDef.enemyDiceProfileId);

			Assert.That(shouldPlay, Is.False);
		}

		[Test]
		public void DraculaLaserAttack_NullOrMissingIdentifierReturnsFalse()
		{
			var stage1 = Stage1Forest.Build();
			var dracula = new EnemyInfo(stage1.boss.name, stage1.boss.hp, stage1.boss.rank, stage1.boss.themeColor);
			var missingSpriteBoss = new BossDef
			{
				name = stage1.boss.name,
				hp = stage1.boss.hp,
				rank = stage1.boss.rank,
				themeColor = stage1.boss.themeColor,
				spritePath = null,
				enemyDiceProfileId = EnemyDiceProfile.DraculaId,
			};

			Assert.That(DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
				true,
				stage1.id,
				null,
				stage1.boss,
				stage1.boss.enemyDiceProfileId), Is.False);
			Assert.That(DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
				true,
				stage1.id,
				dracula,
				null,
				stage1.boss.enemyDiceProfileId), Is.False);
			Assert.That(DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
				true,
				stage1.id,
				dracula,
				missingSpriteBoss,
				EnemyDiceProfile.DraculaId), Is.False);
		}

		[Test]
		public void EnemyDiceRoller_AtlasProfileUsesAtlasWithoutCreatingRecolorTexture()
		{
			var atlas = new Texture2D(4, 4, TextureFormat.RGBA32, false)
			{
				name = "EnemyDiceAtlasTest"
			};
			var sourceTexture = new Texture2D(4, 4, TextureFormat.RGBA32, false)
			{
				name = "EnemyDiceSourceTextureTest"
			};
			var sourceMaterial = CreateTestMaterial();
			var dieGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
			var rollerGo = new GameObject("EnemyDiceRollerAtlasTest");
			try
			{
				FillTexture(atlas, Color.red);
				FillTexture(sourceTexture, Color.white);
				SetMaterialTexture(sourceMaterial, sourceTexture);
				dieGo.GetComponent<MeshRenderer>().sharedMaterial = sourceMaterial;
				dieGo.AddComponent<Rigidbody>();
				var die = dieGo.AddComponent<Dice>();
				var roller = rollerGo.AddComponent<EnemyDiceRoller>();
				var profile = EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Slime);
				profile.faceAtlasTexture = atlas;
				SetPrivateField(roller, "enemyDice", new[] { die });
				SetPrivateField(roller, "vaultCenter", Vector3.zero);
				SetPrivateField(roller, "profileCatalog", CreateCatalog(profile));

				roller.PlaceForCount(1, profile.id);

				var runtimeMaterial = dieGo.GetComponent<MeshRenderer>().sharedMaterial;
				Assert.That(GetMaterialTexture(runtimeMaterial), Is.SameAs(atlas));
				Assert.That(GetStyleTextureCount(roller), Is.EqualTo(0));
			}
			finally
			{
				Object.DestroyImmediate(rollerGo);
				Object.DestroyImmediate(dieGo);
				Object.DestroyImmediate(sourceMaterial);
				Object.DestroyImmediate(sourceTexture);
				Object.DestroyImmediate(atlas);
			}
		}

		[Test]
		public void CreateCleanEnemyDiceFaceAtlas_CropsBackgroundAndPreservesFaceOrder()
		{
			var source = CreateSyntheticEnemyDiceSheet();
			Texture2D atlas = null;
			try
			{
				atlas = DiceBattleSceneBuilder.CreateCleanEnemyDiceFaceAtlas(source);

				Assert.That(atlas.width, Is.EqualTo(1536));
				Assert.That(atlas.height, Is.EqualTo(1024));
				AssertColorNear(atlas.GetPixel(256, 768), Color.red);
				AssertColorNear(atlas.GetPixel(768, 768), Color.green);
				AssertColorNear(atlas.GetPixel(1280, 768), Color.blue);
				AssertColorNear(atlas.GetPixel(256, 256), Color.yellow);
				AssertColorNear(atlas.GetPixel(768, 256), Color.cyan);
				AssertColorNear(atlas.GetPixel(1280, 256), Color.white);
				Assert.That(IsBlackOrMagenta(atlas.GetPixel(4, 1020)), Is.False);
			}
			finally
			{
				Object.DestroyImmediate(source);
				if (atlas != null)
					Object.DestroyImmediate(atlas);
			}
		}

		[Test]
		public void SlimeDiceJellySolver_ContactImpulseCreatesDentThenSettles()
		{
			var profile = EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Slime);
			var solver = new SlimeDiceJellySolver();
			solver.Configure(
				profile.jellyCompressionMax,
				profile.jellyShearMax,
				profile.jellyImpulseScale,
				profile.jellyStiffness,
				profile.jellyDamping,
				profile.jellySettleEpsilon,
				profile.jellyDentRadius,
				profile.jellyBulgeScale,
				profile.jellyWobbleMax);

			solver.ApplyImpulse(new Vector3(0.5f, 0f, 0f), Vector3.right, new Vector3(0f, -5f, 2f), 10f);

			Assert.That(solver.Compression, Is.GreaterThan(0f));
			Assert.That(solver.Shear.magnitude, Is.GreaterThan(0f));
			Assert.That(Mathf.Abs(solver.Wobble), Is.GreaterThan(0f));
			Assert.That(solver.IsSettled, Is.False);
			Assert.That(solver.GetDent(0).localPoint, Is.EqualTo(new Vector3(0.5f, 0f, 0f)));

			for (int i = 0; i < 480; i++)
				solver.Step(0.02f);

			Assert.That(solver.IsSettled, Is.True);
			for (int i = 0; i < SlimeDiceJellySolver.MaxDentSlots; i++)
			{
				var dent = solver.GetDent(i);
				Assert.That(dent.depth, Is.EqualTo(0f).Within(profile.jellySettleEpsilon));
				Assert.That(dent.shear.magnitude, Is.EqualTo(0f).Within(profile.jellySettleEpsilon));
				Assert.That(Mathf.Abs(dent.wobble), Is.EqualTo(0f).Within(profile.jellySettleEpsilon));
			}
		}

		[Test]
		public void SlimeDiceJellySolver_FlipsIncomingNormalToSurfaceOutward()
		{
			var profile = EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Slime);
			var solver = new SlimeDiceJellySolver();
			solver.Configure(
				profile.jellyCompressionMax,
				profile.jellyShearMax,
				profile.jellyImpulseScale,
				profile.jellyStiffness,
				profile.jellyDamping,
				profile.jellySettleEpsilon,
				profile.jellyDentRadius,
				profile.jellyBulgeScale,
				profile.jellyWobbleMax);

			solver.ApplyImpulse(new Vector3(0.5f, 0f, 0f), Vector3.left, Vector3.zero, 4f);

			Assert.That(solver.GetDent(0).outwardNormal.x, Is.GreaterThan(0f));
		}

		[Test]
		public void SlimeDiceJellyDeformer_SnapToRestClearsDentShaderValues()
		{
			var mesh = CreateSixQuadMesh();
			var sourceMaterial = CreateTestMaterial();
			var jellyMaterial = CreateTestMaterial();
			var root = new GameObject("SlimeDiceTest");
			try
			{
				root.AddComponent<MeshFilter>().sharedMesh = mesh;
				root.AddComponent<MeshRenderer>().sharedMaterial = sourceMaterial;
				var deformer = root.AddComponent<SlimeDiceJellyDeformer>();
				var profile = EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Slime);

				deformer.Configure(profile, jellyMaterial);
				deformer.Solver.ApplyImpulse(new Vector3(0.5f, 0f, 0f), Vector3.right, new Vector3(0f, 4f, 0f), 8f);
				deformer.SnapToRest();

				var jellyRenderer = root.transform.Find("SlimeDiceJellyRender").GetComponent<MeshRenderer>();
				var block = new MaterialPropertyBlock();
				jellyRenderer.GetPropertyBlock(block);

				for (int i = 0; i < SlimeDiceJellySolver.MaxDentSlots; i++)
				{
					Assert.That(block.GetVector($"_DentCenter{i}").w, Is.EqualTo(0f));
					Assert.That(block.GetVector($"_DentShear{i}"), Is.EqualTo(Vector4.zero));
					Assert.That(block.GetVector($"_DentWobble{i}").x, Is.EqualTo(0f));
				}
			}
			finally
			{
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(sourceMaterial);
				Object.DestroyImmediate(jellyMaterial);
			}
		}

		[Test]
		public void SlimeDiceJellyDeformer_ReplacementRendererUsesSourceRendererLayer()
		{
			var mesh = CreateSixQuadMesh();
			var sourceMaterial = CreateTestMaterial();
			var jellyMaterial = CreateTestMaterial();
			var root = new GameObject("SlimeDiceLayerRoot");
			var source = new GameObject("SourceRenderer");
			try
			{
				root.layer = 0;
				source.layer = 2;
				source.transform.SetParent(root.transform, false);
				source.AddComponent<MeshFilter>().sharedMesh = mesh;
				source.AddComponent<MeshRenderer>().sharedMaterial = sourceMaterial;
				var deformer = root.AddComponent<SlimeDiceJellyDeformer>();

				deformer.Configure(EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Slime), jellyMaterial);

				var jellyRenderer = root.transform.Find("SourceRenderer/SlimeDiceJellyRender").GetComponent<MeshRenderer>();
				Assert.That(jellyRenderer.gameObject.layer, Is.EqualTo(source.layer));
			}
			finally
			{
				Object.DestroyImmediate(root);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(sourceMaterial);
				Object.DestroyImmediate(jellyMaterial);
			}
		}

		[Test]
		public void SlimeDiceJellyDeformer_ReplacementRendererRendersThroughDiceCamera()
		{
			var mesh = CreateSixQuadMesh();
			var sourceMaterial = CreateTestMaterial();
			var jellyShader = Shader.Find("Capstone/Slime Dice Jelly");
			Assert.That(jellyShader, Is.Not.Null);
			Assert.That(jellyShader.isSupported, Is.True);
			var jellyMaterial = new Material(jellyShader);
			var die = new GameObject("SlimeDiceRenderSmoke");
			try
			{
				SetLayerRecursive(die, 8);
				die.transform.position = Vector3.zero;
				die.transform.rotation = Quaternion.identity;
				die.transform.localScale = Vector3.one;
				die.AddComponent<MeshFilter>().sharedMesh = mesh;
				die.AddComponent<MeshRenderer>().sharedMaterial = sourceMaterial;

				var deformer = die.AddComponent<SlimeDiceJellyDeformer>();
				deformer.Configure(EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Slime), jellyMaterial);

				var sourceRenderer = die.GetComponent<MeshRenderer>();
				var jellyRenderer = die.transform.Find("SlimeDiceJellyRender")?.GetComponent<MeshRenderer>();
				Assert.That(sourceRenderer.enabled, Is.False);
				Assert.That(jellyRenderer, Is.Not.Null);
				Assert.That(jellyRenderer.enabled, Is.True);

				Assert.That(RenderVisiblePixelCount(), Is.GreaterThan(1000));
			}
			finally
			{
				Object.DestroyImmediate(die);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(sourceMaterial);
				Object.DestroyImmediate(jellyMaterial);
			}
		}

		[Test]
		public void SlimeDiceJellyDeformer_ActualD6PrefabRendersWhenAssetAvailable()
		{
			var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dices/Prefabs/Dice_d6_mine.prefab");
			var jellyMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/SlimeDiceJelly.mat");
			if (prefab == null || jellyMaterial == null)
				Assert.Ignore("Ignored asset package Assets/Dices or SlimeDiceJelly material is not present in this checkout.");

			var die = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
			try
			{
				SetLayerRecursive(die, 8);
				die.transform.position = Vector3.zero;
				die.transform.rotation = Quaternion.identity;
				die.transform.localScale = Vector3.one;

				var deformer = die.AddComponent<SlimeDiceJellyDeformer>();
				deformer.Configure(EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Slime), jellyMaterial);

				var sourceRenderer = die.GetComponent<MeshRenderer>();
				var jellyRenderer = die.transform.Find("SlimeDiceJellyRender")?.GetComponent<MeshRenderer>();
				Assert.That(sourceRenderer.enabled, Is.False);
				Assert.That(jellyRenderer, Is.Not.Null);
				Assert.That(jellyRenderer.enabled, Is.True);
				Assert.That(RenderVisiblePixelCount(), Is.GreaterThan(1000));
			}
			finally
			{
				Object.DestroyImmediate(die);
			}
		}

		[Test]
		public void SlimeDiceJellyShader_DarkPipsRemainHighContrast()
		{
			var mesh = CreateSixQuadMesh();
			var sourceTexture = CreateTopFacePipTexture();
			var sourceMaterial = CreateTestMaterial();
			SetMaterialTexture(sourceMaterial, sourceTexture);
			var jellyShader = Shader.Find("Capstone/Slime Dice Jelly");
			Assert.That(jellyShader, Is.Not.Null);
			var jellyMaterial = new Material(jellyShader);
			var die = new GameObject("SlimeDicePipContrast");
			Texture2D capture = null;
			try
			{
				SetLayerRecursive(die, 8);
				die.transform.position = Vector3.zero;
				die.transform.rotation = Quaternion.identity;
				die.transform.localScale = Vector3.one;
				die.AddComponent<MeshFilter>().sharedMesh = mesh;
				die.AddComponent<MeshRenderer>().sharedMaterial = sourceMaterial;

				var deformer = die.AddComponent<SlimeDiceJellyDeformer>();
				deformer.Configure(EnemyDiceProfile.CreatePrototype(EnemyDiceStyleKind.Slime), jellyMaterial);

				capture = RenderCapture();
				Assert.That(CountVisiblePixels(capture), Is.GreaterThan(1000));
				Assert.That(CountBrightPixels(capture), Is.GreaterThan(1000));
				Assert.That(CountDarkPixels(capture), Is.GreaterThan(12));
				Assert.That(CountOpaqueDarkPixels(capture), Is.GreaterThan(12));
			}
			finally
			{
				if (capture != null)
					Object.DestroyImmediate(capture);
				Object.DestroyImmediate(die);
				Object.DestroyImmediate(mesh);
				Object.DestroyImmediate(sourceTexture);
				Object.DestroyImmediate(sourceMaterial);
				Object.DestroyImmediate(jellyMaterial);
			}
		}

		[Test]
		public void SlimeDiceJellyRenderMeshBuilder_SubdividesSixQuadMeshAndPreservesUvRange()
		{
			var source = CreateSixQuadMesh();
			try
			{
				Assert.That(SlimeDiceJellyRenderMeshBuilder.TryBuildSubdividedRenderMesh(source, 3, out var mesh), Is.True);
				try
				{
					Assert.That(mesh.vertexCount, Is.EqualTo(6 * 4 * 4));
					Assert.That(mesh.triangles.Length, Is.EqualTo(6 * 3 * 3 * 6));
					AssertUvRange(source.uv, mesh.uv);
				}
				finally
				{
					Object.DestroyImmediate(mesh);
				}
			}
			finally
			{
				Object.DestroyImmediate(source);
			}
		}

		[Test]
		public void CreateDefault_UsesOverheadOrthographicCamera()
		{
			var profile = EnemyDiceProfile.CreateDefault();

			Assert.That(profile.cameraOrthographic, Is.True);
			Assert.That(profile.cameraOrthographicSize, Is.EqualTo(EnemyDiceProfile.DefaultCameraOrthographicSize).Within(0.0001f));
			Assert.That(profile.cameraOffset, Is.EqualTo(new Vector3(0f, 7f, 0f)));
			Assert.That(profile.cameraEulerAngles, Is.EqualTo(new Vector3(90f, 0f, 0f)));
			Assert.That(profile.diceScale, Is.EqualTo(EnemyDiceProfile.DefaultDiceScale).Within(0.0001f));
			Assert.That(profile.diceSpacing, Is.EqualTo(EnemyDiceProfile.DefaultDiceSpacing).Within(0.0001f));
			Assert.That(profile.arenaSize, Is.EqualTo(EnemyDiceProfile.DefaultArenaSize));
			Assert.That(profile.overlayAspect, Is.EqualTo(16f / 9f).Within(0.0001f));
			Assert.That(profile.overlayMinHeight, Is.EqualTo(EnemyDiceProfile.DefaultOverlayHeight).Within(0.0001f));
			Assert.That(profile.overlayMaxHeight, Is.EqualTo(EnemyDiceProfile.DefaultOverlayHeight).Within(0.0001f));
		}

		[Test]
		public void NormalizeDefaultDisplaySize_ReplacesSerializedDefaultSizing()
		{
			var profile = EnemyDiceProfile.CreateDefault();
			profile.diceScale = 1.80f;
			profile.diceSpacing = 1.55f;
			profile.cameraOrthographicSize = 2.00f;
			profile.arenaSize = new Vector3(9.2f, 8f, 5.0f);
			profile.overlayMinHeight = 112f;
			profile.overlayMaxHeight = 112f;

			profile.NormalizeDefaultDisplaySize();

			Assert.That(profile.diceScale, Is.EqualTo(EnemyDiceProfile.DefaultDiceScale).Within(0.0001f));
			Assert.That(profile.diceSpacing, Is.EqualTo(EnemyDiceProfile.DefaultDiceSpacing).Within(0.0001f));
			Assert.That(profile.cameraOrthographicSize, Is.EqualTo(EnemyDiceProfile.DefaultCameraOrthographicSize).Within(0.0001f));
			Assert.That(profile.arenaSize, Is.EqualTo(EnemyDiceProfile.DefaultArenaSize));
			Assert.That(profile.overlayMinHeight, Is.EqualTo(EnemyDiceProfile.DefaultOverlayHeight).Within(0.0001f));
			Assert.That(profile.overlayMaxHeight, Is.EqualTo(EnemyDiceProfile.DefaultOverlayHeight).Within(0.0001f));
		}

		[Test]
		public void CreateDefault_PreservesPrefabReference()
		{
			var prefab = new GameObject("DicePrefab");
			try
			{
				var catalog = EnemyDiceProfileCatalog.CreateDefault(prefab);
				var profile = catalog.Resolve(EnemyDiceProfile.DefaultId);

				Assert.That(catalog.Profiles, Has.Length.EqualTo(6));
				Assert.That(catalog.Profiles[0].prefab, Is.SameAs(prefab));
				Assert.That(profile.prefab, Is.SameAs(prefab));
			}
			finally
			{
				Object.DestroyImmediate(prefab);
			}
		}

		static EnemyDiceProfileCatalog CreateCatalog(params EnemyDiceProfile[] profiles)
		{
			var catalog = EnemyDiceProfileCatalog.CreateDefault();
			SetPrivateField(catalog, "profiles", profiles);
			return catalog;
		}

		static IEnumerator InvokeRollRoutine(EnemyDiceRoller roller, int diceCount, string profileId,
			System.Action<EnemyDiceResult> onComplete)
		{
			var method = typeof(EnemyDiceRoller).GetMethod("RollRoutine", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(method, Is.Not.Null);
			return (IEnumerator)method.Invoke(roller, new object[] { diceCount, profileId, onComplete });
		}

		static void SetPrivateField(object target, string fieldName, object value)
		{
			var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null, $"Missing private field {fieldName}");
			field.SetValue(target, value);
		}

		static int GetStyleTextureCount(EnemyDiceRoller roller)
		{
			var field = typeof(EnemyDiceRoller).GetField("styleTextures", BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.That(field, Is.Not.Null);
			var dictionary = field.GetValue(roller) as System.Collections.IDictionary;
			Assert.That(dictionary, Is.Not.Null);
			return dictionary.Count;
		}

		static void FillTexture(Texture2D texture, Color color)
		{
			var pixels = new Color[texture.width * texture.height];
			for (int i = 0; i < pixels.Length; i++)
				pixels[i] = color;
			texture.SetPixels(pixels);
			texture.Apply(false, false);
		}

		static Texture GetMaterialTexture(Material material)
		{
			if (material.HasProperty("_BaseMap"))
				return material.GetTexture("_BaseMap");
			if (material.HasProperty("_MainTex"))
				return material.GetTexture("_MainTex");
			return null;
		}

		static Texture2D CreateSyntheticEnemyDiceSheet()
		{
			const int tile = 512;
			var texture = new Texture2D(tile * 3, tile * 2, TextureFormat.RGBA32, false)
			{
				name = "SyntheticEnemyDiceSheet"
			};
			FillTexture(texture, Color.magenta);
			var colors = new[]
			{
				Color.red, Color.green, Color.blue,
				Color.yellow, Color.cyan, Color.white,
			};

			for (int row = 0; row < 2; row++)
			for (int col = 0; col < 3; col++)
			{
				int face = row * 3 + col;
				int baseX = col * tile;
				int baseY = texture.height - (row + 1) * tile;
				for (int y = 0; y < tile; y++)
				for (int x = 0; x < tile; x++)
				{
					if (x < 96 || x >= 416 || y < 80 || y >= 432)
						texture.SetPixel(baseX + x, baseY + y, Color.magenta);
					else
						texture.SetPixel(baseX + x, baseY + y, colors[face]);
				}
			}
			texture.Apply(false, false);
			return texture;
		}

		static void AssertColorNear(Color actual, Color expected)
		{
			Assert.That(actual.r, Is.EqualTo(expected.r).Within(0.05f));
			Assert.That(actual.g, Is.EqualTo(expected.g).Within(0.05f));
			Assert.That(actual.b, Is.EqualTo(expected.b).Within(0.05f));
			Assert.That(actual.a, Is.GreaterThan(0.95f));
		}

		static bool IsBlackOrMagenta(Color color)
		{
			bool black = color.a > 0.04f && color.r <= 0.06f && color.g <= 0.06f && color.b <= 0.06f;
			bool magenta = color.a > 0.04f && color.r >= 0.70f && color.g <= 0.18f && color.b >= 0.70f;
			return black || magenta;
		}

		static Material CreateTestMaterial()
		{
			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
				shader = Shader.Find("Sprites/Default");
			if (shader == null)
				shader = Shader.Find("Hidden/InternalErrorShader");
			Assert.That(shader, Is.Not.Null);
			return new Material(shader);
		}

		static void SetMaterialTexture(Material material, Texture texture)
		{
			if (material.HasProperty("_BaseMap"))
				material.SetTexture("_BaseMap", texture);
			if (material.HasProperty("_MainTex"))
				material.SetTexture("_MainTex", texture);
		}

		static int RenderVisiblePixelCount()
		{
			var capture = RenderCapture();
			try
			{
				return CountVisiblePixels(capture);
			}
			finally
			{
				Object.DestroyImmediate(capture);
			}
		}

		static Texture2D RenderCapture()
		{
			var cameraGo = new GameObject("SlimeDiceRenderSmokeCamera");
			RenderTexture renderTexture = null;
			try
			{
				var camera = cameraGo.AddComponent<Camera>();
				renderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
				camera.clearFlags = CameraClearFlags.SolidColor;
				camera.backgroundColor = Color.clear;
				camera.orthographic = true;
				camera.orthographicSize = 1.8f;
				camera.cullingMask = 1 << 8;
				camera.targetTexture = renderTexture;
				camera.transform.position = new Vector3(0f, 7f, 0f);
				camera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
				camera.Render();

				return ReadRenderTexture(renderTexture);
			}
			finally
			{
				var camera = cameraGo.GetComponent<Camera>();
				if (camera != null)
					camera.targetTexture = null;
				if (renderTexture != null)
					Object.DestroyImmediate(renderTexture);
				Object.DestroyImmediate(cameraGo);
			}
		}

		static Texture2D ReadRenderTexture(RenderTexture renderTexture)
		{
			var previous = RenderTexture.active;
			try
			{
				RenderTexture.active = renderTexture;
				var texture = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);
				texture.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
				texture.Apply(false, false);
				return texture;
			}
			finally
			{
				RenderTexture.active = previous;
			}
		}

		static int CountVisiblePixels(Texture2D texture)
		{
			var pixels = texture.GetPixels32();
			int count = 0;
			for (int i = 0; i < pixels.Length; i++)
			{
				var pixel = pixels[i];
				if (pixel.a > 16 && (pixel.r > 16 || pixel.g > 16 || pixel.b > 16))
					count++;
			}
			return count;
		}

		static int CountBrightPixels(Texture2D texture)
		{
			var pixels = texture.GetPixels32();
			int count = 0;
			for (int i = 0; i < pixels.Length; i++)
			{
				var pixel = pixels[i];
				float luminance = (pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f) / 255f;
				if (pixel.a > 16 && luminance > 0.30f)
					count++;
			}
			return count;
		}

		static int CountDarkPixels(Texture2D texture)
		{
			var pixels = texture.GetPixels32();
			int count = 0;
			for (int i = 0; i < pixels.Length; i++)
			{
				var pixel = pixels[i];
				float luminance = (pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f) / 255f;
				if (pixel.a > 16 && luminance < 0.08f)
					count++;
			}
			return count;
		}

		static int CountOpaqueDarkPixels(Texture2D texture)
		{
			var pixels = texture.GetPixels32();
			int count = 0;
			for (int i = 0; i < pixels.Length; i++)
			{
				var pixel = pixels[i];
				float luminance = (pixel.r * 0.299f + pixel.g * 0.587f + pixel.b * 0.114f) / 255f;
				if (pixel.a > 220 && luminance < 0.08f)
					count++;
			}
			return count;
		}

		static void SetLayerRecursive(GameObject go, int layer)
		{
			go.layer = layer;
			foreach (Transform child in go.transform)
				SetLayerRecursive(child.gameObject, layer);
		}

		static Mesh CreateSixQuadMesh()
		{
			var vertices = new List<Vector3>();
			var normals = new List<Vector3>();
			var uvs = new List<Vector2>();
			var triangles = new List<int>();

			AddFace(vertices, normals, uvs, triangles, Vector3.right, Vector3.forward, Vector3.up, new Rect(0.00f, 0.00f, 0.25f, 0.25f));
			AddFace(vertices, normals, uvs, triangles, Vector3.left, Vector3.back, Vector3.up, new Rect(0.25f, 0.00f, 0.25f, 0.25f));
			AddFace(vertices, normals, uvs, triangles, Vector3.up, Vector3.right, Vector3.forward, new Rect(0.50f, 0.00f, 0.25f, 0.25f));
			AddFace(vertices, normals, uvs, triangles, Vector3.down, Vector3.right, Vector3.back, new Rect(0.00f, 0.25f, 0.25f, 0.25f));
			AddFace(vertices, normals, uvs, triangles, Vector3.forward, Vector3.left, Vector3.up, new Rect(0.25f, 0.25f, 0.25f, 0.25f));
			AddFace(vertices, normals, uvs, triangles, Vector3.back, Vector3.right, Vector3.up, new Rect(0.50f, 0.25f, 0.25f, 0.25f));

			var mesh = new Mesh
			{
				name = "SixQuadTestMesh"
			};
			mesh.SetVertices(vertices);
			mesh.SetNormals(normals);
			mesh.SetUVs(0, uvs);
			mesh.SetTriangles(triangles, 0);
			mesh.RecalculateBounds();
			return mesh;
		}

		static Texture2D CreateTopFacePipTexture()
		{
			const int size = 64;
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				filterMode = FilterMode.Point,
				wrapMode = TextureWrapMode.Clamp
			};
			var face = new Color32(135, 255, 65, 255);
			var pip = new Color32(2, 16, 4, 255);
			var pixels = new Color32[size * size];
			for (int i = 0; i < pixels.Length; i++)
				pixels[i] = face;

			for (int y = 6; y <= 10; y++)
			for (int x = 38; x <= 42; x++)
				pixels[y * size + x] = pip;

			texture.SetPixels32(pixels);
			texture.Apply(false, false);
			return texture;
		}

		static void AddFace(List<Vector3> vertices, List<Vector3> normals, List<Vector2> uvs,
			List<int> triangles, Vector3 normal, Vector3 axisU, Vector3 axisV, Rect uv)
		{
			int start = vertices.Count;
			Vector3 center = normal * 0.5f;
			Vector3 u = axisU * 0.5f;
			Vector3 v = axisV * 0.5f;

			vertices.Add(center - u - v);
			vertices.Add(center - u + v);
			vertices.Add(center + u + v);
			vertices.Add(center + u - v);

			for (int i = 0; i < 4; i++)
				normals.Add(normal);

			uvs.Add(new Vector2(uv.xMin, uv.yMin));
			uvs.Add(new Vector2(uv.xMin, uv.yMax));
			uvs.Add(new Vector2(uv.xMax, uv.yMax));
			uvs.Add(new Vector2(uv.xMax, uv.yMin));

			triangles.Add(start + 0);
			triangles.Add(start + 1);
			triangles.Add(start + 2);
			triangles.Add(start + 0);
			triangles.Add(start + 2);
			triangles.Add(start + 3);
		}

		static void AssertUvRange(Vector2[] sourceUvs, Vector2[] renderUvs)
		{
			Assert.That(MinUv(renderUvs).x, Is.EqualTo(MinUv(sourceUvs).x).Within(0.0001f));
			Assert.That(MinUv(renderUvs).y, Is.EqualTo(MinUv(sourceUvs).y).Within(0.0001f));
			Assert.That(MaxUv(renderUvs).x, Is.EqualTo(MaxUv(sourceUvs).x).Within(0.0001f));
			Assert.That(MaxUv(renderUvs).y, Is.EqualTo(MaxUv(sourceUvs).y).Within(0.0001f));
		}

		static Vector2 MinUv(Vector2[] uvs)
		{
			Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
			for (int i = 0; i < uvs.Length; i++)
			{
				min.x = Mathf.Min(min.x, uvs[i].x);
				min.y = Mathf.Min(min.y, uvs[i].y);
			}
			return min;
		}

		static Vector2 MaxUv(Vector2[] uvs)
		{
			Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
			for (int i = 0; i < uvs.Length; i++)
			{
				max.x = Mathf.Max(max.x, uvs[i].x);
				max.y = Mathf.Max(max.y, uvs[i].y);
			}
			return max;
		}
	}
}
