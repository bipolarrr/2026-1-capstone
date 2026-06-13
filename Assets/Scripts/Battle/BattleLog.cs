using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;
using System;

/// <summary>
/// 전투 로그 패널. 스크롤 가능한 텍스트 영역에 전투 이벤트를 누적 표시.
/// </summary>
public enum BattleEventPresentation
{
	LogOnly,
	LogAndPopup,
	LogAndAnimation
}

public readonly struct BattleLogEntry
{
	public readonly string Message;
	public readonly BattleEventPresentation Presentation;

	public BattleLogEntry(string message, BattleEventPresentation presentation)
	{
		Message = message;
		Presentation = presentation;
	}
}

public class BattleLog : MonoBehaviour
{
	[SerializeField] TMP_Text logText;
	[SerializeField] ScrollRect scrollRect;

	const int MaxCharacters = 2000;
	readonly List<BattleLogEntry> entries = new List<BattleLogEntry>();

	public event Action<BattleLogEntry> EntryAdded;
	public IReadOnlyList<BattleLogEntry> Entries => entries;

	void Awake()
	{
		if (logText != null)
			logText.text = "";
	}

	public void AddEntry(string message, BattleEventPresentation presentation = BattleEventPresentation.LogOnly)
	{
		if (string.IsNullOrEmpty(message))
			return;

		var entry = new BattleLogEntry(message, presentation);
		entries.Add(entry);
		EntryAdded?.Invoke(entry);

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

		// 새 메시지가 올 때마다 항상 최하단으로.
		if (scrollRect != null && isActiveAndEnabled)
		{
			if (scrollCoroutine != null)
				StopCoroutine(scrollCoroutine);
			scrollCoroutine = StartCoroutine(ScrollToBottom());
		}
	}

	Coroutine scrollCoroutine;

	System.Collections.IEnumerator ScrollToBottom()
	{
		// TMP 텍스트 갱신 → 레이아웃 리빌드 → 스크롤 적용에 2프레임 필요
		yield return null;
		yield return null;
		LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect.content);
		Canvas.ForceUpdateCanvases();
		scrollRect.verticalNormalizedPosition = 0f;
		scrollCoroutine = null;
	}

	public void Clear()
	{
		entries.Clear();
		if (logText != null)
			logText.text = "";
	}

	public string BuildHistoryText()
	{
		var lines = new string[entries.Count];
		for (int i = 0; i < entries.Count; i++)
			lines[i] = entries[i].Message;
		return string.Join("\n", lines);
	}
}
