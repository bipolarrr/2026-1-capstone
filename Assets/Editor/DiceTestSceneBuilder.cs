// Assets/Editor/DiceTestSceneBuilder.cs
// Unity 메뉴 → Tools → Build DiceTest Scene
//
// 화면 구성:
//   상반부 — 적 (네모 + 체력바 + 데미지 텍스트)
//   하반부 — 주사위 UI (3D 뷰포트 반폭 + 버튼/텍스트)

using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using TMPro;

public static class DiceTestSceneBuilder
{
	private const string ScenePath = "Assets/Scenes/DiceTest.unity";
	private const string RenderTexturePath = "Assets/Textures/DiceRenderTexture.renderTexture";
	private const string PhysicsMatPath = "Assets/Physics/DiceBouncy.asset";
	private const string D6Path = "Assets/Dices/Prefabs/Dice_d6.prefab";
	private const string LayerName = "Dice3D";
	private const string FontPath = "Assets/TextMesh Pro/Fonts/Mona12.asset";

	private const int DiceCount = 5;
	private const float DiceSpacing = 1.0f;
	private const float DiceScale = 0.5f;
	private const float HomeZ = 0.0f;
	private const float SaveZ = 2.3f;

	[MenuItem("Tools/Build DiceTest Scene")]
	public static void Build()
	{
		if (File.Exists(ScenePath))
		{
			if (!EditorUtility.DisplayDialog("DiceTest 씬 생성",
				$"{ScenePath} 가 이미 존재합니다. 덮어쓰시겠습니까?", "덮어쓰기", "취소"))
				return;
		}

		var d6Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(D6Path);
		if (d6Prefab == null)
		{
			EditorUtility.DisplayDialog("오류", $"d6 프리팹을 찾을 수 없습니다:\n{D6Path}", "확인");
			return;
		}

		// ── 레이어 ───────────────────────────────────────────────────
		int diceLayer = EnsureLayer(LayerName);

		// ── RenderTexture ────────────────────────────────────────────
		Directory.CreateDirectory("Assets/Textures");
		if (File.Exists(RenderTexturePath))
			AssetDatabase.DeleteAsset(RenderTexturePath);
		var renderTexture = new RenderTexture(960, 540, 24, RenderTextureFormat.ARGB32)
		{
			name = "DiceRenderTexture"
		};
		AssetDatabase.CreateAsset(renderTexture, RenderTexturePath);

		// ── PhysicsMaterial ──────────────────────────────────────────
		Directory.CreateDirectory("Assets/Physics");
		if (File.Exists(PhysicsMatPath))
			AssetDatabase.DeleteAsset(PhysicsMatPath);
		var bouncyMaterial = new PhysicsMaterial("DiceBouncy")
		{
			bounciness = 0.45f,
			dynamicFriction = 0.4f,
			staticFriction = 0.5f,
			bounceCombine = PhysicsMaterialCombine.Maximum,
		};
		AssetDatabase.CreateAsset(bouncyMaterial, PhysicsMatPath);
		AssetDatabase.SaveAssets();

		// ── 새 씬 ────────────────────────────────────────────────────
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		// =============================================================
		// 3D 영역
		// =============================================================

		var directionalLight = new GameObject("DirectionalLight");
		var light = directionalLight.AddComponent<Light>();
		light.type = LightType.Directional;
		light.color = new Color(1f, 0.95f, 0.85f);
		light.intensity = 1.2f;
		directionalLight.transform.rotation = Quaternion.Euler(45f, 30f, 0f);

		var diceCameraObject = new GameObject("DiceCamera");
		var diceCamera = diceCameraObject.AddComponent<Camera>();
		diceCamera.clearFlags = CameraClearFlags.SolidColor;
		diceCamera.backgroundColor = new Color(0.08f, 0.09f, 0.15f, 1f);
		diceCamera.cullingMask = 1 << diceLayer;
		diceCamera.orthographic = false;
		diceCamera.fieldOfView = 62f;
		diceCamera.targetTexture = renderTexture;
		diceCamera.depth = -2;
		diceCameraObject.transform.position = new Vector3(0f, 6f, -3f);
		diceCameraObject.transform.rotation = Quaternion.LookRotation(
			new Vector3(0f, 1.15f, 0f) - new Vector3(0f, 6f, -3f));

		MakeBox("Floor_Roll", diceLayer, bouncyMaterial,
			new Vector3(0f, -0.15f, HomeZ), new Vector3(9f, 0.3f, 4f));
		MakeBox("Floor_Save", diceLayer, bouncyMaterial,
			new Vector3(0f, -0.10f, SaveZ), new Vector3(9f, 0.3f, 2f));

		var savePlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
		savePlatform.name = "SaveZonePlatform";
		savePlatform.layer = diceLayer;
		savePlatform.transform.position = new Vector3(0f, -0.08f, SaveZ);
		savePlatform.transform.localScale = new Vector3(9f, 0.04f, 2f);
		Object.DestroyImmediate(savePlatform.GetComponent<BoxCollider>());
		var saveMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
		saveMaterial.color = new Color(0.55f, 0.40f, 0.04f, 0.6f);
		savePlatform.GetComponent<MeshRenderer>().sharedMaterial = saveMaterial;

		// 벽 (두꺼운 콜라이더로 클리핑 방지)
		MakeBox("WallLeft", diceLayer, bouncyMaterial, new Vector3(-4.5f, 1f, 1.15f), new Vector3(1f, 6f, 9f));
		MakeBox("WallRight", diceLayer, bouncyMaterial, new Vector3(4.5f, 1f, 1.15f), new Vector3(1f, 6f, 9f));
		MakeBox("WallBack", diceLayer, bouncyMaterial, new Vector3(0f, 1f, 4f), new Vector3(10f, 6f, 1f));
		MakeBox("WallFront", diceLayer, bouncyMaterial, new Vector3(0f, 1f, -1.7f), new Vector3(10f, 6f, 1f));
		// 천장 (위로 튀어나가는 것 방지)
		MakeBox("Ceiling", diceLayer, bouncyMaterial, new Vector3(0f, 4f, 1.15f), new Vector3(10f, 1f, 9f));

		float startX = -(DiceCount - 1) / 2f * DiceSpacing;
		var diceComponents = new YachtDie[DiceCount];

		for (int i = 0; i < DiceCount; i++)
		{
			var die = (GameObject)PrefabUtility.InstantiatePrefab(d6Prefab);
			die.name = $"Die_{i}";
			SetLayerAll(die, diceLayer);
			die.transform.localScale = Vector3.one * DiceScale;

			var homePosition = new Vector3(startX + i * DiceSpacing, 0.15f, HomeZ);
			die.transform.position = homePosition;

			var meshCollider = die.GetComponent<MeshCollider>();
			if (meshCollider != null)
				meshCollider.sharedMaterial = bouncyMaterial;

			var rigidbody = die.AddComponent<Rigidbody>();
			rigidbody.mass = 0.5f;
			rigidbody.linearDamping = 0.25f;
			rigidbody.angularDamping = 0.25f;
			rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

			diceComponents[i] = die.AddComponent<YachtDie>();
		}

		var interactionObject = new GameObject("DiceViewportInteraction");
		var interaction = interactionObject.AddComponent<DiceViewportInteraction>();
		SetField(interaction, "diceCamera", diceCamera);
		SetField(interaction, "diceLayerIndex", diceLayer);

		// =============================================================
		// 2D UI
		// =============================================================

		var mainCameraObject = new GameObject("MainCamera");
		mainCameraObject.tag = "MainCamera";
		var mainCamera = mainCameraObject.AddComponent<Camera>();
		mainCamera.clearFlags = CameraClearFlags.SolidColor;
		mainCamera.backgroundColor = new Color(0.05f, 0.05f, 0.10f, 1f);
		mainCamera.cullingMask = ~(1 << diceLayer);
		mainCamera.orthographic = true;
		mainCamera.orthographicSize = 5f;
		mainCamera.depth = -1;
		mainCameraObject.transform.position = new Vector3(0f, 0f, -10f);
		mainCameraObject.AddComponent<AudioListener>();

		var eventSystem = new GameObject("EventSystem");
		eventSystem.AddComponent<EventSystem>();
		eventSystem.AddComponent<InputSystemUIInputModule>();

		var canvasObject = new GameObject("Canvas");
		var canvas = canvasObject.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		var scaler = canvasObject.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		canvasObject.AddComponent<GraphicRaycaster>();

		// 전체 배경
		Stretch(MakePanel(canvasObject.transform, "Background", new Color(0.06f, 0.07f, 0.13f, 1f)));

		// =============================================================
		// 중앙 상단 — 적 (y: 0.50 ~ 0.95)
		// =============================================================

		// 적 네모
		var enemyBox = MakePanel(canvasObject.transform, "EnemyBox",
			new Color(0.20f, 0.10f, 0.10f, 1f));
		Anchor(enemyBox, 0.35f, 0.55f, 0.65f, 0.85f);
		var enemyLabel = MakeTMP(enemyBox.transform, "EnemyLabel", "적", 60, FontStyles.Bold,
			new Color(0.9f, 0.3f, 0.3f));
		Stretch(enemyLabel);
		enemyLabel.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

		// 체력바 배경
		var hpBarBackground = MakePanel(canvasObject.transform, "HpBarBg",
			new Color(0.15f, 0.15f, 0.15f, 1f));
		Anchor(hpBarBackground, 0.30f, 0.87f, 0.70f, 0.90f);

		// 체력바 채우기
		var hpFillObject = new GameObject("HpFill");
		hpFillObject.AddComponent<RectTransform>().SetParent(hpBarBackground.transform, false);
		Stretch(hpFillObject);
		var hpFill = hpFillObject.AddComponent<Image>();
		hpFill.color = new Color(0.85f, 0.15f, 0.15f, 1f);
		hpFill.type = Image.Type.Filled;
		hpFill.fillMethod = Image.FillMethod.Horizontal;
		hpFill.fillAmount = 1f;

		// 체력 텍스트
		var hpText = MakeTMP(canvasObject.transform, "HpText", "999 / 999", 36, FontStyles.Bold,
			Color.white);
		Anchor(hpText, 0.30f, 0.91f, 0.70f, 0.95f);
		hpText.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

		// 데미지 텍스트 스폰 영역
		var damageSpawn = new GameObject("DamageSpawnArea");
		damageSpawn.AddComponent<RectTransform>().SetParent(canvasObject.transform, false);
		Anchor(damageSpawn, 0.35f, 0.70f, 0.65f, 0.80f);

		// EnemyDisplay 컴포넌트
		var enemyObject = new GameObject("EnemyDisplay");
		var enemy = enemyObject.AddComponent<EnemyDisplay>();
		SetField(enemy, "hpFill", hpFill);
		SetField(enemy, "hpText", hpText.GetComponent<TMP_Text>());
		SetField(enemy, "damageSpawnArea", damageSpawn.GetComponent<RectTransform>());
		SetField(enemy, "shakeTarget", enemyBox.GetComponent<RectTransform>());
		SetField(enemy, "maxHp", 999);
		var damageFont = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		if (damageFont != null)
			SetField(enemy, "damageFont", damageFont);

		// =============================================================
		// 중앙 하단 — 주사위 3D 뷰포트 (화면의 ~1/4, 패딩 포함)
		// =============================================================

		// 뷰포트 컨테이너 (16:9 화면 기준 가로 6/16, 세로 3.375/9 = 37.5% × 37.5%)
		// AspectRatioFitter 는 부모 RectTransform 기준이므로 컨테이너로 크기 제한
		var viewportContainer = new GameObject("DiceViewportContainer");
		viewportContainer.AddComponent<RectTransform>().SetParent(canvasObject.transform, false);
		Anchor(viewportContainer, 0.3125f, 0.02f, 0.6875f, 0.395f);

		// 실제 뷰포트 (컨테이너 내부에 Stretch + AspectRatioFitter)
		var viewport = new GameObject("DiceViewport");
		viewport.AddComponent<RectTransform>().SetParent(viewportContainer.transform, false);
		Stretch(viewport);
		var rawImage = viewport.AddComponent<RawImage>();
		rawImage.texture = renderTexture;
		var aspectFitter = viewport.AddComponent<AspectRatioFitter>();
		aspectFitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		aspectFitter.aspectRatio = 960f / 540f;

		SetField(interaction, "viewport", rawImage);

		// 데미지 미리보기 (뷰포트 바로 위)
		var damagePreview = MakeTMP(canvasObject.transform, "DamagePreview", "", 52, FontStyles.Bold,
			new Color(1f, 0.90f, 0.28f));
		Anchor(damagePreview, 0.3125f, 0.40f, 0.6875f, 0.45f);
		damagePreview.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

		// 남은 굴리기 (뷰포트 좌측)
		var rollsText = MakeTMP(canvasObject.transform, "RollsText",
			"남은 굴리기  3 / 3", 32, FontStyles.Normal, new Color(0.80f, 0.83f, 1f));
		Anchor(rollsText, 0.05f, 0.15f, 0.30f, 0.20f);
		rollsText.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Center;

		// 굴리기 버튼 (뷰포트 좌측)
		var rollButton = MakeButton(canvasObject.transform, "RollButton", "굴리기!");
		Anchor(rollButton, 0.05f, 0.05f, 0.30f, 0.14f);

		// 확정 버튼 (뷰포트 우측)
		var confirmButton = MakeButton(canvasObject.transform, "ConfirmButton", "확정");
		Anchor(confirmButton, 0.70f, 0.05f, 0.95f, 0.14f);
		confirmButton.SetActive(false);

		// 다음 롤 버튼 (확정과 같은 위치)
		var nextRollButton = MakeButton(canvasObject.transform, "NextRollButton", "다음 롤");
		Anchor(nextRollButton, 0.70f, 0.05f, 0.95f, 0.14f);
		nextRollButton.SetActive(false);

		// ── YachtGameManager ─────────────────────────────────────────
		var managerObject = new GameObject("YachtGameManager");
		var manager = managerObject.AddComponent<YachtGameManager>();

		SetField(manager, "dice", diceComponents);
		SetField(manager, "viewportInteraction", interaction);
		SetField(manager, "rollButton", rollButton.GetComponent<Button>());
		SetField(manager, "confirmButton", confirmButton.GetComponent<Button>());
		SetField(manager, "nextRollButton", nextRollButton.GetComponent<Button>());
		SetField(manager, "rollsRemainingText", rollsText.GetComponent<TMP_Text>());
		SetField(manager, "damagePreviewText", damagePreview.GetComponent<TMP_Text>());
		SetField(manager, "enemyDisplay", enemy);

		// ── 씬 저장 ───────────────────────────────────────────────────
		Directory.CreateDirectory("Assets/Scenes");
		EditorSceneManager.SaveScene(scene, ScenePath);
		AddToBuildSettings(ScenePath);

		EditorUtility.DisplayDialog("완료",
			$"DiceTest 씬이 {ScenePath} 에 생성되었습니다.\n\n" +
			"▶ Play 후 '굴리기!' 버튼 클릭\n" +
			"  상반부: 적 (HP 999) + 체력바\n" +
			"  하반부: 주사위 3D 뷰포트 + 콤보/데미지\n" +
			"  확정 시 적에게 데미지 적용", "확인");
	}

	// =================================================================
	// 헬퍼
	// =================================================================

	private static void MakeBox(string name, int layer, PhysicsMaterial material,
		Vector3 position, Vector3 size, bool visible = false)
	{
		GameObject box;
		if (visible)
		{
			box = GameObject.CreatePrimitive(PrimitiveType.Cube);
			box.name = name;
			Object.DestroyImmediate(box.GetComponent<BoxCollider>());
			box.transform.localScale = size;
		}
		else
		{
			box = new GameObject(name);
		}
		box.layer = layer;
		var collider = box.AddComponent<BoxCollider>();
		collider.size = size;
		collider.sharedMaterial = material;
		box.transform.position = position;
	}

	private static void SetLayerAll(GameObject target, int layer)
	{
		target.layer = layer;
		foreach (Transform child in target.transform)
			SetLayerAll(child.gameObject, layer);
	}

	private static int EnsureLayer(string layerName)
	{
		var tagManager = new SerializedObject(
			AssetDatabase.LoadAssetAtPath<Object>("ProjectSettings/TagManager.asset"));
		var layers = tagManager.FindProperty("layers");

		for (int i = 8; i < 32; i++)
		{
			var property = layers.GetArrayElementAtIndex(i);
			if (property.stringValue == layerName)
				return i;
		}
		for (int i = 8; i < 32; i++)
		{
			var property = layers.GetArrayElementAtIndex(i);
			if (string.IsNullOrEmpty(property.stringValue))
			{
				property.stringValue = layerName;
				tagManager.ApplyModifiedProperties();
				return i;
			}
		}
		Debug.LogWarning($"[DiceTestSceneBuilder] 레이어 '{layerName}' 등록 실패");
		return 0;
	}

	private static GameObject MakePanel(Transform parent, string name, Color color)
	{
		var panel = new GameObject(name);
		panel.AddComponent<RectTransform>().SetParent(parent, false);
		panel.AddComponent<Image>().color = color;
		return panel;
	}

	private static void Stretch(GameObject target)
	{
		var rect = target.GetComponent<RectTransform>();
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.one;
		rect.offsetMin = rect.offsetMax = Vector2.zero;
	}

	private static void Anchor(GameObject target, float x0, float y0, float x1, float y1)
	{
		var rect = target.GetComponent<RectTransform>();
		rect.anchorMin = new Vector2(x0, y0);
		rect.anchorMax = new Vector2(x1, y1);
		rect.offsetMin = rect.offsetMax = Vector2.zero;
	}

	private static GameObject MakeTMP(Transform parent, string name, string text,
		float size, FontStyles style, Color color)
	{
		var textObject = new GameObject(name);
		textObject.AddComponent<RectTransform>().SetParent(parent, false);
		var textMesh = textObject.AddComponent<TextMeshProUGUI>();
		textMesh.text = text;
		textMesh.fontSize = size;
		textMesh.fontStyle = style;
		textMesh.color = color;
		textMesh.enableWordWrapping = false;
		var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);
		if (font != null)
			textMesh.font = font;
		return textObject;
	}

	private static GameObject MakeButton(Transform parent, string name, string label)
	{
		var buttonObject = new GameObject(name);
		buttonObject.AddComponent<RectTransform>().SetParent(parent, false);
		var image = buttonObject.AddComponent<Image>();
		image.color = new Color(0.15f, 0.18f, 0.35f, 0.9f);
		var button = buttonObject.AddComponent<Button>();
		var colors = button.colors;
		colors.normalColor = new Color(0.15f, 0.18f, 0.35f, 0.9f);
		colors.highlightedColor = new Color(0.28f, 0.35f, 0.70f, 1f);
		colors.pressedColor = new Color(0.10f, 0.12f, 0.25f, 1f);
		button.colors = colors;
		button.targetGraphic = image;
		var labelObject = MakeTMP(buttonObject.transform, "Label", label, 40, FontStyles.Bold, Color.white);
		Stretch(labelObject);
		labelObject.GetComponent<TMP_Text>().alignment = TextAlignmentOptions.Midline;
		return buttonObject;
	}

	private static void SetField(object target, string field, object value)
	{
		target.GetType()
			.GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)
			?.SetValue(target, value);
	}

	private static void AddToBuildSettings(string path)
	{
		foreach (var existingScene in EditorBuildSettings.scenes)
			if (existingScene.path == path)
				return;
		var scenes = EditorBuildSettings.scenes;
		var next = new EditorBuildSettingsScene[scenes.Length + 1];
		for (int i = 0; i < scenes.Length; i++)
			next[i] = scenes[i];
		next[scenes.Length] = new EditorBuildSettingsScene(path, true);
		EditorBuildSettings.scenes = next;
	}
}
