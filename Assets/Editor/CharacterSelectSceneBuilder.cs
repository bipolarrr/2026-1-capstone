// Assets/Editor/CharacterSelectSceneBuilder.cs
// Unity 메뉴 → Tools → Build CharacterSelect Scene 으로 씬을 자동 생성합니다.
// 씬이 이미 존재하면 덮어쓸지 물어봅니다.

using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;
using SBU = SceneBuilderUtility;

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
		var backgroundObject = SBU.CreateUIPanel(canvasObject.transform, "Background",
			new Color(0.06f, 0.07f, 0.13f, 1f));
		SetStretch(backgroundObject);

		var topGradientObject = SBU.CreateUIPanel(backgroundObject.transform, "BackgroundTopGradient",
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
		var titleTmp = SBU.CreateTMPText(canvasObject.transform, "TitleText",
			"캐릭터 선택", 60, new Color(0.95f, 0.95f, 1f, 1f),
			TextAlignmentOptions.Center, FontStyles.Bold);
		var titleTextTransform = titleTmp.GetComponent<RectTransform>();
		titleTextTransform.anchorMin = new Vector2(0.1f, 0.88f);
		titleTextTransform.anchorMax = new Vector2(0.9f, 1.0f);
		titleTextTransform.offsetMin = Vector2.zero;
		titleTextTransform.offsetMax = Vector2.zero;

		// ── 좌측 화살표 버튼 ─────────────────────────────────────────
		var leftArrowButtonObject = CreateArrowButton(canvasObject.transform, "LeftArrowButton", "◁");
		var leftArrowButtonTransform = leftArrowButtonObject.GetComponent<RectTransform>();
		leftArrowButtonTransform.anchorMin = new Vector2(0.46f, 0.55f);
		leftArrowButtonTransform.anchorMax = new Vector2(0.51f, 0.75f);
		leftArrowButtonTransform.offsetMin = Vector2.zero;
		leftArrowButtonTransform.offsetMax = Vector2.zero;

		// ── 우측 화살표 버튼 ─────────────────────────────────────────
		var rightArrowButtonObject = CreateArrowButton(canvasObject.transform, "RightArrowButton", "▷");
		var rightArrowButtonTransform = rightArrowButtonObject.GetComponent<RectTransform>();
		rightArrowButtonTransform.anchorMin = new Vector2(0.89f, 0.55f);
		rightArrowButtonTransform.anchorMax = new Vector2(0.94f, 0.75f);
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

		// ── 주사위 규칙 간략 설명 ────────────────────────────────────────
		var diceRulesArea = BuildDiceRulesArea(canvasObject.transform);

		// ── 주사위 세부 규칙 팝업 ─────────────────────────────────────────
		var rulesDimmer = SBU.CreateDimmer(canvasObject.transform, "RulesDimmer");
		var rulesPopup = BuildDiceRulesPopup(canvasObject.transform);
		SetPrivateField(rulesPopup.GetComponent<SimplePopup>(), "dimmer",
			rulesDimmer.GetComponent<Image>());

		// 세부 규칙 버튼 → 팝업 열기
		UnityEventTools.AddPersistentListener(
			diceRulesArea.DetailButton.onClick,
			rulesPopup.GetComponent<SimplePopup>().Open);

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

		// 규칙 패널 연결
		SetPrivateField(characterSelectController, "rulesTitle", diceRulesArea.TitleText);
		SetPrivateField(characterSelectController, "rulesBody", diceRulesArea.BodyText);
		SetPrivateField(characterSelectController, "rulesDetailButton",
			diceRulesArea.DetailButton.gameObject);

		// 기본 캐릭터 데이터 설정
		string diceExplSpritePath = "Assets/Mobs/DiceGambler_explanation_sample.png";
		SBU.EnsureTightSprite(diceExplSpritePath);
		var diceExplSprite = AssetDatabase.LoadAssetAtPath<Sprite>(diceExplSpritePath);
		var defaultCharacters = CreateDefaultCharacters();
		if (diceExplSprite != null)
			defaultCharacters[2].previewSprite = diceExplSprite;
		SetPrivateField(characterSelectController, "characters", defaultCharacters);

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
		previewAreaTransform.anchorMin = new Vector2(0.52f, 0.42f);
		previewAreaTransform.anchorMax = new Vector2(0.88f, 0.86f);
		previewAreaTransform.offsetMin = Vector2.zero;
		previewAreaTransform.offsetMax = Vector2.zero;

		// 플레이스홀더 이미지 (캐릭터 애니메이터가 없을 때 표시)
		var fallbackImageObject = SBU.CreateUIPanel(previewAreaObject.transform, "FallbackPreviewImage",
			new Color(0.25f, 0.38f, 0.75f, 1f));
		var fallbackImageTransform = fallbackImageObject.GetComponent<RectTransform>();
		fallbackImageTransform.anchorMin = new Vector2(0.08f, 0.06f);
		fallbackImageTransform.anchorMax = new Vector2(0.92f, 0.94f);
		fallbackImageTransform.offsetMin = Vector2.zero;
		fallbackImageTransform.offsetMax = Vector2.zero;
		var fallbackImg = fallbackImageObject.GetComponent<Image>();
		fallbackImg.preserveAspect = true;
		fallbackImg.useSpriteMesh = true;

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
		var infoBackgroundObject = SBU.CreateUIPanel(infoAreaObject.transform, "InfoBackground",
			new Color(0.08f, 0.10f, 0.20f, 0.85f));
		SetStretch(infoBackgroundObject);

		// 캐릭터 이름
		var nameTmp = SBU.CreateTMPText(infoAreaObject.transform, "CharacterNameText",
			"작사", 44, new Color(0.95f, 0.95f, 1f, 1f),
			TextAlignmentOptions.MidlineLeft, FontStyles.Bold);
		var nameTextTransform = nameTmp.GetComponent<RectTransform>();
		nameTextTransform.anchorMin = new Vector2(0.04f, 0.72f);
		nameTextTransform.anchorMax = new Vector2(0.96f, 1.0f);
		nameTextTransform.offsetMin = Vector2.zero;
		nameTextTransform.offsetMax = Vector2.zero;

		// 구분선
		CreateDecorationLine(infoAreaObject.transform, "NameDivider", 0.70f,
			new Color(0.3f, 0.5f, 0.9f, 0.5f));

		// 컨셉 설명
		var conceptText = SBU.CreateTMPText(infoAreaObject.transform, "ConceptDescriptionText",
			"패의 조합으로 강력한 공격을 연출하는 전투 전문가.", 30,
			new Color(0.82f, 0.85f, 1f, 0.9f), TextAlignmentOptions.TopLeft);
		conceptText.textWrappingMode = TextWrappingModes.Normal;
		var conceptTextTransform = conceptText.GetComponent<RectTransform>();
		conceptTextTransform.anchorMin = new Vector2(0.04f, 0.38f);
		conceptTextTransform.anchorMax = new Vector2(0.96f, 0.68f);
		conceptTextTransform.offsetMin = Vector2.zero;
		conceptTextTransform.offsetMax = Vector2.zero;

		// 공격 스타일 설명
		var attackText = SBU.CreateTMPText(infoAreaObject.transform, "AttackDescriptionText",
			"패를 투척해 적을 공격한다. 패를 조합해 연속 공격과 광역기를 발동한다.", 28,
			new Color(0.65f, 0.78f, 1f, 0.85f), TextAlignmentOptions.TopLeft, FontStyles.Italic);
		attackText.textWrappingMode = TextWrappingModes.Normal;
		var attackTextTransform = attackText.GetComponent<RectTransform>();
		attackTextTransform.anchorMin = new Vector2(0.04f, 0.04f);
		attackTextTransform.anchorMax = new Vector2(0.96f, 0.36f);
		attackTextTransform.offsetMin = Vector2.zero;
		attackTextTransform.offsetMax = Vector2.zero;

		return new CharacterInfoAreaResult
		{
			NameText = nameTmp,
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
		var popupObject = SBU.CreateUIPanel(canvasParent, "UnavailablePopup",
			new Color(0.08f, 0.09f, 0.18f, 0.97f));
		CenterPopup(popupObject, 500, 280);
		var simplePopup = popupObject.AddComponent<SimplePopup>();

		// 제목
		var titleTmp = SBU.CreateTMPText(popupObject.transform, "TitleText",
			"알림", 38, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
		var titleTextTransform = titleTmp.GetComponent<RectTransform>();
		titleTextTransform.anchorMin = new Vector2(0, 0.72f);
		titleTextTransform.anchorMax = new Vector2(1, 1f);
		titleTextTransform.offsetMin = Vector2.zero;
		titleTextTransform.offsetMax = Vector2.zero;

		// 메시지 텍스트 (CharacterSelectController가 내용을 채워 넣음)
		var messageText = SBU.CreateTMPText(popupObject.transform, "MessageText",
			"아직 개발되지 않음", 26, new Color(0.82f, 0.85f, 1f, 1f),
			TextAlignmentOptions.Center);
		messageText.textWrappingMode = TextWrappingModes.Normal;
		var messageTextTransform = messageText.GetComponent<RectTransform>();
		messageTextTransform.anchorMin = new Vector2(0.06f, 0.32f);
		messageTextTransform.anchorMax = new Vector2(0.94f, 0.70f);
		messageTextTransform.offsetMin = Vector2.zero;
		messageTextTransform.offsetMax = Vector2.zero;

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

	private struct DiceRulesAreaResult
	{
		public Button DetailButton;
		public TMP_Text TitleText;
		public TMP_Text BodyText;
	}

	private static DiceRulesAreaResult BuildDiceRulesArea(Transform canvasParent)
	{
		var area = new GameObject("DiceRulesArea");
		var areaRect = area.AddComponent<RectTransform>();
		areaRect.SetParent(canvasParent, false);
		areaRect.anchorMin = new Vector2(0.05f, 0.42f);
		areaRect.anchorMax = new Vector2(0.44f, 0.86f);
		areaRect.offsetMin = Vector2.zero;
		areaRect.offsetMax = Vector2.zero;

		// 배경
		var bg = SBU.CreateUIPanel(area.transform, "RulesBg", new Color(0.08f, 0.10f, 0.20f, 0.85f));
		SetStretch(bg);

		// 제목
		var titleTmp = SBU.CreateTMPText(area.transform, "RulesTitle",
			"주사위 전투 규칙", 26, new Color(0.95f, 0.95f, 1f, 1f),
			TextAlignmentOptions.Center, FontStyles.Bold);
		var titleRt = titleTmp.GetComponent<RectTransform>();
		titleRt.anchorMin = new Vector2(0.05f, 0.88f);
		titleRt.anchorMax = new Vector2(0.95f, 0.98f);
		titleRt.offsetMin = Vector2.zero;
		titleRt.offsetMax = Vector2.zero;

		// 간략 설명
		var bodyLines = new string[]
		{
			"5개의 주사위를 굴려",
			"눈의 합으로 데미지를 준다.",
			"",
			"<color=#FFD94A>족보를 맞추면</color>",
			"<color=#FFD94A>고정 데미지 + 광역 50%!</color>",
			"",
			"<color=#AAAAAA>· Small Straight</color>  4연속",
			"<color=#AAAAAA>· Full House</color>  2+3",
			"<color=#AAAAAA>· Large Straight</color>  5연속",
			"<color=#AAAAAA>· 4 of a Kind</color>  4개 동일",
			"<color=#FFD94A>· YACHT</color>  5개 동일!",
		};
		var bodyTmp = SBU.CreateTMPText(area.transform, "RulesBody",
			string.Join("\n", bodyLines), 20,
			new Color(0.82f, 0.85f, 1f, 0.95f), TextAlignmentOptions.TopLeft);
		bodyTmp.textWrappingMode = TextWrappingModes.Normal;
		bodyTmp.richText = true;
		var bodyRt = bodyTmp.GetComponent<RectTransform>();
		bodyRt.anchorMin = new Vector2(0.06f, 0.18f);
		bodyRt.anchorMax = new Vector2(0.94f, 0.86f);
		bodyRt.offsetMin = Vector2.zero;
		bodyRt.offsetMax = Vector2.zero;

		// 세부 규칙 버튼
		var detailBtn = CreateMenuButton(area.transform, "DetailButton", "세부 규칙 ▷");
		detailBtn.GetComponentInChildren<TMP_Text>().fontSize = 22;
		var detailRt = detailBtn.GetComponent<RectTransform>();
		detailRt.anchorMin = new Vector2(0.10f, 0.03f);
		detailRt.anchorMax = new Vector2(0.90f, 0.15f);
		detailRt.offsetMin = Vector2.zero;
		detailRt.offsetMax = Vector2.zero;

		return new DiceRulesAreaResult
		{
			DetailButton = detailBtn.GetComponent<Button>(),
			TitleText = titleTmp,
			BodyText = bodyTmp,
		};
	}

	private static GameObject BuildDiceRulesPopup(Transform canvasParent)
	{
		var popup = SBU.CreateUIPanel(canvasParent, "DiceRulesPopup",
			new Color(0.08f, 0.09f, 0.18f, 0.97f));
		CenterPopup(popup, 620, 480);
		popup.AddComponent<SimplePopup>();

		// 제목
		var titleTmp = SBU.CreateTMPText(popup.transform, "Title", "주사위 족보 & 데미지",
			36, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
		var titleRt = titleTmp.GetComponent<RectTransform>();
		titleRt.anchorMin = new Vector2(0, 0.86f);
		titleRt.anchorMax = new Vector2(1, 0.98f);
		titleRt.offsetMin = Vector2.zero;
		titleRt.offsetMax = Vector2.zero;

		// 세부 규칙 본문
		var detailLines = new string[]
		{
			"<color=#AAAAAA>족보 없음</color>          주사위 합계 데미지 (광역 없음)",
			"",
			"<color=#AAAAAA>Small Straight</color>    4개 연속 (1-2-3-4 등)",
			"                                  → <color=#FFD94A>20</color> 데미지",
			"",
			"<color=#AAAAAA>Full House</color>          2개+3개 조합 (A,A,B,B,B)",
			"                                  → <color=#FFD94A>25</color> 데미지",
			"",
			"<color=#AAAAAA>Large Straight</color>   5개 연속",
			"     1-2-3-4-5 → <color=#FFD94A>30</color>  /  2-3-4-5-6 → <color=#FFD94A>35</color>",
			"",
			"<color=#AAAAAA>4 of a Kind</color>        4개 동일 (A,A,A,A,B)",
			"                                  → <color=#FFD94A>40</color> 데미지",
			"",
			"<color=#FFD94A>YACHT</color>                 5개 모두 동일!",
			"                                  → <color=#FF5555>50</color> 데미지",
			"",
			"<color=#55AAFF>족보 발동 시 타겟 100% + 나머지 적 50% 광역</color>",
		};
		var bodyTmp = SBU.CreateTMPText(popup.transform, "DetailBody",
			string.Join("\n", detailLines), 20,
			new Color(0.88f, 0.90f, 1f, 1f), TextAlignmentOptions.TopLeft);
		bodyTmp.textWrappingMode = TextWrappingModes.Normal;
		bodyTmp.richText = true;
		var bodyRt = bodyTmp.GetComponent<RectTransform>();
		bodyRt.anchorMin = new Vector2(0.05f, 0.14f);
		bodyRt.anchorMax = new Vector2(0.95f, 0.84f);
		bodyRt.offsetMin = Vector2.zero;
		bodyRt.offsetMax = Vector2.zero;

		// 닫기 버튼 (좌상단 X, 호버 시 빨간색)
		var closeBtn = new GameObject("CloseButton");
		var closeBtnRt = closeBtn.AddComponent<RectTransform>();
		closeBtnRt.SetParent(popup.transform, false);
		closeBtnRt.anchorMin = new Vector2(0f, 0.88f);
		closeBtnRt.anchorMax = new Vector2(0.10f, 1f);
		closeBtnRt.offsetMin = Vector2.zero;
		closeBtnRt.offsetMax = Vector2.zero;

		var closeBtnImg = closeBtn.AddComponent<Image>();
		closeBtnImg.color = new Color(0, 0, 0, 0);

		var closeBtnComp = closeBtn.AddComponent<Button>();
		var closeBtnColors = closeBtnComp.colors;
		closeBtnColors.normalColor = new Color(0, 0, 0, 0);
		closeBtnColors.highlightedColor = new Color(0, 0, 0, 0);
		closeBtnColors.pressedColor = new Color(0, 0, 0, 0);
		closeBtnColors.selectedColor = new Color(0, 0, 0, 0);
		closeBtnComp.colors = closeBtnColors;
		closeBtnComp.targetGraphic = closeBtnImg;

		var closeLabelTmp = SBU.CreateTMPText(closeBtn.transform, "Label", "✕",
			32, new Color(0.7f, 0.7f, 0.7f, 1f), TextAlignmentOptions.Center, FontStyles.Bold);
		SBU.Stretch(closeLabelTmp.GetComponent<RectTransform>());

		// 호버 시 빨간색 전환
		var hoverEffect = closeBtn.AddComponent<UIHoverEffect>();
		SetPrivateField(hoverEffect, "targetText", closeLabelTmp);
		SetPrivateField(hoverEffect, "normalColor", new Color(0.7f, 0.7f, 0.7f, 1f));
		SetPrivateField(hoverEffect, "hoverColor", new Color(1f, 0.2f, 0.2f, 1f));
		SetPrivateField(hoverEffect, "fontSizeBoost", 0f);
		SetPrivateField(hoverEffect, "scaleFactor", 1f);
		SetPrivateField(hoverEffect, "outlineColor", Color.clear);
		SetPrivateField(hoverEffect, "shadowColor", Color.clear);

		var popupComp = popup.GetComponent<SimplePopup>();
		UnityEventTools.AddPersistentListener(closeBtnComp.onClick, popupComp.Close);

		popup.SetActive(false);
		return popup;
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
				isAvailable = false,
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
				isAvailable = true,
				unavailableMessage = "아직 개발되지 않음",
				previewFallbackColor = new Color(0f, 0f, 0f, 0f),
			},
		};
	}

	// ─────────────────────────────────────────────────────────────────
	//  헬퍼 메서드
	// ─────────────────────────────────────────────────────────────────

	private static void SetStretch(GameObject target)
		=> SBU.Stretch(target.GetComponent<RectTransform>());

	private static void CreateDecorationLine(Transform parent, string name,
		float verticalPosition, Color color)
	{
		var lineObject = SBU.CreateUIPanel(parent, name, color);
		var rectTransform = lineObject.GetComponent<RectTransform>();
		rectTransform.anchorMin = new Vector2(0f, verticalPosition - 0.002f);
		rectTransform.anchorMax = new Vector2(1f, verticalPosition + 0.002f);
		rectTransform.offsetMin = Vector2.zero;
		rectTransform.offsetMax = Vector2.zero;
	}

	private static GameObject CreateMenuButton(Transform parent, string name, string label)
	{
		var go = SBU.CreateButton(parent, name, label,
			45, SBU.ButtonNormal, SBU.ButtonHighlight, SBU.ButtonPressed);
		go.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Midline;
		return go;
	}

	private static GameObject CreateArrowButton(Transform parent, string name, string arrowSymbol)
	{
		var arrowNormal = new Color(SBU.ButtonNormal.r, SBU.ButtonNormal.g,
			SBU.ButtonNormal.b, 0.7f);
		var go = SBU.CreateButton(parent, name, arrowSymbol,
			56, arrowNormal, SBU.ButtonHighlight, SBU.ButtonPressed);
		go.GetComponentInChildren<TMP_Text>().alignment = TextAlignmentOptions.Midline;
		return go;
	}

	private static void CenterPopup(GameObject target, float width, float height)
		=> SBU.CenterPopup(target, width, height);

	private static void SetPrivateField(object target, string fieldName, object value)
		=> SBU.SetField(target, fieldName, value);

	private static void AddSceneToBuildSettings(string path)
		=> SBU.AddSceneToBuildSettings(path);
}
