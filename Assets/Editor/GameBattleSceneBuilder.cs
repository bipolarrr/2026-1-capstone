using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEditor.Events;

public static class GameBattleSceneBuilder
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

	[MenuItem("Tools/Build GameBattle Scene")]
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

		// YachtDie.outlineBaseMaterial에 주입: 빌드 시 Shader.Find 실패를 방지하기 위해 에셋으로 사전 생성
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

		// ── DiceCamera ──
		var diceCamGo = new GameObject("DiceCamera");
		var diceCam = diceCamGo.AddComponent<Camera>();
		diceCam.clearFlags = CameraClearFlags.SolidColor;
		diceCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		diceCam.fieldOfView = 42;
		diceCam.cullingMask = 1 << diceLayer;
		diceCam.targetTexture = renderTex;
		diceCamGo.transform.position = new Vector3(0, 6, -3);
		diceCamGo.transform.rotation = Quaternion.Euler(55, 0, 0);

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

		// ── 주사위 격리 공간 (Vault) ──
		BuildVault(diceLayer, bouncyMat, wallMat);

		// Vault RenderTexture (굴림 뷰와 동일한 가로 해상도, 낮은 세로)
		var vaultRenderTex = EnsureRenderTexture(
			"Assets/Textures/VaultRenderTexture.renderTexture", 45, 135, FilterMode.Point);

		// Vault Camera (직하향 정사영)
		var vaultCamGo = new GameObject("VaultCamera");
		var vaultCam = vaultCamGo.AddComponent<Camera>();
		vaultCam.orthographic = true;
		vaultCam.orthographicSize = 2.4f;
		vaultCam.nearClipPlane = 0.3f;
		vaultCam.farClipPlane = 10f;
		vaultCam.clearFlags = CameraClearFlags.SolidColor;
		vaultCam.backgroundColor = new Color(0f, 0f, 0f, 0f);
		vaultCam.cullingMask = 1 << diceLayer;
		vaultCam.targetTexture = vaultRenderTex;
		vaultCamGo.transform.position = VaultCenter + new Vector3(0, 5, 0);
		vaultCamGo.transform.rotation = Quaternion.Euler(90, 0, 0);

		// ── 주사위 5개 ──
		var dicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dices/Prefabs/Dice_d6.prefab");
		float dieRadius = 0.25f;
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

		// 상단 절반: Fight_Background 이미지 (하단부만 보이도록 마스크 클리핑)
		var fightBgMask = CreateEmpty(canvasGo, "FightBackgroundMask");
		fightBgMask.anchorMin = new Vector2(0f, 0.5f);
		fightBgMask.anchorMax = new Vector2(1f, 1f);
		fightBgMask.offsetMin = Vector2.zero;
		fightBgMask.offsetMax = Vector2.zero;
		fightBgMask.gameObject.AddComponent<RectMask2D>();

		var fightBgTex = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mobs/Fight_Background.png");
		float fightBgAspect = fightBgTex != null ? (float)fightBgTex.width / fightBgTex.height : 16f / 9f;
		float fightBgHeight = 1920f / fightBgAspect;
		var fightBgImg = CreateImage(fightBgMask.gameObject, "FightBackground", Color.white);
		fightBgImg.anchorMin = new Vector2(0f, 0f);
		fightBgImg.anchorMax = new Vector2(1f, 0f);
		fightBgImg.pivot = new Vector2(0.5f, 0f);
		fightBgImg.offsetMin = new Vector2(0f, 0f);
		fightBgImg.offsetMax = new Vector2(0f, fightBgHeight);
		var fightBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Mobs/Fight_Background.png");
		if (fightBgSprite != null)
			fightBgImg.GetComponent<Image>().sprite = fightBgSprite;
		else
			fightBgImg.GetComponent<Image>().color = BgColor;

		// ── 지면 기준선 (배경 흙길 높이 — 캔버스 절대 Y) ──
		const float GroundY = 0.62f;

		// ── 플레이어 캐릭터 (정적 스프라이트 + 호흡 애니메이션) ──
		string idlePath = "Assets/Player/IdleSprites/0.png";
		EnsurePixelSprite(idlePath);
		Sprite idleSprite = AssetDatabase.LoadAssetAtPath<Sprite>(idlePath);

		var playerBody = CreateImage(canvasGo, "PlayerBody", Color.white);
		playerBody.pivot = new Vector2(0.5f, 0f);               // 피벗 하단 → 발 기준 배치
		playerBody.anchorMin = new Vector2(0.19f, GroundY);      // 단일 앵커점 (지면)
		playerBody.anchorMax = new Vector2(0.19f, GroundY);
		playerBody.sizeDelta = new Vector2(150f, 150f);
		playerBody.localScale = new Vector3(2f, 2f, 1f);
		playerBody.localEulerAngles = new Vector3(0f, 180f, 0f); // 좌우 반전 (Y축 회전)
		var playerImg = playerBody.GetComponent<Image>();
		playerImg.preserveAspect = true;
		playerImg.useSpriteMesh = false;
		playerImg.raycastTarget = false;
		if (idleSprite != null)
			playerImg.sprite = idleSprite;

		// SpriteAnimator: Y축 스케일 호흡 애니메이션
		var spriteAnim = playerBody.gameObject.AddComponent<SpriteAnimator>();
		SetField(spriteAnim, "amplitude", 0.03f);
		SetField(spriteAnim, "speed", 2f);

		// ── 적 슬롯 (지면 위, 플레이어 오른쪽 — Explore 동일 구조) ──
		var enemySlotsArea = CreateEmpty(canvasGo, "EnemySlotsArea");
		enemySlotsArea.anchorMin = new Vector2(0.45f, GroundY);
		enemySlotsArea.anchorMax = new Vector2(0.95f, GroundY + 0.35f);
		enemySlotsArea.offsetMin = Vector2.zero;
		enemySlotsArea.offsetMax = Vector2.zero;

		// 몹 스프라이트 임포트 보장
		string[] spriteFiles = { "Slime_sample", "Goblin_sample", "Bat_sample", "Skeleton_sample" };
		for (int s = 0; s < spriteFiles.Length; s++)
			EnsureTightSprite($"Assets/Mobs/{spriteFiles[s]}.png");
		EnsureTightSprite("Assets/Mobs/Boss_Dracula_example.png");

		GameObject[] enemyPanels = new GameObject[4];
		Image[] enemyBodies = new Image[4];
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

			// 타겟 마커 (노란 테두리, Body와 동일 영역)
			var marker = CreateImage(slot.gameObject, "TargetMarker", new Color(0, 0, 0, 0));
			marker.anchorMin = new Vector2(0.05f, 0.0f);
			marker.anchorMax = new Vector2(0.95f, 0.90f);
			marker.offsetMin = Vector2.zero;
			marker.offsetMax = Vector2.zero;
			marker.GetComponent<Image>().raycastTarget = false;
			float borderThickness = 0.05f;
			var bTop = CreateImage(marker.gameObject, "BorderTop", TargetMarkerColor);
			bTop.anchorMin = new Vector2(0f, 1f - borderThickness);
			bTop.anchorMax = Vector2.one;
			bTop.offsetMin = Vector2.zero; bTop.offsetMax = Vector2.zero;
			var bBot = CreateImage(marker.gameObject, "BorderBottom", TargetMarkerColor);
			bBot.anchorMin = Vector2.zero;
			bBot.anchorMax = new Vector2(1f, borderThickness);
			bBot.offsetMin = Vector2.zero; bBot.offsetMax = Vector2.zero;
			var bLeft = CreateImage(marker.gameObject, "BorderLeft", TargetMarkerColor);
			bLeft.anchorMin = new Vector2(0f, borderThickness);
			bLeft.anchorMax = new Vector2(borderThickness, 1f - borderThickness);
			bLeft.offsetMin = Vector2.zero; bLeft.offsetMax = Vector2.zero;
			var bRight = CreateImage(marker.gameObject, "BorderRight", TargetMarkerColor);
			bRight.anchorMin = new Vector2(1f - borderThickness, borderThickness);
			bRight.anchorMax = new Vector2(1f, 1f - borderThickness);
			bRight.offsetMin = Vector2.zero; bRight.offsetMax = Vector2.zero;
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

		// 적 주사위 눈 표시 컨테이너 (각 적 슬롯의 자식 — 패널과 함께 이동)
		GameObject[] enemyDiceFaceContainers = new GameObject[4];
		for (int i = 0; i < 4; i++)
		{
			var container = CreateEmpty(enemyPanels[i], $"EnemyDiceFaces{i}");
			container.anchorMin = new Vector2(0.05f, -0.12f);
			container.anchorMax = new Vector2(0.95f, 0.02f);
			container.offsetMin = Vector2.zero;
			container.offsetMax = Vector2.zero;
			var hlg = container.gameObject.AddComponent<HorizontalLayoutGroup>();
			hlg.spacing = 2;
			hlg.childAlignment = TextAnchor.MiddleCenter;
			hlg.childControlWidth = false;
			hlg.childControlHeight = false;
			hlg.childForceExpandWidth = false;
			hlg.childForceExpandHeight = false;

			// 주사위 5개분 Image 슬롯 생성 (정사각형 고정 크기)
			float diceSize = 28f;
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

		// ── 하단 절반: UI 배경 이미지 ──
		EnsureTightSprite("Assets/Mobs/UI_Background.png");
		var uiBgImg = CreateImage(canvasGo, "UIBackground", Color.white);
		uiBgImg.anchorMin = new Vector2(0f, 0f);
		uiBgImg.anchorMax = new Vector2(1f, 0.5f);
		uiBgImg.offsetMin = Vector2.zero;
		uiBgImg.offsetMax = Vector2.zero;
		var uiBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Mobs/UI_Background.png");
		if (uiBgSprite != null)
			uiBgImg.GetComponent<Image>().sprite = uiBgSprite;
		else
			uiBgImg.GetComponent<Image>().color = BgColor;

		// ── 하단 영역: 패딩 컨테이너 → 주사위 패널(좌) + 전투 로그(우) ──
		var lowerArea = CreateEmpty(canvasGo, "LowerArea");
		lowerArea.anchorMin = new Vector2(0.05f, 0.04f);
		lowerArea.anchorMax = new Vector2(0.95f, 0.46f);
		lowerArea.offsetMin = Vector2.zero;
		lowerArea.offsetMax = Vector2.zero;

		float panelGap = 0.028f;   // 양쪽 합산 ≈ 5% 화면폭 (lowerArea 좌우 패딩과 동일)
		float panelMid = 0.50f;

		var dicePanel = CreateImage(lowerArea.gameObject, "DicePanel",
			new Color(0f, 0f, 0f, 0f));
		dicePanel.anchorMin = new Vector2(panelMid + panelGap, 0f);
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

		var heldContainer = CreateEmpty(heldArea.gameObject, "HeldDiceContainer");
		Stretch(heldContainer);
		var heldAspect = heldContainer.gameObject.AddComponent<AspectRatioFitter>();
		heldAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		heldAspect.aspectRatio = 180f / 540f;

		var heldVpGo = new GameObject("HeldDiceViewport");
		heldVpGo.transform.SetParent(heldContainer, false);
		var heldRawImg = heldVpGo.AddComponent<RawImage>();
		heldRawImg.texture = vaultRenderTex;
		heldRawImg.raycastTarget = true;
		Stretch(heldVpGo.GetComponent<RectTransform>());

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

		// ── 좌측 영역 컨테이너 ──
		var leftArea = CreateEmpty(lowerArea.gameObject, "LeftArea");
		leftArea.anchorMin = new Vector2(0f, 0f);
		leftArea.anchorMax = new Vector2(panelMid - panelGap, 1f);
		leftArea.offsetMin = Vector2.zero;
		leftArea.offsetMax = Vector2.zero;

		// ── 전투 로그 석판 (상단, UI_Chat 배경 — 원본 비율 유지) ──
		EnsureTightSprite("Assets/Mobs/UI_Chat.png");
		var chatBgSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Mobs/UI_Chat.png");

		var logPanelArea = CreateEmpty(leftArea.gameObject, "BattleLogArea");
		logPanelArea.anchorMin = new Vector2(0f, 0.22f);
		logPanelArea.anchorMax = new Vector2(1f, 1f);
		logPanelArea.offsetMin = new Vector2(-50f, -40f);
		logPanelArea.offsetMax = new Vector2(60f, 60f);

		var logPanel = CreateImage(logPanelArea.gameObject, "BattleLogPanel", Color.white);
		Stretch(logPanel);
		logPanel.localScale = new Vector3(1.3f, 1.0f, 1f);
		var logPanelAspect = logPanel.gameObject.AddComponent<AspectRatioFitter>();
		logPanelAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		logPanelAspect.aspectRatio = 1911f / 1160f;
		var logPanelImg = logPanel.GetComponent<Image>();
		logPanelImg.raycastTarget = true;
		logPanelImg.preserveAspect = false;
		if (chatBgSprite != null)
			logPanelImg.sprite = chatBgSprite;

		// ScrollRect 영역 (석판 내부 — 부모 스케일 역보정으로 텍스트 비율 유지)
		var scrollArea = CreateEmpty(logPanel.gameObject, "ScrollArea");
		scrollArea.anchorMin = new Vector2(0.12f, 0.10f);
		scrollArea.anchorMax = new Vector2(0.88f, 0.90f);
		scrollArea.offsetMin = new Vector2(-20f, 80f);
		scrollArea.offsetMax = new Vector2(0f, -50f);
		scrollArea.localScale = new Vector3(1f / 1.5f, 1f / 1.2f, 1f);

		// Viewport (마스크 — 역스케일 보상으로 석판 내부를 꽉 채움)
		// ScrollArea에 (1/1.5, 1/1.2) 역스케일이 걸려 있으므로
		// Viewport를 1.5배 넓고 1.2배 높게 잡아야 시각적으로 석판 내부와 일치
		var viewport = CreateImage(scrollArea.gameObject, "Viewport",
			new Color(0, 0, 0, 0));
		viewport.anchorMin = new Vector2(-0.25f, -0.10f);
		viewport.anchorMax = new Vector2(1.25f, 1.10f);
		viewport.offsetMin = Vector2.zero;
		viewport.offsetMax = Vector2.zero;
		viewport.gameObject.AddComponent<RectMask2D>();

		// Content (Viewport 전체 영역)
		var content = CreateEmpty(viewport.gameObject, "Content");
		content.anchorMin = new Vector2(0f, 0f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.sizeDelta = Vector2.zero;
		content.offsetMin = Vector2.zero;
		content.offsetMax = Vector2.zero;
		var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
		vlg.padding = new RectOffset(0, 0, 0, 0);
		vlg.childControlWidth = true;
		vlg.childControlHeight = false;
		vlg.childForceExpandWidth = true;
		vlg.childForceExpandHeight = false;
		var csf = content.gameObject.AddComponent<ContentSizeFitter>();
		csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		// 로그 텍스트
		var logTmp = CreateTMPText(content.gameObject, "LogText", "",
			30, new Color(0.85f, 0.85f, 0.92f), TextAlignmentOptions.TopLeft);
		logTmp.enableAutoSizing = false;
		logTmp.fontSize = 30;
		logTmp.textWrappingMode = TextWrappingModes.Normal;
		logTmp.overflowMode = TextOverflowModes.Overflow;
		logTmp.richText = true;
		var logTmpRt = logTmp.GetComponent<RectTransform>();
		logTmpRt.anchorMin = new Vector2(0f, 1f);
		logTmpRt.anchorMax = new Vector2(1f, 1f);
		logTmpRt.pivot = new Vector2(0f, 1f);
		logTmpRt.sizeDelta = Vector2.zero;
		var logTmpCsf = logTmp.gameObject.AddComponent<ContentSizeFitter>();
		logTmpCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		// ScrollRect 컴포넌트
		var scrollRect = scrollArea.gameObject.AddComponent<ScrollRect>();
		scrollRect.content = content;
		scrollRect.viewport = viewport;
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 30f;

		// BattleLog 컴포넌트
		var battleLogComp = logPanel.gameObject.AddComponent<BattleLog>();
		SetField(battleLogComp, "logText", logTmp);
		SetField(battleLogComp, "scrollRect", scrollRect);

		// ── 데미지 프리뷰 (석판 아래) ──
		var dmgPreview = CreateTMPText(leftArea.gameObject, "DamagePreview", "",
			22, AccentYellow, TextAlignmentOptions.Right);
		var dpRt = dmgPreview.GetComponent<RectTransform>();
		dpRt.anchorMin = new Vector2(0.40f, 0.12f);
		dpRt.anchorMax = new Vector2(0.97f, 0.22f);
		dpRt.offsetMin = Vector2.zero;
		dpRt.offsetMax = Vector2.zero;
		dmgPreview.fontStyle = FontStyles.Bold;

		// ── 버튼 (석판 아래) ──
		var rollBtn = CreateActionButton(leftArea.gameObject, "RollButton", "굴리기",
			new Vector2(0.03f, 0.0f), new Vector2(0.32f, 0.11f), SceneBuilderUtility.ButtonNormal);

		var confirmBtn = CreateActionButton(leftArea.gameObject, "ConfirmButton", "확정",
			new Vector2(0.68f, 0.0f), new Vector2(0.97f, 0.11f), SceneBuilderUtility.ButtonNormal);

		var cancelBtn = CreateActionButton(leftArea.gameObject, "CancelButton", "취소",
			new Vector2(0.35f, 0.0f), new Vector2(0.65f, 0.11f), CancelColor);
		SetButtonColorSet(cancelBtn.GetComponent<Button>(), CancelColor, CancelHighlight,
			new Color(0.40f, 0.12f, 0.12f));

		var nextRoundBtn = CreateActionButton(leftArea.gameObject, "NextRoundButton", "다음 턴",
			new Vector2(0.68f, 0.0f), new Vector2(0.97f, 0.11f), SceneBuilderUtility.ButtonNormal);

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

		// ── 적 주사위 팝업 (화면 중앙 오버레이) ──
		var enemyDicePopupGo = CreateEmpty(canvasGo, "EnemyDicePopup");
		Stretch(enemyDicePopupGo);

		// 반투명 배경 dimmer
		var popupDimmer = CreateImage(enemyDicePopupGo.gameObject, "Dimmer",
			new Color(0f, 0f, 0f, 0.6f));
		Stretch(popupDimmer);
		popupDimmer.GetComponent<Image>().raycastTarget = true;

		// 주사위 뷰포트 (중앙)
		var popupVpContainer = CreateEmpty(enemyDicePopupGo.gameObject, "EnemyDiceViewportContainer");
		popupVpContainer.anchorMin = new Vector2(0.2f, 0.25f);
		popupVpContainer.anchorMax = new Vector2(0.8f, 0.75f);
		popupVpContainer.offsetMin = Vector2.zero;
		popupVpContainer.offsetMax = Vector2.zero;

		var popupVpBg = CreateImage(popupVpContainer.gameObject, "PopupBg",
			new Color(0.06f, 0.06f, 0.12f, 0.95f));
		Stretch(popupVpBg);

		var popupVpGo = new GameObject("EnemyDiceViewport");
		popupVpGo.transform.SetParent(popupVpContainer, false);
		var popupRawImg = popupVpGo.AddComponent<RawImage>();
		popupRawImg.texture = enemyRenderTex;
		popupRawImg.raycastTarget = false;
		var popupVpRt = popupVpGo.GetComponent<RectTransform>();
		Stretch(popupVpRt);

		var popupTitle = CreateTMPText(popupVpContainer.gameObject, "PopupTitle",
			"적의 주사위!",
			28, AccentYellow, TextAlignmentOptions.Center);
		var popupTitleRt = popupTitle.GetComponent<RectTransform>();
		popupTitleRt.anchorMin = new Vector2(0f, 0.88f);
		popupTitleRt.anchorMax = new Vector2(1f, 1f);
		popupTitleRt.offsetMin = Vector2.zero;
		popupTitleRt.offsetMax = Vector2.zero;
		popupTitle.fontStyle = FontStyles.Bold;

		enemyDicePopupGo.gameObject.SetActive(false);


		// ── EventSystem ──
		var es = new GameObject("EventSystem");
		es.AddComponent<UnityEngine.EventSystems.EventSystem>();
		es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();

		// ── DiceViewportInteraction ──
		var dviGo = new GameObject("DiceViewportInteraction");
		var dvi = dviGo.AddComponent<DiceViewportInteraction>();
		SetField(dvi, "viewport", rawImg);
		SetField(dvi, "diceCamera", diceCam);
		SetField(dvi, "vaultCamera", vaultCam);
		SetField(dvi, "vaultViewport", heldRawImg);
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

		SetField(ctrl, "dice", diceArr);
		SetField(ctrl, "viewportInteraction", dvi);
		SetField(ctrl, "vaultCenter", VaultCenter + new Vector3(0, 0.25f, 0));
		SetField(ctrl, "enemyPanels", enemyPanels);
		SetField(ctrl, "enemyBodies", enemyBodies);
		SetField(ctrl, "enemyNames", enemyNameTexts);
		SetField(ctrl, "enemyHpFills", enemyHpFillArr);
		SetField(ctrl, "enemyHpTexts", enemyHpTextArr);
		SetField(ctrl, "targetMarkers", targetMarkers);
		SetField(ctrl, "deadOverlays", deadOverlays);

		// 몹 스프라이트 (씬 직접 로딩 시 기본 적 생성용)
		Sprite[] mobSprites = new Sprite[4];
		for (int m = 0; m < spriteFiles.Length; m++)
			mobSprites[m] = AssetDatabase.LoadAssetAtPath<Sprite>($"Assets/Mobs/{spriteFiles[m]}.png");
		SetField(ctrl, "mobSprites", mobSprites);

		SetField(ctrl, "vfx", vfxComp);
		SetField(ctrl, "battleLog", battleLogComp);
		SetField(ctrl, "battleAnims", battleAnimsComp);
		SetField(ctrl, "playerBody", playerImg);
		var rollBtnComp = rollBtn.GetComponent<Button>();
		var confirmBtnComp = confirmBtn.GetComponent<Button>();
		var cancelBtnComp = cancelBtn.GetComponent<Button>();
		var nextRoundBtnComp = nextRoundBtn.GetComponent<Button>();

		SetField(ctrl, "rollButton", rollBtnComp);
		SetField(ctrl, "confirmButton", confirmBtnComp);
		SetField(ctrl, "cancelButton", cancelBtnComp);
		SetField(ctrl, "nextRoundButton", nextRoundBtnComp);

		// 버튼 연결 (PersistentListener로 씬에 영속 저장)
		UnityEventTools.AddPersistentListener(rollBtnComp.onClick, ctrl.RollDice);
		UnityEventTools.AddPersistentListener(confirmBtnComp.onClick, ctrl.ConfirmScore);
		UnityEventTools.AddPersistentListener(cancelBtnComp.onClick, ctrl.CancelBattle);
		UnityEventTools.AddPersistentListener(nextRoundBtnComp.onClick, ctrl.NextRound);
		UnityEventTools.AddPersistentListener(enemyPanelButtons[0].onClick, ctrl.OnEnemyPanel0Clicked);
		UnityEventTools.AddPersistentListener(enemyPanelButtons[1].onClick, ctrl.OnEnemyPanel1Clicked);
		UnityEventTools.AddPersistentListener(enemyPanelButtons[2].onClick, ctrl.OnEnemyPanel2Clicked);
		UnityEventTools.AddPersistentListener(enemyPanelButtons[3].onClick, ctrl.OnEnemyPanel3Clicked);
		SetField(ctrl, "rollDotsText", rollDotsText);
		SetField(ctrl, "damagePreviewText", dmgPreview);
		SetField(ctrl, "heartDisplay", heartDisplayComp);

		// 적 주사위 시스템
		SetField(ctrl, "enemyDiceRoller", enemyRoller);
		SetField(ctrl, "enemyDicePopup", enemyDicePopupGo.gameObject);
		SetField(ctrl, "enemyDiceResultTexts", enemyDiceResultTexts);
		SetField(ctrl, "enemyDiceFaceContainers", enemyDiceFaceContainers);
		SetField(ctrl, "diceFaceSprites", diceFaceSprites);

		// ── 플레이어 사망 애니메이션 ──
		var deathAnimComp = root.AddComponent<PlayerDeathAnimator>();
		SetField(deathAnimComp, "playerBody", playerImg);

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
		SetField(rollAnimComp, "idleSprite", idleSprite);

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

		// ── 디버그 콘솔 ──
		var debugGo = new GameObject("DebugConsole");
		debugGo.AddComponent<DebugConsoleController>();

		// ── 씬 저장 ──
		string scenePath = "Assets/Scenes/GameBattleScene.unity";
		EnsureDirectory("Assets/Scenes");
		EditorSceneManager.SaveScene(scene, scenePath);
		AddSceneToBuildSettings(scenePath);
		EditorUtility.DisplayDialog("씬 빌더", "GameBattleScene 생성 완료!", "확인");
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
		Debug.LogWarning($"[GameBattleSceneBuilder] 레이어 '{layerName}' 등록 실패");
		return 0;
	}

	static void SetField(object target, string fieldName, object value)
		=> SceneBuilderUtility.SetField(target, fieldName, value);

	static void EnsureDirectory(string path) => SceneBuilderUtility.EnsureDirectory(path);

	static void AddSceneToBuildSettings(string scenePath)
		=> SceneBuilderUtility.AddSceneToBuildSettings(scenePath);

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

	/// <summary>주사위 눈 1~6 스프라이트를 생성/갱신하여 반환.</summary>
	static Sprite[] GenerateDiceFaceSprites()
	{
		EnsureDirectory("Assets/Textures/DiceFaces");

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

		Sprite[] sprites = new Sprite[6];
		for (int face = 0; face < 6; face++)
		{
			string path = $"Assets/Textures/DiceFaces/face{face + 1}.png";
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

			var importer = (TextureImporter)AssetImporter.GetAtPath(path);
			importer.textureType = TextureImporterType.Sprite;
			importer.spritePixelsPerUnit = 64;
			importer.filterMode = FilterMode.Point;
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			importer.SaveAndReimport();

			sprites[face] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
		}
		return sprites;
	}

	static Vector2Int V(int x, int y) => new Vector2Int(x, y);

	/// <summary>주사위 5개를 생성하고 YachtDie 배열을 반환.</summary>
	static YachtDie[] CreateDiceSet(string namePrefix, Vector3 basePos, int layer,
		PhysicsMaterial bouncyMat, Material outlineMat, GameObject prefab, float dieRadius)
	{
		var dice = new YachtDie[5];
		for (int i = 0; i < 5; i++)
		{
			float x = -2f + i * 1f;
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
			dieGo.transform.localScale = Vector3.one * 0.5f;
			SetLayerRecursive(dieGo, layer);

			var rb = dieGo.GetComponent<Rigidbody>();
			if (rb == null) rb = dieGo.AddComponent<Rigidbody>();
			rb.mass = 0.5f;
			rb.linearDamping = 0.25f;
			rb.angularDamping = 0.25f;
			rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

			foreach (var col in dieGo.GetComponentsInChildren<Collider>())
				col.material = bouncyMat;

			var yachtDie = dieGo.GetComponent<YachtDie>();
			if (yachtDie == null) yachtDie = dieGo.AddComponent<YachtDie>();
			SetField(yachtDie, "outlineBaseMaterial", outlineMat);
			dice[i] = yachtDie;
		}
		return dice;
	}
}
