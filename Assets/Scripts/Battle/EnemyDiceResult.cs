/// <summary>
/// 적 주사위 굴림 결과 데이터.
/// </summary>
[System.Serializable]
public class EnemyDiceResult
{
	public int[] values;
	public string comboName;        // "" if no combo
	public float damageMultiplier;  // 1, 1.5, 2, 2.5, 3
	public bool hasCombo;

	/// <summary>족보 배율 테이블.</summary>
	public static float GetMultiplier(string comboName)
	{
		switch (comboName)
		{
			case "YACHT":            return 3f;
			case "Four of a Kind":   return 2.5f;
			case "Large Straight":   return 2f;
			case "Full House":       return 1.5f;
			case "Small Straight":   return 1.5f;
			default:                 return 0.5f;
		}
	}
}
