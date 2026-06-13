using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(RectTransform))]
public sealed class ExploreMapNodeHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IScrollHandler
{
	[SerializeField] RectTransform animatedTarget;
	[SerializeField] Image outlineImage;
	[SerializeField] GameObject borderRoot;
	[SerializeField] GameObject labelRoot;
	[SerializeField] Outline iconOutline;
	[SerializeField] ScrollRect scrollRect;
	[SerializeField] Color iconOutlineColor = new Color(1f, 0.90f, 0.45f, 0.96f);
	[SerializeField] Vector2 iconOutlineDistance = new Vector2(4f, -4f);
	[SerializeField] float scaleFactor = 1.06f;
	[SerializeField] float transitionDuration = 0.10f;

	Vector3 originalScale = Vector3.one;
	Coroutine transition;
	bool hoverEnabled;
	bool initialized;
	bool labelAlwaysVisible;

	public bool IsHoverEnabled => hoverEnabled;

	void Awake()
	{
		CaptureOriginalScale();
		ResetHoverVisual();
	}

	void OnDisable()
	{
		SetHovered(false, true);
	}

	public void SetHoverEnabled(bool enabled)
	{
		hoverEnabled = enabled;
		if (!enabled)
			SetHovered(false, true);
		else
			SetHoverLabelActive(labelAlwaysVisible);
		DisableHoverRaycasts();
	}

	public void SetLabelAlwaysVisible(bool alwaysVisible)
	{
		labelAlwaysVisible = alwaysVisible;
		SetHoverLabelActive(alwaysVisible);
	}

	public void SetScrollTarget(ScrollRect target)
	{
		scrollRect = target;
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		if (!hoverEnabled)
			return;
		SetHovered(true, false);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		SetHovered(false, false);
	}

	public void OnScroll(PointerEventData eventData)
	{
		if (scrollRect == null)
			scrollRect = GetComponentInParent<ScrollRect>();
		if (scrollRect != null)
			scrollRect.OnScroll(eventData);
	}

	void CaptureOriginalScale()
	{
		originalScale = GetAnimatedTarget().localScale;
		initialized = true;
	}

	void ResetHoverVisual()
	{
		SetHoverVisualActive(false);
		DisableHoverRaycasts();
	}

	void SetHoverVisualActive(bool hovered)
	{
		ConfigureIconOutline(hovered);
		SetHoverLabelActive(labelAlwaysVisible || (hoverEnabled && hovered));
		if (borderRoot != null)
			borderRoot.SetActive(false);
		if (outlineImage != null)
			outlineImage.gameObject.SetActive(false);
	}

	void SetHoverLabelActive(bool active)
	{
		if (labelRoot != null)
			labelRoot.SetActive(active);
	}

	void DisableHoverRaycasts()
	{
		if (outlineImage != null)
			outlineImage.raycastTarget = false;
		if (iconOutline != null)
		{
			var outlineGraphic = iconOutline.GetComponent<Graphic>();
			if (outlineGraphic != null)
				outlineGraphic.raycastTarget = false;
		}

		if (borderRoot == null)
			return;

		var borderGraphics = borderRoot.GetComponentsInChildren<Graphic>(true);
		for (int i = 0; i < borderGraphics.Length; i++)
			if (borderGraphics[i] != null)
				borderGraphics[i].raycastTarget = false;
	}

	void SetHovered(bool hovered, bool immediate)
	{
		if (!initialized)
			CaptureOriginalScale();

		if (transition != null)
		{
			StopCoroutine(transition);
			transition = null;
		}

		var target = GetAnimatedTarget();
		if (target == null)
			return;
		Vector3 targetScale = hovered ? originalScale * scaleFactor : originalScale;
		SetHoverVisualActive(hovered);
		DisableHoverRaycasts();

		if (immediate || !Application.isPlaying || !gameObject.activeInHierarchy || transitionDuration <= 0f)
		{
			target.localScale = targetScale;
			return;
		}

		transition = StartCoroutine(AnimateScale(target, target.localScale, targetScale));
	}

	RectTransform GetAnimatedTarget()
	{
		return animatedTarget != null ? animatedTarget : transform as RectTransform;
	}

	void ConfigureIconOutline(bool enabled)
	{
		if (iconOutline == null)
			return;

		iconOutline.effectColor = iconOutlineColor;
		iconOutline.effectDistance = iconOutlineDistance;
		iconOutline.useGraphicAlpha = true;
		iconOutline.enabled = enabled;
	}

	IEnumerator AnimateScale(RectTransform target, Vector3 from, Vector3 to)
	{
		float elapsed = 0f;
		while (elapsed < transitionDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);
			target.localScale = Vector3.Lerp(from, to, t);
			yield return null;
		}

		target.localScale = to;
		transition = null;
	}
}
