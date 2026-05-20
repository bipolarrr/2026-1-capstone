using UnityEngine;

public static class DiceFaceResolver
{
	public const float DefaultTopFaceDotThreshold = 0.996f;

	static readonly (Vector3 normal, int value)[] FaceMap =
	{
		(Vector3.up,      2),
		(Vector3.down,    5),
		(Vector3.right,   4),
		(Vector3.left,    3),
		(Vector3.forward, 1),
		(Vector3.back,    6),
	};

	public static bool TryResolveTopFace(Quaternion rotation, out int face,
		float dotThreshold = DefaultTopFaceDotThreshold)
	{
		face = 0;
		float bestDot = float.NegativeInfinity;

		for (int i = 0; i < FaceMap.Length; i++)
		{
			float dot = Vector3.Dot(rotation * FaceMap[i].normal, Vector3.up);
			if (dot <= bestDot) continue;
			bestDot = dot;
			face = FaceMap[i].value;
		}

		return bestDot >= dotThreshold;
	}

	public static bool TryGetLocalNormalForFace(int face, out Vector3 normal)
	{
		for (int i = 0; i < FaceMap.Length; i++)
		{
			if (FaceMap[i].value != face) continue;
			normal = FaceMap[i].normal;
			return true;
		}

		normal = Vector3.up;
		return false;
	}
}
