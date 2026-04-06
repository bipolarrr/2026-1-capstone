using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class YachtDie : MonoBehaviour
{
	[Header("물리")]
	[SerializeField] private float launchHeight    = 1.2f;
	[SerializeField] private float rollForce       = 3f;
	[SerializeField] private float rollTorque      = 10f;
	[SerializeField] private float settleThreshold = 0.03f;
	[SerializeField] private float settleConfirmTime = 0.3f;

	[Header("아웃라인")]
	[SerializeField] private float outlineScale = 1.12f;
	[SerializeField] private Material outlineBaseMaterial;

	public int  Result    { get; private set; } = 1;
	public bool IsHeld    { get; private set; } = false;
	public bool IsRolling { get; private set; } = false;

	/// <summary>디버그용: 결과값을 강제로 설정한다.</summary>
	public void ForceResult(int value)
	{
		Result = Mathf.Clamp(value, 1, 6);
	}

	/// <summary>디버그용: 결과값을 설정하고 해당 눈이 위를 향하도록 회전한다.</summary>
	public void ForceResultWithRotation(int value)
	{
		Result = Mathf.Clamp(value, 1, 6);

		// FaceMap에서 해당 값의 로컬 노멀을 찾는다
		Vector3 faceNormal = Vector3.up;
		foreach (var (normal, v) in FaceMap)
		{
			if (v == Result)
			{
				faceNormal = normal;
				break;
			}
		}

		// 해당 노멀이 월드 업을 향하도록 회전
		transform.rotation = Quaternion.FromToRotation(faceNormal, Vector3.up);
	}

	/// <summary>멈춘 뒤 호출. (이 주사위, 눈금)</summary>
	public event System.Action<YachtDie, int> OnSettled;

	private Rigidbody    body;
	private MeshRenderer meshRenderer;

	private GameObject   outlineObject;
	private MeshRenderer outlineRenderer;
	private Material     outlineMaterial;

	private static readonly Color HoverColor = new Color(0.3f, 0.7f, 1.0f, 0.85f);
	private static readonly Color HeldColor  = new Color(1.0f, 0.15f, 0.15f, 0.9f);

	private Coroutine moveCoroutine;

	// 홀드 해제 시 복귀할 위치/회전. SetHeld(true) 시점에 캡처.
	private Vector3    positionBeforeHeld;
	private Quaternion rotationBeforeHeld;

	// 표준 주사위 면 맵 (★ 에셋 방향에 따라 조정 필요)
	private static readonly (Vector3 normal, int value)[] FaceMap =
	{
		(Vector3.up,      2),
		(Vector3.down,    5),
		(Vector3.right,   4),
		(Vector3.left,    3),
		(Vector3.forward, 1),
		(Vector3.back,    6),
	};

	private const float FallThreshold = -5f;
	private Vector3 spawnPosition;

	private void Awake()
	{
		body = GetComponent<Rigidbody>();
		meshRenderer = GetComponent<MeshRenderer>();
		if (meshRenderer == null)
			meshRenderer = GetComponentInChildren<MeshRenderer>();

		spawnPosition = transform.position;
		EnsureDiceCollider();
		CreateOutline();
	}

	// IsRolling 중에는 복구 루틴이 이미 진행 중이므로 재진입 방지
	private void FixedUpdate()
	{
		if (!IsRolling && transform.position.y < FallThreshold)
			RecoverFromFall();
	}

	private void RecoverFromFall()
	{
		if (moveCoroutine != null)
			StopCoroutine(moveCoroutine);
		StopAllCoroutines();

		body.linearVelocity = Vector3.zero;
		body.angularVelocity = Vector3.zero;
		body.isKinematic = false;
		body.constraints = RigidbodyConstraints.None;
		transform.position = spawnPosition + Vector3.up * 1.5f;
		transform.rotation = Random.rotation;

		IsHeld = false;
		IsRolling = true;
		StartCoroutine(WaitForSettleAfterRecovery());
	}

	private IEnumerator WaitForSettleAfterRecovery()
	{
		yield return new WaitForSeconds(0.3f);

		float stableTime = 0f;
		while (stableTime < settleConfirmTime)
		{
			yield return new WaitForFixedUpdate();
			bool isStill = body.linearVelocity.magnitude < settleThreshold
				&& body.angularVelocity.magnitude < settleThreshold;
			if (isStill)
				stableTime += Time.fixedDeltaTime;
			else
				stableTime = 0f;
		}

		Result = ReadTopFace();
		IsRolling = false;
		OnSettled?.Invoke(this, Result);
	}

	/// <summary>레이캐스트용 MeshCollider가 있는지 확인하고, 없으면 생성.</summary>
	private void EnsureDiceCollider()
	{
		var collider = GetComponent<MeshCollider>();
		if (collider != null)
		{
			collider.convex = true;
			return;
		}

		var filter = GetComponent<MeshFilter>();
		if (filter == null) filter = GetComponentInChildren<MeshFilter>();
		if (filter == null || filter.sharedMesh == null) return;

		collider = gameObject.AddComponent<MeshCollider>();
		collider.sharedMesh = filter.sharedMesh;
		collider.convex = true;
	}

	private void CreateOutline()
	{
		var meshFilter = GetComponent<MeshFilter>();
		if (meshFilter == null) meshFilter = GetComponentInChildren<MeshFilter>();
		if (meshFilter == null || meshFilter.sharedMesh == null) return;

		outlineObject = new GameObject("HoverOutline");
		outlineObject.transform.SetParent(transform, false);
		outlineObject.transform.localPosition = Vector3.zero;
		outlineObject.transform.localRotation = Quaternion.identity;
		outlineObject.transform.localScale    = Vector3.one * outlineScale;

		// 부모 주사위와 같은 레이어 (DiceCamera에만 렌더)
		outlineObject.layer = gameObject.layer;

		var filter = outlineObject.AddComponent<MeshFilter>();
		filter.sharedMesh = meshFilter.sharedMesh;

		outlineRenderer = outlineObject.AddComponent<MeshRenderer>();

		// 씬 빌더에서 주입된 베이스 머티리얼을 인스턴스화
		if (outlineBaseMaterial != null)
		{
			outlineMaterial = new Material(outlineBaseMaterial);
		}
		else
		{
			// 폴백: 에디터 전용 (빌드에서는 outlineBaseMaterial이 주입되어야 함)
			var shader = Shader.Find("Universal Render Pipeline/Unlit");
			if (shader == null)
			{
				Debug.LogWarning("[YachtDie] URP Unlit 셰이더를 찾을 수 없음 — 아웃라인 비활성화");
				Destroy(outlineObject);
				outlineObject = null;
				return;
			}
			outlineMaterial = new Material(shader);
		}
		outlineMaterial.SetFloat("_Surface", 1f);
		outlineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
		outlineMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
		outlineMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
		outlineMaterial.SetInt("_ZWrite", 0);
		outlineMaterial.SetInt("_Cull", 1); // 1 = Front → 앞면 컬링, 뒷면만 렌더
		outlineMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
		outlineMaterial.SetColor("_BaseColor", HoverColor);

		outlineRenderer.sharedMaterial = outlineMaterial;
		outlineObject.SetActive(false);
	}

	/// <summary>커서가 올라왔을 때 호출 (파란 아웃라인 표시).</summary>
	public void SetHovered(bool hovered)
	{
		if (IsHeld) return;
		if (outlineObject != null)
			outlineObject.SetActive(hovered);
		if (hovered && outlineMaterial != null)
			outlineMaterial.SetColor("_BaseColor", HoverColor);
	}

	/// <summary>보관 상태 전환. targetPos 는 Vault 월드 좌표.</summary>
	public void SetHeld(bool held, Vector3 targetPos)
	{
		if (moveCoroutine != null) StopCoroutine(moveCoroutine);

		body.linearVelocity  = Vector3.zero;
		body.angularVelocity = Vector3.zero;

		if (held && !IsHeld)
		{
			positionBeforeHeld = transform.position;
			rotationBeforeHeld = transform.rotation;
		}

		IsHeld = held;

		if (outlineObject != null)
			outlineObject.SetActive(held);
		if (held && outlineMaterial != null)
			outlineMaterial.SetColor("_BaseColor", HeldColor);

		if (held)
		{
			// 격리 공간(Vault)으로 순간이동 — 현재 결과값의 눈이 위를 향하도록 정렬
			body.isKinematic = true;
			body.constraints = RigidbodyConstraints.FreezeAll;
			transform.position = targetPos;
			AlignToTopFace();
		}
		else
		{
			// 원래 위치로 순간이동 후 물리 복원
			transform.position = positionBeforeHeld;
			transform.rotation = rotationBeforeHeld;
			body.constraints = RigidbodyConstraints.None;
			body.linearVelocity  = Vector3.zero;
			body.angularVelocity = Vector3.zero;
			body.isKinematic = false;
		}
	}

	/// <summary>보관 중이거나 굴리는 중이면 무시.</summary>
	public void Roll()
	{
		if (IsHeld || IsRolling) return;
		if (moveCoroutine != null) StopCoroutine(moveCoroutine);
		StartCoroutine(RollRoutine());
	}

	private IEnumerator RollRoutine()
	{
		IsRolling = true;
		if (outlineObject != null)
			outlineObject.SetActive(false);

		body.isKinematic = false;
		body.linearVelocity  = Vector3.zero;
		body.angularVelocity = Vector3.zero;
		transform.position = transform.position + new Vector3(
			Random.Range(-0.1f, 0.1f), launchHeight, Random.Range(-0.05f, 0.05f));
		transform.rotation = Random.rotation;

		// transform 직접 이동 후 다음 FixedUpdate까지 대기해야 물리 엔진이 새 위치를 인식
		yield return new WaitForFixedUpdate();

		Vector3 direction = new Vector3(Random.Range(-0.6f, 0.6f), -0.4f, Random.Range(-0.2f, 0.2f)).normalized;
		body.AddForce(direction * rollForce, ForceMode.Impulse);
		body.AddTorque(Random.insideUnitSphere * rollTorque, ForceMode.Impulse);

		yield return new WaitForSeconds(0.5f);

		// 임계값 이하 상태가 settleConfirmTime 동안 유지되어야 안정으로 판정
		float stableTime = 0f;
		while (stableTime < settleConfirmTime)
		{
			yield return new WaitForFixedUpdate();
			bool isStill = body.linearVelocity.magnitude < settleThreshold
				&& body.angularVelocity.magnitude < settleThreshold;
			if (isStill)
				stableTime += Time.fixedDeltaTime;
			else
				stableTime = 0f;
		}

		Result = ReadTopFace();
		IsRolling = false;
		OnSettled?.Invoke(this, Result);
	}

	private IEnumerator SmoothMove(Vector3 target, Quaternion targetRotation)
	{
		Vector3    startPosition = transform.position;
		Quaternion startRotation = transform.rotation;
		float t = 0f;

		while (t < 1f)
		{
			t = Mathf.Min(t + Time.deltaTime * 7f, 1f);
			float ease = 1f - Mathf.Pow(1f - t, 3f);
			transform.position = Vector3.Lerp(startPosition, target, ease);
			transform.rotation = Quaternion.Slerp(startRotation, targetRotation, ease);
			yield return null;
		}
	}

	/// <summary>목표 위치로 이동 후 물리를 복원 (가속도 없이 정지 상태로).</summary>
	private IEnumerator SmoothMoveAndRelease(Vector3 target, Quaternion targetRotation)
	{
		Vector3    startPosition = transform.position;
		Quaternion startRotation = transform.rotation;
		float t = 0f;

		while (t < 1f)
		{
			t = Mathf.Min(t + Time.deltaTime * 7f, 1f);
			float ease = 1f - Mathf.Pow(1f - t, 3f);
			transform.position = Vector3.Lerp(startPosition, target, ease);
			transform.rotation = Quaternion.Slerp(startRotation, targetRotation, ease);
			yield return null;
		}

		// 도착 후 물리 복원 — 제약 해제, 속도 0으로 정지 상태 유지
		body.constraints     = RigidbodyConstraints.None;
		body.linearVelocity  = Vector3.zero;
		body.angularVelocity = Vector3.zero;
		body.isKinematic     = false;
	}

	private int ReadTopFace()
	{
		float best   = float.NegativeInfinity;
		int   result = 1;
		foreach (var (normal, value) in FaceMap)
		{
			float dot = Vector3.Dot(transform.TransformDirection(normal), Vector3.up);
			if (dot > best) { best = dot; result = value; }
		}
		return result;
	}

	/// <summary>현재 Result 값의 면이 정확히 위를 향하도록 회전을 정렬한다.</summary>
	private void AlignToTopFace()
	{
		Vector3 faceNormal = Vector3.up;
		foreach (var (normal, value) in FaceMap)
		{
			if (value == Result)
			{
				faceNormal = normal;
				break;
			}
		}
		transform.rotation = Quaternion.FromToRotation(faceNormal, Vector3.up);
	}
}
