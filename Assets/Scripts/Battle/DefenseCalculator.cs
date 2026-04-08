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
			// 족보 매칭: 플레이어 주사위가 적과 같은 족보를 포함하면 방어
			// (최고 족보가 아니라 해당 족보의 성립 여부를 직접 판정)
			bool matched = PlayerHasCombo(playerDice, enemyResult.comboName);
			return new DefenseResult
			{
				blocked = matched,
				reductionRate = matched ? 1f : 0f,
				description = matched
					? $"{enemyResult.comboName}로 방어 성공!"
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
	/// 플레이어 주사위 5개가 특정 족보를 포함하는지 직접 판정.
	/// DamageCalculator의 우선순위와 무관하게, 해당 족보 조건 자체를 만족하는지 확인.
	/// 예: Large Straight [1,2,3,4,5]는 Small Straight 조건도 충족.
	/// </summary>
	static bool PlayerHasCombo(int[] dice, string comboName)
	{
		int[] counts = new int[7];
		foreach (int v in dice)
			if (v >= 1 && v <= 6)
				counts[v]++;

		int maxCount = 0;
		for (int i = 1; i <= 6; i++)
			if (counts[i] > maxCount)
				maxCount = counts[i];

		int[] sorted = (int[])dice.Clone();
		System.Array.Sort(sorted);

		switch (comboName)
		{
			case "YACHT":
				return maxCount >= 5;
			case "Four of a Kind":
				return maxCount >= 4;
			case "Large Straight":
				return DamageCalculator.HasRun(sorted, 5);
			case "Full House":
				return DamageCalculator.IsFullHouse(counts);
			case "Small Straight":
				return DamageCalculator.HasRun(sorted, 4);
			default:
				return false;
		}
	}

	/// <summary>
	/// 적 주사위 눈 중 플레이어 주사위에 포함되는 개수를 센다.
	/// 같은 눈이 여러 개면 플레이어 쪽 빈도수만큼만 매칭.
	/// </summary>
	public static int CountMatches(int[] playerDice, int[] enemyValues)
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
