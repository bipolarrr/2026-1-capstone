// Assets/Editor/MainMenuSceneBuilder.cs
// Unity 메뉴 → Tools → Build MainMenu Scene 으로 씬을 자동 생성합니다.
// 씬이 이미 존재하면 덮어쓸지 물어봅니다.

using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

public static class MainMenuSceneBuilder
{
	private const string ScenePath = "Assets/Scenes/MainMenu.unity";

	[MenuItem("Tools/Build MainMenu Scene")]
	public static void Build()
	{
		if (File.Exists(ScenePath))
		{
			bool overwrite = EditorUtility.DisplayDialog(
				"MainMenu 씬 생성",
				$"{ScenePath} 가 이미 존재합니다. 덮어쓰시겠습니까?",
				"덮어쓰기", "취소");
			if (!overwrite)
				return;
		}

		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		// ── 카메라 ──────────────────────────────────────────────────
		var camera = new GameObject("MainCamera");
		camera.tag = "MainCamera";
		var cameraComponent = camera.AddComponent<Camera>();
		cameraComponent.clearFlags = CameraClearFlags.SolidColor;
		cameraComponent.backgroundColor = new Color(0.09f, 0.09f, 0.15f, 1f);
		cameraComponent.orthographic = true;
		cameraComponent.orthographicSize = 5f;
		camera.transform.position = new Vector3(0, 0, -10);
		camera.AddComponent<AudioListener>();

		// ── Canvas ──────────────────────────────────────────────────
		var canvasObject = new GameObject("Canvas");
		var canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;

		var scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;

		canvasObject.AddComponent<GraphicRaycaster>();

		// ── EventSystem ──────────────────────────────────────────────
		var eventSystem = new GameObject("EventSystem");
		eventSystem.AddComponent<EventSystem>();
		eventSystem.AddComponent<InputSystemUIInputModule>();

		// ── MenuRoot (MainMenuController + ButtonHandler 붙이는 곳) ──
		var menuRoot = new GameObject("MenuRoot");

		// ── Background ──────────────────────────────────────────────
		var background = CreateUIPanel(canvasObject.transform, "Background", new Color(0.06f, 0.07f, 0.13f, 1f));
		SetStretch(background);

		// 배경 그라디언트 느낌 — 위쪽 반을 살짝 밝게
		var topGradient = CreateUIPanel(background.transform, "BackgroundTopGradient", new Color(0.12f, 0.13f, 0.22f, 0.5f));
		var topGradientRect = topGradient.GetComponent<RectTransform>();
		topGradientRect.anchorMin = new Vector2(0, 0.5f);
		topGradientRect.anchorMax = new Vector2(1, 1f);
		topGradientRect.offsetMin = Vector2.zero;
		topGradientRect.offsetMax = Vector2.zero;

		// 배경 장식선 2개 (수평)
		CreateDecorationLine(background.transform, "DecorationLineTop", new Vector2(0.72f, 0f), new Color(0.3f, 0.5f, 0.9f, 0.15f));
		CreateDecorationLine(background.transform, "DecorationLineBottom", new Vector2(0.28f, 0f), new Color(0.3f, 0.5f, 0.9f, 0.10f));

		// ── LogoGroup ───────────────────────────────────────────────
		var logoGroup = new GameObject("LogoGroup");
		var logoGroupRect = logoGroup.AddComponent<RectTransform>();
		logoGroupRect.SetParent(canvasObject.transform, false);
		// 좌측 중앙보다 살짝 위
		logoGroupRect.anchorMin = new Vector2(0.04f, 0.62f);
		logoGroupRect.anchorMax = new Vector2(0.48f, 0.80f);
		logoGroupRect.offsetMin = Vector2.zero;
		logoGroupRect.offsetMax = Vector2.zero;
		var logoCanvasGroup = logoGroup.AddComponent<CanvasGroup>();

		// 로고 텍스트 (placeholder)
		var logoTextObject = CreateTMPText(logoGroup.transform, "LogoText",
			"마작 홀덤 주사위 디펜스", 100, FontStyles.Bold,
			new Color(0.95f, 0.95f, 1f, 1f));
		SetStretch(logoTextObject);
		var logoText = logoTextObject.GetComponent<TMP_Text>();
		logoText.alignment = TextAlignmentOptions.MidlineLeft;

		// 로고 아래 부제 텍스트
		var subTitleObject = CreateTMPText(logoGroup.transform, "SubTitleText",
			"로그라이트 덱빌딩 요소 함유", 50, FontStyles.Italic,
			new Color(0.7f, 0.75f, 1f, 0.8f));
		SetStretch(subTitleObject);
		var subTitle = subTitleObject.GetComponent<TMP_Text>();
		subTitle.alignment = TextAlignmentOptions.MidlineLeft;
		var subTitleRect = subTitleObject.GetComponent<RectTransform>();
		subTitleRect.anchorMin = new Vector2(0, -0.55f);
		subTitleRect.anchorMax = new Vector2(1, -0.05f);
		subTitleRect.offsetMin = Vector2.zero;
		subTitleRect.offsetMax = Vector2.zero;

		// ── MenuButtons ─────────────────────────────────────────────
		var menuButtons = new GameObject("MenuButtons");
		var menuButtonsRect = menuButtons.AddComponent<RectTransform>();
		menuButtonsRect.SetParent(canvasObject.transform, false);
		// 좌측, 중단~하단 사이
		menuButtonsRect.anchorMin = new Vector2(0.04f, 0.10f);
		menuButtonsRect.anchorMax = new Vector2(0.30f, 0.44f);
		menuButtonsRect.offsetMin = Vector2.zero;
		menuButtonsRect.offsetMax = Vector2.zero;
		var menuButtonsCanvasGroup = menuButtons.AddComponent<CanvasGroup>();

		var verticalLayout = menuButtons.AddComponent<VerticalLayoutGroup>();
		verticalLayout.spacing = 16;
		verticalLayout.childControlHeight = true;
		verticalLayout.childControlWidth = true;
		verticalLayout.childForceExpandHeight = true;
		verticalLayout.childForceExpandWidth = true;

		var playButton = CreateMenuButton(menuButtons.transform, "PlayButton", "▷  Play");
		var settingsButton = CreateMenuButton(menuButtons.transform, "SettingsButton", "⚙  Settings");
		var creditsButton = CreateMenuButton(menuButtons.transform, "CreditsButton", "✦  Credits");

		// ── CharacterAnchor ─────────────────────────────────────────
		var characterAnchor = new GameObject("CharacterAnchor");
		var characterAnchorRect = characterAnchor.AddComponent<RectTransform>();
		characterAnchorRect.SetParent(canvasObject.transform, false);
		characterAnchorRect.SetSiblingIndex(1); // Background 다음, LogoGroup 앞 → 제목 뒤에 렌더링
		characterAnchorRect.anchorMin = new Vector2(0.58f, 0.05f);
		characterAnchorRect.anchorMax = new Vector2(0.92f, 0.92f);
		characterAnchorRect.offsetMin = Vector2.zero;
		characterAnchorRect.offsetMax = Vector2.zero;
		var characterAnchorCanvasGroup = characterAnchor.AddComponent<CanvasGroup>();

		// Character placeholder (단순 이미지 패널)
		var character = new GameObject("Character");
		var characterRect = character.AddComponent<RectTransform>();
		characterRect.SetParent(characterAnchor.transform, false);
		characterRect.anchorMin = new Vector2(0.15f, 0.05f);
		characterRect.anchorMax = new Vector2(0.85f, 0.95f);
		characterRect.offsetMin = Vector2.zero;
		characterRect.offsetMax = Vector2.zero;

		// 캐릭터 몸통 (둥근 사각형 느낌, 파란 계열)
		var body = CreateUIPanel(character.transform, "Body", new Color(0.25f, 0.38f, 0.75f, 1f));
		SetStretch(body);

		// 캐릭터 얼굴 (상단 30%)
		var face = CreateUIPanel(character.transform, "Face", new Color(0.95f, 0.85f, 0.7f, 1f));
		var faceRect = face.GetComponent<RectTransform>();
		faceRect.anchorMin = new Vector2(0.25f, 0.65f);
		faceRect.anchorMax = new Vector2(0.75f, 0.95f);
		faceRect.offsetMin = Vector2.zero;
		faceRect.offsetMax = Vector2.zero;

		// 눈
		var leftEye = CreateUIPanel(character.transform, "EyeLeft", new Color(0.1f, 0.1f, 0.1f, 1f));
		var leftEyeRect = leftEye.GetComponent<RectTransform>();
		leftEyeRect.anchorMin = new Vector2(0.33f, 0.75f);
		leftEyeRect.anchorMax = new Vector2(0.44f, 0.85f);
		leftEyeRect.offsetMin = Vector2.zero;
		leftEyeRect.offsetMax = Vector2.zero;

		var rightEye = CreateUIPanel(character.transform, "EyeRight", new Color(0.1f, 0.1f, 0.1f, 1f));
		var rightEyeRect = rightEye.GetComponent<RectTransform>();
		rightEyeRect.anchorMin = new Vector2(0.56f, 0.75f);
		rightEyeRect.anchorMax = new Vector2(0.67f, 0.85f);
		rightEyeRect.offsetMin = Vector2.zero;
		rightEyeRect.offsetMax = Vector2.zero;

		// 클릭 투명 오버레이 (전체 캐릭터 영역)
		var clickArea = CreateUIPanel(character.transform, "ClickArea", new Color(0, 0, 0, 0));
		SetStretch(clickArea);
		clickArea.AddComponent<CharacterEasterEggController>();

		// ── SpeechBubble ─────────────────────────────────────────────
		var speechBubble = CreateUIPanel(character.transform, "SpeechBubble", new Color(0.12f, 0.12f, 0.2f, 0.92f));
		var speechBubbleRect = speechBubble.GetComponent<RectTransform>();
		speechBubbleRect.anchorMin = new Vector2(-0.4f, 0.85f);
		speechBubbleRect.anchorMax = new Vector2(0.75f, 1.1f);
		speechBubbleRect.offsetMin = Vector2.zero;
		speechBubbleRect.offsetMax = Vector2.zero;
		speechBubble.SetActive(false);

		var speechTextObject = CreateTMPText(speechBubble.transform, "SpeechText","건드리지 마!", 22, FontStyles.Normal, new Color(0.95f, 0.95f, 1f, 1f));
		SetStretch(speechTextObject);
		var speechText = speechTextObject.GetComponent<TMP_Text>();
		speechText.alignment = TextAlignmentOptions.Center;

		// CharacterEasterEggController 참조 자동 연결
		var easterEgg = clickArea.GetComponent<CharacterEasterEggController>();
		SetPrivateField(easterEgg, "speechBubble", speechBubble);
		SetPrivateField(easterEgg, "speechText", speechTextObject.GetComponent<TMP_Text>());

		// ── SettingsPopup ────────────────────────────────────────────
		var settingsPopup = BuildSettingsPopup(canvasObject.transform);
		// ── CreditsPopup ─────────────────────────────────────────────
		var creditsPopup = BuildCreditsPopup(canvasObject.transform);

		// ── MenuRoot에 컴포넌트 붙이기 ───────────────────────────────
		// MenuRoot는 씬 내 적당한 위치 (Canvas 바깥)
		var menuController = menuRoot.AddComponent<MainMenuController>();
		var buttonHandler = menuRoot.AddComponent<MainMenuButtonHandler>();

		// MainMenuController 참조 연결
		SetPrivateField(menuController, "logoGroup", logoCanvasGroup);
		SetPrivateField(menuController, "menuButtonsGroup", menuButtonsCanvasGroup);
		SetPrivateField(menuController, "characterGroup", characterAnchorCanvasGroup);

		// MainMenuButtonHandler 참조 연결
		SetPrivateField(buttonHandler, "menuController", menuController);
		SetPrivateField(buttonHandler, "settingsPopup",
			settingsPopup.GetComponent<SimplePopup>());
		SetPrivateField(buttonHandler, "creditsPopup",
			creditsPopup.GetComponent<SimplePopup>());

		// 버튼 OnClick 연결
		var playButtonComponent = playButton.GetComponent<Button>();
		var settingsButtonComponent = settingsButton.GetComponent<Button>();
		var creditsButtonComponent = creditsButton.GetComponent<Button>();
		UnityEventTools.AddPersistentListener(playButtonComponent.onClick, buttonHandler.OnPlayClicked);
		UnityEventTools.AddPersistentListener(settingsButtonComponent.onClick, buttonHandler.OnSettingsClicked);
		UnityEventTools.AddPersistentListener(creditsButtonComponent.onClick, buttonHandler.OnCreditsClicked);

		// ── 씬 저장 ─────────────────────────────────────────────────
		Directory.CreateDirectory("Assets/Scenes");
		EditorSceneManager.SaveScene(scene, ScenePath);

		// Build Settings에 씬 추가
		AddSceneToBuildSettings(ScenePath);

		EditorUtility.DisplayDialog("완료",
			$"MainMenu 씬이 {ScenePath} 에 생성되었습니다.\n\n" +
			"★ Inspector 연결 필수 항목:\n" +
			"  MenuRoot → MainMenuController: logoGroup, menuButtonsGroup, characterGroup\n" +
			"  MenuRoot → MainMenuButtonHandler: settingsPopup, creditsPopup, menuController\n" +
			"  Character/ClickArea → CharacterEasterEggController: speechBubble, speechText\n\n" +
			"자세한 내용은 README 또는 씬 구성 문서를 참고하세요.",
			"확인");
	}

	// ─────────────────────────────────────────────────────────────────
	//  헬퍼 메서드
	// ─────────────────────────────────────────────────────────────────

	private static GameObject CreateUIPanel(Transform parent, string name, Color color)
	{
		var panel = new GameObject(name);
		var rect = panel.AddComponent<RectTransform>();
		rect.SetParent(parent, false);
		var image = panel.AddComponent<Image>();
		image.color = color;
		return panel;
	}

	private static void SetStretch(GameObject target)
	{
		var rect = target.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

	private static void CreateDecorationLine(Transform parent, string name,
		Vector2 anchorPositionY, Color color)
	{
		// anchorPositionY.x = y 위치 (0~1), anchorPositionY.y = 무시
		float verticalPosition = anchorPositionY.x;
		var line = CreateUIPanel(parent, name, color);
		var rect = line.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0f, verticalPosition - 0.002f);
		rect.anchorMax = new Vector2(1f, verticalPosition + 0.002f);
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;
	}

	private const string FontRegularPath = "Assets/TextMesh Pro/Fonts/Mona12.asset";
	private const string FontBoldPath    = "Assets/TextMesh Pro/Fonts/Mona12-Bold.asset";

	private static TMP_FontAsset LoadFont(FontStyles style)
	{
		var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRegularPath);
		if (font == null)
			Debug.LogWarning($"[MainMenuSceneBuilder] 폰트 에셋을 찾을 수 없습니다: {FontRegularPath}");
		return font;
	}

	private static GameObject CreateTMPText(Transform parent, string name, string text, float size, FontStyles style, Color color)
	{
		var textObject = new GameObject(name);
		var rect = textObject.AddComponent<RectTransform>();
		rect.SetParent(parent, false);
		var textComponent = textObject.AddComponent<TextMeshProUGUI>();
		textComponent.text = text;
		textComponent.fontSize = size;
		textComponent.fontStyle = style;
		textComponent.color = color;
		textComponent.enableWordWrapping = false;
		var font = LoadFont(style);
		if (font != null)
			textComponent.font = font;
		return textObject;
	}

	private static GameObject CreateMenuButton(Transform parent, string name, string label)
	{
		// 버튼 배경 패널
		var buttonObject = new GameObject(name);
		var rect = buttonObject.AddComponent<RectTransform>();
		rect.SetParent(parent, false);

		var backgroundImage = buttonObject.AddComponent<Image>();
		backgroundImage.color = new Color(0.15f, 0.18f, 0.35f, 0.9f);

		var button = buttonObject.AddComponent<Button>();
		var colors = button.colors;
		colors.normalColor = new Color(0.15f, 0.18f, 0.35f, 0.9f);
		colors.highlightedColor = new Color(0.28f, 0.35f, 0.70f, 1f);
		colors.pressedColor = new Color(0.10f, 0.12f, 0.25f, 1f);
		colors.selectedColor = new Color(0.28f, 0.35f, 0.70f, 1f);
		button.colors = colors;
		button.targetGraphic = backgroundImage;

		// 레이블
		var labelObject = CreateTMPText(buttonObject.transform, "Label", label,
			64, FontStyles.Bold, new Color(0.92f, 0.92f, 1f, 1f));
		SetStretch(labelObject);
		var labelText = labelObject.GetComponent<TMP_Text>();
		labelText.alignment = TextAlignmentOptions.MidlineLeft;
		var labelRect = labelObject.GetComponent<RectTransform>();
		labelRect.offsetMin = new Vector2(24, 0);
		labelRect.offsetMax = new Vector2(-8, 0);

		return buttonObject;
	}

	private static GameObject BuildSettingsPopup(Transform canvasParent)
	{
		var settingsPopup = CreateUIPanel(canvasParent, "SettingsPopup",
			new Color(0.08f, 0.09f, 0.18f, 0.97f));
		CenterPopup(settingsPopup, 520, 400);
		settingsPopup.AddComponent<SimplePopup>();
		settingsPopup.AddComponent<SettingsPopupController>();

		// 제목
		var title = CreateTMPText(settingsPopup.transform, "Title", "Settings",
			40, FontStyles.Bold, Color.white);
		var titleRect = title.GetComponent<RectTransform>();
		titleRect.anchorMin = new Vector2(0, 0.78f);
		titleRect.anchorMax = new Vector2(1, 1f);
		titleRect.offsetMin = Vector2.zero;
		titleRect.offsetMax = Vector2.zero;
		title.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

		// BGM 슬라이더 행
		var bgmRow = BuildSliderRow(settingsPopup.transform, "BGM Volume", 0.52f, 0.70f);
		// SFX 슬라이더 행
		var sfxRow = BuildSliderRow(settingsPopup.transform, "SFX Volume", 0.30f, 0.48f);

		// SettingsPopupController 슬라이더 연결
		var settingsController = settingsPopup.GetComponent<SettingsPopupController>();
		SetPrivateField(settingsController, "bgmSlider", bgmRow.Slider);
		SetPrivateField(settingsController, "sfxSlider", sfxRow.Slider);
		SetPrivateField(settingsController, "bgmValueLabel", bgmRow.ValueLabel);
		SetPrivateField(settingsController, "sfxValueLabel", sfxRow.ValueLabel);

		// Close 버튼
		var closeButton = CreateMenuButton(settingsPopup.transform, "CloseButton", "✕  Close");
		var closeButtonRect = closeButton.GetComponent<RectTransform>();
		closeButtonRect.anchorMin = new Vector2(0.3f, 0.04f);
		closeButtonRect.anchorMax = new Vector2(0.7f, 0.22f);
		closeButtonRect.offsetMin = Vector2.zero;
		closeButtonRect.offsetMax = Vector2.zero;
		var popup = settingsPopup.GetComponent<SimplePopup>();
		UnityEventTools.AddPersistentListener(closeButton.GetComponent<Button>().onClick, popup.Close);

		settingsPopup.SetActive(false);
		return settingsPopup;
	}

	private struct SliderRowResult
	{
		public Slider Slider;
		public TMP_Text ValueLabel;
	}

	private static SliderRowResult BuildSliderRow(Transform parent, string label,
		float anchorYMin, float anchorYMax)
	{
		var row = new GameObject(label.Replace(" ", "_") + "_Row");
		var rowRect = row.AddComponent<RectTransform>();
		rowRect.SetParent(parent, false);
		rowRect.anchorMin = new Vector2(0.06f, anchorYMin);
		rowRect.anchorMax = new Vector2(0.94f, anchorYMax);
		rowRect.offsetMin = Vector2.zero;
		rowRect.offsetMax = Vector2.zero;

		var labelObject = CreateTMPText(row.transform, "Label", label,
			24, FontStyles.Normal, new Color(0.8f, 0.8f, 1f, 1f));
		var labelRect = labelObject.GetComponent<RectTransform>();
		labelRect.anchorMin = new Vector2(0, 0.5f);
		labelRect.anchorMax = new Vector2(0.38f, 1f);
		labelRect.offsetMin = Vector2.zero;
		labelRect.offsetMax = Vector2.zero;
		labelObject.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

		// Slider
		var sliderObject = new GameObject("Slider");
		var sliderRect = sliderObject.AddComponent<RectTransform>();
		sliderRect.SetParent(row.transform, false);
		sliderRect.anchorMin = new Vector2(0.4f, 0.1f);
		sliderRect.anchorMax = new Vector2(0.85f, 0.9f);
		sliderRect.offsetMin = Vector2.zero;
		sliderRect.offsetMax = Vector2.zero;
		var sliderBackground = sliderObject.AddComponent<Image>();
		sliderBackground.color = new Color(0.2f, 0.2f, 0.35f, 1f);

		// Fill Area → Fill
		var fillArea = new GameObject("FillArea");
		var fillAreaRect = fillArea.AddComponent<RectTransform>();
		fillAreaRect.SetParent(sliderObject.transform, false);
		fillAreaRect.anchorMin = new Vector2(0f, 0.25f);
		fillAreaRect.anchorMax = new Vector2(1f, 0.75f);
		fillAreaRect.offsetMin = new Vector2(5f, 0f);
		fillAreaRect.offsetMax = new Vector2(-15f, 0f);

		var fill = new GameObject("Fill");
		var fillRect = fill.AddComponent<RectTransform>();
		fillRect.SetParent(fillArea.transform, false);
		fillRect.anchorMin = Vector2.zero;
		fillRect.anchorMax = Vector2.one;
		fillRect.offsetMin = Vector2.zero;
		fillRect.offsetMax = Vector2.zero;
		fill.AddComponent<Image>().color = new Color(0.35f, 0.55f, 1f, 1f);

		// Handle Slide Area → Handle
		var handleSlideArea = new GameObject("HandleSlideArea");
		var handleSlideAreaRect = handleSlideArea.AddComponent<RectTransform>();
		handleSlideAreaRect.SetParent(sliderObject.transform, false);
		handleSlideAreaRect.anchorMin = Vector2.zero;
		handleSlideAreaRect.anchorMax = Vector2.one;
		handleSlideAreaRect.offsetMin = new Vector2(10f, 0f);
		handleSlideAreaRect.offsetMax = new Vector2(-10f, 0f);

		var handle = new GameObject("Handle");
		var handleRect = handle.AddComponent<RectTransform>();
		handleRect.SetParent(handleSlideArea.transform, false);
		handleRect.anchorMin = new Vector2(0f, 0f);
		handleRect.anchorMax = new Vector2(0f, 1f);
		handleRect.sizeDelta = new Vector2(20f, 0f);
		handle.AddComponent<Image>().color = new Color(0.6f, 0.8f, 1f, 1f);

		// Slider 컴포넌트는 자식 오브젝트 구성 후 마지막에 추가
		var slider = sliderObject.AddComponent<Slider>();
		slider.fillRect = fillRect;
		slider.handleRect = handleRect;
		slider.direction = Slider.Direction.LeftToRight;
		slider.minValue = 0f;
		slider.maxValue = 1f;
		slider.value = 0.8f;

		// 값 레이블
		var valueLabel = CreateTMPText(row.transform, "ValueLabel", "80%",
			22, FontStyles.Normal, new Color(0.7f, 0.9f, 1f, 1f));
		var valueLabelRect = valueLabel.GetComponent<RectTransform>();
		valueLabelRect.anchorMin = new Vector2(0.87f, 0f);
		valueLabelRect.anchorMax = new Vector2(1f, 1f);
		valueLabelRect.offsetMin = Vector2.zero;
		valueLabelRect.offsetMax = Vector2.zero;
		valueLabel.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Midline;

		return new SliderRowResult
		{
			Slider = slider,
			ValueLabel = valueLabel.GetComponent<TMP_Text>()
		};
	}

	private static GameObject BuildCreditsPopup(Transform canvasParent)
	{
		var creditsPopup = CreateUIPanel(canvasParent, "CreditsPopup",
			new Color(0.08f, 0.09f, 0.18f, 0.97f));
		CenterPopup(creditsPopup, 480, 360);
		creditsPopup.AddComponent<SimplePopup>();

		var bodyLines = new string[]
		{
			"<size=40><b>GAME TITLE</b></size>",
			"",
			"<size=26>Made by  <color=#8899ff>Team Capstone</color></size>",
			"<size=22>Programming  ·  Art  ·  Design</size>",
			"",
			"<size=20><color=#6677aa>© 2026 All rights reserved.</color></size>"
		};
		var creditsTextObject = CreateTMPText(creditsPopup.transform, "CreditsText",
			string.Join("\n", bodyLines),
			28, FontStyles.Normal, Color.white);
		var creditsTextRect = creditsTextObject.GetComponent<RectTransform>();
		creditsTextRect.anchorMin = new Vector2(0.05f, 0.22f);
		creditsTextRect.anchorMax = new Vector2(0.95f, 0.96f);
		creditsTextRect.offsetMin = Vector2.zero;
		creditsTextRect.offsetMax = Vector2.zero;
		creditsTextObject.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;
		creditsTextObject.GetComponent<TMP_Text>().enableWordWrapping = true;

		var closeButton = CreateMenuButton(creditsPopup.transform, "CloseButton", "✕  Close");
		var closeButtonRect = closeButton.GetComponent<RectTransform>();
		closeButtonRect.anchorMin = new Vector2(0.3f, 0.03f);
		closeButtonRect.anchorMax = new Vector2(0.7f, 0.18f);
		closeButtonRect.offsetMin = Vector2.zero;
		closeButtonRect.offsetMax = Vector2.zero;
		var popup = creditsPopup.GetComponent<SimplePopup>();
		UnityEventTools.AddPersistentListener(closeButton.GetComponent<Button>().onClick, popup.Close);

		creditsPopup.SetActive(false);
		return creditsPopup;
	}

	private static void CenterPopup(GameObject target, float width, float height)
	{
		var rect = target.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(width, height);
		rect.anchoredPosition = Vector2.zero;
	}

	private static void SetPrivateField(object target, string fieldName, object value)
	{
		if (target == null)
			return;
		var fieldInfo = target.GetType().GetField(fieldName,
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		if (fieldInfo != null)
			fieldInfo.SetValue(target, value);
		else
			Debug.LogWarning($"[MainMenuSceneBuilder] Field '{fieldName}' not found on {target.GetType().Name}");
	}

	private static void AddSceneToBuildSettings(string path)
	{
		var scenes = EditorBuildSettings.scenes;
		foreach (var existingScene in scenes)
			if (existingScene.path == path)
				return; // 이미 있음

		var updatedScenes = new EditorBuildSettingsScene[scenes.Length + 1];
		// MainMenu를 첫 번째(index 0)에 삽입
		updatedScenes[0] = new EditorBuildSettingsScene(path, true);
		for (int i = 0; i < scenes.Length; i++)
			updatedScenes[i + 1] = scenes[i];
		EditorBuildSettings.scenes = updatedScenes;
	}
}
