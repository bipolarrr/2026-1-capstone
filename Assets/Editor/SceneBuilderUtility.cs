using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// 모든 씬 빌더에서 공유하는 UI/유틸리티 헬퍼.
/// </summary>
public static class SceneBuilderUtility
{
	public enum BuildSettingsPlacement
	{
		Append,
		InsertFirst,
	}

	public const string FontPath = "Assets/TextMesh Pro/Fonts/Mona12.asset";
	public const string PlayerSpriteRoot = "Assets/Player/Sprites";
	public const string PlayerIdleSpriteFolder = PlayerSpriteRoot + "/Idle";
	public const string PlayerIdleSpritePath = PlayerIdleSpriteFolder + "/0.png";
	public const string PlayerLowHpSpriteFolder = PlayerSpriteRoot + "/LowHp";
	public const string PlayerJumpSpriteFolder = PlayerSpriteRoot + "/Jump";
	public const string PlayerJumpBelowSpriteFolder = PlayerSpriteRoot + "/JumpBelow";
	public const string PlayerDefenseSpriteFolder = PlayerSpriteRoot + "/Defense";
	public const string PlayerSmallHitSpriteFolder = PlayerSpriteRoot + "/SmallHit";
	public const string PlayerStrongHitSpriteFolder = PlayerSpriteRoot + "/StrongHit";
	public const string PlayerDebuffSpriteFolder = PlayerSpriteRoot + "/Debuff";
	public const string PlayerDieSpriteFolder = PlayerSpriteRoot + "/Die/Player_Die_1000x1000";
	public const string PlayerDiceRollSpriteFolder = PlayerSpriteRoot + "/DiceRoll";
	public const string PlayerAttack01SpriteFolder = PlayerSpriteRoot + "/Attack";
	public const string PlayerWeaponSpritePath = PlayerSpriteRoot + "/Weapon/Player_Weapon.png";
	public const string UiHeartSpritePath = "Assets/UI/UI_Heart.png";

	// ── 공유 버튼 색상 ──
	public static readonly Color ButtonNormal    = new Color(0.15f, 0.18f, 0.35f, 0.9f);
	public static readonly Color ButtonHighlight = new Color(0.28f, 0.35f, 0.70f, 1f);
	public static readonly Color ButtonPressed   = new Color(0.10f, 0.12f, 0.25f, 1f);

	// ── BattleScene sprite animation speed tuning ──
	public const float BattlePlayerActionFrameRate = 30f;
	public const float BattlePlayerIdleFrameRate = 24f;
	public const float BattleEnemyIdleFrameRate = 12f;
	public const float BattleEnemyActionFrameRate = 12f;
	public const float BattleEnemyDeathFrameRate = 30f;
	public const float BattlePlayerSpriteScale = 1.4f;
	public const float BattlePlayerJumpBelowScale = BattlePlayerSpriteScale;
	public const float BattlePlayerJumpDuration = 0.3f;
	public const int BattlePlayerLowHpFrameCount = 95;
	public const int BattlePlayerLowHpIntroEndFrame = 94;
	public const int BattlePlayerLowHpLoopStartFrame = 41;
	public const int BattlePlayerLowHpLoopEndFrame = 94;
	public const int BattlePlayerStrongHitFrameCount = 47;
	public const int BattlePlayerAttackFrameCount = 145;
	public const int BattlePlayerDeathFrameCount = 145;
	public static readonly Vector2 HeartSlotSize = new Vector2(88f, 80f);
	public const float HeartSlotSpacing = 12f;
	static Sprite whitePixelSprite;
	static readonly List<string> fieldBindingFailures = new List<string>();
	static string fieldBindingContext;

	public static void BeginSceneBuildValidation(string context)
	{
		fieldBindingContext = context;
		fieldBindingFailures.Clear();
	}

	public static int FieldBindingFailureCount => fieldBindingFailures.Count;

	public static void SetField(object target, string fieldName, object value)
	{
		if (target == null)
		{
			RecordFieldBindingFailure($"[SceneBuilderUtility] Field '{fieldName}' not set because target is null");
			return;
		}
		// 베이스 클래스에 선언된 protected 필드도 찾도록 타입 체인을 위로 탐색.
		var t = target.GetType();
		while (t != null)
		{
			var field = t.GetField(fieldName,
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
				BindingFlags.DeclaredOnly);
			if (field != null)
			{
				try
				{
					field.SetValue(target, value);
				}
				catch (System.Exception ex)
				{
					RecordFieldBindingFailure($"[SceneBuilderUtility] Field '{fieldName}' on {target.GetType().Name} rejected value: {ex.Message}");
				}
				return;
			}
			t = t.BaseType;
		}
		RecordFieldBindingFailure($"[SceneBuilderUtility] Field '{fieldName}' not found on {target.GetType().Name}");
	}

	static void RecordFieldBindingFailure(string message)
	{
		fieldBindingFailures.Add(message);
		Debug.LogWarning(message);
	}

	public static void ReportSceneBuildValidation()
	{
		if (fieldBindingFailures.Count == 0)
			return;

		var sb = new System.Text.StringBuilder();
		string context = string.IsNullOrEmpty(fieldBindingContext) ? "SceneBuilder" : fieldBindingContext;
		sb.AppendLine($"[{context}] SetField wiring warnings: {fieldBindingFailures.Count}");
		for (int i = 0; i < fieldBindingFailures.Count; i++)
			sb.AppendLine($"- {fieldBindingFailures[i]}");
		Debug.LogWarning(sb.ToString().TrimEnd());
	}

	public static RectTransform CreateImage(Transform parent, string name, Color color, bool raycastTarget = false)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var img = go.AddComponent<Image>();
		img.color = color;
		img.raycastTarget = raycastTarget;
		return go.GetComponent<RectTransform>();
	}

	public static RectTransform CreateEmpty(Transform parent, string name)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		go.AddComponent<RectTransform>();
		return go.GetComponent<RectTransform>();
	}

	public static void AnchorBox(RectTransform rt, float xMin, float yMin, float xMax, float yMax)
	{
		rt.anchorMin = new Vector2(xMin, yMin);
		rt.anchorMax = new Vector2(xMax, yMax);
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	public static void SetButtonColorSet(Button btn, Color normal, Color highlight, Color pressed)
	{
		var cb = btn.colors;
		cb.normalColor = normal;
		cb.highlightedColor = highlight;
		cb.pressedColor = pressed;
		cb.selectedColor = highlight;
		btn.colors = cb;
	}

	public static GameObject CreateAnchoredButton(Transform parent, string name, string label,
		Vector2 anchorMin, Vector2 anchorMax, Color bgColor,
		Color? highlightColor = null, Color? pressedColor = null, float labelSize = 28f)
	{
		var highlight = highlightColor ?? ButtonHighlight;
		var pressed = pressedColor ?? ButtonPressed;
		var go = CreateButton(parent, name, label, labelSize, bgColor, highlight, pressed);
		var rt = go.GetComponent<RectTransform>();
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		return go;
	}

	public static HeartDisplay BuildHeartDisplay(Transform parent, string name,
		Vector2 anchorMin, Vector2 anchorMax)
	{
		var root = CreateEmpty(parent, name);
		root.anchorMin = anchorMin;
		root.anchorMax = anchorMax;
		root.offsetMin = Vector2.zero;
		root.offsetMax = Vector2.zero;

		var layout = root.gameObject.AddComponent<HorizontalLayoutGroup>();
		layout.childAlignment = TextAnchor.MiddleLeft;
		layout.childControlWidth = false;
		layout.childControlHeight = false;
		layout.childForceExpandWidth = false;
		layout.childForceExpandHeight = false;
		layout.spacing = HeartSlotSpacing;

		var display = root.gameObject.AddComponent<HeartDisplay>();
		var sprites = LoadUiHeartSprites();
		SetField(display, "slotRoot", root);
		SetField(display, "heartImages", CreateHeartSlotImages(root, 5));
		SetField(display, "emptyHeartSprite", sprites.empty);
		SetField(display, "halfHeartSprite", sprites.half);
		SetField(display, "fullHeartSprite", sprites.full);
		SetField(display, "slotSize", HeartSlotSize);
		return display;
	}

	static Image[] CreateHeartSlotImages(RectTransform root, int count)
	{
		var images = new Image[count];
		for (int i = 0; i < count; i++)
		{
			var slot = CreateImage(root, $"HeartSlot_{i + 1}", Color.white);
			slot.sizeDelta = HeartSlotSize;
			var image = slot.GetComponent<Image>();
			image.preserveAspect = true;
			image.raycastTarget = false;
			images[i] = image;
		}
		return images;
	}

	static (Sprite empty, Sprite half, Sprite full) LoadUiHeartSprites()
	{
		var sprites = AssetDatabase.LoadAllAssetRepresentationsAtPath(UiHeartSpritePath);
		Sprite empty = null;
		Sprite half = null;
		Sprite full = null;
		foreach (var asset in sprites)
		{
			if (asset is Sprite sprite)
			{
				if (sprite.name == "UI_Heart_0") empty = sprite;
				else if (sprite.name == "UI_Heart_1") half = sprite;
				else if (sprite.name == "UI_Heart_2") full = sprite;
			}
		}

		if (empty == null || half == null || full == null)
			Debug.LogWarning($"[SceneBuilderUtility] UI 하트 스프라이트 로드 실패: {UiHeartSpritePath}");

		return (empty, half, full);
	}

	public static TMP_Text CreateTMPText(Transform parent, string name, string text,
		float fontSize, Color color, TextAlignmentOptions alignment = TextAlignmentOptions.Center,
		FontStyles style = FontStyles.Normal)
	{
		var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(FontPath);

		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var tmp = go.AddComponent<TextMeshProUGUI>();
		if (font != null)
			tmp.font = font;
		tmp.text = text;
		tmp.fontSize = fontSize;
		tmp.fontStyle = style;
		tmp.color = color;
		tmp.alignment = alignment;
		tmp.raycastTarget = false;
		tmp.textWrappingMode = TextWrappingModes.NoWrap;
		return tmp;
	}

	public static GameObject CreateButton(Transform parent, string name, string label,
		float labelSize, Color bgColor, Color highlightColor, Color pressedColor)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var img = go.AddComponent<Image>();
		img.color = bgColor;

		var btn = go.AddComponent<Button>();
		btn.targetGraphic = img;
		var cb = btn.colors;
		cb.normalColor = bgColor;
		cb.highlightedColor = highlightColor;
		cb.pressedColor = pressedColor;
		cb.selectedColor = highlightColor;
		btn.colors = cb;

		var txt = CreateTMPText(go.transform, "Label", label,
			labelSize, new Color(0.92f, 0.92f, 1f, 1f),
			TextAlignmentOptions.Center, FontStyles.Bold);
		Stretch(txt.GetComponent<RectTransform>());

		return go;
	}

	public static void Stretch(RectTransform rt)
	{
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	public static void CenterPopup(GameObject target, float width, float height)
	{
		var rt = target.GetComponent<RectTransform>();
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(width, height);
		rt.anchoredPosition = Vector2.zero;
	}

	public static void EnsureDirectory(string path)
	{
		if (!AssetDatabase.IsValidFolder(path))
		{
			string parent = System.IO.Path.GetDirectoryName(path).Replace("\\", "/");
			string folder = System.IO.Path.GetFileName(path);
			AssetDatabase.CreateFolder(parent, folder);
		}
	}

	/// <summary>
	/// 모든 무기 배틀 씬의 적 타겟 마커 테두리(4장)를 공통 규약 이름으로 생성.
	/// 런타임 BattleControllerBase.ApplyBorderThickness가 이 이름("BorderTop/Bottom/Left/Right")을
	/// Find해서 MobDef.borderThickness에 맞게 앵커를 재계산한다.
	/// </summary>
	public static void MakeEnemyTargetBorders(RectTransform marker, float thickness, Color color)
	{
		MakeBorderChild(marker, "BorderTop",    new Vector2(0f, 1f - thickness), Vector2.one, color);
		MakeBorderChild(marker, "BorderBottom", Vector2.zero,                    new Vector2(1f, thickness), color);
		MakeBorderChild(marker, "BorderLeft",   new Vector2(0f, thickness),       new Vector2(thickness, 1f - thickness), color);
		MakeBorderChild(marker, "BorderRight",  new Vector2(1f - thickness, thickness), new Vector2(1f, 1f - thickness), color);
	}

	public static Image CreateEnemyProjectileImage(Transform parent, string name, string spritePath)
	{
		if (!string.IsNullOrEmpty(spritePath))
			EnsureTightSprite(spritePath);

		var rt = CreateImage(parent, name, Color.white);
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(132f, 32f);

		var img = rt.GetComponent<Image>();
		img.sprite = !string.IsNullOrEmpty(spritePath)
			? AssetDatabase.LoadAssetAtPath<Sprite>(spritePath)
			: null;
		img.preserveAspect = true;
		img.useSpriteMesh = true;
		img.raycastTarget = false;
		rt.gameObject.SetActive(false);
		rt.SetAsLastSibling();
		return img;
	}

	public static EnemyProjectileAttachmentFollower AddEnemyProjectileFollower(
		Image projectileImage,
		EnemySpriteAnimator animator)
	{
		if (projectileImage == null)
			return null;

		var follower = projectileImage.GetComponent<EnemyProjectileAttachmentFollower>();
		if (follower == null)
			follower = projectileImage.gameObject.AddComponent<EnemyProjectileAttachmentFollower>();

		SetField(follower, "projectileImage", projectileImage);
		SetField(follower, "animator", animator);
		SetField(follower, "size", new Vector2(132f, 32f));
		SetField(follower, "normalizedX", AnimationCurve.EaseInOut(0f, 0.24f, 1f, 0.37f));
		SetField(follower, "normalizedY", AnimationCurve.EaseInOut(0f, 0.57f, 1f, 0.50f));
		SetField(follower, "rotation", AnimationCurve.EaseInOut(0f, -5f, 1f, 7f));
		SetField(follower, "scaleX", AnimationCurve.EaseInOut(0f, 0.82f, 1f, 1.08f));
		SetField(follower, "releasePointOnArrow", 0.12f);
		return follower;
	}

	public static Image CreatePlayerWeaponProjectileImage(Transform parent, string name = "PlayerWeaponProjectile")
	{
		EnsureTightSprite(PlayerWeaponSpritePath);

		var rt = CreateImage(parent, name, Color.white);
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(84f, 84f);

		var image = rt.GetComponent<Image>();
		image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PlayerWeaponSpritePath);
		image.preserveAspect = true;
		image.raycastTarget = false;
		rt.gameObject.SetActive(false);
		rt.SetAsLastSibling();
		return image;
	}

	public static PlayerAttackAnimator AddPlayerAttackAnimator(GameObject root,
		Image playerBody,
		PlayerBodyAnimator bodyAnimator,
		Image weaponProjectile,
		float frameRate = 22.5f,
		int frameStep = 2)
	{
		if (root == null)
			return null;

		var attackAnimator = root.AddComponent<PlayerAttackAnimator>();
		SetField(attackAnimator, "playerBody", playerBody);
		SetField(attackAnimator, "weaponProjectile", weaponProjectile);
		SetField(attackAnimator, "bodyAnimator", bodyAnimator);
		SetField(attackAnimator, "frameRate", frameRate);
		SetField(attackAnimator, "frameStep", frameStep);
		SetField(attackAnimator, "attackBodyScaleMultiplier", 0.75f);
		SetField(attackAnimator, "weaponVisibleRatio", 0.50f);
		SetField(attackAnimator, "weaponLaunchRatio", 0.60f);
		SetField(attackAnimator, "projectileEndRatio", 0.84f);
		SetField(attackAnimator, "impactNormalizedTime", 0.80f);
		SetField(attackAnimator, "handAttachStartOffset", new Vector2(70f, 155f));
		SetField(attackAnimator, "handAttachEndOffset", new Vector2(120f, 170f));
		SetField(attackAnimator, "handAttachNormalizedTimes", new[] { 0.507f, 0.534f, 0.562f, 0.589f });
		SetField(attackAnimator, "handAttachOffsets", new[]
		{
			new Vector2(50f, 94f),
			new Vector2(20f, 84f),
			new Vector2(-32f, 110f),
			new Vector2(-28f, 132f)
		});
		SetField(attackAnimator, "projectileTargetOffset", new Vector2(0f, 95f));

		Sprite[] attackSprites = LoadNumberedPixelSprites(
			PlayerAttack01SpriteFolder, "", "png", BattlePlayerAttackFrameCount);
		SetField(attackAnimator, "attackSprites", attackSprites);
		if (bodyAnimator != null)
			SetField(bodyAnimator, "attackDisplaySprites", attackSprites);

		return attackAnimator;
	}

	static void MakeBorderChild(RectTransform parent, string name, Vector2 min, Vector2 max, Color color)
	{
		var b = CreateImage(parent, name, color);
		b.anchorMin = min; b.anchorMax = max;
		b.offsetMin = Vector2.zero; b.offsetMax = Vector2.zero;
	}

	/// <summary>1x1 흰색 픽셀 스프라이트 — Filled Image용 (둥근 모서리 없음)</summary>
	public static Sprite WhitePixelSprite()
	{
		if (whitePixelSprite != null)
			return whitePixelSprite;

		var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		tex.name = "SceneBuilderUtility_WhitePixel";
		tex.SetPixel(0, 0, Color.white);
		tex.Apply();
		whitePixelSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
		whitePixelSprite.name = "SceneBuilderUtility_WhitePixelSprite";
		return whitePixelSprite;
	}

	// ── 공통 전투 로그 스크롤 빌더 ──

	public struct ScrollableLogOptions
	{
		public float fontSize;
		public Color textColor;
		public Color viewportColor;
		public TextAlignmentOptions alignment;
		public RectOffset textPadding;

		public static ScrollableLogOptions Default => new ScrollableLogOptions
		{
			fontSize = 16f,
			textColor = new Color(0.85f, 0.95f, 0.9f),
			viewportColor = new Color(0, 0, 0, 0),
			alignment = TextAlignmentOptions.TopLeft,
			textPadding = new RectOffset(8, 8, 8, 8),
		};
	}

	public struct ScrollableLogHandles
	{
		public BattleLog log;
		public ScrollRect scrollRect;
		public RectTransform viewport;
		public RectTransform content;
		public TMP_Text logText;
	}

	public struct BattleBottomFocusHandles
	{
		public BattleBottomFocusController focus;
		public BattleLog log;
		public CanvasGroup inputGroup;
		public RectTransform messagePanel;
		public RectTransform historyPanel;
		public Button logButton;
	}

	public struct SceneShellHandles
	{
		public Camera camera;
		public Canvas canvas;
		public RectTransform canvasRoot;
		public GameObject eventSystem;
	}

	public struct BattleRootHandles<TController> where TController : BattleControllerBase
	{
		public GameObject root;
		public TController controller;
		public BattleDamageVFX vfx;
		public BattleAnimations animations;
	}

	public struct StageBackgroundHandles
	{
		public RectTransform mask;
		public RectTransform imageRoot;
		public Image image;
	}

	public struct BattlePlayerRigHandles
	{
		public RectTransform bodyRoot;
		public Image bodyImage;
		public PlayerBodyAnimator bodyAnimator;
		public RectTransform jumpBelowRoot;
		public Image jumpBelowImage;
		public Sprite[] jumpBelowSprites;
	}

	public struct EnemySlotStripHandles
	{
		public RectTransform root;
		public RectTransform[] slotRoots;
		public GameObject[] panels;
		public Image[] bodies;
		public Image[] idleProjectiles;
		public EnemySpriteAnimator[] animators;
		public TMP_Text[] names;
		public Image[] hpFills;
		public TMP_Text[] hpTexts;
		public Image[] targetMarkers;
		public TMP_Text[] deadOverlays;
		public Button[] buttons;
	}

	public static SceneShellHandles BuildSceneShell(string cameraName, Color backgroundColor,
		bool includeEventSystem = true, int canvasSortingOrder = 0, float orthographicSize = 5f,
		Vector3? cameraPosition = null, bool includeAudioListener = false)
	{
		var cameraGO = new GameObject(cameraName);
		cameraGO.tag = "MainCamera";
		var cam = cameraGO.AddComponent<Camera>();
		cam.orthographic = true;
		cam.orthographicSize = orthographicSize;
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = backgroundColor;
		cameraGO.transform.position = cameraPosition ?? Vector3.zero;
		if (includeAudioListener)
			cameraGO.AddComponent<AudioListener>();

		var canvasGO = new GameObject("Canvas");
		var canvas = canvasGO.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;
		canvas.sortingOrder = canvasSortingOrder;
		var scaler = canvasGO.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;
		canvasGO.AddComponent<GraphicRaycaster>();

		GameObject eventSystem = null;
		if (includeEventSystem)
			eventSystem = BuildEventSystem();

		return new SceneShellHandles
		{
			camera = cam,
			canvas = canvas,
			canvasRoot = canvasGO.GetComponent<RectTransform>(),
			eventSystem = eventSystem,
		};
	}

	public static GameObject BuildEventSystem()
	{
		var es = new GameObject("EventSystem");
		es.AddComponent<UnityEngine.EventSystems.EventSystem>();
		es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
		return es;
	}

	public static StageBackgroundHandles BuildStageBackground(Transform canvasRoot,
		string maskName, string imageName, Color fallbackColor,
		float visibleBottom = 1f / 3f)
	{
		var mask = CreateEmpty(canvasRoot, maskName);
		AnchorBox(mask, 0f, visibleBottom, 1f, 1f);
		mask.gameObject.AddComponent<RectMask2D>();

		var defaultStage = StageRegistry.DefaultStage;
		var defaultBundle = defaultStage != null ? BuildStageBundle(defaultStage) : null;
		Sprite sprite = defaultBundle != null ? defaultBundle.background : null;
		float aspect = 16f / 9f;
		if (sprite != null && sprite.texture != null)
			aspect = (float)sprite.texture.width / sprite.texture.height;
		float height = 1920f / aspect;

		var imageRoot = CreateImage(mask, imageName, Color.white);
		imageRoot.anchorMin = new Vector2(0f, 0f);
		imageRoot.anchorMax = new Vector2(1f, 0f);
		imageRoot.pivot = new Vector2(0.5f, 0f);
		imageRoot.offsetMin = new Vector2(0f, 0f);
		imageRoot.offsetMax = new Vector2(0f, height);
		var image = imageRoot.GetComponent<Image>();
		if (sprite != null)
			image.sprite = sprite;
		else if (defaultStage != null)
			image.color = defaultStage.themeColor;
		else
			image.color = fallbackColor;

		return new StageBackgroundHandles
		{
			mask = mask,
			imageRoot = imageRoot,
			image = image,
		};
	}

	public static BattlePlayerRigHandles BuildBattlePlayerRig(Transform parent, float groundY,
		bool includeJumpBelow)
	{
		var idleSprites = LoadAvailablePixelSpriteFrames(PlayerIdleSpriteFolder);
		var lowHpSprites = LoadNumberedPixelSprites(PlayerLowHpSpriteFolder, "", "png", BattlePlayerLowHpFrameCount);
		var jumpSprites = LoadAvailablePixelSpriteFrames(PlayerJumpSpriteFolder);
		var defenseSprites = LoadAvailablePixelSpriteFrames(PlayerDefenseSpriteFolder);
		var smallHitSprites = LoadAvailablePixelSpriteFrames(PlayerSmallHitSpriteFolder);
		var strongHitSprites = LoadNumberedPixelSprites(PlayerStrongHitSpriteFolder, "", "png", BattlePlayerStrongHitFrameCount);
		var debuffSprites = LoadNumberedPixelSprites(PlayerDebuffSpriteFolder, "", "png", 156);
		var attackDisplaySprites = LoadNumberedPixelSprites(
			PlayerAttack01SpriteFolder, "", "png", BattlePlayerAttackFrameCount);
		var deathDisplaySprites = LoadNumberedPixelSprites(PlayerDieSpriteFolder, "", "png", BattlePlayerDeathFrameCount);
		var idleSprite = idleSprites != null && idleSprites.Length > 0 ? idleSprites[0] : null;

		RectTransform jumpBelowRoot = null;
		Image jumpBelowImage = null;
		Sprite[] jumpBelowSprites = null;
		if (includeJumpBelow)
		{
			jumpBelowSprites = LoadNumberedPixelSprites(PlayerJumpBelowSpriteFolder, "", "png", 145);
			jumpBelowRoot = CreateBattlePlayerImage(parent, "PlayerJumpBelow", groundY);
			jumpBelowRoot.localScale = new Vector3(BattlePlayerJumpBelowScale, BattlePlayerJumpBelowScale, 1f);
			jumpBelowImage = jumpBelowRoot.GetComponent<Image>();
			jumpBelowImage.enabled = false;
		}

		var bodyRoot = CreateBattlePlayerImage(parent, "PlayerBody", groundY);
		bodyRoot.localScale = new Vector3(BattlePlayerSpriteScale, BattlePlayerSpriteScale, 1f);
		var bodyImage = bodyRoot.GetComponent<Image>();
		if (idleSprite != null)
			bodyImage.sprite = idleSprite;

		var bodyAnimator = bodyRoot.gameObject.AddComponent<PlayerBodyAnimator>();
		SetField(bodyAnimator, "playerBody", bodyImage);
		SetField(bodyAnimator, "frameRate", BattlePlayerActionFrameRate);
		SetField(bodyAnimator, "idleFrameRate", BattlePlayerIdleFrameRate);
		SetField(bodyAnimator, "idleSprites", idleSprites);
		SetField(bodyAnimator, "lowHpSprites", lowHpSprites);
		SetField(bodyAnimator, "jumpSprites", jumpSprites);
		SetField(bodyAnimator, "defenseSprites", defenseSprites);
		SetField(bodyAnimator, "smallHitSprites", smallHitSprites);
		SetField(bodyAnimator, "strongHitSprites", strongHitSprites);
		SetField(bodyAnimator, "debuffSprites", debuffSprites);
		SetField(bodyAnimator, "attackDisplaySprites", attackDisplaySprites);
		SetField(bodyAnimator, "deathDisplaySprites", deathDisplaySprites);
		SetField(bodyAnimator, "smallHitFrameStep", 1);
		SetField(bodyAnimator, "strongHitFrameStep", 1);
		SetField(bodyAnimator, "lowHpIntroEndFrame", BattlePlayerLowHpIntroEndFrame);
		SetField(bodyAnimator, "lowHpLoopStartFrame", BattlePlayerLowHpLoopStartFrame);
		SetField(bodyAnimator, "lowHpLoopEndFrame", BattlePlayerLowHpLoopEndFrame);

		return new BattlePlayerRigHandles
		{
			bodyRoot = bodyRoot,
			bodyImage = bodyImage,
			bodyAnimator = bodyAnimator,
			jumpBelowRoot = jumpBelowRoot,
			jumpBelowImage = jumpBelowImage,
			jumpBelowSprites = jumpBelowSprites,
		};
	}

	static RectTransform CreateBattlePlayerImage(Transform parent, string name, float groundY)
	{
		var rt = CreateImage(parent, name, Color.white);
		rt.pivot = new Vector2(0.5f, 0f);
		rt.anchorMin = new Vector2(0.19f, groundY);
		rt.anchorMax = new Vector2(0.19f, groundY);
		rt.sizeDelta = new Vector2(234.375f, 234.375f);
		var image = rt.GetComponent<Image>();
		image.preserveAspect = true;
		image.useSpriteMesh = false;
		image.raycastTarget = false;
		return rt;
	}

	public static EnemySlotStripHandles BuildBattleEnemySlots(Transform parent, float groundY,
		Color hpFillColor, Color targetMarkerColor)
	{
		var root = CreateEmpty(parent, "EnemySlotsArea");
		AnchorBox(root, 0.45f, groundY, 0.95f, groundY + 0.35f);

		var handles = new EnemySlotStripHandles
		{
			root = root,
			slotRoots = new RectTransform[4],
			panels = new GameObject[4],
			bodies = new Image[4],
			idleProjectiles = new Image[4],
			animators = new EnemySpriteAnimator[4],
			names = new TMP_Text[4],
			hpFills = new Image[4],
			hpTexts = new TMP_Text[4],
			targetMarkers = new Image[4],
			deadOverlays = new TMP_Text[4],
			buttons = new Button[4],
		};

		for (int i = 0; i < 4; i++)
			BuildBattleEnemySlot(handles, i, hpFillColor, targetMarkerColor);

		return handles;
	}

	public static EnemySlotStripHandles BuildExploreEnemySlots(Transform parent, float groundY,
		Color hpBarBgColor, Color hpFillColor, string projectileSpritePath)
	{
		var root = CreateEmpty(parent, "EnemySlotsArea");
		AnchorBox(root, 0.40f, groundY, 0.90f, groundY + 0.35f);

		var handles = new EnemySlotStripHandles
		{
			root = root,
			slotRoots = new RectTransform[4],
			panels = new GameObject[4],
			bodies = new Image[4],
			idleProjectiles = new Image[4],
			animators = new EnemySpriteAnimator[4],
			names = new TMP_Text[4],
			hpFills = new Image[4],
			hpTexts = new TMP_Text[4],
			targetMarkers = new Image[4],
			deadOverlays = new TMP_Text[4],
			buttons = new Button[4],
		};

		for (int i = 0; i < 4; i++)
			BuildExploreEnemySlot(handles, i, hpBarBgColor, hpFillColor, projectileSpritePath);

		return handles;
	}

	static void BuildExploreEnemySlot(EnemySlotStripHandles handles, int index,
		Color hpBarBgColor, Color hpFillColor, string projectileSpritePath)
	{
		float x0 = index * 0.25f;
		float x1 = x0 + 0.24f;

		var slot = CreateImage(handles.root, $"EnemySlot{index}", new Color(0, 0, 0, 0));
		AnchorBox(slot, x0, 0f, x1, 1f);
		slot.GetComponent<Image>().raycastTarget = false;
		handles.slotRoots[index] = slot;
		handles.panels[index] = slot.gameObject;

		var body = CreateImage(slot, "Body", Color.gray);
		AnchorBox(body, 0.05f, 0f, 0.95f, 0.90f);
		var bodyImg = body.GetComponent<Image>();
		bodyImg.useSpriteMesh = true;
		handles.bodies[index] = bodyImg;

		var idleProjectile = CreateEnemyProjectileImage(body, "IdleProjectile", projectileSpritePath);
		idleProjectile.gameObject.SetActive(false);
		handles.idleProjectiles[index] = idleProjectile;

		var nameContainer = CreateImage(slot, "NameContainer", new Color(0f, 0f, 0f, 0.5f));
		AnchorBox(nameContainer, 0.05f, 0f, 0.95f, 0.12f);
		nameContainer.GetComponent<Image>().raycastTarget = false;
		nameContainer.gameObject.SetActive(false);

		var name = CreateTMPText(nameContainer, "Name", "적", 28, Color.white, TextAlignmentOptions.Center);
		Stretch(name.GetComponent<RectTransform>());
		handles.names[index] = name;

		var hpBg = CreateImage(slot, "HpBarBg", hpBarBgColor);
		AnchorBox(hpBg, 0.10f, 0f, 0.90f, 0.06f);
		hpBg.gameObject.SetActive(false);

		var fill = CreateImage(hpBg, "HpFill", hpFillColor);
		Stretch(fill);
		var fillImg = fill.GetComponent<Image>();
		fillImg.type = Image.Type.Filled;
		fillImg.fillMethod = Image.FillMethod.Horizontal;
		handles.hpFills[index] = fillImg;

		var hpText = CreateTMPText(slot, "HpText", "0 / 0", 22, Color.white, TextAlignmentOptions.Center);
		AnchorBox(hpText.GetComponent<RectTransform>(), 0.05f, 0f, 0.95f, 0.12f);
		hpText.gameObject.SetActive(false);
		handles.hpTexts[index] = hpText;
	}

	static void BuildBattleEnemySlot(EnemySlotStripHandles handles, int index,
		Color hpFillColor, Color targetMarkerColor)
	{
		float x0 = index * 0.25f;
		float x1 = x0 + 0.24f;

		var slot = CreateImage(handles.root, $"EnemySlot{index}", new Color(0, 0, 0, 0), true);
		AnchorBox(slot, x0, 0f, x1, 1f);
		handles.slotRoots[index] = slot;
		handles.panels[index] = slot.gameObject;

		var body = CreateImage(slot, "Body", Color.gray);
		AnchorBox(body, 0.05f, 0f, 0.95f, 0.90f);
		var bodyImg = body.GetComponent<Image>();
		bodyImg.preserveAspect = true;
		bodyImg.useSpriteMesh = true;
		handles.bodies[index] = bodyImg;

		var idleProjectile = CreateEnemyProjectileImage(
			body,
			"IdleProjectile",
			"Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png");
		idleProjectile.gameObject.SetActive(false);
		handles.idleProjectiles[index] = idleProjectile;

		var enemyAnimator = body.gameObject.AddComponent<EnemySpriteAnimator>();
		SetField(enemyAnimator, "targetImage", bodyImg);
		SetField(enemyAnimator, "idleFrameRate", BattleEnemyIdleFrameRate);
		SetField(enemyAnimator, "actionFrameRate", BattleEnemyActionFrameRate);
		SetField(enemyAnimator, "deathFrameRate", BattleEnemyDeathFrameRate);
		AddEnemyProjectileFollower(idleProjectile, enemyAnimator);
		handles.animators[index] = enemyAnimator;

		var marker = CreateImage(slot, "TargetMarker", new Color(0, 0, 0, 0));
		AnchorBox(marker, 0.05f, 0f, 0.95f, 0.90f);
		marker.GetComponent<Image>().raycastTarget = false;
		MakeEnemyTargetBorders(marker, 0.05f, targetMarkerColor);
		handles.targetMarkers[index] = marker.GetComponent<Image>();
		marker.gameObject.SetActive(false);

		var dead = CreateTMPText(slot, "DeadOverlay", "✕", 60,
			new Color(1f, 0.2f, 0.2f, 0.85f), TextAlignmentOptions.Center);
		AnchorBox(dead.GetComponent<RectTransform>(), 0.05f, 0f, 0.95f, 0.90f);
		dead.raycastTarget = false;
		dead.gameObject.SetActive(false);
		handles.deadOverlays[index] = dead;

		var info = CreateImage(slot, "InfoPanel", new Color(0, 0, 0, 0.5f));
		AnchorBox(info, 0f, 0.90f, 1f, 1.08f);
		info.GetComponent<Image>().raycastTarget = false;

		var name = CreateTMPText(info, "Name", "적", 22, Color.white, TextAlignmentOptions.Center);
		name.fontStyle = FontStyles.Bold;
		AnchorBox(name.GetComponent<RectTransform>(), 0f, 0.333f, 1f, 1f);
		handles.names[index] = name;

		var hpBg = CreateImage(info, "HpBarBg", new Color(0.15f, 0.15f, 0.15f));
		AnchorBox(hpBg, 0.1f, 0f, 0.9f, 0.333f);

		var fill = CreateImage(hpBg, "HpFill", hpFillColor);
		Stretch(fill);
		var fillImg = fill.GetComponent<Image>();
		fillImg.sprite = WhitePixelSprite();
		fillImg.type = Image.Type.Filled;
		fillImg.fillMethod = Image.FillMethod.Horizontal;
		handles.hpFills[index] = fillImg;

		var hpText = CreateTMPText(info, "HpText", "0 / 0", 18, Color.white, TextAlignmentOptions.Center);
		AnchorBox(hpText.GetComponent<RectTransform>(), 0.05f, 0f, 0.95f, 0.333f);
		handles.hpTexts[index] = hpText;

		var btn = slot.gameObject.AddComponent<Button>();
		btn.targetGraphic = slot.GetComponent<Image>();
		SetButtonColorSet(btn, new Color(0, 0, 0, 0),
			new Color(1f, 1f, 1f, 0.08f),
			new Color(1f, 1f, 1f, 0.15f));
		handles.buttons[index] = btn;
	}

	public static RectTransform BuildBattleDamageSpawnArea(Transform parent, float groundY)
	{
		var area = CreateEmpty(parent, "DamageSpawnArea");
		AnchorBox(area, 0.40f, groundY + 0.20f, 0.98f, groundY + 0.35f);
		return area;
	}

	public static void BindBattleControllerBase(BattleControllerBase ctrl,
		StageBackgroundHandles background,
		StageSpriteBundle[] stageBundles,
		BattlePlayerRigHandles player,
		EnemySlotStripHandles enemies,
		float enemyDeathGroundY,
		HeartDisplay heartDisplay,
		BattleLog battleLog,
		BattleBottomFocusController bottomFocus,
		BattleDamageVFX vfx,
		BattleAnimations battleAnims)
	{
		SetField(ctrl, "fightBackgroundImage", background.image);
		SetField(ctrl, "stageBundles", stageBundles);
		SetField(ctrl, "playerBody", player.bodyImage);
		SetField(ctrl, "playerBodyAnimator", player.bodyAnimator);
		SetField(ctrl, "enemyPanels", enemies.panels);
		SetField(ctrl, "enemyBodies", enemies.bodies);
		SetField(ctrl, "enemyIdleProjectiles", enemies.idleProjectiles);
		SetField(ctrl, "enemyAnimators", enemies.animators);
		SetField(ctrl, "enemyNames", enemies.names);
		SetField(ctrl, "enemyHpFills", enemies.hpFills);
		SetField(ctrl, "enemyHpTexts", enemies.hpTexts);
		SetField(ctrl, "targetMarkers", enemies.targetMarkers);
		SetField(ctrl, "deadOverlays", enemies.deadOverlays);
		SetField(ctrl, "enemyDeathGroundY", enemyDeathGroundY);
		SetField(ctrl, "heartDisplay", heartDisplay);
		SetField(ctrl, "battleLog", battleLog);
		SetField(ctrl, "bottomFocus", bottomFocus);
		SetField(ctrl, "vfx", vfx);
		SetField(ctrl, "battleAnims", battleAnims);
	}

	public static BattleRootHandles<TController> BuildBattleRootBase<TController>(
		StageBackgroundHandles background,
		StageSpriteBundle[] stageBundles,
		BattlePlayerRigHandles player,
		EnemySlotStripHandles enemies,
		float enemyDeathGroundY,
		HeartDisplay heartDisplay,
		BattleLog battleLog,
		BattleBottomFocusController bottomFocus,
		RectTransform damageSpawnParent)
		where TController : BattleControllerBase
	{
		var root = new GameObject("BattleRoot");
		var vfx = root.AddComponent<BattleDamageVFX>();
		SetField(vfx, "damageSpawnParent", damageSpawnParent);
		SetField(vfx, "enemyPanels", enemies.panels);

		var animations = root.AddComponent<BattleAnimations>();
		var controller = root.AddComponent<TController>();
		BindBattleControllerBase(controller, background, stageBundles, player, enemies,
			enemyDeathGroundY, heartDisplay, battleLog, bottomFocus, vfx, animations);

		return new BattleRootHandles<TController>
		{
			root = root,
			controller = controller,
			vfx = vfx,
			animations = animations,
		};
	}

	public static void BindBattleEnemyPanelButtons(BattleControllerBase ctrl, Button[] buttons)
	{
		if (ctrl == null || buttons == null)
			return;

		if (buttons.Length > 0 && buttons[0] != null)
			UnityEventTools.AddPersistentListener(buttons[0].onClick, ctrl.OnEnemyPanel0Clicked);
		if (buttons.Length > 1 && buttons[1] != null)
			UnityEventTools.AddPersistentListener(buttons[1].onClick, ctrl.OnEnemyPanel1Clicked);
		if (buttons.Length > 2 && buttons[2] != null)
			UnityEventTools.AddPersistentListener(buttons[2].onClick, ctrl.OnEnemyPanel2Clicked);
		if (buttons.Length > 3 && buttons[3] != null)
			UnityEventTools.AddPersistentListener(buttons[3].onClick, ctrl.OnEnemyPanel3Clicked);
	}

	public static GameObject BuildDebugConsole()
	{
		var debugGo = new GameObject("DebugConsole");
		debugGo.AddComponent<DebugConsoleController>();
		return debugGo;
	}

	public static bool SaveSceneAndShowDialog(UnityEngine.SceneManagement.Scene scene,
		string scenePath, string message,
		BuildSettingsPlacement buildSettingsPlacement = BuildSettingsPlacement.Append,
		bool showDialog = true)
	{
		EnsureDirectory(System.IO.Path.GetDirectoryName(scenePath).Replace("\\", "/"));
		ReportSceneBuildValidation();
		bool saved = EditorSceneManager.SaveScene(scene, scenePath);
		if (!saved)
			return false;
		AddSceneToBuildSettings(scenePath, buildSettingsPlacement);
		if (Application.isBatchMode)
			Debug.Log($"[SceneBuilderUtility] {message}");
		else if (showDialog)
			EditorUtility.DisplayDialog("씬 빌더", message, "확인");
		return true;
	}

	public static bool SaveSceneAssetOnlyAndShowDialog(UnityEngine.SceneManagement.Scene scene,
		string scenePath, string message, bool showDialog = true)
	{
		EnsureDirectory(System.IO.Path.GetDirectoryName(scenePath).Replace("\\", "/"));
		ReportSceneBuildValidation();
		bool saved = EditorSceneManager.SaveScene(scene, scenePath);
		if (!saved)
			return false;
		if (Application.isBatchMode)
			Debug.Log($"[SceneBuilderUtility] {message}");
		else if (showDialog)
			EditorUtility.DisplayDialog("씬 빌더", message, "확인");
		return true;
	}

	/// <summary>
	/// 모든 배틀 씬에서 공유하는 전투 로그 스크롤 뷰 빌더.
	/// container 내부에 ScrollRect + Viewport(RectMask2D) + Content(VerticalLayoutGroup + ContentSizeFitter)
	/// + LogText(상단 앵커 + 자체 ContentSizeFitter) 구조를 생성한다.
	/// 새 메시지 추가 시 텍스트 → LogText RT → Content RT로 preferred height가 전파되어
	/// ScrollRect가 올바르게 스크롤 범위를 계산하고 BattleLog.ScrollToBottom()이 동작.
	///
	/// 반환된 핸들로 내부 Viewport/Content 앵커를 추가 튜닝할 수 있음 (예: DiceBattle의 역스케일 보상).
	/// </summary>
	public static ScrollableLogHandles BuildScrollableBattleLog(RectTransform container, ScrollableLogOptions options)
	{
		if (container == null) throw new System.ArgumentNullException(nameof(container));

		var scrollRect = container.gameObject.AddComponent<ScrollRect>();
		scrollRect.horizontal = false;
		scrollRect.vertical = true;
		scrollRect.movementType = ScrollRect.MovementType.Clamped;
		scrollRect.scrollSensitivity = 30f;

		var viewport = CreateImage(container, "Viewport", options.viewportColor, raycastTarget: true);
		Stretch(viewport);
		viewport.gameObject.AddComponent<RectMask2D>();
		scrollRect.viewport = viewport;

		var content = CreateEmpty(viewport, "Content");
		content.anchorMin = new Vector2(0f, 1f);
		content.anchorMax = new Vector2(1f, 1f);
		content.pivot = new Vector2(0.5f, 1f);
		content.offsetMin = Vector2.zero;
		content.offsetMax = Vector2.zero;
		content.sizeDelta = Vector2.zero;
		var vlg = content.gameObject.AddComponent<VerticalLayoutGroup>();
		vlg.padding = new RectOffset(0, 0, 0, 0);
		vlg.childControlWidth = true;
		vlg.childControlHeight = false;
		vlg.childForceExpandWidth = true;
		vlg.childForceExpandHeight = false;
		var contentCsf = content.gameObject.AddComponent<ContentSizeFitter>();
		contentCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
		scrollRect.content = content;

		var logText = CreateTMPText(content, "LogText", "",
			options.fontSize, options.textColor, options.alignment);
		logText.enableAutoSizing = false;
		logText.textWrappingMode = TextWrappingModes.Normal;
		logText.overflowMode = TextOverflowModes.Overflow;
		logText.richText = true;
		var logRT = logText.GetComponent<RectTransform>();
		logRT.anchorMin = new Vector2(0f, 1f);
		logRT.anchorMax = new Vector2(1f, 1f);
		logRT.pivot = new Vector2(0.5f, 1f);
		logRT.sizeDelta = Vector2.zero;
		if (options.textPadding != null)
		{
			logRT.offsetMin = new Vector2(options.textPadding.left, options.textPadding.bottom);
			logRT.offsetMax = new Vector2(-options.textPadding.right, -options.textPadding.top);
		}
		var textCsf = logText.gameObject.AddComponent<ContentSizeFitter>();
		textCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

		var battleLog = container.gameObject.AddComponent<BattleLog>();
		SetField(battleLog, "logText", logText);
		SetField(battleLog, "scrollRect", scrollRect);

		return new ScrollableLogHandles
		{
			log = battleLog,
			scrollRect = scrollRect,
			viewport = viewport,
			content = content,
			logText = logText,
		};
	}

	public static BattleBottomFocusHandles BuildBattleBottomFocus(RectTransform canvasRoot,
		RectTransform inputPanel, Color panelColor)
	{
		if (canvasRoot == null) throw new System.ArgumentNullException(nameof(canvasRoot));
		if (inputPanel == null) throw new System.ArgumentNullException(nameof(inputPanel));

		var inputGroup = inputPanel.gameObject.GetComponent<CanvasGroup>();
		if (inputGroup == null)
			inputGroup = inputPanel.gameObject.AddComponent<CanvasGroup>();

		var focusRoot = CreateEmpty(canvasRoot, "BottomFocusController");
		focusRoot.anchorMin = Vector2.zero;
		focusRoot.anchorMax = Vector2.zero;
		focusRoot.sizeDelta = Vector2.zero;
		var focus = focusRoot.gameObject.AddComponent<BattleBottomFocusController>();

		var logButtonGo = CreateButton(canvasRoot, "BattleLogButton", "로그", 20,
			new Color(0.10f, 0.12f, 0.18f, 0.88f),
			new Color(0.22f, 0.26f, 0.36f, 1f),
			new Color(0.06f, 0.07f, 0.10f, 1f));
		var logButtonRt = logButtonGo.GetComponent<RectTransform>();
		logButtonRt.anchorMin = new Vector2(0.91f, 0.352f);
		logButtonRt.anchorMax = new Vector2(0.975f, 0.392f);
		logButtonRt.offsetMin = Vector2.zero;
		logButtonRt.offsetMax = Vector2.zero;

		var messagePanel = CreateImage(canvasRoot, "BattleMessagePopup", panelColor, raycastTarget: true);
		CopyRect(inputPanel, messagePanel);
		var messageGroup = messagePanel.gameObject.AddComponent<CanvasGroup>();

		var messageText = CreateTMPText(messagePanel, "MessageText", "", 40,
			new Color(0.94f, 0.94f, 1f), TextAlignmentOptions.MidlineLeft);
		messageText.richText = true;
		messageText.textWrappingMode = TextWrappingModes.Normal;
		messageText.overflowMode = TextOverflowModes.Page;
		messageText.maxVisibleLines = 5;
		var msgTextRt = messageText.GetComponent<RectTransform>();
		msgTextRt.anchorMin = new Vector2(0.055f, 0.10f);
		msgTextRt.anchorMax = new Vector2(0.90f, 0.90f);
		msgTextRt.offsetMin = Vector2.zero;
		msgTextRt.offsetMax = Vector2.zero;

		var advanceGlyph = CreateTMPText(messagePanel, "AdvanceGlyph", "▼", 28,
			new Color(1f, 0.86f, 0.32f), TextAlignmentOptions.Center);
		var glyphRt = advanceGlyph.GetComponent<RectTransform>();
		glyphRt.anchorMin = new Vector2(0.92f, 0.08f);
		glyphRt.anchorMax = new Vector2(0.98f, 0.26f);
		glyphRt.offsetMin = Vector2.zero;
		glyphRt.offsetMax = Vector2.zero;

		var messageButton = messagePanel.gameObject.AddComponent<Button>();
		messageButton.transition = Selectable.Transition.None;

		var historyPanel = CreateImage(canvasRoot, "BattleHistoryLog", panelColor, raycastTarget: true);
		CopyRect(inputPanel, historyPanel);
		var historyGroup = historyPanel.gameObject.AddComponent<CanvasGroup>();

		var closeGo = CreateButton(historyPanel, "CloseHistoryButton", "닫기", 20,
			new Color(0.32f, 0.13f, 0.13f, 0.95f),
			new Color(0.52f, 0.20f, 0.20f, 1f),
			new Color(0.20f, 0.08f, 0.08f, 1f));
		var closeRt = closeGo.GetComponent<RectTransform>();
		closeRt.anchorMin = new Vector2(0.91f, 0.84f);
		closeRt.anchorMax = new Vector2(0.985f, 0.96f);
		closeRt.offsetMin = Vector2.zero;
		closeRt.offsetMax = Vector2.zero;

		var historyScrollArea = CreateEmpty(historyPanel, "HistoryScroll");
		historyScrollArea.anchorMin = new Vector2(0.035f, 0.08f);
		historyScrollArea.anchorMax = new Vector2(0.89f, 0.94f);
		historyScrollArea.offsetMin = Vector2.zero;
		historyScrollArea.offsetMax = Vector2.zero;
		var opts = ScrollableLogOptions.Default;
		opts.fontSize = 30f;
		opts.textColor = new Color(0.90f, 0.92f, 1f);
		opts.viewportColor = new Color(0f, 0f, 0f, 0.18f);
		opts.textPadding = new RectOffset(18, 18, 14, 14);
		var logHandles = BuildScrollableBattleLog(historyScrollArea, opts);

		SetField(focus, "inputGroup", inputGroup);
		SetField(focus, "messageGroup", messageGroup);
		SetField(focus, "messageText", messageText);
		SetField(focus, "messageAdvanceButton", messageButton);
		SetField(focus, "historyGroup", historyGroup);
		SetField(focus, "historyText", logHandles.logText);
		SetField(focus, "historyScroll", logHandles.scrollRect);
		SetField(focus, "logButton", logButtonGo.GetComponent<Button>());
		SetField(focus, "closeHistoryButton", closeGo.GetComponent<Button>());

		messagePanel.gameObject.SetActive(true);
		historyPanel.gameObject.SetActive(true);

		return new BattleBottomFocusHandles
		{
			focus = focus,
			log = logHandles.log,
			inputGroup = inputGroup,
			messagePanel = messagePanel,
			historyPanel = historyPanel,
			logButton = logButtonGo.GetComponent<Button>(),
		};
	}

	static void CopyRect(RectTransform source, RectTransform target)
	{
		target.anchorMin = source.anchorMin;
		target.anchorMax = source.anchorMax;
		target.pivot = source.pivot;
		target.offsetMin = source.offsetMin;
		target.offsetMax = source.offsetMax;
		target.sizeDelta = source.sizeDelta;
		target.anchoredPosition = source.anchoredPosition;
	}

	public static void AddSceneToBuildSettings(string scenePath,
		BuildSettingsPlacement placement = BuildSettingsPlacement.Append)
	{
		var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
		for (int i = scenes.Count - 1; i >= 0; i--)
		{
			if (scenes[i].path == scenePath)
			{
				if (placement == BuildSettingsPlacement.Append)
					return;
				scenes.RemoveAt(i);
			}
		}

		var scene = new EditorBuildSettingsScene(scenePath, true);
		if (placement == BuildSettingsPlacement.InsertFirst)
			scenes.Insert(0, scene);
		else
			scenes.Add(scene);
		EditorBuildSettings.scenes = scenes.ToArray();
	}

	/// <summary>UI 패널 생성 (raycastTarget = true, GameObject 반환)</summary>
	public static GameObject CreateUIPanel(Transform parent, string name, Color color)
	{
		var go = new GameObject(name);
		var rt = go.AddComponent<RectTransform>();
		rt.SetParent(parent, false);
		var img = go.AddComponent<Image>();
		img.color = color;
		return go;
	}

	/// <summary>화면 전체를 덮는 투명 Dimmer 생성 (비활성 상태로 반환)</summary>
	public static GameObject CreateDimmer(Transform parent, string name)
	{
		var dimmer = new GameObject(name);
		var rt = dimmer.AddComponent<RectTransform>();
		rt.SetParent(parent, false);
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;

		var image = dimmer.AddComponent<Image>();
		image.color = new Color(0f, 0f, 0f, 0f);
		image.raycastTarget = true;

		dimmer.SetActive(false);
		return dimmer;
	}

	/// <summary>픽셀아트 스프라이트 임포트 (Point 필터 + FullRect 메시)</summary>
	public static void EnsurePixelSprite(string path)
	{
		bool reimport = EnsureSpriteImport(path, SpriteMeshType.FullRect);
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null) return;
		if (importer.filterMode != FilterMode.Point)
		{
			importer.filterMode = FilterMode.Point;
			reimport = true;
		}
		if (reimport)
			importer.SaveAndReimport();
	}

	/// <summary>
	/// 슬라이스된 스프라이트 시트에서 모든 서브스프라이트를 로드.
	/// 이름 뒤 숫자로 정렬하여 프레임 순서를 보장한다. 시트의 Multiple 슬라이스 설정은 건드리지 않고
	/// Point 필터만 맞춰 픽셀아트 선명도를 보장한다.
	/// </summary>
	public static Sprite[] LoadSlicedSpriteFrames(string sheetPath)
	{
		var importer = AssetImporter.GetAtPath(sheetPath) as TextureImporter;
		if (importer != null && importer.filterMode != FilterMode.Point)
		{
			importer.filterMode = FilterMode.Point;
			importer.SaveAndReimport();
		}

		var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(sheetPath);
		var list = new List<Sprite>();
		foreach (var rep in reps)
		{
			if (rep is Sprite s)
				list.Add(s);
		}
		list.Sort((a, b) => ExtractTrailingNumber(a.name).CompareTo(ExtractTrailingNumber(b.name)));
		if (list.Count == 0)
			Debug.LogWarning($"[SceneBuilderUtility] 슬라이스된 스프라이트가 없음: {sheetPath}");
		return list.ToArray();
	}

	/// <summary>
	/// folder/{prefix}{i}.{extension} 경로에서 i=0..count-1 순서로 픽셀 스프라이트를 로드한다.
	/// 예: LoadNumberedPixelSprites("Assets/Player/Sprites/Idle", "", "png", 145)
	/// </summary>
	public static Sprite[] LoadNumberedPixelSprites(string folder, string prefix, string extension, int count)
	{
		var paths = ResolveNumberedSpritePaths(folder, prefix, extension, count);
		var sprites = new List<Sprite>(paths.Count);
		for (int i = 0; i < paths.Count; i++)
		{
			string p = paths[i].Replace('\\', '/');
			var sprite = LoadPixelSpriteFrame(p);
			if (sprite != null)
				sprites.Add(sprite);
			else
				Debug.LogWarning($"[SceneBuilderUtility] 프레임 로드 실패: {p}");
		}
		return sprites.ToArray();
	}

	public static Sprite[] LoadAvailablePixelSpriteFrames(string folder)
	{
		var ordered = GetOrderedPngFiles(folder);

		var sprites = new List<Sprite>(ordered.Count);
		for (int i = 0; i < ordered.Count; i++)
		{
			string assetPath = ordered[i].Replace('\\', '/');
			var sprite = LoadPixelSpriteFrame(assetPath);
			if (sprite != null)
				sprites.Add(sprite);
			else
				Debug.LogWarning($"[SceneBuilderUtility] 프레임 로드 실패: {assetPath}");
		}
		return sprites.ToArray();
	}

	static List<string> ResolveNumberedSpritePaths(string folder, string prefix, string extension, int count)
	{
		var paths = new List<string>(Mathf.Max(0, count));
		if (count <= 0 || string.IsNullOrEmpty(folder))
			return paths;

		string normalizedExtension = string.IsNullOrEmpty(extension)
			? "png"
			: extension.TrimStart('.');
		bool directSequenceExists = true;
		for (int i = 0; i < count; i++)
		{
			string directPath = $"{folder}/{prefix}{i}.{normalizedExtension}";
			paths.Add(directPath);
			if (!System.IO.File.Exists(directPath))
				directSequenceExists = false;
		}

		if (directSequenceExists)
			return paths;

		var ordered = GetOrderedSpriteFiles(folder, normalizedExtension);
		if (!string.IsNullOrEmpty(prefix))
		{
			ordered = ordered
				.FindAll(p => System.IO.Path.GetFileNameWithoutExtension(p).StartsWith(prefix, System.StringComparison.Ordinal));
		}

		if (ordered.Count == 0)
			return paths;

		if (ordered.Count < count)
		{
			Debug.LogWarning(
				$"[SceneBuilderUtility] 요청 프레임 수보다 PNG가 적음: {folder} requested={count} found={ordered.Count}");
		}

		int resolvedCount = Mathf.Min(count, ordered.Count);
		var resolved = new List<string>(resolvedCount);
		for (int i = 0; i < resolvedCount; i++)
			resolved.Add(ordered[i]);
		return resolved;
	}

	static Sprite LoadPixelSpriteFrame(string assetPath)
	{
		var importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
		if (importer != null &&
			importer.textureType == TextureImporterType.Sprite &&
			importer.spriteImportMode == SpriteImportMode.Multiple)
		{
			var representativeSprite = LoadBestSpriteRepresentation(assetPath);
			return representativeSprite != null ? representativeSprite : AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
		}

		EnsurePixelSprite(assetPath);
		var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
		return sprite != null ? sprite : LoadBestSpriteRepresentation(assetPath);
	}

	static Sprite LoadBestSpriteRepresentation(string assetPath)
	{
		var reps = AssetDatabase.LoadAllAssetRepresentationsAtPath(assetPath);
		Sprite best = null;
		float bestArea = -1f;
		foreach (var rep in reps)
		{
			if (rep is not Sprite sprite)
				continue;

			float area = sprite.rect.width * sprite.rect.height;
			if (best == null || area > bestArea ||
				(Mathf.Approximately(area, bestArea) && string.CompareOrdinal(sprite.name, best.name) < 0))
			{
				best = sprite;
				bestArea = area;
			}
		}
		return best;
	}

	static bool WriteBytesIfChanged(string assetPath, byte[] bytes)
	{
		string fullPath = System.IO.Path.GetFullPath(assetPath);
		string dir = System.IO.Path.GetDirectoryName(fullPath);
		if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
			System.IO.Directory.CreateDirectory(dir);

		if (System.IO.File.Exists(fullPath))
		{
			var existing = System.IO.File.ReadAllBytes(fullPath);
			if (ByteArraysEqual(existing, bytes))
				return false;
		}

		System.IO.File.WriteAllBytes(fullPath, bytes);
		return true;
	}

	static bool ByteArraysEqual(byte[] a, byte[] b)
	{
		if (ReferenceEquals(a, b))
			return true;
		if (a == null || b == null || a.Length != b.Length)
			return false;
		for (int i = 0; i < a.Length; i++)
		{
			if (a[i] != b[i])
				return false;
		}
		return true;
	}

	static List<string> GetOrderedPngFiles(string folder)
		=> GetOrderedSpriteFiles(folder, "png");

	static List<string> GetOrderedSpriteFiles(string folder, string extension)
	{
		if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder))
			return new List<string>();

		string searchPattern = $"*.{(string.IsNullOrEmpty(extension) ? "png" : extension.TrimStart('.'))}";
		var files = System.IO.Directory.GetFiles(folder, searchPattern);
		var ordered = new List<string>(files);
		ordered.Sort((a, b) =>
		{
			int na = ExtractTrailingNumber(System.IO.Path.GetFileNameWithoutExtension(a));
			int nb = ExtractTrailingNumber(System.IO.Path.GetFileNameWithoutExtension(b));
			int cmp = na.CompareTo(nb);
			return cmp != 0 ? cmp : string.CompareOrdinal(a, b);
		});
		return ordered;
	}

	static int ExtractTrailingNumber(string name)
	{
		int i = name.Length;
		while (i > 0 && char.IsDigit(name[i - 1])) i--;
		if (i == name.Length) return int.MaxValue;
		return int.TryParse(name.Substring(i), out int n) ? n : int.MaxValue;
	}

	static bool SameAssetFolder(string a, string b)
	{
		if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b))
			return false;
		return string.Equals(
			a.TrimEnd('/', '\\').Replace('\\', '/'),
			b.TrimEnd('/', '\\').Replace('\\', '/'),
			System.StringComparison.Ordinal);
	}

	/// <summary>스프라이트 임포트 (Tight 메시로 투명 영역 제거)</summary>
	public static void EnsureTightSprite(string path)
	{
		if (EnsureSpriteImport(path, SpriteMeshType.Tight))
		{
			var importer = AssetImporter.GetAtPath(path) as TextureImporter;
			if (importer != null)
				importer.SaveAndReimport();
		}
	}

	/// <summary>
	/// 지정 경로에 스프라이트가 있으면 로드, 없으면 themeColor로 단순 실루엣을 생성해
	/// Assets/Generated/Fallback_{sanitizedKey}.png에 저장 후 로드한다.
	/// 스테이지/몹 에셋 누락 시 자동 폴백용 — 런타임에서 null 참조가 나지 않도록 보장.
	/// </summary>
	public static Sprite LoadSpriteOrColoredFallback(string path, string fallbackKey, Color themeColor, int size = 128)
	{
		if (!string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
		{
			EnsureTightSprite(path);
			var s = AssetDatabase.LoadAssetAtPath<Sprite>(path);
			if (s != null) return s;
		}

		string safeKey = string.IsNullOrEmpty(fallbackKey) ? "unnamed" : SanitizeFileName(fallbackKey);
		string fallbackPath = $"Assets/Generated/Fallback_{safeKey}.png";
		EnsureDirectory("Assets/Generated");

		if (!System.IO.File.Exists(fallbackPath))
			WriteColoredPlaceholderPng(fallbackPath, themeColor, size);

		EnsureTightSprite(fallbackPath);
		return AssetDatabase.LoadAssetAtPath<Sprite>(fallbackPath);
	}

	static string SanitizeFileName(string input)
	{
		var invalid = System.IO.Path.GetInvalidFileNameChars();
		var sb = new System.Text.StringBuilder(input.Length);
		foreach (char c in input)
		{
			bool ok = !System.Array.Exists(invalid, ch => ch == c);
			sb.Append(ok && c != ' ' ? c : '_');
		}
		return sb.ToString();
	}

	static void WriteColoredPlaceholderPng(string path, Color themeColor, int size)
	{
		var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
		var pixels = new Color[size * size];

		// 단순 타원형 실루엣 (중심 기준 반지름 0.45) — 배경은 투명, 내부는 themeColor, 테두리는 약간 어둡게
		float cx = size * 0.5f;
		float cy = size * 0.5f;
		float rMain = size * 0.45f;
		Color inside = themeColor;
		Color edge   = new Color(themeColor.r * 0.55f, themeColor.g * 0.55f, themeColor.b * 0.55f, 1f);
		Color shade  = new Color(themeColor.r * 0.80f, themeColor.g * 0.80f, themeColor.b * 0.80f, 1f);
		for (int y = 0; y < size; y++)
		{
			for (int x = 0; x < size; x++)
			{
				float dx = (x - cx) / rMain;
				float dy = (y - cy) / rMain;
				float d = Mathf.Sqrt(dx * dx + dy * dy);
				int i = y * size + x;
				if (d > 1.02f)
					pixels[i] = new Color(0, 0, 0, 0);
				else if (d > 0.92f)
					pixels[i] = edge;
				else if (dy < 0f && d > 0.55f)
					pixels[i] = shade; // 아래쪽 그림자 힌트
				else
					pixels[i] = inside;
			}
		}

		tex.SetPixels(pixels);
		tex.Apply();
		System.IO.File.WriteAllBytes(path, tex.EncodeToPNG());
		Object.DestroyImmediate(tex);
		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
	}

	/// <summary>
	/// 등록된 모든 스테이지에 대해 번들(배경+몹스프라이트+보스스프라이트)을 편집시점에 미리 로드.
	/// 런타임 컨트롤러의 [SerializeField] StageSpriteBundle[] 필드에 SetField로 주입한다.
	/// </summary>
	public static StageSpriteBundle[] BuildAllStageBundles()
	{
		var ids = StageRegistry.AllIds;
		var bundles = new StageSpriteBundle[ids.Count];
		for (int i = 0; i < ids.Count; i++)
		{
			var stage = StageRegistry.Get(ids[i]);
			bundles[i] = BuildStageBundle(stage);
		}
		return bundles;
	}

	public static StageSpriteBundle BuildStageBundle(StageData stage)
	{
		if (stage == null) return null;

		var bundle = new StageSpriteBundle
		{
			stageId = stage.id,
			background = LoadSpriteOrColoredFallback(
				stage.backgroundSpritePath,
				$"bg_{stage.id}",
				stage.themeColor,
				256),
		};

		if (stage.mobPool != null)
		{
			bundle.mobSprites = new Sprite[stage.mobPool.Count];
			bundle.mobAnimations = new EnemySpriteAnimationSet[stage.mobPool.Count];
			bundle.mobProjectileSprites = new Sprite[stage.mobPool.Count];
			bundle.mobAttackVfxSprites = new Sprite[stage.mobPool.Count];
			for (int m = 0; m < stage.mobPool.Count; m++)
			{
				var def = stage.mobPool[m];
				var anim = BuildMobAnimationSet(def);
				bundle.mobAnimations[m] = anim;

				bundle.mobSprites[m] = anim.idleSprites != null && anim.idleSprites.Length > 0
					? anim.idleSprites[0]
					: LoadSpriteOrColoredFallback(
					def.spritePath,
					$"mob_{stage.id}_{def.name}",
					def.themeColor,
					128);

				if (!string.IsNullOrEmpty(def.projectileSpritePath) &&
					System.IO.File.Exists(def.projectileSpritePath))
				{
					EnsureTightSprite(def.projectileSpritePath);
					bundle.mobProjectileSprites[m] =
						AssetDatabase.LoadAssetAtPath<Sprite>(def.projectileSpritePath);
				}

				if (!string.IsNullOrEmpty(def.attackVfxSpritePath) &&
					System.IO.File.Exists(def.attackVfxSpritePath))
				{
					EnsurePixelSprite(def.attackVfxSpritePath);
					bundle.mobAttackVfxSprites[m] =
						AssetDatabase.LoadAssetAtPath<Sprite>(def.attackVfxSpritePath);
				}
			}
		}
		else
		{
			bundle.mobSprites = new Sprite[0];
			bundle.mobAnimations = new EnemySpriteAnimationSet[0];
			bundle.mobProjectileSprites = new Sprite[0];
			bundle.mobAttackVfxSprites = new Sprite[0];
		}

		if (stage.boss != null)
		{
			bundle.bossAnimation = BuildBossAnimationSet(stage.boss);
			bundle.bossSprite = LoadSpriteOrColoredFallback(
				stage.boss.spritePath,
				$"boss_{stage.id}_{stage.boss.name}",
				stage.boss.themeColor,
				256);
			if (bundle.bossAnimation != null &&
				bundle.bossAnimation.idleSprites != null &&
				bundle.bossAnimation.idleSprites.Length > 0)
			{
				bundle.bossSprite = bundle.bossAnimation.idleSprites[0];
			}
		}

		return bundle;
	}

	static EnemySpriteAnimationSet BuildMobAnimationSet(MobDef def)
	{
		if (def == null)
			return new EnemySpriteAnimationSet();

		var set = BuildEnemyAnimationSet(
			def.idleSpriteFolderPath,
			def.attackSpriteFolderPath,
			def.hitSpriteFolderPath,
			def.deathSpriteFolderPath,
			def.deathAnimationClipPath,
			def.deathFrameRateMultiplier,
			def.attackFrameRate,
			def.attackSpriteFrameCount,
			def.hitSpriteFrameCount);
		set.attackVisualScaleMultiplier = def.attackVisualScaleMultiplier;
		set.attackVisualOffset = def.attackVisualOffset;
		set.attackUseFullTextureFrames = def.attackUseFullTextureFrames;
		return set;
	}

	static EnemySpriteAnimationSet BuildBossAnimationSet(BossDef def)
	{
		if (def == null)
			return new EnemySpriteAnimationSet();

		return BuildEnemyAnimationSet(
			def.idleSpriteFolderPath,
			def.attackSpriteFolderPath,
			def.hitSpriteFolderPath,
			def.deathSpriteFolderPath,
			def.deathAnimationClipPath);
	}

	static EnemySpriteAnimationSet BuildEnemyAnimationSet(
		string idleFolderPath,
		string attackFolderPath,
		string hitFolderPath,
		string deathFolderPath,
		string deathAnimationClipPath = null,
		float deathFrameRateMultiplier = 1f,
		float attackFrameRate = 0f,
		int attackFrameCount = 0,
		int hitFrameCount = 0)
	{
		var idle = LoadAvailablePixelSpriteFrames(idleFolderPath);
		var attack = attackFrameCount > 0
			? LoadNumberedPixelSprites(attackFolderPath, "", "png", attackFrameCount)
			: LoadAvailablePixelSpriteFrames(attackFolderPath);
		var hit = hitFrameCount > 0
			? LoadNumberedPixelSprites(hitFolderPath, "", "png", hitFrameCount)
			: LoadAvailablePixelSpriteFrames(hitFolderPath);
		var death = LoadAvailablePixelSpriteFrames(deathFolderPath);
		var deathClip = LoadAnimationClip(deathAnimationClipPath);
		Sprite[] clipDeathSprites = null;
		Vector2[] deathCenterOffsets = null;
		float deathFrameRate = 0f;

		if (TryExtractSpriteFrames(deathClip, out clipDeathSprites))
		{
			deathFrameRate = Mathf.Max(1f, deathClip.frameRate);
			death = clipDeathSprites;
			deathClip = null;
		}

		if (clipDeathSprites != null && clipDeathSprites.Length > 0)
			deathCenterOffsets = BuildVisualCenterOffsets(clipDeathSprites);

		return new EnemySpriteAnimationSet
		{
			idleSprites = idle,
			attackSprites = ContainsSprite(attack) ? attack : idle,
			hitSprites = ContainsSprite(hit) ? hit : idle,
			deathSprites = death,
			deathSpriteCenterOffsets = deathCenterOffsets,
			deathAnimationClip = deathClip,
			attackFrameRate = Mathf.Max(0f, attackFrameRate),
			deathFrameRate = deathFrameRate,
			deathFrameRateMultiplier = Mathf.Max(0.01f, deathFrameRateMultiplier),
			attackPingPong = !ContainsSprite(attack) || SameAssetFolder(attackFolderPath, idleFolderPath),
			hitPingPong = true,
		};
	}

	static bool ContainsSprite(Sprite[] sprites)
	{
		if (sprites == null)
			return false;
		for (int i = 0; i < sprites.Length; i++)
		{
			if (sprites[i] != null)
				return true;
		}
		return false;
	}

	static AnimationClip LoadAnimationClip(string clipPath)
	{
		if (string.IsNullOrEmpty(clipPath))
			return null;

		var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
		if (clip == null)
			Debug.LogWarning($"[SceneBuilderUtility] 애니메이션 클립 로드 실패: {clipPath}");
		return clip;
	}

	static bool TryExtractSpriteFrames(AnimationClip clip, out Sprite[] sprites)
	{
		sprites = null;
		if (clip == null)
			return false;
		if (AnimationUtility.GetCurveBindings(clip).Length > 0)
			return false;

		var bindings = AnimationUtility.GetObjectReferenceCurveBindings(clip);
		if (bindings == null || bindings.Length != 1)
			return false;

		var binding = bindings[0];
		if (binding.type != typeof(SpriteRenderer) || binding.propertyName != "m_Sprite")
			return false;

		var keyframes = AnimationUtility.GetObjectReferenceCurve(clip, binding);
		if (keyframes == null || keyframes.Length == 0)
			return false;

		var extracted = new List<Sprite>(keyframes.Length);
		for (int i = 0; i < keyframes.Length; i++)
		{
			if (keyframes[i].value is Sprite sprite)
				extracted.Add(sprite);
		}

		if (extracted.Count == 0)
			return false;

		sprites = extracted.ToArray();
		return true;
	}

	static Vector2[] BuildVisualCenterOffsets(Sprite[] sprites)
	{
		if (sprites == null || sprites.Length == 0)
			return null;

		var textureCache = new Dictionary<string, Texture2D>();
		try
		{
			var centers = new Vector2[sprites.Length];
			var hasCenter = new bool[sprites.Length];
			int referenceIndex = -1;

			for (int i = 0; i < sprites.Length; i++)
			{
				if (!TryGetSpriteAlphaCenter(sprites[i], textureCache, out centers[i]))
					continue;

				hasCenter[i] = true;
				if (referenceIndex < 0)
					referenceIndex = i;
			}

			if (referenceIndex < 0)
				return null;

			Vector2 referenceCenter = centers[referenceIndex];
			var offsets = new Vector2[sprites.Length];
			for (int i = 0; i < sprites.Length; i++)
				offsets[i] = hasCenter[i] ? referenceCenter - centers[i] : Vector2.zero;
			return offsets;
		}
		finally
		{
			foreach (var pair in textureCache)
			{
				if (pair.Value != null)
					UnityEngine.Object.DestroyImmediate(pair.Value);
			}
		}
	}

	static bool TryGetSpriteAlphaCenter(Sprite sprite, Dictionary<string, Texture2D> textureCache,
		out Vector2 center)
	{
		center = Vector2.zero;
		if (sprite == null || sprite.texture == null)
			return false;

		string texturePath = AssetDatabase.GetAssetPath(sprite.texture);
		if (string.IsNullOrEmpty(texturePath) || !System.IO.File.Exists(texturePath))
			return false;

		if (!textureCache.TryGetValue(texturePath, out Texture2D texture) || texture == null)
		{
			byte[] bytes = System.IO.File.ReadAllBytes(texturePath);
			texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			if (!texture.LoadImage(bytes))
				return false;
			textureCache[texturePath] = texture;
		}

		var rect = sprite.rect;
		int xMin = Mathf.Clamp(Mathf.RoundToInt(rect.xMin), 0, texture.width);
		int yMin = Mathf.Clamp(Mathf.RoundToInt(rect.yMin), 0, texture.height);
		int xMax = Mathf.Clamp(Mathf.RoundToInt(rect.xMax), 0, texture.width);
		int yMax = Mathf.Clamp(Mathf.RoundToInt(rect.yMax), 0, texture.height);
		if (xMax <= xMin || yMax <= yMin)
			return false;

		Color32[] pixels = texture.GetPixels32();
		int minLocalX = int.MaxValue;
		int minLocalY = int.MaxValue;
		int maxLocalX = int.MinValue;
		int maxLocalY = int.MinValue;

		for (int y = yMin; y < yMax; y++)
		{
			int rowOffset = y * texture.width;
			for (int x = xMin; x < xMax; x++)
			{
				if (pixels[rowOffset + x].a <= 5)
					continue;

				int localX = x - xMin;
				int localY = y - yMin;
				minLocalX = Mathf.Min(minLocalX, localX);
				minLocalY = Mathf.Min(minLocalY, localY);
				maxLocalX = Mathf.Max(maxLocalX, localX);
				maxLocalY = Mathf.Max(maxLocalY, localY);
			}
		}

		if (minLocalX == int.MaxValue)
			return false;

		center = new Vector2(
			(minLocalX + maxLocalX) * 0.5f,
			(minLocalY + maxLocalY) * 0.5f);
		return true;
	}

	// ── SE (Sound Effect) 경로 카탈로그 ──
	public const string SeRoot = "Assets/Se/True 8-bit Sound Effect Collection - Lite";
	public const string DrumRollClipPath = "Assets/Se/DiceRoll_WakuWaku.wav";

	/// <summary>SE 카탈로그 — 이름(확장자 제외) → 에셋 경로. 각 씬 빌더에서 필요한 이름만 골라 로드한다.</summary>
	static readonly Dictionary<string, string> SeCatalog = new Dictionary<string, string>
	{
		{ "Player_Attack",         SeRoot + "/Sword Slashes/Player_Attack.wav" },
		{ "Player_Attack_Small",   SeRoot + "/Explosions/Player_Attack_Small.wav" },
		{ "Player_Attack_Medium",  SeRoot + "/Explosions/Player_Attack_Medium.wav" },
		{ "Player_Attack_Big",     SeRoot + "/Explosions/Player_Attack_Big.wav" },
		{ "Enemy123_Attack",       SeRoot + "/Gunshots/Enemy123_Attack.wav" },
		{ "Enemy45_Attack",        SeRoot + "/Gunshots/Enemy45_Attack.wav" },
		{ "Enemy_Die",             SeRoot + "/Lasers/Enemy_Die.wav" },
		{ "Player_Death",          SeRoot + "/Deaths/Player_Death.wav" },
		{ "Player_PerfectDefense", SeRoot + "/Sword Blocks/Player_PerfectDefense.wav" },
		{ "Player_PartialDefense", SeRoot + "/Sword Unsheaths/Player_PartialDefense.wav" },
		{ "Alert_LowHP",           SeRoot + "/Alarms/Alert_LowHP.wav" },
		{ "Gauge_Empty",           SeRoot + "/UI Data Down/Gauge_Empty.wav" },
		{ "Gauge_Fill",            SeRoot + "/UI Data Up/Gauge_Fill.wav" },
		{ "UI_Click",              SeRoot + "/UI Clicks Positive/UI_Click.wav" },
		{ "UI_OK",                 SeRoot + "/UI Clicks Positive/UI_OK.wav" },
		{ "UI_Back_NO",            SeRoot + "/UI Clicks Positive/UI_Back_NO.wav" },
		{ "UI_Failure",            SeRoot + "/Deaths/UI_Failure.wav" },
		{ "UI_Purchase_OK_LockIn", SeRoot + "/Cash/UI_Purchase_OK_LockIn.wav" },
		{ "Player_EarnDrop",       SeRoot + "/Coin Collects/Player_EarnDrop.wav" },
		{ "Transition_2",          SeRoot + "/Transitions/Transition_2.wav" },
		{ "Transition_2_Quit",     SeRoot + "/Transitions/Transition_2_Quit.wav" },
		{ "Transition_3",          SeRoot + "/Transitions/Transition_3.wav" },
		{ "Environment_Desert",    SeRoot + "/Wind/Environment_Desert.wav" },
		{ "DIce_WakuWaku_Level3",  SeRoot + "/PowerUps/DIce_WakuWaku_Level3.wav" },
	};

	/// <summary>
	/// 씬 루트에 AudioManager 부트 오브젝트를 만들고, 지정된 SE 이름 목록을 카탈로그에서 로드해 주입한다.
	/// drumRoll=true인 씬에서는 DiceDrumRollAudio 용 별도 AudioSource와 DiceRoll_WakuWaku 클립도 함께 연결.
	/// </summary>
	public static void BuildAudioManager(string[] seNames, bool includeDrumRoll)
	{
		var go = new GameObject("AudioManager");
		// 씬에 AudioListener가 없으면 매 프레임 경고가 뜨므로, 없을 때만 추가.
		if (Object.FindFirstObjectByType<AudioListener>() == null)
			go.AddComponent<AudioListener>();
		var src = go.AddComponent<AudioSource>();
		src.playOnAwake = false;
		src.loop        = false;

		var mgr = go.AddComponent<AudioManager>();

		var list = new List<AudioClip>();
		if (seNames != null)
		{
			foreach (var name in seNames)
			{
				if (!SeCatalog.TryGetValue(name, out var path))
				{
					Debug.LogWarning($"[SceneBuilderUtility] SE 카탈로그에 '{name}' 없음");
					continue;
				}
				var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
				if (clip == null)
				{
					Debug.LogWarning($"[SceneBuilderUtility] SE 로드 실패: {path}");
					continue;
				}
				list.Add(clip);
			}
		}
		SetField(mgr, "clips",  list.ToArray());
		SetField(mgr, "source", src);

		if (includeDrumRoll)
		{
			var drumSrc = go.AddComponent<AudioSource>();
			drumSrc.playOnAwake = false;
			drumSrc.loop        = true;
			var drumClip = AssetDatabase.LoadAssetAtPath<AudioClip>(DrumRollClipPath);
			if (drumClip == null)
				Debug.LogWarning($"[SceneBuilderUtility] 드럼롤 클립 로드 실패: {DrumRollClipPath}");
			SetField(mgr, "drumRollSource", drumSrc);
			SetField(mgr, "drumRollClip",   drumClip);
		}
	}

	/// <summary>스프라이트 임포트 공통 설정 (Sprite 타입 + Single 모드 + 메시 타입). reimport 필요 여부를 반환.</summary>
	static bool EnsureSpriteImport(string path, SpriteMeshType meshType)
	{
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null && !string.IsNullOrEmpty(path) && System.IO.File.Exists(path))
		{
			AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
			importer = AssetImporter.GetAtPath(path) as TextureImporter;
		}
		if (importer == null) return false;
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
		if (settings.spriteMeshType != meshType)
		{
			settings.spriteMeshType = meshType;
			importer.SetTextureSettings(settings);
			reimport = true;
		}
		return reimport;
	}
}

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class ExploreMapCircleGraphic : MaskableGraphic
{
	const int SegmentCount = 48;

	protected override void OnPopulateMesh(VertexHelper vh)
	{
		vh.Clear();

		Rect rect = rectTransform.rect;
		float radius = Mathf.Min(rect.width, rect.height) * 0.5f;
		if (radius <= 0f)
			return;

		Vector2 center = rect.center;
		var vertex = UIVertex.simpleVert;
		vertex.color = color;
		vertex.position = center;
		vh.AddVert(vertex);

		for (int i = 0; i < SegmentCount; i++)
		{
			float angle = i * Mathf.PI * 2f / SegmentCount;
			vertex.position = center + new Vector2(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius);
			vh.AddVert(vertex);
		}

		for (int i = 1; i <= SegmentCount; i++)
		{
			int next = i == SegmentCount ? 1 : i + 1;
			vh.AddTriangle(0, i, next);
		}
	}
}
