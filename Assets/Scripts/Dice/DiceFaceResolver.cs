using UnityEngine;

public static class DiceFaceResolver
{
	public const float DefaultTopFaceDotThreshold = 0.996f;
	public const float DefaultCameraAngleDeg = 90f;

	public static readonly Vector3 DefaultCameraFaceDir = new Vector3(
		0f,
		Mathf.Sin(DefaultCameraAngleDeg * Mathf.Deg2Rad),
		-Mathf.Cos(DefaultCameraAngleDeg * Mathf.Deg2Rad));

	public static readonly Vector3 DefaultCameraFaceUp = new Vector3(
		0f,
		Mathf.Cos(DefaultCameraAngleDeg * Mathf.Deg2Rad),
		Mathf.Sin(DefaultCameraAngleDeg * Mathf.Deg2Rad));

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

	public static Quaternion ComputeCameraFacingRotationForFace(int face)
	{
		return ComputeRotationForFace(face, DefaultCameraFaceDir, DefaultCameraFaceUp);
	}

	public static Quaternion ComputeRotationForFace(int face, Vector3 faceDirection, Vector3 faceUp)
	{
		if (!TryGetLocalNormalForFace(face, out Vector3 faceNormal))
			faceNormal = Vector3.up;

		if (faceDirection.sqrMagnitude < 0.001f)
			faceDirection = Vector3.up;
		faceDirection.Normalize();

		faceUp = Vector3.ProjectOnPlane(faceUp, faceDirection);
		if (faceUp.sqrMagnitude < 0.001f)
			faceUp = Vector3.ProjectOnPlane(Vector3.forward, faceDirection);
		if (faceUp.sqrMagnitude < 0.001f)
			faceUp = Vector3.ProjectOnPlane(Vector3.right, faceDirection);
		faceUp.Normalize();

		Quaternion alignFace = Quaternion.FromToRotation(faceNormal, faceDirection);

		Vector3 localUpRaw = Vector3.ProjectOnPlane(Vector3.up, faceNormal);
		Vector3 localUp = localUpRaw.sqrMagnitude > 0.001f
			? localUpRaw.normalized
			: Vector3.ProjectOnPlane(Vector3.forward, faceNormal).normalized;

		Vector3 alignedUp = Vector3.ProjectOnPlane(alignFace * localUp, faceDirection);
		if (alignedUp.sqrMagnitude < 0.001f)
			return alignFace;
		alignedUp.Normalize();

		float rollAngle = Vector3.SignedAngle(alignedUp, faceUp, faceDirection);
		Quaternion alignRoll = Quaternion.AngleAxis(rollAngle, faceDirection);
		return alignRoll * alignFace;
	}
}
