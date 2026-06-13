using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEditor.Events;
using TMPro;
using Mahjong;

/// <summary>
/// MahjongBattleScene 빌더. DiceBattleScene의 적 패널 레이아웃/스테이지 번들/디버그 콘솔/오디오 매니저 패턴을 미러링하되,
/// 하단 40%는 마작 특유의 손패 스트립과 액션 버튼을 중심으로 재구성.
/// </summary>
public static class MahjongBattleSceneBuilder
{
	// ── 색상 (DiceBattle 톤 참조) ──
	static readonly Color BgColor          = new Color(0.06f, 0.10f, 0.08f);
	static readonly Color PanelBg          = new Color(0.10f, 0.14f, 0.20f, 0.92f);
	static readonly Color EnemyHpFill      = new Color(0.85f, 0.25f, 0.25f);
	static readonly Color TargetMarkerColor= new Color(1f, 0.85f, 0.2f, 0.9f);
	static readonly Color TileBg           = new Color(0.92f, 0.88f, 0.74f);
	static readonly Color ActionButtonBg   = new Color(0.40f, 0.30f, 0.55f, 0.95f);
	static readonly Color ActionButtonHi   = new Color(0.58f, 0.45f, 0.72f, 1f);
	static readonly Color TempButtonBg     = new Color(0.30f, 0.35f, 0.45f, 0.95f);
	static readonly Color TempButtonHi     = new Color(0.45f, 0.52f, 0.62f, 1f);

	// DiceBattle과 완전 일치 — 지면 기준 Y(배경 흙길 높이).
	const float GroundY = 0.44f;

	[MenuItem("Tools/Build MahjongBattle Scene")]
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
		SceneBuilderUtility.BeginSceneBuildValidation(nameof(MahjongBattleSceneBuilder));
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var shell = SceneBuilderUtility.BuildSceneShell("Main Camera", BgColor);
		var canvasRT = shell.canvasRoot;

		// ── 스테이지 번들 (편집 시점 스프라이트 프리로드) ──
		var stageBundles = SceneBuilderUtility.BuildAllStageBundles();

		var fightBackground = SceneBuilderUtility.BuildStageBackground(
			canvasRT, "FightBackgroundMask", "FightBackground", BgColor);

		var playerRig = SceneBuilderUtility.BuildBattlePlayerRig(canvasRT, GroundY, includeJumpBelow: false);

		// ═══════════════════════════════════════════════════════════════
		// 상단 — 적 패널 스트립 (DiceBattle EnemySlotsArea 미러)
		// ═══════════════════════════════════════════════════════════════

		var enemySlots = SceneBuilderUtility.BuildBattleEnemySlots(
			canvasRT, GroundY, EnemyHpFill, TargetMarkerColor);
		var waitDisplays = new EnemyWaitTilesDisplay[4];

		for (int i = 0; i < 4; i++)
			waitDisplays[i] = BuildWaitTilesUnderEnemy(enemySlots.slotRoots[i]);

		// ── 데미지 스폰 영역 (적 슬롯 위쪽에 겹침 — DiceBattle 동일 위치) ──
		var dmgSpawn = SceneBuilderUtility.BuildBattleDamageSpawnArea(canvasRT, GroundY);

		// ── 플레이어 하트 (좌상단 — DiceBattle 동일 위치/스타일) ──
		var heartDisplay = SceneBuilderUtility.BuildHeartDisplay(
			canvasRT, "PlayerHeartDisplay",
			new Vector2(0.02f, 0.88f), new Vector2(0.56f, 0.995f));

		// ═══════════════════════════════════════════════════════════════
		// 하단 40% — 마작 UI (좌우로 긴 손패 + 도라/버림/버튼)
		// ═══════════════════════════════════════════════════════════════

		// UIBackground (하단 40% 전체) — 테이블 이미지 뒤의 어두운 받침
		var uiBg = SceneBuilderUtility.CreateImage(canvasRT, "UIBackground", new Color(0.035f, 0.055f, 0.035f, 1f));
		AnchorBox(uiBg, 0f, 0f, 1f, 0.4f);

		// LowerArea: bottom 40% input focus panel
		var lowerArea = SceneBuilderUtility.CreateEmpty(canvasRT, "LowerArea");
		AnchorBox(lowerArea, 0f, 0f, 1f, 0.4f);

		const string tablePath = "Assets/Mahjong/Table.png";
		if (System.IO.File.Exists(tablePath))
		{
			SceneBuilderUtility.EnsurePixelSprite(tablePath);
			var tableSprite = AssetDatabase.LoadAssetAtPath<Sprite>(tablePath);
			if (tableSprite != null)
			{
				var tableBg = SceneBuilderUtility.CreateImage(lowerArea, "MahjongTable", Color.white);
				tableBg.anchorMin = new Vector2(0.5f, 0f);
				tableBg.anchorMax = new Vector2(0.5f, 0f);
				tableBg.pivot = new Vector2(0.5f, 0f);
				tableBg.anchoredPosition = Vector2.zero;
				tableBg.sizeDelta = new Vector2(1920f, 350f);
				var tableImg = tableBg.GetComponent<Image>();
				tableImg.sprite = tableSprite;
				tableImg.preserveAspect = true;
				tableImg.raycastTarget = false;
			}
		}
		else
		{
			Debug.LogWarning($"[MahjongBattleSceneBuilder] 마작 테이블 이미지 없음: {tablePath}");
		}

		// [Top] 액션 버튼 — 하단 40% 영역 위쪽
		var actionPanel = SceneBuilderUtility.CreateImage(lowerArea, "ActionPanel", new Color(0.02f, 0.04f, 0.03f, 0.18f));
		AnchorBox(actionPanel, 0.05f, 0.82f, 0.95f, 0.98f);

		var btnKan = MakeActionBtn(actionPanel, "KanButton",    "깡",        0.01f, 0.14f, 0.15f, 0.86f, ActionButtonBg, ActionButtonHi);
		var btnRii = MakeActionBtn(actionPanel, "RiichiButton", "리치",       0.17f, 0.14f, 0.31f, 0.86f, ActionButtonBg, ActionButtonHi);
		var btnT1  = MakeActionBtn(actionPanel, "Temp1Button",  "중간공격", 0.33f, 0.14f, 0.52f, 0.86f, TempButtonBg,   TempButtonHi);
		var btnCancel = MakeActionBtn(actionPanel, "CancelButton", "취소",    0.80f, 0.14f, 0.98f, 0.86f,
			new Color(0.55f, 0.18f, 0.18f, 0.9f), new Color(0.70f, 0.25f, 0.25f, 1f));

		// [Middle] 도라(좌) + 버림패(중/우) — 초록 필드 아래쪽 프레임 위
		var row1 = SceneBuilderUtility.CreateEmpty(lowerArea, "Row1_DoraDiscard");
		AnchorBox(row1, 0.10f, 0.15f, 0.90f, 0.32f);

		var doraStrip = SceneBuilderUtility.CreateImage(row1, "DoraStrip", new Color(0.03f, 0.09f, 0.05f, 0.55f));
		AnchorBox(doraStrip, 0f, 0f, 0.28f, 1f);
		var doraLabel = SceneBuilderUtility.CreateTMPText(doraStrip, "Label", "도라", 18, new Color(0.86f, 0.96f, 0.78f), TextAlignmentOptions.TopLeft);
		AnchorBox(doraLabel.GetComponent<RectTransform>(), 0.03f, 0.78f, 1f, 1f);
		var doraRoot = SceneBuilderUtility.CreateEmpty(doraStrip, "DoraTiles");
		AnchorBox(doraRoot, 0.03f, 0.06f, 0.97f, 0.90f);
		AddHLayout(doraRoot, spacing: 4f, alignment: TextAnchor.MiddleLeft);

		var discardStrip = SceneBuilderUtility.CreateImage(row1, "DiscardStrip", new Color(0.03f, 0.09f, 0.05f, 0.48f));
		AnchorBox(discardStrip, 0.30f, 0f, 1f, 1f);
		var discardLabel = SceneBuilderUtility.CreateTMPText(discardStrip, "Label", "버림패", 18, new Color(0.86f, 0.96f, 0.78f), TextAlignmentOptions.TopLeft);
		AnchorBox(discardLabel.GetComponent<RectTransform>(), 0.02f, 0.78f, 1f, 1f);
		var discardRoot = SceneBuilderUtility.CreateEmpty(discardStrip, "DiscardTiles");
		AnchorBox(discardRoot, 0.02f, 0.06f, 0.98f, 0.90f);
		var discardGrid = discardRoot.gameObject.AddComponent<GridLayoutGroup>();
		discardGrid.cellSize = new Vector2(36, 50);
		discardGrid.spacing = new Vector2(3, 3);
		discardGrid.childAlignment = TextAnchor.UpperLeft;

		// [Bottom] 손패 (Table.png의 초록 필드 하단 경계에 맞춤)
		var row3 = SceneBuilderUtility.CreateImage(lowerArea, "HandStrip", new Color(0.02f, 0.06f, 0.035f, 0.35f));
		AnchorBox(row3, 0.072f, 0.36f, 0.965f, 0.74f);

		var handLabel = SceneBuilderUtility.CreateTMPText(row3, "HandLabel", "손패", 20, new Color(0.86f, 0.96f, 0.78f), TextAlignmentOptions.TopLeft);
		AnchorBox(handLabel.GetComponent<RectTransform>(), 0.01f, 0.78f, 0.10f, 1f);

		// 손패/쯔모패는 도라·버림(프리팹 기본 52x72)보다 가로 2배(104x72)로 강제 — GridLayoutGroup이 자식 크기 override.
		var handTiles = SceneBuilderUtility.CreateEmpty(row3, "HandTiles");
		AnchorBox(handTiles, 0.005f, 0f, 0.875f, 1f);
		var handGrid = handTiles.gameObject.AddComponent<GridLayoutGroup>();
		handGrid.cellSize = new Vector2(105, 147);
		handGrid.spacing = new Vector2(3, 0);
		handGrid.childAlignment = TextAnchor.MiddleLeft;
		handGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
		handGrid.startAxis = GridLayoutGroup.Axis.Horizontal;

		var drawSlot = SceneBuilderUtility.CreateEmpty(row3, "DrawSlot");
		AnchorBox(drawSlot, 0.89f, 0f, 0.985f, 1f);
		var drawGrid = drawSlot.gameObject.AddComponent<GridLayoutGroup>();
		drawGrid.cellSize = new Vector2(105, 147);
		drawGrid.spacing = new Vector2(3, 0);
		drawGrid.childAlignment = TextAnchor.MiddleCenter;

		var bottomFocusHandles = SceneBuilderUtility.BuildBattleBottomFocus(canvasRT, lowerArea, PanelBg);
		var battleLog = bottomFocusHandles.log;

		// ═══════════════════════════════════════════════════════════════
		// 프리팹들
		// ═══════════════════════════════════════════════════════════════
		var tilePrefab = BuildTilePrefab();

		// 타일 스프라이트 DB — Assets/Mahjong/MahjongTileSprites.asset을 로드해 컨트롤러에 주입.
		// 에셋이 없으면 경고만 내고 진행 (런타임에 null이면 색상/라벨 폴백 동작).
		const string tileDbPath = "Assets/Mahjong/MahjongTileSprites.asset";
		var tileSprites = AssetDatabase.LoadAssetAtPath<MahjongTileSpriteDatabase>(tileDbPath);
		if (tileSprites == null)
			Debug.LogWarning($"[MahjongBattleSceneBuilder] {tileDbPath} 없음 — Create > Mahjong > Tile Sprite Database로 에셋을 만들고 스프라이트를 드래그로 할당하세요.");

		// ═══════════════════════════════════════════════════════════════
		// BattleRoot (VFX + Animations) + 컨트롤러 (DiceBattle과 동일 구조)
		// ═══════════════════════════════════════════════════════════════
		var battleRootHandles = SceneBuilderUtility.BuildBattleRootBase<MahjongBattleController>(
			fightBackground, stageBundles, playerRig, enemySlots, GroundY,
			heartDisplay, battleLog, bottomFocusHandles.focus, dmgSpawn);
		var battleRoot = battleRootHandles.root;
		var ctrl = battleRootHandles.controller;

		var drawAnimator = battleRoot.AddComponent<MahjongDrawTileAnimator>();
		var intuitionCfg = battleRoot.AddComponent<MahjongIntuitionConfig>();
		var attackProjectileVfx = battleRoot.AddComponent<EnemyAttackProjectileVfx>();
		SceneBuilderUtility.SetField(attackProjectileVfx, "vfxParent", canvasRT);
		var enemyProjectileImg = SceneBuilderUtility.CreateEnemyProjectileImage(
			canvasRT,
			"EnemyProjectile",
			"Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png");
		var weaponProjectileImg = SceneBuilderUtility.CreatePlayerWeaponProjectileImage(canvasRT);
		var attackAnimator = SceneBuilderUtility.AddPlayerAttackAnimator(
			battleRoot, playerRig.bodyImage, playerRig.bodyAnimator, weaponProjectileImg);

		var waitPanel = BuildWaitInfoPanel(canvasRT, tilePrefab, tileSprites);
		var ronBubble = BuildRonBubble(canvasRT);

		// 대기패 디스플레이에 스프라이트 DB 주입은 런타임 InitWaitDisplays가 처리.
		for (int wi = 0; wi < waitDisplays.Length; wi++)
		{
			if (waitDisplays[wi] == null) continue;
			// 기본 뒷면 표시 — 런타임에서도 재초기화됨. 편집 시점에서 스프라이트 DB로 한 번 세팅.
		}

		SceneBuilderUtility.SetField(ctrl, "enemyPanelButtons", enemySlots.buttons);

		SceneBuilderUtility.SetField(ctrl, "doraIndicatorRoot", doraRoot.transform);
		SceneBuilderUtility.SetField(ctrl, "handTilesRoot", handTiles.transform);
		SceneBuilderUtility.SetField(ctrl, "drawTileSlot", drawSlot.transform);
		SceneBuilderUtility.SetField(ctrl, "discardRoot", discardRoot.transform);
		SceneBuilderUtility.SetField(ctrl, "tilePrefab", tilePrefab);
		SceneBuilderUtility.SetField(ctrl, "tileSprites", tileSprites);

		SceneBuilderUtility.SetField(ctrl, "kanButton", btnKan.GetComponent<Button>());
		SceneBuilderUtility.SetField(ctrl, "riichiButton", btnRii.GetComponent<Button>());
		SceneBuilderUtility.SetField(ctrl, "tempButton1", btnT1.GetComponent<Button>());
		SceneBuilderUtility.SetField(ctrl, "cancelButton", btnCancel.GetComponent<Button>());
		SceneBuilderUtility.SetField(ctrl, "drawAnimator", drawAnimator);
		SceneBuilderUtility.SetField(ctrl, "waitInfoPanel", waitPanel.GetComponent<MahjongWaitInfoPanel>());
		SceneBuilderUtility.SetField(ctrl, "waitDisplays", waitDisplays);
		SceneBuilderUtility.SetField(ctrl, "ronBubble", ronBubble);
		SceneBuilderUtility.SetField(ctrl, "intuitionConfig", intuitionCfg);
		SceneBuilderUtility.SetField(ctrl, "enemyProjectile", enemyProjectileImg);
		SceneBuilderUtility.SetField(ctrl, "attackProjectileVfx", attackProjectileVfx);
		SceneBuilderUtility.SetField(ctrl, "attackAnimator", attackAnimator);
		SceneBuilderUtility.SetField(ctrl, "enemyTsumoChancePerTurn", 0.02f);
		SceneBuilderUtility.SetField(ctrl, "rank3WaitRevealChancePerTurn", 0.05f);

		UnityEventTools.AddPersistentListener(btnKan.GetComponent<Button>().onClick, ctrl.OnDeclareKan);
		UnityEventTools.AddPersistentListener(btnRii.GetComponent<Button>().onClick, ctrl.OnDeclareRiichi);
		UnityEventTools.AddPersistentListener(btnT1 .GetComponent<Button>().onClick, ctrl.OnPartialAttack);
		UnityEventTools.AddPersistentListener(btnCancel.GetComponent<Button>().onClick, ctrl.CancelBattle);

		// ── 디버그 콘솔 (DiceBattle 동일 패턴) ──
		SceneBuilderUtility.BuildDebugConsole();

		// ── 오디오 매니저 ──
		SceneBuilderUtility.BuildAudioManager(new[]
		{
			"Player_Attack", "Player_Attack_Small", "Player_Attack_Medium", "Player_Attack_Big",
			"Enemy123_Attack", "Enemy_Die", "Player_Death",
			"UI_Back_NO", "Transition_2"
		}, includeDrumRoll: false);

		// ── 저장 ──
		return SceneBuilderUtility.SaveSceneAndShowDialog(scene,
			"Assets/Scenes/MahjongBattleScene.unity",
			"MahjongBattleScene 생성 완료!",
			showDialog: showCompletionDialog);
	}

	// ── 헬퍼 ──

	static GameObject MakeActionBtn(RectTransform parent, string name, string label,
		float xMin, float yMin, float xMax, float yMax, Color bg, Color hi)
	{
		var go = SceneBuilderUtility.CreateButton(parent, name, label, 22,
			bg, hi, new Color(bg.r * 0.6f, bg.g * 0.6f, bg.b * 0.6f, 1f));
		AnchorBox(go.GetComponent<RectTransform>(), xMin, yMin, xMax, yMax);
		return go;
	}

	static void AnchorBox(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
	{
		SceneBuilderUtility.AnchorBox(rt, xMin, yMin, xMax, yMax);
	}

	static void AddHLayout(RectTransform parent, float spacing, TextAnchor alignment)
	{
		var h = parent.gameObject.AddComponent<HorizontalLayoutGroup>();
		h.spacing = spacing;
		h.childAlignment = alignment;
		h.childForceExpandWidth = false;
		h.childForceExpandHeight = false;
	}

	static GameObject BuildTilePrefab()
	{
		var go = new GameObject("MahjongTile");
		var rt = go.AddComponent<RectTransform>();
		rt.sizeDelta = new Vector2(52, 72);

		// 호버 lift는 자식 Content를 움직여서 GridLayoutGroup의 anchoredPosition 덮어쓰기를 회피.
		var content = new GameObject("Content");
		var contentRT = content.AddComponent<RectTransform>();
		contentRT.SetParent(go.transform, false);
		SceneBuilderUtility.Stretch(contentRT);

		// 호버 시에만 활성화되는 보조 raycast 캐처 — Content가 위로 떠서 비는 원래 자리를 메워
		// 마우스가 원래 셀 위에 있을 때 OnPointerExit가 즉시 튀지 않도록 한다.
		var extenderGO = new GameObject("HoverHitboxExtender");
		var extenderRT = extenderGO.AddComponent<RectTransform>();
		extenderRT.SetParent(go.transform, false);
		SceneBuilderUtility.Stretch(extenderRT);
		var extenderImg = extenderGO.AddComponent<Image>();
		extenderImg.color = new Color(0f, 0f, 0f, 0f);
		extenderImg.raycastTarget = true;
		extenderGO.SetActive(false);

		var bg = content.AddComponent<Image>();
		bg.color = TileBg;
		bg.raycastTarget = true;

		var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(SceneBuilderUtility.FontPath);

		var labelGO = new GameObject("Label");
		var labelRT = labelGO.AddComponent<RectTransform>();
		labelRT.SetParent(content.transform, false);
		SceneBuilderUtility.Stretch(labelRT);
		var label = labelGO.AddComponent<TextMeshProUGUI>();
		if (font != null) label.font = font;
		label.text = "?";
		label.alignment = TextAlignmentOptions.Center;
		label.fontSize = 22;
		label.color = Color.black;
		label.raycastTarget = false;

		var redGO = new GameObject("RedMarker");
		var redRT = redGO.AddComponent<RectTransform>();
		redRT.SetParent(content.transform, false);
		redRT.anchorMin = new Vector2(0f, 0f);
		redRT.anchorMax = new Vector2(1f, 0.15f);
		redRT.offsetMin = Vector2.zero;
		redRT.offsetMax = Vector2.zero;
		var redImg = redGO.AddComponent<Image>();
		redImg.color = new Color(0.9f, 0.1f, 0.1f, 1f);
		redImg.raycastTarget = false;
		redGO.SetActive(false);

		// 강조 테두리 — Outline 컴포넌트 대신 Image 4장으로 둘러쌈 (간단한 노란 테두리).
		var borderGO = new GameObject("HighlightBorder");
		var borderRT = borderGO.AddComponent<RectTransform>();
		borderRT.SetParent(content.transform, false);
		SceneBuilderUtility.Stretch(borderRT);
		var borderImg = borderGO.AddComponent<Image>();
		borderImg.color = new Color(1f, 0.92f, 0.4f, 0.9f);
		borderImg.raycastTarget = false;
		SceneBuilderUtility.MakeEnemyTargetBorders(borderRT, 0.06f, new Color(1f, 0.92f, 0.4f, 0.95f));
		// 본체 Image는 투명 — 자식 4장이 테두리 역할.
		borderImg.color = new Color(0f, 0f, 0f, 0f);
		borderGO.SetActive(false);

		// 한글 이름 라벨 — 타일 위쪽에 떠 있음. 가로폭은 타일 폭에 맞춰 auto-size.
		var nameGO = new GameObject("NameLabel");
		var nameRT = nameGO.AddComponent<RectTransform>();
		nameRT.SetParent(content.transform, false);
		nameRT.anchorMin = new Vector2(0f, 1f);
		nameRT.anchorMax = new Vector2(1f, 1f);
		nameRT.pivot = new Vector2(0.5f, 0f);
		nameRT.sizeDelta = new Vector2(0f, 28f);
		nameRT.anchoredPosition = new Vector2(0f, 6f);
		var nameLabel = nameGO.AddComponent<TextMeshProUGUI>();
		if (font != null) nameLabel.font = font;
		nameLabel.text = "";
		nameLabel.alignment = TextAlignmentOptions.Center;
		nameLabel.color = new Color(1f, 1f, 0.85f);
		nameLabel.raycastTarget = false;
		nameLabel.enableAutoSizing = true;
		nameLabel.fontSizeMin = 14;
		nameLabel.fontSizeMax = 22;
		nameLabel.textWrappingMode = TextWrappingModes.NoWrap;
		nameGO.SetActive(false);

		// 해골 오버레이 — 쏘인 버림패 표시. TMP 이모지(풍경상 폰트가 미지원이면 □로 보일 수 있음).
		var skullGO = new GameObject("SkullOverlay");
		var skullRT = skullGO.AddComponent<RectTransform>();
		skullRT.SetParent(content.transform, false);
		skullRT.anchorMin = new Vector2(0f, 1f);
		skullRT.anchorMax = new Vector2(1f, 1f);
		skullRT.pivot = new Vector2(0.5f, 0f);
		skullRT.sizeDelta = new Vector2(0f, 30f);
		skullRT.anchoredPosition = new Vector2(0f, 2f);
		var skullText = skullGO.AddComponent<TextMeshProUGUI>();
		if (font != null) skullText.font = font;
		skullText.text = "💀";
		skullText.alignment = TextAlignmentOptions.Center;
		skullText.color = new Color(1f, 0.4f, 0.4f);
		skullText.fontSize = 26;
		skullText.raycastTarget = false;
		skullGO.SetActive(false);

		var visual = go.AddComponent<MahjongTileVisual>();
		var hover = go.AddComponent<MahjongTileHoverEffect>();
		var discardTip = go.AddComponent<MahjongDiscardHoverTooltip>();
		discardTip.enabled = false;

		SceneBuilderUtility.SetField(visual, "background", bg);
		SceneBuilderUtility.SetField(visual, "label", label);
		SceneBuilderUtility.SetField(visual, "redMarker", redImg);
		SceneBuilderUtility.SetField(visual, "skullOverlay", skullGO);
		SceneBuilderUtility.SetField(visual, "hoverEffect", hover);
		SceneBuilderUtility.SetField(visual, "discardTooltip", discardTip);

		SceneBuilderUtility.SetField(hover, "content", contentRT);
		SceneBuilderUtility.SetField(hover, "highlightBorder", borderImg);
		SceneBuilderUtility.SetField(hover, "nameLabel", nameLabel);
		SceneBuilderUtility.SetField(hover, "tileVisual", visual);
		SceneBuilderUtility.SetField(hover, "hitboxExtender", extenderGO);

		return go;
	}

	static EnemyWaitTilesDisplay BuildWaitTilesUnderEnemy(RectTransform slot)
	{
		// 적 바디(slot의 anchor 0..0.9) 아래 "발 밑" 영역. 3개 슬롯(A, B, Need)을 root 중앙에 앵커 →
		// 모든 경우에 적을 기준으로 가운데 정렬. 런타임 컴포넌트가 anchoredPosition으로 직접 배치.
		var root = SceneBuilderUtility.CreateEmpty(slot, "WaitTilesDisplay");
		root.anchorMin = new Vector2(0f, -0.06f);
		root.anchorMax = new Vector2(1f, 0.16f);
		root.offsetMin = Vector2.zero;
		root.offsetMax = Vector2.zero;

		var group = root.gameObject.AddComponent<CanvasGroup>();
		group.blocksRaycasts = false;
		group.interactable = false;
		group.alpha = 1f;

		var slotA = BuildWaitTileSlot(root, "SlotA");
		var slotB = BuildWaitTileSlot(root, "SlotB");
		var slotNeed = BuildWaitTileSlot(root, "SlotNeed");
		// 기본 상태에서 Need는 숨김 (런타임 초기화에서도 ApplyBackedLayout가 비활성 보장).
		slotNeed.rt.gameObject.SetActive(false);

		var display = root.gameObject.AddComponent<EnemyWaitTilesDisplay>();
		SceneBuilderUtility.SetField(display, "slotA", slotA.rt);
		SceneBuilderUtility.SetField(display, "slotB", slotB.rt);
		SceneBuilderUtility.SetField(display, "slotNeed", slotNeed.rt);
		SceneBuilderUtility.SetField(display, "imgA", slotA.img);
		SceneBuilderUtility.SetField(display, "imgB", slotB.img);
		SceneBuilderUtility.SetField(display, "imgNeed", slotNeed.img);
		SceneBuilderUtility.SetField(display, "markA", slotA.mark);
		SceneBuilderUtility.SetField(display, "markB", slotB.mark);
		SceneBuilderUtility.SetField(display, "markNeed", slotNeed.mark);
		SceneBuilderUtility.SetField(display, "group", group);
		return display;
	}

	struct WaitTileSlotRefs { public RectTransform rt; public Image img; public TMP_Text mark; }

	static WaitTileSlotRefs BuildWaitTileSlot(RectTransform parent, string name)
	{
		var go = new GameObject(name);
		var rt = go.AddComponent<RectTransform>();
		rt.SetParent(parent, false);
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(32f, 44f);
		rt.anchoredPosition = Vector2.zero;

		var img = go.AddComponent<Image>();
		img.color = Color.white;
		img.raycastTarget = false;
		img.preserveAspect = true;

		var mark = SceneBuilderUtility.CreateTMPText(rt, "Mark", "?", 22, Color.black,
			TextAlignmentOptions.Center, FontStyles.Bold);
		SceneBuilderUtility.Stretch(mark.GetComponent<RectTransform>());

		return new WaitTileSlotRefs { rt = rt, img = img, mark = mark };
	}

	static RonSpeechBubble BuildRonBubble(Transform canvas)
	{
		var go = new GameObject("RonBubble");
		var rt = go.AddComponent<RectTransform>();
		rt.SetParent(canvas, false);
		rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0f);
		rt.sizeDelta = new Vector2(160f, 80f);

		var bg = go.AddComponent<Image>();
		bg.color = new Color(0f, 0f, 0f, 0.55f);
		bg.raycastTarget = false;

		var label = SceneBuilderUtility.CreateTMPText(go.transform, "Label", "론!", 42,
			new Color(1f, 0.9f, 0.3f), TextAlignmentOptions.Center, FontStyles.Bold);
		SceneBuilderUtility.Stretch(label.GetComponent<RectTransform>());

		var bubble = go.AddComponent<RonSpeechBubble>();
		SceneBuilderUtility.SetField(bubble, "root", rt);
		SceneBuilderUtility.SetField(bubble, "label", label);

		go.SetActive(false);
		return bubble;
	}

	static GameObject BuildWaitInfoPanel(Transform parent, GameObject tilePrefab, MahjongTileSpriteDatabase db)
	{
		var panelGO = new GameObject("WaitInfoPanel");
		var panelRT = panelGO.AddComponent<RectTransform>();
		panelRT.SetParent(parent, false);
		panelRT.anchorMin = panelRT.anchorMax = new Vector2(0.5f, 0.5f);
		panelRT.pivot = new Vector2(0.5f, 0f);
		panelRT.sizeDelta = new Vector2(280f, 130f);

		var bg = panelGO.AddComponent<Image>();
		bg.color = new Color(0.05f, 0.07f, 0.10f, 0.95f);
		bg.raycastTarget = false;

		// 헤더 (적 이름)
		var header = SceneBuilderUtility.CreateTMPText(panelGO.transform, "Header", "적", 18,
			new Color(1f, 0.85f, 0.55f), TextAlignmentOptions.Center, FontStyles.Bold);
		var hRT = header.GetComponent<RectTransform>();
		hRT.anchorMin = new Vector2(0f, 0.72f);
		hRT.anchorMax = new Vector2(1f, 1f);
		hRT.offsetMin = new Vector2(8f, 0f);
		hRT.offsetMax = new Vector2(-8f, -4f);

		// 대기 패 영역 (작은 타일 3개 + 화살표)
		var tilesGO = new GameObject("WaitTiles");
		var tilesRT = tilesGO.AddComponent<RectTransform>();
		tilesRT.SetParent(panelGO.transform, false);
		tilesRT.anchorMin = new Vector2(0f, 0.28f);
		tilesRT.anchorMax = new Vector2(1f, 0.72f);
		tilesRT.offsetMin = Vector2.zero;
		tilesRT.offsetMax = Vector2.zero;
		var tilesLayout = tilesGO.AddComponent<HorizontalLayoutGroup>();
		tilesLayout.spacing = 4f;
		tilesLayout.childAlignment = TextAnchor.MiddleCenter;
		tilesLayout.childForceExpandWidth = false;
		tilesLayout.childForceExpandHeight = false;

		var arrow = SceneBuilderUtility.CreateTMPText(tilesGO.transform, "Arrow", "→", 22,
			Color.white, TextAlignmentOptions.Center);
		var arrowRT = arrow.GetComponent<RectTransform>();
		arrowRT.sizeDelta = new Vector2(24f, 40f);

		// 피해 표시
		var dmg = SceneBuilderUtility.CreateTMPText(panelGO.transform, "DamageText", "피해: 0 하트", 16,
			new Color(1f, 0.55f, 0.55f), TextAlignmentOptions.Center);
		var dRT = dmg.GetComponent<RectTransform>();
		dRT.anchorMin = new Vector2(0f, 0f);
		dRT.anchorMax = new Vector2(1f, 0.28f);
		dRT.offsetMin = new Vector2(8f, 4f);
		dRT.offsetMax = new Vector2(-8f, 0f);

		var panel = panelGO.AddComponent<MahjongWaitInfoPanel>();
		SceneBuilderUtility.SetField(panel, "root", panelRT);
		SceneBuilderUtility.SetField(panel, "headerText", header);
		SceneBuilderUtility.SetField(panel, "damageText", dmg);
		SceneBuilderUtility.SetField(panel, "tilesRoot", tilesGO.transform);
		SceneBuilderUtility.SetField(panel, "arrowText", arrow);
		SceneBuilderUtility.SetField(panel, "tilePrefab", tilePrefab);
		SceneBuilderUtility.SetField(panel, "tileSprites", db);

		panelGO.SetActive(false);
		return panelGO;
	}
}
