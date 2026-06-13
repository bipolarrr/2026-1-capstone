using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Holdem.UI
{
	public sealed class HoldemCardView : MonoBehaviour,
		IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
	{
		[SerializeField] Image cardImage;
		[SerializeField] TMP_Text cardLabel;
		[SerializeField] TMP_Text subLabel;
		[SerializeField] CanvasGroup canvasGroup;
		[SerializeField] Outline outline;
		[SerializeField] Shadow shadow;
		[SerializeField] float hoverLift = 16f;
		[SerializeField] float hoverScale = 1.08f;

		const float TopCropPixels = 4f;
		static readonly Dictionary<Sprite, Sprite> displaySpriteCache = new Dictionary<Sprite, Sprite>();

		RectTransform rectTransform;
		Vector2 baseAnchoredPosition;
		Vector3 baseScale = Vector3.one;
		Quaternion baseRotation = Quaternion.identity;
		bool pointerInside;
		bool interactableVisual;
		bool hasBasePose;
		bool legacySheenChecked;
		Coroutine hoverRoutine;
		Coroutine attentionRoutine;

		public RectTransform RectTransform
		{
			get
			{
				EnsureInitialized();
				return rectTransform;
			}
		}

		public void Bind(Image image, TMP_Text label, TMP_Text detailLabel)
		{
			cardImage = image;
			cardLabel = label;
			subLabel = detailLabel;
			EnsureInitialized();
		}

		void Awake()
		{
			EnsureInitialized();
		}

		void OnEnable()
		{
			EnsureInitialized();
			CaptureBasePose();
		}

		void EnsureInitialized()
		{
			if (rectTransform == null)
				rectTransform = GetComponent<RectTransform>();
			if (cardImage == null)
				cardImage = GetComponent<Image>();
			if (canvasGroup == null)
				canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
			if (outline == null)
				outline = GetComponent<Outline>();
			if (shadow == null)
				shadow = GetComponent<Shadow>();
			if (rectTransform != null && !hasBasePose)
			{
				baseAnchoredPosition = rectTransform.anchoredPosition;
				baseScale = rectTransform.localScale;
				baseRotation = rectTransform.localRotation;
				hasBasePose = true;
			}
			if (canvasGroup != null)
				canvasGroup.blocksRaycasts = true;
			DisableLegacyTopSheen();
			SetHighlight(false);
		}

		void DisableLegacyTopSheen()
		{
			if (legacySheenChecked)
				return;
			legacySheenChecked = true;

			var topSheen = transform.Find("TopSheen");
			if (topSheen != null)
				topSheen.gameObject.SetActive(false);
		}

		public void CaptureBasePose()
		{
			EnsureInitialized();
			if (rectTransform == null)
				return;

			baseAnchoredPosition = rectTransform.anchoredPosition;
			baseScale = rectTransform.localScale;
			baseRotation = rectTransform.localRotation;
			hasBasePose = true;
		}

		public void SetFace(Sprite sprite, Color fallbackColor, string mainText, string detailText, Color textColor)
		{
			EnsureInitialized();
			if (cardImage != null)
			{
				cardImage.sprite = GetDisplaySprite(sprite);
				cardImage.color = sprite != null ? Color.white : fallbackColor;
				cardImage.preserveAspect = sprite != null;
			}
			SetText(mainText, detailText, textColor, new Color(textColor.r, textColor.g, textColor.b, 0.78f));
		}

		public void SetBack(Sprite sprite, Color fallbackColor, string mainText, string detailText)
		{
			EnsureInitialized();
			if (cardImage != null)
			{
				cardImage.sprite = GetDisplaySprite(sprite);
				cardImage.color = sprite != null ? Color.white : fallbackColor;
				cardImage.preserveAspect = sprite != null;
			}
			SetText(mainText, detailText, new Color(1f, 0.82f, 0.32f, 1f), new Color(0.78f, 0.86f, 1f, 0.82f));
		}

		public static Sprite GetDisplaySprite(Sprite sprite)
		{
			if (sprite == null)
				return null;
			if (displaySpriteCache.TryGetValue(sprite, out var cached))
				return cached;

			var rect = sprite.rect;
			if (rect.height <= TopCropPixels + 1f || sprite.texture == null)
			{
				displaySpriteCache[sprite] = sprite;
				return sprite;
			}

			float crop = Mathf.Min(TopCropPixels, rect.height - 1f);
			var croppedRect = new Rect(rect.x, rect.y, rect.width, rect.height - crop);
			var pivot = new Vector2(
				Mathf.Clamp01(sprite.pivot.x / Mathf.Max(1f, croppedRect.width)),
				Mathf.Clamp01(sprite.pivot.y / Mathf.Max(1f, croppedRect.height)));
			var border = sprite.border;
			border.w = Mathf.Max(0f, border.w - crop);
			var cropped = Sprite.Create(
				sprite.texture,
				croppedRect,
				pivot,
				sprite.pixelsPerUnit,
				0,
				SpriteMeshType.FullRect,
				border);
			cropped.name = sprite.name + "_DisplayCrop";
			displaySpriteCache[sprite] = cropped;
			return cropped;
		}

		void SetText(string mainText, string detailText, Color mainColor, Color detailColor)
		{
			if (cardLabel != null)
			{
				cardLabel.text = mainText;
				cardLabel.color = mainColor;
			}
			if (subLabel != null)
			{
				subLabel.text = detailText;
				subLabel.color = detailColor;
			}
		}

		public void SetInteractableVisual(bool value)
		{
			interactableVisual = value;
			if (!value)
			{
				pointerInside = false;
				SetHighlight(false);
				ReturnToBasePose();
			}
		}

		public void SetDimmed(bool value)
		{
			EnsureInitialized();
			if (canvasGroup != null)
				canvasGroup.alpha = value ? 0.42f : 1f;
		}

		public void ShowImmediate(bool visible)
		{
			EnsureInitialized();
			ReturnToBasePose();
			gameObject.SetActive(visible);
			if (canvasGroup != null)
				canvasGroup.alpha = visible ? 1f : 0f;
		}

		public IEnumerator PlayDealIn(float delay, Vector2 fromOffset)
		{
			EnsureInitialized();
			CaptureBasePose();
			if (delay > 0f)
				yield return new WaitForSeconds(delay);

			if (canvasGroup != null)
				canvasGroup.alpha = 0f;
			if (rectTransform != null)
			{
				rectTransform.anchoredPosition = baseAnchoredPosition + fromOffset;
				rectTransform.localScale = baseScale * 0.82f;
				rectTransform.localRotation = baseRotation;
			}

			yield return LerpPose(0.26f, t =>
			{
				float eased = EaseOutBack(t);
				if (canvasGroup != null)
					canvasGroup.alpha = t;
				if (rectTransform != null)
				{
					rectTransform.anchoredPosition = Vector2.LerpUnclamped(
						baseAnchoredPosition + fromOffset, baseAnchoredPosition, eased);
					rectTransform.localScale = Vector3.LerpUnclamped(baseScale * 0.82f, baseScale, eased);
				}
			});
			ReturnToBasePose();
			if (canvasGroup != null)
				canvasGroup.alpha = 1f;
		}

		public IEnumerator PlayFlip(Action swapVisual)
		{
			EnsureInitialized();
			CaptureBasePose();
			yield return ScaleX(1f, 0.05f, 0.10f);
			swapVisual?.Invoke();
			yield return ScaleX(0.05f, 1f, 0.12f);
			yield return PlayPop(1.10f);
		}

		public IEnumerator PlayRedraw()
		{
			EnsureInitialized();
			CaptureBasePose();
			Vector2 start = baseAnchoredPosition;
			Vector2 outPos = start + new Vector2(0f, -40f);
			Vector2 inPos = start + new Vector2(0f, 58f);

			yield return LerpPose(0.12f, t =>
			{
				if (canvasGroup != null)
					canvasGroup.alpha = 1f - t;
				if (rectTransform != null)
					rectTransform.anchoredPosition = Vector2.Lerp(start, outPos, t);
			});
			if (rectTransform != null)
				rectTransform.anchoredPosition = inPos;
			yield return LerpPose(0.18f, t =>
			{
				float eased = EaseOutCubic(t);
				if (canvasGroup != null)
					canvasGroup.alpha = t;
				if (rectTransform != null)
					rectTransform.anchoredPosition = Vector2.Lerp(inPos, start, eased);
			});
			ReturnToBasePose();
			if (canvasGroup != null)
				canvasGroup.alpha = 1f;
		}

		public IEnumerator PlayPop(float targetScale = 1.08f)
		{
			EnsureInitialized();
			CaptureBasePose();
			yield return LerpPose(0.10f, t =>
			{
				if (rectTransform != null)
					rectTransform.localScale = Vector3.Lerp(baseScale, baseScale * targetScale, EaseOutCubic(t));
			});
			yield return LerpPose(0.12f, t =>
			{
				if (rectTransform != null)
					rectTransform.localScale = Vector3.Lerp(baseScale * targetScale, baseScale, EaseOutCubic(t));
			});
			ReturnToBasePose();
		}

		public IEnumerator PlayResultFeedback(bool success)
		{
			EnsureInitialized();
			if (success)
			{
				SetHighlight(true);
				yield return PlayPop(1.14f);
				yield return new WaitForSeconds(0.08f);
				SetHighlight(false);
				yield break;
			}

			yield return PlayShake(16f, 0.22f);
		}

		public IEnumerator PlayDefeatedDrop()
		{
			EnsureInitialized();
			CaptureBasePose();
			Vector2 targetPosition = baseAnchoredPosition + new Vector2(0f, -32f);
			yield return LerpPose(0.20f, t =>
			{
				float eased = EaseOutCubic(t);
				if (canvasGroup != null)
					canvasGroup.alpha = Mathf.Lerp(1f, 0.38f, eased);
				if (rectTransform != null)
				{
					rectTransform.anchoredPosition = Vector2.Lerp(baseAnchoredPosition, targetPosition, eased);
					rectTransform.localRotation = baseRotation * Quaternion.Euler(0f, 0f, Mathf.Lerp(0f, -10f, eased));
				}
			});
		}

		public IEnumerator PlayShake(float distance = 14f, float duration = 0.20f)
		{
			EnsureInitialized();
			CaptureBasePose();
			float elapsed = 0f;
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				float wave = Mathf.Sin(t * Mathf.PI * 8f) * (1f - t);
				if (rectTransform != null)
					rectTransform.anchoredPosition = baseAnchoredPosition + new Vector2(wave * distance, 0f);
				yield return null;
			}
			ReturnToBasePose();
		}

		IEnumerator ScaleX(float from, float to, float duration)
		{
			Vector3 scale = rectTransform != null ? rectTransform.localScale : Vector3.one;
			float y = Mathf.Approximately(scale.y, 0f) ? 1f : scale.y;
			float z = Mathf.Approximately(scale.z, 0f) ? 1f : scale.z;
			yield return LerpPose(duration, t =>
			{
				if (rectTransform != null)
					rectTransform.localScale = new Vector3(Mathf.Lerp(from, to, EaseInOut(t)), y, z);
			});
		}

		void ReturnToBasePose()
		{
			if (rectTransform == null)
				return;
			rectTransform.anchoredPosition = baseAnchoredPosition + (pointerInside && interactableVisual ? new Vector2(0f, hoverLift) : Vector2.zero);
			rectTransform.localScale = pointerInside && interactableVisual ? baseScale * hoverScale : baseScale;
			rectTransform.localRotation = baseRotation;
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (!interactableVisual)
				return;
			pointerInside = true;
			AnimateHover(true);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (!interactableVisual)
				return;
			pointerInside = false;
			AnimateHover(false);
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			if (!interactableVisual)
				return;
			if (attentionRoutine != null)
				StopCoroutine(attentionRoutine);
			attentionRoutine = StartCoroutine(PlayPop(1.12f));
		}

		void AnimateHover(bool active)
		{
			EnsureInitialized();
			if (hoverRoutine != null)
				StopCoroutine(hoverRoutine);
			hoverRoutine = StartCoroutine(HoverRoutine(active));
		}

		IEnumerator HoverRoutine(bool active)
		{
			Vector2 fromPosition = rectTransform != null ? rectTransform.anchoredPosition : baseAnchoredPosition;
			Vector3 fromScale = rectTransform != null ? rectTransform.localScale : baseScale;
			Vector2 toPosition = baseAnchoredPosition + (active ? new Vector2(0f, hoverLift) : Vector2.zero);
			Vector3 toScale = active ? baseScale * hoverScale : baseScale;
			SetHighlight(active);
			yield return LerpPose(0.12f, t =>
			{
				float eased = EaseOutCubic(t);
				if (rectTransform != null)
				{
					rectTransform.anchoredPosition = Vector2.Lerp(fromPosition, toPosition, eased);
					rectTransform.localScale = Vector3.Lerp(fromScale, toScale, eased);
				}
			});
		}

		void SetHighlight(bool active)
		{
			if (outline != null)
				outline.effectColor = active
					? new Color(1f, 0.86f, 0.32f, 0.95f)
					: new Color(0.12f, 0.08f, 0.02f, 0.55f);
			if (shadow != null)
			{
				shadow.effectColor = active
					? new Color(1f, 0.68f, 0.18f, 0.55f)
					: new Color(0f, 0f, 0f, 0.45f);
				shadow.effectDistance = active ? new Vector2(0f, -10f) : new Vector2(5f, -7f);
			}
		}

		static IEnumerator LerpPose(float duration, Action<float> apply)
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

		static float EaseInOut(float t)
		{
			t = Mathf.Clamp01(t);
			return t * t * (3f - 2f * t);
		}

		static float EaseOutCubic(float t)
		{
			t = Mathf.Clamp01(t);
			return 1f - Mathf.Pow(1f - t, 3f);
		}

		static float EaseOutBack(float t)
		{
			t = Mathf.Clamp01(t);
			const float c1 = 1.70158f;
			const float c3 = c1 + 1f;
			return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
		}
	}
}
