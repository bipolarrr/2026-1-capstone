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
///   BeginPhysicalRoll(frame) → Rigidbody 물리 굴림, 유효 정착 후 윗면으로 Result 확정
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

	[Header("물리 굴림")]
	[SerializeField] float physicalLaunchHeight = 1.8f;
	[SerializeField] float physicalPositionJitter = 0.35f;
	[SerializeField] float physicalForceMin = 4.5f;
	[SerializeField] float physicalForceMax = 7.5f;
	[SerializeField] float physicalTorqueMin = 12f;
	[SerializeField] float physicalTorqueMax = 22f;
	[SerializeField] float settleLinearVelocity = 0.08f;
	[SerializeField] float settleAngularVelocity = 0.15f;
	[SerializeField] float settleStableSeconds = 0.35f;
	[SerializeField] float settleTimeoutSeconds = 7f;
	[SerializeField] float topFaceDotThreshold = DiceFaceResolver.DefaultTopFaceDotThreshold;
	[SerializeField] float penetrationTolerance = 0.035f;
	[SerializeField] float invalidSettleNudgeForce = 0.25f;
	[SerializeField] float invalidSettleNudgeTorque = 0.9f;

	public int  Result      { get; private set; } = 1;
	public bool IsHeld      { get; private set; }
	public bool IsSpinning  { get; private set; }
	public bool IsPhysicsSettled
	{
		get
		{
			if (!TryEnsureBody()) return false;
			return body.linearVelocity.sqrMagnitude <= settleLinearVelocity * settleLinearVelocity
			    && body.angularVelocity.sqrMagnitude <= settleAngularVelocity * settleAngularVelocity;
		}
	}

	public event System.Action<Dice> OnSettled;

	Rigidbody body;
	Collider[] ownColliders;
	Coroutine activeRoutine;
	Coroutine slideRoutine;
	Vector3 homePosition;
	Quaternion rotationBeforeHeld;

	GameObject   outlineObject;
	Material     outlineMaterial;
	bool emphasisActive;

	static readonly Color HoverColor    = new Color(0.30f, 0.70f, 1.00f, 0.85f);
	static readonly Color HeldColor     = new Color(1.00f, 0.15f, 0.15f, 0.90f);
	static readonly Color EmphasisColor = new Color(1.00f, 0.85f, 0.10f, 0.90f);
	static readonly Collider[] OverlapBuffer = new Collider[32];

	void Awake()
	{
		if (!TryEnsureBody()) return;
		body.useGravity  = false;
		if (!body.isKinematic)
		{
			body.linearVelocity  = Vector3.zero;
			body.angularVelocity = Vector3.zero;
		}
		body.isKinematic = true;

		homePosition = transform.position;
		transform.rotation = DiceFaceResolver.ComputeCameraFacingRotationForFace(Result);

		EnsureCollider();
		ownColliders = GetComponentsInChildren<Collider>();
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

		if (outlineBaseMaterial != null)
		{
			outlineMaterial = new Material(outlineBaseMaterial);
		}
		else
		{
			Debug.LogWarning("[Dice] outlineBaseMaterial 미주입 — 아웃라인 비활성화");
			return;
		}

		outlineMaterial.SetFloat("_Surface", 1f);
		outlineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		outlineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		outlineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		outlineMaterial.SetInt("_ZWrite", 0);
		outlineMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
		outlineMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		SetOutlineColor(HoverColor);

		outlineObject = new GameObject("OutlineEdges");
		outlineObject.transform.SetParent(filter.transform, false);
		outlineObject.transform.localPosition = Vector3.zero;
		outlineObject.transform.localRotation = Quaternion.identity;
		outlineObject.transform.localScale = Vector3.one;
		outlineObject.layer = gameObject.layer;

		BuildOutlineEdges(filter.sharedMesh.bounds);
		outlineObject.SetActive(false);
	}

	void BuildOutlineEdges(Bounds bounds)
	{
		Vector3 min = bounds.min * outlineScale;
		Vector3 max = bounds.max * outlineScale;
		Vector3[] corners =
		{
			new Vector3(min.x, min.y, min.z),
			new Vector3(max.x, min.y, min.z),
			new Vector3(max.x, max.y, min.z),
			new Vector3(min.x, max.y, min.z),
			new Vector3(min.x, min.y, max.z),
			new Vector3(max.x, min.y, max.z),
			new Vector3(max.x, max.y, max.z),
			new Vector3(min.x, max.y, max.z),
		};

		int[,] edges =
		{
			{ 0, 1 }, { 1, 2 }, { 2, 3 }, { 3, 0 },
			{ 4, 5 }, { 5, 6 }, { 6, 7 }, { 7, 4 },
			{ 0, 4 }, { 1, 5 }, { 2, 6 }, { 3, 7 },
		};

		float largestExtent = Mathf.Max(bounds.extents.x, Mathf.Max(bounds.extents.y, bounds.extents.z));
		float width = Mathf.Max(0.025f, largestExtent * 0.065f);

		for (int i = 0; i < edges.GetLength(0); i++)
		{
			var edge = new GameObject($"Edge{i:00}");
			edge.transform.SetParent(outlineObject.transform, false);
			edge.layer = gameObject.layer;

			var line = edge.AddComponent<LineRenderer>();
			line.useWorldSpace = false;
			line.positionCount = 2;
			line.SetPosition(0, corners[edges[i, 0]]);
			line.SetPosition(1, corners[edges[i, 1]]);
			line.sharedMaterial = outlineMaterial;
			line.startWidth = width;
			line.endWidth = width;
			line.numCapVertices = 2;
			line.numCornerVertices = 2;
			line.alignment = LineAlignment.View;
			line.textureMode = LineTextureMode.Stretch;
			line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
			line.receiveShadows = false;
		}
	}

	void SetOutlineColor(Color color)
	{
		if (outlineMaterial == null) return;
		if (outlineMaterial.HasProperty("_BaseColor"))
			outlineMaterial.SetColor("_BaseColor", color);
		if (outlineMaterial.HasProperty("_Color"))
			outlineMaterial.SetColor("_Color", color);
	}

	// ─────────────────────────────────────────────────────
	// Public API
	// ─────────────────────────────────────────────────────

	public void SetHovered(bool hovered)
	{
		if (IsHeld) return;
		if (outlineObject != null) outlineObject.SetActive(hovered);
		if (hovered)
			SetOutlineColor(HoverColor);
	}

	public void SetHeld(bool held, Vector3 targetPos)
	{
		CancelSlide();
		CancelRoutine();
		StopPhysicsMotion(makeKinematic: true);

		if (held && !IsHeld)
			rotationBeforeHeld = transform.rotation;

		IsHeld     = held;
		IsSpinning = false;

		if (outlineObject != null) outlineObject.SetActive(held);
		if (held)
			SetOutlineColor(HeldColor);

		// 보관(hold): vault slot으로 이동. 해제(unhold): 호출자가 지정한 슬롯으로 복귀 +
		// 이후 spin의 기준점(homePosition)도 해당 슬롯으로 재설정 — 슬롯은 동적으로 계산된다.
		transform.position = targetPos;
		if (held)
		{
			transform.rotation = DiceFaceResolver.ComputeCameraFacingRotationForFace(Result);
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
		StopPhysicsMotion(makeKinematic: true);
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

	public void BeginPhysicalRoll(Quaternion impulseFrame)
	{
		if (IsHeld) return;
		if (!TryEnsureBody()) return;
		CancelSlide();
		CancelRoutine();

		IsSpinning = true;
		emphasisActive = false;
		if (outlineObject != null) outlineObject.SetActive(false);

		Vector2 jitter = Random.insideUnitCircle * physicalPositionJitter;
		transform.position = homePosition + new Vector3(jitter.x, physicalLaunchHeight, jitter.y);
		transform.rotation = Random.rotationUniform;

		body.isKinematic = false;
		body.useGravity = true;
		body.linearVelocity = Vector3.zero;
		body.angularVelocity = Vector3.zero;
		body.maxAngularVelocity = Mathf.Max(body.maxAngularVelocity, physicalTorqueMax);
		body.WakeUp();

		Vector3 horizontal = impulseFrame * Vector3.forward;
		horizontal.y = 0f;
		if (horizontal.sqrMagnitude < 0.0001f)
		{
			horizontal = Random.onUnitSphere;
			horizontal.y = 0f;
		}
		if (horizontal.sqrMagnitude < 0.0001f)
			horizontal = Vector3.forward;
		horizontal = horizontal.normalized;

		Vector3 force = (horizontal + Vector3.up * Random.Range(0.25f, 0.65f)).normalized
		              * Random.Range(physicalForceMin, physicalForceMax);
		Vector3 torque = (impulseFrame * Random.onUnitSphere).normalized
		               * Random.Range(physicalTorqueMin, physicalTorqueMax);
		body.AddForce(force, ForceMode.Impulse);
		body.AddTorque(torque, ForceMode.Impulse);
	}

	public void ConfigurePhysicalRoll(float launchHeight, float positionJitter,
		float forceMin, float forceMax, float torqueMin, float torqueMax)
	{
		physicalLaunchHeight = Mathf.Max(0.1f, launchHeight);
		physicalPositionJitter = Mathf.Max(0f, positionJitter);
		physicalForceMin = Mathf.Max(0.1f, Mathf.Min(forceMin, forceMax));
		physicalForceMax = Mathf.Max(physicalForceMin, forceMax);
		physicalTorqueMin = Mathf.Max(0.1f, Mathf.Min(torqueMin, torqueMax));
		physicalTorqueMax = Mathf.Max(physicalTorqueMin, torqueMax);
	}

	public IEnumerator WaitForValidSettle(System.Action<bool, int> onComplete)
	{
		float stableTime = 0f;
		float elapsed = 0f;

		while (elapsed < settleTimeoutSeconds)
		{
			elapsed += Time.deltaTime;

			if (IsPhysicsSettled)
			{
				stableTime += Time.deltaTime;
				if (stableTime >= settleStableSeconds)
				{
					bool valid = TryReadValidTopFace(out int face);
					onComplete?.Invoke(valid, valid ? face : 0);
					yield break;
				}
			}
			else
			{
				stableTime = 0f;
			}

			yield return null;
		}

		onComplete?.Invoke(false, 0);
	}

	public bool TryReadValidTopFace(out int face)
	{
		face = 0;
		if (!IsPhysicsSettled) return false;
		if (HasAbnormalPenetration()) return false;
		return DiceFaceResolver.TryResolveTopFace(transform.rotation, out face, topFaceDotThreshold);
	}

	public void FinalizePhysicalRoll(int face)
	{
		Result = Mathf.Clamp(face, 1, 6);
		StopPhysicsMotion(makeKinematic: true);
		FinalizeSettle();
	}

	public void NudgeInvalidSettle(Vector3 fallbackTarget)
	{
		if (!TryEnsureBody()) return;

		CancelSlide();
		CancelRoutine();
		IsSpinning = true;
		emphasisActive = false;
		if (outlineObject != null) outlineObject.SetActive(false);

		body.isKinematic = false;
		body.useGravity = true;
		body.linearVelocity = Vector3.zero;
		body.angularVelocity = Vector3.zero;
		body.WakeUp();

		Vector3 direction = ResolveWallOppositeDirection(fallbackTarget);
		Vector3 force = (direction + Vector3.up * 0.12f).normalized * invalidSettleNudgeForce;
		Vector3 torqueAxis = Vector3.Cross(Vector3.up, direction);
		if (torqueAxis.sqrMagnitude < 0.0001f)
			torqueAxis = Random.onUnitSphere;
		torqueAxis = (torqueAxis.normalized + Random.onUnitSphere * 0.2f).normalized;

		body.AddForce(force, ForceMode.Impulse);
		body.AddTorque(torqueAxis * invalidSettleNudgeTorque, ForceMode.Impulse);
	}

	public void ResetForReroll(Vector3 homePosition)
	{
		SetSpinAnchor(homePosition);
		transform.position = homePosition + Vector3.up * physicalLaunchHeight;
		transform.rotation = Random.rotationUniform;
		StopPhysicsMotion(makeKinematic: true);
		IsSpinning = false;
	}

	public void ForcePhysicalFallback(int face, Vector3 safePosition)
	{
		CancelSlide();
		CancelRoutine();
		SetSpinAnchor(safePosition);
		transform.position = safePosition;
		Result = Mathf.Clamp(face, 1, 6);
		transform.rotation = DiceFaceResolver.ComputeCameraFacingRotationForFace(Result);
		StopPhysicsMotion(makeKinematic: true);
		IsSpinning = true;
		FinalizeSettle();
	}

	/// <summary>디버그/강제 설정: 현재 면만 즉시 갱신 (연출 없음).</summary>
	public void ForceResult(int value)
	{
		StopPhysicsMotion(makeKinematic: true);
		Result = Mathf.Clamp(value, 1, 6);
		transform.rotation = DiceFaceResolver.ComputeCameraFacingRotationForFace(Result);
	}

	/// <summary>굴림 시 복귀 기준점을 재설정 + 즉시 teleport. 외부 배치(적 주사위 rearrangement 등) 후 호출.</summary>
	public void SetHome(Vector3 pos)
	{
		StopPhysicsMotion(makeKinematic: true);
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
					SetOutlineColor(c);
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
		Quaternion targetRot = DiceFaceResolver.ComputeCameraFacingRotationForFace(Result);
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

			transform.rotation = DiceFaceResolver.ComputeCameraFacingRotationForFace(face);

			yield return new WaitForSeconds(interval);
			elapsed += interval;

			float remaining = Mathf.Clamp01(1f - elapsed / duration);
			interval = Mathf.Lerp(0.18f, 0.06f, remaining);
		}

		transform.rotation = DiceFaceResolver.ComputeCameraFacingRotationForFace(Result);
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

	void StopPhysicsMotion(bool makeKinematic)
	{
		if (!TryEnsureBody()) return;

		if (makeKinematic)
		{
			if (!body.isKinematic)
			{
				body.linearVelocity = Vector3.zero;
				body.angularVelocity = Vector3.zero;
			}
			body.useGravity = false;
			body.isKinematic = true;
			return;
		}

		body.isKinematic = false;
		body.useGravity = true;
		body.linearVelocity = Vector3.zero;
		body.angularVelocity = Vector3.zero;
	}

	bool TryEnsureBody()
	{
		if (body != null) return true;
		body = GetComponent<Rigidbody>();
		return body != null;
	}

	bool HasAbnormalPenetration()
	{
		if (!TryGetOwnColliderBounds(out Bounds bounds))
			return false;

		int count = Physics.OverlapBoxNonAlloc(bounds.center, bounds.extents + Vector3.one * 0.02f,
			OverlapBuffer, Quaternion.identity, ~0, QueryTriggerInteraction.Ignore);
		count = ClampOverlapCount(count);

		for (int i = 0; i < count; i++)
		{
			var other = OverlapBuffer[i];
			if (other == null || IsOwnCollider(other)) continue;

			for (int j = 0; j < ownColliders.Length; j++)
			{
				var own = ownColliders[j];
				if (own == null || !own.enabled) continue;
				if (!Physics.ComputePenetration(
					own, own.transform.position, own.transform.rotation,
					other, other.transform.position, other.transform.rotation,
					out _, out float distance))
					continue;

				if (distance > penetrationTolerance)
					return true;
			}
		}

		return false;
	}

	Vector3 ResolveWallOppositeDirection(Vector3 fallbackTarget)
	{
		if (TryResolvePenetrationDirection(out Vector3 direction))
			return direction;
		if (TryResolveNearbyWallDirection(out direction))
			return direction;

		direction = fallbackTarget - transform.position;
		direction.y = 0f;
		if (direction.sqrMagnitude < 0.0001f)
			direction = -transform.forward;
		direction.y = 0f;
		if (direction.sqrMagnitude < 0.0001f)
			direction = Vector3.forward;
		return direction.normalized;
	}

	bool TryResolvePenetrationDirection(out Vector3 direction)
	{
		direction = Vector3.zero;
		if (ownColliders == null || ownColliders.Length == 0)
			ownColliders = GetComponentsInChildren<Collider>();

		for (int i = 0; i < ownColliders.Length; i++)
		{
			var own = ownColliders[i];
			if (own == null || !own.enabled) continue;

			int count = Physics.OverlapBoxNonAlloc(
				own.bounds.center,
				own.bounds.extents + Vector3.one * 0.03f,
				OverlapBuffer,
				Quaternion.identity,
				~0,
				QueryTriggerInteraction.Ignore);
			count = ClampOverlapCount(count);

			for (int j = 0; j < count; j++)
			{
				var other = OverlapBuffer[j];
				if (other == null || IsOwnCollider(other)) continue;
				if (!LooksLikeArenaWall(other)) continue;
				if (!Physics.ComputePenetration(
					own, own.transform.position, own.transform.rotation,
					other, other.transform.position, other.transform.rotation,
					out Vector3 separationDirection, out float distance))
					continue;
				if (distance <= 0f) continue;

				separationDirection.y = 0f;
				if (separationDirection.sqrMagnitude < 0.0001f) continue;

				direction = separationDirection.normalized;
				return true;
			}
		}

		return false;
	}

	bool TryResolveNearbyWallDirection(out Vector3 direction)
	{
		direction = Vector3.zero;
		if (!TryGetOwnColliderBounds(out Bounds bounds))
			return false;

		int count = Physics.OverlapBoxNonAlloc(
			bounds.center,
			bounds.extents + Vector3.one * 0.18f,
			OverlapBuffer,
			Quaternion.identity,
			~0,
			QueryTriggerInteraction.Ignore);
		count = ClampOverlapCount(count);

		float bestSqrDistance = float.PositiveInfinity;
		Vector3 bestDirection = Vector3.zero;
		for (int i = 0; i < count; i++)
		{
			var other = OverlapBuffer[i];
			if (other == null || IsOwnCollider(other)) continue;
			if (!LooksLikeArenaWall(other)) continue;

			Vector3 closest = other.ClosestPoint(bounds.center);
			Vector3 candidate = bounds.center - closest;
			candidate.y = 0f;
			float sqrDistance = candidate.sqrMagnitude;
			if (sqrDistance < 0.0001f || sqrDistance >= bestSqrDistance)
				continue;

			bestSqrDistance = sqrDistance;
			bestDirection = candidate;
		}

		if (bestDirection.sqrMagnitude < 0.0001f)
			return false;

		direction = bestDirection.normalized;
		return true;
	}

	bool TryGetOwnColliderBounds(out Bounds bounds)
	{
		if (ownColliders == null || ownColliders.Length == 0)
			ownColliders = GetComponentsInChildren<Collider>();

		for (int i = 0; i < ownColliders.Length; i++)
		{
			var own = ownColliders[i];
			if (own == null || !own.enabled) continue;

			bounds = own.bounds;
			for (int j = i + 1; j < ownColliders.Length; j++)
			{
				own = ownColliders[j];
				if (own == null || !own.enabled) continue;
				bounds.Encapsulate(own.bounds);
			}
			return true;
		}

		bounds = default;
		return false;
	}

	static int ClampOverlapCount(int count)
	{
		return Mathf.Min(count, OverlapBuffer.Length);
	}

	static bool LooksLikeArenaWall(Collider collider)
	{
		string objectName = collider.gameObject.name;
		return objectName.IndexOf("Wall", System.StringComparison.OrdinalIgnoreCase) >= 0;
	}

	bool IsOwnCollider(Collider candidate)
	{
		if (ownColliders == null) return false;
		for (int i = 0; i < ownColliders.Length; i++)
			if (ownColliders[i] == candidate)
				return true;
		return false;
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

}
