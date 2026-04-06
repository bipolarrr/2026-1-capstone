using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Reflection;
using UnityEditor.Events;

public static class GameExploreSceneBuilder
{
	// ── 색상 팔레트 ──
	static readonly Color BgColor = new Color(0.10f, 0.10f, 0.18f);
	static readonly Color GroundColor = new Color(0.18f, 0.28f, 0.16f);
	static readonly Color PanelBg = new Color(0.12f, 0.12f, 0.22f, 0.95f);
	static readonly Color ButtonNormal = new Color(0.15f, 0.18f, 0.35f, 0.9f);
	static readonly Color ButtonHighlight = new Color(0.28f, 0.35f, 0.70f, 1f);
	static readonly Color ButtonPressed = new Color(0.10f, 0.12f, 0.25f, 1f);
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
		var bg = CreateImage(canvasGo, "Background", BgColor);
		Stretch(bg);

		var ground = CreateImage(canvasGo, "Ground", GroundColor);
		SetAnchors(ground, 0f, 0f, 1f, 0.18f);
		ground.offsetMin = Vector2.zero;
		ground.offsetMax = Vector2.zero;

		// ── 스크롤 장식 ──
		var scrollRoot = CreateEmpty(canvasGo, "ScrollingRoot");
		Stretch(scrollRoot);
		BuildScrollingDecorations(scrollRoot.gameObject);

		// ── 플레이어 캐릭터 ──
		string playerSpritePath = "Assets/Mobs/DiceGambler_sample.png";
		EnsureTightSprite(playerSpritePath);
		var playerSprite = AssetDatabase.LoadAssetAtPath<Sprite>(playerSpritePath);

		var playerBody = CreateImage(canvasGo, "PlayerBody", Color.white);
		playerBody.anchorMin = new Vector2(0.15f, 0.18f);
		playerBody.anchorMax = new Vector2(0.23f, 0.42f);
		playerBody.offsetMin = Vector2.zero;
		playerBody.offsetMax = Vector2.zero;
		var playerImg = playerBody.GetComponent<Image>();
		if (playerSprite != null)
		{
			playerImg.sprite = playerSprite;
			playerImg.preserveAspect = true;
			playerImg.useSpriteMesh = true;
		}

		var playerNameTxt = CreateTMPText(canvasGo, "PlayerName", "도박꾼",
			30, DarkerColor(PlayerColor), TextAlignmentOptions.Center);
		var pnRt = playerNameTxt.GetComponent<RectTransform>();
		pnRt.anchorMin = new Vector2(0.12f, 0.43f);
		pnRt.anchorMax = new Vector2(0.26f, 0.48f);
		pnRt.offsetMin = Vector2.zero;
		pnRt.offsetMax = Vector2.zero;

		// ── HUD ──
		var hudParent = CreateEmpty(canvasGo, "HUD");
		Stretch(hudParent);

		var hpBarBg = CreateImage(hudParent.gameObject, "PlayerHpBarBg", HpBarBg);
		hpBarBg.anchorMin = new Vector2(0.02f, 0.92f);
		hpBarBg.anchorMax = new Vector2(0.25f, 0.96f);
		hpBarBg.offsetMin = Vector2.zero;
		hpBarBg.offsetMax = Vector2.zero;

		var hpFill = CreateImage(hpBarBg.gameObject, "PlayerHpFill", HpBarFill);
		Stretch(hpFill);
		hpFill.GetComponent<Image>().type = Image.Type.Filled;
		hpFill.GetComponent<Image>().fillMethod = Image.FillMethod.Horizontal;

		var hpText = CreateTMPText(hudParent.gameObject, "PlayerHpText", "100 / 100",
			22, Color.white, TextAlignmentOptions.Left);
		var hpTxtRt = hpText.GetComponent<RectTransform>();
		hpTxtRt.anchorMin = new Vector2(0.02f, 0.88f);
		hpTxtRt.anchorMax = new Vector2(0.25f, 0.92f);
		hpTxtRt.offsetMin = Vector2.zero;
		hpTxtRt.offsetMax = Vector2.zero;

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

		// 적 스프라이트 (경로 위, 플레이어 앞)
		var enemySlotsArea = CreateEmpty(combatGroup.gameObject, "EnemySlotsArea");
		enemySlotsArea.anchorMin = new Vector2(0.40f, 0.18f);
		enemySlotsArea.anchorMax = new Vector2(0.90f, 0.50f);
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

			// 이름 (바인딩용, 기본 숨김)
			var nameT = CreateTMPText(slot.gameObject, "Name", "적",
				28, Color.white, TextAlignmentOptions.Center);
			var nrt = nameT.GetComponent<RectTransform>();
			nrt.anchorMin = new Vector2(0.05f, 0.0f);
			nrt.anchorMax = new Vector2(0.95f, 0.12f);
			nrt.offsetMin = Vector2.zero;
			nrt.offsetMax = Vector2.zero;
			nameT.gameObject.SetActive(false);
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
		SetField(ctrl, "playerHpFill", hpFill.GetComponent<Image>());
		SetField(ctrl, "playerHpText", hpText);
		SetField(ctrl, "powerUpText", pupText);
		SetField(ctrl, "playerBody", playerBody.GetComponent<Image>());
		SetField(ctrl, "playerNameText", playerNameTxt);
		SetField(ctrl, "scrollingRoot", scrollRoot);
		SetField(ctrl, "encounterPanel", encounterPanelCG);
		SetField(ctrl, "encounterTitle", encounterTitle);
		SetField(ctrl, "itemEncounterTitle", itemEncounterTitle);
		SetField(ctrl, "combatGroup", combatGroupCG);
		SetField(ctrl, "enemySlots", enemySlots);
		SetField(ctrl, "enemyBodies", enemyBodies);
		SetField(ctrl, "enemyNames", enemyNameTexts);
		SetField(ctrl, "enemyHpFills", enemyHpFills);
		SetField(ctrl, "enemyHpTexts", enemyHpTextArr);

		// 몹 스프라이트 로드 (슬라임, 고블린, 박쥐, 해골 순)
		// 텍스처 임포트: Sprite 타입 + Single 모드를 보장
		Sprite[] mobSprites = new Sprite[4];
		string[] spriteFiles = { "Slime_sample", "Goblin_sample", "Bat_sample", "Skeleton_sample" };
		for (int i = 0; i < spriteFiles.Length; i++)
			EnsureTightSprite($"Assets/Mobs/{spriteFiles[i]}.png");
		for (int i = 0; i < spriteFiles.Length; i++)
			mobSprites[i] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Mobs/{spriteFiles[i]}.png");
		SetField(ctrl, "mobSprites", mobSprites);
		EnsureTightSprite("Assets/Mobs/Boss_Dracula_example.png");
		var bossSpr = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Mobs/Boss_Dracula_example.png");
		SetField(ctrl, "bossSprite", bossSpr);

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

		// ── 씬 저장 ──
		string scenePath = "Assets/Scenes/GameExploreScene.unity";
		EnsureDirectory("Assets/Scenes");
		EditorSceneManager.SaveScene(scene, scenePath);
		AddSceneToBuildSettings(scenePath);
		EditorUtility.DisplayDialog("씬 빌더", "GameExploreScene 생성 완료!", "확인");
	}

	// ── 스크롤 장식 생성 ──

	static void BuildScrollingDecorations(GameObject parent)
	{
		Color treeColor = new Color(0.25f, 0.50f, 0.30f);
		Color trunkColor = new Color(0.40f, 0.28f, 0.15f);

		for (int i = 0; i < 5; i++)
		{
			float x = 0.1f + i * 0.2f;

			// 나무 줄기
			var trunk = CreateImage(parent, $"Trunk{i}", trunkColor);
			trunk.anchorMin = new Vector2(x - 0.005f, 0.15f);
			trunk.anchorMax = new Vector2(x + 0.005f, 0.30f);
			trunk.offsetMin = Vector2.zero;
			trunk.offsetMax = Vector2.zero;

			// 나무 관 (원형)
			var crown = CreateImage(parent, $"Crown{i}", treeColor);
			crown.anchorMin = new Vector2(x - 0.025f, 0.28f);
			crown.anchorMax = new Vector2(x + 0.025f, 0.40f);
			crown.offsetMin = Vector2.zero;
			crown.offsetMax = Vector2.zero;
		}
	}

	// ── 스프라이트 임포트 (Tight 메시로 투명 영역 제거) ──

	static void EnsureTightSprite(string path)
	{
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null) return;
		bool reimport = false;
		if (importer.textureType != TextureImporterType.Sprite ||
			importer.spriteImportMode != SpriteImportMode.Single)
		{
			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Single;
			reimport = true;
		}
		var settings = new TextureImporterSettings();
		importer.ReadTextureSettings(settings);
		if (settings.spriteMeshType != SpriteMeshType.Tight)
		{
			settings.spriteMeshType = SpriteMeshType.Tight;
			importer.SetTextureSettings(settings);
			reimport = true;
		}
		if (reimport)
			importer.SaveAndReimport();
	}

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
			28, ButtonNormal, ButtonHighlight, ButtonPressed);
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
		cb.normalColor = ButtonNormal;
		cb.highlightedColor = ButtonHighlight;
		cb.pressedColor = ButtonPressed;
		cb.selectedColor = ButtonHighlight;
		btn.colors = cb;
	}

	static void SetAnchors(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
	{
		rt.anchorMin = new Vector2(xMin, yMin);
		rt.anchorMax = new Vector2(xMax, yMax);
	}

	static Color DarkerColor(Color c)
	{
		return new Color(c.r * 0.5f, c.g * 0.5f, c.b * 0.5f, 1f);
	}
}
