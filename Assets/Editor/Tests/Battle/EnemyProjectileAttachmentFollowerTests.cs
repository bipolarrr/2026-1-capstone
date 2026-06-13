using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BattleTests
{
	public class EnemyProjectileAttachmentFollowerTests
	{
		[Test]
		public void ApplyPose_ChangesPositionRotationAndScaleAcrossFrameProgress()
		{
			var parentGo = new GameObject("Parent", typeof(RectTransform));
			var projectileGo = new GameObject("Projectile", typeof(RectTransform), typeof(Image), typeof(EnemyProjectileAttachmentFollower));
			try
			{
				var parentRt = parentGo.GetComponent<RectTransform>();
				parentRt.sizeDelta = new Vector2(200f, 100f);
				projectileGo.transform.SetParent(parentGo.transform, false);

				var follower = projectileGo.GetComponent<EnemyProjectileAttachmentFollower>();
				follower.ApplyPose(0f);
				Vector2 pos0 = projectileGo.GetComponent<RectTransform>().anchoredPosition;
				float rot0 = projectileGo.transform.localEulerAngles.z;
				float scale0 = projectileGo.transform.localScale.x;

				follower.ApplyPose(0.5f);
				Vector2 posHalf = projectileGo.GetComponent<RectTransform>().anchoredPosition;

				follower.ApplyPose(1f);
				Vector2 pos1 = projectileGo.GetComponent<RectTransform>().anchoredPosition;
				float rot1 = projectileGo.transform.localEulerAngles.z;
				float scale1 = projectileGo.transform.localScale.x;

				Assert.AreNotEqual(pos0, posHalf);
				Assert.AreNotEqual(posHalf, pos1);
				Assert.AreNotEqual(rot0, rot1);
				Assert.AreNotEqual(scale0, scale1);
			}
			finally
			{
				Object.DestroyImmediate(projectileGo);
				Object.DestroyImmediate(parentGo);
			}
		}

		[Test]
		public void EnemyProjectileAttack_WithoutFollower_UsesShooterBodyFallbackStart()
		{
			var animationGo = new GameObject("Animation", typeof(BattleAnimations));
			var shooterGo = new GameObject("Shooter", typeof(RectTransform));
			var playerGo = new GameObject("Player", typeof(RectTransform), typeof(Image));
			var projectileGo = new GameObject("Projectile", typeof(RectTransform), typeof(Image));
			try
			{
				var shooterRt = shooterGo.GetComponent<RectTransform>();
				shooterRt.sizeDelta = new Vector2(100f, 100f);
				var playerRt = playerGo.GetComponent<RectTransform>();
				playerRt.sizeDelta = new Vector2(100f, 100f);
				playerRt.position = new Vector3(300f, 0f, 0f);

				var animations = animationGo.GetComponent<BattleAnimations>();
				var projectileImage = projectileGo.GetComponent<Image>();
				Vector3 expected = shooterRt.TransformPoint(new Vector3(-42f, 5f, 0f));
				IEnumerator routine = animations.EnemyProjectileAttack(
					projectileImage,
					shooterRt,
					playerRt,
					playerGo.GetComponent<Image>(),
					duration: 1000000f);

				Assert.IsTrue(routine.MoveNext());
				Assert.That(Vector3.Distance(expected, projectileGo.transform.position), Is.LessThan(0.001f));
			}
			finally
			{
				Object.DestroyImmediate(projectileGo);
				Object.DestroyImmediate(playerGo);
				Object.DestroyImmediate(shooterGo);
				Object.DestroyImmediate(animationGo);
			}
		}
	}

	public class PlayerAttackAnimatorHandAttachmentTests
	{
		[Test]
		public void ResolveHandAttachOffset_InterpolatesBetweenHandFrameKeys()
		{
			var value = ResolveHandAttachOffset(
				0.55f,
				0f,
				new[] { 0.50f, 0.60f },
				new[] { new Vector2(10f, 20f), new Vector2(30f, 60f) },
				Vector2.zero,
				Vector2.zero);

			Assert.That(value.x, Is.EqualTo(20f).Within(0.001f));
			Assert.That(value.y, Is.EqualTo(40f).Within(0.001f));
		}

		[Test]
		public void ResolveHandAttachOffset_ClampsToFirstAndLastFrameKeys()
		{
			float[] times = { 0.50f, 0.60f };
			Vector2[] offsets =
			{
				new Vector2(10f, 20f),
				new Vector2(30f, 60f)
			};

			Vector2 before = ResolveHandAttachOffset(0.10f, 0f, times, offsets, Vector2.zero, Vector2.zero);
			Vector2 after = ResolveHandAttachOffset(0.90f, 0f, times, offsets, Vector2.zero, Vector2.zero);

			Assert.That(before.x, Is.EqualTo(10f).Within(0.001f));
			Assert.That(before.y, Is.EqualTo(20f).Within(0.001f));
			Assert.That(after.x, Is.EqualTo(30f).Within(0.001f));
			Assert.That(after.y, Is.EqualTo(60f).Within(0.001f));
		}

		[Test]
		public void ResolveHandAttachOffset_InvalidKeysUseFallbackOffsets()
		{
			var value = ResolveHandAttachOffset(
				0.55f,
				0.25f,
				new[] { 0.50f },
				new[] { new Vector2(10f, 20f) },
				new Vector2(0f, 0f),
				new Vector2(100f, 200f));

			Assert.That(value.x, Is.EqualTo(25f).Within(0.001f));
			Assert.That(value.y, Is.EqualTo(50f).Within(0.001f));
		}

		[Test]
		public void ResolveEffectiveLaunchRatio_DoesNotLaunchBeforeLastHandKey()
		{
			float[] times = { 0.507f, 0.534f, 0.562f, 0.589f };
			Vector2[] offsets =
			{
				new Vector2(50f, 94f),
				new Vector2(20f, 84f),
				new Vector2(-32f, 110f),
				new Vector2(-28f, 132f)
			};

			float visible = ResolveEffectiveVisibleRatio(0.02f, times, offsets);
			float launch = ResolveEffectiveLaunchRatio(visible, 0.32f, times, offsets);

			Assert.That(visible, Is.EqualTo(0.507f).Within(0.001f));
			Assert.That(launch, Is.GreaterThanOrEqualTo(0.598f));
		}

		static Vector2 ResolveHandAttachOffset(float sequenceT,
			float fallbackAttachT,
			float[] normalizedTimes,
			Vector2[] offsets,
			Vector2 fallbackStart,
			Vector2 fallbackEnd)
		{
			var method = typeof(PlayerAttackAnimator).GetMethod("ResolveHandAttachOffset",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(method);
			return (Vector2)method.Invoke(null, new object[]
			{
				sequenceT,
				fallbackAttachT,
				normalizedTimes,
				offsets,
				fallbackStart,
				fallbackEnd
			});
		}

		static float ResolveEffectiveVisibleRatio(float configuredVisible,
			float[] normalizedTimes,
			Vector2[] offsets)
		{
			var method = typeof(PlayerAttackAnimator).GetMethod("ResolveEffectiveVisibleRatio",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(method);
			return (float)method.Invoke(null, new object[] { configuredVisible, normalizedTimes, offsets });
		}

		static float ResolveEffectiveLaunchRatio(float visibleT,
			float configuredLaunch,
			float[] normalizedTimes,
			Vector2[] offsets)
		{
			var method = typeof(PlayerAttackAnimator).GetMethod("ResolveEffectiveLaunchRatio",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.IsNotNull(method);
			return (float)method.Invoke(null, new object[] { visibleT, configuredLaunch, normalizedTimes, offsets });
		}
	}
}
