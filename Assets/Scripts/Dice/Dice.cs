using System.Collections;
using UnityEngine;

/// <summary>
/// 단일 주사위 시각 컴포넌트. 상태/연출만 담당하고 정지 정책은 모른다 —
/// DiceRollDirector/EnemyDiceRoller가 외부에서 라이프사이클을 명령한다.
///
/// 라이프사이클:
///   BeginSpin(face)  → 무한 회전 (Result 저장)
///   RetargetFace(f)  → 회전 중 Result 교체 (TryBoost 반영)
///   SetEmphasis(b)   → 흔들림 + 아웃라인 맥동 on/off
///   StopToFace(s)    → s초간 Result 면으로 스냅, OnSettled 1회 발사
///   FlickerStop(d)   → d초간 랜덤 플리커 후 Result 면으로 스냅, OnSettled 1회 발사
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class Dice : MonoBehaviour
{
	[Header("회전 모션")]
	[SerializeField] float spinSpeedMin = 720f;
	[SerializeField] float spinSpeedMax = 1260f;
	[SerializeField] float spinHeightOffset = 0.6f;

	[Header("강조 (두구두구)")]
	[SerializeField] float emphasisShakeAmplitude = 0.05f;
	[SerializeField] float emphasisPulseFrequency = 22f;

	[Header("아웃라인")]
	[SerializeField] float outlineScale = 1.12f;
	[SerializeField] Material outlineBaseMaterial;

	public int  Result      { get; private set; } = 1;
	public bool IsHeld      { get; private set; }
	public bool IsSpinning  { get; private set; }

	public event System.Action<Dice> OnSettled;

	Rigidbody body;
	Coroutine activeRoutine;
	Coroutine slideRoutine;
	Vector3 homePosition;
	Quaternion rotationBeforeHeld;

	GameObject   outlineObject;
	MeshRenderer outlineRenderer;
	Material     outlineMaterial;
	bool emphasisActive;

	static readonly Color HoverColor    = new Color(0.30f, 0.70f, 1.00f, 0.85f);
	static readonly Color HeldColor     = new Color(1.00f, 0.15f, 0.15f, 0.90f);
	static readonly Color EmphasisColor = new Color(1.00f, 0.85f, 0.10f, 0.90f);

	// 로컬 노멀 ↔ 눈 값. 주사위 메쉬 방향에 의존하므로 에셋 교체 시 재조정.
	static readonly (Vector3 normal, int value)[] FaceMap =
	{
		(Vector3.up,      2),
		(Vector3.down,    5),
		(Vector3.right,   4),
		(Vector3.left,    3),
		(Vector3.forward, 1),
		(Vector3.back,    6),
	};

	void Awake()
	{
		body = GetComponent<Rigidbody>();
		body.isKinematic = true;
		body.useGravity  = false;
		body.linearVelocity  = Vector3.zero;
		body.angularVelocity = Vector3.zero;

		homePosition = transform.position;

		EnsureCollider();
		CreateOutline();
	}

	void EnsureCollider()
	{
		var existing = GetComponent<MeshCollider>();
		if (existing != null)
		{
			existing.convex = true;
			return;
		}

		var filter = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>();
		if (filter == null || filter.sharedMesh == null) return;

		var collider = gameObject.AddComponent<MeshCollider>();
		collider.sharedMesh = filter.sharedMesh;
		collider.convex = true;
	}

	void CreateOutline()
	{
		var filter = GetComponent<MeshFilter>() ?? GetComponentInChildren<MeshFilter>();
		if (filter == null || filter.sharedMesh == null) return;

		outlineObject = new GameObject("Outline");
		outlineObject.transform.SetParent(transform, false);
		outlineObject.transform.localPosition = Vector3.zero;
		outlineObject.transform.localRotation = Quaternion.identity;
		outlineObject.transform.localScale    = Vector3.one * outlineScale;
		outlineObject.layer = gameObject.layer;

		var outlineFilter = outlineObject.AddComponent<MeshFilter>();
		outlineFilter.sharedMesh = filter.sharedMesh;
		outlineRenderer = outlineObject.AddComponent<MeshRenderer>();

		if (outlineBaseMaterial != null)
		{
			outlineMaterial = new Material(outlineBaseMaterial);
		}
		else
		{
			Debug.LogWarning("[Dice] outlineBaseMaterial 미주입 — 아웃라인 비활성화");
			Destroy(outlineObject);
			outlineObject = null;
			return;
		}

		outlineMaterial.SetFloat("_Surface", 1f);
		outlineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		outlineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		outlineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		outlineMaterial.SetInt("_ZWrite", 0);
		outlineMaterial.SetInt("_Cull", 1);
		outlineMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		outlineMaterial.SetColor("_BaseColor", HoverColor);

		outlineRenderer.sharedMaterial = outlineMaterial;
		outlineObject.SetActive(false);
	}

	// ─────────────────────────────────────────────────────
	// Public API
	// ─────────────────────────────────────────────────────

	public void SetHovered(bool hovered)
	{
		if (IsHeld) return;
		if (outlineObject != null) outlineObject.SetActive(hovered);
		if (hovered && outlineMaterial != null)
			outlineMaterial.SetColor("_BaseColor", HoverColor);
	}

	public void SetHeld(bool held, Vector3 targetPos)
	{
		CancelSlide();
		CancelRoutine();

		if (held && !IsHeld)
			rotationBeforeHeld = transform.rotation;

		IsHeld     = held;
		IsSpinning = false;

		if (outlineObject != null) outlineObject.SetActive(held);
		if (held && outlineMaterial != null)
			outlineMaterial.SetColor("_BaseColor", HeldColor);

		// 보관(hold): vault slot으로 이동. 해제(unhold): 호출자가 지정한 슬롯으로 복귀 +
		// 이후 spin의 기준점(homePosition)도 해당 슬롯으로 재설정 — 슬롯은 동적으로 계산된다.
		transform.position = targetPos;
		if (held)
		{
			transform.rotation = ComputeRotationForFace(Result);
		}
		else
		{
			transform.rotation = rotationBeforeHeld;
			homePosition = targetPos;
		}
	}

	public void BeginSpin(int resolvedFace)
	{
		if (IsHeld) return;
		CancelSlide();
		CancelRoutine();
		Result = Mathf.Clamp(resolvedFace, 1, 6);
		IsSpinning = true;
		emphasisActive = false;
		if (outlineObject != null) outlineObject.SetActive(false);
		transform.position = homePosition + Vector3.up * spinHeightOffset;
		activeRoutine = StartCoroutine(SpinRoutine());
	}

	public void RetargetFace(int face)
	{
		Result = Mathf.Clamp(face, 1, 6);
	}

	public void SetEmphasis(bool on)
	{
		emphasisActive = on;
		if (outlineObject != null)
			outlineObject.SetActive(on && IsSpinning);
	}

	public void StopToFace(float snapDuration)
	{
		if (!IsSpinning) return;
		CancelRoutine();
		activeRoutine = StartCoroutine(StopRoutine(Mathf.Max(0f, snapDuration)));
	}

	public void FlickerStop(float duration)
	{
		if (!IsSpinning) return;
		CancelRoutine();
		activeRoutine = StartCoroutine(FlickerRoutine(Mathf.Max(0.2f, duration)));
	}

	/// <summary>디버그/강제 설정: 현재 면만 즉시 갱신 (연출 없음).</summary>
	public void ForceResult(int value)
	{
		Result = Mathf.Clamp(value, 1, 6);
		transform.rotation = ComputeRotationForFace(Result);
	}

	/// <summary>굴림 시 복귀 기준점을 재설정 + 즉시 teleport. 외부 배치(적 주사위 rearrangement 등) 후 호출.</summary>
	public void SetHome(Vector3 pos)
	{
		homePosition = pos;
		transform.position = pos;
	}

	/// <summary>spin 기준점(homePosition)만 갱신 — transform은 건드리지 않는다.
	/// 이미 원하는 위치로 슬라이드 중인 주사위의 다음 굴림 복귀점을 바꿀 때 사용.</summary>
	public void SetSpinAnchor(Vector3 pos)
	{
		homePosition = pos;
	}

	/// <summary>MeshRenderer를 켜거나 끈다. 저장 슬롯 스프라이트 표시 전환용.</summary>
	public void SetVisible(bool visible)
	{
		foreach (var r in GetComponentsInChildren<MeshRenderer>())
		{
			if (outlineObject != null && r.gameObject == outlineObject) continue;
			r.enabled = visible;
		}
		if (!visible && outlineObject != null) outlineObject.SetActive(false);
	}

	// ─────────────────────────────────────────────────────
	// Routines
	// ─────────────────────────────────────────────────────

	IEnumerator SpinRoutine()
	{
		Vector3 axis  = Random.onUnitSphere;
		float   speed = Random.Range(spinSpeedMin, spinSpeedMax);
		Vector3 spinBasePos = homePosition + Vector3.up * spinHeightOffset;
		float   time = 0f;

		while (IsSpinning)
		{
			transform.Rotate(axis, speed * Time.deltaTime, Space.World);
			if (Random.value < 0.015f)
				axis = Vector3.Slerp(axis, Random.onUnitSphere, 0.35f).normalized;

			if (emphasisActive)
			{
				time += Time.deltaTime;
				float angle  = Random.Range(0f, Mathf.PI * 2f);
				float radius = Random.Range(0f, emphasisShakeAmplitude);
				transform.position = spinBasePos + new Vector3(
					Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);

				if (outlineMaterial != null)
				{
					float pulse = 0.5f + 0.5f * Mathf.Sin(time * emphasisPulseFrequency);
					Color c = EmphasisColor;
					c.a = 0.45f + 0.55f * pulse;
					outlineMaterial.SetColor("_BaseColor", c);
				}
			}
			else if (transform.position != spinBasePos)
			{
				transform.position = spinBasePos;
			}

			yield return null;
		}
	}

	IEnumerator StopRoutine(float snapDuration)
	{
		Quaternion startRot = transform.rotation;
		Quaternion targetRot = ComputeRotationForFace(Result);
		Vector3 basePos = homePosition + Vector3.up * spinHeightOffset;

		if (snapDuration > 0f)
		{
			float t = 0f;
			while (t < snapDuration)
			{
				t += Time.deltaTime;
				float k = Mathf.Clamp01(t / snapDuration);
				float ease = 1f - Mathf.Pow(1f - k, 3f);
				transform.rotation = Quaternion.Slerp(startRot, targetRot, ease);
				yield return null;
			}
		}

		transform.rotation = targetRot;
		transform.position = basePos;
		FinalizeSettle();
	}

	IEnumerator FlickerRoutine(float duration)
	{
		float elapsed = 0f;
		float interval = 0.06f;
		int lastFace = -1;

		while (elapsed < duration)
		{
			int face;
			int guard = 0;
			do { face = Random.Range(1, 7); guard++; }
			while (face == lastFace && guard < 4);
			lastFace = face;

			transform.rotation = ComputeRotationForFace(face);

			yield return new WaitForSeconds(interval);
			elapsed += interval;

			float remaining = Mathf.Clamp01(1f - elapsed / duration);
			interval = Mathf.Lerp(0.18f, 0.06f, remaining);
		}

		transform.rotation = ComputeRotationForFace(Result);
		FinalizeSettle();
	}

	void FinalizeSettle()
	{
		IsSpinning = false;
		emphasisActive = false;
		activeRoutine = null;
		if (outlineObject != null && !IsHeld) outlineObject.SetActive(false);
		OnSettled?.Invoke(this);
	}

	// ─────────────────────────────────────────────────────
	// Slide (정렬용 위치 이동, spin/stop과 독립)
	// ─────────────────────────────────────────────────────

	public void SlideTo(Vector3 target, float duration)
	{
		CancelSlide();
		slideRoutine = StartCoroutine(SlideRoutine(target, duration));
	}

	IEnumerator SlideRoutine(Vector3 target, float duration)
	{
		Vector3 start = transform.position;
		if ((target - start).sqrMagnitude < 0.0001f)
		{
			slideRoutine = null;
			yield break;
		}

		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = t * t * (3f - 2f * t);
			transform.position = Vector3.Lerp(start, target, eased);
			yield return null;
		}
		transform.position = target;
		slideRoutine = null;
	}

	void CancelSlide()
	{
		if (slideRoutine != null)
		{
			StopCoroutine(slideRoutine);
			slideRoutine = null;
		}
	}

	void CancelRoutine()
	{
		if (activeRoutine != null)
		{
			StopCoroutine(activeRoutine);
			activeRoutine = null;
		}
	}

	// DiceCamera Euler(55,0,0) 기준.
	// 카메라 각도 변경 시 아래 두 값도 함께 수정.
	const float CameraAngleDeg = 90f;
	static readonly Vector3 CameraFaceDir = new Vector3(
		0f,
		Mathf.Sin(CameraAngleDeg * Mathf.Deg2Rad),
		-Mathf.Cos(CameraAngleDeg * Mathf.Deg2Rad));

	// CameraFaceDir에 수직인 "위" 방향 = Vector3.up을 CameraFaceDir 평면에 투영
	static readonly Vector3 CameraFaceUp = new Vector3(
		0f,
		Mathf.Cos(CameraAngleDeg * Mathf.Deg2Rad),
		Mathf.Sin(CameraAngleDeg * Mathf.Deg2Rad));

	static Quaternion ComputeRotationForFace(int value)
	{
		Vector3 faceNormal = Vector3.up;
		foreach (var (normal, v) in FaceMap)
		{
			if (v == value) { faceNormal = normal; break; }
		}

		// 1차: 면 법선을 카메라 방향으로 정렬
		Quaternion rot1 = Quaternion.FromToRotation(faceNormal, CameraFaceDir);

		// 2차: roll 보정 — 면 내부의 "위" 방향을 카메라 up에 맞춤
		// faceNormal에 수직인 로컬 up 선택 (faceNormal과 평행하면 forward로 대체)
		Vector3 localUpRaw = Vector3.ProjectOnPlane(Vector3.up, faceNormal);
		Vector3 localUp = localUpRaw.sqrMagnitude > 0.001f
			? localUpRaw.normalized
			: Vector3.ProjectOnPlane(Vector3.forward, faceNormal).normalized;

		// rot1 이후 로컬 up이 향하는 방향 (CameraFaceDir 평면에 투영)
		Vector3 rotatedUp = Vector3.ProjectOnPlane(rot1 * localUp, CameraFaceDir).normalized;
		// CameraFaceDir 축 기준 AngleAxis 사용 → 면 법선 방향 보존 (FromToRotation은 antiparallel 시 임의 축 선택)
		float rollAngle = Vector3.SignedAngle(rotatedUp, CameraFaceUp, CameraFaceDir);
		Quaternion rot2 = Quaternion.AngleAxis(rollAngle, CameraFaceDir);

		return rot2 * rot1;
	}
}
