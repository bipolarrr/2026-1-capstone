using NUnit.Framework;
using UnityEngine;

namespace BattleTests
{
	public class EnemyAttackPositionResolverTests
	{
		GameObject slotGo;
		GameObject bodyGo;
		GameObject playerGo;

		[TearDown]
		public void TearDown()
		{
			if (playerGo != null) Object.DestroyImmediate(playerGo);
			if (bodyGo != null) Object.DestroyImmediate(bodyGo);
			if (slotGo != null) Object.DestroyImmediate(slotGo);
		}

		[Test]
		public void Melee_StandsInFrontOfPlayer()
		{
			var (slot, body, player) = BuildTransforms();
			var plan = EnemyAttackPositionResolver.Resolve(slot, body, player,
				new MobDef { attackRangeType = EnemyAttackRangeType.Melee });

			Assert.AreEqual(EnemyAttackRangeType.Melee, plan.rangeType);
			AssertVector(new Vector3(500.8f, 10f, 0f), plan.standWorldPosition);
			AssertVector(new Vector3(300f, 10f, 0f), plan.impactWorldPosition);
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

			Vector3 melee = new Vector3(500.8f, 10f, 0f);
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
		public void DefaultProjectileResolvesAsRanged()
		{
			var range = EnemyAttackPositionResolver.ResolveRangeType(
				new MobDef { projectileSpritePath = "Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png" });

			Assert.AreEqual(EnemyAttackRangeType.Ranged, range);
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
	}
}
