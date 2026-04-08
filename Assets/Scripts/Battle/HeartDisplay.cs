using UnityEngine;
using TMPro;

/// <summary>
/// HeartContainer를 TMP 텍스트로 렌더링.
/// 임시 에셋: 컬러 하트 이모지(♥/♡)로 표시.
/// Red=빨강, Black=검정, Blue=하늘색.
/// </summary>
public class HeartDisplay : MonoBehaviour
{
	[SerializeField] TMP_Text heartText;

	const string RedFull   = "<color=#FF2222>●</color>";
	const string RedHalf   = "<color=#FF2222>◐</color>";
	const string RedEmpty  = "<color=#FF2222>○</color>";
	const string BlackFull = "<color=#222222>●</color>";
	const string BlackHalf = "<color=#222222>◐</color>";
	const string BlueFull  = "<color=#66CCFF>●</color>";
	const string BlueHalf  = "<color=#66CCFF>◐</color>";

	public void Refresh(HeartContainer hearts)
	{
		if (heartText == null)
			return;

		if (hearts == null)
		{
			heartText.text = "";
			return;
		}

		var slots = hearts.GetDisplaySlots();
		var sb = new System.Text.StringBuilder();

		foreach (var (type, full) in slots)
		{
			switch (type)
			{
				case HeartType.Red:
					sb.Append(full ? RedFull : RedHalf);
					break;
				case HeartType.Black:
					sb.Append(full ? BlackFull : BlackHalf);
					break;
				case HeartType.Blue:
					sb.Append(full ? BlueFull : BlueHalf);
					break;
			}
			sb.Append(" ");
		}

		// 빈 레드 슬롯 표시 (최대 5칸 기준)
		int redCount = 0;
		foreach (var (type, _) in slots)
			if (type == HeartType.Red) redCount++;
		for (int i = redCount; i < HeartContainer.MaxRedSlots; i++)
			sb.Append(RedEmpty + " ");

		heartText.text = sb.ToString().TrimEnd();
	}
}
