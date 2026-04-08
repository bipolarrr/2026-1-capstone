using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 전용 주사위 굴림 관리. 별도 물리 아레나에서 rank 개수만큼 주사위를 굴린다.
/// </summary>
public class EnemyDiceRoller : MonoBehaviour
{
	[SerializeField] YachtDie[] enemyDice;
	[SerializeField] Vector3 vaultCenter;

	int settledCount;
	int activeDiceCount;
	Action<EnemyDiceResult> currentCallback;

	void OnDestroy()
	{
		if (enemyDice != null)
		{
			foreach (var die in enemyDice)
			{
				if (die != null)
					die.OnSettled -= HandleSettled;
			}
		}
	}

	/// <summary>
	/// diceCount개 주사위를 굴리고 결과를 콜백으로 반환하는 코루틴.
	/// </summary>
	public Coroutine RollForEnemy(int diceCount, Action<EnemyDiceResult> onComplete)
	{
		return StartCoroutine(RollRoutine(diceCount, onComplete));
	}

	IEnumerator RollRoutine(int diceCount, Action<EnemyDiceResult> onComplete)
	{
		int count = Mathf.Clamp(diceCount, 1, enemyDice.Length);
		activeDiceCount = count;
		settledCount = 0;
		currentCallback = onComplete;

		// 활성/비활성 처리
		for (int i = 0; i < enemyDice.Length; i++)
		{
			if (i < count)
			{
				enemyDice[i].gameObject.SetActive(true);
				enemyDice[i].OnSettled += HandleSettled;
			}
			else
			{
				enemyDice[i].gameObject.SetActive(false);
			}
		}

		// 약간의 딜레이 후 굴림
		yield return new WaitForSeconds(0.2f);

		for (int i = 0; i < count; i++)
			enemyDice[i].Roll();

		// 모든 주사위 정지 대기
		while (settledCount < activeDiceCount)
			yield return null;

		// 이벤트 해제
		for (int i = 0; i < count; i++)
			enemyDice[i].OnSettled -= HandleSettled;

		// 결과 수집
		int[] values = new int[count];
		for (int i = 0; i < count; i++)
			values[i] = enemyDice[i].Result;

		// 정렬된 위치로 이동 (vault 스타일)
		ArrangeDice(count);

		// 족보 판정 (파워업 없음)
		var (_, comboName, _, _) = DamageCalculator.Calculate(
			PadToFive(values), new List<PowerUpType>());

		// 주사위가 4개 미만이면 족보 무효
		bool hasCombo = count >= 4 && !string.IsNullOrEmpty(comboName);
		if (!hasCombo)
			comboName = "";

		float multiplier = EnemyDiceResult.GetMultiplier(comboName);

		var result = new EnemyDiceResult
		{
			values = values,
			comboName = comboName,
			damageMultiplier = multiplier,
			hasCombo = hasCombo
		};

		Debug.Log($"[EnemyDice] Roll complete: [{string.Join(",", values)}] combo=\"{comboName}\" multiplier={multiplier}");
		onComplete?.Invoke(result);
	}

	void HandleSettled(YachtDie die, int result)
	{
		settledCount++;
	}

	/// <summary>정지된 주사위를 vault 스타일로 정렬.</summary>
	void ArrangeDice(int count)
	{
		for (int i = 0; i < count; i++)
		{
			float offset = -((count - 1) * 0.5f) + i;
			Vector3 pos = new Vector3(vaultCenter.x + offset, vaultCenter.y, vaultCenter.z);
			enemyDice[i].transform.position = pos;

			var rb = enemyDice[i].GetComponent<Rigidbody>();
			if (rb != null)
			{
				rb.linearVelocity = Vector3.zero;
				rb.angularVelocity = Vector3.zero;
			}
		}
	}

	/// <summary>
	/// DamageCalculator는 5개 배열을 기대하므로 부족분을 0으로 패딩.
	/// 0은 어떤 족보에도 기여하지 않음.
	/// </summary>
	static int[] PadToFive(int[] values)
	{
		if (values.Length >= 5)
			return values;

		int[] padded = new int[5];
		for (int i = 0; i < values.Length; i++)
			padded[i] = values[i];
		// 나머지는 0 (기본값)
		return padded;
	}
}
