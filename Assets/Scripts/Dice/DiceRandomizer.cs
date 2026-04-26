using UnityEngine;

/// <summary>
/// 주사위 결과값을 결정하는 단일 진입점.
/// 물리 시뮬레이션 대신 호출자가 미리 값을 받아 시각 컴포넌트(Dice)에 전달한다.
/// 향후 2D 스프라이트 기반 주사위로 교체해도 이 클래스는 그대로 사용 가능.
/// </summary>
public static class DiceRandomizer
{
	public const int MinFace = 1;
	public const int MaxFace = 6;

	/// <summary>1~6 균등분포 한 개.</summary>
	public static int Next()
	{
		return Random.Range(MinFace, MaxFace + 1);
	}

	/// <summary>지정된 개수만큼 1~6 균등분포 결과를 채워 반환.</summary>
	public static int[] NextMany(int count)
	{
		int[] values = new int[count];
		for (int i = 0; i < count; i++)
			values[i] = Next();
		return values;
	}
}
