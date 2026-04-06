using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 전투 로그 패널. 스크롤 가능한 텍스트 영역에 전투 이벤트를 누적 표시.
/// </summary>
public class BattleLog : MonoBehaviour
{
	[SerializeField] TMP_Text logText;
	[SerializeField] ScrollRect scrollRect;

	const int MaxCharacters = 2000;

	void Awake()
	{
		if (logText != null)
			logText.text = "";
	}

	public void AddEntry(string message)
	{
		if (logText == null)
			return;

		if (logText.text.Length > 0)
			logText.text += "\n";
		logText.text += message;

		// 너무 길어지면 앞부분 잘라냄
		if (logText.text.Length > MaxCharacters)
		{
			int cutIdx = logText.text.IndexOf('\n', logText.text.Length - MaxCharacters);
			if (cutIdx >= 0)
				logText.text = logText.text.Substring(cutIdx + 1);
		}

		// 다음 프레임에 스크롤을 맨 아래로
		if (scrollRect != null)
			StartCoroutine(ScrollToBottom());
	}

	System.Collections.IEnumerator ScrollToBottom()
	{
		// TMP 텍스트 갱신 → 레이아웃 리빌드 → 스크롤 적용에 2프레임 필요
		yield return null;
		yield return null;
		Canvas.ForceUpdateCanvases();
		scrollRect.verticalNormalizedPosition = 0f;
	}

	public void Clear()
	{
		if (logText != null)
			logText.text = "";
	}
}
