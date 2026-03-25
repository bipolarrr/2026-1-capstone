// Assets/Scripts/Dice/DiceRoller.cs
// Rigidbody 기반으로 3D 주사위를 굴리고, 멈춘 후 윗면 눈금(1~6)을 반환한다.

using System.Collections;
using UnityEngine;
using UnityEngine.Events;

public class DiceRoller : MonoBehaviour
{
	[Header("물리 설정")]
	[SerializeField] private float launchHeight    = 1.5f;
	[SerializeField] private float rollForce       = 4f;
	[SerializeField] private float rollTorque      = 12f;
	[SerializeField] private float settleThreshold = 0.08f;

	[Header("결과 이벤트 (int: 1~6)")]
	public UnityEvent<int> onRollComplete;

	private Rigidbody body;
	private bool isRolling;

	// 표준 주사위: 마주 보는 면의 합 = 7
	// 로컬 좌표 기준 각 면의 노말 → 눈금 값
	private static readonly (Vector3 normal, int value)[] FaceMap =
	{
		(Vector3.up,      2),
		(Vector3.down,    5),
		(Vector3.right,   4),
		(Vector3.left,    3),
		(Vector3.forward, 1),
		(Vector3.back,    6),
	};

	private void Awake() => body = GetComponent<Rigidbody>();

	/// <summary>주사위를 굴린다. 이미 굴리는 중이면 무시된다.</summary>
	public void Roll()
	{
		if (isRolling) return;
		StopAllCoroutines();
		StartCoroutine(RollRoutine());
	}

	private IEnumerator RollRoutine()
	{
		isRolling = true;

		// 위치·자세 초기화
		body.linearVelocity  = Vector3.zero;
		body.angularVelocity = Vector3.zero;
		transform.position = new Vector3(Random.Range(-0.25f, 0.25f), launchHeight, 0f);
		transform.rotation = Random.rotation;

		yield return new WaitForFixedUpdate();

		// 무작위 방향 힘 + 회전력 적용
		Vector3 direction = new Vector3(Random.Range(-1f, 1f), -0.5f, 0f).normalized;
		body.AddForce(direction * rollForce, ForceMode.Impulse);
		body.AddTorque(Random.insideUnitSphere * rollTorque, ForceMode.Impulse);

		// 충분히 움직인 뒤 안정될 때까지 대기
		yield return new WaitForSeconds(0.5f);
		while (body.linearVelocity.magnitude  > settleThreshold ||
		       body.angularVelocity.magnitude > settleThreshold)
		{
			yield return new WaitForSeconds(0.05f);
		}

		isRolling = false;
		onRollComplete.Invoke(ReadTopFace());
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
}
