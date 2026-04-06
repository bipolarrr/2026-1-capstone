using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using System.Reflection;
using UnityEditor.Events;

public static class GameBattleSceneBuilder
{
	// ── 색상 ──
	static readonly Color BgColor = new Color(0.08f, 0.08f, 0.14f);
	static readonly Color PanelBg = new Color(0.12f, 0.14f, 0.24f, 0.95f);
	static readonly Color ButtonNormal = new Color(0.15f, 0.18f, 0.35f, 0.9f);
	static readonly Color ButtonHighlight = new Color(0.28f, 0.35f, 0.70f, 1f);
	static readonly Color ButtonPressed = new Color(0.10f, 0.12f, 0.25f, 1f);
	static readonly Color CancelColor = new Color(0.55f, 0.18f, 0.18f, 0.9f);
	static readonly Color CancelHighlight = new Color(0.70f, 0.25f, 0.25f, 1f);
	static readonly Color HpBarBg = new Color(0.15f, 0.15f, 0.15f);
	static readonly Color EnemyHpFill = new Color(0.85f, 0.25f, 0.25f);
	static readonly Color PlayerHpFill = new Color(0.3f, 0.85f, 0.35f);
	static readonly Color TargetMarkerColor = new Color(1f, 0.85f, 0.2f, 0.9f);
	static readonly Color AccentYellow = new Color(1f, 0.85f, 0.3f);
	static readonly Color SaveZoneVisual = new Color(0.55f, 0.40f, 0.04f, 0.6f);

	[MenuItem("Tools/Build GameBattle Scene")]
	public static void BuildScene()
	{
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		// ── RenderTexture ──
		string rtPath = "Assets/Textures/DiceRenderTexture.renderTexture";
		EnsureDirectory("Assets/Textures");
		var renderTex = AssetDatabase.LoadAssetAtPath<RenderTexture>(rtPath);
		if (renderTex == null)
		{
			renderTex = new RenderTexture(960, 540, 16, RenderTextureFormat.ARGB32);
			renderTex.name = "DiceRenderTexture";
			AssetDatabase.CreateAsset(renderTex, rtPath);
		}

		// ── PhysicsMaterial ──
		string pmPath = "Assets/Physics/DiceBouncy.asset";
		EnsureDirectory("Assets/Physics");
		var bouncyMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(pmPath);
		if (bouncyMat == null)
		{
			bouncyMat = new PhysicsMaterial("DiceBouncy")
			{
				bounciness = 0.45f,
				dynamicFriction = 0.4f,
				staticFriction = 0.5f,
				bounceCombine = PhysicsMaterialCombine.Maximum
			};
			AssetDatabase.CreateAsset(bouncyMat, pmPath);
		}

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
		diceCam.backgroundColor = new Color(0.12f, 0.12f, 0.20f);
		diceCam.fieldOfView = 62;
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
		var wallMat = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(wallPmPath);
		if (wallMat == null)
		{
			wallMat = new PhysicsMaterial("WallBouncy")
			{
				bounciness = 0.7f,
				dynamicFriction = 0.1f,
				staticFriction = 0.1f,
				bounceCombine = PhysicsMaterialCombine.Maximum
			};
			AssetDatabase.CreateAsset(wallMat, wallPmPath);
		}

		// ── 물리 환경 ──
		BuildPhysicsEnvironment(diceLayer, bouncyMat, wallMat);

		// ── 주사위 격리 공간 (Vault) ──
		BuildVault(diceLayer, bouncyMat, wallMat);

		// Vault RenderTexture (굴림 뷰와 동일한 가로 해상도, 낮은 세로)
		string vaultRtPath = "Assets/Textures/VaultRenderTexture.renderTexture";
		var vaultRenderTex = AssetDatabase.LoadAssetAtPath<RenderTexture>(vaultRtPath);
		if (vaultRenderTex == null)
		{
			vaultRenderTex = new RenderTexture(960, 180, 16, RenderTextureFormat.ARGB32);
			vaultRenderTex.name = "VaultRenderTexture";
			AssetDatabase.CreateAsset(vaultRenderTex, vaultRtPath);
		}
		else if (vaultRenderTex.width != 960 || vaultRenderTex.height != 180)
		{
			vaultRenderTex.Release();
			vaultRenderTex.width = 960;
			vaultRenderTex.height = 180;
			vaultRenderTex.Create();
		}

		// Vault Camera (직하향 정사영)
		var vaultCamGo = new GameObject("VaultCamera");
		var vaultCam = vaultCamGo.AddComponent<Camera>();
		vaultCam.orthographic = true;
		vaultCam.orthographicSize = 0.55f;
		vaultCam.nearClipPlane = 0.3f;
		vaultCam.farClipPlane = 10f;
		vaultCam.clearFlags = CameraClearFlags.SolidColor;
		vaultCam.backgroundColor = new Color(0.06f, 0.06f, 0.12f);
		vaultCam.cullingMask = 1 << diceLayer;
		vaultCam.targetTexture = vaultRenderTex;
		vaultCamGo.transform.position = VaultCenter + new Vector3(0, 5, 0);
		vaultCamGo.transform.rotation = Quaternion.Euler(90, 0, 0);

		// ── 주사위 5개 ──
		var dicePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Dices/Prefabs/Dice_d6.prefab");
		YachtDie[] diceArr = new YachtDie[5];
		float dieRadius = 0.25f;

		for (int i = 0; i < 5; i++)
		{
			float x = -2f + i * 1f;
			GameObject dieGo;
			if (dicePrefab != null)
			{
				dieGo = (GameObject)PrefabUtility.InstantiatePrefab(dicePrefab);
				dieGo.name = $"Die{i}";
			}
			else
			{
				dieGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
				dieGo.name = $"Die{i}";
				dieGo.transform.localScale = Vector3.one * 0.5f;
			}

			dieGo.transform.position = new Vector3(x, dieRadius, 0);
			dieGo.transform.localScale = Vector3.one * 0.5f;
			SetLayerRecursive(dieGo, diceLayer);

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
			diceArr[i] = yachtDie;
		}

		// ── 캔버스 ──
		var canvasGo = new GameObject("Canvas");
		var canvas = canvasGo.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		var scaler = canvasGo.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		canvasGo.AddComponent<GraphicRaycaster>();

		// ── 배경 ──
		var bg = CreateImage(canvasGo, "Background", BgColor);
		Stretch(bg);

		// ── 적 영역 (상단 반) ──
		var enemyArea = CreateEmpty(canvasGo, "EnemyArea");
		enemyArea.anchorMin = new Vector2(0.05f, 0.53f);
		enemyArea.anchorMax = new Vector2(0.95f, 0.90f);
		enemyArea.offsetMin = Vector2.zero;
		enemyArea.offsetMax = Vector2.zero;

		GameObject[] enemyPanels = new GameObject[4];
		Image[] enemyBodies = new Image[4];
		TMP_Text[] enemyNameTexts = new TMP_Text[4];
		Image[] enemyHpFillArr = new Image[4];
		TMP_Text[] enemyHpTextArr = new TMP_Text[4];
		Image[] targetMarkers = new Image[4];
		TMP_Text[] deadOverlays = new TMP_Text[4];
		Button[] enemyPanelButtons = new Button[4];

		for (int i = 0; i < 4; i++)
		{
			float x0 = i * 0.25f;
			float x1 = x0 + 0.24f;

			var panel = CreateImage(enemyArea.gameObject, $"EnemyPanel{i}", PanelBg);
			panel.anchorMin = new Vector2(x0, 0.0f);
			panel.anchorMax = new Vector2(x1, 1.0f);
			panel.offsetMin = Vector2.zero;
			panel.offsetMax = Vector2.zero;
			enemyPanels[i] = panel.gameObject;

			// 타겟 마커 (테두리만 표시)
			var marker = CreateImage(panel.gameObject, "TargetMarker", new Color(0, 0, 0, 0));
			Stretch(marker);
			marker.GetComponent<Image>().raycastTarget = false;
			float borderThickness = 0.04f;
			// 상
			var bTop = CreateImage(marker.gameObject, "BorderTop", TargetMarkerColor);
			bTop.anchorMin = new Vector2(0f, 1f - borderThickness);
			bTop.anchorMax = Vector2.one;
			bTop.offsetMin = Vector2.zero; bTop.offsetMax = Vector2.zero;
			// 하
			var bBot = CreateImage(marker.gameObject, "BorderBottom", TargetMarkerColor);
			bBot.anchorMin = Vector2.zero;
			bBot.anchorMax = new Vector2(1f, borderThickness);
			bBot.offsetMin = Vector2.zero; bBot.offsetMax = Vector2.zero;
			// 좌
			var bLeft = CreateImage(marker.gameObject, "BorderLeft", TargetMarkerColor);
			bLeft.anchorMin = new Vector2(0f, borderThickness);
			bLeft.anchorMax = new Vector2(borderThickness, 1f - borderThickness);
			bLeft.offsetMin = Vector2.zero; bLeft.offsetMax = Vector2.zero;
			// 우
			var bRight = CreateImage(marker.gameObject, "BorderRight", TargetMarkerColor);
			bRight.anchorMin = new Vector2(1f - borderThickness, borderThickness);
			bRight.anchorMax = new Vector2(1f, 1f - borderThickness);
			bRight.offsetMin = Vector2.zero; bRight.offsetMax = Vector2.zero;
			targetMarkers[i] = marker.GetComponent<Image>();
			marker.gameObject.SetActive(false);

			// 적 몸체
			var body = CreateImage(panel.gameObject, "Body", Color.gray);
			body.anchorMin = new Vector2(0.10f, 0.35f);
			body.anchorMax = new Vector2(0.90f, 0.90f);
			body.offsetMin = Vector2.zero;
			body.offsetMax = Vector2.zero;
			enemyBodies[i] = body.GetComponent<Image>();

			// 사망 오버레이 (X 표시, 몸체 위에 겹침)
			var deadOverlay = CreateTMPText(panel.gameObject, "DeadOverlay", "✕",
				80, new Color(1f, 0.2f, 0.2f, 0.85f), TextAlignmentOptions.Center);
			var deadRt = deadOverlay.GetComponent<RectTransform>();
			deadRt.anchorMin = new Vector2(0.10f, 0.35f);
			deadRt.anchorMax = new Vector2(0.90f, 0.90f);
			deadRt.offsetMin = Vector2.zero;
			deadRt.offsetMax = Vector2.zero;
			deadOverlay.raycastTarget = false;
			deadOverlay.gameObject.SetActive(false);
			deadOverlays[i] = deadOverlay;

			// 이름 (좌하단, HP바 위)
			var nameT = CreateTMPText(panel.gameObject, "Name", "적",
				28, Color.white, TextAlignmentOptions.BottomLeft);
			var nrt = nameT.GetComponent<RectTransform>();
			nrt.anchorMin = new Vector2(0.08f, 0.17f);
			nrt.anchorMax = new Vector2(0.50f, 0.30f);
			nrt.offsetMin = Vector2.zero;
			nrt.offsetMax = Vector2.zero;
			nameT.fontStyle = FontStyles.Bold;
			enemyNameTexts[i] = nameT;

			// HP 바
			var hpBg = CreateImage(panel.gameObject, "HpBarBg", HpBarBg);
			hpBg.anchorMin = new Vector2(0.10f, 0.08f);
			hpBg.anchorMax = new Vector2(0.90f, 0.16f);
			hpBg.offsetMin = Vector2.zero;
			hpBg.offsetMax = Vector2.zero;

			var eFill = CreateImage(hpBg.gameObject, "HpFill", EnemyHpFill);
			Stretch(eFill);
			var eFillImg = eFill.GetComponent<Image>();
			eFillImg.sprite = SceneBuilderUtility.WhitePixelSprite();
			eFillImg.type = Image.Type.Filled;
			eFillImg.fillMethod = Image.FillMethod.Horizontal;
			enemyHpFillArr[i] = eFill.GetComponent<Image>();

			var eHpT = CreateTMPText(panel.gameObject, "HpText", "0 / 0",
				22, Color.white, TextAlignmentOptions.BottomRight);
			var eHpRt = eHpT.GetComponent<RectTransform>();
			eHpRt.anchorMin = new Vector2(0.50f, 0.17f);
			eHpRt.anchorMax = new Vector2(0.92f, 0.30f);
			eHpRt.offsetMin = Vector2.zero;
			eHpRt.offsetMax = Vector2.zero;
			enemyHpTextArr[i] = eHpT;

			// 패널 클릭으로 타겟 선택 — 기존 Image를 targetGraphic으로 사용
			var panelImg = panel.GetComponent<Image>();
			panelImg.raycastTarget = true;
			var panelBtn = panel.gameObject.AddComponent<Button>();
			panelBtn.targetGraphic = panelImg;
			var btnColors = panelBtn.colors;
			btnColors.normalColor = PanelBg;
			btnColors.highlightedColor = new Color(0.20f, 0.22f, 0.38f, 0.95f);
			btnColors.pressedColor = new Color(0.08f, 0.10f, 0.18f, 0.95f);
			btnColors.selectedColor = btnColors.highlightedColor;
			panelBtn.colors = btnColors;
			enemyPanelButtons[i] = panelBtn;
		}

		// 데미지 스폰 영역 (적 패널 위쪽에 겹침)
		var dmgSpawn = CreateEmpty(canvasGo, "DamageSpawnArea");
		dmgSpawn.anchorMin = new Vector2(0.05f, 0.68f);
		dmgSpawn.anchorMax = new Vector2(0.95f, 0.82f);
		dmgSpawn.offsetMin = Vector2.zero;
		dmgSpawn.offsetMax = Vector2.zero;

		// ── 플레이어 HP (좌상단) ──
		var pHpBg = CreateImage(canvasGo, "PlayerHpBarBg", HpBarBg);
		pHpBg.anchorMin = new Vector2(0.02f, 0.95f);
		pHpBg.anchorMax = new Vector2(0.22f, 0.98f);
		pHpBg.offsetMin = Vector2.zero;
		pHpBg.offsetMax = Vector2.zero;

		var pHpFill = CreateImage(pHpBg.gameObject, "PlayerHpFill", PlayerHpFill);
		Stretch(pHpFill);
		var pHpFillImg = pHpFill.GetComponent<Image>();
		pHpFillImg.sprite = SceneBuilderUtility.WhitePixelSprite();
		pHpFillImg.type = Image.Type.Filled;
		pHpFillImg.fillMethod = Image.FillMethod.Horizontal;

		var pHpText = CreateTMPText(canvasGo, "PlayerHpText", "HP 100 / 100",
			22, Color.white, TextAlignmentOptions.Left);
		var pHpRt = pHpText.GetComponent<RectTransform>();
		pHpRt.anchorMin = new Vector2(0.02f, 0.91f);
		pHpRt.anchorMax = new Vector2(0.22f, 0.95f);
		pHpRt.offsetMin = Vector2.zero;
		pHpRt.offsetMax = Vector2.zero;

		// ── 하단 영역: 주사위 패널(좌) + 전투 로그(우) — 적 영역과 좌우 정렬 ──
		float panelY0 = 0.08f, panelY1 = 0.50f;
		float panelMargin = 0.05f;  // 적 영역과 동일
		float panelGap = 0.01f;
		float panelMid = 0.50f;

		var dicePanel = CreateImage(canvasGo, "DicePanel",
			new Color(0.06f, 0.06f, 0.12f, 0.95f));
		dicePanel.anchorMin = new Vector2(panelMargin, panelY0);
		dicePanel.anchorMax = new Vector2(panelMid - panelGap, panelY1);
		dicePanel.offsetMin = Vector2.zero;
		dicePanel.offsetMax = Vector2.zero;
		dicePanel.GetComponent<Image>().raycastTarget = false;
		var diceBorder = dicePanel.gameObject.AddComponent<Outline>();
		diceBorder.effectColor = new Color(0.25f, 0.28f, 0.50f, 0.6f);
		diceBorder.effectDistance = new Vector2(2f, 2f);

		// ── 주사위 굴림 뷰포트 (상단 71%, AspectRatioFitter로 비율 보존) ──
		var vpArea = CreateEmpty(dicePanel.gameObject, "DiceViewportArea");
		vpArea.anchorMin = new Vector2(0f, 0.29f);
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

		// ── 구분선 + 라벨 ──
		var divider = CreateImage(dicePanel.gameObject, "Divider",
			new Color(0.30f, 0.35f, 0.60f, 0.5f));
		divider.anchorMin = new Vector2(0.02f, 0.268f);
		divider.anchorMax = new Vector2(0.98f, 0.272f);
		divider.offsetMin = Vector2.zero;
		divider.offsetMax = Vector2.zero;

		var heldLabel = CreateTMPText(dicePanel.gameObject, "HeldDiceLabel", "저장된 주사위",
			22, new Color(0.65f, 0.68f, 0.85f), TextAlignmentOptions.Left);
		var hlRt = heldLabel.GetComponent<RectTransform>();
		hlRt.anchorMin = new Vector2(0.03f, 0.22f);
		hlRt.anchorMax = new Vector2(0.35f, 0.27f);
		hlRt.offsetMin = Vector2.zero;
		hlRt.offsetMax = Vector2.zero;

		// ── 저장된 주사위 프리뷰 (하단 22%, AspectRatioFitter로 비율 보존) ──
		var heldArea = CreateEmpty(dicePanel.gameObject, "HeldDiceArea");
		heldArea.anchorMin = new Vector2(0f, 0f);
		heldArea.anchorMax = new Vector2(1f, 0.22f);
		heldArea.offsetMin = Vector2.zero;
		heldArea.offsetMax = Vector2.zero;

		var heldContainer = CreateEmpty(heldArea.gameObject, "HeldDiceContainer");
		Stretch(heldContainer);
		var heldAspect = heldContainer.gameObject.AddComponent<AspectRatioFitter>();
		heldAspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		heldAspect.aspectRatio = 960f / 180f;

		var heldVpGo = new GameObject("HeldDiceViewport");
		heldVpGo.transform.SetParent(heldContainer, false);
		var heldRawImg = heldVpGo.AddComponent<RawImage>();
		heldRawImg.texture = vaultRenderTex;
		heldRawImg.raycastTarget = true;
		Stretch(heldVpGo.GetComponent<RectTransform>());

		// ── 전투 로그 패널 (우측, 주사위 패널과 대칭) ──
		var logPanel = CreateImage(canvasGo, "BattleLogPanel",
			new Color(0.06f, 0.06f, 0.12f, 0.95f));
		logPanel.anchorMin = new Vector2(panelMid + panelGap, panelY0);
		logPanel.anchorMax = new Vector2(1f - panelMargin, panelY1);
		logPanel.offsetMin = Vector2.zero;
		logPanel.offsetMax = Vector2.zero;
		logPanel.GetComponent<Image>().raycastTarget = true;
		var logBorder = logPanel.gameObject.AddComponent<Outline>();
		logBorder.effectColor = new Color(0.25f, 0.28f, 0.50f, 0.6f);
		logBorder.effectDistance = new Vector2(2f, 2f);

		// ScrollRect 영역 (상단 ~78% — 하단에 버튼 공간 확보)
		var scrollArea = CreateEmpty(logPanel.gameObject, "ScrollArea");
		scrollArea.anchorMin = new Vector2(0.02f, 0.22f);
		scrollArea.anchorMax = new Vector2(0.98f, 0.98f);
		scrollArea.offsetMin = Vector2.zero;
		scrollArea.offsetMax = Vector2.zero;

		// Viewport (마스크)
		var viewport = CreateImage(scrollArea.gameObject, "Viewport",
			new Color(0, 0, 0, 0));
		Stretch(viewport);
		viewport.gameObject.AddComponent<RectMask2D>();

		// Content (텍스트가 들어가는 늘어나는 영역)
		var content = CreateEmpty(viewport.gameObject, "Content");
		content.anchorMin = new Vector2(0f, 0f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.sizeDelta = Vector2.zero;
		content.offsetMin = Vector2.zero;
		content.offsetMax = Vector2.zero;
		var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
		vlg.childControlWidth = true;
		vlg.childControlHeight = false;
		vlg.childForceExpandWidth = true;
		vlg.childForceExpandHeight = false;
		var csf = content.gameObject.AddComponent<ContentSizeFitter>();
		csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		// 로그 텍스트
		var logTmp = CreateTMPText(content.gameObject, "LogText", "",
			20, new Color(0.85f, 0.85f, 0.92f), TextAlignmentOptions.TopLeft);
		logTmp.textWrappingMode = TextWrappingModes.Normal;
		logTmp.overflowMode = TextOverflowModes.Overflow;
		logTmp.richText = true;
		var logTmpRt = logTmp.GetComponent<RectTransform>();
		logTmpRt.anchorMin = new Vector2(0f, 1f);
		logTmpRt.anchorMax = new Vector2(1f, 1f);
		logTmpRt.pivot = new Vector2(0.5f, 1f);
		logTmpRt.sizeDelta = new Vector2(-12f, 0f);
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

		// ── 로그 패널 하단 버튼 ──
		var rollBtn = CreateActionButton(logPanel.gameObject, "RollButton", "굴리기",
			new Vector2(0.03f, 0.02f), new Vector2(0.32f, 0.12f), ButtonNormal);

		var confirmBtn = CreateActionButton(logPanel.gameObject, "ConfirmButton", "확정",
			new Vector2(0.68f, 0.02f), new Vector2(0.97f, 0.12f), ButtonNormal);

		var cancelBtn = CreateActionButton(logPanel.gameObject, "CancelButton", "취소",
			new Vector2(0.35f, 0.02f), new Vector2(0.65f, 0.12f), CancelColor);
		SetButtonColorSet(cancelBtn.GetComponent<Button>(), CancelColor, CancelHighlight,
			new Color(0.40f, 0.12f, 0.12f));

		var nextRoundBtn = CreateActionButton(logPanel.gameObject, "NextRoundButton", "다음 턴",
			new Vector2(0.68f, 0.02f), new Vector2(0.97f, 0.12f), ButtonNormal);

		// 남은 굴림 + 데미지 프리뷰 (로그 패널 중단)
		var rollsText = CreateTMPText(logPanel.gameObject, "RollsText", "남은 굴림: 3",
			22, Color.white, TextAlignmentOptions.Left);
		var rlRt = rollsText.GetComponent<RectTransform>();
		rlRt.anchorMin = new Vector2(0.04f, 0.13f);
		rlRt.anchorMax = new Vector2(0.40f, 0.21f);
		rlRt.offsetMin = Vector2.zero;
		rlRt.offsetMax = Vector2.zero;

		var dmgPreview = CreateTMPText(logPanel.gameObject, "DamagePreview", "",
			22, AccentYellow, TextAlignmentOptions.Right);
		var dpRt = dmgPreview.GetComponent<RectTransform>();
		dpRt.anchorMin = new Vector2(0.40f, 0.13f);
		dpRt.anchorMax = new Vector2(0.97f, 0.21f);
		dpRt.offsetMin = Vector2.zero;
		dpRt.offsetMax = Vector2.zero;
		dmgPreview.fontStyle = FontStyles.Bold;

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
		SetField(ctrl, "vfx", vfxComp);
		SetField(ctrl, "battleLog", battleLogComp);
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
		SetField(ctrl, "rollsText", rollsText);
		SetField(ctrl, "damagePreviewText", dmgPreview);
		SetField(ctrl, "playerHpFill", pHpFill.GetComponent<Image>());
		SetField(ctrl, "playerHpText", pHpText);

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
		// 밀폐 물리 공간: 모든 벽·천장이 바닥/서로와 겹치도록(overlap) 배치하여 틈을 원천 차단.
		// 벽 두께 1.0, 높이 8로 충분한 여유. 바닥/천장은 벽을 덮을 만큼 넓게.
		float wallThick = 1.0f;
		float halfThick = wallThick * 0.5f;
		float arenaW = 10f;    // X 방향 내부 폭
		float arenaD = 4f;     // Z 방향 내부 깊이
		float arenaH = 8f;     // Y 방향 내부 높이
		float totalW = arenaW + wallThick * 2f;
		float totalD = arenaD + wallThick * 2f;

		// 바닥 (floorMat — 낮은 탄성)
		CreatePhysicsBox("Floor",
			new Vector3(0, -halfThick, 0),
			new Vector3(totalW, wallThick, totalD),
			layer, floorMat, false);

		// 천장
		CreatePhysicsBox("Ceiling",
			new Vector3(0, arenaH + halfThick, 0),
			new Vector3(totalW, wallThick, totalD),
			layer, wallMat, false);

		// 좌우 벽 (wallMat — 높은 탄성)
		float wallX = arenaW * 0.5f + halfThick;
		CreatePhysicsBox("WallLeft",
			new Vector3(-wallX, arenaH * 0.5f, 0),
			new Vector3(wallThick, arenaH + wallThick * 2f, totalD),
			layer, wallMat, false);
		CreatePhysicsBox("WallRight",
			new Vector3(wallX, arenaH * 0.5f, 0),
			new Vector3(wallThick, arenaH + wallThick * 2f, totalD),
			layer, wallMat, false);

		// 전후 벽
		float wallZ_front = -(arenaD * 0.5f + halfThick);
		float wallZ_back  =  (arenaD * 0.5f + halfThick);
		CreatePhysicsBox("WallFront",
			new Vector3(0, arenaH * 0.5f, wallZ_front),
			new Vector3(totalW, arenaH + wallThick * 2f, wallThick),
			layer, wallMat, false);
		CreatePhysicsBox("WallBack",
			new Vector3(0, arenaH * 0.5f, wallZ_back),
			new Vector3(totalW, arenaH + wallThick * 2f, wallThick),
			layer, wallMat, false);
	}

	// ── 주사위 격리 공간 (Vault) ──

	static readonly Vector3 VaultCenter = new Vector3(0, 0, 50);

	static void BuildVault(int layer, PhysicsMaterial floorMat, PhysicsMaterial wallMat)
	{
		var c = VaultCenter;
		float wallThick = 1.0f;
		float halfThick = wallThick * 0.5f;
		float vW = 8f, vD = 4f, vH = 6f;
		float totalW = vW + wallThick * 2f;
		float totalD = vD + wallThick * 2f;

		// 바닥 (카메라에 보이도록 visible)
		var floor = CreatePhysicsBox("VaultFloor",
			c + new Vector3(0, -halfThick, 0),
			new Vector3(totalW, wallThick, totalD),
			layer, floorMat, true);
		var floorRenderer = floor.GetComponent<MeshRenderer>();
		if (floorRenderer != null)
		{
			var floorRendMat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
			floorRendMat.color = new Color(0.10f, 0.10f, 0.18f);
			floorRenderer.material = floorRendMat;
		}

		// 천장
		CreatePhysicsBox("VaultCeiling",
			c + new Vector3(0, vH + halfThick, 0),
			new Vector3(totalW, wallThick, totalD),
			layer, wallMat, false);

		// 좌우 벽
		float wx = vW * 0.5f + halfThick;
		CreatePhysicsBox("VaultWallL",
			c + new Vector3(-wx, vH * 0.5f, 0),
			new Vector3(wallThick, vH + wallThick * 2f, totalD),
			layer, wallMat, false);
		CreatePhysicsBox("VaultWallR",
			c + new Vector3(wx, vH * 0.5f, 0),
			new Vector3(wallThick, vH + wallThick * 2f, totalD),
			layer, wallMat, false);

		// 전후 벽
		float wzF = -(vD * 0.5f + halfThick);
		float wzB =  (vD * 0.5f + halfThick);
		CreatePhysicsBox("VaultWallF",
			c + new Vector3(0, vH * 0.5f, wzF),
			new Vector3(totalW, vH + wallThick * 2f, wallThick),
			layer, wallMat, false);
		CreatePhysicsBox("VaultWallB",
			c + new Vector3(0, vH * 0.5f, wzB),
			new Vector3(totalW, vH + wallThick * 2f, wallThick),
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
			28, bgColor, ButtonHighlight, ButtonPressed);
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
}
