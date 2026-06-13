using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HeartContainer를 UI_Heart 스프라이트 슬롯으로 렌더링.
/// </summary>
public class HeartDisplay : MonoBehaviour
{
	[SerializeField] RectTransform slotRoot;
	[SerializeField] Image[] heartImages;
	[SerializeField] Sprite emptyHeartSprite;
	[SerializeField] Sprite halfHeartSprite;
	[SerializeField] Sprite fullHeartSprite;
	[SerializeField] Vector2 slotSize = new Vector2(88f, 80f);

	static readonly Color RedColor = Color.white;
	static readonly Color BlackColor = new Color(0.12f, 0.12f, 0.12f, 1f);
	static readonly Color BlueColor = new Color(0.45f, 0.75f, 1f, 1f);

	public void Refresh(HeartContainer hearts)
	{
		if (slotRoot == null)
			slotRoot = transform as RectTransform;

		if (slotRoot == null)
			return;

		if (hearts == null)
		{
			SetVisibleCount(0);
			return;
		}

		var slots = hearts.GetDisplaySlots();
		int redCount = 0;
		foreach (var (type, _) in slots)
		{
			if (type == HeartType.Red)
				redCount++;
		}

		int emptyRedCount = Mathf.Max(0, HeartContainer.MaxRedSlots - redCount);
		int visibleCount = slots.Count + emptyRedCount;
		EnsureSlotCount(visibleCount);

		int imageIndex = 0;
		foreach (var (type, full) in slots)
		{
			SetHeartImage(imageIndex, full ? fullHeartSprite : halfHeartSprite, ColorFor(type));
			imageIndex++;
		}

		for (int i = 0; i < emptyRedCount; i++)
		{
			SetHeartImage(imageIndex, emptyHeartSprite, RedColor);
			imageIndex++;
		}

		SetVisibleCount(visibleCount);
	}

	void EnsureSlotCount(int count)
	{
		if (heartImages == null)
			heartImages = new Image[0];

		if (heartImages.Length >= count)
			return;

		var next = new Image[count];
		for (int i = 0; i < heartImages.Length; i++)
			next[i] = heartImages[i];

		for (int i = heartImages.Length; i < count; i++)
			next[i] = CreateSlot(i);

		heartImages = next;
	}

	Image CreateSlot(int index)
	{
		var go = new GameObject($"HeartSlot_{index + 1}");
		go.transform.SetParent(slotRoot, false);

		var image = go.AddComponent<Image>();
		image.raycastTarget = false;
		image.preserveAspect = true;

		var rt = image.GetComponent<RectTransform>();
		rt.sizeDelta = slotSize;
		return image;
	}

	void SetHeartImage(int index, Sprite sprite, Color color)
	{
		if (heartImages == null || index < 0 || index >= heartImages.Length || heartImages[index] == null)
			return;

		var image = heartImages[index];
		image.sprite = sprite;
		image.color = color;
		image.enabled = sprite != null;
		image.gameObject.SetActive(true);
	}

	void SetVisibleCount(int count)
	{
		if (heartImages == null)
			return;

		for (int i = 0; i < heartImages.Length; i++)
		{
			if (heartImages[i] != null)
				heartImages[i].gameObject.SetActive(i < count);
		}
	}

	static Color ColorFor(HeartType type)
	{
		switch (type)
		{
			case HeartType.Black:
				return BlackColor;
			case HeartType.Blue:
				return BlueColor;
			default:
				return RedColor;
		}
	}
}
