using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 디버그 콘솔 UI. 코나미 커맨드(↑↑↓↓←→←→BA)로 토글, Esc로 닫기.
/// 런타임에 자체 Canvas를 생성하므로 씬 빌더에서는 컴포넌트만 추가하면 된다.
/// </summary>
public class DebugConsoleController : MonoBehaviour
{
	GameObject panel;
	TMP_InputField inputField;
	TMP_Text logText;
	bool isOpen;
	readonly List<string> logLines = new List<string>();
	const int MaxLogLines = 30;

	// 코나미 커맨드: ↑↑↓↓←→←→BA
	static readonly Key[] KonamiSequence =
	{
		Key.UpArrow, Key.UpArrow, Key.DownArrow, Key.DownArrow,
		Key.LeftArrow, Key.RightArrow, Key.LeftArrow, Key.RightArrow,
		Key.B, Key.A
	};
	int konamiIndex;
	float konamiResetTimer;
	const float KonamiTimeout = 3f;

	void Start()
	{
		BuildUI();
		panel.SetActive(false);
	}

	void Update()
	{
		var kb = Keyboard.current;
		if (kb == null)
			return;

		if (isOpen && kb.escapeKey.wasPressedThisFrame)
		{
			SetOpen(false);
			return;
		}

		// 콘솔 열려 있을 때 InputField가 포커스 중이면 시퀀스 감지 건너뜀
		if (isOpen)
			return;

		UpdateKonamiSequence(kb);
	}

	void UpdateKonamiSequence(Keyboard kb)
	{
		// 타임아웃: 입력 간격이 너무 길면 리셋
		konamiResetTimer += Time.unscaledDeltaTime;
		if (konamiResetTimer > KonamiTimeout)
			konamiIndex = 0;

		// 아무 키나 눌렸을 때만 검사
		if (!kb.anyKey.wasPressedThisFrame)
			return;

		if (kb[KonamiSequence[konamiIndex]].wasPressedThisFrame)
		{
			konamiIndex++;
			konamiResetTimer = 0f;

			if (konamiIndex >= KonamiSequence.Length)
			{
				konamiIndex = 0;
				SetOpen(true);
			}
		}
		else
		{
			// 틀리면 리셋 (단, 첫 키와 같으면 1부터 재시작)
			konamiIndex = kb[KonamiSequence[0]].wasPressedThisFrame ? 1 : 0;
			konamiResetTimer = 0f;
		}
	}

	void SetOpen(bool open)
	{
		isOpen = open;
		panel.SetActive(open);
		if (open)
		{
			inputField.text = "";
			StartCoroutine(FocusInputField());
		}
		else
		{
			inputField.DeactivateInputField();
		}
	}

	IEnumerator FocusInputField()
	{
		yield return null;
		inputField.ActivateInputField();
		inputField.Select();
	}

	void OnCommandSubmit(string text)
	{
		if (string.IsNullOrWhiteSpace(text))
			return;

		AppendLog($"> {text}");
		string result = DebugCommandProcessor.Execute(text);
		if (!string.IsNullOrEmpty(result))
			AppendLog(result);

		inputField.text = "";
		StartCoroutine(FocusInputField());
	}

	void AppendLog(string msg)
	{
		logLines.Add(msg);
		while (logLines.Count > MaxLogLines)
			logLines.RemoveAt(0);
		logText.text = string.Join("\n", logLines);
	}

	// ── UI 구축 ──

	void BuildUI()
	{
		// 오버레이 캔버스 (sortingOrder 999: 게임 UI 위에 표시)
		var canvasGo = new GameObject("DebugConsoleCanvas");
		canvasGo.transform.SetParent(transform, false);
		var canvas = canvasGo.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 999;
		var scaler = canvasGo.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		canvasGo.AddComponent<GraphicRaycaster>();

		// 패널 (화면 중앙, 720x480)
		panel = new GameObject("DebugPanel");
		panel.transform.SetParent(canvasGo.transform, false);
		var panelImg = panel.AddComponent<Image>();
		panelImg.color = new Color(0.05f, 0.05f, 0.12f, 0.92f);
		panelImg.raycastTarget = true;
		var panelRt = panel.GetComponent<RectTransform>();
		panelRt.anchorMin = new Vector2(0.5f, 0.5f);
		panelRt.anchorMax = new Vector2(0.5f, 0.5f);
		panelRt.pivot = new Vector2(0.5f, 0.5f);
		panelRt.sizeDelta = new Vector2(720, 480);

		BuildTitle();
		BuildLogArea();
		BuildInputField();
	}

	void BuildTitle()
	{
		var go = new GameObject("Title");
		go.transform.SetParent(panel.transform, false);
		var tmp = go.AddComponent<TextMeshProUGUI>();
		tmp.text = "Debug Console  (↑↑↓↓←→←→BA / Esc)";
		tmp.fontSize = 22;
		tmp.color = new Color(0.8f, 0.8f, 1f);
		tmp.alignment = TextAlignmentOptions.Center;
		tmp.fontStyle = FontStyles.Bold;
		tmp.raycastTarget = false;
		tmp.textWrappingMode = TextWrappingModes.NoWrap;
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0f, 1f);
		rt.anchorMax = new Vector2(1f, 1f);
		rt.pivot = new Vector2(0.5f, 1f);
		rt.sizeDelta = new Vector2(0f, 36f);
		rt.anchoredPosition = Vector2.zero;
	}

	void BuildLogArea()
	{
		var bg = new GameObject("LogBg");
		bg.transform.SetParent(panel.transform, false);
		var bgImg = bg.AddComponent<Image>();
		bgImg.color = new Color(0.02f, 0.02f, 0.06f, 0.95f);
		bgImg.raycastTarget = false;
		var bgRt = bg.GetComponent<RectTransform>();
		bgRt.anchorMin = new Vector2(0.02f, 0.13f);
		bgRt.anchorMax = new Vector2(0.98f, 0.92f);
		bgRt.offsetMin = Vector2.zero;
		bgRt.offsetMax = Vector2.zero;

		bg.AddComponent<RectMask2D>();

		var logGo = new GameObject("LogText");
		logGo.transform.SetParent(bg.transform, false);
		logText = logGo.AddComponent<TextMeshProUGUI>();
		logText.text = "";
		logText.fontSize = 18;
		logText.color = new Color(0.75f, 0.95f, 0.75f);
		logText.alignment = TextAlignmentOptions.BottomLeft;
		logText.textWrappingMode = TextWrappingModes.Normal;
		logText.raycastTarget = false;
		var logRt = logGo.GetComponent<RectTransform>();
		logRt.anchorMin = Vector2.zero;
		logRt.anchorMax = Vector2.one;
		logRt.offsetMin = new Vector2(8f, 4f);
		logRt.offsetMax = new Vector2(-8f, -4f);
	}

	void BuildInputField()
	{
		// 입력 필드 배경
		var go = new GameObject("InputField");
		go.transform.SetParent(panel.transform, false);
		var bgImg = go.AddComponent<Image>();
		bgImg.color = new Color(0.10f, 0.10f, 0.18f, 1f);
		var goRt = go.GetComponent<RectTransform>();
		goRt.anchorMin = new Vector2(0.02f, 0.02f);
		goRt.anchorMax = new Vector2(0.98f, 0.11f);
		goRt.offsetMin = Vector2.zero;
		goRt.offsetMax = Vector2.zero;

		// 텍스트 영역 (뷰포트 + 마스크)
		var textArea = new GameObject("Text Area");
		textArea.transform.SetParent(go.transform, false);
		textArea.AddComponent<RectTransform>();
		textArea.AddComponent<RectMask2D>();
		var taRt = textArea.GetComponent<RectTransform>();
		taRt.anchorMin = Vector2.zero;
		taRt.anchorMax = Vector2.one;
		taRt.offsetMin = new Vector2(10f, 2f);
		taRt.offsetMax = new Vector2(-10f, -2f);

		// 플레이스홀더
		var phGo = new GameObject("Placeholder");
		phGo.transform.SetParent(textArea.transform, false);
		var placeholder = phGo.AddComponent<TextMeshProUGUI>();
		placeholder.text = "명령 입력... (/help)";
		placeholder.fontSize = 20;
		placeholder.color = new Color(0.5f, 0.5f, 0.6f, 0.7f);
		placeholder.alignment = TextAlignmentOptions.Left;
		placeholder.textWrappingMode = TextWrappingModes.NoWrap;
		placeholder.raycastTarget = false;
		var phRt = phGo.GetComponent<RectTransform>();
		phRt.anchorMin = Vector2.zero;
		phRt.anchorMax = Vector2.one;
		phRt.offsetMin = Vector2.zero;
		phRt.offsetMax = Vector2.zero;

		// 입력 텍스트
		var txtGo = new GameObject("Text");
		txtGo.transform.SetParent(textArea.transform, false);
		var textComp = txtGo.AddComponent<TextMeshProUGUI>();
		textComp.text = "";
		textComp.fontSize = 20;
		textComp.color = Color.white;
		textComp.alignment = TextAlignmentOptions.Left;
		textComp.textWrappingMode = TextWrappingModes.NoWrap;
		textComp.raycastTarget = false;
		var txtRt = txtGo.GetComponent<RectTransform>();
		txtRt.anchorMin = Vector2.zero;
		txtRt.anchorMax = Vector2.one;
		txtRt.offsetMin = Vector2.zero;
		txtRt.offsetMax = Vector2.zero;

		// TMP_InputField 컴포넌트
		inputField = go.AddComponent<TMP_InputField>();
		inputField.textViewport = taRt;
		inputField.textComponent = textComp;
		inputField.placeholder = placeholder;
		inputField.lineType = TMP_InputField.LineType.SingleLine;
		inputField.onSubmit.AddListener(OnCommandSubmit);
	}
}
