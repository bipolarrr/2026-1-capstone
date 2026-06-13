using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ExploreMapNodeDisplay : MonoBehaviour
{
	const float DefaultPlateSize = 64f;
	const float BossPlateSize = 74f;
	const float CurrentPlateSize = 86f;
	const float OutlinePadding = 8f;
	const float ShadowPadding = 12f;
	const float AccentSize = 17f;
	const float AccentOffsetRatio = 0.34f;
	const float IconSizeRatio = 1.25f;
	const float LabelWidth = 176f;
	const float LabelHeight = 44f;
	const float LabelOffset = 44f;

	[SerializeField] RectTransform root;
	[SerializeField] Graphic shadowGraphic;
	[SerializeField] Graphic outlineGraphic;
	[SerializeField] Graphic stateGraphic;
	[SerializeField] Graphic accentGraphic;
	[SerializeField] Button button;
	[SerializeField] TMP_Text iconLabel;
	[SerializeField] Image iconImage;
	[SerializeField] TMP_Text symbolLabel;
	[SerializeField] ExploreMapNodeHoverEffect hoverEffect;
	[SerializeField] TMP_Text title;
	[SerializeField] TMP_Text description;

	public Button Button
	{
		get
		{
			ResolveReferences();
			return button;
		}
	}

	public void Show(ExploreMapNodeView node, Sprite iconSprite)
	{
		ResolveReferences();
		var rect = ResolveRoot();
		if (rect == null)
			return;

		rect.gameObject.SetActive(true);
		ApplyLayout(rect, node);
		ApplyText(node);
		ApplyVisual(node, iconSprite);
		SetSelectable(node.IsSelectable);
	}

	public void Show(ExploreMapNodeView node, Sprite iconSprite, Vector2 contentPosition)
	{
		ResolveReferences();
		var rect = ResolveRoot();
		if (rect == null)
			return;

		rect.gameObject.SetActive(true);
		ApplyContentLayout(rect, node, contentPosition);
		ApplyText(node);
		ApplyVisual(node, iconSprite);
		SetSelectable(node.IsSelectable);
	}

	public void Hide()
	{
		ResolveReferences();
		SetSelectable(false);

		var rect = ResolveRoot();
		if (rect != null)
			rect.gameObject.SetActive(false);
	}

	public static Color ResolveIconColor(ExploreMapNodeView node)
	{
		if (node.IsReachable || node.IsCurrent)
			return Color.white;
		if (node.IsCompleted)
			return new Color(0.86f, 0.76f, 0.56f, 0.90f);
		return new Color(0.70f, 0.66f, 0.58f, 0.58f);
	}

	public static Color ResolveFillColor(ExploreMapNodeView node)
	{
		if (node.IsCurrent)
			return new Color(0.18f, 0.86f, 1f, 0.98f);
		if (node.IsReachable)
			return new Color(1f, 0.86f, 0.42f, 0.98f);
		if (node.IsCompleted)
			return new Color(0.62f, 0.42f, 0.22f, 0.94f);
		return new Color(0.34f, 0.30f, 0.25f, 0.84f);
	}

	void ApplyLayout(RectTransform rect, ExploreMapNodeView node)
	{
		Vector2 center = ExploreMapLayout.ResolveNodeCenter(node);
		rect.anchorMin = center;
		rect.anchorMax = center;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = ExploreMapLayout.ResolveNodeSize(node.Kind);
		rect.anchoredPosition = Vector2.zero;
	}

	void ApplyContentLayout(RectTransform rect, ExploreMapNodeView node, Vector2 contentPosition)
	{
		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.zero;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = ExploreMapLayout.ResolveNodeSize(node.Kind);
		rect.anchoredPosition = contentPosition;
		rect.localRotation = Quaternion.identity;
		rect.localScale = Vector3.one;
	}

	void ApplyText(ExploreMapNodeView node)
	{
		SetText(iconLabel, node.IconLabel);
		SetText(title, node.Title);
		SetText(description, node.Description);
		SetText(symbolLabel, ResolveFallbackSymbol(node.Kind));
		if (iconLabel != null)
			iconLabel.gameObject.SetActive(false);
	}

	void ApplyVisual(ExploreMapNodeView node, Sprite iconSprite)
	{
		float plateSize = ResolvePlateSize(node);
		ApplyPlateGraphic(shadowGraphic, ResolveShadowColor(), plateSize + ShadowPadding, new Vector2(3f, -3f), true);
		ApplyPlateGraphic(outlineGraphic, ResolveOutlineColor(), plateSize + OutlinePadding, Vector2.zero, true);
		ApplyPlateGraphic(stateGraphic, ResolveFillColor(node), plateSize, Vector2.zero, true);
		ApplyAccentGraphic(accentGraphic, ResolveFillColor(node), plateSize);
		ApplyIconImage(node, iconSprite, plateSize);
		ApplySymbolLabel(node, iconSprite, plateSize);
		ApplyTitleLabel(node);

		if (hoverEffect != null)
			hoverEffect.SetLabelAlwaysVisible(node.IsCurrent);
	}

	void SetSelectable(bool selectable)
	{
		var rect = ResolveRoot();
		if (rect != null)
		{
			var graphics = rect.GetComponentsInChildren<Graphic>(true);
			for (int i = 0; i < graphics.Length; i++)
				if (graphics[i] != null)
					graphics[i].raycastTarget = false;
		}

		if (button != null)
		{
			button.interactable = selectable;
			if (button.targetGraphic != null)
				button.targetGraphic.raycastTarget = selectable;
		}

		if (hoverEffect != null)
			hoverEffect.SetHoverEnabled(selectable);
	}

	void ApplyPlateGraphic(Graphic graphic, Color color, float size, Vector2 anchoredPosition, bool diamond)
	{
		if (graphic == null)
			return;

		var rect = graphic.rectTransform;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(size, size);
		rect.anchoredPosition = anchoredPosition;
		rect.localRotation = diamond ? Quaternion.Euler(0f, 0f, 45f) : Quaternion.identity;
		rect.localScale = Vector3.one;
		graphic.color = color;
		graphic.raycastTarget = false;
		graphic.gameObject.SetActive(color.a > 0f);
	}

	void ApplyAccentGraphic(Graphic graphic, Color color, float plateSize)
	{
		if (graphic == null)
			return;

		ApplyPlateGraphic(
			graphic,
			color,
			AccentSize,
			new Vector2(plateSize * AccentOffsetRatio, -plateSize * AccentOffsetRatio),
			true);
	}

	void ApplyIconImage(ExploreMapNodeView node, Sprite iconSprite, float plateSize)
	{
		if (iconImage == null)
			return;

		var rect = iconImage.rectTransform;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		float iconSize = plateSize * IconSizeRatio;
		rect.sizeDelta = new Vector2(iconSize, iconSize);
		rect.anchoredPosition = Vector2.zero;
		rect.localRotation = Quaternion.identity;
		rect.localScale = Vector3.one;
		iconImage.sprite = iconSprite;
		iconImage.color = iconSprite != null ? ResolveIconColor(node) : Color.clear;
		iconImage.preserveAspect = true;
		iconImage.raycastTarget = false;
		iconImage.gameObject.SetActive(iconSprite != null);
	}

	void ApplySymbolLabel(ExploreMapNodeView node, Sprite iconSprite, float plateSize)
	{
		if (symbolLabel == null)
			return;

		var rect = symbolLabel.rectTransform;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(plateSize, plateSize);
		rect.anchoredPosition = Vector2.zero;
		rect.localRotation = Quaternion.identity;
		rect.localScale = Vector3.one;
		symbolLabel.fontSize = Mathf.Max(30f, plateSize * 0.50f);
		symbolLabel.color = ResolveSymbolColor(node);
		symbolLabel.alignment = TextAlignmentOptions.Center;
		symbolLabel.raycastTarget = false;
		symbolLabel.gameObject.SetActive(iconSprite == null);
	}

	void ApplyTitleLabel(ExploreMapNodeView node)
	{
		if (title == null)
			return;

		var rect = title.rectTransform;
		rect.anchorMin = new Vector2(0.5f, 0.5f);
		rect.anchorMax = new Vector2(0.5f, 0.5f);
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = new Vector2(LabelWidth, LabelHeight);
		float y = node.Row >= ExploreMapPresentationPolicy.BossRow - 1 ? -LabelOffset : LabelOffset;
		rect.anchoredPosition = new Vector2(0f, y);
		rect.localRotation = Quaternion.identity;
		rect.localScale = Vector3.one;
		title.fontSize = 42f;
		title.enableAutoSizing = true;
		title.fontSizeMin = 30f;
		title.fontSizeMax = 42f;
		title.color = node.IsCurrent
			? new Color(0.76f, 0.98f, 1f, 1f)
			: new Color(1f, 0.90f, 0.56f, 1f);
		title.raycastTarget = false;
		title.gameObject.SetActive(node.IsCurrent);
	}

	void ResolveReferences()
	{
		if (root == null)
			root = transform as RectTransform;
		if (button == null)
			button = GetComponent<Button>();
		if (hoverEffect == null)
			hoverEffect = GetComponent<ExploreMapNodeHoverEffect>();
	}

	RectTransform ResolveRoot()
	{
		if (root == null)
			root = transform as RectTransform;
		return root;
	}

	static void SetText(TMP_Text target, string value)
	{
		if (target != null)
			target.text = value;
	}

	static float ResolvePlateSize(ExploreMapNodeView node)
	{
		if (node.IsCurrent)
			return CurrentPlateSize;
		if (node.Kind == ExploreMapNodeKind.Boss)
			return BossPlateSize;
		return DefaultPlateSize;
	}

	static Color ResolveOutlineColor()
	{
		return new Color(0.10f, 0.065f, 0.04f, 0.98f);
	}

	static Color ResolveShadowColor()
	{
		return new Color(0.04f, 0.025f, 0.015f, 0.58f);
	}

	static Color ResolveSymbolColor(ExploreMapNodeView node)
	{
		if (node.IsCurrent)
			return new Color(0.04f, 0.12f, 0.16f, 1f);
		if (node.IsReachable)
			return new Color(0.18f, 0.12f, 0.04f, 1f);
		if (node.IsCompleted)
			return new Color(0.92f, 0.80f, 0.52f, 0.92f);
		return new Color(0.78f, 0.72f, 0.62f, 0.72f);
	}

	static string ResolveFallbackSymbol(ExploreMapNodeKind kind)
	{
		switch (kind)
		{
			case ExploreMapNodeKind.Start:
				return "S";
			case ExploreMapNodeKind.Heal:
				return "+";
			case ExploreMapNodeKind.Shop:
				return "$";
			case ExploreMapNodeKind.Loot:
				return "*";
			case ExploreMapNodeKind.Boss:
				return "B";
			default:
				return "!";
		}
	}
}
