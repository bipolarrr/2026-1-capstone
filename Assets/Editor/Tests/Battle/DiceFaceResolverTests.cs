using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Battle.Tests
{
	public class DiceFaceResolverTests
	{
		[Test]
		public void TryResolveTopFace_ReturnsFaceWhoseLocalNormalPointsUp()
		{
			for (int face = 1; face <= 6; face++)
			{
				Assert.That(DiceFaceResolver.TryGetLocalNormalForFace(face, out Vector3 normal), Is.True);
				Quaternion rotation = Quaternion.FromToRotation(normal, Vector3.up);

				bool valid = DiceFaceResolver.TryResolveTopFace(rotation, out int resolved);

				Assert.That(valid, Is.True);
				Assert.That(resolved, Is.EqualTo(face));
			}
		}

		[Test]
		public void ComputeCameraFacingRotationForFace_RoundTripsThroughTopFaceResolver()
		{
			for (int face = 1; face <= 6; face++)
			{
				Quaternion rotation = DiceFaceResolver.ComputeCameraFacingRotationForFace(face);

				bool valid = DiceFaceResolver.TryResolveTopFace(rotation, out int resolved);

				Assert.That(valid, Is.True);
				Assert.That(resolved, Is.EqualTo(face));
			}
		}

		[Test]
		public void TryResolveTopFace_ReturnsInvalidWhenBestFaceIsBelowThreshold()
		{
			Assert.That(DiceFaceResolver.TryGetLocalNormalForFace(1, out Vector3 normal), Is.True);
			Quaternion upright = Quaternion.FromToRotation(normal, Vector3.up);
			Quaternion tilted = Quaternion.AngleAxis(6f, Vector3.right) * upright;

			bool valid = DiceFaceResolver.TryResolveTopFace(tilted, out int resolved);

			Assert.That(valid, Is.False);
			Assert.That(resolved, Is.EqualTo(1));
		}

		[Test]
		public void NudgeInvalidSettle_DoesNotThrowWhenNearbyWallQuerySaturates()
		{
			var roots = new System.Collections.Generic.List<GameObject>();
			Dice dice = CreatePhysicsDice(roots);

			for (int i = 0; i < 40; i++)
			{
				float angle = i * Mathf.PI * 2f / 40f;
				var wall = new GameObject($"ArenaWall{i:00}");
				roots.Add(wall);
				wall.transform.position = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.64f;
				var collider = wall.AddComponent<BoxCollider>();
				collider.size = Vector3.one * 0.1f;
			}

			try
			{
				Physics.SyncTransforms();

				Assert.DoesNotThrow(() => dice.NudgeInvalidSettle(Vector3.forward));
			}
			finally
			{
				DestroyAll(roots);
			}
		}

		[Test]
		public void ForceResult_WhenRigidbodyIsAlreadyKinematic_DoesNotLogVelocityWarning()
		{
			var roots = new System.Collections.Generic.List<GameObject>();
			Dice dice = CreatePhysicsDice(roots);

			try
			{
				Assert.DoesNotThrow(() => dice.ForceResult(3));
				LogAssert.NoUnexpectedReceived();
			}
			finally
			{
				DestroyAll(roots);
			}
		}

		[Test]
		public void OverlapPenetrationCheck_DoesNotThrowWhenQuerySaturates()
		{
			var roots = new System.Collections.Generic.List<GameObject>();
			Dice dice = CreatePhysicsDice(roots);
			MethodInfo method = typeof(Dice).GetMethod("HasAbnormalPenetration",
				BindingFlags.Instance | BindingFlags.NonPublic);

			for (int i = 0; i < 40; i++)
			{
				var wall = new GameObject($"OverlapWall{i:00}");
				roots.Add(wall);
				wall.transform.position = Vector3.zero;
				var collider = wall.AddComponent<BoxCollider>();
				collider.size = Vector3.one * 0.2f;
			}

			try
			{
				Physics.SyncTransforms();

				Assert.That(method, Is.Not.Null);
				Assert.DoesNotThrow(() => method.Invoke(dice, null));
			}
			finally
			{
				DestroyAll(roots);
			}
		}

		static Dice CreatePhysicsDice(System.Collections.Generic.List<GameObject> roots)
		{
			var die = new GameObject("TestDice");
			roots.Add(die);
			die.AddComponent<BoxCollider>();
			die.AddComponent<Rigidbody>();
			return die.AddComponent<Dice>();
		}

		static void DestroyAll(System.Collections.Generic.List<GameObject> roots)
		{
			for (int i = roots.Count - 1; i >= 0; i--)
			{
				if (roots[i] != null)
					Object.DestroyImmediate(roots[i]);
			}
		}
	}
}
