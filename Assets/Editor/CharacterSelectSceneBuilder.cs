// Assets/Editor/CharacterSelectSceneBuilder.cs
// Unity 메뉴 → Tools → Build CharacterSelect Scene 으로 씬을 자동 생성합니다.
// 컷씬 슬라이드 + 무기 선택 화면을 프로그래밍 방식으로 빌드합니다.

using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mahjong;
using SBU = SceneBuilderUtility;

public static class CharacterSelectSceneBuilder
{
	private const string ScenePath = "Assets/Scenes/CharacterSelect.unity";

	// ── 슬라이드 색상 ──
	static readonly Color BgColor         = new Color(0.06f, 0.07f, 0.13f, 1f);
	static readonly Color SlideHolder1    = new Color(0.15f, 0.22f, 0.35f, 1f);
	static readonly Color SlideHolder2    = new Color(0.20f, 0.15f, 0.30f, 1f);
	static readonly Color WeaponBgColor   = new Color(0.10f, 0.12f, 0.20f, 1f);
	static readonly Color MahjongColor    = new Color(0.20f, 0.65f, 0.40f, 1f);
	static readonly Color HoldemColor     = new Color(0.50f, 0.25f, 0.70f, 1f);
	static readonly Color DiceColor       = new Color(0.85f, 0.85f, 0.90f, 1f);
	static readonly Color SubtitleBgColor = new Color(0.04f, 0.04f, 0.08f, 0.55f);
	static readonly Color LabelColor      = new Color(0.92f, 0.92f, 1f, 1f);
	const string HoldemCardSpriteRoot = "Assets/Holdem/Sprites/Cards";
	const string HoldemCardBackPath = HoldemCardSpriteRoot + "/card_back_acorn.png";
	const string HoldemCardFrontFolder = HoldemCardSpriteRoot + "/Fronts";
	const string HoldemAceSpadesPath = HoldemCardFrontFolder + "/AS.png";
	const string HoldemKingHeartsPath = HoldemCardFrontFolder + "/KH.png";
	const string DiceMinePrefabPath = "Assets/Dices/Prefabs/Dice_d6_mine.prefab";
	const string DiceFallbackPrefabPath = "Assets/Dices/Prefabs/Dice_d6.prefab";
	const string MahjongTileDbPath = "Assets/Mahjong/MahjongTileSprites.asset";
	const string MahjongRedManFivePath = "Assets/Mahjong/m_5_red.png";
	const string StorySlide0Path = "Assets/Story/Story_CutScene_0.png";
	const string StorySlide1Path = "Assets/Story/Story_CutScene_1.png";
	const string StorySlide2Path = "Assets/Story/Story_CutScene_2.png";

	[MenuItem("Tools/Build CharacterSelect Scene")]
	public static void Build()
	{
		BuildInternal(confirmOverwrite: true, showCompletionDialog: true);
	}

	public static bool BuildForIncremental()
	{
		return BuildInternal(confirmOverwrite: false, showCompletionDialog: false);
	}

	static bool BuildInternal(bool confirmOverwrite, bool showCompletionDialog)
	{
		if (confirmOverwrite && File.Exists(ScenePath) && !Application.isBatchMode)
		{
			bool overwrite = EditorUtility.DisplayDialog(
				"CharacterSelect 씬 생성",
				$"{ScenePath} 가 이미 존재합니다. 덮어쓰시겠습니까?",
				"덮어쓰기", "취소");
			if (!overwrite)
				return false;
		}

		SBU.BeginSceneBuildValidation(nameof(CharacterSelectSceneBuilder));
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var shell = SBU.BuildSceneShell("MainCamera", new Color(0.09f, 0.09f, 0.15f, 1f),
			cameraPosition: new Vector3(0, 0, -10), includeAudioListener: true);
		var canvasGo = shell.canvas.gameObject;

		// ── 배경 ────────────────────────────────────────────────────
		var bgPanel = SBU.CreateUIPanel(canvasGo.transform, "Background", BgColor);
		var bgRt = bgPanel.GetComponent<RectTransform>();
		bgRt.anchorMin = Vector2.zero;
		bgRt.anchorMax = Vector2.one;
		bgRt.offsetMin = Vector2.zero;
		bgRt.offsetMax = Vector2.zero;

		// ── FadeGroup (CanvasGroup) ─────────────────────────────────
		var fadeGo = new GameObject("FadeGroup");
		fadeGo.transform.SetParent(canvasGo.transform, false);
		var fadeRt = fadeGo.AddComponent<RectTransform>();
		SBU.Stretch(fadeRt);
		var fadeCanvasGroup = fadeGo.AddComponent<CanvasGroup>();
		fadeCanvasGroup.alpha = 1f;

		// ── 슬라이드 컨테이너 ────────────────────────────────────────
		var slidesContainer = SBU.CreateEmpty(fadeGo.transform, "SlidesContainer");
		SBU.Stretch(slidesContainer);

		// ── 슬라이드 1: 숲에서 돌아다니던 당신(다람쥐)... ────────────
		var slide1Top = BuildImageSlide(slidesContainer, "Slide1TopContent",
			SlideHolder1, "컷씬 1", StorySlide0Path);

		// ── 슬라이드 2: 적들과 조우하게 되는데...! ────────────────────
		var slide2Top = BuildImageSlide(slidesContainer, "Slide2TopContent",
			SlideHolder2, "컷씬 2", StorySlide1Path);
		slide2Top.SetActive(false);

		// ── 슬라이드 3: 무기 선택 ────────────────────────────────────
		var slide3Top = BuildWeaponSelectSlide(slidesContainer, StorySlide2Path);
		slide3Top.SetActive(false);

		// ── 하단 자막 영역 ──────────────────────────────────────────
		var subtitleBg = SBU.CreateImage(fadeGo.transform, "SubtitleBackground",
			SubtitleBgColor, false);
		subtitleBg.anchorMin = new Vector2(0f, 0f);
		subtitleBg.anchorMax = new Vector2(1f, 0.20f);
		subtitleBg.offsetMin = Vector2.zero;
		subtitleBg.offsetMax = Vector2.zero;

		var subtitleTmp = SBU.CreateTMPText(fadeGo.transform, "SubtitleText",
			"숲에서 돌아다니던 당신(다람쥐)...", 44, LabelColor,
			TextAlignmentOptions.Center, FontStyles.Normal);
		var subtitleRt = subtitleTmp.GetComponent<RectTransform>();
		subtitleRt.anchorMin = new Vector2(0.05f, 0.03f);
		subtitleRt.anchorMax = new Vector2(0.95f, 0.17f);
		subtitleRt.offsetMin = Vector2.zero;
		subtitleRt.offsetMax = Vector2.zero;
		subtitleTmp.textWrappingMode = TextWrappingModes.Normal;
		subtitleTmp.enableWordWrapping = true;

		// ── ClickCatcher (전체화면 투명 버튼) ────────────────────────
		var clickCatcherGo = new GameObject("ClickCatcher");
		clickCatcherGo.transform.SetParent(canvasGo.transform, false);
		var clickRt = clickCatcherGo.AddComponent<RectTransform>();
		SBU.Stretch(clickRt);
		var clickImg = clickCatcherGo.AddComponent<Image>();
		clickImg.color = new Color(0, 0, 0, 0);
		clickImg.raycastTarget = true;
		clickCatcherGo.AddComponent<Button>().transition = Selectable.Transition.None;

		// ── Skip / Back 버튼 — 반투명 배경 ─────────────────────────
		var btnNormal = new Color(0.10f, 0.12f, 0.25f, 0.45f);
		var btnHover  = new Color(0.20f, 0.25f, 0.50f, 0.65f);
		var btnPress  = new Color(0.06f, 0.08f, 0.18f, 0.70f);

		var skipGo = SBU.CreateButton(canvasGo.transform, "SkipButton", "Skip ▷▷",
			36, btnNormal, btnHover, btnPress);
		var skipRt = skipGo.GetComponent<RectTransform>();
		skipRt.anchorMin = new Vector2(0.82f, 0.02f);
		skipRt.anchorMax = new Vector2(0.98f, 0.08f);
		skipRt.offsetMin = Vector2.zero;
		skipRt.offsetMax = Vector2.zero;

		var backGo = SBU.CreateButton(canvasGo.transform, "BackButton", "◁ 뒤로",
			36, btnNormal, btnHover, btnPress);
		var backRt = backGo.GetComponent<RectTransform>();
		backRt.anchorMin = new Vector2(0.02f, 0.02f);
		backRt.anchorMax = new Vector2(0.18f, 0.08f);
		backRt.offsetMin = Vector2.zero;
		backRt.offsetMax = Vector2.zero;

		// ── 컨트롤러 셋업 ───────────────────────────────────────────
		var controllerGo = new GameObject("CutsceneController");
		var controller = controllerGo.AddComponent<CharacterSelectController>();

		// CutsceneSlide 배열 구성
		var slideArray = new CutsceneSlide[]
		{
			new CutsceneSlide
			{
				subtitleText = "숲에서 돌아다니던 당신(다람쥐)...",
				topContent = slide1Top,
				isWeaponSelect = false,
			},
			new CutsceneSlide
			{
				subtitleText = "적들과 조우하게 되는데...!",
				topContent = slide2Top,
				isWeaponSelect = false,
			},
			new CutsceneSlide
			{
				subtitleText = "당신의 눈앞에 떨어진 무기는?",
				topContent = slide3Top,
				isWeaponSelect = true,
			},
		};

		SBU.SetField(controller, "slides", slideArray);
		SBU.SetField(controller, "subtitleText", subtitleTmp);
		SBU.SetField(controller, "clickCatcher", clickCatcherGo);
		SBU.SetField(controller, "skipButton", skipGo);
		SBU.SetField(controller, "fadeGroup", fadeCanvasGroup);

		// ── 버튼 이벤트 연결 ────────────────────────────────────────
		UnityEventTools.AddPersistentListener(
			clickCatcherGo.GetComponent<Button>().onClick,
			controller.AdvanceSlide);

		UnityEventTools.AddPersistentListener(
			skipGo.GetComponent<Button>().onClick,
			controller.SkipToWeaponSelect);

		UnityEventTools.AddPersistentListener(
			backGo.GetComponent<Button>().onClick,
			controller.OnBackClicked);

		// 무기 버튼 이벤트
		var mahjongBtn = FindWeaponButton(slide3Top, "Btn_Mahjong");
		var holdemBtn  = FindWeaponButton(slide3Top, "Btn_Holdem");
		var diceBtn    = FindWeaponButton(slide3Top, "Btn_Dice");

		if (mahjongBtn != null)
			UnityEventTools.AddPersistentListener(
				mahjongBtn.GetComponent<Button>().onClick,
				controller.OnWeaponSelected_Mahjong);

		if (holdemBtn != null)
			UnityEventTools.AddPersistentListener(
				holdemBtn.GetComponent<Button>().onClick,
				controller.OnWeaponSelected_Holdem);

		if (diceBtn != null)
			UnityEventTools.AddPersistentListener(
				diceBtn.GetComponent<Button>().onClick,
				controller.OnWeaponSelected_Dice);

		// ── AudioManager ────────────────────────────────────────────
		SBU.BuildAudioManager(new[]
		{
			"UI_Click", "UI_OK", "UI_Back_NO",
			"Transition_2", "Transition_2_Quit",
		}, false);

		// ── 씬 저장 ────────────────────────────────────────────────
		return SBU.SaveSceneAndShowDialog(scene, ScenePath,
			$"CharacterSelect 씬이 생성되었습니다.\n{ScenePath}",
			showDialog: showCompletionDialog);
	}

	static Transform FindWeaponButton(GameObject slideTopContent, string buttonName)
	{
		if (slideTopContent == null)
			return null;

		var root = slideTopContent.transform;
		var direct = root.Find($"WeaponButtonsRow/{buttonName}");
		if (direct != null)
			return direct;

		var visualRoot = root.Find($"WeaponVisualRoot/WeaponButtonsRow/{buttonName}");
		if (visualRoot != null)
			return visualRoot;

		Debug.LogWarning($"[CharacterSelectSceneBuilder] 무기 버튼 이벤트 연결 실패: {buttonName}");
		return null;
	}

	// ── 헬퍼: 이미지 슬라이드 ───────────────────────────────────────

	/// <summary>
	/// 스프라이트(있으면) 원본 비율 영역 또는 색상 플레이스홀더를 배치한 슬라이드 GO를 반환.
	/// 자막바는 이미지 위에 반투명으로 뜬다.
	/// </summary>
	static GameObject BuildImageSlide(RectTransform parent, string name,
		Color holderColor, string labelText, string spritePath = null)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var rt = go.AddComponent<RectTransform>();
		SBU.Stretch(rt);

		var sprite = LoadStorySlideSprite(spritePath);

		if (sprite != null)
		{
			var visualRoot = CreateSpriteAspectFitRoot(go.transform, "CutsceneVisualRoot", sprite);

			var imgGo = new GameObject("CutsceneImage");
			imgGo.transform.SetParent(visualRoot, false);
			var imgRt = imgGo.AddComponent<RectTransform>();
			SBU.Stretch(imgRt);
			var img = imgGo.AddComponent<Image>();
			img.sprite = sprite;
			img.color = Color.white;
			img.preserveAspect = false;
			img.raycastTarget = false;
		}
		else
		{
			var imgRt = SBU.CreateImage(go.transform, "PlaceholderImage", holderColor, false);
			SBU.Stretch(imgRt);

			var label = SBU.CreateTMPText(go.transform, "PlaceholderLabel", labelText,
				60, new Color(1f, 1f, 1f, 0.6f),
				TextAlignmentOptions.Center, FontStyles.Bold);
			var labelRt = label.GetComponent<RectTransform>();
			SBU.Stretch(labelRt);
		}

		return go;
	}

	// ── 헬퍼: 무기 선택 슬라이드 ────────────────────────────────────

	/// <summary>무기 선택 UI가 포함된 슬라이드 GO를 반환</summary>
	static GameObject BuildWeaponSelectSlide(RectTransform parent, string bgSpritePath)
	{
		var go = new GameObject("Slide3TopContent");
		go.transform.SetParent(parent, false);
		var rt = go.AddComponent<RectTransform>();
		SBU.Stretch(rt);

		// 배경: 실제 이미지가 있으면 사용, 없으면 색상 폴백
		var bgSprite = LoadStorySlideSprite(bgSpritePath);
		Transform buttonParent = go.transform;

		if (bgSprite != null)
		{
			var visualRoot = CreateSpriteAspectFitRoot(go.transform, "WeaponVisualRoot", bgSprite);
			buttonParent = visualRoot;

			var bgGo = new GameObject("WeaponBackground");
			bgGo.transform.SetParent(visualRoot, false);
			var bgRt = bgGo.AddComponent<RectTransform>();
			SBU.Stretch(bgRt);
			var bgImg = bgGo.AddComponent<Image>();
			bgImg.sprite = bgSprite;
			bgImg.color = Color.white;
			bgImg.preserveAspect = false;
			bgImg.raycastTarget = false;
		}
		else
		{
			var bgImgRt = SBU.CreateImage(go.transform, "WeaponBgPlaceholder", WeaponBgColor, false);
			SBU.Stretch(bgImgRt);
		}

		// 무기 버튼 컨테이너 — 이미지 전체 영역 기준 배치
		var row = new GameObject("WeaponButtonsRow");
		row.transform.SetParent(buttonParent, false);
		var rowRt = row.AddComponent<RectTransform>();
		SBU.Stretch(rowRt);

		// 3개 무기 버튼 — 이미지 내 비석 중심 좌표 (앵커=배경 이미지 비율)
		// X 균등 간격 0.168, Y 동일, 중앙 비석 ≈ 0.504
		const float stoneY = 0.505f;
		var mahjongBtnGo = BuildWeaponButton(rowRt, "Btn_Mahjong", "마작패",
			MahjongColor, 0.336f, stoneY, 180, 200);
		var holdemBtnGo = BuildWeaponButton(rowRt, "Btn_Holdem", "플레잉카드",
			HoldemColor, 0.504f, stoneY, 180, 200);
		var diceBtnGo = BuildWeaponButton(rowRt, "Btn_Dice", "주사위",
			DiceColor, 0.672f, stoneY, 180, 200);

		ApplyMahjongButtonIcon(mahjongBtnGo);
		ApplyHoldemButtonIcon(holdemBtnGo);
		ApplyDiceButtonIcon(diceBtnGo);

		return go;
	}

	static Sprite LoadStorySlideSprite(string path)
	{
		if (string.IsNullOrEmpty(path))
			return null;

		if (!File.Exists(path))
		{
			Debug.LogWarning($"[CharacterSelectSceneBuilder] 스토리 스프라이트 없음: {path}");
			return null;
		}

		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
		SBU.EnsurePixelSprite(path);

		var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
		if (sprite != null)
			return sprite;

		var representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
		foreach (var asset in representations)
		{
			if (asset is Sprite subSprite)
				return subSprite;
		}

		Debug.LogWarning($"[CharacterSelectSceneBuilder] 스토리 스프라이트 로드 실패: {path}");
		return null;
	}

	static RectTransform CreateSpriteAspectFitRoot(Transform parent, string name, Sprite sprite)
	{
		var rootGo = new GameObject(name);
		rootGo.transform.SetParent(parent, false);
		var rt = rootGo.AddComponent<RectTransform>();
		SBU.Stretch(rt);

		var fitter = rootGo.AddComponent<AspectRatioFitter>();
		fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
		fitter.aspectRatio = sprite.rect.width / sprite.rect.height;
		return rt;
	}

	// ── 마작패 아이콘: 3만·4만 기본 표시, hover 시 적5만 쯔모 ─────
	static void ApplyMahjongButtonIcon(GameObject buttonGo)
	{
		if (buttonGo == null) return;
		var iconTrans = buttonGo.transform.Find("IconPlaceholder");
		if (iconTrans == null) return;
		var iconRt = iconTrans as RectTransform;

		// 투명 배경으로 전환 — 타일을 크게 겹쳐 얹음
		var placeholderImg = iconTrans.GetComponent<Image>();
		if (placeholderImg != null) placeholderImg.color = new Color(1f, 1f, 1f, 0f);

		var tileDb = AssetDatabase.LoadAssetAtPath<MahjongTileSpriteDatabase>(MahjongTileDbPath);
		if (tileDb == null)
			Debug.LogWarning($"[CharacterSelectSceneBuilder] 마작패 DB 로드 실패: {MahjongTileDbPath}");

		Sprite man3Sprite = tileDb != null ? tileDb.GetMan(3) : null;
		Sprite man4Sprite = tileDb != null ? tileDb.GetMan(4) : null;
		Sprite redMan5Sprite = LoadSpriteForButtonIcon(MahjongRedManFivePath);

		var tileSize = new Vector2(96f, 144f);
		var tile3 = CreateIconSprite(iconRt, "Tile_Man3", man3Sprite,
			new Vector2(-54f, 4f), tileSize, 0f, new Color(0.88f, 0.80f, 0.64f, 1f));
		var tile4 = CreateIconSprite(iconRt, "Tile_Man4", man4Sprite,
			new Vector2(54f, 4f), tileSize, 0f, new Color(0.88f, 0.80f, 0.64f, 1f));
		var redFive = CreateIconSprite(iconRt, "Tile_RedMan5", redMan5Sprite,
			new Vector2(126f, 75f), tileSize, 18f, new Color(0.88f, 0.80f, 0.64f, 1f));
		redFive.gameObject.SetActive(false);

		var animator = AddWeaponIconAnimator(buttonGo, WeaponSelectIconAnimator.IconMode.Mahjong);
		SBU.SetField(animator, "mahjongTile3", tile3);
		SBU.SetField(animator, "mahjongTile4", tile4);
		SBU.SetField(animator, "mahjongRedFive", redFive);
		SBU.SetField(animator, "mahjongTile3HoverPosition", new Vector2(-102f, 2f));
		SBU.SetField(animator, "mahjongTile4HoverPosition", new Vector2(0f, 2f));
		SBU.SetField(animator, "mahjongRedFiveTargetPosition", new Vector2(102f, 2f));
	}

	// ── 홀덤 아이콘: 카드백 기본 표시, hover 시 A♠/K♥ 앞면 전개 ──
	static void ApplyHoldemButtonIcon(GameObject buttonGo)
	{
		if (buttonGo == null) return;
		var iconTrans = buttonGo.transform.Find("IconPlaceholder");
		if (iconTrans == null) return;
		var iconRt = iconTrans as RectTransform;

		var placeholderImg = iconTrans.GetComponent<Image>();
		if (placeholderImg != null) placeholderImg.color = new Color(1f, 1f, 1f, 0f);

		Sprite cardBack = LoadSpriteForButtonIcon(HoldemCardBackPath);
		Sprite aceSpades = LoadSpriteForButtonIcon(HoldemAceSpadesPath);
		Sprite kingHearts = LoadSpriteForButtonIcon(HoldemKingHeartsPath);

		var cardSize = new Vector2(102f, 144f);
		var cardBackRt = CreateIconSprite(iconRt, "Card_Back", cardBack,
			Vector2.zero, cardSize, 0f, HoldemColor);
		var aceRt = CreateIconSprite(iconRt, "Card_AS", aceSpades,
			Vector2.zero, cardSize, 0f, new Color(0.94f, 0.90f, 0.78f, 1f));
		var kingRt = CreateIconSprite(iconRt, "Card_KH", kingHearts,
			Vector2.zero, cardSize, 0f, new Color(0.94f, 0.86f, 0.78f, 1f));
		aceRt.gameObject.SetActive(false);
		kingRt.gameObject.SetActive(false);

		var animator = AddWeaponIconAnimator(buttonGo, WeaponSelectIconAnimator.IconMode.Holdem);
		SBU.SetField(animator, "holdemBackCard", cardBackRt);
		SBU.SetField(animator, "holdemAceSpadesCard", aceRt);
		SBU.SetField(animator, "holdemKingHeartsCard", kingRt);
		SBU.SetField(animator, "holdemLeftCardTarget", new Vector2(-60f, 8f));
		SBU.SetField(animator, "holdemRightCardTarget", new Vector2(60f, 8f));
	}

	static Sprite LoadSpriteForButtonIcon(string path)
	{
		if (!File.Exists(path))
		{
			Debug.LogWarning($"[CharacterSelectSceneBuilder] 아이콘 스프라이트 없음: {path}");
			return null;
		}

		var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
		if (sprite != null)
			return sprite;

		var representations = AssetDatabase.LoadAllAssetRepresentationsAtPath(path);
		foreach (var asset in representations)
		{
			if (asset is Sprite subSprite)
				return subSprite;
		}

		Debug.LogWarning($"[CharacterSelectSceneBuilder] 아이콘 스프라이트 로드 실패: {path}");
		return null;
	}

	static RectTransform CreateIconSprite(RectTransform parent, string name, Sprite sprite,
		Vector2 anchoredPosition, Vector2 size, float rotation, Color fallbackColor)
	{
		if (sprite == null)
			Debug.LogWarning($"[CharacterSelectSceneBuilder] {name} 스프라이트 로드 실패, 폴백 색상 사용");

		var itemGo = new GameObject(name);
		itemGo.transform.SetParent(parent, false);
		var rt = itemGo.AddComponent<RectTransform>();
		rt.anchorMin = new Vector2(0.5f, 0.5f);
		rt.anchorMax = new Vector2(0.5f, 0.5f);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = size;
		rt.anchoredPosition = anchoredPosition;
		rt.localEulerAngles = new Vector3(0f, 0f, rotation);

		var img = itemGo.AddComponent<Image>();
		img.sprite = sprite;
		img.color = sprite != null ? Color.white : fallbackColor;
		img.preserveAspect = true;
		img.raycastTarget = false;

		return rt;
	}

	static WeaponSelectIconAnimator AddWeaponIconAnimator(GameObject buttonGo,
		WeaponSelectIconAnimator.IconMode mode)
	{
		var animator = buttonGo.GetComponent<WeaponSelectIconAnimator>();
		if (animator == null)
			animator = buttonGo.AddComponent<WeaponSelectIconAnimator>();

		SBU.SetField(animator, "mode", mode);
		return animator;
	}

	// ── 주사위 아이콘: 실제 D6 프리팹을 RenderTexture로 정적 표시 ─
	static void ApplyDiceButtonIcon(GameObject buttonGo)
	{
		if (buttonGo == null) return;
		var iconTrans = buttonGo.transform.Find("IconPlaceholder");
		if (iconTrans == null) return;
		var iconRt = iconTrans as RectTransform;

		var placeholderImg = iconTrans.GetComponent<Image>();
		if (placeholderImg != null) placeholderImg.color = new Color(1f, 1f, 1f, 0f);

		var dicePrefab = LoadDiceIconPrefab();
		if (dicePrefab == null)
		{
			Debug.LogWarning($"[CharacterSelectSceneBuilder] 주사위 프리팹 로드 실패: {DiceMinePrefabPath}, {DiceFallbackPrefabPath}");
			if (placeholderImg != null) placeholderImg.color = DiceColor;
			return;
		}

		var renderTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32)
		{
			name = "DiceButtonIconRenderTexture",
			antiAliasing = 4,
		};

		var rawRt = SBU.CreateEmpty(iconRt, "DicePrefabRenderImage");
		SBU.Stretch(rawRt);
		rawRt.localScale = new Vector3(1.5f, 1.5f, 1f);
		var raw = rawRt.gameObject.AddComponent<RawImage>();
		raw.texture = renderTexture;
		raw.color = Color.white;
		raw.raycastTarget = false;

		var renderRoot = new GameObject("DiceButtonIconRenderRoot");
		renderRoot.transform.position = new Vector3(60f, 60f, 60f);

		var diceInstances = CreateDiceIconInstances(dicePrefab, renderRoot.transform);
		if (diceInstances == null || diceInstances.Length == 0)
			return;

		var diceBounds = FitDiceIconPrefabs(diceInstances, renderRoot.transform.position, 2.55f);
		CreateDiceIconGround(renderRoot.transform, diceBounds);
		CreateDiceIconLight(renderRoot.transform);
		CreateDiceIconCamera(renderRoot.transform, renderTexture, diceBounds);

		var diceTransforms = new Transform[diceInstances.Length];
		for (int i = 0; i < diceInstances.Length; i++)
			diceTransforms[i] = diceInstances[i].transform;

		var animator = AddWeaponIconAnimator(buttonGo, WeaponSelectIconAnimator.IconMode.Dice);
		SBU.SetField(animator, "diceTargets", diceTransforms);
	}

	static GameObject LoadDiceIconPrefab()
	{
		var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(DiceMinePrefabPath);
		if (prefab != null)
			return prefab;

		Debug.LogWarning($"[CharacterSelectSceneBuilder] 주사위 프리팹 없음, fallback 사용: {DiceMinePrefabPath}");
		return AssetDatabase.LoadAssetAtPath<GameObject>(DiceFallbackPrefabPath);
	}

	static GameObject[] CreateDiceIconInstances(GameObject dicePrefab, Transform parent)
	{
		var localPositions = new[]
		{
			new Vector3(-1.78f, -0.08f, 0.12f),
			new Vector3(0f, 0.20f, -0.22f),
			new Vector3(1.78f, -0.08f, 0.06f),
		};
		var localRotations = new[]
		{
			Quaternion.Euler(24f, -50f, 8f),
			Quaternion.Euler(30f, -32f, 0f),
			Quaternion.Euler(24f, -14f, -8f),
		};

		var diceInstances = new GameObject[localPositions.Length];
		for (int i = 0; i < diceInstances.Length; i++)
		{
			var diceInstance = PrefabUtility.InstantiatePrefab(dicePrefab) as GameObject;
			if (diceInstance == null)
			{
				Debug.LogWarning($"[CharacterSelectSceneBuilder] 주사위 프리팹 인스턴스 생성 실패: {dicePrefab.name}");
				for (int j = 0; j < i; j++)
				{
					if (diceInstances[j] != null)
						Object.DestroyImmediate(diceInstances[j]);
				}
				return null;
			}

			diceInstance.name = $"DiceButtonIconPrefab_{i + 1}";
			diceInstance.transform.SetParent(parent, false);
			diceInstance.transform.localPosition = localPositions[i];
			diceInstance.transform.localRotation = localRotations[i];
			PrepareStaticDiceIconPrefab(diceInstance);
			diceInstances[i] = diceInstance;
		}

		return diceInstances;
	}

	static void PrepareStaticDiceIconPrefab(GameObject diceInstance)
	{
		foreach (var rb in diceInstance.GetComponentsInChildren<Rigidbody>(true))
		{
			rb.useGravity = false;
			rb.isKinematic = true;
		}

		foreach (var collider in diceInstance.GetComponentsInChildren<Collider>(true))
			collider.enabled = false;
	}

	static Bounds FitDiceIconPrefabs(GameObject[] diceInstances, Vector3 targetCenter, float targetSize)
	{
		var bounds = CalculateDiceIconBounds(diceInstances, targetCenter);
		if (bounds.size == Vector3.zero)
			return new Bounds(targetCenter, Vector3.one);

		float maxDimension = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
		if (maxDimension > 0.0001f)
		{
			float scale = targetSize / maxDimension;
			foreach (var diceInstance in diceInstances)
			{
				if (diceInstance != null)
				{
					diceInstance.transform.localScale *= scale;
					diceInstance.transform.localPosition *= scale;
				}
			}
		}

		bounds = CalculateDiceIconBounds(diceInstances, targetCenter);
		var centerOffset = targetCenter - bounds.center;
		foreach (var diceInstance in diceInstances)
		{
			if (diceInstance != null)
				diceInstance.transform.position += centerOffset;
		}

		return CalculateDiceIconBounds(diceInstances, targetCenter);
	}

	static Bounds CalculateDiceIconBounds(GameObject[] diceInstances, Vector3 fallbackCenter)
	{
		var bounds = new Bounds(fallbackCenter, Vector3.zero);
		bool hasBounds = false;

		foreach (var diceInstance in diceInstances)
		{
			if (diceInstance == null)
				continue;

			var renderers = diceInstance.GetComponentsInChildren<Renderer>(true);
			foreach (var renderer in renderers)
			{
				if (!hasBounds)
				{
					bounds = renderer.bounds;
					hasBounds = true;
				}
				else
				{
					bounds.Encapsulate(renderer.bounds);
				}
			}
		}

		return hasBounds ? bounds : new Bounds(fallbackCenter, Vector3.zero);
	}

	static void CreateDiceIconGround(Transform parent, Bounds diceBounds)
	{
		var ground = GameObject.CreatePrimitive(PrimitiveType.Quad);
		ground.name = "DiceButtonIconGroundShadow";
		ground.transform.SetParent(parent, false);
		ground.transform.position = new Vector3(diceBounds.center.x, diceBounds.min.y - 0.02f, diceBounds.center.z);
		ground.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
		float groundWidth = Mathf.Max(1.45f, diceBounds.size.x * 1.10f);
		float groundDepth = Mathf.Max(0.45f, diceBounds.size.z * 0.85f);
		ground.transform.localScale = new Vector3(groundWidth, groundDepth, 1f);

		var collider = ground.GetComponent<Collider>();
		if (collider != null)
			Object.DestroyImmediate(collider);

		var shader = Shader.Find("Sprites/Default");
		if (shader != null)
		{
			var material = new Material(shader)
			{
				name = "DiceButtonIconGroundShadowMaterial",
				color = new Color(0f, 0f, 0f, 0.28f),
			};
			ground.GetComponent<Renderer>().sharedMaterial = material;
		}
	}

	static void CreateDiceIconLight(Transform parent)
	{
		var lightGo = new GameObject("DiceButtonIconLight");
		lightGo.transform.SetParent(parent, false);
		lightGo.transform.localPosition = new Vector3(-1.2f, 2.1f, -1.4f);
		lightGo.transform.localRotation = Quaternion.Euler(45f, 35f, 0f);
		var light = lightGo.AddComponent<Light>();
		light.type = LightType.Directional;
		light.intensity = 1.3f;
		light.color = new Color(1f, 0.96f, 0.88f, 1f);
	}

	static void CreateDiceIconCamera(Transform parent, RenderTexture renderTexture, Bounds diceBounds)
	{
		var cameraGo = new GameObject("DiceButtonIconCamera");
		cameraGo.transform.SetParent(parent, false);
		Vector3 target = diceBounds.center;
		cameraGo.transform.position = target + new Vector3(0f, 0.72f, -3.5f);
		cameraGo.transform.localRotation = Quaternion.LookRotation(
			target - cameraGo.transform.position,
			Vector3.up);

		var camera = cameraGo.AddComponent<Camera>();
		camera.clearFlags = CameraClearFlags.SolidColor;
		camera.backgroundColor = Color.clear;
		camera.orthographic = true;
		camera.orthographicSize = Mathf.Max(1.15f, Mathf.Max(diceBounds.size.x, diceBounds.size.y) * 0.58f);
		camera.nearClipPlane = 0.1f;
		camera.farClipPlane = 10f;
		camera.targetTexture = renderTexture;
	}

	/// <summary>
	/// 무기 버튼 1개 — 중앙 앵커 방식.
	/// anchorX/anchorY = 컨테이너 내 비석 중심 비율, w/h = sizeDelta (px @1920x864)
	/// </summary>
	static GameObject BuildWeaponButton(RectTransform parent, string name, string label,
		Color iconColor, float anchorX, float anchorY, float w, float h)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var rt = go.AddComponent<RectTransform>();
		rt.anchorMin = new Vector2(anchorX, anchorY);
		rt.anchorMax = new Vector2(anchorX, anchorY);
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.sizeDelta = new Vector2(w, h);
		rt.anchoredPosition = Vector2.zero;

		// 버튼 배경 — 투명 (비석 이미지가 보이도록), 호버 시 반투명 하이라이트
		var bgImg = go.AddComponent<Image>();
		bgImg.color = new Color(1f, 1f, 1f, 0f);

		var btn = go.AddComponent<Button>();
		btn.targetGraphic = bgImg;
		var cb = btn.colors;
		cb.normalColor      = new Color(1f, 1f, 1f, 0f);
		cb.highlightedColor = new Color(1f, 1f, 1f, 0.20f);
		cb.pressedColor     = new Color(1f, 1f, 1f, 0.35f);
		cb.selectedColor    = new Color(1f, 1f, 1f, 0.20f);
		btn.colors = cb;

		// 아이콘 플레이스홀더 — 비석 내부에 맞춤 (패딩 15%)
		var iconRt = SBU.CreateImage(go.transform, "IconPlaceholder", iconColor, false);
		iconRt.anchorMin = new Vector2(0.12f, 0.15f);
		iconRt.anchorMax = new Vector2(0.88f, 0.85f);
		iconRt.offsetMin = Vector2.zero;
		iconRt.offsetMax = Vector2.zero;

		// 라벨 — 비석 바로 아래, 부모 컨테이너에 중앙 앵커로 배치
		var labelTmp = SBU.CreateTMPText(parent, $"{name}_Label", label,
			36, LabelColor, TextAlignmentOptions.Center, FontStyles.Bold);
		var labelRt = labelTmp.GetComponent<RectTransform>();
		labelRt.anchorMin = new Vector2(anchorX, anchorY);
		labelRt.anchorMax = new Vector2(anchorX, anchorY);
		labelRt.pivot = new Vector2(0.5f, 1f);
		labelRt.sizeDelta = new Vector2(w * 1.2f, 50);
		labelRt.anchoredPosition = new Vector2(0, -(h * 0.5f + 8));

		ApplyWeaponButtonHover(go, bgImg, labelTmp, iconRt);

		return go;
	}

	static void ApplyWeaponButtonHover(GameObject buttonGo, Image targetImage, TMP_Text targetText, Transform animatedTarget)
	{
		var hover = buttonGo.GetComponent<UIHoverEffect>();
		if (hover == null)
			hover = buttonGo.AddComponent<UIHoverEffect>();

		SBU.SetField(hover, "animatedTarget", animatedTarget);
		SBU.SetField(hover, "targetImage", targetImage);
		SBU.SetField(hover, "targetText", targetText);
		SBU.SetField(hover, "fontSizeBoost", 3f);
		SBU.SetField(hover, "scaleFactor", 1.14f);
		SBU.SetField(hover, "transitionDuration", 0.14f);
		SBU.SetField(hover, "outlineColor", new Color(0.75f, 0.88f, 1f, 0.70f));
		SBU.SetField(hover, "outlineDistance", new Vector2(4f, 4f));
		SBU.SetField(hover, "shadowColor", new Color(0f, 0f, 0f, 0.35f));
		SBU.SetField(hover, "shadowDistance", new Vector2(5f, -5f));
		SBU.SetField(hover, "normalColor", LabelColor);
		SBU.SetField(hover, "hoverColor", Color.white);
	}
}
