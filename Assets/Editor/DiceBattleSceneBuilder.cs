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
	static readonly Color HpBarBg = new Color(0.15f, 0.15f, 0.15f);
	static readonly Color EnemyHpFill = new Color(0.85f, 0.25f, 0.25f);
	static readonly Color TargetMarkerColor = new Color(1f, 0.85f, 0.2f, 0.9f);
	static readonly Color AccentYellow = new Color(1f, 0.85f, 0.3f);
	static readonly Color SaveZoneVisual = new Color(0.55f, 0.40f, 0.04f, 0.6f);
	const float PlayerSpriteScale = 1.4f;
	const float DiceRollSpriteScaleMultiplier = 1.5f;
	const float SmallHitSpriteScaleMultiplier = 1.5f;
	const float StrongHitSpriteScaleMultiplier = 1.41f;

	[MenuItem("Tools/Build DiceBattle Scene")]
	public static void BuildScene()
	{
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		// ── RenderTexture ──
		EnsureDirectory("Assets/Textures");
		var renderTex = EnsureRenderTexture(
			"Assets/Textures/DiceRenderTexture.renderTexture", 240, 135, FilterMode.Point);

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

		// ── Dice3D 레이어 ──
		int diceLayer = EnsureLayer("Dice3D");

		// ── MainCamera ──
		var mainCamGo = new GameObject("MainCamera");
		mainCamGo.tag = "MainCamera";
		var mainCam = mainCamGo.AddComponent<Camera>();
		mainCam.orthographic = true;
		mainCam.orthographicSize = 5;
		mainCam.clearFlags = CameraClearFlags.SolidColor;
		mainCam.backgroundColor = BgColor;
		mainCam.cullingMask = ~(1 << diceLayer);

		// ── DiceCamera (top-down orthographic) ──
		// 주사위 정지 위치: x ∈ [-2.8, 2.8], y ≈ 0.95, z = 1.2
		// ortho size 2, aspect 16:9 → visible x ±3.56, z ±2 (중심 z=1.2)
		var diceCamGo = new GameObject("DiceCamera");
		var diceCam = diceCamGo.AddComponent<Camera>();
		diceCam.clearFlags = CameraClearFlags.SolidColor;
		diceCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		diceCam.orthographic     = true;
		diceCam.orthographicSize = 2f;
		diceCam.cullingMask = 1 << diceLayer;
		diceCam.targetTexture = renderTex;
		diceCamGo.transform.position = new Vector3(0f, 10f, 1.2f);
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

		// ── 주사위 5개 ──
		var dicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dices/Prefabs/Dice_d6.prefab");
		float dieRadius = 0.35f;
		var diceArr = CreateDiceSet("Die", Vector3.zero, diceLayer,
			bouncyMat, outlineMat, dicePrefab, dieRadius);

		// ── 캔버스 ──
		var canvasGo = new GameObject("Canvas");
		var canvas = canvasGo.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		var scaler = canvasGo.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		canvasGo.AddComponent<GraphicRaycaster>();

		// 하단 조작창 위 2/3: Fight_Background 이미지 (하단부만 보이도록 마스크 클리핑)
		var fightBgMask = CreateEmpty(canvasGo, "FightBackgroundMask");
		fightBgMask.anchorMin = new Vector2(0f, 1f / 3f);
		fightBgMask.anchorMax = new Vector2(1f, 1f);
		fightBgMask.offsetMin = Vector2.zero;
		fightBgMask.offsetMax = Vector2.zero;
		fightBgMask.gameObject.AddComponent<RectMask2D>();

		// 기본 스테이지의 배경으로 초기 렌더 — 런타임이 ApplyStageBackground로 활성 스테이지에 맞게 교체
		var defaultStage = StageRegistry.DefaultStage;
		var defaultBundle = defaultStage != null ? SceneBuilderUtility.BuildStageBundle(defaultStage) : null;
		Sprite fightBgSprite = defaultBundle != null ? defaultBundle.background : null;
		float fightBgAspect = 16f / 9f;
		if (fightBgSprite != null && fightBgSprite.texture != null)
			fightBgAspect = (float)fightBgSprite.texture.width / fightBgSprite.texture.height;
		float fightBgHeight = 1920f / fightBgAspect;
		var fightBgImg = CreateImage(fightBgMask.gameObject, "FightBackground", Color.white);
		fightBgImg.anchorMin = new Vector2(0f, 0f);
		fightBgImg.anchorMax = new Vector2(1f, 0f);
		fightBgImg.pivot = new Vector2(0.5f, 0f);
		fightBgImg.offsetMin = new Vector2(0f, 0f);
		fightBgImg.offsetMax = new Vector2(0f, fightBgHeight);
		var fightBgImageComp = fightBgImg.GetComponent<Image>();
		if (fightBgSprite != null)
			fightBgImageComp.sprite = fightBgSprite;
		else if (defaultStage != null)
			fightBgImageComp.color = defaultStage.themeColor;
		else
			fightBgImageComp.color = BgColor;

		// ── 지면 기준선 (배경 흙길 높이 — 캔버스 절대 Y) ──
		const float GroundY = 0.44f;

		// ── 플레이어 캐릭터 (프레임 기반 자동 애니메이션) ──
		Sprite[] idleSprites = LoadPlayerSpriteFrames("Assets/Player/IdleSprites", 19);
		Sprite[] lowHpSprites = LoadPlayerSpriteFrames("Assets/Player/LowHpSprites", 174);
		Sprite[] jumpSprites = LoadPlayerSpriteFrames("Assets/Player/JumpSprites", 145);
		Sprite[] jumpBelowSprites = LoadPlayerSpriteFrames("Assets/Player/JumpBelowSprites", 145);
		Sprite[] defenseSprites = LoadPlayerSpriteFrames("Assets/Player/DefenseSprites", 145);
		Sprite[] smallHitSprites = LoadPlayerSpriteFrames("Assets/Player/SmallHitSprites", 56);
		Sprite[] strongHitSprites = LoadPlayerSpriteFrames("Assets/Player/StrongHitSprites", 28);
		Sprite[] debuffSprites = LoadPlayerSpriteFrames("Assets/Player/DebuffSprites", 156);

		Sprite idleSprite = (idleSprites != null && idleSprites.Length > 0) ? idleSprites[0] : null;

		// 점프 발밑 효과 (PlayerBody보다 먼저 생성 → 더 낮은 sibling index = 플레이어 뒤로 렌더)
		var jumpBelow = CreateImage(canvasGo, "PlayerJumpBelow", Color.white);
		jumpBelow.pivot = new Vector2(0.5f, 0f);
		jumpBelow.anchorMin = new Vector2(0.19f, GroundY);
		jumpBelow.anchorMax = new Vector2(0.19f, GroundY);
		jumpBelow.sizeDelta = new Vector2(150f, 150f);
		jumpBelow.localScale = new Vector3(2f, 2f, 1f);
		var jumpBelowImg = jumpBelow.GetComponent<Image>();
		jumpBelowImg.preserveAspect = true;
		jumpBelowImg.useSpriteMesh = false;
		jumpBelowImg.raycastTarget = false;
		jumpBelowImg.enabled = false;

		var playerBody = CreateImage(canvasGo, "PlayerBody", Color.white);
		playerBody.pivot = new Vector2(0.5f, 0f);               // 피벗 하단 → 발 기준 배치
		playerBody.anchorMin = new Vector2(0.19f, GroundY);      // 단일 앵커점 (지면)
		playerBody.anchorMax = new Vector2(0.19f, GroundY);
		playerBody.sizeDelta = new Vector2(150f, 150f);
		playerBody.localScale = new Vector3(PlayerSpriteScale, PlayerSpriteScale, 1f);
		var playerImg = playerBody.GetComponent<Image>();
		playerImg.preserveAspect = true;
		playerImg.useSpriteMesh = false;
		playerImg.raycastTarget = false;
		if (idleSprite != null)
			playerImg.sprite = idleSprite;

		// PlayerBodyAnimator: Idle 루프 + HP 20% 이하면 LowHP 루프로 자동 전환
		// SmallHit/StrongHit/Defense/Jump/Debuff는 주입만 (외부 재생 시점에 사용)
		var bodyAnim = playerBody.gameObject.AddComponent<PlayerBodyAnimator>();
		SetField(bodyAnim, "playerBody", playerImg);
		SetField(bodyAnim, "frameRate", SceneBuilderUtility.BattlePlayerActionFrameRate);
		SetField(bodyAnim, "idleFrameRate", SceneBuilderUtility.BattlePlayerIdleFrameRate);
		SetField(bodyAnim, "idleSprites", idleSprites);
		SetField(bodyAnim, "lowHpSprites", lowHpSprites);
		SetField(bodyAnim, "jumpSprites", jumpSprites);
		SetField(bodyAnim, "defenseSprites", defenseSprites);
		SetField(bodyAnim, "smallHitSprites", smallHitSprites);
		SetField(bodyAnim, "strongHitSprites", strongHitSprites);
		SetField(bodyAnim, "debuffSprites", debuffSprites);
		SetField(bodyAnim, "smallHitScaleMultiplier", SmallHitSpriteScaleMultiplier);
		SetField(bodyAnim, "smallHitHorizontalFlip", true);
		SetField(bodyAnim, "smallHitFrameStep", 2);
		SetField(bodyAnim, "strongHitScaleMultiplier", StrongHitSpriteScaleMultiplier);
		SetField(bodyAnim, "strongHitHorizontalFlip", true);
		SetField(bodyAnim, "strongHitFrameStep", 1);

		// ── 적 슬롯 (지면 위, 플레이어 오른쪽 — Explore 동일 구조) ──
		var enemySlotsArea = CreateEmpty(canvasGo, "EnemySlotsArea");
		enemySlotsArea.anchorMin = new Vector2(0.45f, GroundY);
		enemySlotsArea.anchorMax = new Vector2(0.95f, GroundY + 0.35f);
		enemySlotsArea.offsetMin = Vector2.zero;
		enemySlotsArea.offsetMax = Vector2.zero;

		// 스테이지별 스프라이트 번들을 편집 시점에 한 번 로드 — 누락 에셋은 themeColor 폴백
		var stageBundles = SceneBuilderUtility.BuildAllStageBundles();

		GameObject[] enemyPanels = new GameObject[4];
		Image[] enemyBodies = new Image[4];
		EnemySpriteAnimator[] enemyAnimators = new EnemySpriteAnimator[4];
		TMP_Text[] enemyNameTexts = new TMP_Text[4];
		Image[] enemyHpFillArr = new Image[4];
		TMP_Text[] enemyHpTextArr = new TMP_Text[4];
		Image[] targetMarkers = new Image[4];
		TMP_Text[] deadOverlays = new TMP_Text[4];
		Button[] enemyPanelButtons = new Button[4];
		TMP_Text[] enemyDiceResultTexts = new TMP_Text[4];

		for (int i = 0; i < 4; i++)
		{
			float x0 = i * 0.25f;
			float x1 = x0 + 0.24f;

			// 슬롯 (투명 — 스프라이트만 보임)
			var slot = CreateImage(enemySlotsArea.gameObject, $"EnemySlot{i}",
				new Color(0, 0, 0, 0));
			slot.anchorMin = new Vector2(x0, 0.0f);
			slot.anchorMax = new Vector2(x1, 1.0f);
			slot.offsetMin = Vector2.zero;
			slot.offsetMax = Vector2.zero;
			slot.GetComponent<Image>().raycastTarget = true;
			enemyPanels[i] = slot.gameObject;

			// 적 몸체 (하단을 바닥에 밀착 — 런타임에서 MobBodyAnchors로 재조정)
			var body = CreateImage(slot.gameObject, "Body", Color.gray);
			body.anchorMin = new Vector2(0.05f, 0.0f);
			body.anchorMax = new Vector2(0.95f, 0.90f);
			body.offsetMin = Vector2.zero;
			body.offsetMax = Vector2.zero;
			var bodyImg = body.GetComponent<Image>();
			bodyImg.preserveAspect = true;
			bodyImg.useSpriteMesh = true;
			enemyBodies[i] = bodyImg;
			var enemyAnimator = body.gameObject.AddComponent<EnemySpriteAnimator>();
			SetField(enemyAnimator, "targetImage", bodyImg);
			SetField(enemyAnimator, "idleFrameRate", SceneBuilderUtility.BattleEnemyIdleFrameRate);
			SetField(enemyAnimator, "actionFrameRate", SceneBuilderUtility.BattleEnemyActionFrameRate);
			enemyAnimators[i] = enemyAnimator;

			// 타겟 마커 (노란 테두리, Body와 동일 영역)
			var marker = CreateImage(slot.gameObject, "TargetMarker", new Color(0, 0, 0, 0));
			marker.anchorMin = new Vector2(0.05f, 0.0f);
			marker.anchorMax = new Vector2(0.95f, 0.90f);
			marker.offsetMin = Vector2.zero;
			marker.offsetMax = Vector2.zero;
			marker.GetComponent<Image>().raycastTarget = false;
			// 테두리 두께 기본값 — 런타임에 MobDef.borderThickness로 몹별 재적용됨.
			SceneBuilderUtility.MakeEnemyTargetBorders(marker, 0.05f, TargetMarkerColor);
			targetMarkers[i] = marker.GetComponent<Image>();
			marker.gameObject.SetActive(false);

			// 사망 오버레이 (Body와 동일 영역)
			var deadOverlay = CreateTMPText(slot.gameObject, "DeadOverlay", "✕",
				60, new Color(1f, 0.2f, 0.2f, 0.85f), TextAlignmentOptions.Center);
			var deadRt = deadOverlay.GetComponent<RectTransform>();
			deadRt.anchorMin = new Vector2(0.05f, 0.0f);
			deadRt.anchorMax = new Vector2(0.95f, 0.90f);
			deadRt.offsetMin = Vector2.zero;
			deadRt.offsetMax = Vector2.zero;
			deadOverlay.raycastTarget = false;
			deadOverlay.gameObject.SetActive(false);
			deadOverlays[i] = deadOverlay;

			// 정보 패널 (반투명 배경 — 이름 + HP 바 + HP 텍스트)
			var infoPanel = CreateImage(slot.gameObject, "InfoPanel",
				new Color(0f, 0f, 0f, 0.5f));
			infoPanel.anchorMin = new Vector2(0.0f, 0.90f);
			infoPanel.anchorMax = new Vector2(1.0f, 1.08f);
			infoPanel.offsetMin = Vector2.zero;
			infoPanel.offsetMax = Vector2.zero;
			infoPanel.GetComponent<Image>().raycastTarget = false;

			// 이름 (InfoPanel 상단 — 슬롯 기준 0.96~1.08 → 패널 내 0.333~1.0)
			var nameT = CreateTMPText(infoPanel.gameObject, "Name", "적",
				22, Color.white, TextAlignmentOptions.Center);
			var nrt = nameT.GetComponent<RectTransform>();
			nrt.anchorMin = new Vector2(0.0f, 0.333f);
			nrt.anchorMax = new Vector2(1.0f, 1.0f);
			nrt.offsetMin = Vector2.zero;
			nrt.offsetMax = Vector2.zero;
			nameT.fontStyle = FontStyles.Bold;
			enemyNameTexts[i] = nameT;

			// HP 바 (InfoPanel 하단 — 슬롯 기준 0.90~0.96 → 패널 내 0.0~0.333)
			var hpBg = CreateImage(infoPanel.gameObject, "HpBarBg", HpBarBg);
			hpBg.anchorMin = new Vector2(0.10f, 0.0f);
			hpBg.anchorMax = new Vector2(0.90f, 0.333f);
			hpBg.offsetMin = Vector2.zero;
			hpBg.offsetMax = Vector2.zero;

			var eFill = CreateImage(hpBg.gameObject, "HpFill", EnemyHpFill);
			Stretch(eFill);
			var eFillImg = eFill.GetComponent<Image>();
			eFillImg.sprite = SceneBuilderUtility.WhitePixelSprite();
			eFillImg.type = Image.Type.Filled;
			eFillImg.fillMethod = Image.FillMethod.Horizontal;
			enemyHpFillArr[i] = eFill.GetComponent<Image>();

			// HP 텍스트 (HP 바와 겹침)
			var eHpT = CreateTMPText(infoPanel.gameObject, "HpText", "0 / 0",
				18, Color.white, TextAlignmentOptions.Center);
			var eHpRt = eHpT.GetComponent<RectTransform>();
			eHpRt.anchorMin = new Vector2(0.05f, 0.0f);
			eHpRt.anchorMax = new Vector2(0.95f, 0.333f);
			eHpRt.offsetMin = Vector2.zero;
			eHpRt.offsetMax = Vector2.zero;
			enemyHpTextArr[i] = eHpT;

			// 슬롯 클릭으로 타겟 선택
			var panelBtn = slot.gameObject.AddComponent<Button>();
			panelBtn.targetGraphic = slot.GetComponent<Image>();
			var btnColors = panelBtn.colors;
			btnColors.normalColor = new Color(0, 0, 0, 0);
			btnColors.highlightedColor = new Color(1f, 1f, 1f, 0.08f);
			btnColors.pressedColor = new Color(1f, 1f, 1f, 0.15f);
			btnColors.selectedColor = btnColors.highlightedColor;
			panelBtn.colors = btnColors;
			enemyPanelButtons[i] = panelBtn;

			// 적 주사위 결과 텍스트 (이름 위)
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

		// 적 주사위 눈 표시 컨테이너 — 적 패널 왼쪽(=플레이어 방향)에 나란히 배치.
		// 적이 플레이어 앞까지 걸어와 멈추면 이 컨테이너가 적 바로 앞에 놓인다.
		GameObject[] enemyDiceFaceContainers = new GameObject[4];
		for (int i = 0; i < 4; i++)
		{
			var container = CreateEmpty(enemyPanels[i], $"EnemyDiceFaces{i}");
			container.anchorMin = new Vector2(-0.35f, 0.00f);
			container.anchorMax = new Vector2(0.00f, 0.25f);
			container.offsetMin = Vector2.zero;
			container.offsetMax = Vector2.zero;
			var hlg = container.gameObject.AddComponent<HorizontalLayoutGroup>();
			hlg.spacing = 4;
			hlg.childAlignment = TextAnchor.MiddleRight;
			hlg.childControlWidth = false;
			hlg.childControlHeight = false;
			hlg.childForceExpandWidth = false;
			hlg.childForceExpandHeight = false;

			// 주사위 5개분 Image 슬롯 생성 (정사각형 고정 크기)
			float diceSize = 42f;
			for (int d = 0; d < 5; d++)
			{
				var faceImg = CreateImage(container.gameObject, $"Face{d}", Color.white);
				faceImg.GetComponent<Image>().preserveAspect = true;
				faceImg.GetComponent<Image>().raycastTarget = false;
				faceImg.sizeDelta = new Vector2(diceSize, diceSize);
				faceImg.gameObject.SetActive(false);
			}

			container.gameObject.SetActive(false);
			enemyDiceFaceContainers[i] = container.gameObject;
		}

		// 데미지 스폰 영역 (적 슬롯 위쪽에 겹침)
		var dmgSpawn = CreateEmpty(canvasGo, "DamageSpawnArea");
		dmgSpawn.anchorMin = new Vector2(0.40f, GroundY + 0.20f);
		dmgSpawn.anchorMax = new Vector2(0.98f, GroundY + 0.35f);
		dmgSpawn.offsetMin = Vector2.zero;
		dmgSpawn.offsetMax = Vector2.zero;

		// ── 플레이어 하트 (좌상단) ──
		var heartTextObj = CreateTMPText(canvasGo, "PlayerHeartText", "● ● ● ● ●",
			48, new Color(1f, 0.13f, 0.13f), TextAlignmentOptions.Left);
		heartTextObj.richText = true;
		var heartRt = heartTextObj.GetComponent<RectTransform>();
		heartRt.anchorMin = new Vector2(0.02f, 0.90f);
		heartRt.anchorMax = new Vector2(0.40f, 0.99f);
		heartRt.offsetMin = Vector2.zero;
		heartRt.offsetMax = Vector2.zero;

		var heartDisplayComp = heartTextObj.gameObject.AddComponent<HeartDisplay>();
		SetField(heartDisplayComp, "heartText", heartTextObj);

		// ── 하단 1/3: UI 배경 이미지 ──
		EnsureTightSprite("Assets/Mobs/UI_Background.png");
		var uiBgImg = CreateImage(canvasGo, "UIBackground", Color.white);
		uiBgImg.anchorMin = new Vector2(0f, 0f);
		uiBgImg.anchorMax = new Vector2(1f, 1f / 3f);
		uiBgImg.offsetMin = Vector2.zero;
		uiBgImg.offsetMax = Vector2.zero;
		var uiBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Mobs/UI_Background.png");
		if (uiBgSprite != null)
			uiBgImg.GetComponent<Image>().sprite = uiBgSprite;
		else
			uiBgImg.GetComponent<Image>().color = BgColor;

		// ── 하단 1/3 영역: 현재 필요한 조작 패널을 크게 표시 ──
		var lowerArea = CreateEmpty(canvasGo, "LowerArea");
		lowerArea.anchorMin = new Vector2(0.03f, 0.02f);
		lowerArea.anchorMax = new Vector2(0.97f, 0.315f);
		lowerArea.offsetMin = Vector2.zero;
		lowerArea.offsetMax = Vector2.zero;

		var dicePanel = CreateImage(lowerArea.gameObject, "DicePanel",
			new Color(0f, 0f, 0f, 0f));
		dicePanel.anchorMin = new Vector2(0.28f, 0f);
		dicePanel.anchorMax = new Vector2(1f, 1f);
		dicePanel.offsetMin = Vector2.zero;
		dicePanel.offsetMax = Vector2.zero;
		dicePanel.GetComponent<Image>().raycastTarget = false;

		// ── 저장된 주사위 (좌측 세로 스트립) ──
		float heldStripWidth = 0.14f;

		var heldStrip = CreateImage(dicePanel.gameObject, "HeldDiceStrip",
			new Color(0.08f, 0.08f, 0.16f, 0.85f));
		heldStrip.anchorMin = new Vector2(0f, 0f);
		heldStrip.anchorMax = new Vector2(heldStripWidth, 1f);
		heldStrip.offsetMin = Vector2.zero;
		heldStrip.offsetMax = Vector2.zero;
		heldStrip.GetComponent<Image>().raycastTarget = false;

		var heldLabel = CreateTMPText(heldStrip.gameObject, "HeldDiceLabel", "저장",
			18, new Color(0.65f, 0.68f, 0.85f), TextAlignmentOptions.Center);
		var hlRt = heldLabel.GetComponent<RectTransform>();
		hlRt.anchorMin = new Vector2(0f, 0.92f);
		hlRt.anchorMax = new Vector2(1f, 1f);
		hlRt.offsetMin = Vector2.zero;
		hlRt.offsetMax = Vector2.zero;

		var heldArea = CreateEmpty(heldStrip.gameObject, "HeldDiceArea");
		heldArea.anchorMin = new Vector2(0f, 0f);
		heldArea.anchorMax = new Vector2(1f, 0.92f);
		heldArea.offsetMin = Vector2.zero;
		heldArea.offsetMax = Vector2.zero;

		// ── 저장 슬롯: 스프라이트 이미지 5개 세로 배치 ──
		var heldSlotsRt = CreateEmpty(heldArea.gameObject, "HeldDiceSlots");
		Stretch(heldSlotsRt);
		var heldVlg = heldSlotsRt.gameObject.AddComponent<VerticalLayoutGroup>();
		heldVlg.childAlignment     = TextAnchor.UpperCenter;
		heldVlg.spacing            = 10f;
		heldVlg.padding            = new RectOffset(18, 18, 8, 6);
		heldVlg.childControlWidth  = true;
		heldVlg.childControlHeight = false;
		heldVlg.childForceExpandWidth  = true;
		heldVlg.childForceExpandHeight = false;

		var heldSlotImages = new Image[5];
		for (int s = 0; s < 5; s++)
		{
			var slotRt  = CreateImage(heldSlotsRt.gameObject, $"HeldSlot{s}", Color.white);
			var slotImg = slotRt.GetComponent<Image>();
			slotImg.preserveAspect  = true;
			slotImg.raycastTarget   = true;
			slotImg.enabled         = false;
			var slotArf = slotRt.gameObject.AddComponent<AspectRatioFitter>();
			slotArf.aspectMode  = AspectRatioFitter.AspectMode.WidthControlsHeight;
			slotArf.aspectRatio = 1f;
			var slotBtn = slotRt.gameObject.AddComponent<Button>();
			slotBtn.targetGraphic = slotImg;
			var cb = slotBtn.colors;
			cb.normalColor      = Color.white;
			cb.highlightedColor = Color.white;
			cb.pressedColor     = new Color(0.7f, 0.7f, 0.7f, 1f);
			slotBtn.colors = cb;
			var hover = slotRt.gameObject.AddComponent<UIHoverEffect>();
			SetField(hover, "targetImage", slotImg);
			SetField(hover, "fontSizeBoost", 0f);
			SetField(hover, "scaleFactor", 1.08f);
			SetField(hover, "transitionDuration", 0.1f);
			SetField(hover, "outlineColor", new Color(1f, 0.85f, 0.3f, 0.9f));
			SetField(hover, "outlineDistance", new Vector2(2f, 2f));
			SetField(hover, "shadowColor", new Color(0f, 0f, 0f, 0.35f));
			SetField(hover, "shadowDistance", new Vector2(3f, -3f));
			heldSlotImages[s] = slotImg;
		}

		// ── 주사위 굴림 뷰포트 (우측, 전체 높이) ──
		var vpArea = CreateEmpty(dicePanel.gameObject, "DiceViewportArea");
		vpArea.anchorMin = new Vector2(heldStripWidth + 0.01f, 0f);
		vpArea.anchorMax = new Vector2(1f, 1f);
		vpArea.offsetMin = Vector2.zero;
		vpArea.offsetMax = Vector2.zero;

		var vpContainer = CreateEmpty(vpArea.gameObject, "DiceViewportContainer");
		Stretch(vpContainer);
		var vpAspect = vpContainer.gameObject.AddComponent<AspectRatioFitter>();
		vpAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		vpAspect.aspectRatio = 960f / 540f;

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
		BuildEnemyDiceArena(diceLayer, bouncyMat, wallMat, enemyDiceCenter);

		// 적 주사위 RenderTexture
		var enemyRenderTex = EnsureRenderTexture(
			"Assets/Textures/EnemyDiceRenderTexture.renderTexture", 960, 540);

		// 적 DiceCamera
		var enemyDiceCamGo = new GameObject("EnemyDiceCamera");
		var enemyDiceCam = enemyDiceCamGo.AddComponent<Camera>();
		enemyDiceCam.clearFlags = CameraClearFlags.SolidColor;
		enemyDiceCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		enemyDiceCam.fieldOfView = 42;
		enemyDiceCam.cullingMask = 1 << diceLayer;
		enemyDiceCam.targetTexture = enemyRenderTex;
		enemyDiceCamGo.transform.position = enemyDiceCenter + new Vector3(0, 6, -3);
		enemyDiceCamGo.transform.rotation = Quaternion.Euler(55, 0, 0);

		// 적 주사위 5개
		var enemyDiceArr = CreateDiceSet("EnemyDie", enemyDiceCenter, diceLayer,
			bouncyMat, outlineMat, dicePrefab, dieRadius);

		// EnemyDiceRoller 컴포넌트
		var enemyRollerGo = new GameObject("EnemyDiceRoller");
		var enemyRoller = enemyRollerGo.AddComponent<EnemyDiceRoller>();
		SetField(enemyRoller, "enemyDice", enemyDiceArr);
		SetField(enemyRoller, "vaultCenter", enemyDiceCenter + new Vector3(0, 0.25f, 0));

		// ── 적 주사위 UI 오버레이 (작은 RawImage — RenderTexture를 몹 앞으로 이동) ──
		// 레거시 팝업 placeholder (BattleSceneController의 enemyDicePopup 필드 호환용)
		var enemyDicePopupGo = CreateEmpty(canvasGo, "EnemyDicePopup");
		enemyDicePopupGo.anchorMin = Vector2.zero;
		enemyDicePopupGo.anchorMax = Vector2.zero;
		enemyDicePopupGo.sizeDelta = Vector2.zero;
		enemyDicePopupGo.gameObject.SetActive(false);

		// 실제 비행 오버레이 — 런타임에 RectTransform.position을 직접 조작
		var enemyDiceOverlayGo = CreateEmpty(canvasGo, "EnemyDiceOverlay");
		enemyDiceOverlayGo.anchorMin = new Vector2(0.5f, 0.5f);
		enemyDiceOverlayGo.anchorMax = new Vector2(0.5f, 0.5f);
		enemyDiceOverlayGo.pivot     = new Vector2(0.5f, 0.5f);
		enemyDiceOverlayGo.sizeDelta = new Vector2(640f, 360f); // 16:9 (RenderTex 비율, 2배 확대)

		var overlayRawGo = new GameObject("EnemyDiceRawImage");
		overlayRawGo.transform.SetParent(enemyDiceOverlayGo, false);
		var overlayRaw = overlayRawGo.AddComponent<RawImage>();
		overlayRaw.texture = enemyRenderTex;
		overlayRaw.raycastTarget = false;
		var overlayRawRt = overlayRawGo.GetComponent<RectTransform>();
		Stretch(overlayRawRt);

		enemyDiceOverlayGo.gameObject.SetActive(false);


		// ── EventSystem ──
		var es = new GameObject("EventSystem");
		es.AddComponent<UnityEngine.EventSystems.EventSystem>();
		es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

		// ── DiceViewportInteraction ──
		var dviGo = new GameObject("DiceViewportInteraction");
		var dvi = dviGo.AddComponent<DiceViewportInteraction>();
		SetField(dvi, "viewport", rawImg);
		SetField(dvi, "diceCamera", diceCam);
		SetField(dvi, "diceLayerIndex", diceLayer);

		// ── BattleDamageVFX ──
		var root = new GameObject("BattleRoot");
		var vfxComp = root.AddComponent<BattleDamageVFX>();
		SetField(vfxComp, "damageSpawnParent", dmgSpawn);
		SetField(vfxComp, "enemyPanels", enemyPanels);

		// ── BattleAnimations (피격 점멸, 돌진 등 재사용 애니메이션) ──
		var battleAnimsComp = root.AddComponent<BattleAnimations>();

		// ── BattleSceneController ──
		var ctrl = root.AddComponent<BattleSceneController>();

		SetField(ctrl, "enemyPanels", enemyPanels);
		SetField(ctrl, "enemyBodies", enemyBodies);
		SetField(ctrl, "enemyAnimators", enemyAnimators);
		SetField(ctrl, "enemyNames", enemyNameTexts);
		SetField(ctrl, "enemyHpFills", enemyHpFillArr);
		SetField(ctrl, "enemyHpTexts", enemyHpTextArr);
		SetField(ctrl, "targetMarkers", targetMarkers);
		SetField(ctrl, "deadOverlays", deadOverlays);

		// 스테이지 번들 주입 — 디버그 씬 직접 로딩 시 GenerateDefaultEnemies / 런타임 배경 교체에 사용
		SetField(ctrl, "stageBundles", stageBundles);
		SetField(ctrl, "fightBackgroundImage", fightBgImageComp);

		SetField(ctrl, "vfx", vfxComp);
		SetField(ctrl, "battleLog", battleLogComp);
		SetField(ctrl, "bottomFocus", bottomFocusHandles.focus);
		SetField(ctrl, "battleAnims", battleAnimsComp);
		SetField(ctrl, "playerBody", playerImg);
		SetField(ctrl, "playerBodyAnimator", bodyAnim);
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
		// 주사위 row 슬롯 레이아웃 — 비홀드 주사위는 slotCenter 기준 slotSpacing 간격으로 대칭 배치.
		// slotCenter.y는 base(non-elevated) 값 — Dice.BeginSpin이 spin 높이 offset을 추가한다.
		// slotCenter.z는 DiceCamera(top-down, Euler 90°) 화면 수직 좌표. 카메라 중심 z=1.2와 동일하게 맞춰 화면 중앙 정렬.
		SetField(diceDirector, "slotCenter", new Vector3(0f, dieRadius, 1.2f));
		SetField(diceDirector, "slotSpacing", 1.4f);
		SetField(diceDirector, "heldDiceImages", heldSlotImages);
		SetField(diceDirector, "diceFaceSprites", diceFaceSprites);
		SetField(ctrl, "diceDirector", diceDirector);

		// 저장 슬롯 unhold 버튼 연결
		{
			var btn0 = heldSlotImages[0].GetComponent<Button>();
			var btn1 = heldSlotImages[1].GetComponent<Button>();
			var btn2 = heldSlotImages[2].GetComponent<Button>();
			var btn3 = heldSlotImages[3].GetComponent<Button>();
			var btn4 = heldSlotImages[4].GetComponent<Button>();
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
		UnityEventTools.AddPersistentListener(enemyPanelButtons[0].onClick, ctrl.OnEnemyPanel0Clicked);
		UnityEventTools.AddPersistentListener(enemyPanelButtons[1].onClick, ctrl.OnEnemyPanel1Clicked);
		UnityEventTools.AddPersistentListener(enemyPanelButtons[2].onClick, ctrl.OnEnemyPanel2Clicked);
		UnityEventTools.AddPersistentListener(enemyPanelButtons[3].onClick, ctrl.OnEnemyPanel3Clicked);
		SetField(ctrl, "heartDisplay", heartDisplayComp);
		// rollDotsText / damagePreviewText / comboLabel는 BattleHudPresenter로 이동(아래 블록).
		// enemyDice*, jumpAnimator는 EnemyCounterAttackDirector로 이동(아래 블록).

		// ── 플레이어 사망 애니메이션 ──
		var deathAnimComp = root.AddComponent<PlayerDeathAnimator>();
		SetField(deathAnimComp, "playerBody", playerImg);
		SetField(deathAnimComp, "bodyAnimator", bodyAnim);
		SetField(deathAnimComp, "frameRate", SceneBuilderUtility.BattlePlayerActionFrameRate);

		// 사망 스프라이트 로드 (0~144)
		const int deathFrameCount = 145;
		Sprite[] deathSprites = new Sprite[deathFrameCount];
		for (int f = 0; f < deathFrameCount; f++)
		{
			string framePath = $"Assets/Player/DieSprites/{f}.png";
			EnsurePixelSprite(framePath);
			deathSprites[f] = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
		}
		SetField(deathAnimComp, "deathSprites", deathSprites);

		// 화면 어두워짐용 Dimmer (최상위 — 모든 UI 위에)
		var deathDimmer = SceneBuilderUtility.CreateDimmer(canvasGo.transform, "DeathDimmer");
		deathDimmer.transform.SetAsLastSibling();
		SetField(deathAnimComp, "screenDimmer", deathDimmer.GetComponent<Image>());

		SetField(ctrl, "deathAnimator", deathAnimComp);

		// ── 플레이어 주사위 굴림 애니메이션 ──
		var rollAnimComp = root.AddComponent<PlayerRollAnimator>();
		SetField(rollAnimComp, "playerBody", playerImg);
		SetField(rollAnimComp, "bodyAnimator", bodyAnim);
		SetField(rollAnimComp, "bodyScaleMultiplier", DiceRollSpriteScaleMultiplier);
		SetField(rollAnimComp, "frameRate", SceneBuilderUtility.BattlePlayerActionFrameRate);

		const int rollFrameCount = 145;
		Sprite[] rollSprites = new Sprite[rollFrameCount];
		for (int f = 0; f < rollFrameCount; f++)
		{
			string framePath = $"Assets/Player/DiceRollSprites/{f}.png";
			EnsurePixelSprite(framePath);
			rollSprites[f] = AssetDatabase.LoadAssetAtPath<Sprite>(framePath);
		}
		SetField(rollAnimComp, "rollSprites", rollSprites);

		SetField(ctrl, "rollAnimator", rollAnimComp);

		// ── 플레이어 공격 애니메이션 ──
		EnsureTightSprite("Assets/Player/Player_Weapon_transparent.png");
		var weaponProjectile = CreateImage(canvasGo, "PlayerWeaponProjectile", Color.white);
		weaponProjectile.anchorMin = new Vector2(0.5f, 0.5f);
		weaponProjectile.anchorMax = new Vector2(0.5f, 0.5f);
		weaponProjectile.pivot = new Vector2(0.5f, 0.5f);
		weaponProjectile.sizeDelta = new Vector2(84f, 84f);
		var weaponProjectileImg = weaponProjectile.GetComponent<Image>();
		weaponProjectileImg.sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Player/Player_Weapon_transparent.png");
		weaponProjectileImg.preserveAspect = true;
		weaponProjectileImg.raycastTarget = false;
		weaponProjectile.gameObject.SetActive(false);

		var attackAnimComp = root.AddComponent<PlayerAttackAnimator>();
		SetField(attackAnimComp, "playerBody", playerImg);
		SetField(attackAnimComp, "weaponProjectile", weaponProjectileImg);
		SetField(attackAnimComp, "bodyAnimator", bodyAnim);
		SetField(attackAnimComp, "frameRate", 22.5f);
		SetField(attackAnimComp, "frameStep", 2);

		Sprite[] attackSprites = LoadAvailablePlayerSpriteFrames(
			"Assets/Player/AttackSprites/Attack_01_transparent");
		SetField(attackAnimComp, "attackSprites", attackSprites);

		SetField(ctrl, "attackAnimator", attackAnimComp);

		// ── 플레이어 점프(회피) 애니메이션 + 발밑 효과 ──
		var jumpAnimComp = root.AddComponent<PlayerJumpAnimator>();
		SetField(jumpAnimComp, "playerBody", playerImg);
		SetField(jumpAnimComp, "belowEffect", jumpBelowImg);
		SetField(jumpAnimComp, "bodyAnimator", bodyAnim);
		SetField(jumpAnimComp, "jumpSprites", jumpSprites);
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
		SetField(counterAttackDir, "enemyDiceRoller", enemyRoller);
		SetField(counterAttackDir, "enemyDicePopup", enemyDicePopupGo.gameObject);
		SetField(counterAttackDir, "enemyDiceOverlay", enemyDiceOverlayGo);
		SetField(counterAttackDir, "enemyDiceResultTexts", enemyDiceResultTexts);
		SetField(counterAttackDir, "enemyDiceFaceContainers", enemyDiceFaceContainers);
		SetField(counterAttackDir, "diceFaceSprites", diceFaceSprites);
		SetField(counterAttackDir, "jumpAnimator", jumpAnimComp);
		SetField(ctrl, "counterAttackDirector", counterAttackDir);

		// ── 디버그 콘솔 ──
		var debugGo = new GameObject("DebugConsole");
		debugGo.AddComponent<DebugConsoleController>();

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
		string scenePath = "Assets/Scenes/DiceBattleScene.unity";
		EnsureDirectory("Assets/Scenes");
		EditorSceneManager.SaveScene(scene, scenePath);
		AddSceneToBuildSettings(scenePath);
		EditorUtility.DisplayDialog("씬 빌더", "DiceBattleScene 생성 완료!", "확인");
	}

	// ── 물리 환경 ──

	static void BuildPhysicsEnvironment(int layer, PhysicsMaterial floorMat, PhysicsMaterial wallMat)
	{
		// DiceCamera(0,6,-3), 55° 하방, FOV 42° → 바닥(Y=0) 가시 범위: X ≈ ±5, Z ≈ -1.5 ~ 5.9
		// 물리 공간을 가시 영역에 맞춰 배치 (중심 Z=2)
		BuildPhysicsBox6("", new Vector3(0, 0, 2f), 8f, 6f, 8f, layer, floorMat, wallMat);
	}

	static readonly Vector3 VaultCenter = new Vector3(0, 0, 50);

	static void BuildVault(int layer, PhysicsMaterial floorMat, PhysicsMaterial wallMat)
	{
		BuildPhysicsBox6("Vault", VaultCenter, 8f, 4f, 6f, layer, floorMat, wallMat);
	}

	static void BuildEnemyDiceArena(int layer, PhysicsMaterial floorMat, PhysicsMaterial wallMat, Vector3 center)
	{
		BuildPhysicsBox6("EnemyDice", new Vector3(center.x, center.y, center.z + 2f),
			8f, 6f, 8f, layer, floorMat, wallMat);
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

	static void AddSceneToBuildSettings(string scenePath)
		=> SceneBuilderUtility.AddSceneToBuildSettings(scenePath);

	static void EnsurePixelSprite(string path) => SceneBuilderUtility.EnsurePixelSprite(path);

	static void EnsureTightSprite(string path) => SceneBuilderUtility.EnsureTightSprite(path);

	/// <summary>플레이어 프레임 스프라이트 폴더에서 0..count-1 순서로 로드. 픽셀아트 임포트 보장.</summary>
	static Sprite[] LoadPlayerSpriteFrames(string folder, int count)
	{
		var arr = new Sprite[count];
		for (int i = 0; i < count; i++)
		{
			string p = $"{folder}/{i}.png";
			EnsurePixelSprite(p);
			arr[i] = AssetDatabase.LoadAssetAtPath<Sprite>(p);
			if (arr[i] == null)
				Debug.LogWarning($"[DiceBattleSceneBuilder] 프레임 로드 실패: {p}");
		}
		return arr;
	}

	/// <summary>폴더에 존재하는 숫자 파일명 PNG를 모두 숫자 순서로 로드. 프레임 수가 바뀌는 애니메이션용.</summary>
	static Sprite[] LoadAvailablePlayerSpriteFrames(string folder)
	{
		if (!System.IO.Directory.Exists(folder))
		{
			Debug.LogWarning($"[DiceBattleSceneBuilder] 프레임 폴더 없음: {folder}");
			return new Sprite[0];
		}

		var paths = System.IO.Directory.GetFiles(folder, "*.png");
		System.Array.Sort(paths, (a, b) => ExtractFrameIndex(a).CompareTo(ExtractFrameIndex(b)));

		var sprites = new System.Collections.Generic.List<Sprite>(paths.Length);
		foreach (var path in paths)
		{
			string assetPath = path.Replace("\\", "/");
			EnsurePixelSprite(assetPath);
			var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
			if (sprite != null)
				sprites.Add(sprite);
			else
				Debug.LogWarning($"[DiceBattleSceneBuilder] 프레임 로드 실패: {assetPath}");
		}
		return sprites.ToArray();
	}

	static int ExtractFrameIndex(string path)
	{
		string name = System.IO.Path.GetFileNameWithoutExtension(path);
		return int.TryParse(name, out int index) ? index : int.MaxValue;
	}

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

	/// <summary>
	/// 주사위 눈 1~6 스프라이트를 반환.
	/// 이름 규칙: Assets/Textures/DiceFaces/face{1..6}.png — 파일이 이미 있으면 그대로 로드하고,
	/// 없는 면만 점 패턴으로 절차 생성해 폴백. 수동 제작 스프라이트를 덮어쓰지 않는다.
	/// </summary>
	static Sprite[] GenerateDiceFaceSprites()
	{
		EnsureDirectory("Assets/Textures/DiceFaces");

		Sprite[] sprites = new Sprite[6];
		for (int face = 0; face < 6; face++)
		{
			string path = $"Assets/Textures/DiceFaces/face{face + 1}.png";

			if (System.IO.File.Exists(path))
			{
				// 수동 스프라이트 존재 — 임포터 설정만 보정 후 로드
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
		if (importer.textureType != TextureImporterType.Sprite)
		{
			importer.textureType = TextureImporterType.Sprite;
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
		PhysicsMaterial bouncyMat, Material outlineMat, GameObject prefab, float dieRadius)
	{
		var dice = new Dice[5];
		for (int i = 0; i < 5; i++)
		{
			float x = -2.8f + i * 1.4f;
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

			dieGo.transform.position = basePos + new Vector3(x, dieRadius, 1.2f);
			dieGo.transform.localScale = Vector3.one * 0.7f;
			SetLayerRecursive(dieGo, layer);

			var rb = dieGo.GetComponent<Rigidbody>();
			if (rb == null) rb = dieGo.AddComponent<Rigidbody>();
			rb.mass = 0.5f;
			rb.linearDamping = 0.25f;
			rb.angularDamping = 0.25f;
			rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

			foreach (var col in dieGo.GetComponentsInChildren<Collider>())
				col.material = bouncyMat;

			var dieComp = dieGo.GetComponent<Dice>();
			if (dieComp == null) dieComp = dieGo.AddComponent<Dice>();
			SetField(dieComp, "outlineBaseMaterial", outlineMat);
			dice[i] = dieComp;
		}
		return dice;
	}
}
