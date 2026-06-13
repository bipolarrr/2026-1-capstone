using UnityEngine;
using UnityEngine.UI;

public sealed class ExploreMapConnectionLineDisplay : MonoBehaviour
{
	const float BackStrokeThickness = 5f;
	const float ForegroundStrokeThickness = 3f;

	[SerializeField] Image lineImage;
	[SerializeField] RectTransform lineTransform;
	[SerializeField] ExploreMapEdgeGraphic foregroundStroke;
	[SerializeField] RectTransform foregroundTransform;

	public void Show(ExploreMapNodeView fromNode, ExploreMapNodeView toNode, RectTransform connectionRoot)
	{
		ResolveReferences();
		if (lineImage == null || lineTransform == null)
			return;

		lineImage.gameObject.SetActive(true);
		lineImage.raycastTarget = false;
		lineImage.color = ResolveConnectionBackColor();
		if (foregroundStroke != null)
		{
			foregroundStroke.gameObject.SetActive(true);
			foregroundStroke.raycastTarget = false;
			foregroundStroke.color = ResolveConnectionColor(fromNode, toNode);
		}
		ApplyLayout(
			ExploreMapLayout.ResolveNodeCenter(fromNode),
			ExploreMapLayout.ResolveNodeCenter(toNode),
			ResolveConnectionRootSize(connectionRoot));
	}

	public void Show(
		ExploreMapNodeView fromNode,
		ExploreMapNodeView toNode,
		Vector2 fromPosition,
		Vector2 toPosition)
	{
		ResolveReferences();
		if (lineImage == null || lineTransform == null)
			return;

		lineImage.gameObject.SetActive(true);
		lineImage.raycastTarget = false;
		lineImage.color = ResolveConnectionBackColor();
		if (foregroundStroke != null)
		{
			foregroundStroke.gameObject.SetActive(true);
			foregroundStroke.raycastTarget = false;
			foregroundStroke.color = ResolveConnectionColor(fromNode, toNode);
		}
		ApplyContentLayout(fromPosition, toPosition);
	}

	public void Hide()
	{
		ResolveReferences();
		if (lineImage != null)
		{
			lineImage.raycastTarget = false;
			lineImage.gameObject.SetActive(false);
		}
		if (foregroundStroke != null)
		{
			foregroundStroke.raycastTarget = false;
			foregroundStroke.gameObject.SetActive(false);
		}
	}

	public static Color ResolveConnectionColor(ExploreMapNodeView fromNode, ExploreMapNodeView toNode)
	{
		if (fromNode.IsCurrent && toNode.IsReachable)
			return new Color(0.24f, 0.86f, 1f, 0.96f);
		if (toNode.IsReachable)
			return new Color(1f, 0.78f, 0.22f, 0.92f);
		if (fromNode.IsCompleted && (toNode.IsCompleted || toNode.IsReachable))
			return new Color(0.86f, 0.58f, 0.24f, 0.86f);
		return new Color(0.34f, 0.28f, 0.22f, 0.72f);
	}

	void ApplyLayout(Vector2 from, Vector2 to, Vector2 rootSize)
	{
		Vector2 midpoint = (from + to) * 0.5f;
		lineTransform.anchorMin = midpoint;
		lineTransform.anchorMax = midpoint;
		lineTransform.pivot = new Vector2(0.5f, 0.5f);
		lineTransform.anchoredPosition = Vector2.zero;
		Vector2 scaledDelta = new Vector2((to.x - from.x) * rootSize.x, (to.y - from.y) * rootSize.y);
		lineTransform.sizeDelta = new Vector2(Mathf.Max(8f, scaledDelta.magnitude), BackStrokeThickness);
		lineTransform.localRotation = Quaternion.Euler(
			0f,
			0f,
			Mathf.Atan2(scaledDelta.y, scaledDelta.x) * Mathf.Rad2Deg);
		ApplyForegroundLayout();
	}

	void ApplyContentLayout(Vector2 from, Vector2 to)
	{
		Vector2 midpoint = (from + to) * 0.5f;
		Vector2 delta = to - from;
		lineTransform.anchorMin = Vector2.zero;
		lineTransform.anchorMax = Vector2.zero;
		lineTransform.pivot = new Vector2(0.5f, 0.5f);
		lineTransform.anchoredPosition = midpoint;
		lineTransform.sizeDelta = new Vector2(Mathf.Max(8f, delta.magnitude), BackStrokeThickness);
		lineTransform.localRotation = Quaternion.Euler(
			0f,
			0f,
			Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
		lineTransform.localScale = Vector3.one;
		ApplyForegroundLayout();
	}

	void ResolveReferences()
	{
		if (lineImage == null)
			lineImage = GetComponent<Image>();
		if (lineTransform == null && lineImage != null)
			lineTransform = lineImage.rectTransform;
		if (lineTransform == null)
			lineTransform = transform as RectTransform;
		EnsureForegroundStroke();
		if (lineImage != null)
			lineImage.raycastTarget = false;
	}

	static Vector2 ResolveConnectionRootSize(RectTransform connectionRoot)
	{
		Rect rect = connectionRoot != null ? connectionRoot.rect : default;
		float width = rect.width > 0f ? rect.width : 900f;
		float height = rect.height > 0f ? rect.height : 820f;
		return new Vector2(width, height);
	}

	static Color ResolveConnectionBackColor()
	{
		return new Color(0.10f, 0.07f, 0.045f, 0.90f);
	}

	void EnsureForegroundStroke()
	{
		if (foregroundStroke != null && foregroundTransform != null)
			return;

		var existing = transform.Find("ForegroundStroke");
		if (existing == null)
		{
			var go = new GameObject("ForegroundStroke", typeof(RectTransform));
			go.transform.SetParent(transform, false);
			existing = go.transform;
		}

		foregroundTransform = existing as RectTransform;
		if (foregroundTransform == null)
			return;

		foregroundStroke = existing.GetComponent<ExploreMapEdgeGraphic>();
		if (foregroundStroke == null)
			foregroundStroke = existing.gameObject.AddComponent<ExploreMapEdgeGraphic>();

		foregroundStroke.raycastTarget = false;
		ApplyForegroundLayout();
	}

	void ApplyForegroundLayout()
	{
		if (foregroundTransform == null)
			return;

		foregroundTransform.anchorMin = new Vector2(0f, 0.5f);
		foregroundTransform.anchorMax = new Vector2(1f, 0.5f);
		foregroundTransform.pivot = new Vector2(0.5f, 0.5f);
		foregroundTransform.anchoredPosition = Vector2.zero;
		foregroundTransform.sizeDelta = new Vector2(0f, ForegroundStrokeThickness);
		foregroundTransform.localRotation = Quaternion.identity;
		foregroundTransform.localScale = Vector3.one;
	}
}

[RequireComponent(typeof(RectTransform), typeof(CanvasRenderer))]
public sealed class ExploreMapEdgeGraphic : MaskableGraphic
{
	protected override void OnPopulateMesh(VertexHelper vh)
	{
		vh.Clear();

		Rect rect = rectTransform.rect;
		if (rect.width <= 0f || rect.height <= 0f)
			return;

		var vertex = UIVertex.simpleVert;
		vertex.color = color;

		vertex.position = new Vector2(rect.xMin, rect.yMin);
		vh.AddVert(vertex);
		vertex.position = new Vector2(rect.xMin, rect.yMax);
		vh.AddVert(vertex);
		vertex.position = new Vector2(rect.xMax, rect.yMax);
		vh.AddVert(vertex);
		vertex.position = new Vector2(rect.xMax, rect.yMin);
		vh.AddVert(vertex);

		vh.AddTriangle(0, 1, 2);
		vh.AddTriangle(2, 3, 0);
	}
}
