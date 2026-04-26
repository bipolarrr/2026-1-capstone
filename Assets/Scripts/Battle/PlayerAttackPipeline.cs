using System.Collections.Generic;

/// <summary>
/// 플레이어 공격 수치 계산 파이프라인. 상태 없는 순수 static 유틸.
/// DamageCalculator 호출 + SE 선택 + 5칸 패딩만 담당한다.
/// 데미지 적용(TakeDamage/VFX/Flash/로그)은 호출자(BattleSceneController)에 남긴다.
/// </summary>
public static class PlayerAttackPipeline
{
	public readonly struct AttackResolution
	{
		public readonly int damage;
		public readonly string comboName;
		public readonly float shakeIntensity;
		public readonly float splashRatio;
		public readonly int splashDamage;

		public AttackResolution(int damage, string comboName, float shake, float splashRatio)
		{
			this.damage          = damage;
			this.comboName       = comboName;
			this.shakeIntensity  = shake;
			this.splashRatio     = splashRatio;
			this.splashDamage    = UnityEngine.Mathf.FloorToInt(damage * splashRatio);
		}

		public bool HasCombo => !string.IsNullOrEmpty(comboName);
	}

	public static AttackResolution Resolve(int[] values, List<PowerUpType> powerUps)
	{
		var (damage, comboName, shake, splashRatio) = DamageCalculator.Calculate(values, powerUps);
		return new AttackResolution(damage, comboName, shake, splashRatio);
	}

	/// <summary>조합명 → 플레이어 공격 SE 이름.</summary>
	public static string GetPlayerAttackClipName(string comboName)
	{
		if (string.IsNullOrEmpty(comboName))
			return "Player_Attack";
		if (comboName == "YACHT")
			return "Player_Attack_Big";
		if (comboName == "Small Straight")
			return "Player_Attack_Small";
		// Full House, Four of a Kind, Large Straight → Medium
		return "Player_Attack_Medium";
	}

	/// <summary>DamageCalculator용 5칸 배열 패딩. 원본이 5칸 이상이면 그대로 반환.</summary>
	public static int[] PadToFive(int[] values)
	{
		if (values == null) return new int[5];
		if (values.Length >= 5) return values;
		int[] padded = new int[5];
		for (int i = 0; i < values.Length; i++)
			padded[i] = values[i];
		return padded;
	}
}
