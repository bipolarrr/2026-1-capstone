using System.Collections.Generic;

/// <summary>
/// 주사위 콤보 판정 및 데미지 계산. 순수 정적 클래스 — 상태 없음.
/// </summary>
public static class DamageCalculator
{
	/// <summary>
	/// 족보 발동 시 splashRatio=0.5 (50% 광역), 족보 없을 시 splashRatio=0 (단일 타격).
	/// </summary>
	public static (int damage, string comboName, float shakeIntensity, float splashRatio) Calculate(int[] values, List<PowerUpType> powerUps)
	{
		int[] counts = new int[7];
		foreach (int v in values)
			counts[v]++;

		int maxCount = 0;
		for (int i = 1; i <= 6; i++)
		{
			if (counts[i] > maxCount)
				maxCount = counts[i];
		}

		int[] sorted = (int[])values.Clone();
		System.Array.Sort(sorted);

		string comboName;
		int baseDamage;
		float shake;
		bool isCombo;

		if (maxCount == 5)
		{
			comboName = "YACHT";
			baseDamage = 50;
			shake = 25f;
			isCombo = true;
		}
		else if (maxCount == 4)
		{
			comboName = "Four of a Kind";
			baseDamage = 40;
			shake = 18f;
			isCombo = true;
		}
		else if (maxCount == 1 && HasRun(sorted, 5))
		{
			comboName = "Large Straight";
			baseDamage = sorted[0] == 2 ? 35 : 30;
			shake = 14f;
			isCombo = true;
		}
		else if (IsFullHouse(counts))
		{
			comboName = "Full House";
			baseDamage = 25;
			shake = 10f;
			isCombo = true;
		}
		else if (HasRun(sorted, 4))
		{
			comboName = "Small Straight";
			baseDamage = 20;
			shake = 7f;
			isCombo = true;
		}
		else
		{
			comboName = "";
			baseDamage = 0;
			foreach (int v in values)
				baseDamage += v;
			shake = 0f;
			isCombo = false;
		}

		int finalDamage = ApplyPowerUps(baseDamage, comboName, values, powerUps);
		float splashRatio = isCombo ? 0.5f : 0f;
		return (finalDamage, comboName, shake, splashRatio);
	}

	public static int ApplyPowerUps(int baseDamage, string comboName, int[] values, List<PowerUpType> powerUps)
	{
		int damage = baseDamage;

		// 올인 전략: 족보 있으면 절반, 없으면 2배
		if (powerUps.Contains(PowerUpType.AllOrNothing))
		{
			if (!string.IsNullOrEmpty(comboName))
				damage = damage / 2;
			else
				damage = damage * 2;
		}

		// 홀짝 특화: 전부 홀수 또는 전부 짝수면 2배
		if (powerUps.Contains(PowerUpType.OddEvenDouble))
		{
			bool allOdd = true, allEven = true;
			foreach (int v in values)
			{
				if (v % 2 == 0) allOdd = false;
				if (v % 2 == 1) allEven = false;
			}
			if (allOdd || allEven)
				damage *= 2;
		}

		return damage;
	}

	public static bool HasRun(int[] sorted, int length)
	{
		HashSet<int> unique = new HashSet<int>(sorted);
		List<int> distinct = new List<int>(unique);
		distinct.Sort();

		int run = 1;
		for (int i = 1; i < distinct.Count; i++)
		{
			if (distinct[i] == distinct[i - 1] + 1)
			{
				run++;
				if (run >= length)
					return true;
			}
			else
			{
				run = 1;
			}
		}
		return run >= length;
	}

	public static bool IsFullHouse(int[] counts)
	{
		bool hasThree = false, hasTwo = false;
		for (int i = 1; i <= 6; i++)
		{
			if (counts[i] == 3) hasThree = true;
			if (counts[i] == 2) hasTwo = true;
		}
		return hasThree && hasTwo;
	}
}
