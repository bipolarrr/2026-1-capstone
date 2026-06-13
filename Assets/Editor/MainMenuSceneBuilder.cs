// Assets/Editor/MainMenuSceneBuilder.cs
// Unity 메뉴 → Tools → Build MainMenu Scene 으로 씬을 자동 생성합니다.
// 씬이 이미 존재하면 덮어쓸지 물어봅니다.

using System;
using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using SBU = SceneBuilderUtility;

public static class MainMenuSceneBuilder
{
	private const string ScenePath = "Assets/Scenes/MainMenu.unity";
	private const string MainScreenLogoPath = "Assets/UI/MainScreen_Logo.png";

	[MenuItem("Tools/Build MainMenu Scene")]
	public static void Build()
	{
		BuildInternal(confirmOverwrite: true, showCompletionDialog: true);
	}

	public static bool BuildForIncremental()
	{
		return BuildInternal(confirmOverwrite: false, showCompletionDialog: false);
	}

	static bool BuildInternal(bool confirmOverwrite, bool showCompletionDialog)
	{
		if (confirmOverwrite && File.Exists(ScenePath))
		{
			bool overwrite = EditorUtility.DisplayDialog(
				"MainMenu 씬 생성",
				$"{ScenePath} 가 이미 존재합니다. 덮어쓰시겠습니까?",
				"덮어쓰기", "취소");
			if (!overwrite)
				return false;
		}

		SBU.BeginSceneBuildValidation(nameof(MainMenuSceneBuilder));
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var shell = SBU.BuildSceneShell("MainCamera", Color.black,
			cameraPosition: new Vector3(0, 0, -10), includeAudioListener: true);
		var canvasObject = shell.canvas.gameObject;

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

		var logoSprite = LoadMainScreenLogoSprite();
		if (logoSprite == null)
			Debug.LogWarning($"[MainMenuSceneBuilder] 메인화면 스프라이트를 찾을 수 없습니다: {MainScreenLogoPath}");
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
		var settingsDimmer = SBU.CreateDimmer(canvasObject.transform, "SettingsDimmer");
		var settingsPopup = BuildSettingsPopup(canvasObject.transform);
		SBU.SetField(settingsPopup.GetComponent<SimplePopup>(), "dimmer", settingsDimmer.GetComponent<Image>());
		// ── CreditsPopup ─────────────────────────────────────────────
		var creditsDimmer = SBU.CreateDimmer(canvasObject.transform, "CreditsDimmer");
		var creditsPopup = BuildCreditsPopup(canvasObject.transform);
		SBU.SetField(creditsPopup.GetComponent<SimplePopup>(), "dimmer", creditsDimmer.GetComponent<Image>());

		// ── MenuRoot에 컴포넌트 붙이기 ───────────────────────────────
		// MenuRoot는 씬 내 적당한 위치 (Canvas 바깥)
		var menuController = menuRoot.AddComponent<MainMenuController>();
		var buttonHandler = menuRoot.AddComponent<MainMenuButtonHandler>();

		// MainMenuController 참조 연결
		SBU.SetField(menuController, "logoGroup", logoCanvasGroup);
		SBU.SetField(menuController, "menuButtonsGroup", menuButtonsCanvasGroup);

		// MainMenuButtonHandler 참조 연결
		SBU.SetField(buttonHandler, "menuController", menuController);
		SBU.SetField(buttonHandler, "settingsPopup",
			settingsPopup.GetComponent<SimplePopup>());
		SBU.SetField(buttonHandler, "creditsPopup",
			creditsPopup.GetComponent<SimplePopup>());

		// 버튼 OnClick 연결
		var playButtonComponent = playButton.GetComponent<Button>();
		var settingsButtonComponent = settingsButton.GetComponent<Button>();
		var creditsButtonComponent = creditsButton.GetComponent<Button>();
		UnityEventTools.AddPersistentListener(playButtonComponent.onClick, buttonHandler.OnPlayClicked);
		UnityEventTools.AddPersistentListener(settingsButtonComponent.onClick, buttonHandler.OnSettingsClicked);
		UnityEventTools.AddPersistentListener(creditsButtonComponent.onClick, buttonHandler.OnCreditsClicked);

		// ── 오디오 매니저 ─────────────────────────────────────────
		SceneBuilderUtility.BuildAudioManager(new[]
		{
			"UI_Click", "UI_OK", "Transition_2"
		}, includeDrumRoll: false);

		// ── 씬 저장 ─────────────────────────────────────────────────
		return SBU.SaveSceneAndShowDialog(scene, ScenePath,
			$"MainMenu 씬이 {ScenePath} 에 생성되었습니다.\n\n" +
			"★ Inspector 연결 필수 항목:\n" +
			"  MenuRoot → MainMenuController: logoGroup, menuButtonsGroup, characterGroup\n" +
			"  MenuRoot → MainMenuButtonHandler: settingsPopup, creditsPopup, menuController\n" +
			"  Character/ClickArea → CharacterEasterEggController: speechBubble, speechText\n\n" +
			"자세한 내용은 README 또는 씬 구성 문서를 참고하세요.",
			SBU.BuildSettingsPlacement.InsertFirst,
			showCompletionDialog);
	}

	// ─────────────────────────────────────────────────────────────────
	//  헬퍼 메서드
	// ─────────────────────────────────────────────────────────────────

	private static void SetStretch(GameObject target)
		=> SBU.Stretch(target.GetComponent<RectTransform>());

	private static Sprite LoadMainScreenLogoSprite()
	{
		EnsureMainScreenLogoImportSettings();

		var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(MainScreenLogoPath);
		if (sprite != null)
			return sprite;

		var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(MainScreenLogoPath);
		foreach (var asset in sprites)
		{
			if (asset is Sprite subSprite)
				return subSprite;
		}

		return null;
	}

	private static void EnsureMainScreenLogoImportSettings()
	{
		if (!File.Exists(MainScreenLogoPath))
		{
			Debug.LogWarning($"[MainMenuSceneBuilder] 메인화면 에셋 파일이 없습니다: {MainScreenLogoPath}");
			return;
		}

		if (ConvertMisnamedPngIfNeeded(MainScreenLogoPath))
			AssetDatabase.ImportAsset(MainScreenLogoPath, ImportAssetOptions.ForceUpdate);

		if (AssetImporter.GetAtPath(MainScreenLogoPath) is not TextureImporter importer)
		{
			Debug.LogWarning($"[MainMenuSceneBuilder] TextureImporter를 찾을 수 없습니다: {MainScreenLogoPath}");
			return;
		}

		bool changed = false;
		if (importer.textureType != TextureImporterType.Sprite)
		{
			importer.textureType = TextureImporterType.Sprite;
			changed = true;
		}

		if (importer.spriteImportMode != SpriteImportMode.Single)
		{
			importer.spriteImportMode = SpriteImportMode.Single;
			changed = true;
		}

		if (importer.mipmapEnabled)
		{
			importer.mipmapEnabled = false;
			changed = true;
		}

		if (!importer.alphaIsTransparency)
		{
			importer.alphaIsTransparency = true;
			changed = true;
		}

		if (importer.npotScale != TextureImporterNPOTScale.None)
		{
			importer.npotScale = TextureImporterNPOTScale.None;
			changed = true;
		}

		var settings = new TextureImporterSettings();
		importer.ReadTextureSettings(settings);
		if (settings.spriteMeshType != SpriteMeshType.FullRect)
		{
			settings.spriteMeshType = SpriteMeshType.FullRect;
			importer.SetTextureSettings(settings);
			changed = true;
		}

		if (changed)
			importer.SaveAndReimport();
	}

	private static bool ConvertMisnamedPngIfNeeded(string assetPath)
	{
		if (!assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
			return false;

		var bytes = File.ReadAllBytes(assetPath);
		if (HasPngHeader(bytes))
			return false;

		var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
		try
		{
			if (!ImageConversion.LoadImage(texture, bytes, false))
			{
				Debug.LogWarning($"[MainMenuSceneBuilder] {assetPath} 확장자는 PNG지만 PNG/JPG로 디코딩할 수 없습니다. 원본 포맷을 PNG로 다시 저장해 주세요.");
				return false;
			}

			File.WriteAllBytes(assetPath, ImageConversion.EncodeToPNG(texture));
			Debug.LogWarning($"[MainMenuSceneBuilder] {assetPath}의 실제 포맷이 .png 확장자와 달라 PNG로 변환했습니다.");
			return true;
		}
		finally
		{
			UnityEngine.Object.DestroyImmediate(texture);
		}
	}

	private static bool HasPngHeader(byte[] bytes)
	{
		return bytes.Length >= 8
			&& bytes[0] == 0x89
			&& bytes[1] == 0x50
			&& bytes[2] == 0x4E
			&& bytes[3] == 0x47
			&& bytes[4] == 0x0D
			&& bytes[5] == 0x0A
			&& bytes[6] == 0x1A
			&& bytes[7] == 0x0A;
	}

	private static GameObject CreateMenuButton(Transform parent, string name, string label)
	{
		var go = SBU.CreateButton(parent, name, label,
			64, SBU.ButtonNormal, SBU.ButtonHighlight, SBU.ButtonPressed);
		var labelText = go.GetComponentInChildren<TMP_Text>();
		labelText.alignment = TextAlignmentOptions.MidlineLeft;
		var labelRect = labelText.GetComponent<RectTransform>();
		labelRect.offsetMin = new Vector2(24, 0);
		labelRect.offsetMax = new Vector2(-8, 0);
		return go;
	}

	private static GameObject BuildSettingsPopup(Transform canvasParent)
	{
		var settingsPopup = SBU.CreateUIPanel(canvasParent, "SettingsPopup",
			new Color(0.08f, 0.09f, 0.18f, 0.97f));
		CenterPopup(settingsPopup, 520, 400);
		settingsPopup.AddComponent<SimplePopup>();
		settingsPopup.AddComponent<SettingsPopupController>();

		// 제목
		var title = SBU.CreateTMPText(settingsPopup.transform, "Title", "Settings",
			40, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
		var titleRect = title.GetComponent<RectTransform>();
		titleRect.anchorMin = new Vector2(0, 0.78f);
		titleRect.anchorMax = new Vector2(1, 1f);
		titleRect.offsetMin = Vector2.zero;
		titleRect.offsetMax = Vector2.zero;

		// BGM 슬라이더 행
		var bgmRow = BuildSliderRow(settingsPopup.transform, "BGM Volume", 0.52f, 0.70f);
		// SFX 슬라이더 행
		var sfxRow = BuildSliderRow(settingsPopup.transform, "SFX Volume", 0.30f, 0.48f);

		// SettingsPopupController 슬라이더 연결
		var settingsController = settingsPopup.GetComponent<SettingsPopupController>();
		SBU.SetField(settingsController, "bgmSlider", bgmRow.Slider);
		SBU.SetField(settingsController, "sfxSlider", sfxRow.Slider);
		SBU.SetField(settingsController, "bgmValueLabel", bgmRow.ValueLabel);
		SBU.SetField(settingsController, "sfxValueLabel", sfxRow.ValueLabel);

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

		var labelTmp = SBU.CreateTMPText(row.transform, "Label", label,
			24, new Color(0.8f, 0.8f, 1f, 1f), TextAlignmentOptions.MidlineLeft);
		var labelRect = labelTmp.GetComponent<RectTransform>();
		labelRect.anchorMin = new Vector2(0, 0.5f);
		labelRect.anchorMax = new Vector2(0.38f, 1f);
		labelRect.offsetMin = Vector2.zero;
		labelRect.offsetMax = Vector2.zero;

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
		var valueLabelTmp = SBU.CreateTMPText(row.transform, "ValueLabel", "80%",
			22, new Color(0.7f, 0.9f, 1f, 1f), TextAlignmentOptions.Midline);
		var valueLabelRect = valueLabelTmp.GetComponent<RectTransform>();
		valueLabelRect.anchorMin = new Vector2(0.87f, 0f);
		valueLabelRect.anchorMax = new Vector2(1f, 1f);
		valueLabelRect.offsetMin = Vector2.zero;
		valueLabelRect.offsetMax = Vector2.zero;

		return new SliderRowResult
		{
			Slider = slider,
			ValueLabel = valueLabelTmp
		};
	}

	private static GameObject BuildCreditsPopup(Transform canvasParent)
	{
		var creditsPopup = SBU.CreateUIPanel(canvasParent, "CreditsPopup",
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
		var creditsTmp = SBU.CreateTMPText(creditsPopup.transform, "CreditsText",
			string.Join("\n", bodyLines),
			28, Color.white, TextAlignmentOptions.Center);
		creditsTmp.textWrappingMode = TextWrappingModes.Normal;
		var creditsTextRect = creditsTmp.GetComponent<RectTransform>();
		creditsTextRect.anchorMin = new Vector2(0.05f, 0.22f);
		creditsTextRect.anchorMax = new Vector2(0.95f, 0.96f);
		creditsTextRect.offsetMin = Vector2.zero;
		creditsTextRect.offsetMax = Vector2.zero;

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

	private static void CenterPopup(GameObject target, float width, float height)
		=> SBU.CenterPopup(target, width, height);
}
