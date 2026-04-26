using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace Mahjong
{
	/// <summary>
	/// 손패/쯔모패 호버 효과 — Content 자식을 위로 띄우고, 강조 테두리·한글 이름 라벨을 토글.
	/// 비활성 상태(=버림패/도라)에서는 아무 동작도 하지 않음.
	/// </summary>
	public class MahjongTileHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
	{
		[SerializeField] RectTransform content;          // 위로 띄울 대상 (타일 시각 요소 묶음)
		[SerializeField] Image highlightBorder;          // 강조 테두리 (Image, 기본 비활성)
		[SerializeField] TMP_Text nameLabel;             // 위에 표시할 한글 이름 (TMP, 기본 비활성)
		[SerializeField] MahjongTileVisual tileVisual;   // 이름 추출용
		[SerializeField] GameObject hitboxExtender;      // 호버 시 활성 — 원래 자리에도 raycast가 잡히도록 보조

		[Header("Tuning")]
		[SerializeField, Range(0f, 1f)] float liftRatio = 1f / 3f;
		[SerializeField] float duration = 0.12f;
		[SerializeField] AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

		Coroutine running;
		float baseY;
		bool baseCaptured;

		void OnEnable()
		{
			CaptureBase();
		}

		void OnDisable()
		{
			if (running != null) { StopCoroutine(running); running = null; }
			if (content != null && baseCaptured)
			{
				var p = content.anchoredPosition;
				p.y = baseY;
				content.anchoredPosition = p;
			}
			if (highlightBorder != null) highlightBorder.gameObject.SetActive(false);
			if (nameLabel != null) nameLabel.gameObject.SetActive(false);
			if (hitboxExtender != null) hitboxExtender.SetActive(false);
		}

		void CaptureBase()
		{
			if (content == null) return;
			baseY = content.anchoredPosition.y;
			baseCaptured = true;
		}

		public void OnPointerEnter(PointerEventData eventData)
		{
			if (!isActiveAndEnabled || content == null) return;
			if (!baseCaptured) CaptureBase();

			if (highlightBorder != null) highlightBorder.gameObject.SetActive(true);
			if (nameLabel != null && tileVisual != null)
			{
				nameLabel.text = MahjongTileVisual.LabelFor(tileVisual.Data);
				nameLabel.gameObject.SetActive(true);
			}
			// 호버 동안만 원래 위치에도 raycast 캐처가 깔리도록 활성화 — lift로 인해 생긴
			// "빈 공간"에서도 OnPointerExit가 즉시 발생하지 않게 한다.
			if (hitboxExtender != null) hitboxExtender.SetActive(true);

			float lift = LiftAmount();
			StartTween(baseY + lift);
		}

		public void OnPointerExit(PointerEventData eventData)
		{
			if (content == null) return;
			if (highlightBorder != null) highlightBorder.gameObject.SetActive(false);
			if (nameLabel != null) nameLabel.gameObject.SetActive(false);
			if (hitboxExtender != null) hitboxExtender.SetActive(false);
			StartTween(baseY);
		}

		float LiftAmount()
		{
			var rt = transform as RectTransform;
			float h = rt != null ? rt.rect.height : 100f;
			return h * liftRatio;
		}

		void StartTween(float targetY)
		{
			if (running != null) StopCoroutine(running);
			running = StartCoroutine(TweenY(targetY));
		}

		IEnumerator TweenY(float targetY)
		{
			float startY = content.anchoredPosition.y;
			float t = 0f;
			while (t < duration)
			{
				t += Time.unscaledDeltaTime;
				float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
				float e = ease.Evaluate(k);
				var p = content.anchoredPosition;
				p.y = Mathf.LerpUnclamped(startY, targetY, e);
				content.anchoredPosition = p;
				yield return null;
			}
			var fp = content.anchoredPosition;
			fp.y = targetY;
			content.anchoredPosition = fp;
			running = null;
		}
	}
}
