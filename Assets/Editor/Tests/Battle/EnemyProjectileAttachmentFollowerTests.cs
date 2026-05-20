using System.Collections;
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
				IEnumerator routine = animations.EnemyProjectileAttack(
					projectileImage,
					shooterRt,
					playerRt,
					playerGo.GetComponent<Image>());

				Assert.IsTrue(routine.MoveNext());
				Vector3 expected = shooterRt.TransformPoint(new Vector3(-42f, 5f, 0f));
				Assert.AreEqual(expected, projectileGo.transform.position);
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
}
