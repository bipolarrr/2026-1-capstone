using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 주사위 — 별도 3D 아레나에서 rank 개수만큼 회전시키고 결과를 반환한다.
/// 렌더링은 EnemyDiceCamera가 RenderTexture로 뽑아 UI 오버레이(RawImage)에 보여줌.
/// </summary>
public class EnemyDiceRoller : MonoBehaviour
{
	[SerializeField] Dice[] enemyDice;
	[SerializeField] Vector3 vaultCenter;

	[SerializeField] float spinDuration = 1f;
	[SerializeField] float snapDuration = 0.18f;
	[SerializeField] float diceSpacing  = 1f;

	int settledCount;
	int activeDiceCount;

	void OnDestroy()
	{
		if (enemyDice == null) return;
		foreach (var d in enemyDice)
			if (d != null) d.OnSettled -= HandleSettled;
	}

	/// <summary>
	/// count개 주사위를 활성화하고 중앙 정렬 배치. 굴리지 않고 정적인 면만 보여줌.
	/// "날아오는" 연출 중 UI 오버레이에 미리 보일 상태를 만든다.
	/// 각 주사위의 home position도 이 자리로 재설정해, 이후 BeginSpin이 같은 자리에서 회전 시작하게 함.
	/// </summary>
	public void PlaceForCount(int count)
	{
		if (enemyDice == null) return;
		count = Mathf.Clamp(count, 1, enemyDice.Length);
		float startX = -((count - 1) * diceSpacing * 0.5f);

		for (int i = 0; i < enemyDice.Length; i++)
		{
			if (enemyDice[i] == null) continue;

			if (i < count)
			{
				enemyDice[i].gameObject.SetActive(true);
				var pos = new Vector3(
					vaultCenter.x + startX + i * diceSpacing,
					vaultCenter.y,
					vaultCenter.z);
				enemyDice[i].SetHome(pos);
				enemyDice[i].ForceResult(DiceRandomizer.Next()); // 비행 중 보여줄 정적 면
			}
			else
			{
				enemyDice[i].gameObject.SetActive(false);
			}
		}
	}

	public Coroutine RollForEnemy(int diceCount, Action<EnemyDiceResult> onComplete)
	{
		return StartCoroutine(RollRoutine(diceCount, onComplete));
	}

	IEnumerator RollRoutine(int diceCount, Action<EnemyDiceResult> onComplete)
	{
		int count = Mathf.Clamp(diceCount, 1, enemyDice.Length);
		activeDiceCount = count;
		settledCount = 0;

		for (int i = 0; i < count; i++)
		{
			enemyDice[i].OnSettled += HandleSettled;
			enemyDice[i].BeginSpin(DiceRandomizer.Next());
		}

		// 족보가 존재 가능한 경우(rank ≥ 4)만 드럼롤 — 그 외는 무음 (향후 던지는 SE 자리).
		bool playBgm = count >= 4;
		if (playBgm) DiceDrumRollAudio.Play();

		yield return new WaitForSeconds(spinDuration);

		for (int i = 0; i < count; i++)
			enemyDice[i].StopToFace(snapDuration);

		while (settledCount < activeDiceCount)
			yield return null;

		if (playBgm) DiceDrumRollAudio.Stop();

		for (int i = 0; i < count; i++)
			enemyDice[i].OnSettled -= HandleSettled;

		int[] values = new int[count];
		for (int i = 0; i < count; i++)
			values[i] = enemyDice[i].Result;

		var (_, comboName, _, _) = DamageCalculator.Calculate(
			PadToFive(values), new List<PowerUpType>());

		bool hasCombo = count >= 4 && !string.IsNullOrEmpty(comboName);
		if (!hasCombo) comboName = "";

		float multiplier = EnemyDiceResult.GetMultiplier(comboName);

		var result = new EnemyDiceResult
		{
			values           = values,
			comboName        = comboName,
			damageMultiplier = multiplier,
			hasCombo         = hasCombo
		};

		Debug.Log($"[EnemyDice] roll complete: [{string.Join(",", values)}] combo=\"{comboName}\"");
		onComplete?.Invoke(result);
	}

	void HandleSettled(Dice die)
	{
		settledCount++;
	}

	static int[] PadToFive(int[] values)
	{
		if (values.Length >= 5) return values;
		int[] padded = new int[5];
		for (int i = 0; i < values.Length; i++) padded[i] = values[i];
		return padded;
	}
}
