// Assets/Editor/CharacterSelectSceneBuilder.cs
// Unity 메뉴 → Tools → Build CharacterSelect Scene 으로 씬을 자동 생성합니다.
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

public static class CharacterSelectSceneBuilder
{
	private const string ScenePath = "Assets/Scenes/CharacterSelect.unity";

	[MenuItem("Tools/Build CharacterSelect Scene")]
	public static void Build()
	{
		if (File.Exists(ScenePath))
		{
			bool overwrite = EditorUtility.DisplayDialog(
				"CharacterSelect 씬 생성",
				$"{ScenePath} 가 이미 존재합니다. 덮어쓰시겠습니까?",
				"덮어쓰기", "취소");
			if (!overwrite)
				return;
		}

		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		// ── 카메라 ──────────────────────────────────────────────────
		var cameraObject = new GameObject("MainCamera");
		cameraObject.tag = "MainCamera";
		var mainCamera = cameraObject.AddComponent<Camera>();
		mainCamera.clearFlags = CameraClearFlags.SolidColor;
		mainCamera.backgroundColor = new Color(0.09f, 0.09f, 0.15f, 1f);
		mainCamera.orthographic = true;
		mainCamera.orthographicSize = 5f;
		cameraObject.transform.position = new Vector3(0, 0, -10);
		cameraObject.AddComponent<AudioListener>();

		// ── 월드 스페이스 캐릭터 미리보기 오브젝트 ───────────────────
		// 실제 스프라이트/애니메이터가 할당되면 이 오브젝트를 통해 재생됩니다.
		// 현재는 빈 컴포넌트만 부착되며 SpriteRenderer에 스프라이트가 없어 보이지 않습니다.
		var characterPreviewObject = new GameObject("CharacterPreviewObject");
		characterPreviewObject.transform.position = new Vector3(0f, 0.5f, 0f);
		var previewSpriteRenderer = characterPreviewObject.AddComponent<SpriteRenderer>();
		var previewAnimator = characterPreviewObject.AddComponent<Animator>();

		// ── Canvas ──────────────────────────────────────────────────
		var canvasObject = new GameObject("Canvas");
		var canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;

		var canvasScaler = canvasObject.AddComponent<CanvasScaler>();
		canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		canvasScaler.referenceResolution = new Vector2(1920, 1080);
		canvasScaler.matchWidthOrHeight = 0.5f;

		canvasObject.AddComponent<GraphicRaycaster>();

		// ── EventSystem ──────────────────────────────────────────────
		var eventSystemObject = new GameObject("EventSystem");
		eventSystemObject.AddComponent<EventSystem>();
		eventSystemObject.AddComponent<InputSystemUIInputModule>();

		// ── CharacterSelectRoot ───────────────────────────────────────
		var characterSelectRootObject = new GameObject("CharacterSelectRoot");

		// ── 배경 ────────────────────────────────────────────────────
		var backgroundObject = CreateUIPanel(canvasObject.transform, "Background",
			new Color(0.06f, 0.07f, 0.13f, 1f));
		SetStretch(backgroundObject);

		var topGradientObject = CreateUIPanel(backgroundObject.transform, "BackgroundTopGradient",
			new Color(0.12f, 0.13f, 0.22f, 0.5f));
		var topGradientTransform = topGradientObject.GetComponent<RectTransform>();
		topGradientTransform.anchorMin = new Vector2(0, 0.5f);
		topGradientTransform.anchorMax = new Vector2(1, 1f);
		topGradientTransform.offsetMin = Vector2.zero;
		topGradientTransform.offsetMax = Vector2.zero;

		CreateDecorationLine(backgroundObject.transform, "DecorationLineTop",
			0.88f, new Color(0.3f, 0.5f, 0.9f, 0.15f));
		CreateDecorationLine(backgroundObject.transform, "DecorationLineBottom",
			0.14f, new Color(0.3f, 0.5f, 0.9f, 0.10f));

		// ── 타이틀 텍스트 ────────────────────────────────────────────
		var titleTextObject = CreateTMPText(canvasObject.transform, "TitleText",
			"캐릭터 선택", 60, FontStyles.Bold,
			new Color(0.95f, 0.95f, 1f, 1f));
		var titleTextTransform = titleTextObject.GetComponent<RectTransform>();
		titleTextTransform.anchorMin = new Vector2(0.1f, 0.88f);
		titleTextTransform.anchorMax = new Vector2(0.9f, 1.0f);
		titleTextTransform.offsetMin = Vector2.zero;
		titleTextTransform.offsetMax = Vector2.zero;
		titleTextObject.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

		// ── 좌측 화살표 버튼 ─────────────────────────────────────────
		var leftArrowButtonObject = CreateArrowButton(canvasObject.transform, "LeftArrowButton", "◁");
		var leftArrowButtonTransform = leftArrowButtonObject.GetComponent<RectTransform>();
		leftArrowButtonTransform.anchorMin = new Vector2(0.02f, 0.30f);
		leftArrowButtonTransform.anchorMax = new Vector2(0.13f, 0.82f);
		leftArrowButtonTransform.offsetMin = Vector2.zero;
		leftArrowButtonTransform.offsetMax = Vector2.zero;

		// ── 우측 화살표 버튼 ─────────────────────────────────────────
		var rightArrowButtonObject = CreateArrowButton(canvasObject.transform, "RightArrowButton", "▷");
		var rightArrowButtonTransform = rightArrowButtonObject.GetComponent<RectTransform>();
		rightArrowButtonTransform.anchorMin = new Vector2(0.87f, 0.30f);
		rightArrowButtonTransform.anchorMax = new Vector2(0.98f, 0.82f);
		rightArrowButtonTransform.offsetMin = Vector2.zero;
		rightArrowButtonTransform.offsetMax = Vector2.zero;

		// ── 캐릭터 미리보기 영역 ──────────────────────────────────────
		var characterPreviewAreaObject = BuildCharacterPreviewArea(canvasObject.transform);

		// ── 캐릭터 정보 영역 ──────────────────────────────────────────
		var characterInfoAreaResult = BuildCharacterInfoArea(canvasObject.transform);

		// ── 하단 버튼 영역 ────────────────────────────────────────────
		var backButtonObject = CreateMenuButton(canvasObject.transform, "BackButton", "◁  뒤로");
		var backButtonTransform = backButtonObject.GetComponent<RectTransform>();
		backButtonTransform.anchorMin = new Vector2(0.10f, 0.02f);
		backButtonTransform.anchorMax = new Vector2(0.38f, 0.13f);
		backButtonTransform.offsetMin = Vector2.zero;
		backButtonTransform.offsetMax = Vector2.zero;

		var startButtonObject = CreateMenuButton(canvasObject.transform, "StartButton", "▷  시작");
		var startButtonTransform = startButtonObject.GetComponent<RectTransform>();
		startButtonTransform.anchorMin = new Vector2(0.62f, 0.02f);
		startButtonTransform.anchorMax = new Vector2(0.90f, 0.13f);
		startButtonTransform.offsetMin = Vector2.zero;
		startButtonTransform.offsetMax = Vector2.zero;

		// ── 미구현 캐릭터 팝업 ─────────────────────────────────────────
		var unavailablePopupResult = BuildUnavailablePopup(canvasObject.transform);

		// ── CharacterSelectController 컴포넌트 부착 및 참조 연결 ─────────
		var characterSelectController = characterSelectRootObject.AddComponent<CharacterSelectController>();

		// 미리보기 오브젝트 연결
		SetPrivateField(characterSelectController, "worldPreviewAnimator", previewAnimator);
		SetPrivateField(characterSelectController, "worldPreviewSpriteRenderer", previewSpriteRenderer);
		SetPrivateField(characterSelectController, "previewFallbackImage",
			characterPreviewAreaObject.GetComponentInChildren<Image>());

		// 정보 텍스트 연결
		SetPrivateField(characterSelectController, "characterNameText", characterInfoAreaResult.NameText);
		SetPrivateField(characterSelectController, "conceptDescriptionText", characterInfoAreaResult.ConceptText);
		SetPrivateField(characterSelectController, "attackDescriptionText", characterInfoAreaResult.AttackText);

		// 팝업 연결
		SetPrivateField(characterSelectController, "unavailablePopup", unavailablePopupResult.Popup);
		SetPrivateField(characterSelectController, "unavailableMessageText", unavailablePopupResult.MessageText);

		// 기본 캐릭터 데이터 설정
		SetPrivateField(characterSelectController, "characters", CreateDefaultCharacters());

		// ── 버튼 이벤트 연결 ─────────────────────────────────────────
		var leftArrowButton = leftArrowButtonObject.GetComponent<Button>();
		var rightArrowButton = rightArrowButtonObject.GetComponent<Button>();
		var startButton = startButtonObject.GetComponent<Button>();
		var backButton = backButtonObject.GetComponent<Button>();

		UnityEventTools.AddPersistentListener(leftArrowButton.onClick, characterSelectController.SelectPrevious);
		UnityEventTools.AddPersistentListener(rightArrowButton.onClick, characterSelectController.SelectNext);
		UnityEventTools.AddPersistentListener(startButton.onClick, characterSelectController.OnStartClicked);
		UnityEventTools.AddPersistentListener(backButton.onClick, characterSelectController.OnBackClicked);

		// ── 씬 저장 ─────────────────────────────────────────────────
		Directory.CreateDirectory("Assets/Scenes");
		EditorSceneManager.SaveScene(scene, ScenePath);

		AddSceneToBuildSettings(ScenePath);

		EditorUtility.DisplayDialog("완료",
			$"CharacterSelect 씬이 {ScenePath} 에 생성되었습니다.\n\n" +
			"★ Inspector 추가 연결 항목:\n" +
			"  CharacterSelectRoot → CharacterSelectController → characters 배열:\n" +
			"    각 캐릭터의 previewAnimatorController, previewSprite를\n" +
			"    에셋이 준비된 후 Inspector에서 연결하세요.\n\n" +
			"  CharacterPreviewObject → CharacterPreviewObject의 Animator에\n" +
			"    실제 애니메이터 컨트롤러가 생기면 CharacterData에 연결하세요.\n\n" +
			"현재는 previewFallbackColor로 플레이스홀더가 표시됩니다.",
			"확인");
	}

	// ─────────────────────────────────────────────────────────────────
	//  복합 영역 생성 메서드
	// ─────────────────────────────────────────────────────────────────

	private static GameObject BuildCharacterPreviewArea(Transform canvasParent)
	{
		var previewAreaObject = new GameObject("CharacterPreviewArea");
		var previewAreaTransform = previewAreaObject.AddComponent<RectTransform>();
		previewAreaTransform.SetParent(canvasParent, false);
		previewAreaTransform.anchorMin = new Vector2(0.30f, 0.42f);
		previewAreaTransform.anchorMax = new Vector2(0.70f, 0.86f);
		previewAreaTransform.offsetMin = Vector2.zero;
		previewAreaTransform.offsetMax = Vector2.zero;

		// 테두리 프레임
		var previewFrameObject = CreateUIPanel(previewAreaObject.transform, "PreviewFrame",
			new Color(0.25f, 0.30f, 0.55f, 0.4f));
		SetStretch(previewFrameObject);

		// 플레이스홀더 이미지 (캐릭터 애니메이터가 없을 때 표시)
		var fallbackImageObject = CreateUIPanel(previewAreaObject.transform, "FallbackPreviewImage",
			new Color(0.25f, 0.38f, 0.75f, 1f));
		var fallbackImageTransform = fallbackImageObject.GetComponent<RectTransform>();
		fallbackImageTransform.anchorMin = new Vector2(0.08f, 0.06f);
		fallbackImageTransform.anchorMax = new Vector2(0.92f, 0.94f);
		fallbackImageTransform.offsetMin = Vector2.zero;
		fallbackImageTransform.offsetMax = Vector2.zero;

		return previewAreaObject;
	}

	private struct CharacterInfoAreaResult
	{
		public TMP_Text NameText;
		public TMP_Text ConceptText;
		public TMP_Text AttackText;
	}

	private static CharacterInfoAreaResult BuildCharacterInfoArea(Transform canvasParent)
	{
		var infoAreaObject = new GameObject("CharacterInfoArea");
		var infoAreaTransform = infoAreaObject.AddComponent<RectTransform>();
		infoAreaTransform.SetParent(canvasParent, false);
		infoAreaTransform.anchorMin = new Vector2(0.15f, 0.15f);
		infoAreaTransform.anchorMax = new Vector2(0.85f, 0.42f);
		infoAreaTransform.offsetMin = Vector2.zero;
		infoAreaTransform.offsetMax = Vector2.zero;

		// 정보 영역 배경
		var infoBackgroundObject = CreateUIPanel(infoAreaObject.transform, "InfoBackground",
			new Color(0.08f, 0.10f, 0.20f, 0.85f));
		SetStretch(infoBackgroundObject);

		// 캐릭터 이름
		var nameTextObject = CreateTMPText(infoAreaObject.transform, "CharacterNameText",
			"작사", 44, FontStyles.Bold,
			new Color(0.95f, 0.95f, 1f, 1f));
		var nameTextTransform = nameTextObject.GetComponent<RectTransform>();
		nameTextTransform.anchorMin = new Vector2(0.04f, 0.72f);
		nameTextTransform.anchorMax = new Vector2(0.96f, 1.0f);
		nameTextTransform.offsetMin = Vector2.zero;
		nameTextTransform.offsetMax = Vector2.zero;
		nameTextObject.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.MidlineLeft;

		// 구분선
		CreateDecorationLine(infoAreaObject.transform, "NameDivider", 0.70f,
			new Color(0.3f, 0.5f, 0.9f, 0.5f));

		// 컨셉 설명
		var conceptTextObject = CreateTMPText(infoAreaObject.transform, "ConceptDescriptionText",
			"패의 조합으로 강력한 공격을 연출하는 전투 전문가.", 30, FontStyles.Normal,
			new Color(0.82f, 0.85f, 1f, 0.9f));
		var conceptTextTransform = conceptTextObject.GetComponent<RectTransform>();
		conceptTextTransform.anchorMin = new Vector2(0.04f, 0.38f);
		conceptTextTransform.anchorMax = new Vector2(0.96f, 0.68f);
		conceptTextTransform.offsetMin = Vector2.zero;
		conceptTextTransform.offsetMax = Vector2.zero;
		var conceptText = conceptTextObject.GetComponent<TMP_Text>();
		conceptText.alignment = TextAlignmentOptions.TopLeft;
		conceptText.enableWordWrapping = true;

		// 공격 스타일 설명
		var attackTextObject = CreateTMPText(infoAreaObject.transform, "AttackDescriptionText",
			"패를 투척해 적을 공격한다. 패를 조합해 연속 공격과 광역기를 발동한다.", 28, FontStyles.Italic,
			new Color(0.65f, 0.78f, 1f, 0.85f));
		var attackTextTransform = attackTextObject.GetComponent<RectTransform>();
		attackTextTransform.anchorMin = new Vector2(0.04f, 0.04f);
		attackTextTransform.anchorMax = new Vector2(0.96f, 0.36f);
		attackTextTransform.offsetMin = Vector2.zero;
		attackTextTransform.offsetMax = Vector2.zero;
		var attackText = attackTextObject.GetComponent<TMP_Text>();
		attackText.alignment = TextAlignmentOptions.TopLeft;
		attackText.enableWordWrapping = true;

		return new CharacterInfoAreaResult
		{
			NameText = nameTextObject.GetComponent<TMP_Text>(),
			ConceptText = conceptText,
			AttackText = attackText,
		};
	}

	private struct UnavailablePopupResult
	{
		public SimplePopup Popup;
		public TMP_Text MessageText;
	}

	private static UnavailablePopupResult BuildUnavailablePopup(Transform canvasParent)
	{
		var popupObject = CreateUIPanel(canvasParent, "UnavailablePopup",
			new Color(0.08f, 0.09f, 0.18f, 0.97f));
		CenterPopup(popupObject, 500, 280);
		var simplePopup = popupObject.AddComponent<SimplePopup>();

		// 제목
		var titleTextObject = CreateTMPText(popupObject.transform, "TitleText",
			"알림", 38, FontStyles.Bold, Color.white);
		var titleTextTransform = titleTextObject.GetComponent<RectTransform>();
		titleTextTransform.anchorMin = new Vector2(0, 0.72f);
		titleTextTransform.anchorMax = new Vector2(1, 1f);
		titleTextTransform.offsetMin = Vector2.zero;
		titleTextTransform.offsetMax = Vector2.zero;
		titleTextObject.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

		// 메시지 텍스트 (CharacterSelectController가 내용을 채워 넣음)
		var messageTextObject = CreateTMPText(popupObject.transform, "MessageText",
			"아직 개발되지 않음", 26, FontStyles.Normal,
			new Color(0.82f, 0.85f, 1f, 1f));
		var messageTextTransform = messageTextObject.GetComponent<RectTransform>();
		messageTextTransform.anchorMin = new Vector2(0.06f, 0.32f);
		messageTextTransform.anchorMax = new Vector2(0.94f, 0.70f);
		messageTextTransform.offsetMin = Vector2.zero;
		messageTextTransform.offsetMax = Vector2.zero;
		var messageText = messageTextObject.GetComponent<TMP_Text>();
		messageText.alignment = TextAlignmentOptions.Center;
		messageText.enableWordWrapping = true;

		// 닫기 버튼
		var closeButtonObject = CreateMenuButton(popupObject.transform, "CloseButton", "✕  닫기");
		var closeButtonTransform = closeButtonObject.GetComponent<RectTransform>();
		closeButtonTransform.anchorMin = new Vector2(0.28f, 0.05f);
		closeButtonTransform.anchorMax = new Vector2(0.72f, 0.26f);
		closeButtonTransform.offsetMin = Vector2.zero;
		closeButtonTransform.offsetMax = Vector2.zero;
		UnityEventTools.AddPersistentListener(closeButtonObject.GetComponent<Button>().onClick, simplePopup.Close);

		popupObject.SetActive(false);

		return new UnavailablePopupResult
		{
			Popup = simplePopup,
			MessageText = messageText,
		};
	}

	private static CharacterData[] CreateDefaultCharacters()
	{
		return new CharacterData[]
		{
			new CharacterData
			{
				characterType = CharacterType.Mahjong,
				displayName = "작사",
				conceptDescription = "패의 조합으로 강력한 공격을 연출하는 전투 전문가.",
				attackDescription = "패를 투척해 적을 공격한다. 패를 조합해 연속 공격과 광역기를 발동한다.",
				isAvailable = true,
				unavailableMessage = "아직 개발되지 않음",
				previewFallbackColor = new Color(0.25f, 0.38f, 0.75f, 1f),
			},
			new CharacterData
			{
				characterType = CharacterType.Holdem,
				displayName = "프로 포커 플레이어",
				conceptDescription = "카드 배합으로 다양한 기술을 구사하는 전략가.",
				attackDescription = "카드를 날려 적을 베거나 묶는다. 패의 조합에 따라 발동되는 기술이 달라진다.",
				isAvailable = false,
				unavailableMessage = "아직 개발되지 않음",
				previewFallbackColor = new Color(0.55f, 0.20f, 0.75f, 1f),
			},
			new CharacterData
			{
				characterType = CharacterType.Dice,
				displayName = "도박꾼",
				conceptDescription = "눈금에 따라 달라지는 결과로 적을 제압하는 한탕주의자.",
				attackDescription = "굴려 나온 눈금만큼 위력이 변하는 공격을 날린다. 운에 따라 대박이 터진다.",
				isAvailable = false,
				unavailableMessage = "아직 개발되지 않음",
				previewFallbackColor = new Color(0.20f, 0.60f, 0.45f, 1f),
			},
		};
	}

	// ─────────────────────────────────────────────────────────────────
	//  헬퍼 메서드
	// ─────────────────────────────────────────────────────────────────

	private static GameObject CreateUIPanel(Transform parent, string name, Color color)
	{
		var panel = new GameObject(name);
		var rectTransform = panel.AddComponent<RectTransform>();
		rectTransform.SetParent(parent, false);
		var image = panel.AddComponent<Image>();
		image.color = color;
		return panel;
	}

	private static void SetStretch(GameObject target)
	{
		var rectTransform = target.GetComponent<RectTransform>();
		rectTransform.anchorMin = Vector2.zero;
		rectTransform.anchorMax = Vector2.one;
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
	}

	private static void CreateDecorationLine(Transform parent, string name,
		float verticalPosition, Color color)
	{
		var lineObject = CreateUIPanel(parent, name, color);
		var rectTransform = lineObject.GetComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, verticalPosition - 0.002f);
		rectTransform.anchorMax = new Vector2(1f, verticalPosition + 0.002f);
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
	}

	private const string FontRegularPath = "Assets/TextMesh Pro/Fonts/Mona12.asset";
	private const string FontBoldPath    = "Assets/TextMesh Pro/Fonts/Mona12-Bold.asset";

	private static TMP_FontAsset LoadFont(FontStyles style)
	{
		var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontRegularPath);
		if (font == null)
			Debug.LogWarning($"[CharacterSelectSceneBuilder] 폰트 에셋을 찾을 수 없습니다: {FontRegularPath}");
		return font;
	}

	private static GameObject CreateTMPText(Transform parent, string name, string text,
		float size, FontStyles style, Color color)
	{
		var textObject = new GameObject(name);
		var rectTransform = textObject.AddComponent<RectTransform>();
		rectTransform.SetParent(parent, false);
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
		var buttonObject = new GameObject(name);
		var rectTransform = buttonObject.AddComponent<RectTransform>();
		rectTransform.SetParent(parent, false);

		var backgroundImage = buttonObject.AddComponent<Image>();
		backgroundImage.color = new Color(0.15f, 0.18f, 0.35f, 0.9f);

		var button = buttonObject.AddComponent<Button>();
		var colorBlock = button.colors;
		colorBlock.normalColor = new Color(0.15f, 0.18f, 0.35f, 0.9f);
		colorBlock.highlightedColor = new Color(0.28f, 0.35f, 0.70f, 1f);
		colorBlock.pressedColor = new Color(0.10f, 0.12f, 0.25f, 1f);
		colorBlock.selectedColor = new Color(0.28f, 0.35f, 0.70f, 1f);
		button.colors = colorBlock;
		button.targetGraphic = backgroundImage;

		var labelObject = CreateTMPText(buttonObject.transform, "Label", label,
			45, FontStyles.Bold, new Color(0.92f, 0.92f, 1f, 1f));
		SetStretch(labelObject);
		var labelText = labelObject.GetComponent<TMP_Text>();
		labelText.alignment = TextAlignmentOptions.Midline;

		return buttonObject;
	}

	private static GameObject CreateArrowButton(Transform parent, string name, string arrowSymbol)
	{
		var buttonObject = new GameObject(name);
		var rectTransform = buttonObject.AddComponent<RectTransform>();
		rectTransform.SetParent(parent, false);

		var backgroundImage = buttonObject.AddComponent<Image>();
		backgroundImage.color = new Color(0.15f, 0.18f, 0.35f, 0.7f);

		var button = buttonObject.AddComponent<Button>();
		var colorBlock = button.colors;
		colorBlock.normalColor = new Color(0.15f, 0.18f, 0.35f, 0.7f);
		colorBlock.highlightedColor = new Color(0.28f, 0.35f, 0.70f, 1f);
		colorBlock.pressedColor = new Color(0.10f, 0.12f, 0.25f, 1f);
		colorBlock.selectedColor = new Color(0.28f, 0.35f, 0.70f, 1f);
		button.colors = colorBlock;
		button.targetGraphic = backgroundImage;

		var labelObject = CreateTMPText(buttonObject.transform, "Label", arrowSymbol,
			56, FontStyles.Bold, new Color(0.92f, 0.92f, 1f, 1f));
		SetStretch(labelObject);
		labelObject.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Midline;

		return buttonObject;
	}

	private static void CenterPopup(GameObject target, float width, float height)
	{
		var rectTransform = target.GetComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
		rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
		rectTransform.pivot = new Vector2(0.5f, 0.5f);
		rectTransform.sizeDelta = new Vector2(width, height);
		rectTransform.anchoredPosition = Vector2.zero;
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
			Debug.LogWarning($"[CharacterSelectSceneBuilder] Field '{fieldName}' not found on {target.GetType().Name}");
	}

	private static void AddSceneToBuildSettings(string path)
	{
		var existingScenes = EditorBuildSettings.scenes;

		foreach (var existingScene in existingScenes)
		{
			if (existingScene.path == path)
				return;
		}

		var updatedScenes = new EditorBuildSettingsScene[existingScenes.Length + 1];
		for (int i = 0; i < existingScenes.Length; i++)
			updatedScenes[i] = existingScenes[i];
		updatedScenes[existingScenes.Length] = new EditorBuildSettingsScene(path, true);

		EditorBuildSettings.scenes = updatedScenes;
	}
}
