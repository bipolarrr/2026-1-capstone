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

	public static void SetField(object target, string fieldName, object value)
	{
		if (target == null)
			return;
		var field = target.GetType().GetField(fieldName,
			BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
		if (field != null)
			field.SetValue(target, value);
		else
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

	/// <summary>1x1 흰색 픽셀 스프라이트 — Filled Image용 (둥근 모서리 없음)</summary>
	public static Sprite WhitePixelSprite()
	{
		var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
		tex.SetPixel(0, 0, Color.white);
		tex.Apply();
		return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
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

	/// <summary>스프라이트 임포트 공통 설정 (Sprite 타입 + Single 모드 + 메시 타입). reimport 필요 여부를 반환.</summary>
	static bool EnsureSpriteImport(string path, SpriteMeshType meshType)
	{
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
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
