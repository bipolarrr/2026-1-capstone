// Assets/Editor/CharacterSelectSceneBuilder.cs
// Unity 메뉴 → Tools → Build CharacterSelect Scene 으로 씬을 자동 생성합니다.
// 컷씬 슬라이드 + 무기 선택 화면을 프로그래밍 방식으로 빌드합니다.

using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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

	[MenuItem("Tools/Build CharacterSelect Scene")]
	public static void Build()
	{
		if (File.Exists(ScenePath))
		{
			bool overwrite = EditorUtility.DisplayDialog(
				"CharacterSelect 씬 생성",
				$"{ScenePath} 가 이미 존재합니다. 덮어쓰시겠습니까?",
				"덮어쓰기", "취소");
			if (!overwrite)
				return;
		}

		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		// ── 카메라 ──────────────────────────────────────────────────
		var cameraGo = new GameObject("MainCamera");
		cameraGo.tag = "MainCamera";
		var cam = cameraGo.AddComponent<Camera>();
		cam.clearFlags = CameraClearFlags.SolidColor;
		cam.backgroundColor = new Color(0.09f, 0.09f, 0.15f, 1f);
		cam.orthographic = true;
		cam.orthographicSize = 5f;
		cameraGo.transform.position = new Vector3(0, 0, -10);
		cameraGo.AddComponent<AudioListener>();

		// ── Canvas ──────────────────────────────────────────────────
		var canvasGo = new GameObject("Canvas");
		var canvas = canvasGo.AddComponent<Canvas>();
		canvas.renderMode = RenderMode.ScreenSpaceOverlay;

		var scaler = canvasGo.AddComponent<CanvasScaler>();
		scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
		scaler.referenceResolution = new Vector2(1920, 1080);
		scaler.matchWidthOrHeight = 0.5f;

		canvasGo.AddComponent<GraphicRaycaster>();

		// ── EventSystem ─────────────────────────────────────────────
		var eventGo = new GameObject("EventSystem");
		eventGo.AddComponent<EventSystem>();
		eventGo.AddComponent<InputSystemUIInputModule>();

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
			SlideHolder1, "컷씬 1", "Assets/Mobs/Story_CutScene_0.png");

		// ── 슬라이드 2: 적들과 조우하게 되는데...! ────────────────────
		var slide2Top = BuildImageSlide(slidesContainer, "Slide2TopContent",
			SlideHolder2, "컷씬 2", "Assets/Mobs/Story_CutScene_1.png");
		slide2Top.SetActive(false);

		// ── 슬라이드 3: 무기 선택 ────────────────────────────────────
		var slide3Top = BuildWeaponSelectSlide(slidesContainer,
			"Assets/Mobs/Story_CutScene_2.png");
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
		var mahjongBtn = slide3Top.transform.Find("WeaponButtonsRow/Btn_Mahjong");
		var holdemBtn  = slide3Top.transform.Find("WeaponButtonsRow/Btn_Holdem");
		var diceBtn    = slide3Top.transform.Find("WeaponButtonsRow/Btn_Dice");

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
		SBU.EnsureDirectory("Assets/Scenes");
		EditorSceneManager.SaveScene(scene, ScenePath);
		SBU.AddSceneToBuildSettings(ScenePath);

		EditorUtility.DisplayDialog("완료",
			$"CharacterSelect 씬이 생성되었습니다.\n{ScenePath}", "확인");
	}

	// ── 헬퍼: 이미지 슬라이드 ───────────────────────────────────────

	/// <summary>
	/// 전체 화면 영역에 스프라이트(있으면) 또는 색상 플레이스홀더를 배치한 슬라이드 GO를 반환.
	/// 자막바가 이미지 위에 반투명으로 뜨므로 이미지는 화면 전체를 채운다.
	/// </summary>
	static GameObject BuildImageSlide(RectTransform parent, string name,
		Color holderColor, string labelText, string spritePath = null)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var rt = go.AddComponent<RectTransform>();
		SBU.Stretch(rt);

		// 스프라이트 로드 시도
		Sprite sprite = null;
		if (!string.IsNullOrEmpty(spritePath) && System.IO.File.Exists(spritePath))
		{
			SBU.EnsurePixelSprite(spritePath);
			sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
		}

		if (sprite != null)
		{
			var imgGo = new GameObject("CutsceneImage");
			imgGo.transform.SetParent(go.transform, false);
			var imgRt = imgGo.AddComponent<RectTransform>();
			SBU.Stretch(imgRt);
			var img = imgGo.AddComponent<Image>();
			img.sprite = sprite;
			img.color = Color.white;
			img.preserveAspect = true;
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
	static GameObject BuildWeaponSelectSlide(RectTransform parent, string bgSpritePath = null)
	{
		var go = new GameObject("Slide3TopContent");
		go.transform.SetParent(parent, false);
		var rt = go.AddComponent<RectTransform>();
		SBU.Stretch(rt);

		// 배경: 실제 이미지가 있으면 사용, 없으면 색상 폴백
		Sprite bgSprite = null;
		if (!string.IsNullOrEmpty(bgSpritePath) && System.IO.File.Exists(bgSpritePath))
		{
			SBU.EnsurePixelSprite(bgSpritePath);
			bgSprite = AssetDatabase.LoadAssetAtPath<Sprite>(bgSpritePath);
		}

		if (bgSprite != null)
		{
			var bgGo = new GameObject("WeaponBackground");
			bgGo.transform.SetParent(go.transform, false);
			var bgRt = bgGo.AddComponent<RectTransform>();
			SBU.Stretch(bgRt);
			var bgImg = bgGo.AddComponent<Image>();
			bgImg.sprite = bgSprite;
			bgImg.color = Color.white;
			bgImg.preserveAspect = true;
			bgImg.raycastTarget = false;
		}
		else
		{
			var bgImgRt = SBU.CreateImage(go.transform, "WeaponBgPlaceholder", WeaponBgColor, false);
			SBU.Stretch(bgImgRt);
		}

		// 무기 버튼 컨테이너 — 이미지 전체 영역 기준 배치
		var row = new GameObject("WeaponButtonsRow");
		row.transform.SetParent(go.transform, false);
		var rowRt = row.AddComponent<RectTransform>();
		SBU.Stretch(rowRt);

		// 3개 무기 버튼 — 이미지 내 비석 중심 좌표 (preserveAspect 꺼서 앵커=이미지 비율)
		// X 균등 간격 0.168, Y 동일, 중앙 비석 ≈ 0.504
		const float stoneY = 0.505f;
		var mahjongBtnGo = BuildWeaponButton(rowRt, "Btn_Mahjong", "마작패",
			MahjongColor, 0.336f, stoneY, 180, 200);
		BuildWeaponButton(rowRt, "Btn_Holdem", "플레잉카드",
			HoldemColor, 0.504f, stoneY, 180, 200);
		var diceBtnGo = BuildWeaponButton(rowRt, "Btn_Dice", "주사위",
			DiceColor, 0.672f, stoneY, 180, 200);

		ApplyMahjongButtonIcon(mahjongBtnGo);
		ApplyDiceButtonIcon(diceBtnGo);

		return go;
	}

	// ── 마작패 아이콘: 1통·1만·1삭·백 2x2 배치 ─────────────────────
	static void ApplyMahjongButtonIcon(GameObject buttonGo)
	{
		if (buttonGo == null) return;
		var iconTrans = buttonGo.transform.Find("IconPlaceholder");
		if (iconTrans == null) return;
		var iconRt = iconTrans as RectTransform;

		// 투명 배경으로 전환 — 타일 4장을 위에 얹음
		var placeholderImg = iconTrans.GetComponent<Image>();
		if (placeholderImg != null) placeholderImg.color = new Color(1f, 1f, 1f, 0f);

		const string tileDbPath = "Assets/Mahjong/MahjongTileSprites.asset";
		var tileDb = AssetDatabase.LoadAssetAtPath<MahjongTileSpriteDatabase>(tileDbPath);
		Sprite manSprite = tileDb != null ? tileDb.GetMan(1) : null;
		Sprite pinSprite = tileDb != null ? tileDb.GetPin(1) : null;
		Sprite souSprite = tileDb != null ? tileDb.GetSou(1) : null;

		const string haku = "Assets/Mahjong/w.png";
		Sprite hakuSprite = null;
		if (File.Exists(haku))
		{
			SBU.EnsurePixelSprite(haku);
			hakuSprite = AssetDatabase.LoadAssetAtPath<Sprite>(haku);
		}

		// 2x2 그리드: 좌상 1통, 우상 1만, 좌하 1삭, 우하 백
		PlaceMahjongTile(iconRt, "Tile_Pin1",  pinSprite,  new Vector2(0f, 0.5f), new Vector2(0.5f, 1f));
		PlaceMahjongTile(iconRt, "Tile_Man1",  manSprite,  new Vector2(0.5f, 0.5f), new Vector2(1f, 1f));
		PlaceMahjongTile(iconRt, "Tile_Sou1",  souSprite,  new Vector2(0f, 0f), new Vector2(0.5f, 0.5f));
		PlaceMahjongTile(iconRt, "Tile_Haku",  hakuSprite, new Vector2(0.5f, 0f), new Vector2(1f, 0.5f));
	}

	static void PlaceMahjongTile(RectTransform parent, string name, Sprite sprite,
		Vector2 anchorMin, Vector2 anchorMax)
	{
		if (sprite == null)
		{
			Debug.LogWarning($"[CharacterSelectSceneBuilder] {name} 스프라이트 로드 실패 — 폴백 색상 사용");
		}
		var tileGo = new GameObject(name);
		tileGo.transform.SetParent(parent, false);
		var rt = tileGo.AddComponent<RectTransform>();
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		// 타일 간 약간의 패딩
		rt.offsetMin = new Vector2(3, 3);
		rt.offsetMax = new Vector2(-3, -3);
		var img = tileGo.AddComponent<Image>();
		img.sprite = sprite;
		img.color = sprite != null ? Color.white : new Color(0.85f, 0.80f, 0.65f, 1f);
		img.preserveAspect = true;
		img.raycastTarget = false;
	}

	// ── 주사위 아이콘: DiceRollSprites 단일 프레임 ────────────────
	static void ApplyDiceButtonIcon(GameObject buttonGo)
	{
		if (buttonGo == null) return;
		var iconTrans = buttonGo.transform.Find("IconPlaceholder");
		if (iconTrans == null) return;
		var img = iconTrans.GetComponent<Image>();
		if (img == null) return;

		const string dicePath = "Assets/Player/DiceRollSprites/0.png";
		if (!File.Exists(dicePath))
		{
			Debug.LogWarning($"[CharacterSelectSceneBuilder] 주사위 스프라이트 없음: {dicePath}");
			return;
		}
		SBU.EnsurePixelSprite(dicePath);
		var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(dicePath);
		if (sprite == null) return;

		img.sprite = sprite;
		img.color = Color.white;
		img.preserveAspect = true;
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

		return go;
	}
}
