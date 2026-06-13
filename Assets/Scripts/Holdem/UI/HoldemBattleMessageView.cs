using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Holdem.UI
{
	public sealed class HoldemBattleMessageView : MonoBehaviour
	{
		[SerializeField] TMP_Text messageLabel;
		[SerializeField] CanvasGroup canvasGroup;
		[SerializeField] Image background;
		[SerializeField] Vector2 hiddenOffset = new Vector2(0f, -20f);

		RectTransform rectTransform;
		Vector2 shownPosition;
		bool hasShownPosition;
		Coroutine routine;

		public void Bind(TMP_Text label, CanvasGroup group, Image image)
		{
			messageLabel = label;
			canvasGroup = group;
			background = image;
			EnsureInitialized();
			HideImmediate();
		}

		void Awake()
		{
			EnsureInitialized();
		}

		void EnsureInitialized()
		{
			if (rectTransform == null)
				rectTransform = GetComponent<RectTransform>();
			if (canvasGroup == null)
				canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
			if (background == null)
				background = GetComponent<Image>();
			if (rectTransform != null && !hasShownPosition)
			{
				shownPosition = rectTransform.anchoredPosition;
				hasShownPosition = true;
			}
			if (canvasGroup != null)
				canvasGroup.blocksRaycasts = false;
			if (background != null)
				background.raycastTarget = false;
		}

		public void Show(string message, Color accentColor)
		{
			EnsureInitialized();
			if (routine != null)
				StopCoroutine(routine);
			routine = StartCoroutine(ShowRoutine(message, accentColor));
		}

		public void Hide()
		{
			EnsureInitialized();
			if (routine != null)
				StopCoroutine(routine);
			routine = StartCoroutine(HideRoutine());
		}

		public void HideImmediate()
		{
			EnsureInitialized();
			if (routine != null)
			{
				StopCoroutine(routine);
				routine = null;
			}
			if (canvasGroup != null)
				canvasGroup.alpha = 0f;
			if (rectTransform != null)
				rectTransform.anchoredPosition = shownPosition + hiddenOffset;
		}

		IEnumerator ShowRoutine(string message, Color accentColor)
		{
			if (messageLabel != null)
			{
				messageLabel.text = message;
				messageLabel.color = accentColor;
			}
			if (canvasGroup != null)
				canvasGroup.alpha = 0f;
			if (rectTransform != null)
				rectTransform.anchoredPosition = shownPosition + hiddenOffset;

			yield return Lerp(0.18f, t =>
			{
				float eased = EaseOutCubic(t);
				if (canvasGroup != null)
					canvasGroup.alpha = t;
				if (rectTransform != null)
					rectTransform.anchoredPosition = Vector2.Lerp(shownPosition + hiddenOffset, shownPosition, eased);
			});

			yield return Lerp(0.12f, t =>
			{
				if (rectTransform != null)
				{
					float scale = Mathf.Lerp(1.04f, 1f, EaseOutCubic(t));
					rectTransform.localScale = new Vector3(scale, scale, 1f);
				}
			});
			if (rectTransform != null)
				rectTransform.localScale = Vector3.one;
			routine = null;
		}

		IEnumerator HideRoutine()
		{
			float startAlpha = canvasGroup != null ? canvasGroup.alpha : 1f;
			Vector2 startPosition = rectTransform != null ? rectTransform.anchoredPosition : shownPosition;
			yield return Lerp(0.16f, t =>
			{
				float eased = EaseOutCubic(t);
				if (canvasGroup != null)
					canvasGroup.alpha = Mathf.Lerp(startAlpha, 0f, eased);
				if (rectTransform != null)
					rectTransform.anchoredPosition = Vector2.Lerp(startPosition, shownPosition + hiddenOffset, eased);
			});
			if (rectTransform != null)
				rectTransform.anchoredPosition = shownPosition + hiddenOffset;
			routine = null;
		}

		static IEnumerator Lerp(float duration, System.Action<float> apply)
		{
			if (duration <= 0f)
			{
				apply?.Invoke(1f);
				yield break;
			}

			float elapsed = 0f;
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				apply?.Invoke(Mathf.Clamp01(elapsed / duration));
				yield return null;
			}
			apply?.Invoke(1f);
		}

		static float EaseOutCubic(float t)
		{
			t = Mathf.Clamp01(t);
			return 1f - Mathf.Pow(1f - t, 3f);
		}
	}
}
