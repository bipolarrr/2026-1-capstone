using System.Collections.Generic;
using UnityEngine;

public enum HeartType
{
	Red,
	Black,
	Blue
}

[System.Serializable]
public struct Heart
{
	public HeartType type;
	public bool isHalf;

	public Heart(HeartType type, bool isHalf = false)
	{
		this.type = type;
		this.isHalf = isHalf;
	}
}

/// <summary>
/// 하트 기반 HP 컨테이너 (Binding of Isaac 스타일).
/// Red = 일반, Black = 소울/아머, Blue = 임시.
/// 데미지 흡수 순서: Blue → Black → Red.
/// </summary>
[System.Serializable]
public class HeartContainer
{
	public const int MaxRedSlots = 5;

	// 각 슬롯은 full(2반칸) 또는 half(1반칸) 하트 하나
	[SerializeField] List<Heart> hearts = new List<Heart>();

	/// <summary>5개 풀 레드 하트로 초기화 (10 반칸).</summary>
	public void Reset()
	{
		hearts.Clear();
		for (int i = 0; i < MaxRedSlots; i++)
			hearts.Add(new Heart(HeartType.Red, false));
	}

	public bool IsAlive
	{
		get
		{
			foreach (var h in hearts)
			{
				// 반칸이라도 남아 있으면 생존
				return true;
			}
			return false;
		}
	}

	public int TotalHalfHearts
	{
		get
		{
			int total = 0;
			foreach (var h in hearts)
				total += h.isHalf ? 1 : 2;
			return total;
		}
	}

	/// <summary>
	/// 데미지를 반칸 단위로 적용. Blue → Black → Red 순서로 소모.
	/// </summary>
	public void TakeDamage(int halfHearts)
	{
		int remaining = halfHearts;

		// Blue 먼저
		remaining = DrainType(HeartType.Blue, remaining);
		// Black 다음
		remaining = DrainType(HeartType.Black, remaining);
		// Red 마지막
		remaining = DrainType(HeartType.Red, remaining);
	}

	int DrainType(HeartType type, int remaining)
	{
		if (remaining <= 0)
			return 0;

		// 뒤에서부터 소모 (마지막 추가된 것부터)
		for (int i = hearts.Count - 1; i >= 0 && remaining > 0; i--)
		{
			if (hearts[i].type != type)
				continue;

			int has = hearts[i].isHalf ? 1 : 2;
			if (remaining >= has)
			{
				remaining -= has;
				hearts.RemoveAt(i);
			}
			else
			{
				// full → half로 전환 (1반칸 남음)
				hearts[i] = new Heart(type, true);
				remaining = 0;
			}
		}
		return remaining;
	}

	/// <summary>레드 하트 회복 (반칸 단위). 기존 반칸 → 풀 전환 후 새 슬롯 추가.</summary>
	public void HealRed(int halfHearts)
	{
		int remaining = halfHearts;

		// 기존 반칸 레드를 풀로 채움
		for (int i = 0; i < hearts.Count && remaining > 0; i++)
		{
			if (hearts[i].type == HeartType.Red && hearts[i].isHalf)
			{
				hearts[i] = new Heart(HeartType.Red, false);
				remaining--;
			}
		}

		// 레드 슬롯 수 확인 후 새 하트 추가
		while (remaining > 0 && CountType(HeartType.Red) < MaxRedSlots)
		{
			if (remaining >= 2)
			{
				hearts.Insert(RedInsertIndex(), new Heart(HeartType.Red, false));
				remaining -= 2;
			}
			else
			{
				hearts.Insert(RedInsertIndex(), new Heart(HeartType.Red, true));
				remaining--;
			}
		}
	}

	/// <summary>블루/블랙 하트 추가 (임시 버프용).</summary>
	public void AddHeart(HeartType type, bool full = true)
	{
		if (type == HeartType.Red)
		{
			if (CountType(HeartType.Red) >= MaxRedSlots)
				return;
			hearts.Insert(RedInsertIndex(), new Heart(HeartType.Red, !full));
		}
		else
		{
			// Black/Blue는 Red 뒤에 추가
			hearts.Add(new Heart(type, !full));
		}
	}

	/// <summary>UI 렌더링용 슬롯 목록 반환. Red → Black → Blue 순서.</summary>
	public List<(HeartType type, bool full)> GetDisplaySlots()
	{
		var slots = new List<(HeartType, bool)>();
		foreach (var h in hearts)
			slots.Add((h.type, !h.isHalf));
		return slots;
	}

	int CountType(HeartType type)
	{
		int count = 0;
		foreach (var h in hearts)
		{
			if (h.type == type)
				count++;
		}
		return count;
	}

	/// <summary>레드 하트 삽입 위치: 마지막 레드 다음.</summary>
	int RedInsertIndex()
	{
		int lastRed = -1;
		for (int i = 0; i < hearts.Count; i++)
		{
			if (hearts[i].type == HeartType.Red)
				lastRed = i;
		}
		return lastRed + 1;
	}
}
