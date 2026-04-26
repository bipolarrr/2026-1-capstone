using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Battle scenes share one bottom-third focus area. The input panel stays mounted,
/// while battle text and history log can temporarily cover it.
/// </summary>
public sealed class BattleBottomFocusController : MonoBehaviour
{
	[SerializeField] CanvasGroup inputGroup;
	[SerializeField] CanvasGroup messageGroup;
	[SerializeField] TMP_Text messageText;
	[SerializeField] Button messageAdvanceButton;
	[SerializeField] CanvasGroup historyGroup;
	[SerializeField] TMP_Text historyText;
	[SerializeField] ScrollRect historyScroll;
	[SerializeField] Button logButton;
	[SerializeField] Button closeHistoryButton;
	[SerializeField] BattleLog battleLog;

	readonly Queue<string> messageQueue = new Queue<string>();
	bool historyOpen;

	void Awake()
	{
		if (battleLog != null)
			battleLog.EntryAdded += QueueMessage;
		if (messageAdvanceButton != null)
			messageAdvanceButton.onClick.AddListener(AdvanceMessage);
		if (logButton != null)
			logButton.onClick.AddListener(OpenHistory);
		if (closeHistoryButton != null)
			closeHistoryButton.onClick.AddListener(CloseHistory);
		SetGroup(messageGroup, false, false);
		SetGroup(historyGroup, false, false);
		SetGroup(inputGroup, true, true);
	}

	void OnDestroy()
	{
		if (battleLog != null)
			battleLog.EntryAdded -= QueueMessage;
	}

	public void Bind(BattleLog log)
	{
		if (battleLog == log) return;
		if (battleLog != null)
			battleLog.EntryAdded -= QueueMessage;
		battleLog = log;
		if (battleLog != null)
			battleLog.EntryAdded += QueueMessage;
	}

	public void ShowInput()
	{
		if (historyOpen || messageQueue.Count > 0 || IsGroupVisible(messageGroup))
			return;
		SetGroup(inputGroup, true, true);
	}

	public void QueueMessage(string message)
	{
		if (string.IsNullOrEmpty(message)) return;
		if (historyOpen && historyText != null)
			historyText.text = battleLog != null ? battleLog.BuildHistoryText() : "";
		messageQueue.Enqueue(message);
		if (!historyOpen && !IsGroupVisible(messageGroup))
			ShowNextMessage();
	}

	void ShowNextMessage()
	{
		if (messageQueue.Count == 0)
		{
			SetGroup(messageGroup, false, false);
			SetGroup(inputGroup, true, true);
			return;
		}

		if (messageText != null)
			messageText.text = messageQueue.Dequeue();
		SetGroup(inputGroup, true, false);
		SetGroup(messageGroup, true, true);
	}

	void AdvanceMessage()
	{
		if (historyOpen) return;
		ShowNextMessage();
	}

	void OpenHistory()
	{
		historyOpen = true;
		if (historyText != null)
			historyText.text = battleLog != null ? battleLog.BuildHistoryText() : "";
		SetGroup(messageGroup, false, false);
		SetGroup(inputGroup, true, false);
		SetGroup(historyGroup, true, true);
		if (historyScroll != null)
		{
			Canvas.ForceUpdateCanvases();
			historyScroll.verticalNormalizedPosition = 0f;
		}
	}

	void CloseHistory()
	{
		historyOpen = false;
		SetGroup(historyGroup, false, false);
		if (messageQueue.Count > 0)
			ShowNextMessage();
		else
			SetGroup(inputGroup, true, true);
	}

	static bool IsGroupVisible(CanvasGroup group)
	{
		return group != null && group.alpha > 0.5f;
	}

	static void SetGroup(CanvasGroup group, bool visible, bool interactive)
	{
		if (group == null) return;
		group.alpha = visible ? 1f : 0f;
		group.blocksRaycasts = visible && interactive;
		group.interactable = visible && interactive;
	}
}
