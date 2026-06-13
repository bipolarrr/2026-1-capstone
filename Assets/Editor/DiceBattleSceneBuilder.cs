using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor.Events;

public static class DiceBattleSceneBuilder
{
	// ── 색상 ──
	static readonly Color BgColor = new Color(0.08f, 0.08f, 0.14f);
	static readonly Color PanelBg = new Color(0.12f, 0.14f, 0.24f, 0.95f);
	static readonly Color CancelColor = new Color(0.55f, 0.18f, 0.18f, 0.9f);
	static readonly Color CancelHighlight = new Color(0.70f, 0.25f, 0.25f, 1f);
	static readonly Color EnemyHpFill = new Color(0.85f, 0.25f, 0.25f);
	static readonly Color TargetMarkerColor = new Color(1f, 0.85f, 0.2f, 0.9f);
	static readonly Color AccentYellow = new Color(1f, 0.85f, 0.3f);
	static readonly Color SaveZoneVisual = new Color(0.55f, 0.40f, 0.04f, 0.6f);
	const float DiceSlotCenterZ = 1.2f;
	const float DiceArenaDepth = 4f;
	const float DiceArenaAspect = 968f / 496f;
	const float DiceArenaWidth = DiceArenaDepth * DiceArenaAspect;

	[MenuItem("Tools/Build DiceBattle Scene")]
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
		SceneBuilderUtility.BeginSceneBuildValidation(nameof(DiceBattleSceneBuilder));
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		// ── RenderTexture ──
		EnsureDirectory("Assets/Textures");
		var renderTex = EnsureRenderTexture(
			"Assets/Textures/DiceRenderTexture.renderTexture", 968, 496, FilterMode.Point);

		// ── PhysicsMaterial ──
		string pmPath = "Assets/Physics/DiceBouncy.asset";
		EnsureDirectory("Assets/Physics");
		var bouncyMat = RecreateAsset(pmPath, new PhysicsMaterial("DiceBouncy")
		{
			bounciness = 0.55f,
			dynamicFriction = 0.3f,
			staticFriction = 0.35f,
			bounceCombine = PhysicsMaterialCombine.Maximum
		});

		// Dice.outlineBaseMaterial에 주입: 빌드 시 Shader.Find 실패를 방지하기 위해 에셋으로 사전 생성
		string outlineMatPath = "Assets/Materials/DiceOutline.mat";
		EnsureDirectory("Assets/Materials");
		var outlineMat = AssetDatabase.LoadAssetAtPath<Material>(outlineMatPath);
		if (outlineMat == null)
		{
			var unlitShader = Shader.Find("Universal Render Pipeline/Unlit");
			outlineMat = new Material(unlitShader);
			outlineMat.name = "DiceOutline";
			AssetDatabase.CreateAsset(outlineMat, outlineMatPath);
		}
		var slimeJellyMaterial = EnsureSlimeJellyMaterial();

		// ── Dice3D 레이어 ──
		int diceLayer = EnsureLayer("Dice3D");

		var shell = SceneBuilderUtility.BuildSceneShell("MainCamera", BgColor, includeEventSystem: false);
		var mainCam = shell.camera;
		mainCam.cullingMask = ~(1 << diceLayer);

		// ── DiceCamera (top-down orthographic) ──
		// Dice_Tray.png 큰 주사위칸 내부 바닥(x 360..1328, y 96..592)에 맞춘 카메라.
		var diceCamGo = new GameObject("DiceCamera");
		var diceCam = diceCamGo.AddComponent<Camera>();
		diceCam.clearFlags = CameraClearFlags.SolidColor;
		diceCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		diceCam.orthographic     = true;
		diceCam.orthographicSize = DiceArenaDepth * 0.5f;
		diceCam.cullingMask = 1 << diceLayer;
		diceCam.targetTexture = renderTex;
		diceCamGo.transform.position = new Vector3(0f, 10f, DiceSlotCenterZ);
		diceCamGo.transform.rotation = Quaternion.Euler(90, 0, 0);

		// ── 조명 ──
		var lightGo = new GameObject("DirectionalLight");
		var light = lightGo.AddComponent<Light>();
		light.type = LightType.Directional;
		light.color = new Color(1f, 0.95f, 0.85f);
		light.intensity = 1.2f;
		lightGo.transform.rotation = Quaternion.Euler(45, 30, 0);

		// 벽 전용 PhysicsMaterial (높은 탄성 → 벽에 박히거나 기대지 않음)
		string wallPmPath = "Assets/Physics/WallBouncy.asset";
		var wallMat = RecreateAsset(wallPmPath, new PhysicsMaterial("WallBouncy")
		{
			bounciness = 1.0f,
			dynamicFriction = 0.0f,
			staticFriction = 0.0f,
			bounceCombine = PhysicsMaterialCombine.Maximum
		});

		// ── 물리 환경 ──
		BuildPhysicsEnvironment(diceLayer, bouncyMat, wallMat);
		BuildVault(diceLayer, bouncyMat, wallMat);

		// ── 주사위 5개 ──
		var dicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dices/Prefabs/Dice_d6_mine.prefab");
		if (dicePrefab == null)
		{
			Debug.LogWarning("[DiceBattleSceneBuilder] Dice_d6_mine.prefab 없음 — Tools/Build Dice Prefabs/D6 Mine 실행 후 다시 빌드하세요. 기존 Dice_d6.prefab으로 대체합니다.");
			dicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dices/Prefabs/Dice_d6.prefab");
		}
		var defaultEnemyDiceProfile = EnemyDiceProfile.CreateDefault(dicePrefab);
		var enemyDiceProfileCatalog = CreateEnemyDiceProfileCatalog(dicePrefab);
		NormalizeEnemyDiceProfiles(defaultEnemyDiceProfile, enemyDiceProfileCatalog);
		float dieRadius = 0.2625f;
		var diceArr = CreateDiceSet("Die", Vector3.zero, diceLayer,
			bouncyMat, outlineMat, dicePrefab, dieRadius, 0.525f);

		var canvasGo = shell.canvas.gameObject;

		var fightBackground = SceneBuilderUtility.BuildStageBackground(
			canvasGo.transform, "FightBackgroundMask", "FightBackground", BgColor);

		// ── 지면 기준선 (배경 흙길 높이 — 캔버스 절대 Y) ──
		const float GroundY = 0.44f;

		var playerRig = SceneBuilderUtility.BuildBattlePlayerRig(
			canvasGo.transform, GroundY, includeJumpBelow: true);
		var playerImg = playerRig.bodyImage;
		var bodyAnim = playerRig.bodyAnimator;
		var jumpBelowImg = playerRig.jumpBelowImage;
		var jumpBelowSprites = playerRig.jumpBelowSprites;

		// 스테이지별 스프라이트 번들을 편집 시점에 한 번 로드 — 누락 에셋은 themeColor 폴백
		var stageBundles = SceneBuilderUtility.BuildAllStageBundles();

		var enemySlots = SceneBuilderUtility.BuildBattleEnemySlots(
			canvasGo.transform, GroundY, EnemyHpFill, TargetMarkerColor);
		TMP_Text[] enemyDiceResultTexts = new TMP_Text[4];
		for (int i = 0; i < 4; i++)
		{
			// 적 주사위 결과 텍스트 (이름 위)
			var slot = enemySlots.slotRoots[i];
			var diceResultT = CreateTMPText(slot.gameObject, "DiceResult", "",
				18, AccentYellow, TextAlignmentOptions.Center);
			var drRt = diceResultT.GetComponent<RectTransform>();
			drRt.anchorMin = new Vector2(0.0f, 1.08f);
			drRt.anchorMax = new Vector2(1.0f, 1.20f);
			drRt.offsetMin = Vector2.zero;
			drRt.offsetMax = Vector2.zero;
			diceResultT.fontStyle = FontStyles.Bold;
			enemyDiceResultTexts[i] = diceResultT;
		}

		// 주사위 눈 스프라이트 생성 (1~6)
		var diceFaceSprites = GenerateDiceFaceSprites();

		// 데미지 스폰 영역 (적 슬롯 위쪽에 겹침)
		var dmgSpawn = SceneBuilderUtility.BuildBattleDamageSpawnArea(canvasGo.transform, GroundY);

		// ── 플레이어 하트 (좌상단) ──
		var heartDisplayComp = SceneBuilderUtility.BuildHeartDisplay(
			canvasGo.transform, "PlayerHeartDisplay",
			new Vector2(0.02f, 0.88f), new Vector2(0.56f, 0.995f));

		// ── 하단 40%: UI 배경 이미지 ──
		EnsureTightSprite("Assets/UI/UI_Background.png");
		var uiBgImg = CreateImage(canvasGo, "UIBackground", Color.white);
		uiBgImg.anchorMin = new Vector2(0f, 0f);
		uiBgImg.anchorMax = new Vector2(1f, 0.4f);
		uiBgImg.offsetMin = Vector2.zero;
		uiBgImg.offsetMax = Vector2.zero;
		var uiBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/UI/UI_Background.png");
		if (uiBgSprite != null)
			uiBgImg.GetComponent<Image>().sprite = uiBgSprite;
		else
			uiBgImg.GetComponent<Image>().color = BgColor;

		// ── 하단 40% 영역: 현재 필요한 조작 패널을 크게 표시 ──
		var lowerArea = CreateEmpty(canvasGo, "LowerArea");
		lowerArea.anchorMin = new Vector2(0.03f, 0.02f);
		lowerArea.anchorMax = new Vector2(0.97f, 0.385f);
		lowerArea.offsetMin = Vector2.zero;
		lowerArea.offsetMax = Vector2.zero;

		var dicePanel = CreateImage(canvasGo, "DicePanel",
			new Color(0f, 0f, 0f, 0f));
		dicePanel.anchorMin = new Vector2(1f, 0f);
		dicePanel.anchorMax = new Vector2(1f, 0f);
		dicePanel.pivot = new Vector2(1f, 0f);
		dicePanel.anchoredPosition = Vector2.zero;
		dicePanel.sizeDelta = new Vector2(864f, 432f);
		var dicePanelImage = dicePanel.GetComponent<Image>();
		dicePanelImage.raycastTarget = false;
		dicePanelImage.preserveAspect = true;
		const string diceTrayPath = "Assets/Dices/Dice_Tray.png";
		if (System.IO.File.Exists(diceTrayPath))
		{
			EnsurePixelSprite(diceTrayPath);
			var diceTraySprite = AssetDatabase.LoadAssetAtPath<Sprite>(diceTrayPath);
			if (diceTraySprite != null)
			{
				dicePanelImage.sprite = diceTraySprite;
				dicePanelImage.color = Color.white;
			}
		}
		else
		{
			Debug.LogWarning($"[DiceBattleSceneBuilder] 주사위 트레이 이미지 없음: {diceTrayPath}");
		}

		// ── 저장된 주사위 (트레이 좌측 5칸) ──
		var heldStrip = CreateImage(dicePanel.gameObject, "HeldDiceStrip",
			new Color(0f, 0f, 0f, 0f));
		heldStrip.anchorMin = new Vector2(0.045f, 0.085f);
		heldStrip.anchorMax = new Vector2(0.20f, 0.905f);
		heldStrip.offsetMin = Vector2.zero;
		heldStrip.offsetMax = Vector2.zero;
		heldStrip.GetComponent<Image>().raycastTarget = false;

		var heldLabel = CreateTMPText(heldStrip.gameObject, "HeldDiceLabel", "",
			18, new Color(0.65f, 0.68f, 0.85f), TextAlignmentOptions.MidlineLeft);
		var hlRt = heldLabel.GetComponent<RectTransform>();
		hlRt.anchorMin = new Vector2(0f, 0f);
		hlRt.anchorMax = new Vector2(0f, 1f);
		hlRt.pivot = new Vector2(0f, 0.5f);
		hlRt.anchoredPosition = new Vector2(14f, 0f);
		hlRt.sizeDelta = new Vector2(54f, 0f);

		var heldArea = CreateEmpty(heldStrip.gameObject, "HeldDiceArea");
		heldArea.anchorMin = new Vector2(0f, 0f);
		heldArea.anchorMax = new Vector2(1f, 1f);
		heldArea.offsetMin = Vector2.zero;
		heldArea.offsetMax = Vector2.zero;

		// ── 저장 슬롯: 각 칸 높이에 맞춘 정사각형 슬롯 5개 ──
		var heldSlotsRt = CreateEmpty(heldArea.gameObject, "HeldDiceSlots");
		Stretch(heldSlotsRt);
		var heldVlg = heldSlotsRt.gameObject.AddComponent<VerticalLayoutGroup>();
		heldVlg.childAlignment     = TextAnchor.MiddleCenter;
		heldVlg.spacing            = 8f;
		heldVlg.padding            = new RectOffset(0, 0, 0, 0);
		heldVlg.childControlWidth  = true;
		heldVlg.childControlHeight = true;
		heldVlg.childForceExpandWidth  = true;
		heldVlg.childForceExpandHeight = true;

		var heldSlotImages = new Image[5];
		var heldSlotButtons = new Button[5];
		for (int s = 0; s < 5; s++)
		{
			var slotCellRt = CreateEmpty(heldSlotsRt.gameObject, $"HeldSlotCell{s}");
			var cellLayout = slotCellRt.gameObject.AddComponent<LayoutElement>();
			cellLayout.minHeight = 48f;

			var slotRt = CreateImage(slotCellRt.gameObject, $"HeldSlot{s}",
				new Color(1f, 1f, 1f, 0f));
			slotRt.anchorMin = new Vector2(0.5f, 0f);
			slotRt.anchorMax = new Vector2(0.5f, 1f);
			slotRt.pivot = new Vector2(0.5f, 0.5f);
			slotRt.anchoredPosition = Vector2.zero;
			slotRt.sizeDelta = Vector2.zero;
			var slotAspect = slotRt.gameObject.AddComponent<AspectRatioFitter>();
			slotAspect.aspectMode = AspectRatioFitter.AspectMode.HeightControlsWidth;
			slotAspect.aspectRatio = 1f;
			var slotBg = slotRt.GetComponent<Image>();
			slotBg.raycastTarget = true;

			var slotBtn = slotRt.gameObject.AddComponent<Button>();
			slotBtn.targetGraphic = slotBg;
			var cb = slotBtn.colors;
			cb.normalColor      = new Color(1f, 1f, 1f, 0f);
			cb.highlightedColor = new Color(1f, 0.84f, 0.32f, 0.18f);
			cb.pressedColor     = new Color(0.85f, 0.55f, 0.16f, 0.25f);
			cb.selectedColor    = cb.highlightedColor;
			slotBtn.colors = cb;

			var iconRt = CreateImage(slotRt.gameObject, "DiceIcon", Color.white);
			iconRt.anchorMin = Vector2.zero;
			iconRt.anchorMax = Vector2.one;
			iconRt.pivot = new Vector2(0.5f, 0.5f);
			iconRt.offsetMin = new Vector2(5f, 5f);
			iconRt.offsetMax = new Vector2(-5f, -5f);
			var slotImg = iconRt.GetComponent<Image>();
			slotImg.preserveAspect = true;
			slotImg.raycastTarget  = false;
			slotImg.enabled        = false;

			var hover = slotRt.gameObject.AddComponent<UIHoverEffect>();
			SetField(hover, "targetImage", slotImg);
			SetField(hover, "fontSizeBoost", 0f);
			SetField(hover, "scaleFactor", 1.04f);
			SetField(hover, "transitionDuration", 0.1f);
			SetField(hover, "outlineColor", new Color(1f, 0.85f, 0.3f, 0.9f));
			SetField(hover, "outlineDistance", new Vector2(2f, 2f));
			SetField(hover, "shadowColor", new Color(0f, 0f, 0f, 0.35f));
			SetField(hover, "shadowDistance", new Vector2(3f, -3f));
			heldSlotImages[s] = slotImg;
			heldSlotButtons[s] = slotBtn;
		}

		// ── 주사위 굴림 뷰포트 (트레이 우측 큰 칸) ──
		var vpArea = CreateEmpty(dicePanel.gameObject, "DiceViewportArea");
		// Dice_Tray.png 1440x720 기준 큰 주사위칸의 내부 바닥 영역.
		vpArea.anchorMin = new Vector2(360f / 1440f, 128f / 720f);
		vpArea.anchorMax = new Vector2(1328f / 1440f, 624f / 720f);
		vpArea.offsetMin = Vector2.zero;
		vpArea.offsetMax = Vector2.zero;

		var vpContainer = CreateEmpty(vpArea.gameObject, "DiceViewportContainer");
		Stretch(vpContainer);

		var vpGo = new GameObject("DiceViewport");
		vpGo.transform.SetParent(vpContainer, false);
		var rawImg = vpGo.AddComponent<RawImage>();
		rawImg.texture = renderTex;
		rawImg.raycastTarget = true;
		var vpRt = vpGo.GetComponent<RectTransform>();
		Stretch(vpRt);

		// ── 남은 굴림 도트 (뷰포트 좌상단, ●/○ 텍스트) ──
		var rollDotsText = CreateTMPText(vpArea.gameObject, "RollDots", "● ● ●",
			26, Color.white, TextAlignmentOptions.TopLeft);
		var rdRt = rollDotsText.GetComponent<RectTransform>();
		rdRt.anchorMin = new Vector2(0f, 1f);
		rdRt.anchorMax = new Vector2(0.5f, 1f);
		rdRt.pivot = new Vector2(0f, 1f);
		rdRt.anchoredPosition = new Vector2(10f, -6f);
		rdRt.sizeDelta = new Vector2(0f, 36f);

		// ── 족보 완성 자막 (뷰포트 상단 중앙, 평상시 비활성) ──
		var comboLabel = CreateTMPText(vpArea.gameObject, "ComboLabel", "",
			34, Color.white, TextAlignmentOptions.Top);
		var clRt = comboLabel.GetComponent<RectTransform>();
		clRt.anchorMin = new Vector2(0f, 1f);
		clRt.anchorMax = new Vector2(1f, 1f);
		clRt.pivot     = new Vector2(0.5f, 1f);
		clRt.anchoredPosition = new Vector2(0f, -4f);
		clRt.sizeDelta = new Vector2(0f, 48f);
		comboLabel.fontStyle = FontStyles.Bold;
		comboLabel.gameObject.SetActive(false);

		// ── 좌측 조작 버튼 컨테이너 ──
		var leftArea = CreateEmpty(lowerArea.gameObject, "LeftArea");
		leftArea.anchorMin = new Vector2(0f, 0f);
		leftArea.anchorMax = new Vector2(0.26f, 1f);
		leftArea.offsetMin = Vector2.zero;
		leftArea.offsetMax = Vector2.zero;

		// ── 데미지 프리뷰 (석판 아래) ──
		var dmgPreview = CreateTMPText(leftArea.gameObject, "DamagePreview", "",
			22, AccentYellow, TextAlignmentOptions.Right);
		var dpRt = dmgPreview.GetComponent<RectTransform>();
		dpRt.anchorMin = new Vector2(0.03f, 0.70f);
		dpRt.anchorMax = new Vector2(0.97f, 0.96f);
		dpRt.offsetMin = Vector2.zero;
		dpRt.offsetMax = Vector2.zero;
		dmgPreview.fontStyle = FontStyles.Bold;

		// ── 버튼 (석판 아래) ──
		var rollBtn = CreateActionButton(leftArea.gameObject, "RollButton", "굴리기",
			new Vector2(0.04f, 0.48f), new Vector2(0.96f, 0.66f), SceneBuilderUtility.ButtonNormal);

		var confirmBtn = CreateActionButton(leftArea.gameObject, "ConfirmButton", "확정",
			new Vector2(0.04f, 0.27f), new Vector2(0.96f, 0.45f), SceneBuilderUtility.ButtonNormal);

		var cancelBtn = CreateActionButton(leftArea.gameObject, "CancelButton", "취소",
			new Vector2(0.04f, 0.06f), new Vector2(0.47f, 0.24f), CancelColor);
		SetButtonColorSet(cancelBtn.GetComponent<Button>(), CancelColor, CancelHighlight,
			new Color(0.40f, 0.12f, 0.12f));

		var nextRoundBtn = CreateActionButton(leftArea.gameObject, "NextRoundButton", "다음 턴",
			new Vector2(0.50f, 0.06f), new Vector2(0.96f, 0.24f), SceneBuilderUtility.ButtonNormal);

		var bottomFocusHandles = SceneBuilderUtility.BuildBattleBottomFocus(
			canvasGo.GetComponent<RectTransform>(), lowerArea, PanelBg);
		var battleLogComp = bottomFocusHandles.log;

		// "나와라!" 는 이제 별도 버튼 없이 굴리기 버튼이 모드 전환으로 대체.
		// BattleSceneController.SetRollButtonComeOutMode가 색상·텍스트를 바꿔 동일 버튼을 come-out 트리거로 재활용.

		// ── 적 주사위 물리 환경 (Z=100 오프셋) ──
		var enemyDiceCenter = new Vector3(0, 0, 100);
		var enemyDiceVaultCenter = enemyDiceCenter + new Vector3(0, dieRadius, 0);
		BuildEnemyDiceArena(diceLayer, bouncyMat, wallMat, enemyDiceCenter, defaultEnemyDiceProfile);

		// 적 주사위 RenderTexture
		var enemyRenderTex = EnsureRenderTexture(
			"Assets/Textures/EnemyDiceRenderTexture.renderTexture", 960, 540);

		// 적 DiceCamera
		var enemyDiceCamGo = new GameObject("EnemyDiceCamera");
		var enemyDiceCam = enemyDiceCamGo.AddComponent<Camera>();
		enemyDiceCam.clearFlags = CameraClearFlags.SolidColor;
		enemyDiceCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		enemyDiceCam.cullingMask = 1 << diceLayer;
		enemyDiceCam.targetTexture = enemyRenderTex;
		enemyDiceCam.orthographic = defaultEnemyDiceProfile.cameraOrthographic;
		enemyDiceCam.orthographicSize = defaultEnemyDiceProfile.cameraOrthographicSize;
		enemyDiceCam.fieldOfView = defaultEnemyDiceProfile.cameraFieldOfView;
		enemyDiceCamGo.transform.position = enemyDiceVaultCenter + defaultEnemyDiceProfile.cameraOffset;
		enemyDiceCamGo.transform.rotation = Quaternion.Euler(defaultEnemyDiceProfile.cameraEulerAngles);

		// 적 주사위 5개
		var enemyDiceArr = CreateDiceSet("EnemyDie", enemyDiceCenter, diceLayer,
			bouncyMat, outlineMat, dicePrefab, dieRadius, defaultEnemyDiceProfile.diceScale);

		// EnemyDiceRoller 컴포넌트
		var enemyRollerGo = new GameObject("EnemyDiceRoller");
		var enemyRoller = enemyRollerGo.AddComponent<EnemyDiceRoller>();
		SetField(enemyRoller, "enemyDice", enemyDiceArr);
		SetField(enemyRoller, "vaultCenter", enemyDiceVaultCenter);
		SetField(enemyRoller, "diceCamera", enemyDiceCam);
		SetField(enemyRoller, "profileCatalog", enemyDiceProfileCatalog);
		SetField(enemyRoller, "slimeJellyMaterial", slimeJellyMaterial);
		SetField(enemyRoller, "diceSpacing", defaultEnemyDiceProfile.diceSpacing);

		// ── 적 주사위 UI 오버레이 (몹 머리 위 배경 패널 + RenderTexture) ──
		// 레거시 팝업 placeholder (BattleSceneController의 enemyDicePopup 필드 호환용)
		var enemyDicePopupGo = CreateEmpty(canvasGo, "EnemyDicePopup");
		enemyDicePopupGo.anchorMin = Vector2.zero;
		enemyDicePopupGo.anchorMax = Vector2.zero;
		enemyDicePopupGo.sizeDelta = Vector2.zero;
		enemyDicePopupGo.gameObject.SetActive(false);

		// 실제 주사위 오버레이 — 런타임에 몹 머리 위로 배치
		var enemyDiceOverlayGo = CreateEmpty(canvasGo, "EnemyDiceOverlay");
		enemyDiceOverlayGo.anchorMin = new Vector2(0.5f, 0.5f);
		enemyDiceOverlayGo.anchorMax = new Vector2(0.5f, 0.5f);
		enemyDiceOverlayGo.pivot     = new Vector2(0.5f, 0.5f);
		float enemyDiceOverlayHeight = defaultEnemyDiceProfile.overlayMaxHeight;
		float enemyDiceOverlayWidth = enemyDiceOverlayHeight * defaultEnemyDiceProfile.overlayAspect;
		enemyDiceOverlayGo.sizeDelta = new Vector2(enemyDiceOverlayWidth, enemyDiceOverlayHeight);
		var overlayBg = enemyDiceOverlayGo.gameObject.AddComponent<Image>();
		overlayBg.color = new Color(0.10f, 0.18f, 0.32f, 0.92f);
		overlayBg.raycastTarget = false;
		var overlayOutline = enemyDiceOverlayGo.gameObject.AddComponent<Outline>();
		overlayOutline.effectColor = new Color(0.35f, 0.62f, 1f, 0.65f);
		overlayOutline.effectDistance = new Vector2(2f, -2f);

		var overlayRawGo = new GameObject("EnemyDiceRawImage");
		overlayRawGo.transform.SetParent(enemyDiceOverlayGo, false);
		var overlayRaw = overlayRawGo.AddComponent<RawImage>();
		overlayRaw.texture = enemyRenderTex;
		overlayRaw.raycastTarget = false;
		var overlayRawRt = overlayRawGo.GetComponent<RectTransform>();
		overlayRawRt.anchorMin = Vector2.zero;
		overlayRawRt.anchorMax = Vector2.one;
		overlayRawRt.offsetMin = Vector2.zero;
		overlayRawRt.offsetMax = Vector2.zero;

		enemyDiceOverlayGo.gameObject.SetActive(false);


		// ── EventSystem ──
		SceneBuilderUtility.BuildEventSystem();

		// ── DiceViewportInteraction ──
		var dviGo = new GameObject("DiceViewportInteraction");
		var dvi = dviGo.AddComponent<DiceViewportInteraction>();
		SetField(dvi, "viewport", rawImg);
		SetField(dvi, "diceCamera", diceCam);
		SetField(dvi, "diceLayerIndex", diceLayer);

		var battleRoot = SceneBuilderUtility.BuildBattleRootBase<BattleSceneController>(
			fightBackground, stageBundles, playerRig, enemySlots, GroundY,
			heartDisplayComp, battleLogComp, bottomFocusHandles.focus, dmgSpawn);
		var root = battleRoot.root;
		var ctrl = battleRoot.controller;
		var rollBtnComp = rollBtn.GetComponent<Button>();
		var confirmBtnComp = confirmBtn.GetComponent<Button>();
		var cancelBtnComp = cancelBtn.GetComponent<Button>();
		var nextRoundBtnComp = nextRoundBtn.GetComponent<Button>();

		SetField(ctrl, "rollButton", rollBtnComp);
		SetField(ctrl, "confirmButton", confirmBtnComp);
		SetField(ctrl, "cancelButton", cancelBtnComp);
		SetField(ctrl, "nextRoundButton", nextRoundBtnComp);

		// ── DiceRollDirector: 주사위 굴림 라이프사이클 전담 컨트롤러 ──
		// "나와라 송" 오디오는 AudioManager의 drumRollSource를 공유하므로 여기서 별도 AudioSource를 만들지 않는다.
		var diceDirector = root.AddComponent<DiceRollDirector>();
		SetField(diceDirector, "dice", diceArr);
		SetField(diceDirector, "viewportInteraction", dvi);
		SetField(diceDirector, "rollButton", rollBtnComp);
		SetField(diceDirector, "vaultCenter", VaultCenter + new Vector3(0, 0.25f, 0));
		// 주사위 슬롯 레이아웃 — 5개는 위 3개/아래 2개 그리드, 그 외는 한 줄 대칭 배치.
		// slotCenter.y는 base(non-elevated) 값 — Dice.BeginPhysicalRoll이 launch 높이를 추가한다.
		// slotCenter.z는 DiceCamera(top-down, Euler 90°) 화면 수직 좌표. 카메라 중심 z와 동일하게 맞춰 화면 중앙 정렬.
		SetField(diceDirector, "slotCenter", new Vector3(0f, dieRadius, DiceSlotCenterZ));
		SetField(diceDirector, "slotSpacing", 1.05f);
		SetField(diceDirector, "slotRowSpacing", 1.05f);
		SetField(diceDirector, "heldDiceImages", heldSlotImages);
		SetField(diceDirector, "diceFaceSprites", diceFaceSprites);
		SetField(ctrl, "diceDirector", diceDirector);

		// 저장 슬롯 unhold 버튼 연결
		{
			var btn0 = heldSlotButtons[0];
			var btn1 = heldSlotButtons[1];
			var btn2 = heldSlotButtons[2];
			var btn3 = heldSlotButtons[3];
			var btn4 = heldSlotButtons[4];
			if (btn0 != null) UnityEventTools.AddPersistentListener(btn0.onClick, diceDirector.UnholdSlot0);
			if (btn1 != null) UnityEventTools.AddPersistentListener(btn1.onClick, diceDirector.UnholdSlot1);
			if (btn2 != null) UnityEventTools.AddPersistentListener(btn2.onClick, diceDirector.UnholdSlot2);
			if (btn3 != null) UnityEventTools.AddPersistentListener(btn3.onClick, diceDirector.UnholdSlot3);
			if (btn4 != null) UnityEventTools.AddPersistentListener(btn4.onClick, diceDirector.UnholdSlot4);
		}

		// 버튼 연결 (PersistentListener로 씬에 영속 저장)
		UnityEventTools.AddPersistentListener(rollBtnComp.onClick, ctrl.RollDice);
		UnityEventTools.AddPersistentListener(confirmBtnComp.onClick, ctrl.ConfirmScore);
		UnityEventTools.AddPersistentListener(cancelBtnComp.onClick, ctrl.CancelBattle);
		UnityEventTools.AddPersistentListener(nextRoundBtnComp.onClick, ctrl.NextRound);
		SceneBuilderUtility.BindBattleEnemyPanelButtons(ctrl, enemySlots.buttons);
		// rollDotsText / damagePreviewText / comboLabel는 BattleHudPresenter로 이동(아래 블록).
		// enemyDice*, jumpAnimator는 EnemyCounterAttackDirector로 이동(아래 블록).

		// ── 플레이어 사망 애니메이션 ──
		var deathAnimComp = root.AddComponent<PlayerDeathAnimator>();
		SetField(deathAnimComp, "playerBody", playerImg);
		SetField(deathAnimComp, "bodyAnimator", bodyAnim);
		SetField(deathAnimComp, "frameRate", SceneBuilderUtility.BattlePlayerActionFrameRate);

		// 사망 스프라이트 로드 (0~144)
		Sprite[] deathSprites = SceneBuilderUtility.LoadNumberedPixelSprites(
			SceneBuilderUtility.PlayerDieSpriteFolder, "", "png", SceneBuilderUtility.BattlePlayerDeathFrameCount);
		SetField(deathAnimComp, "deathSprites", deathSprites);
		SetField(bodyAnim, "deathDisplaySprites", deathSprites);

		// 화면 어두워짐용 Dimmer (최상위 — 모든 UI 위에)
		var deathDimmer = SceneBuilderUtility.CreateDimmer(canvasGo.transform, "DeathDimmer");
		deathDimmer.transform.SetAsLastSibling();
		SetField(deathAnimComp, "screenDimmer", deathDimmer.GetComponent<Image>());

		SetField(ctrl, "deathAnimator", deathAnimComp);

		// ── 플레이어 공격 애니메이션 ──
		var weaponProjectileImg = SceneBuilderUtility.CreatePlayerWeaponProjectileImage(canvasGo.transform);
		var attackAnimComp = SceneBuilderUtility.AddPlayerAttackAnimator(
			root, playerImg, bodyAnim, weaponProjectileImg);
		SetField(ctrl, "attackAnimator", attackAnimComp);

		var enemyProjectileImg = SceneBuilderUtility.CreateEnemyProjectileImage(
			canvasGo.transform,
			"EnemyProjectile",
			"Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png");

		// ── 플레이어 점프(회피) 애니메이션 + 발밑 효과 ──
		var jumpAnimComp = root.AddComponent<PlayerJumpAnimator>();
		SetField(jumpAnimComp, "playerBody", playerImg);
		SetField(jumpAnimComp, "belowEffect", jumpBelowImg);
		SetField(jumpAnimComp, "bodyAnimator", bodyAnim);
		SetField(jumpAnimComp, "belowSprites", jumpBelowSprites);
		SetField(jumpAnimComp, "jumpDuration", SceneBuilderUtility.BattlePlayerJumpDuration);

		// jumpAnimator는 BSC가 직접 호출하지 않고 EnemyCounterAttackDirector가 소유 — 아래 블록에서 SetField.

		// ── BattleHudPresenter: 굴림 점/데미지 프리뷰/콤보 라벨 HUD ──
		var hudComp = root.AddComponent<BattleHudPresenter>();
		SetField(hudComp, "rollDotsText", rollDotsText);
		SetField(hudComp, "damagePreviewText", dmgPreview);
		SetField(hudComp, "comboLabel", comboLabel);
		SetField(ctrl, "hud", hudComp);

		// ── EnemyCounterAttackDirector: 적 반격 시퀀스 + 방어 페이즈 상태 ──
		var counterAttackDir = root.AddComponent<EnemyCounterAttackDirector>();
		var attackProjectileVfx = root.AddComponent<EnemyAttackProjectileVfx>();
		SetField(attackProjectileVfx, "vfxParent", canvasGo.transform);
		SetField(counterAttackDir, "enemyDiceRoller", enemyRoller);
		SetField(counterAttackDir, "enemyDicePopup", enemyDicePopupGo.gameObject);
		SetField(counterAttackDir, "enemyDiceOverlay", enemyDiceOverlayGo);
		SetField(counterAttackDir, "enemyDiceResultTexts", enemyDiceResultTexts);
		SetField(counterAttackDir, "jumpAnimator", jumpAnimComp);
		SetField(counterAttackDir, "enemyProjectile", enemyProjectileImg);
		SetField(counterAttackDir, "attackProjectileVfx", attackProjectileVfx);
		SetField(ctrl, "counterAttackDirector", counterAttackDir);

		// ── 디버그 콘솔 ──
		SceneBuilderUtility.BuildDebugConsole();

		// ── 오디오 매니저 ─────────────────────────────────────────
		SceneBuilderUtility.BuildAudioManager(new[]
		{
			"Player_Attack", "Player_Attack_Small", "Player_Attack_Medium", "Player_Attack_Big",
			"Enemy123_Attack", "Enemy45_Attack", "Enemy_Die", "Player_Death",
			"Player_PerfectDefense", "Player_PartialDefense",
			"Alert_LowHP", "Gauge_Empty", "Gauge_Fill",
			"UI_Back_NO", "Transition_2",
			"DIce_WakuWaku_Level3"
		}, includeDrumRoll: true);

		// ── 씬 저장 ──
		return SceneBuilderUtility.SaveSceneAndShowDialog(scene,
			"Assets/Scenes/DiceBattleScene.unity",
			"DiceBattleScene 생성 완료!",
			showDialog: showCompletionDialog);
	}

	// ── 물리 환경 ──

	static void BuildPhysicsEnvironment(int layer, PhysicsMaterial floorMat, PhysicsMaterial wallMat)
	{
		// DiceCamera가 보는 큰 주사위칸 내부 바닥 영역과 물리 벽 위치를 일치시킨다.
		BuildPhysicsBox6("", new Vector3(0, 0, DiceSlotCenterZ),
			DiceArenaWidth, DiceArenaDepth, 30f, layer, floorMat, wallMat);
	}

	static readonly Vector3 VaultCenter = new Vector3(0, 0, 50);

	static void BuildVault(int layer, PhysicsMaterial floorMat, PhysicsMaterial wallMat)
	{
		BuildPhysicsBox6("Vault", VaultCenter, 8f, 4f, 30f, layer, floorMat, wallMat);
	}

	static void BuildEnemyDiceArena(int layer, PhysicsMaterial floorMat, PhysicsMaterial wallMat,
		Vector3 center, EnemyDiceProfile profile)
	{
		Vector3 arenaSize = profile != null ? profile.arenaSize : EnemyDiceProfile.DefaultArenaSize;
		BuildPhysicsBox6("EnemyDice", center,
			arenaSize.x, arenaSize.z, arenaSize.y, layer, floorMat, wallMat);
	}

	static void NormalizeEnemyDiceProfiles(EnemyDiceProfile defaultProfile, EnemyDiceProfileCatalog catalog)
	{
		defaultProfile?.NormalizeSafetySizing();
		if (catalog?.Profiles != null)
		{
			for (int i = 0; i < catalog.Profiles.Length; i++)
				catalog.Profiles[i]?.NormalizeSafetySizing();
		}

		Vector3 arenaSize = ResolveEnemyDiceArenaSize(defaultProfile, catalog);
		ApplyEnemyDiceArenaSize(defaultProfile, arenaSize);
		if (catalog?.Profiles == null)
			return;

		for (int i = 0; i < catalog.Profiles.Length; i++)
			ApplyEnemyDiceArenaSize(catalog.Profiles[i], arenaSize);
	}

	static Vector3 ResolveEnemyDiceArenaSize(EnemyDiceProfile defaultProfile, EnemyDiceProfileCatalog catalog)
	{
		Vector3 arenaSize = defaultProfile != null
			? defaultProfile.arenaSize
			: EnemyDiceProfile.DefaultArenaSize;

		if (catalog?.Profiles == null)
			return arenaSize;

		for (int i = 0; i < catalog.Profiles.Length; i++)
		{
			var profile = catalog.Profiles[i];
			if (profile == null)
				continue;

			arenaSize = new Vector3(
				Mathf.Max(arenaSize.x, profile.arenaSize.x),
				Mathf.Max(arenaSize.y, profile.arenaSize.y),
				Mathf.Max(arenaSize.z, profile.arenaSize.z));
		}

		return arenaSize;
	}

	static void ApplyEnemyDiceArenaSize(EnemyDiceProfile profile, Vector3 arenaSize)
	{
		if (profile == null)
			return;

		profile.arenaSize = arenaSize;
		profile.cameraOrthographicSize = Mathf.Max(
			profile.cameraOrthographicSize,
			EnemyDiceProfile.ComputeCameraOrthographicSize(arenaSize));
	}

	/// <summary>바닥+천장+좌우벽+전후벽 6면 물리 박스를 일괄 생성.</summary>
	static void BuildPhysicsBox6(string prefix, Vector3 center,
		float width, float depth, float height,
		int layer, PhysicsMaterial floorMat, PhysicsMaterial wallMat)
	{
		float wallThick = 1.0f;
		float halfThick = wallThick * 0.5f;
		float totalW = width + wallThick * 2f;
		float totalD = depth + wallThick * 2f;

		CreatePhysicsBox($"{prefix}Floor",
			center + new Vector3(0, -halfThick, 0),
			new Vector3(totalW, wallThick, totalD),
			layer, floorMat, false);

		CreatePhysicsBox($"{prefix}Ceiling",
			center + new Vector3(0, height + halfThick, 0),
			new Vector3(totalW, wallThick, totalD),
			layer, wallMat, false);

		float wallX = width * 0.5f + halfThick;
		CreatePhysicsBox($"{prefix}WallL",
			center + new Vector3(-wallX, height * 0.5f, 0),
			new Vector3(wallThick, height + wallThick * 2f, totalD),
			layer, wallMat, false);
		CreatePhysicsBox($"{prefix}WallR",
			center + new Vector3(wallX, height * 0.5f, 0),
			new Vector3(wallThick, height + wallThick * 2f, totalD),
			layer, wallMat, false);

		float wallZF = -(depth * 0.5f + halfThick);
		float wallZB =  (depth * 0.5f + halfThick);
		CreatePhysicsBox($"{prefix}WallF",
			center + new Vector3(0, height * 0.5f, wallZF),
			new Vector3(totalW, height + wallThick * 2f, wallThick),
			layer, wallMat, false);
		CreatePhysicsBox($"{prefix}WallB",
			center + new Vector3(0, height * 0.5f, wallZB),
			new Vector3(totalW, height + wallThick * 2f, wallThick),
			layer, wallMat, false);
	}

	static GameObject CreatePhysicsBox(string name, Vector3 pos, Vector3 scale,
		int layer, PhysicsMaterial mat, bool visible)
	{
		var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
		go.name = name;
		go.transform.position = pos;
		go.transform.localScale = scale;
		go.layer = layer;

		var col = go.GetComponent<BoxCollider>();
		if (col != null) col.material = mat;

		if (!visible)
		{
			var renderer = go.GetComponent<MeshRenderer>();
			if (renderer != null) Object.DestroyImmediate(renderer);
			var filter = go.GetComponent<MeshFilter>();
			if (filter != null) Object.DestroyImmediate(filter);
		}

		return go;
	}

	// ── 레이어 재귀 설정 ──

	static void SetLayerRecursive(GameObject go, int layer)
	{
		go.layer = layer;
		foreach (Transform child in go.transform)
			SetLayerRecursive(child.gameObject, layer);
	}

	// ── UI 헬퍼 (유틸리티 위임 + 로컬 전용) ──

	static RectTransform CreateImage(GameObject parent, string name, Color color)
		=> SceneBuilderUtility.CreateImage(parent.transform, name, color);

	static RectTransform CreateEmpty(GameObject parent, string name)
		=> SceneBuilderUtility.CreateEmpty(parent.transform, name);

	static TMP_Text CreateTMPText(GameObject parent, string name, string text,
		float fontSize, Color color, TextAlignmentOptions alignment)
		=> SceneBuilderUtility.CreateTMPText(parent.transform, name, text, fontSize, color, alignment);

	static void Stretch(RectTransform rt) => SceneBuilderUtility.Stretch(rt);

	static GameObject CreateActionButton(GameObject parent, string name, string label,
		Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
	{
		var go = SceneBuilderUtility.CreateButton(parent.transform, name, label,
			28, bgColor, SceneBuilderUtility.ButtonHighlight, SceneBuilderUtility.ButtonPressed);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		return go;
	}

	static void SetButtonColorSet(Button btn, Color normal, Color highlight, Color pressed)
	{
		var cb = btn.colors;
		cb.normalColor = normal;
		cb.highlightedColor = highlight;
		cb.pressedColor = pressed;
		cb.selectedColor = highlight;
		btn.colors = cb;
	}

	static int EnsureLayer(string layerName)
	{
		var tagManager = new SerializedObject(
			AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
		var layers = tagManager.FindProperty("layers");

		for (int i = 8; i < 32; i++)
		{
			var prop = layers.GetArrayElementAtIndex(i);
			if (prop.stringValue == layerName)
				return i;
		}
		for (int i = 8; i < 32; i++)
		{
			var prop = layers.GetArrayElementAtIndex(i);
			if (string.IsNullOrEmpty(prop.stringValue))
			{
				prop.stringValue = layerName;
				tagManager.ApplyModifiedProperties();
				return i;
			}
		}
		Debug.LogWarning($"[DiceBattleSceneBuilder] 레이어 '{layerName}' 등록 실패");
		return 0;
	}

	static void SetField(object target, string fieldName, object value)
		=> SceneBuilderUtility.SetField(target, fieldName, value);

	static void EnsureDirectory(string path) => SceneBuilderUtility.EnsureDirectory(path);

	static void EnsurePixelSprite(string path) => SceneBuilderUtility.EnsurePixelSprite(path);

	static void EnsureTightSprite(string path) => SceneBuilderUtility.EnsureTightSprite(path);

	/// <summary>에셋을 삭제 후 새로 생성. 빌드할 때마다 최신 값이 보장된다.</summary>
	static T RecreateAsset<T>(string path, T asset) where T : Object
	{
		var existing = AssetDatabase.LoadAssetAtPath<T>(path);
		if (existing != null)
			AssetDatabase.DeleteAsset(path);
		AssetDatabase.CreateAsset(asset, path);
		return asset;
	}

	/// <summary>RenderTexture 에셋을 삭제 후 새로 생성.</summary>
	static RenderTexture EnsureRenderTexture(string path, int width, int height,
		FilterMode filter = FilterMode.Bilinear)
	{
		var rt = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32);
		rt.name = System.IO.Path.GetFileNameWithoutExtension(path);
		rt.filterMode = filter;
		return RecreateAsset(path, rt);
	}

	static Material EnsureSlimeJellyMaterial()
	{
		const string materialPath = "Assets/Materials/SlimeDiceJelly.mat";
		const string shaderName = "Capstone/Slime Dice Jelly";
		var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
		var shader = Shader.Find(shaderName);
		if (shader == null)
		{
			Debug.LogWarning($"[DiceBattleSceneBuilder] 셰이더 로드 실패: {shaderName}");
			return material;
		}

		if (material == null)
		{
			material = new Material(shader)
			{
				name = "SlimeDiceJelly"
			};
			material.SetColor("_BaseColor", new Color(0.48f, 1.00f, 0.18f, 0.46f));
			AssetDatabase.CreateAsset(material, materialPath);
		}
		else if (material.shader != shader)
		{
			material.shader = shader;
			EditorUtility.SetDirty(material);
		}
		return material;
	}

	static EnemyDiceProfileCatalog CreateEnemyDiceProfileCatalog(GameObject dicePrefab)
	{
		var catalog = EnemyDiceProfileCatalog.CreateDefault(dicePrefab);
		AssignEnemyDiceAtlas(catalog, EnemyDiceProfile.SlimeId, "slime");
		AssignEnemyDiceAtlas(catalog, EnemyDiceProfile.SkeletonId, "skeleton");
		AssignEnemyDiceAtlas(catalog, EnemyDiceProfile.BatId, "bat");
		AssignEnemyDiceAtlas(catalog, EnemyDiceProfile.GoblinId, "goblin");
		AssignEnemyDiceAtlas(catalog, EnemyDiceProfile.DraculaId, "dracula");
		return catalog;
	}

	static void AssignEnemyDiceAtlas(EnemyDiceProfileCatalog catalog, string profileId, string enemyKey)
	{
		var profile = catalog != null ? catalog.Resolve(profileId) : null;
		if (profile == null)
			return;

		string targetPath = $"Assets/Dices/EnemyStyles/{ToTitleCase(enemyKey)}/{enemyKey}_d6_atlas.png";
		EnsureEnemyDiceAtlasAsset(enemyKey, targetPath);
		profile.faceAtlasTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(targetPath);
		if (profile.faceAtlasTexture == null)
			Debug.LogWarning($"[DiceBattleSceneBuilder] 적 주사위 atlas 로드 실패: {targetPath}");
	}

	static void EnsureEnemyDiceAtlasAsset(string enemyKey, string targetPath)
	{
		string sourcePath = $"SpritePipelineWork/enemy_dice_{enemyKey}/sprite_sheets/{enemyKey}_dice_faces_sheet.png";
		if (!System.IO.File.Exists(sourcePath))
		{
			Debug.LogWarning($"[DiceBattleSceneBuilder] 적 주사위 face sheet 없음: {sourcePath}");
			return;
		}

		string targetDirectory = System.IO.Path.GetDirectoryName(targetPath)?.Replace('\\', '/');
		if (!string.IsNullOrEmpty(targetDirectory))
			EnsureDirectory(targetDirectory);

		bool shouldRegenerate = !System.IO.File.Exists(targetPath)
			|| System.IO.File.GetLastWriteTimeUtc(sourcePath) > System.IO.File.GetLastWriteTimeUtc(targetPath);
		if (shouldRegenerate)
			WriteCleanEnemyDiceAtlas(sourcePath, targetPath);

		ConfigureEnemyDiceAtlasImporter(targetPath);
	}

	static void WriteCleanEnemyDiceAtlas(string sourcePath, string targetPath)
	{
		var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
		Texture2D atlas = null;
		try
		{
			if (!source.LoadImage(System.IO.File.ReadAllBytes(sourcePath)))
			{
				Debug.LogWarning($"[DiceBattleSceneBuilder] 적 주사위 face sheet 디코드 실패: {sourcePath}");
				return;
			}

			atlas = CreateCleanEnemyDiceFaceAtlas(source);
			System.IO.File.WriteAllBytes(targetPath, atlas.EncodeToPNG());
			AssetDatabase.ImportAsset(targetPath, ImportAssetOptions.ForceUpdate);
		}
		finally
		{
			if (atlas != null)
				Object.DestroyImmediate(atlas);
			Object.DestroyImmediate(source);
		}
	}

	static void ConfigureEnemyDiceAtlasImporter(string path)
	{
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null)
			return;

		bool dirty = false;
		if (importer.textureType != TextureImporterType.Default)
		{
			importer.textureType = TextureImporterType.Default;
			dirty = true;
		}
		if (!importer.sRGBTexture)
		{
			importer.sRGBTexture = true;
			dirty = true;
		}
		if (importer.mipmapEnabled)
		{
			importer.mipmapEnabled = false;
			dirty = true;
		}
		if (importer.isReadable)
		{
			importer.isReadable = false;
			dirty = true;
		}
		if (importer.filterMode != FilterMode.Bilinear)
		{
			importer.filterMode = FilterMode.Bilinear;
			dirty = true;
		}
		if (importer.textureCompression != TextureImporterCompression.Uncompressed)
		{
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			dirty = true;
		}

		if (dirty)
			importer.SaveAndReimport();
	}

	public static Texture2D CreateCleanEnemyDiceFaceAtlas(Texture2D sourceSheet)
	{
		if (sourceSheet == null)
			return null;
		if (sourceSheet.width % 3 != 0 || sourceSheet.height % 2 != 0)
			throw new System.ArgumentException("Enemy dice face sheet must be a 3x2 grid.", nameof(sourceSheet));

		int tileWidth = sourceSheet.width / 3;
		int tileHeight = sourceSheet.height / 2;
		var atlas = new Texture2D(sourceSheet.width, sourceSheet.height, TextureFormat.RGBA32, false)
		{
			name = $"{sourceSheet.name}_clean_atlas",
			filterMode = FilterMode.Bilinear,
			wrapMode = TextureWrapMode.Clamp
		};
		var transparent = new Color32(0, 0, 0, 0);
		var clearPixels = new Color32[sourceSheet.width * sourceSheet.height];
		for (int i = 0; i < clearPixels.Length; i++)
			clearPixels[i] = transparent;
		atlas.SetPixels32(clearPixels);

		for (int row = 0; row < 2; row++)
		for (int col = 0; col < 3; col++)
		{
			int sourceX = col * tileWidth;
			int sourceY = sourceSheet.height - (row + 1) * tileHeight;
			CopyCleanEnemyDiceTile(sourceSheet, atlas, sourceX, sourceY, tileWidth, tileHeight);
		}

		atlas.Apply(false, false);
		return atlas;
	}

	static void CopyCleanEnemyDiceTile(Texture2D source, Texture2D atlas,
		int tileX, int tileY, int tileWidth, int tileHeight)
	{
		if (!TryFindDiceFaceBounds(source, tileX, tileY, tileWidth, tileHeight, out var bounds))
			bounds = new RectInt(tileX, tileY, tileWidth, tileHeight);

		for (int y = 0; y < tileHeight; y++)
		for (int x = 0; x < tileWidth; x++)
		{
			float u = tileWidth <= 1 ? 0f : x / (float)(tileWidth - 1);
			float v = tileHeight <= 1 ? 0f : y / (float)(tileHeight - 1);
			int sx = Mathf.Clamp(bounds.xMin + Mathf.RoundToInt(u * Mathf.Max(0, bounds.width - 1)), bounds.xMin, bounds.xMax - 1);
			int sy = Mathf.Clamp(bounds.yMin + Mathf.RoundToInt(v * Mathf.Max(0, bounds.height - 1)), bounds.yMin, bounds.yMax - 1);
			var pixel = source.GetPixel(sx, sy);
			if (IsEnemyDiceSheetBackground(pixel))
				pixel = Color.clear;
			atlas.SetPixel(tileX + x, tileY + y, pixel);
		}
	}

	static bool TryFindDiceFaceBounds(Texture2D source, int tileX, int tileY, int tileWidth, int tileHeight,
		out RectInt bounds)
	{
		int minX = tileX + tileWidth;
		int minY = tileY + tileHeight;
		int maxX = tileX - 1;
		int maxY = tileY - 1;

		for (int y = tileY; y < tileY + tileHeight; y++)
		for (int x = tileX; x < tileX + tileWidth; x++)
		{
			if (IsEnemyDiceSheetBackground(source.GetPixel(x, y)))
				continue;

			minX = Mathf.Min(minX, x);
			minY = Mathf.Min(minY, y);
			maxX = Mathf.Max(maxX, x);
			maxY = Mathf.Max(maxY, y);
		}

		if (maxX < minX || maxY < minY)
		{
			bounds = default;
			return false;
		}

		bounds = new RectInt(minX, minY, maxX - minX + 1, maxY - minY + 1);
		return true;
	}

	static bool IsEnemyDiceSheetBackground(Color pixel)
	{
		if (pixel.a <= 0.04f)
			return true;

		bool nearBlack = pixel.r <= 0.06f && pixel.g <= 0.06f && pixel.b <= 0.06f;
		bool nearMagenta = pixel.r >= 0.70f && pixel.g <= 0.18f && pixel.b >= 0.70f;
		return nearBlack || nearMagenta;
	}

	static string ToTitleCase(string value)
	{
		if (string.IsNullOrEmpty(value))
			return value;
		return char.ToUpperInvariant(value[0]) + value.Substring(1).ToLowerInvariant();
	}

	/// <summary>
	/// 주사위 눈 1~6 스프라이트를 반환.
	/// 우선 D6_mine 원본에서 현재 주사위 면 이미지를 다시 잘라 저장 슬롯과 3D 주사위 외형을 맞춘다.
	/// 원본이 없을 때만 기존 face{1..6}.png 또는 절차 생성 점 패턴으로 폴백한다.
	/// </summary>
	static Sprite[] GenerateDiceFaceSprites()
	{
		EnsureDirectory("Assets/Textures/DiceFaces");
		DicePrefabBuilder.TryRegenerateFaceSpritesFromSource();

		Sprite[] sprites = new Sprite[6];
		for (int face = 0; face < 6; face++)
		{
			string path = $"Assets/Textures/DiceFaces/face{face + 1}.png";

			if (System.IO.File.Exists(path))
			{
				// 생성된 스프라이트 존재 — 임포터 설정만 보정 후 로드
				EnsureFaceSpriteImporter(path);
				sprites[face] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
				if (sprites[face] != null)
					continue;
				Debug.LogWarning($"[Builder] face{face + 1}.png 로드 실패 — 절차 생성으로 폴백");
			}

			sprites[face] = CreateProceduralFaceSprite(face, path);
		}
		return sprites;
	}

	static void EnsureFaceSpriteImporter(string path)
	{
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null) return;
		bool dirty = false;
		if (importer.textureType != TextureImporterType.Sprite ||
		    importer.spriteImportMode != SpriteImportMode.Single)
		{
			importer.textureType = TextureImporterType.Sprite;
			importer.spriteImportMode = SpriteImportMode.Single;
			dirty = true;
		}
		var settings = new TextureImporterSettings();
		importer.ReadTextureSettings(settings);
		if (settings.spriteMeshType != SpriteMeshType.FullRect)
		{
			settings.spriteMeshType = SpriteMeshType.FullRect;
			importer.SetTextureSettings(settings);
			dirty = true;
		}
		if (importer.filterMode != FilterMode.Point)
		{
			importer.filterMode = FilterMode.Point;
			dirty = true;
		}
		if (importer.textureCompression != TextureImporterCompression.Uncompressed)
		{
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			dirty = true;
		}
		if (dirty)
			importer.SaveAndReimport();
	}

	static Sprite CreateProceduralFaceSprite(int face, string path)
	{
		int size = 64;
		int dotR = 7;
		Color bg = new Color(0.92f, 0.90f, 0.85f);
		Color dot = new Color(0.12f, 0.12f, 0.12f);

		// 각 면의 점 위치 (64×64 기준, 좌하단 원점)
		Vector2Int[][] dotPositions =
		{
			new[] { V(32,32) },
			new[] { V(18,46), V(46,18) },
			new[] { V(18,46), V(32,32), V(46,18) },
			new[] { V(18,46), V(46,46), V(18,18), V(46,18) },
			new[] { V(18,46), V(46,46), V(32,32), V(18,18), V(46,18) },
			new[] { V(18,46), V(46,46), V(18,32), V(46,32), V(18,18), V(46,18) },
		};

		var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
		var pixels = new Color[size * size];
		for (int p = 0; p < pixels.Length; p++) pixels[p] = bg;

		foreach (var pos in dotPositions[face])
		{
			for (int dy = -dotR; dy <= dotR; dy++)
			for (int dx = -dotR; dx <= dotR; dx++)
			{
				if (dx * dx + dy * dy > dotR * dotR) continue;
				int px = pos.x + dx, py = pos.y + dy;
				if (px >= 0 && px < size && py >= 0 && py < size)
					pixels[py * size + px] = dot;
			}
		}

		tex.SetPixels(pixels);
		tex.Apply();
		System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
		Object.DestroyImmediate(tex);
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

		EnsureFaceSpriteImporter(path);
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	static Vector2Int V(int x, int y) => new Vector2Int(x, y);

	/// <summary>주사위 5개를 생성하고 Dice 배열을 반환.</summary>
	static Dice[] CreateDiceSet(string namePrefix, Vector3 basePos, int layer,
		PhysicsMaterial bouncyMat, Material outlineMat, GameObject prefab, float dieRadius,
		float diceScale)
	{
		var dice = new Dice[5];
		for (int i = 0; i < 5; i++)
		{
			GameObject dieGo;
			if (prefab != null)
			{
				dieGo = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
				dieGo.name = $"{namePrefix}{i}";
			}
			else
			{
				dieGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
				dieGo.name = $"{namePrefix}{i}";
				dieGo.transform.localScale = Vector3.one * 0.5f;
			}

			dieGo.transform.position = basePos + InitialDiceSlotPosition(i, dieRadius);
			dieGo.transform.localScale = Vector3.one * Mathf.Max(0.1f, diceScale);
			SetLayerRecursive(dieGo, layer);

			var rb = dieGo.GetComponent<Rigidbody>();
			if (rb == null) rb = dieGo.AddComponent<Rigidbody>();
			rb.mass = 0.5f;
			rb.linearDamping = 0.25f;
			rb.angularDamping = 0.25f;
			rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
			rb.maxAngularVelocity = 50f;

			foreach (var col in dieGo.GetComponentsInChildren<Collider>())
				col.material = bouncyMat;

			var dieComp = dieGo.GetComponent<Dice>();
			if (dieComp == null) dieComp = dieGo.AddComponent<Dice>();
			SetField(dieComp, "outlineBaseMaterial", outlineMat);
			dice[i] = dieComp;
		}
		return dice;
	}

	static Vector3 InitialDiceSlotPosition(int index, float dieRadius)
	{
		const float spacing = 1.05f;
		const float rowSpacing = 1.05f;

		if (index < 3)
			return new Vector3((index - 1) * spacing, dieRadius, DiceSlotCenterZ + rowSpacing * 0.5f);

		return new Vector3((index - 3.5f) * spacing, dieRadius, DiceSlotCenterZ - rowSpacing * 0.5f);
	}
}
