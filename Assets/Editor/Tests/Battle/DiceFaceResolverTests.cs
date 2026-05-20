using NUnit.Framework;
using UnityEngine;

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
		public void TryResolveTopFace_ReturnsInvalidWhenBestFaceIsBelowThreshold()
		{
			Assert.That(DiceFaceResolver.TryGetLocalNormalForFace(1, out Vector3 normal), Is.True);
			Quaternion upright = Quaternion.FromToRotation(normal, Vector3.up);
			Quaternion tilted = Quaternion.AngleAxis(6f, Vector3.right) * upright;

			bool valid = DiceFaceResolver.TryResolveTopFace(tilted, out int resolved);

			Assert.That(valid, Is.False);
			Assert.That(resolved, Is.EqualTo(1));
		}
	}
}
