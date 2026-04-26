using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor.Events;

public static class GameExploreSceneBuilder
{
	// ── 색상 팔레트 ──
	static readonly Color BgColor = new Color(0.10f, 0.10f, 0.18f);
	static readonly Color PanelBg = new Color(0.12f, 0.12f, 0.22f, 0.95f);
	static readonly Color PlayerColor = new Color(0.55f, 0.85f, 0.65f);
	static readonly Color AccentYellow = new Color(1f, 0.85f, 0.3f);
	static readonly Color HpBarBg = new Color(0.15f, 0.15f, 0.15f);
	static readonly Color HpBarFill = new Color(0.3f, 0.85f, 0.35f);
	static readonly Color EnemyHpFill = new Color(0.85f, 0.25f, 0.25f);
	static readonly Color ItemCardBg = new Color(0.18f, 0.22f, 0.38f, 0.95f);

	[MenuItem("Tools/Build GameExplore Scene")]
	public static void BuildScene()
	{
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		// ── 카메라 ──
		var camGo = new GameObject("MainCamera");
		camGo.tag = "MainCamera";
		var cam = camGo.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = 5;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = BgColor;

		// ── 캔버스 ──
		var canvasGo = new GameObject("Canvas");
		var canvas = canvasGo.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = 0;
		var scaler = canvasGo.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		canvasGo.AddComponent<GraphicRaycaster>();

		// ── 배경 ──
		// 마스크 컨테이너: 전체 화면을 덮되 이미지는 하단 기준으로 배치 → 위쪽이 잘려 하단부만 보임
		var bgMask = CreateEmpty(canvasGo, "BackgroundMask");
		Stretch(bgMask);
		bgMask.gameObject.AddComponent<RectMask2D>();

		var bgImg = CreateImage(bgMask.gameObject, "Background", Color.white);
		// 기본 스테이지(보통 Stage 1 Forest)의 배경을 시작값으로 사용 — 런타임 ApplyStageVisuals가 활성 스테이지에 맞게 교체
		var defaultStage = StageRegistry.DefaultStage;
		var defaultBundle = defaultStage != null ? SceneBuilderUtility.BuildStageBundle(defaultStage) : null;
		Sprite bgSprite = defaultBundle != null ? defaultBundle.background : null;
		float bgAspect = 16f / 9f;
		if (bgSprite != null && bgSprite.texture != null)
			bgAspect = (float)bgSprite.texture.width / bgSprite.texture.height;
		float bgImgHeight = 1920f / bgAspect;
		bgImg.anchorMin = new Vector2(0f, 0f);
		bgImg.anchorMax = new Vector2(1f, 0f);
		bgImg.pivot = new Vector2(0.5f, 0f);
		bgImg.offsetMin = new Vector2(0f, 0f);
		bgImg.offsetMax = new Vector2(0f, bgImgHeight);
		var bgImageComp = bgImg.GetComponent<Image>();
		if (bgSprite != null)
			bgImageComp.sprite = bgSprite;
		else if (defaultStage != null)
			bgImageComp.color = defaultStage.themeColor;
		else
			bgImageComp.color = BgColor;

		// ── 지면 기준선 (배경 흙길 높이) ──
		const float GroundY = 0.12f;
		var groundLine = CreateEmpty(canvasGo, "GroundLine");
		groundLine.anchorMin = new Vector2(0f, GroundY);
		groundLine.anchorMax = new Vector2(1f, GroundY);
		groundLine.offsetMin = Vector2.zero;
		groundLine.offsetMax = Vector2.zero;

		// ── 플레이어 캐릭터 (정적 스프라이트 + 호흡 애니메이션) ──
		string idlePath = "Assets/Player/IdleSprites/0.png";
		EnsurePixelSprite(idlePath);
		Sprite idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(idlePath);

		var playerBody = CreateImage(canvasGo, "PlayerBody", Color.white);
		playerBody.pivot = new Vector2(0.5f, 0f);               // 피벗 하단 → 발 기준 배치
		playerBody.anchorMin = new Vector2(0.19f, GroundY);      // 단일 앵커점 (지면)
		playerBody.anchorMax = new Vector2(0.19f, GroundY);
		playerBody.sizeDelta = new Vector2(150f, 150f);          // 기본 크기 (px)
		playerBody.localScale = new Vector3(2f, 2f, 1f);        // 스케일 2배
		playerBody.localEulerAngles = new Vector3(0f, 180f, 0f); // 좌우 반전 (Y축 회전)
		var playerImg = playerBody.GetComponent<Image>();
		playerImg.preserveAspect = true;
		playerImg.useSpriteMesh = false;
		if (idleSprite != null)
			playerImg.sprite = idleSprite;

		// SpriteAnimator: Y축 스케일 호흡 애니메이션
		var spriteAnim = playerBody.gameObject.AddComponent<SpriteAnimator>();
		SetField(spriteAnim, "amplitude", 0.03f);
		SetField(spriteAnim, "speed", 2f);

		var playerNameTxt = CreateTMPText(canvasGo, "PlayerName", "다람쥐(주사위)",
			30, Color.white, TextAlignmentOptions.Center);
		var playerNameTmpUGUI = playerNameTxt as TextMeshProUGUI;
		playerNameTmpUGUI.fontSharedMaterial = GetOrCreateOutlineMaterial(playerNameTmpUGUI.font);
		var pnRt = playerNameTxt.GetComponent<RectTransform>();
		pnRt.anchorMin = new Vector2(0.10f, GroundY + 0.22f);   // 머리 바로 위
		pnRt.anchorMax = new Vector2(0.28f, GroundY + 0.27f);
		pnRt.offsetMin = Vector2.zero;
		pnRt.offsetMax = Vector2.zero;

		// ── HUD ──
		var hudParent = CreateEmpty(canvasGo, "HUD");
		Stretch(hudParent);

		var heartTextObj = CreateTMPText(hudParent.gameObject, "PlayerHeartText", "● ● ● ● ●",
			48, new Color(1f, 0.13f, 0.13f), TextAlignmentOptions.Left);
		heartTextObj.richText = true;
		var heartRt = heartTextObj.GetComponent<RectTransform>();
		heartRt.anchorMin = new Vector2(0.02f, 0.86f);
		heartRt.anchorMax = new Vector2(0.40f, 0.97f);
		heartRt.offsetMin = Vector2.zero;
		heartRt.offsetMax = Vector2.zero;

		var heartDisplayComp = heartTextObj.gameObject.AddComponent<HeartDisplay>();
		SetField(heartDisplayComp, "heartText", heartTextObj);

		var pupText = CreateTMPText(hudParent.gameObject, "PowerUpText", "",
			22, AccentYellow, TextAlignmentOptions.Left);
		var pupRt = pupText.GetComponent<RectTransform>();
		pupRt.anchorMin = new Vector2(0.02f, 0.84f);
		pupRt.anchorMax = new Vector2(0.40f, 0.88f);
		pupRt.offsetMin = Vector2.zero;
		pupRt.offsetMax = Vector2.zero;

		// ── 전투 조우 오버레이 (팝업 없이, 배경 위에 직접 표시) ──
		var combatGroup = CreateEmpty(canvasGo, "CombatGroup");
		Stretch(combatGroup);
		var combatGroupCG = combatGroup.gameObject.AddComponent<CanvasGroup>();
		combatGroupCG.alpha = 0f;
		combatGroupCG.blocksRaycasts = false;
		combatGroupCG.interactable = false;

		// 조우 타이틀 (배경 상단)
		var encounterTitle = CreateTMPText(combatGroup.gameObject, "EncounterTitle", "적을 만났다!",
			44, Color.white, TextAlignmentOptions.Center);
		var etRt = encounterTitle.GetComponent<RectTransform>();
		etRt.anchorMin = new Vector2(0.20f, 0.82f);
		etRt.anchorMax = new Vector2(0.80f, 0.94f);
		etRt.offsetMin = Vector2.zero;
		etRt.offsetMax = Vector2.zero;
		encounterTitle.fontStyle = FontStyles.Bold;

		// 싸운다 / 도망 버튼 (타이틀 아래)
		var fightBtnGo = CreateMenuButton(combatGroup.gameObject, "FightButton", "싸운다",
			new Vector2(0.25f, 0.72f), new Vector2(0.48f, 0.80f));
		var fightButton = fightBtnGo.GetComponent<Button>();

		var fleeBtnGo = CreateMenuButton(combatGroup.gameObject, "FleeButton", "도망",
			new Vector2(0.52f, 0.72f), new Vector2(0.75f, 0.80f));
		var fleeButton = fleeBtnGo.GetComponent<Button>();

		// 적 스프라이트 (지면 위, 플레이어 앞)
		var enemySlotsArea = CreateEmpty(combatGroup.gameObject, "EnemySlotsArea");
		enemySlotsArea.anchorMin = new Vector2(0.40f, GroundY);
		enemySlotsArea.anchorMax = new Vector2(0.90f, GroundY + 0.35f);
		enemySlotsArea.offsetMin = Vector2.zero;
		enemySlotsArea.offsetMax = Vector2.zero;

		GameObject[] enemySlots = new GameObject[4];
		Image[] enemyBodies = new Image[4];
		TMP_Text[] enemyNameTexts = new TMP_Text[4];
		Image[] enemyHpFills = new Image[4];
		TMP_Text[] enemyHpTextArr = new TMP_Text[4];

		for (int i = 0; i < 4; i++)
		{
			float x0 = i * 0.25f;
			float x1 = x0 + 0.24f;

			var slot = CreateImage(enemySlotsArea.gameObject, $"EnemySlot{i}", new Color(0, 0, 0, 0));
			slot.anchorMin = new Vector2(x0, 0.0f);
			slot.anchorMax = new Vector2(x1, 1.0f);
			slot.offsetMin = Vector2.zero;
			slot.offsetMax = Vector2.zero;
			slot.GetComponent<Image>().raycastTarget = false;
			enemySlots[i] = slot.gameObject;

			// 적 몸체 (하단을 바닥에 밀착)
			var body = CreateImage(slot.gameObject, "Body", Color.gray);
			body.anchorMin = new Vector2(0.05f, 0.0f);
			body.anchorMax = new Vector2(0.95f, 0.90f);
			body.offsetMin = Vector2.zero;
			body.offsetMax = Vector2.zero;
			enemyBodies[i] = body.GetComponent<Image>();
			enemyBodies[i].useSpriteMesh = true;

			// 이름 컨테이너 (반투명 배경 + 텍스트, 기본 숨김)
			var nameContainer = CreateImage(slot.gameObject, "NameContainer",
				new Color(0f, 0f, 0f, 0.5f));
			nameContainer.anchorMin = new Vector2(0.05f, 0.0f);
			nameContainer.anchorMax = new Vector2(0.95f, 0.12f);
			nameContainer.offsetMin = Vector2.zero;
			nameContainer.offsetMax = Vector2.zero;
			nameContainer.GetComponent<Image>().raycastTarget = false;
			nameContainer.gameObject.SetActive(false);

			var nameT = CreateTMPText(nameContainer.gameObject, "Name", "적",
				28, Color.white, TextAlignmentOptions.Center);
			var nrt = nameT.GetComponent<RectTransform>();
			Stretch(nrt);
			enemyNameTexts[i] = nameT;

			// HP 바 (바인딩용, 기본 숨김)
			var hpBg = CreateImage(slot.gameObject, "HpBarBg", HpBarBg);
			hpBg.anchorMin = new Vector2(0.10f, 0.0f);
			hpBg.anchorMax = new Vector2(0.90f, 0.06f);
			hpBg.offsetMin = Vector2.zero;
			hpBg.offsetMax = Vector2.zero;
			hpBg.gameObject.SetActive(false);

			var eFill = CreateImage(hpBg.gameObject, "HpFill", EnemyHpFill);
			Stretch(eFill);
			eFill.GetComponent<Image>().type = Image.Type.Filled;
			eFill.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;
			enemyHpFills[i] = eFill.GetComponent<Image>();

			// HP 텍스트 (바인딩용, 기본 숨김)
			var eHpT = CreateTMPText(slot.gameObject, "HpText", "0 / 0",
				22, Color.white, TextAlignmentOptions.Center);
			var eHpRt = eHpT.GetComponent<RectTransform>();
			eHpRt.anchorMin = new Vector2(0.05f, 0.0f);
			eHpRt.anchorMax = new Vector2(0.95f, 0.12f);
			eHpRt.offsetMin = Vector2.zero;
			eHpRt.offsetMax = Vector2.zero;
			eHpT.gameObject.SetActive(false);
			enemyHpTextArr[i] = eHpT;
		}

		// ── 조우 패널 (아이템 전용) ──
		var encounterPanel = CreateImage(canvasGo, "EncounterPanel", new Color(0, 0, 0, 0.6f));
		Stretch(encounterPanel);
		var encounterPanelCG = encounterPanel.gameObject.AddComponent<CanvasGroup>();
		encounterPanelCG.alpha = 0f;
		encounterPanelCG.blocksRaycasts = false;
		encounterPanelCG.interactable = false;

		var panelCenter = CreateImage(encounterPanel.gameObject, "PanelCenter", PanelBg);
		panelCenter.anchorMin = new Vector2(0.15f, 0.08f);
		panelCenter.anchorMax = new Vector2(0.85f, 0.92f);
		panelCenter.offsetMin = Vector2.zero;
		panelCenter.offsetMax = Vector2.zero;

		var itemEncounterTitle = CreateTMPText(panelCenter.gameObject, "ItemEncounterTitle", "아이템 상자!",
			44, Color.white, TextAlignmentOptions.Center);
		var ietRt = itemEncounterTitle.GetComponent<RectTransform>();
		ietRt.anchorMin = new Vector2(0.05f, 0.85f);
		ietRt.anchorMax = new Vector2(0.95f, 0.95f);
		ietRt.offsetMin = Vector2.zero;
		ietRt.offsetMax = Vector2.zero;
		itemEncounterTitle.fontStyle = FontStyles.Bold;

		// ── 아이템 그룹 ──
		var itemGroupGo = CreateEmpty(panelCenter.gameObject, "ItemGroup");
		Stretch(itemGroupGo);
		var itemGroupCG = itemGroupGo.gameObject.AddComponent<CanvasGroup>();
		itemGroupCG.alpha = 0f;
		itemGroupCG.blocksRaycasts = false;
		itemGroupCG.interactable = false;

		Button[] itemBtns = new Button[3];
		TMP_Text[] itemTitleArr = new TMP_Text[3];
		TMP_Text[] itemDescArr = new TMP_Text[3];

		for (int i = 0; i < 3; i++)
		{
			float x0 = 0.03f + i * 0.32f;
			float x1 = x0 + 0.30f;

			var card = CreateImage(itemGroupGo.gameObject, $"ItemCard{i}", ItemCardBg);
			card.anchorMin = new Vector2(x0, 0.15f);
			card.anchorMax = new Vector2(x1, 0.82f);
			card.offsetMin = Vector2.zero;
			card.offsetMax = Vector2.zero;
			card.GetComponent<Image>().raycastTarget = true;

			// 상단 악센트 바 — 카드 정체성을 색으로 표현
			Color[] accentColors =
			{
				new Color(0.40f, 0.75f, 0.95f),
				new Color(0.95f, 0.55f, 0.35f),
				new Color(0.55f, 0.90f, 0.55f)
			};
			var accent = CreateImage(card.gameObject, "AccentBar", accentColors[i]);
			accent.anchorMin = new Vector2(0.08f, 0.92f);
			accent.anchorMax = new Vector2(0.92f, 0.95f);
			accent.offsetMin = Vector2.zero;
			accent.offsetMax = Vector2.zero;

			var title = CreateTMPText(card.gameObject, "Title", "강화",
				30, AccentYellow, TextAlignmentOptions.Center);
			var trt = title.GetComponent<RectTransform>();
			trt.anchorMin = new Vector2(0.05f, 0.75f);
			trt.anchorMax = new Vector2(0.95f, 0.90f);
			trt.offsetMin = Vector2.zero;
			trt.offsetMax = Vector2.zero;
			title.fontStyle = FontStyles.Bold;
			itemTitleArr[i] = title;

			// 구분선
			var divider = CreateImage(card.gameObject, "Divider",
				new Color(1f, 1f, 1f, 0.12f));
			divider.anchorMin = new Vector2(0.12f, 0.73f);
			divider.anchorMax = new Vector2(0.88f, 0.735f);
			divider.offsetMin = Vector2.zero;
			divider.offsetMax = Vector2.zero;

			var desc = CreateTMPText(card.gameObject, "Desc", "설명",
				24, new Color(0.82f, 0.82f, 0.90f), TextAlignmentOptions.Center);
			var drt = desc.GetComponent<RectTransform>();
			drt.anchorMin = new Vector2(0.08f, 0.10f);
			drt.anchorMax = new Vector2(0.92f, 0.70f);
			drt.offsetMin = Vector2.zero;
			drt.offsetMax = Vector2.zero;
			itemDescArr[i] = desc;

			var btn = card.gameObject.AddComponent<Button>();
			btn.targetGraphic = card.GetComponent<Image>();
			SetButtonColors(btn);
			itemBtns[i] = btn;

			var hover = card.gameObject.AddComponent<UIHoverEffect>();
			SetField(hover, "targetText", title);
			SetField(hover, "targetImage", card.GetComponent<Image>());
			SetField(hover, "outlineColor", accentColors[i]);
		}

		// ── 승리 패널 ──
		var victoryPanel = CreateImage(canvasGo, "VictoryPanel", new Color(0, 0, 0, 0.7f));
		Stretch(victoryPanel);
		var victoryPanelCG = victoryPanel.gameObject.AddComponent<CanvasGroup>();
		victoryPanelCG.alpha = 0f;
		victoryPanelCG.blocksRaycasts = false;
		victoryPanelCG.interactable = false;

		var vicText = CreateTMPText(victoryPanel.gameObject, "VictoryText", "승리!",
			64, AccentYellow, TextAlignmentOptions.Center);
		var vrt = vicText.GetComponent<RectTransform>();
		vrt.anchorMin = new Vector2(0.2f, 0.55f);
		vrt.anchorMax = new Vector2(0.8f, 0.75f);
		vrt.offsetMin = Vector2.zero;
		vrt.offsetMax = Vector2.zero;
		vicText.fontStyle = FontStyles.Bold;

		var returnBtn = CreateMenuButton(victoryPanel.gameObject, "ReturnButton", "메인 메뉴로",
			new Vector2(0.35f, 0.30f), new Vector2(0.65f, 0.42f));

		// ── EventSystem ──
		var es = new GameObject("EventSystem");
		es.AddComponent<UnityEngine.EventSystems.EventSystem>();
		es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

		// ── 컨트롤러 ──
		var root = new GameObject("ExploreRoot");
		var ctrl = root.AddComponent<GameExploreController>();

		// 필드 바인딩
		SetField(ctrl, "heartDisplay", heartDisplayComp);
		SetField(ctrl, "powerUpText", pupText);
		SetField(ctrl, "playerBody", playerBody.GetComponent<Image>());
		SetField(ctrl, "playerNameText", playerNameTxt);

		SetField(ctrl, "encounterPanel", encounterPanelCG);
		SetField(ctrl, "encounterTitle", encounterTitle);
		SetField(ctrl, "itemEncounterTitle", itemEncounterTitle);
		SetField(ctrl, "combatGroup", combatGroupCG);
		SetField(ctrl, "enemySlots", enemySlots);
		SetField(ctrl, "enemyBodies", enemyBodies);
		SetField(ctrl, "enemyNames", enemyNameTexts);
		SetField(ctrl, "enemyHpFills", enemyHpFills);
		SetField(ctrl, "enemyHpTexts", enemyHpTextArr);

		// 스테이지별 스프라이트 번들 — 레지스트리에 등록된 모든 스테이지를 편집 시점에 로드
		// 누락 에셋은 스테이지/몹의 themeColor로 자동 폴백 생성
		var stageBundles = SceneBuilderUtility.BuildAllStageBundles();
		SetField(ctrl, "stageBundles", stageBundles);
		SetField(ctrl, "backgroundImage", bgImageComp);

		SetField(ctrl, "fightButton", fightButton);
		SetField(ctrl, "fleeButton", fleeButton);
		SetField(ctrl, "itemGroup", itemGroupCG);
		SetField(ctrl, "itemButtons", itemBtns);
		SetField(ctrl, "itemTitles", itemTitleArr);
		SetField(ctrl, "itemDescs", itemDescArr);
		SetField(ctrl, "victoryPanel", victoryPanelCG);

		// 버튼 연결 (PersistentListener로 씬에 영속 저장)
		UnityEventTools.AddPersistentListener(fightButton.onClick, ctrl.OnFightClicked);
		UnityEventTools.AddPersistentListener(fleeButton.onClick, ctrl.OnFleeClicked);
		UnityEventTools.AddPersistentListener(returnBtn.GetComponent<Button>().onClick, ctrl.OnReturnToMainMenu);

		// ── 디버그 콘솔 ──
		var debugGo = new GameObject("DebugConsole");
		debugGo.AddComponent<DebugConsoleController>();

		// ── 오디오 매니저 ─────────────────────────────────────────
		SceneBuilderUtility.BuildAudioManager(new[]
		{
			"UI_OK", "UI_Back_NO", "UI_Purchase_OK_LockIn", "Player_EarnDrop",
			"Transition_2", "Transition_2_Quit", "Transition_3", "Environment_Desert"
		}, includeDrumRoll: false);

		// ── 씬 저장 ──
		string scenePath = "Assets/Scenes/GameExploreScene.unity";
		EnsureDirectory("Assets/Scenes");
		EditorSceneManager.SaveScene(scene, scenePath);
		AddSceneToBuildSettings(scenePath);
		EditorUtility.DisplayDialog("씬 빌더", "GameExploreScene 생성 완료!", "확인");
	}

	static void EnsurePixelSprite(string path) => SceneBuilderUtility.EnsurePixelSprite(path);

	static void EnsureTightSprite(string path) => SceneBuilderUtility.EnsureTightSprite(path);

	// ── 헬퍼 (유틸리티 위임 + 로컬 전용) ──

	static RectTransform CreateImage(GameObject parent, string name, Color color)
		=> SceneBuilderUtility.CreateImage(parent.transform, name, color);

	static RectTransform CreateEmpty(GameObject parent, string name)
		=> SceneBuilderUtility.CreateEmpty(parent.transform, name);

	static TMP_Text CreateTMPText(GameObject parent, string name, string text,
		float fontSize, Color color, TextAlignmentOptions alignment)
		=> SceneBuilderUtility.CreateTMPText(parent.transform, name, text, fontSize, color, alignment);

	static void Stretch(RectTransform rt) => SceneBuilderUtility.Stretch(rt);

	static void SetField(object target, string fieldName, object value)
		=> SceneBuilderUtility.SetField(target, fieldName, value);

	static void EnsureDirectory(string path) => SceneBuilderUtility.EnsureDirectory(path);

	static void AddSceneToBuildSettings(string scenePath)
		=> SceneBuilderUtility.AddSceneToBuildSettings(scenePath);

	static GameObject CreateMenuButton(GameObject parent, string name, string label,
		Vector2 anchorMin, Vector2 anchorMax)
	{
		var go = SceneBuilderUtility.CreateButton(parent.transform, name, label,
			28, SceneBuilderUtility.ButtonNormal, SceneBuilderUtility.ButtonHighlight,
			SceneBuilderUtility.ButtonPressed);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		return go;
	}

	static void SetButtonColors(Button btn)
	{
		var cb = btn.colors;
		cb.normalColor = SceneBuilderUtility.ButtonNormal;
		cb.highlightedColor = SceneBuilderUtility.ButtonHighlight;
		cb.pressedColor = SceneBuilderUtility.ButtonPressed;
		cb.selectedColor = SceneBuilderUtility.ButtonHighlight;
		btn.colors = cb;
	}

	static Material GetOrCreateOutlineMaterial(TMP_FontAsset fontAsset)
	{
		string path = "Assets/Materials/Mona12Outline.mat";
		EnsureDirectory("Assets/Materials");
		var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
		if (mat == null)
		{
			mat = new Material(fontAsset.material);
			mat.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.35f);
			mat.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
			AssetDatabase.CreateAsset(mat, path);
			AssetDatabase.SaveAssets();
		}
		return mat;
	}
}
