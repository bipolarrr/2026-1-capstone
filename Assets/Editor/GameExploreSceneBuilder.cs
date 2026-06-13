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
	static readonly Color MapNodeHitArea = new Color(1f, 1f, 1f, 0f);
	static readonly Color MapNodeText = new Color(0.88f, 0.92f, 0.98f);
	static readonly Color MapGraphVeil = new Color(0.055f, 0.035f, 0.025f, 0.24f);
	static readonly Color MapNodeDark = new Color(0.10f, 0.065f, 0.04f, 0.98f);
	static readonly Color MapNodeShadow = new Color(0.04f, 0.025f, 0.015f, 0.58f);
	const string MapBackgroundPath = "Assets/UI/UI_Map.png";
	const string MapIconSheetPath = "Assets/UI/UI_MapIcon.png";
	const string MapIconBossPath = "Assets/UI/MapIcons/UI_MapIcon_Boss.png";
	const string MapIconShopPath = "Assets/UI/MapIcons/UI_MapIcon_Shop.png";
	const string MapIconHealPath = "Assets/UI/MapIcons/UI_MapIcon_Heal.png";
	const string MapIconCombatPath = "Assets/UI/MapIcons/UI_MapIcon_Combat.png";
	const string MapPlayerLocationMarkerPath = "Assets/UI/MapIcons/PlayerLocation_SquirrelFace.png";
	const int MapNodeLabelFontSize = 18;
	const int MapReadableLabelFontSize = 42;
	const int MapSymbolFontSize = 34;
	const float MapNodeDefaultPlateSize = 64f;
	const float MapNodeOutlinePadding = 8f;
	const float MapNodeShadowPadding = 12f;
	const float MapNodeAccentSize = 17f;
	const float MapNodeAccentOffsetRatio = 0.34f;
	const float MapNodeIconSize = 80f;
	const float MapNodeTitleOffset = 44f;
	static readonly Vector2 MapGraphSafeAnchorMin = new Vector2(0.13f, 0.135f);
	static readonly Vector2 MapGraphSafeAnchorMax = new Vector2(0.87f, 0.89f);
	static readonly Vector2 MapViewportDesignSize = new Vector2(
		1920f * (0.71f - 0.29f) * (0.87f - 0.13f),
		1080f * (0.94f - 0.005f) * (0.89f - 0.135f));
	static readonly Vector2 PlayerLocationMarkerSize = new Vector2(54f, 50f);
	static readonly Vector2 PlayerLocationMarkerOffset = new Vector2(0f, 52f);

	[MenuItem("Tools/Build GameExplore Scene")]
	public static void BuildScene()
	{
		BuildSceneInternal(showCompletionDialog: true);
	}

	public static bool BuildForIncremental()
	{
		return BuildSceneInternal(showCompletionDialog: false);
	}

	static bool BuildSceneInternal(bool showCompletionDialog)
	{
		SceneBuilderUtility.BeginSceneBuildValidation(nameof(GameExploreSceneBuilder));
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var shell = SceneBuilderUtility.BuildSceneShell("MainCamera", BgColor);
		var canvasGo = shell.canvas.gameObject;

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
		string idlePath = SceneBuilderUtility.PlayerIdleSpritePath;
		EnsurePixelSprite(idlePath);
		Sprite idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(idlePath);

		var playerBody = CreateImage(canvasGo, "PlayerBody", Color.white);
		playerBody.pivot = new Vector2(0.5f, 0f);               // 피벗 하단 → 발 기준 배치
		playerBody.anchorMin = new Vector2(0.19f, GroundY);      // 단일 앵커점 (지면)
		playerBody.anchorMax = new Vector2(0.19f, GroundY);
		playerBody.sizeDelta = new Vector2(150f, 150f);          // 기본 크기 (px)
		playerBody.localRotation = Quaternion.identity;          // 좌우 반전 방지
		playerBody.localScale = new Vector3(2f, 2f, 1f);        // 스케일 2배
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

		var heartDisplayComp = SceneBuilderUtility.BuildHeartDisplay(
			hudParent, "PlayerHeartDisplay",
			new Vector2(0.02f, 0.84f), new Vector2(0.56f, 0.98f));

		var pupText = CreateTMPText(hudParent.gameObject, "PowerUpText", "",
			22, AccentYellow, TextAlignmentOptions.Left);
		var pupRt = pupText.GetComponent<RectTransform>();
		pupRt.anchorMin = new Vector2(0.02f, 0.84f);
		pupRt.anchorMax = new Vector2(0.40f, 0.88f);
		pupRt.offsetMin = Vector2.zero;
		pupRt.offsetMax = Vector2.zero;

		// ── 경로 지도 패널 ──
		var mapGraphGroup = CreateEmpty(canvasGo, "MapGraphGroup");
		Stretch(mapGraphGroup);
		var mapGraphGroupCG = mapGraphGroup.gameObject.AddComponent<CanvasGroup>();
		mapGraphGroupCG.alpha = 0f;
		mapGraphGroupCG.blocksRaycasts = false;
		mapGraphGroupCG.interactable = false;

		var mapPanel = CreateEmpty(mapGraphGroup.gameObject, "MapGraphPanel");
		mapPanel.anchorMin = new Vector2(0.29f, 0.005f);
		mapPanel.anchorMax = new Vector2(0.71f, 0.94f);
		mapPanel.offsetMin = Vector2.zero;
		mapPanel.offsetMax = Vector2.zero;

		var mapBackground = CreateImage(mapPanel.gameObject, "MapBackground", Color.white);
		Stretch(mapBackground);
		var mapBackgroundImage = mapBackground.GetComponent<Image>();
		mapBackgroundImage.sprite = LoadMapUiSprite(MapBackgroundPath);
		mapBackgroundImage.preserveAspect = true;
		mapBackgroundImage.raycastTarget = false;
		mapBackground.SetAsFirstSibling();

		var defaultMapTitle = ResolveStageMapTitle(defaultStage);
		var mapTitle = CreateTMPText(mapGraphGroup.gameObject, "MapTitle", defaultMapTitle,
			34, Color.white, TextAlignmentOptions.Center);
		var mapTitleRt = mapTitle.GetComponent<RectTransform>();
		mapTitleRt.anchorMin = new Vector2(0.29f, 0.945f);
		mapTitleRt.anchorMax = new Vector2(0.71f, 0.995f);
		mapTitleRt.offsetMin = Vector2.zero;
		mapTitleRt.offsetMax = Vector2.zero;
		mapTitle.fontStyle = FontStyles.Bold;

		var mapProgressText = CreateTMPText(mapPanel.gameObject, "MapProgress", "0 / 9",
			24, AccentYellow, TextAlignmentOptions.Right);
		var mapProgressRt = mapProgressText.GetComponent<RectTransform>();
		mapProgressRt.anchorMin = new Vector2(0.72f, 0.92f);
		mapProgressRt.anchorMax = new Vector2(0.94f, 0.98f);
		mapProgressRt.offsetMin = Vector2.zero;
		mapProgressRt.offsetMax = Vector2.zero;

		var mapReadabilityVeil = CreateImage(mapPanel.gameObject, "MapReadabilityVeil", MapGraphVeil);
		mapReadabilityVeil.anchorMin = MapGraphSafeAnchorMin;
		mapReadabilityVeil.anchorMax = MapGraphSafeAnchorMax;
		mapReadabilityVeil.offsetMin = Vector2.zero;
		mapReadabilityVeil.offsetMax = Vector2.zero;
		mapReadabilityVeil.GetComponent<Image>().raycastTarget = false;

		var mapViewport = CreateImage(mapPanel.gameObject, "MapViewport", new Color(1f, 1f, 1f, 0f));
		mapViewport.anchorMin = MapGraphSafeAnchorMin;
		mapViewport.anchorMax = MapGraphSafeAnchorMax;
		mapViewport.offsetMin = Vector2.zero;
		mapViewport.offsetMax = Vector2.zero;
		mapViewport.gameObject.AddComponent<RectMask2D>();
		var mapViewportImage = mapViewport.GetComponent<Image>();
		mapViewportImage.raycastTarget = true;

		var mapScrollRect = mapViewport.gameObject.AddComponent<ScrollRect>();
		mapScrollRect.horizontal = false;
		mapScrollRect.vertical = true;
		mapScrollRect.movementType = ScrollRect.MovementType.Clamped;
		mapScrollRect.inertia = false;
		mapScrollRect.scrollSensitivity = 40f;
		mapScrollRect.viewport = mapViewport;

		var mapContent = CreateEmpty(mapViewport.gameObject, "MapContent");
		mapContent.anchorMin = new Vector2(0f, 1f);
		mapContent.anchorMax = new Vector2(1f, 1f);
		mapContent.pivot = new Vector2(0.5f, 1f);
		mapContent.anchoredPosition = Vector2.zero;
		mapContent.sizeDelta = new Vector2(0f, MapViewportDesignSize.y);
		mapScrollRect.content = mapContent;

		var mapConnectionRoot = CreateEmpty(mapContent.gameObject, "MapConnectionRoot");
		Stretch(mapConnectionRoot);

		var mapNodeRoot = CreateEmpty(mapContent.gameObject, "MapNodeRoot");
		Stretch(mapNodeRoot);

		ExploreMapPresentationPolicy.TryBuild(defaultMapTitle, 0, out var mapTemplate);
		var initialMapLayout = ExploreMapLayout.Build(
			mapTemplate,
			ExploreMapLayout.CreateDefaultConfig(MapViewportDesignSize));
		mapContent.sizeDelta = new Vector2(0f, Mathf.Max(MapViewportDesignSize.y, initialMapLayout.ContentHeight));

		EnsureMapUiSprite(MapIconSheetPath);
		Sprite mapBossIcon = LoadMapUiSprite(MapIconBossPath);
		Sprite mapShopIcon = LoadMapUiSprite(MapIconShopPath);
		Sprite mapHealIcon = LoadMapUiSprite(MapIconHealPath);
		Sprite mapCombatIcon = LoadMapUiSprite(MapIconCombatPath);
		Sprite playerLocationMarkerIcon = LoadMapUiSprite(MapPlayerLocationMarkerPath);

		Image[] mapConnectionLines = new Image[ExploreMapPresentationPolicy.MaxConnectionCount];
		ExploreMapConnectionLineDisplay[] mapConnectionLineViews = new ExploreMapConnectionLineDisplay[ExploreMapPresentationPolicy.MaxConnectionCount];
		for (int i = 0; i < mapTemplate.ConnectionCount && i < mapConnectionLines.Length; i++)
		{
			var connection = mapTemplate.GetConnection(i);
			var fromLayout = initialMapLayout.GetNode(connection.FromNodeIndex);
			var toLayout = initialMapLayout.GetNode(connection.ToNodeIndex);
			mapConnectionLines[i] = CreateMapLine(
				mapConnectionRoot,
				$"MapLine_{connection.FromNodeId}_{connection.ToNodeId}",
				fromLayout.Center,
				toLayout.Center);
			mapConnectionLineViews[i] = mapConnectionLines[i].gameObject.AddComponent<ExploreMapConnectionLineDisplay>();
			SetField(mapConnectionLineViews[i], "lineImage", mapConnectionLines[i]);
			SetField(mapConnectionLineViews[i], "lineTransform", mapConnectionLines[i].rectTransform);
		}

		RectTransform[] mapNodeRects = new RectTransform[ExploreMapPresentationPolicy.MaxNodeCount];
		ExploreMapNodeDisplay[] mapNodeViews = new ExploreMapNodeDisplay[ExploreMapPresentationPolicy.MaxNodeCount];
		Graphic[] mapNodeGraphics = new Graphic[ExploreMapPresentationPolicy.MaxNodeCount];
		Button[] mapNodeButtons = new Button[ExploreMapPresentationPolicy.MaxNodeCount];
		TMP_Text[] mapNodeIconLabels = new TMP_Text[ExploreMapPresentationPolicy.MaxNodeCount];
		Image[] mapNodeIconImages = new Image[ExploreMapPresentationPolicy.MaxNodeCount];
		ExploreMapNodeHoverEffect[] mapNodeHoverEffects = new ExploreMapNodeHoverEffect[ExploreMapPresentationPolicy.MaxNodeCount];
		TMP_Text[] mapNodeTitleArr = new TMP_Text[ExploreMapPresentationPolicy.MaxNodeCount];
		TMP_Text[] mapNodeDescArr = new TMP_Text[ExploreMapPresentationPolicy.MaxNodeCount];

		for (int i = 0; i < mapTemplate.NodeCount && i < mapNodeRects.Length; i++)
		{
			var nodeView = mapTemplate.GetNode(i);
			var nodeLayout = initialMapLayout.GetNode(i);
			var node = CreateMapNode(
				mapNodeRoot.gameObject,
				$"MapNode_{nodeView.NodeId}",
				nodeLayout.Center,
				ResolveMapNodeSize(nodeView.Kind),
				nodeView.IconLabel,
				nodeView.Title,
				nodeView.Description);

			var btn = node.root.gameObject.AddComponent<Button>();
			btn.targetGraphic = node.hitGraphic;
			btn.interactable = false;
			SetMapNodeButtonColors(btn);
			SetField(node.view, "button", btn);
			node.hoverEffect.SetScrollTarget(mapScrollRect);

			mapNodeRects[i] = node.root;
			mapNodeViews[i] = node.view;
			mapNodeGraphics[i] = node.graphic;
			mapNodeButtons[i] = btn;
			mapNodeIconLabels[i] = node.iconLabel;
			mapNodeIconImages[i] = node.iconImage;
			mapNodeHoverEffects[i] = node.hoverEffect;
			mapNodeTitleArr[i] = node.title;
			mapNodeDescArr[i] = node.desc;
		}

		var playerMapMarker = CreateImage(mapNodeRoot.gameObject, "PlayerLocationMarker", Color.white);
		playerMapMarker.anchorMin = new Vector2(0.5f, 0.5f);
		playerMapMarker.anchorMax = new Vector2(0.5f, 0.5f);
		playerMapMarker.pivot = new Vector2(0.5f, 0.5f);
		playerMapMarker.sizeDelta = PlayerLocationMarkerSize;
		playerMapMarker.anchoredPosition = PlayerLocationMarkerOffset;
		playerMapMarker.gameObject.SetActive(false);
		playerMapMarker.SetAsLastSibling();
		var playerMapMarkerImage = playerMapMarker.GetComponent<Image>();
		playerMapMarkerImage.sprite = playerLocationMarkerIcon;
		playerMapMarkerImage.color = playerLocationMarkerIcon != null ? Color.white : Color.clear;
		playerMapMarkerImage.preserveAspect = true;
		playerMapMarkerImage.raycastTarget = false;
		var playerMapMarkerView = playerMapMarker.gameObject.AddComponent<ExploreMapMarkerDisplay>();
		SetField(playerMapMarkerView, "root", playerMapMarker);

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
		var exploreEnemySlots = SceneBuilderUtility.BuildExploreEnemySlots(
			combatGroup.transform, GroundY, HpBarBg, EnemyHpFill,
			"Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png");
		GameObject[] enemySlots = exploreEnemySlots.panels;
		Image[] enemyBodies = exploreEnemySlots.bodies;
		Image[] enemyIdleProjectiles = exploreEnemySlots.idleProjectiles;
		TMP_Text[] enemyNameTexts = exploreEnemySlots.names;
		Image[] enemyHpFills = exploreEnemySlots.hpFills;
		TMP_Text[] enemyHpTextArr = exploreEnemySlots.hpTexts;

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

		// ── 스토리 피날레 패널 ──
		var victoryPanelCG = CreateStoryFinalePanel(canvasGo, out var returnBtn);

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
		SetField(ctrl, "mapGraphGroup", mapGraphGroupCG);
		SetField(ctrl, "mapTitle", mapTitle);
		SetField(ctrl, "mapProgressText", mapProgressText);
		SetField(ctrl, "mapScrollRect", mapScrollRect);
		SetField(ctrl, "mapViewport", mapViewport);
		SetField(ctrl, "mapContent", mapContent);
		SetField(ctrl, "mapNodeRoot", mapNodeRoot);
		SetField(ctrl, "mapConnectionRoot", mapConnectionRoot);
		SetField(ctrl, "playerMapMarker", playerMapMarker);
		SetField(ctrl, "mapNodeRects", mapNodeRects);
		SetField(ctrl, "mapNodeViews", mapNodeViews);
		SetField(ctrl, "mapNodeGraphics", mapNodeGraphics);
		SetField(ctrl, "mapNodeButtons", mapNodeButtons);
		SetField(ctrl, "mapNodeIconLabels", mapNodeIconLabels);
		SetField(ctrl, "mapNodeIconImages", mapNodeIconImages);
		SetField(ctrl, "mapNodeHoverEffects", mapNodeHoverEffects);
		SetField(ctrl, "mapNodeTitles", mapNodeTitleArr);
		SetField(ctrl, "mapNodeDescs", mapNodeDescArr);
		SetField(ctrl, "mapConnectionLines", mapConnectionLines);
		SetField(ctrl, "mapConnectionLineViews", mapConnectionLineViews);
		SetField(ctrl, "playerMapMarkerView", playerMapMarkerView);
		SetField(ctrl, "mapBossIconSprite", mapBossIcon);
		SetField(ctrl, "mapShopIconSprite", mapShopIcon);
		SetField(ctrl, "mapHealIconSprite", mapHealIcon);
		SetField(ctrl, "mapCombatIconSprite", mapCombatIcon);
		SetField(ctrl, "combatGroup", combatGroupCG);
		SetField(ctrl, "enemySlots", enemySlots);
		SetField(ctrl, "enemyBodies", enemyBodies);
		SetField(ctrl, "enemyIdleProjectiles", enemyIdleProjectiles);
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
		SceneBuilderUtility.BuildDebugConsole();

		// ── 오디오 매니저 ─────────────────────────────────────────
		SceneBuilderUtility.BuildAudioManager(new[]
		{
			"UI_OK", "UI_Back_NO", "UI_Purchase_OK_LockIn", "Player_EarnDrop",
			"Transition_2", "Transition_2_Quit", "Transition_3", "Environment_Desert"
		}, includeDrumRoll: false);

		// ── 씬 저장 ──
		return SceneBuilderUtility.SaveSceneAndShowDialog(scene,
			"Assets/Scenes/GameExploreScene.unity",
			"GameExploreScene 생성 완료!",
			showDialog: showCompletionDialog);
	}

	static CanvasGroup CreateStoryFinalePanel(GameObject parent, out GameObject returnButton)
	{
		var finalePanel = CreateImage(parent, "VictoryPanel", new Color(0f, 0f, 0f, 0.82f));
		Stretch(finalePanel);
		var finalePanelGroup = finalePanel.gameObject.AddComponent<CanvasGroup>();
		finalePanelGroup.alpha = 0f;
		finalePanelGroup.blocksRaycasts = false;
		finalePanelGroup.interactable = false;

		var title = CreateTMPText(finalePanel.gameObject, "FinaleTitle", "동굴의 끝",
			40, Color.white, TextAlignmentOptions.Center);
		var titleRt = title.GetComponent<RectTransform>();
		titleRt.anchorMin = new Vector2(0.22f, 0.88f);
		titleRt.anchorMax = new Vector2(0.78f, 0.95f);
		titleRt.offsetMin = Vector2.zero;
		titleRt.offsetMax = Vector2.zero;
		title.fontStyle = FontStyles.Bold;

		var cutsceneFrame = CreateImage(finalePanel.gameObject, "FinaleCutscenePlaceholder",
			new Color(0.08f, 0.075f, 0.09f, 0.98f));
		cutsceneFrame.anchorMin = new Vector2(0.22f, 0.48f);
		cutsceneFrame.anchorMax = new Vector2(0.78f, 0.87f);
		cutsceneFrame.offsetMin = Vector2.zero;
		cutsceneFrame.offsetMax = Vector2.zero;
		var frameOutline = cutsceneFrame.gameObject.AddComponent<Outline>();
		frameOutline.effectColor = new Color(0.75f, 0.68f, 0.46f, 0.85f);
		frameOutline.effectDistance = new Vector2(4f, -4f);

		var placeholder = CreateTMPText(cutsceneFrame.gameObject, "CutscenePlaceholderText", "컷씬 준비 중",
			34, new Color(0.82f, 0.86f, 0.94f), TextAlignmentOptions.Center);
		var placeholderRt = placeholder.GetComponent<RectTransform>();
		placeholderRt.anchorMin = new Vector2(0.08f, 0.40f);
		placeholderRt.anchorMax = new Vector2(0.92f, 0.60f);
		placeholderRt.offsetMin = Vector2.zero;
		placeholderRt.offsetMax = Vector2.zero;
		placeholder.fontStyle = FontStyles.Bold;

		var dialogue = CreateTMPText(finalePanel.gameObject, "FinaleDialogue",
			"동굴 끝에서 오래된 문이 열린다.\n수호자의 그림자가 조용히 길을 비킨다.\n다음 여정은 아직 어둠 속에 남아 있다.",
			27, new Color(0.90f, 0.91f, 0.95f), TextAlignmentOptions.Center);
		var dialogueRt = dialogue.GetComponent<RectTransform>();
		dialogueRt.anchorMin = new Vector2(0.18f, 0.27f);
		dialogueRt.anchorMax = new Vector2(0.82f, 0.45f);
		dialogueRt.offsetMin = Vector2.zero;
		dialogueRt.offsetMax = Vector2.zero;
		dialogue.lineSpacing = 10f;
		dialogue.textWrappingMode = TextWrappingModes.Normal;

		var continuedText = CreateTMPText(finalePanel.gameObject, "ToBeContinuedText", "TO BE CONTINUED",
			58, AccentYellow, TextAlignmentOptions.Center);
		var continuedRt = continuedText.GetComponent<RectTransform>();
		continuedRt.anchorMin = new Vector2(0.16f, 0.16f);
		continuedRt.anchorMax = new Vector2(0.84f, 0.25f);
		continuedRt.offsetMin = Vector2.zero;
		continuedRt.offsetMax = Vector2.zero;
		continuedText.fontStyle = FontStyles.Bold;
		continuedText.enableAutoSizing = true;
		continuedText.fontSizeMin = 34f;
		continuedText.fontSizeMax = 58f;
		continuedText.textWrappingMode = TextWrappingModes.NoWrap;

		returnButton = CreateMenuButton(finalePanel.gameObject, "ReturnButton", "메인 화면으로",
			new Vector2(0.37f, 0.06f), new Vector2(0.63f, 0.13f));

		return finalePanelGroup;
	}

	struct MapNodeHandles
	{
		public RectTransform root;
		public Graphic graphic;
		public Graphic hitGraphic;
		public ExploreMapNodeDisplay view;
		public TMP_Text iconLabel;
		public Image iconImage;
		public TMP_Text symbolLabel;
		public ExploreMapNodeHoverEffect hoverEffect;
		public TMP_Text title;
		public TMP_Text desc;
	}

	static MapNodeHandles CreateMapNode(
		GameObject parent,
		string name,
		Vector2 contentPosition,
		Vector2 size,
		string topText,
		string titleText,
		string descText)
	{
		var root = CreateImage(parent, name, MapNodeHitArea);
		root.anchorMin = Vector2.zero;
		root.anchorMax = Vector2.zero;
		root.pivot = new Vector2(0.5f, 0.5f);
		root.sizeDelta = size;
		root.anchoredPosition = contentPosition;
		var hitGraphic = root.GetComponent<Graphic>();
		hitGraphic.raycastTarget = true;

		var topLabel = CreateTMPText(root.gameObject, "TopLabel", topText,
			MapNodeLabelFontSize, AccentYellow, TextAlignmentOptions.Center);
		var topLabelRt = topLabel.GetComponent<RectTransform>();
		topLabelRt.anchorMin = new Vector2(0.5f, 0.5f);
		topLabelRt.anchorMax = new Vector2(0.5f, 0.5f);
		topLabelRt.pivot = new Vector2(0.5f, 0.5f);
		topLabelRt.sizeDelta = new Vector2(120f, 24f);
		topLabelRt.anchoredPosition = Vector2.zero;
		topLabel.fontStyle = FontStyles.Bold;
		topLabel.textWrappingMode = TextWrappingModes.NoWrap;
		topLabel.raycastTarget = false;
		topLabel.gameObject.SetActive(false);

		var stateShadow = CreateImage(root.gameObject, "StateShadow", MapNodeShadow);
		ConfigureMapNodeDiamond(
			stateShadow,
			MapNodeDefaultPlateSize + MapNodeShadowPadding,
			new Vector2(3f, -3f));
		var shadowGraphic = stateShadow.GetComponent<Graphic>();
		shadowGraphic.raycastTarget = false;

		var stateOutline = CreateImage(root.gameObject, "StateOutline", MapNodeDark);
		ConfigureMapNodeDiamond(stateOutline, MapNodeDefaultPlateSize + MapNodeOutlinePadding, Vector2.zero);
		var outlineGraphic = stateOutline.GetComponent<Graphic>();
		outlineGraphic.raycastTarget = false;

		var initialPlateColor = new Color(1f, 0.86f, 0.42f, 1f);
		var statePlate = CreateImage(root.gameObject, "StatePlate", initialPlateColor);
		ConfigureMapNodeDiamond(statePlate, MapNodeDefaultPlateSize, Vector2.zero);
		var stateGraphic = statePlate.GetComponent<Graphic>();
		stateGraphic.raycastTarget = false;

		var stateAccent = CreateImage(root.gameObject, "StateAccent", initialPlateColor);
		ConfigureMapNodeDiamond(
			stateAccent,
			MapNodeAccentSize,
			new Vector2(
				MapNodeDefaultPlateSize * MapNodeAccentOffsetRatio,
				-MapNodeDefaultPlateSize * MapNodeAccentOffsetRatio));
		var accentGraphic = stateAccent.GetComponent<Graphic>();
		accentGraphic.raycastTarget = false;

		var icon = CreateImage(root.gameObject, "IconImage", Color.white);
		icon.anchorMin = new Vector2(0.5f, 0.5f);
		icon.anchorMax = new Vector2(0.5f, 0.5f);
		icon.pivot = new Vector2(0.5f, 0.5f);
		icon.sizeDelta = new Vector2(MapNodeIconSize, MapNodeIconSize);
		icon.anchoredPosition = Vector2.zero;
		var iconImage = icon.GetComponent<Image>();
		iconImage.preserveAspect = true;
		iconImage.raycastTarget = false;
		var iconOutline = icon.gameObject.AddComponent<Outline>();
		iconOutline.effectColor = new Color(1f, 0.90f, 0.45f, 0.96f);
		iconOutline.effectDistance = new Vector2(4f, -4f);
		iconOutline.useGraphicAlpha = true;
		iconOutline.enabled = false;

		var symbolLabel = CreateTMPText(root.gameObject, "SymbolLabel", "",
			MapSymbolFontSize, MapNodeDark, TextAlignmentOptions.Center);
		var symbolRt = symbolLabel.GetComponent<RectTransform>();
		symbolRt.anchorMin = new Vector2(0.5f, 0.5f);
		symbolRt.anchorMax = new Vector2(0.5f, 0.5f);
		symbolRt.pivot = new Vector2(0.5f, 0.5f);
		symbolRt.sizeDelta = new Vector2(MapNodeDefaultPlateSize, MapNodeDefaultPlateSize);
		symbolRt.anchoredPosition = Vector2.zero;
		symbolLabel.fontStyle = FontStyles.Bold;
		symbolLabel.raycastTarget = false;
		AddMapTextEffects(symbolLabel, new Color(1f, 0.95f, 0.70f, 0.65f));
		symbolLabel.gameObject.SetActive(false);

		var hoverBorderRoot = CreateMapNodeHoverBorder(root.gameObject, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));

		var title = CreateTMPText(root.gameObject, "BottomLabel", titleText,
			MapReadableLabelFontSize, AccentYellow, TextAlignmentOptions.Center);
		var titleRt = title.GetComponent<RectTransform>();
		titleRt.anchorMin = new Vector2(0.5f, 0.5f);
		titleRt.anchorMax = new Vector2(0.5f, 0.5f);
		titleRt.pivot = new Vector2(0.5f, 0.5f);
		titleRt.sizeDelta = new Vector2(176f, 44f);
		titleRt.anchoredPosition = new Vector2(0f, MapNodeTitleOffset);
		title.fontStyle = FontStyles.Bold;
		title.enableAutoSizing = true;
		title.fontSizeMin = 30f;
		title.fontSizeMax = MapReadableLabelFontSize;
		title.textWrappingMode = TextWrappingModes.NoWrap;
		title.raycastTarget = false;
		AddMapTextEffects(title, new Color(0.05f, 0.03f, 0.02f, 0.92f));
		title.gameObject.SetActive(false);

		var desc = CreateTMPText(root.gameObject, "Desc", descText,
			1, MapNodeText, TextAlignmentOptions.Center);
		var descRt = desc.GetComponent<RectTransform>();
		descRt.anchorMin = Vector2.zero;
		descRt.anchorMax = Vector2.zero;
		descRt.offsetMin = Vector2.zero;
		descRt.offsetMax = Vector2.zero;
		desc.textWrappingMode = TextWrappingModes.Normal;
		desc.gameObject.SetActive(false);

		var hoverEffect = root.gameObject.AddComponent<ExploreMapNodeHoverEffect>();
		SetField(hoverEffect, "animatedTarget", root);
		SetField(hoverEffect, "borderRoot", hoverBorderRoot.gameObject);
		SetField(hoverEffect, "labelRoot", title.gameObject);
		SetField(hoverEffect, "iconOutline", iconOutline);
		SetField(hoverEffect, "scaleFactor", 1.06f);

		var nodeView = root.gameObject.AddComponent<ExploreMapNodeDisplay>();
		SetField(nodeView, "root", root);
		SetField(nodeView, "shadowGraphic", shadowGraphic);
		SetField(nodeView, "outlineGraphic", outlineGraphic);
		SetField(nodeView, "stateGraphic", stateGraphic);
		SetField(nodeView, "accentGraphic", accentGraphic);
		SetField(nodeView, "iconLabel", topLabel);
		SetField(nodeView, "iconImage", iconImage);
		SetField(nodeView, "symbolLabel", symbolLabel);
		SetField(nodeView, "hoverEffect", hoverEffect);
		SetField(nodeView, "title", title);
		SetField(nodeView, "description", desc);

		return new MapNodeHandles
		{
			root = root,
			graphic = stateGraphic,
			hitGraphic = hitGraphic,
			view = nodeView,
			iconLabel = topLabel,
			iconImage = iconImage,
			symbolLabel = symbolLabel,
			hoverEffect = hoverEffect,
			title = title,
			desc = desc
		};
	}

	static void ConfigureMapNodeDiamond(RectTransform rect, float size, Vector2 anchoredPosition)
	{
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(size, size);
		rect.anchoredPosition = anchoredPosition;
		rect.localRotation = Quaternion.Euler(0f, 0f, 45f);
		rect.localScale = Vector3.one;
	}

	static void AddMapTextEffects(TMP_Text text, Color outlineColor)
	{
		var outline = text.gameObject.AddComponent<Outline>();
		outline.effectColor = outlineColor;
		outline.effectDistance = new Vector2(2f, -2f);
		outline.useGraphicAlpha = true;

		var shadow = text.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0.03f, 0.02f, 0.01f, 0.78f);
		shadow.effectDistance = new Vector2(3f, -3f);
		shadow.useGraphicAlpha = true;
	}

	static RectTransform CreateMapNodeHoverBorder(GameObject parent, Vector2 anchorMin, Vector2 anchorMax)
	{
		const float BorderThickness = 4f;
		var borderRoot = CreateEmpty(parent, "HoverBorderRoot");
		borderRoot.anchorMin = anchorMin;
		borderRoot.anchorMax = anchorMax;
		borderRoot.offsetMin = new Vector2(-6f, -6f);
		borderRoot.offsetMax = new Vector2(6f, 6f);

		CreateMapNodeBorderStrip(
			borderRoot,
			"BorderTop",
			new Vector2(0f, 1f),
			new Vector2(1f, 1f),
			new Vector2(0.5f, 1f),
			new Vector2(0f, BorderThickness));
		CreateMapNodeBorderStrip(
			borderRoot,
			"BorderBottom",
			new Vector2(0f, 0f),
			new Vector2(1f, 0f),
			new Vector2(0.5f, 0f),
			new Vector2(0f, BorderThickness));
		CreateMapNodeBorderStrip(
			borderRoot,
			"BorderLeft",
			new Vector2(0f, 0f),
			new Vector2(0f, 1f),
			new Vector2(0f, 0.5f),
			new Vector2(BorderThickness, 0f));
		CreateMapNodeBorderStrip(
			borderRoot,
			"BorderRight",
			new Vector2(1f, 0f),
			new Vector2(1f, 1f),
			new Vector2(1f, 0.5f),
			new Vector2(BorderThickness, 0f));

		borderRoot.gameObject.SetActive(false);
		return borderRoot;
	}

	static void CreateMapNodeBorderStrip(
		RectTransform parent,
		string name,
		Vector2 anchorMin,
		Vector2 anchorMax,
		Vector2 pivot,
		Vector2 sizeDelta)
	{
		var strip = CreateImage(parent.gameObject, name, new Color(1f, 0.90f, 0.45f, 0.96f));
		strip.anchorMin = anchorMin;
		strip.anchorMax = anchorMax;
		strip.pivot = pivot;
		strip.anchoredPosition = Vector2.zero;
		strip.sizeDelta = sizeDelta;
		strip.GetComponent<Image>().raycastTarget = false;
	}

	static Image CreateMapLine(RectTransform parent, string name, Vector2 from, Vector2 to)
	{
		var line = SceneBuilderUtility.CreateImage(parent, name, new Color(0.10f, 0.07f, 0.045f, 0.90f));
		var rt = line.GetComponent<RectTransform>();
		Vector2 midpoint = (from + to) * 0.5f;
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.zero;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = midpoint;
		Vector2 delta = to - from;
		rt.sizeDelta = new Vector2(Mathf.Max(8f, delta.magnitude), 5f);
		rt.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
		var image = line.GetComponent<Image>();
		image.raycastTarget = false;
		return image;
	}

	static RectTransform CreateCircleGraphic(GameObject parent, string name, Color color, bool raycastTarget = true)
	{
		var rt = SceneBuilderUtility.CreateImage(parent.transform, name, color, raycastTarget);
		var image = rt.GetComponent<Image>();
		image.raycastTarget = raycastTarget;
		return rt;
	}

	static Vector2 ResolveMapNodeCenter(int row, int lane)
	{
		return ExploreMapLayout.ResolveDefaultNodeCenter(row, lane);
	}

	static Vector2 ResolveMapNodeSize(ExploreMapNodeKind kind)
	{
		return ExploreMapLayout.ResolveNodeSize(kind);
	}

	static string ResolveStageMapTitle(StageData stage)
	{
		if (stage == null)
			return "";
		if (!string.IsNullOrEmpty(stage.mapTitle))
			return stage.mapTitle;
		return stage.displayName ?? "";
	}

	static void SetMapNodeButtonColors(Button btn)
	{
		var cb = btn.colors;
		cb.normalColor = Color.white;
		cb.highlightedColor = new Color(1f, 0.92f, 0.55f, 1f);
		cb.pressedColor = new Color(0.84f, 0.78f, 0.56f, 1f);
		cb.selectedColor = cb.highlightedColor;
		btn.colors = cb;
	}

	static Sprite LoadMapUiSprite(string path)
	{
		EnsureMapUiSprite(path);
		var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
		if (sprite == null)
			Debug.LogWarning($"[GameExploreSceneBuilder] 지도 UI 스프라이트 로드 실패: {path}");
		return sprite;
	}

	static void EnsureMapUiSprite(string path)
	{
		if (string.IsNullOrEmpty(path) || !System.IO.File.Exists(path))
		{
			Debug.LogWarning($"[GameExploreSceneBuilder] 지도 UI 스프라이트 파일 없음: {path}");
			return;
		}

		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null)
		{
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			importer = AssetImporter.GetAtPath(path) as TextureImporter;
		}
		if (importer == null)
			return;

		bool reimport = false;
		if (importer.textureType != TextureImporterType.Sprite)
		{
			importer.textureType = TextureImporterType.Sprite;
			reimport = true;
		}
		if (importer.spriteImportMode != SpriteImportMode.Single)
		{
			importer.spriteImportMode = SpriteImportMode.Single;
			reimport = true;
		}
		if (importer.filterMode != FilterMode.Point)
		{
			importer.filterMode = FilterMode.Point;
			reimport = true;
		}
		if (importer.mipmapEnabled)
		{
			importer.mipmapEnabled = false;
			reimport = true;
		}
		if (importer.textureCompression != TextureImporterCompression.Uncompressed)
		{
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			reimport = true;
		}
		if (importer.npotScale != TextureImporterNPOTScale.None)
		{
			importer.npotScale = TextureImporterNPOTScale.None;
			reimport = true;
		}
		if (Mathf.Abs(importer.spritePixelsPerUnit - 100f) > 0.001f)
		{
			importer.spritePixelsPerUnit = 100f;
			reimport = true;
		}

		var settings = new TextureImporterSettings();
		importer.ReadTextureSettings(settings);
		if (settings.spriteMeshType != SpriteMeshType.FullRect)
		{
			settings.spriteMeshType = SpriteMeshType.FullRect;
			importer.SetTextureSettings(settings);
			reimport = true;
		}

		if (reimport)
			importer.SaveAndReimport();
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

	static GameObject CreateMenuButton(GameObject parent, string name, string label,
		Vector2 anchorMin, Vector2 anchorMax)
	{
		return SceneBuilderUtility.CreateAnchoredButton(
			parent.transform, name, label, anchorMin, anchorMax,
			SceneBuilderUtility.ButtonNormal);
	}

	static void SetButtonColors(Button btn)
		=> SceneBuilderUtility.SetButtonColorSet(btn,
			SceneBuilderUtility.ButtonNormal,
			SceneBuilderUtility.ButtonHighlight,
			SceneBuilderUtility.ButtonPressed);

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
