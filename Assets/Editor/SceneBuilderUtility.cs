using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using System.Reflection;
using System.Collections.Generic;

/// <summary>
/// 모든 씬 빌더에서 공유하는 UI/유틸리티 헬퍼.
/// </summary>
public static class SceneBuilderUtility
{
	public const string FontPath = "Assets/TextMesh Pro/Fonts/Mona12.asset";

	// ── 공유 버튼 색상 ──
	public static readonly Color ButtonNormal    = new Color(0.15f, 0.18f, 0.35f, 0.9f);
	public static readonly Color ButtonHighlight = new Color(0.28f, 0.35f, 0.70f, 1f);
	public static readonly Color ButtonPressed   = new Color(0.10f, 0.12f, 0.25f, 1f);

	// ── BattleScene sprite animation speed tuning ──
	public const float BattlePlayerActionFrameRate = 30f;
	public const float BattlePlayerIdleFrameRate = 6.5f;
	public const float BattleEnemyIdleFrameRate = 12f;
	public const float BattleEnemyActionFrameRate = 18f;
	public const float BattlePlayerJumpDuration = 0.3f;

	public static void SetField(object target, string fieldName, object value)
	{
		if (target == null)
			return;
		// 베이스 클래스에 선언된 protected 필드도 찾도록 타입 체인을 위로 탐색.
		var t = target.GetType();
		while (t != null)
		{
			var field = t.GetField(fieldName,
				BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public |
				BindingFlags.DeclaredOnly);
			if (field != null)
			{
				field.SetValue(target, value);
				return;
			}
			t = t.BaseType;
		}
		Debug.LogWarning($"[SceneBuilderUtility] Field '{fieldName}' not found on {target.GetType().Name}");
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

	static void MakeBorderChild(RectTransform parent, string name, Vector2 min, Vector2 max, Color color)
	{
		var b = CreateImage(parent, name, color);
		b.anchorMin = min; b.anchorMax = max;
		b.offsetMin = Vector2.zero; b.offsetMax = Vector2.zero;
	}

	/// <summary>1x1 흰색 픽셀 스프라이트 — Filled Image용 (둥근 모서리 없음)</summary>
	public static Sprite WhitePixelSprite()
	{
		var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		tex.SetPixel(0, 0, Color.white);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
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
		logButtonRt.anchorMin = new Vector2(0.91f, 0.292f);
		logButtonRt.anchorMax = new Vector2(0.975f, 0.327f);
		logButtonRt.offsetMin = Vector2.zero;
		logButtonRt.offsetMax = Vector2.zero;

		var messagePanel = CreateImage(canvasRoot, "BattleMessagePopup", panelColor, raycastTarget: true);
		CopyRect(inputPanel, messagePanel);
		var messageGroup = messagePanel.gameObject.AddComponent<CanvasGroup>();

		var messageText = CreateTMPText(messagePanel, "MessageText", "", 46,
			new Color(0.94f, 0.94f, 1f), TextAlignmentOptions.MidlineLeft);
		messageText.richText = true;
		messageText.textWrappingMode = TextWrappingModes.Normal;
		messageText.overflowMode = TextOverflowModes.Ellipsis;
		var msgTextRt = messageText.GetComponent<RectTransform>();
		msgTextRt.anchorMin = new Vector2(0.055f, 0.18f);
		msgTextRt.anchorMax = new Vector2(0.90f, 0.86f);
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
		SetField(focus, "battleLog", logHandles.log);

		focus.Bind(logHandles.log);
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

	public static void AddSceneToBuildSettings(string scenePath)
	{
		var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
		foreach (var s in scenes)
		{
			if (s.path == scenePath)
				return;
		}
		scenes.Add(new EditorBuildSettingsScene(scenePath, true));
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
	/// 예: LoadNumberedPixelSprites("Assets/Player/IdleSprites", "New_Idle_", "jpg", 9)
	/// </summary>
	public static Sprite[] LoadNumberedPixelSprites(string folder, string prefix, string extension, int count)
	{
		var arr = new Sprite[count];
		for (int i = 0; i < count; i++)
		{
			string p = $"{folder}/{prefix}{i}.{extension}";
			EnsurePixelSprite(p);
			arr[i] = AssetDatabase.LoadAssetAtPath<Sprite>(p);
			if (arr[i] == null)
				Debug.LogWarning($"[SceneBuilderUtility] 프레임 로드 실패: {p}");
		}
		return arr;
	}

	public static Sprite[] LoadAvailablePixelSpriteFrames(string folder)
	{
		if (string.IsNullOrEmpty(folder) || !System.IO.Directory.Exists(folder))
			return new Sprite[0];

		var files = System.IO.Directory.GetFiles(folder, "*.png");
		var ordered = new List<string>(files);
		ordered.Sort((a, b) =>
		{
			int na = ExtractTrailingNumber(System.IO.Path.GetFileNameWithoutExtension(a));
			int nb = ExtractTrailingNumber(System.IO.Path.GetFileNameWithoutExtension(b));
			int cmp = na.CompareTo(nb);
			return cmp != 0 ? cmp : string.CompareOrdinal(a, b);
		});

		var sprites = new List<Sprite>(ordered.Count);
		for (int i = 0; i < ordered.Count; i++)
		{
			string assetPath = ordered[i].Replace('\\', '/');
			EnsurePixelSprite(assetPath);
			var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(assetPath);
			if (sprite != null)
				sprites.Add(sprite);
			else
				Debug.LogWarning($"[SceneBuilderUtility] 프레임 로드 실패: {assetPath}");
		}
		return sprites.ToArray();
	}

	static int ExtractTrailingNumber(string name)
	{
		int i = name.Length;
		while (i > 0 && char.IsDigit(name[i - 1])) i--;
		if (i == name.Length) return int.MaxValue;
		return int.TryParse(name.Substring(i), out int n) ? n : int.MaxValue;
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
			for (int m = 0; m < stage.mobPool.Count; m++)
			{
				var def = stage.mobPool[m];
				var anim = new EnemySpriteAnimationSet
				{
					idleSprites = LoadAvailablePixelSpriteFrames(def.idleSpriteFolderPath),
					attackSprites = LoadAvailablePixelSpriteFrames(def.attackSpriteFolderPath),
					hitSprites = LoadAvailablePixelSpriteFrames(def.hitSpriteFolderPath),
				};
				bundle.mobAnimations[m] = anim;

				bundle.mobSprites[m] = anim.idleSprites != null && anim.idleSprites.Length > 0
					? anim.idleSprites[0]
					: LoadSpriteOrColoredFallback(
					def.spritePath,
					$"mob_{stage.id}_{def.name}",
					def.themeColor,
					128);
			}
		}
		else
		{
			bundle.mobSprites = new Sprite[0];
			bundle.mobAnimations = new EnemySpriteAnimationSet[0];
		}

		if (stage.boss != null)
		{
			bundle.bossSprite = LoadSpriteOrColoredFallback(
				stage.boss.spritePath,
				$"boss_{stage.id}_{stage.boss.name}",
				stage.boss.themeColor,
				256);
		}

		return bundle;
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
