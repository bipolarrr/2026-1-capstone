using System.Collections.Generic;
using System.Reflection;
using TMPro;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BattleTests
{
	public class EnemyAttackPositionResolverTests
	{
		GameObject slotGo;
		GameObject bodyGo;
		GameObject playerGo;
		GameObject canvasGo;

		[TearDown]
		public void TearDown()
		{
			if (playerGo != null) Object.DestroyImmediate(playerGo);
			if (bodyGo != null) Object.DestroyImmediate(bodyGo);
			if (slotGo != null) Object.DestroyImmediate(slotGo);
			if (canvasGo != null) Object.DestroyImmediate(canvasGo);
		}

		[Test]
		public void Melee_StandsInFrontOfPlayer()
		{
			var (slot, body, player) = BuildTransforms();
			var plan = EnemyAttackPositionResolver.Resolve(slot, body, player,
				new MobDef { attackRangeType = EnemyAttackRangeType.Melee });

			Assert.AreEqual(EnemyAttackRangeType.Melee, plan.rangeType);
			AssertVector(ExpectedMeleeStand(body, player, slot.position), plan.standWorldPosition);
			AssertVector(ExpectedMeleeImpact(body, player, slot.position), plan.impactWorldPosition);
		}

		[Test]
		public void Ranged_StaysAtHome()
		{
			var (slot, body, player) = BuildTransforms();
			var plan = EnemyAttackPositionResolver.Resolve(slot, body, player,
				new MobDef { attackRangeType = EnemyAttackRangeType.Ranged });

			Assert.AreEqual(EnemyAttackRangeType.Ranged, plan.rangeType);
			AssertVector(slot.position, plan.standWorldPosition);
		}

		[Test]
		public void MidRange_StandsBetweenHomeAndMelee()
		{
			var (slot, body, player) = BuildTransforms();
			var plan = EnemyAttackPositionResolver.Resolve(slot, body, player,
				new MobDef { attackRangeType = EnemyAttackRangeType.MidRange });

			Vector3 melee = ExpectedMeleeStand(body, player, slot.position);
			AssertVector(Vector3.Lerp(slot.position, melee, 0.55f), plan.standWorldPosition);
		}

		[Test]
		public void Unique_StaysAtHome()
		{
			var (slot, body, player) = BuildTransforms();
			var plan = EnemyAttackPositionResolver.Resolve(slot, body, player,
				new MobDef { attackRangeType = EnemyAttackRangeType.Unique });

			Assert.AreEqual(EnemyAttackRangeType.Unique, plan.rangeType);
			AssertVector(slot.position, plan.standWorldPosition);
		}

		[Test]
		public void SlimeLeapSlamPlan_UsesFixedReferenceOffsets()
		{
			var home = new Vector3(10f, 24f, 0f);
			var playerRect = Rect.MinMaxRect(240f, 20f, 360f, 180f);

			var plan = BattleAnimations.ResolveEnemyLeapSlamMotionPlan(home, playerRect);
			float leapTargetOffset = 1080f * BattleAnimations.EnemyLeapSlamLeapTargetOffsetNormalizedY;
			float apexOffset = 1080f * BattleAnimations.EnemyLeapSlamApexOffsetNormalizedY;
			float impactOffset = 1080f * BattleAnimations.EnemyLeapSlamImpactOffsetNormalizedY;
			float expectedLeapTargetY = playerRect.yMax + leapTargetOffset;
			float expectedApexY = Mathf.Max(home.y, expectedLeapTargetY) + apexOffset;
			float expectedImpactY = playerRect.yMax + impactOffset;

			AssertVector(home, plan.homeWorldPosition);
			Assert.That(plan.apexWorldPosition.x, Is.EqualTo(Mathf.Lerp(home.x, playerRect.center.x, 0.5f)).Within(0.001f));
			Assert.That(plan.leapTargetWorldPosition.x, Is.EqualTo(playerRect.center.x).Within(0.001f));
			Assert.That(plan.impactWorldPosition.x, Is.EqualTo(playerRect.center.x).Within(0.001f));
			Assert.That(plan.leapTargetWorldPosition.y, Is.EqualTo(expectedLeapTargetY).Within(0.001f));
			Assert.That(plan.apexWorldPosition.y, Is.EqualTo(expectedApexY).Within(0.001f));
			Assert.That(plan.impactWorldPosition.y, Is.EqualTo(expectedImpactY).Within(0.001f));
			Assert.That(plan.impactWorldPosition.y, Is.LessThan(plan.leapTargetWorldPosition.y));
		}

		[Test]
		public void SlimeLeapSlamPlan_SameInputsShareSameApexAcrossBattleModes()
		{
			var home = new Vector3(10f, 24f, 0f);
			var playerRect = Rect.MinMaxRect(240f, 20f, 360f, 180f);

			var dice = BattleAnimations.ResolveEnemyLeapSlamMotionPlan(home, playerRect);
			var mahjong = BattleAnimations.ResolveEnemyLeapSlamMotionPlan(home, playerRect);
			var holdem = BattleAnimations.ResolveEnemyLeapSlamMotionPlan(home, playerRect);

			Assert.That(mahjong.apexWorldPosition.y, Is.EqualTo(dice.apexWorldPosition.y).Within(0.001f));
			Assert.That(holdem.apexWorldPosition.y, Is.EqualTo(dice.apexWorldPosition.y).Within(0.001f));
			Assert.That(mahjong.leapTargetWorldPosition.y, Is.EqualTo(dice.leapTargetWorldPosition.y).Within(0.001f));
			Assert.That(holdem.impactWorldPosition.y, Is.EqualTo(dice.impactWorldPosition.y).Within(0.001f));
		}

		[Test]
		public void SlimeLeapSlamPlanFromBody_RepeatedResolveKeepsSameApex()
		{
			var (_, body, player) = BuildTransforms();

			var first = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);
			var second = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);
			var third = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);

			AssertVector(first.homeWorldPosition, second.homeWorldPosition);
			Assert.That(second.apexWorldPosition.y, Is.EqualTo(first.apexWorldPosition.y).Within(0.001f));
			Assert.That(third.apexWorldPosition.y, Is.EqualTo(first.apexWorldPosition.y).Within(0.001f));
			Assert.That(second.leapTargetWorldPosition.y, Is.EqualTo(first.leapTargetWorldPosition.y).Within(0.001f));
			Assert.That(second.impactWorldPosition.y, Is.EqualTo(first.impactWorldPosition.y).Within(0.001f));
		}

		[Test]
		public void SlimeLeapSlamPlanFromBody_IgnoresEnemyVisualSizeScaleAndSprite()
		{
			var (_, body, player) = BuildTransforms();
			var first = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);
			var image = body.gameObject.AddComponent<Image>();
			var sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
			sprite.name = "visual-only";

			try
			{
				body.sizeDelta = new Vector2(480f, 12f);
				body.localScale = new Vector3(3.5f, 0.25f, 1f);
				image.sprite = sprite;

				var second = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);

				AssertVector(first.homeWorldPosition, second.homeWorldPosition);
				Assert.That(second.apexWorldPosition.y, Is.EqualTo(first.apexWorldPosition.y).Within(0.001f));
				Assert.That(second.leapTargetWorldPosition.y, Is.EqualTo(first.leapTargetWorldPosition.y).Within(0.001f));
				Assert.That(second.impactWorldPosition.y, Is.EqualTo(first.impactWorldPosition.y).Within(0.001f));
			}
			finally
			{
				Object.DestroyImmediate(sprite);
			}
		}

		[Test]
		public void EnemySpriteAnimator_StopOnFirstIdle_ResetsCurrentFrameToZero()
		{
			var animatorGo = new GameObject("Animator", typeof(RectTransform), typeof(Image), typeof(EnemySpriteAnimator));
			var animator = animatorGo.GetComponent<EnemySpriteAnimator>();
			var image = animatorGo.GetComponent<Image>();
			var first = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
			var second = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
			first.name = "idle-0";
			second.name = "idle-1";

			try
			{
				SetPrivateField(animator, "targetImage", image);
				SetPrivateField(animator, "idleSprites", new[] { first, second });
				SetPrivateField(animator, "currentFrameIndex", 1);
				SetPrivateField(animator, "currentFrameCount", 2);
				image.sprite = second;

				animator.StopOnFirstIdle(null);

				Assert.That(animator.CurrentFrameIndex, Is.EqualTo(0));
				Assert.That(animator.CurrentFrameCount, Is.EqualTo(2));
				Assert.That(image.sprite, Is.SameAs(first));
			}
			finally
			{
				Object.DestroyImmediate(first);
				Object.DestroyImmediate(second);
				Object.DestroyImmediate(animatorGo);
			}
		}

		[Test]
		public void EnemySpriteAnimator_RestoresVisualScaleAndOffset_AfterAttack()
		{
			var animatorGo = new GameObject("Animator", typeof(RectTransform), typeof(Image), typeof(EnemySpriteAnimator));
			var animator = animatorGo.GetComponent<EnemySpriteAnimator>();
			var image = animatorGo.GetComponent<Image>();
			var idle = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
			var attack = Sprite.Create(Texture2D.whiteTexture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f));
			idle.name = "idle";
			attack.name = "attack";

			try
			{
				var set = new EnemySpriteAnimationSet
				{
					idleSprites = new[] { idle },
					attackSprites = new[] { attack },
					attackFrameRate = 30f,
					attackVisualScaleMultiplier = 0.842f,
					attackVisualOffset = new Vector2(0f, -27.2f),
				};
				SetPrivateField(animator, "targetImage", image);
				animator.Bind(set, idle);
				var rt = image.rectTransform;
				var originalScale = new Vector3(1.2f, 1.4f, 1f);
				var originalPosition = new Vector2(5f, 6f);
				rt.localScale = originalScale;
				rt.anchoredPosition = originalPosition;

				Assert.That(animator.PlayAttack(), Is.Not.Null);

				AssertVector(originalScale * 0.842f, rt.localScale);
				AssertVector(originalPosition + new Vector2(0f, -27.2f), rt.anchoredPosition);

				animator.ReturnToIdle(idle);

				AssertVector(originalScale, rt.localScale);
				AssertVector(originalPosition, rt.anchoredPosition);
			}
			finally
			{
				Object.DestroyImmediate(idle);
				Object.DestroyImmediate(attack);
				Object.DestroyImmediate(animatorGo);
			}
		}

		[Test]
		public void EnemySpriteAnimator_UsesFullTextureAttackFrame_WhenConfigured()
		{
			var animatorGo = new GameObject("Animator", typeof(RectTransform), typeof(Image), typeof(EnemySpriteAnimator));
			var animator = animatorGo.GetComponent<EnemySpriteAnimator>();
			var image = animatorGo.GetComponent<Image>();
			var texture = new Texture2D(16, 12, TextureFormat.RGBA32, false);
			var idle = Sprite.Create(texture, new Rect(0f, 0f, 16f, 12f), new Vector2(0.5f, 0.5f));
			var croppedAttack = Sprite.Create(texture, new Rect(4f, 2f, 8f, 6f), new Vector2(0.5f, 0.5f));
			idle.name = "idle";
			croppedAttack.name = "attack_crop";

			try
			{
				var set = new EnemySpriteAnimationSet
				{
					idleSprites = new[] { idle },
					attackSprites = new[] { croppedAttack },
					attackFrameRate = 30f,
					attackUseFullTextureFrames = true,
				};
				SetPrivateField(animator, "targetImage", image);
				animator.Bind(set, idle);

				Assert.That(animator.PlayAttack(), Is.Not.Null);

				Assert.That(image.sprite, Is.Not.SameAs(croppedAttack));
				Assert.That(image.sprite.rect.x, Is.EqualTo(0f).Within(0.001f));
				Assert.That(image.sprite.rect.y, Is.EqualTo(0f).Within(0.001f));
				Assert.That(image.sprite.rect.width, Is.EqualTo(16f).Within(0.001f));
				Assert.That(image.sprite.rect.height, Is.EqualTo(12f).Within(0.001f));
			}
			finally
			{
				Object.DestroyImmediate(animatorGo);
				Object.DestroyImmediate(idle);
				Object.DestroyImmediate(croppedAttack);
				Object.DestroyImmediate(texture);
			}
		}

		[Test]
		public void SlimeLeapSlamPlanFromBody_RestoresScaleBeforeNextTrajectory()
		{
			var (_, body, player) = BuildTransforms();

			var first = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);
			body.sizeDelta = new Vector2(320f, 24f);
			body.localScale = new Vector3(1.26f, 0.62f, 1f);
			body.anchoredPosition = new Vector2(12f, -35f);

			var second = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);

			AssertVector(first.homeWorldPosition, second.homeWorldPosition);
			AssertVector(Vector3.one, body.localScale);
			AssertVector(Vector2.zero, body.anchoredPosition);
			Assert.That(second.apexWorldPosition.y, Is.EqualTo(first.apexWorldPosition.y).Within(0.001f));
		}

		[Test]
		public void SlimeLeapSlamPlanFromBody_UsesCanvasReferenceScale()
		{
			var (slot, body, player) = BuildTransforms();
			canvasGo = new GameObject("Canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
			var scaler = canvasGo.GetComponent<CanvasScaler>();
			scaler.referenceResolution = new Vector2(1920f, 2160f);
			slot.SetParent(canvasGo.transform, true);
			player.SetParent(canvasGo.transform, true);

			var fromBody = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);
			var expected = BattleAnimations.ResolveEnemyLeapSlamMotionPlan(
				body.position,
				WorldRect(player),
				canvasReferenceScale: 2f);

			Assert.That(fromBody.apexWorldPosition.y, Is.EqualTo(expected.apexWorldPosition.y).Within(0.001f));
			Assert.That(fromBody.leapTargetWorldPosition.y, Is.EqualTo(expected.leapTargetWorldPosition.y).Within(0.001f));
			Assert.That(fromBody.impactWorldPosition.y, Is.EqualTo(expected.impactWorldPosition.y).Within(0.001f));
		}

		[Test]
		public void SlimeLeapSlamPlanFromBody_UsesStableHomeWhenCurrentPositionDrifts()
		{
			var (_, body, player) = BuildTransforms();

			var first = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);
			body.position += new Vector3(0f, -48f, 0f);

			var second = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);

			AssertVector(first.homeWorldPosition, second.homeWorldPosition);
			Assert.That(second.apexWorldPosition.y, Is.EqualTo(first.apexWorldPosition.y).Within(0.001f));
		}

		[Test]
		public void VisualBounds_PreserveAspectUsesRenderedBodyNotSlot()
		{
			var (slot, body, _) = BuildTransforms();
			slot.sizeDelta = new Vector2(400f, 160f);
			body.sizeDelta = new Vector2(400f, 120f);
			var image = body.gameObject.AddComponent<Image>();
			image.preserveAspect = true;
			var texture = new Texture2D(60, 180, TextureFormat.RGBA32, false);
			var sprite = Sprite.Create(texture, new Rect(0f, 0f, 60f, 180f), new Vector2(0.5f, 0.5f));
			image.sprite = sprite;

			try
			{
				Assert.IsTrue(EnemyVisualBoundsResolver.TryResolveBoundsIn(image, slot, out var bounds));
				Assert.That(bounds.width, Is.EqualTo(40f).Within(0.01f));
				Assert.That(bounds.height, Is.EqualTo(120f).Within(0.01f));
				Assert.That(bounds.width, Is.LessThan(slot.rect.width));
			}
			finally
			{
				Object.DestroyImmediate(sprite);
				Object.DestroyImmediate(texture);
			}
		}

		[Test]
		public void VisualBounds_NullSpriteFallsBackToBodyRect()
		{
			var (slot, body, _) = BuildTransforms();
			body.sizeDelta = new Vector2(220f, 80f);
			var image = body.gameObject.AddComponent<Image>();
			image.preserveAspect = true;
			image.sprite = null;

			Assert.IsTrue(EnemyVisualBoundsResolver.TryResolveBoundsIn(image, slot, out var bounds));
			Assert.That(bounds.width, Is.EqualTo(220f).Within(0.01f));
			Assert.That(bounds.height, Is.EqualTo(80f).Within(0.01f));
		}

		[Test]
		public void BattleEnemyLayout_UsesSameVisualBoundsForInfoAndTargetFrame()
		{
			var (slot, body, _) = BuildTransforms();
			slot.sizeDelta = new Vector2(420f, 180f);
			body.sizeDelta = new Vector2(420f, 126f);
			var bodyImage = body.gameObject.AddComponent<Image>();
			bodyImage.preserveAspect = true;
			var texture = new Texture2D(70, 210, TextureFormat.RGBA32, false);
			var sprite = Sprite.Create(texture, new Rect(0f, 0f, 70f, 210f), new Vector2(0.5f, 0.5f));
			bodyImage.sprite = sprite;

			var info = new GameObject("InfoPanel", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
			info.SetParent(slot, false);
			var name = new GameObject("Name", typeof(RectTransform), typeof(TextMeshProUGUI))
				.GetComponent<TextMeshProUGUI>();
			name.rectTransform.SetParent(info, false);
			var hpBg = new GameObject("HpBarBg", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
			hpBg.SetParent(info, false);
			var hpFill = new GameObject("HpFill", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
			hpFill.rectTransform.SetParent(hpBg, false);
			var hpText = new GameObject("HpText", typeof(RectTransform), typeof(TextMeshProUGUI))
				.GetComponent<TextMeshProUGUI>();
			hpText.rectTransform.SetParent(info, false);
			var marker = new GameObject("TargetMarker", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
			marker.rectTransform.SetParent(slot, false);

			var controllerGo = new GameObject("Controller", typeof(TestBattleController));
			var controller = controllerGo.GetComponent<TestBattleController>();
			controller.BindForTest(slot.gameObject, bodyImage, name, hpFill, hpText, marker);

			try
			{
				controller.RefreshForTest();

				Assert.IsTrue(EnemyVisualBoundsResolver.TryResolveBoundsIn(bodyImage, slot, out var visualBounds));
				Assert.IsTrue(EnemyVisualBoundsResolver.TryResolveRectTransformBoundsIn(
					marker.rectTransform, slot, out var markerBounds));
				Assert.IsTrue(EnemyVisualBoundsResolver.TryResolveRectTransformBoundsIn(
					info, slot, out var infoBounds));

				Assert.That(markerBounds.width,
					Is.EqualTo(visualBounds.width + BattleControllerBase.EnemyVisualFramePadding * 2f).Within(0.01f));
				Assert.That(markerBounds.center.x, Is.EqualTo(visualBounds.center.x).Within(0.01f));
				Assert.That(infoBounds.yMin,
					Is.EqualTo(visualBounds.yMax + BattleControllerBase.EnemyVisualUiPadding).Within(0.01f));
				Assert.That(infoBounds.center.x, Is.EqualTo(visualBounds.center.x).Within(0.01f));
				Assert.That(hpBg.rect.width, Is.EqualTo(BattleControllerBase.EnemyHpMinWidth).Within(0.01f));
			}
			finally
			{
				Object.DestroyImmediate(controllerGo);
				Object.DestroyImmediate(sprite);
				Object.DestroyImmediate(texture);
			}
		}

		[Test]
		public void SlimeLeapSlamPlanFromBody_RestoresImpactPoseAndSiblingBeforeNextTrajectory()
		{
			var (slot, body, player) = BuildTransforms();
			var sibling = new GameObject("Sibling", typeof(RectTransform)).GetComponent<RectTransform>();
			sibling.SetParent(slot, false);
			body.SetSiblingIndex(0);
			sibling.SetSiblingIndex(1);

			var first = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);
			body.SetAsLastSibling();
			body.position = first.impactWorldPosition;
			body.localScale = new Vector3(1.26f, 0.62f, 1f);

			var second = BattleAnimations.ResolveEnemyLeapSlamMotionPlanFromBody(body, player);

			AssertVector(first.homeWorldPosition, second.homeWorldPosition);
			AssertVector(Vector3.one, body.localScale);
			AssertVector(Vector2.zero, body.anchoredPosition);
			Assert.That(body.GetSiblingIndex(), Is.EqualTo(0));
			Assert.That(second.apexWorldPosition.y, Is.EqualTo(first.apexWorldPosition.y).Within(0.001f));
			Assert.That(second.impactWorldPosition.y, Is.EqualTo(first.impactWorldPosition.y).Within(0.001f));
		}

		[Test]
		public void SlimeLeapSlamIdentity_RequiresSlimeProfileAndUniqueNonProjectile()
		{
			var slime = new EnemyInfo("슬라임", 10, 1, Color.white);
			var slimeDef = new MobDef
			{
				name = "슬라임",
				attackRangeType = EnemyAttackRangeType.Unique,
				enemyDiceProfileId = EnemyDiceProfile.SlimeId,
				attackSpriteFolderPath = "Assets/Mobs/Sprites/Slime/Attack",
			};
			var elementalDef = new MobDef
			{
				name = "물의 정령",
				attackRangeType = EnemyAttackRangeType.Unique,
				attackVfxSpritePath = "Assets/Mobs/Sprites/Elemental/WaterCannon/WaterCannon.png",
			};
			var batDef = new MobDef
			{
				name = "박쥐",
				attackRangeType = EnemyAttackRangeType.Melee,
				uniqueAttackProfileId = "bat",
				enemyDiceProfileId = EnemyDiceProfile.BatId,
				attackSpriteFolderPath = "Assets/Mobs/Sprites/Bat/Attack",
			};
			var skeletonDef = new MobDef
			{
				name = "해골",
				attackRangeType = EnemyAttackRangeType.Ranged,
				projectileSpritePath = "Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png",
				enemyDiceProfileId = EnemyDiceProfile.SkeletonId,
			};
			var goblinWithSlimeProfile = new MobDef
			{
				name = "고블린",
				attackRangeType = EnemyAttackRangeType.Melee,
				enemyDiceProfileId = EnemyDiceProfile.SlimeId,
				attackSpriteFolderPath = "Assets/Mobs/Sprites/Goblin/Attack",
			};

			Assert.IsTrue(BattleAnimations.ShouldUseSlimeLeapSlam(slime, slimeDef));
			Assert.IsFalse(BattleAnimations.ShouldUseSlimeLeapSlam(
				new EnemyInfo("물의 정령", 10, 1, Color.white), elementalDef));
			Assert.IsFalse(BattleAnimations.ShouldUseSlimeLeapSlam(
				new EnemyInfo("박쥐", 10, 3, Color.white), batDef));
			Assert.IsFalse(BattleAnimations.ShouldUseSlimeLeapSlam(
				new EnemyInfo("해골", 10, 2, Color.white), skeletonDef));
			Assert.IsFalse(BattleAnimations.ShouldUseSlimeLeapSlam(
				new EnemyInfo("고블린", 10, 1, Color.white), goblinWithSlimeProfile));
		}

		[Test]
		public void DefaultProjectileResolvesAsRanged()
		{
			var range = EnemyAttackPositionResolver.ResolveRangeType(
				new MobDef { projectileSpritePath = "Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png" });

			Assert.AreEqual(EnemyAttackRangeType.Ranged, range);
		}

		[Test]
		public void GoblinMeleeTimingProfile_IsAssignedInRuntimeStages()
		{
			var forestGoblin = Stage1Forest.Build().FindMob("고블린");
			var caveGoblin = Stage2Cave.Build().FindMob("고블린");

			AssertGoblinTimingProfile(forestGoblin);
			AssertGoblinTimingProfile(caveGoblin);
		}

		[Test]
		public void ProjectileAndOtherMeleeMobs_DoNotInheritGoblinTimingProfile()
		{
			var forest = Stage1Forest.Build();
			var cave = Stage2Cave.Build();

			Assert.IsNull(forest.FindMob("해골").attackTimingProfile);
			Assert.IsNull(forest.FindMob("박쥐").attackTimingProfile);
			Assert.IsNull(cave.FindMob("골렘").attackTimingProfile);
			Assert.IsNull(cave.FindMob("물의 정령").attackTimingProfile);
			Assert.AreEqual(EnemyAttackRangeType.Ranged,
				EnemyAttackPositionResolver.ResolveRangeType(forest.FindMob("해골")));
		}

		[Test]
		public void GoblinHitStartDelay_AlignsSmallHitReactionWithVisualImpact()
		{
			float enemyAttackDuration = EnemyAttackTiming.GoblinMeleeAttackFrameCount / 30f;
			float playerHitDuration = EnemyAttackTiming.PlayerSmallHitFrameCount / 30f;

			float delay = EnemyAttackTiming.ComputePlayerHitStartDelay(
				enemyAttackDuration,
				EnemyAttackTiming.GoblinMeleeImpactNormalizedTime,
				playerHitDuration,
				EnemyAttackTiming.PlayerSmallHitReactionNormalizedTime);

			float visualImpactTime = enemyAttackDuration * EnemyAttackTiming.GoblinMeleeImpactNormalizedTime;
			float playerReactionTime = delay
				+ playerHitDuration * EnemyAttackTiming.PlayerSmallHitReactionNormalizedTime;

			Assert.That(delay, Is.GreaterThan(0f));
			Assert.That(delay, Is.EqualTo(22f / 30f).Within(0.0001f));
			Assert.That(playerReactionTime, Is.EqualTo(visualImpactTime).Within(0.0001f));
			Assert.That(delay, Is.LessThanOrEqualTo(enemyAttackDuration));
		}

		[Test]
		public void HitStartDelay_ClampsWhenReactionWouldNeedNegativeStart()
		{
			float delay = EnemyAttackTiming.ComputePlayerHitStartDelay(
				enemyAttackDuration: 0.2f,
				enemyImpactNormalizedTime: 0.25f,
				playerHitDuration: 1f,
				playerReactionNormalizedTime: 0.5f);

			Assert.That(delay, Is.EqualTo(0f).Within(0.0001f));
			Assert.IsFalse(float.IsNaN(delay));
		}

		(RectTransform slot, RectTransform body, RectTransform player) BuildTransforms()
		{
			slotGo = new GameObject("Slot", typeof(RectTransform));
			bodyGo = new GameObject("Body", typeof(RectTransform));
			playerGo = new GameObject("Player", typeof(RectTransform));

			var slot = slotGo.GetComponent<RectTransform>();
			slot.localPosition = new Vector3(5f, 7f, 0f);
			slot.position = new Vector3(0f, 10f, 0f);

			var body = bodyGo.GetComponent<RectTransform>();
			body.SetParent(slot, false);
			body.sizeDelta = new Vector2(100f, 100f);

			var player = playerGo.GetComponent<RectTransform>();
			player.position = new Vector3(300f, 20f, 0f);
			player.sizeDelta = new Vector2(120f, 160f);

			return (slot, body, player);
		}

		static void AssertVector(Vector3 expected, Vector3 actual)
		{
			Assert.That(Vector3.Distance(expected, actual), Is.LessThan(0.001f));
		}

		static void AssertVector(Vector2 expected, Vector2 actual)
		{
			Assert.That(Vector2.Distance(expected, actual), Is.LessThan(0.001f));
		}

		static void AssertGoblinTimingProfile(MobDef goblin)
		{
			Assert.NotNull(goblin);
			Assert.AreEqual(EnemyAttackRangeType.Melee, goblin.attackRangeType);
			Assert.AreEqual(EnemyAttackTiming.GoblinMeleeAttackFrameCount, goblin.attackSpriteFrameCount);
			Assert.AreEqual(30f, goblin.attackFrameRate);
			Assert.NotNull(goblin.attackTimingProfile);
			Assert.AreEqual(EnemyAttackTiming.GoblinEnemyId, goblin.attackTimingProfile.enemyId);
			Assert.That(goblin.attackTimingProfile.impactNormalizedTime,
				Is.EqualTo(EnemyAttackTiming.GoblinMeleeImpactNormalizedTime).Within(0.0001f));
			Assert.That(goblin.attackTimingProfile.playerSmallHitReactionNormalizedTime,
				Is.EqualTo(EnemyAttackTiming.PlayerSmallHitReactionNormalizedTime).Within(0.0001f));
			Assert.That(goblin.attackTimingProfile.playerStrongHitReactionNormalizedTime,
				Is.EqualTo(EnemyAttackTiming.PlayerStrongHitReactionNormalizedTime).Within(0.0001f));
		}

			static Rect WorldRect(RectTransform rt)
			{
				Vector3[] corners = new Vector3[4];
				rt.GetWorldCorners(corners);
				float xMin = Mathf.Min(corners[0].x, corners[2].x);
			float xMax = Mathf.Max(corners[0].x, corners[2].x);
			float yMin = Mathf.Min(corners[0].y, corners[2].y);
				float yMax = Mathf.Max(corners[0].y, corners[2].y);
				return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
			}

			static void SetPrivateField(object target, string fieldName, object value)
			{
				var field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
				Assert.That(field, Is.Not.Null, $"Missing field {fieldName}");
				field.SetValue(target, value);
			}

			static Vector3 ExpectedMeleeStand(RectTransform enemyBody, RectTransform playerBody, Vector3 slotPosition)
			{
			float enemyLeftOffset = -enemyBody.rect.width * 0.5f;
			float playerRight = playerBody.position.x + playerBody.rect.width * 0.5f;
			float meleeGap = 8f;
			return new Vector3(playerRight + meleeGap - enemyLeftOffset, slotPosition.y, slotPosition.z);
		}

		static Vector3 ExpectedMeleeImpact(RectTransform enemyBody, RectTransform playerBody, Vector3 slotPosition)
		{
			float enemyLeftOffset = -enemyBody.rect.width * 0.5f;
			float playerRight = playerBody.position.x + playerBody.rect.width * 0.5f;
			float impactOverlap = 4f;
			return new Vector3(playerRight - impactOverlap - enemyLeftOffset, slotPosition.y, slotPosition.z);
		}

		sealed class TestBattleController : BattleControllerBase
		{
			public void BindForTest(GameObject panel, Image body, TMP_Text name,
				Image hpFill, TMP_Text hpText, Image marker)
			{
				enemyPanels = new[] { panel };
				enemyBodies = new[] { body };
				enemyNames = new[] { name };
				enemyHpFills = new[] { hpFill };
				enemyHpTexts = new[] { hpText };
				targetMarkers = new[] { marker };
				deadOverlays = new TMP_Text[1];
				enemies = new List<EnemyInfo>
				{
					new EnemyInfo("테스트", 10, 1, Color.white, body != null ? body.sprite : null)
				};
				targetIndex = 0;
			}

			public void RefreshForTest()
			{
				RefreshEnemyVisualLayouts();
			}
		}
	}
}
