using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 범용 UI 호버 효과. 버튼, 카드, 선택지 등 어디든 붙여서 사용.
/// 마우스 오버 시 스케일 확대 + 텍스트 크기 증가 + 아웃라인 테두리 + 그림자가
/// 부드럽게 보간되며 전환된다.
/// </summary>
public class UIHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	[SerializeField] TMP_Text targetText;
	[SerializeField] Image targetImage;
	[SerializeField] float fontSizeBoost = 4f;
	[SerializeField] float scaleFactor = 1.05f;
	[SerializeField] float transitionDuration = 0.12f;
	[SerializeField] Color outlineColor = new Color(1f, 0.85f, 0.3f, 0.9f);
	[SerializeField] Vector2 outlineDistance = new Vector2(3f, 3f);
	[SerializeField] Color shadowColor = new Color(0f, 0f, 0f, 0.4f);
	[SerializeField] Vector2 shadowDistance = new Vector2(4f, -4f);
	[SerializeField] Color normalColor = Color.clear;
	[SerializeField] Color hoverColor = Color.clear;

	float originalFontSize;
	Vector3 originalScale;
	Outline outline;
	Shadow shadow;
	Coroutine transition;

	bool useTextColor;

	void Awake()
	{
		originalScale = transform.localScale;
		useTextColor = normalColor.a > 0f || hoverColor.a > 0f;

		if (targetText != null)
		{
			originalFontSize = targetText.fontSize;
			if (useTextColor)
				targetText.color = normalColor;
		}

		if (targetImage != null)
		{
			outline = targetImage.gameObject.AddComponent<Outline>();
			outline.effectColor = Color.clear;
			outline.effectDistance = outlineDistance;

			shadow = targetImage.gameObject.AddComponent<Shadow>();
			shadow.effectColor = Color.clear;
			shadow.effectDistance = shadowDistance;
		}
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		TransitionTo(true);
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		TransitionTo(false);
	}

	void TransitionTo(bool hovered)
	{
		if (transition != null)
			StopCoroutine(transition);
		transition = StartCoroutine(LerpTransition(hovered));
	}

	IEnumerator LerpTransition(bool hovered)
	{
		float targetFontSize = hovered ? originalFontSize + fontSizeBoost : originalFontSize;
		Vector3 targetScale = hovered ? originalScale * scaleFactor : originalScale;
		Color targetOutline = hovered ? outlineColor : Color.clear;
		Color targetShadow = hovered ? shadowColor : Color.clear;
		Color targetTextColor = hovered ? hoverColor : normalColor;

		float startFontSize = targetText != null ? targetText.fontSize : 0f;
		Vector3 startScale = transform.localScale;
		Color startOutline = outline != null ? outline.effectColor : Color.clear;
		Color startShadow = shadow != null ? shadow.effectColor : Color.clear;
		Color startTextColor = (useTextColor && targetText != null) ? targetText.color : Color.clear;

		float elapsed = 0f;
		while (elapsed < transitionDuration)
		{
			elapsed += Time.unscaledDeltaTime;
			float t = Mathf.SmoothStep(0f, 1f, elapsed / transitionDuration);

			transform.localScale = Vector3.Lerp(startScale, targetScale, t);

			if (targetText != null)
			{
				targetText.fontSize = Mathf.Lerp(startFontSize, targetFontSize, t);
				if (useTextColor)
					targetText.color = Color.Lerp(startTextColor, targetTextColor, t);
			}

			if (outline != null)
				outline.effectColor = Color.Lerp(startOutline, targetOutline, t);

			if (shadow != null)
				shadow.effectColor = Color.Lerp(startShadow, targetShadow, t);

			yield return null;
		}

		transform.localScale = targetScale;

		if (targetText != null)
		{
			targetText.fontSize = targetFontSize;
			if (useTextColor)
				targetText.color = targetTextColor;
		}

		if (outline != null)
			outline.effectColor = targetOutline;

		if (shadow != null)
			shadow.effectColor = targetShadow;

		transition = null;
	}
}
