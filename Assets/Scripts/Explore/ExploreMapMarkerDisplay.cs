using UnityEngine;

public sealed class ExploreMapMarkerDisplay : MonoBehaviour
{
	public static readonly Vector2 DefaultSize = new Vector2(58f, 28f);
	public static readonly Vector2 DefaultOffset = new Vector2(42f, 34f);

	[SerializeField] RectTransform root;

	public void ShowAt(ExploreMapNodeView node)
	{
		var rect = ResolveRoot();
		if (rect == null)
			return;

		Vector2 center = ExploreMapLayout.ResolveNodeCenter(node);
		rect.anchorMin = center;
		rect.anchorMax = center;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = DefaultSize;
		rect.anchoredPosition = DefaultOffset;
		rect.gameObject.SetActive(true);
	}

	public void ShowAt(ExploreMapNodeView node, Vector2 contentPosition)
	{
		var rect = ResolveRoot();
		if (rect == null)
			return;

		rect.anchorMin = Vector2.zero;
		rect.anchorMax = Vector2.zero;
		rect.pivot = new Vector2(0.5f, 0.5f);
		rect.sizeDelta = DefaultSize;
		rect.anchoredPosition = contentPosition + DefaultOffset;
		rect.gameObject.SetActive(true);
	}

	public void Hide()
	{
		var rect = ResolveRoot();
		if (rect != null)
			rect.gameObject.SetActive(false);
	}

	RectTransform ResolveRoot()
	{
		if (root == null)
			root = transform as RectTransform;
		return root;
	}
}
