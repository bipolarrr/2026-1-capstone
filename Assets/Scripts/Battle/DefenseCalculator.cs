using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 방어 판정 결과.
/// </summary>
public struct DefenseResult
{
	public bool blocked;
	/// <summary>데미지 감소 비율 (0.0 = 피해 그대로, 1.0 = 완전 방어).</summary>
	public float reductionRate;
	public string description;
}

/// <summary>
/// 플레이어 방어 판정 및 적 데미지 계산. 순수 정적 클래스.
/// </summary>
public static class DefenseCalculator
{
	/// <summary>
	/// 플레이어 주사위로 적 주사위 결과를 방어할 수 있는지 판정.
	/// - 족보 있음: 같은 족보를 만들면 100% 방어
	/// - 족보 없음: 플레이어 주사위 5개 중 적 눈과 일치하는 수에 비례하여 방어
	///   (5개 일치 100%, 4개 80%, 3개 60%, 2개 40%, 1개 20%, 0개 0%)
	/// </summary>
	public static DefenseResult Evaluate(int[] playerDice, EnemyDiceResult enemyResult)
	{
		if (enemyResult == null)
			return new DefenseResult { blocked = false, reductionRate = 0f, description = "적 결과 없음" };

		if (enemyResult.hasCombo)
		{
			// 족보 매칭: 플레이어가 같은 족보를 만들면 방어
			var (_, playerCombo, _, _) = DamageCalculator.Calculate(playerDice, new List<PowerUpType>());
			bool matched = !string.IsNullOrEmpty(playerCombo) && playerCombo == enemyResult.comboName;
			return new DefenseResult
			{
				blocked = matched,
				reductionRate = matched ? 1f : 0f,
				description = matched
					? $"{playerCombo}로 방어 성공!"
					: $"{enemyResult.comboName} 방어 실패..."
			};
		}
		else
		{
			int matchCount = CountMatches(playerDice, enemyResult.values);
			int total = enemyResult.values.Length;
			float rate = total > 0 ? (float)matchCount / total : 0f;
			bool perfect = matchCount >= total;

			string desc;
			if (perfect)
				desc = $"완벽 방어! ({matchCount}/{total} 일치)";
			else if (matchCount > 0)
				desc = $"부분 방어 ({matchCount}/{total} 일치, {Mathf.RoundToInt(rate * 100)}% 감소)";
			else
				desc = "방어 실패...";

			return new DefenseResult
			{
				blocked = perfect,
				reductionRate = rate,
				description = desc
			};
		}
	}

	/// <summary>
	/// 적 주사위 눈 중 플레이어 주사위에 포함되는 개수를 센다.
	/// 같은 눈이 여러 개면 플레이어 쪽 빈도수만큼만 매칭.
	/// </summary>
	static int CountMatches(int[] playerDice, int[] enemyValues)
	{
		int[] playerCounts = new int[7];
		foreach (int v in playerDice)
		{
			if (v >= 1 && v <= 6)
				playerCounts[v]++;
		}

		int matched = 0;
		foreach (int v in enemyValues)
		{
			if (v >= 1 && v <= 6 && playerCounts[v] > 0)
			{
				playerCounts[v]--;
				matched++;
			}
		}
		return matched;
	}

	/// <summary>
	/// 적 공격 데미지 계산 (반칸 단위).
	/// 공식: ceil(rank × 배율)
	/// </summary>
	public static int CalculateEnemyDamage(int rank, float multiplier)
	{
		return Mathf.CeilToInt(rank * multiplier);
	}
}
