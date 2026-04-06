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
		cameraComponent.backgroundColor = Color.black;
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

		// ── 배경 이미지 (화면 전체) ────────────────────────────────
		var logoGroup = new GameObject("LogoGroup");
		var logoGroupRect = logoGroup.AddComponent<RectTransform>();
		logoGroupRect.SetParent(canvasObject.transform, false);
		logoGroupRect.anchorMin = Vector2.zero;
		logoGroupRect.anchorMax = Vector2.one;
		logoGroupRect.offsetMin = Vector2.zero;
		logoGroupRect.offsetMax = Vector2.zero;
		var logoCanvasGroup = logoGroup.AddComponent<CanvasGroup>();

		var logoSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Mobs/MainScreen_logo.png");
		if (logoSprite == null)
			Debug.LogWarning("[MainMenuSceneBuilder] 메인화면 스프라이트를 찾을 수 없습니다: Assets/Mobs/MainScreen_logo.png");
		var logoImage = new GameObject("BackgroundImage");
		var logoImageRect = logoImage.AddComponent<RectTransform>();
		logoImageRect.SetParent(logoGroup.transform, false);
		SetStretch(logoImage);
		var logoImg = logoImage.AddComponent<Image>();
		logoImg.sprite = logoSprite;
		logoImg.type = Image.Type.Simple;
		logoImg.preserveAspect = false;
		logoImg.raycastTarget = false;

		// ── MenuButtons (우측) ──────────────────────────────────────
		var menuButtons = new GameObject("MenuButtons");
		var menuButtonsRect = menuButtons.AddComponent<RectTransform>();
		menuButtonsRect.SetParent(canvasObject.transform, false);
		menuButtonsRect.anchorMin = new Vector2(0.65f, 0.08f);
		menuButtonsRect.anchorMax = new Vector2(0.95f, 0.45f);
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

		// ── SettingsPopup ────────────────────────────────────────────
		var settingsDimmer = CreateDimmer(canvasObject.transform, "SettingsDimmer");
		var settingsPopup = BuildSettingsPopup(canvasObject.transform);
		SetPrivateField(settingsPopup.GetComponent<SimplePopup>(), "dimmer", settingsDimmer.GetComponent<Image>());
		// ── CreditsPopup ─────────────────────────────────────────────
		var creditsDimmer = CreateDimmer(canvasObject.transform, "CreditsDimmer");
		var creditsPopup = BuildCreditsPopup(canvasObject.transform);
		SetPrivateField(creditsPopup.GetComponent<SimplePopup>(), "dimmer", creditsDimmer.GetComponent<Image>());

		// ── MenuRoot에 컴포넌트 붙이기 ───────────────────────────────
		// MenuRoot는 씬 내 적당한 위치 (Canvas 바깥)
		var menuController = menuRoot.AddComponent<MainMenuController>();
		var buttonHandler = menuRoot.AddComponent<MainMenuButtonHandler>();

		// MainMenuController 참조 연결
		SetPrivateField(menuController, "logoGroup", logoCanvasGroup);
		SetPrivateField(menuController, "menuButtonsGroup", menuButtonsCanvasGroup);

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
		=> SceneBuilderUtility.Stretch(target.GetComponent<RectTransform>());

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
		var font = LoadFont(style);

		var textObject = new GameObject(name);
		var rect = textObject.AddComponent<RectTransform>();
		rect.SetParent(parent, false);
		var textComponent = textObject.AddComponent<TextMeshProUGUI>();
		if (font != null)
			textComponent.font = font;
		textComponent.text = text;
		textComponent.fontSize = size;
		textComponent.fontStyle = style;
		textComponent.color = color;
		textComponent.textWrappingMode = TextWrappingModes.NoWrap;
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
		closeButton.GetComponentInChildren<TMP_Text>().fontSize = 28;
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
			"<size=40><b>마작홀덤주사위디펜스</b></size>",
			"",
			"<size=26>Made by  <color=#8899ff>이차원스튜디오</color></size>",
			"<size=22>Code  ·  송지한</size>",
			"<size=22>Art / Design  ·  장진영</size>",
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
		creditsTextObject.GetComponent<TMP_Text>().textWrappingMode = TextWrappingModes.Normal;

		var closeButton = CreateMenuButton(creditsPopup.transform, "CloseButton", "✕  Close");
		closeButton.GetComponentInChildren<TMP_Text>().fontSize = 28;
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

	private static GameObject CreateDimmer(Transform canvasParent, string name)
	{
		var dimmer = new GameObject(name);
		var rect = dimmer.AddComponent<RectTransform>();
		rect.SetParent(canvasParent, false);
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = Vector2.zero;
		rect.offsetMax = Vector2.zero;

		var image = dimmer.AddComponent<Image>();
		image.color = new Color(0f, 0f, 0f, 0f);
		image.raycastTarget = true;

		dimmer.SetActive(false);
		return dimmer;
	}

	private static void CenterPopup(GameObject target, float width, float height)
		=> SceneBuilderUtility.CenterPopup(target, width, height);

	private static void SetPrivateField(object target, string fieldName, object value)
		=> SceneBuilderUtility.SetField(target, fieldName, value);

	private static void AddSceneToBuildSettings(string path)
	{
		var scenes = EditorBuildSettings.scenes;
		foreach (var existingScene in scenes)
			if (existingScene.path == path)
				return;

		var updatedScenes = new EditorBuildSettingsScene[scenes.Length + 1];
		// MainMenu를 첫 번째(index 0)에 삽입
		updatedScenes[0] = new EditorBuildSettingsScene(path, true);
		for (int i = 0; i < scenes.Length; i++)
			updatedScenes[i + 1] = scenes[i];
		EditorBuildSettings.scenes = updatedScenes;
	}
}
