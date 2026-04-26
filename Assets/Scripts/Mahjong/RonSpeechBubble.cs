using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Mahjong
{
	/// <summary>
	/// 적의 "론!" 연출용 임시 말풍선. 정식 말풍선 에셋 도입 전까지 반투명 네모로 대체.
	/// Show(anchor, text, duration)로 지정 시간만큼 띄우고 자동 숨김.
	/// </summary>
	public class RonSpeechBubble : MonoBehaviour
	{
		[SerializeField] RectTransform root;
		[SerializeField] TMP_Text label;
		[SerializeField] Vector2 offset = new Vector2(0f, 40f);

		Canvas parentCanvas;

		void Awake()
		{
			parentCanvas = GetComponentInParent<Canvas>();
			if (root == null) root = transform as RectTransform;
		}

		public IEnumerator ShowRoutine(RectTransform anchor, string text, float duration = 0.9f)
		{
			if (root == null) yield break;
			if (label != null) label.text = text;
			gameObject.SetActive(true);
			PositionAbove(anchor);
			yield return new WaitForSeconds(duration);
			gameObject.SetActive(false);
		}

		public void Hide() { gameObject.SetActive(false); }

		void PositionAbove(RectTransform anchor)
		{
			if (anchor == null || parentCanvas == null) return;
			var canvasRT = parentCanvas.transform as RectTransform;
			Vector3 worldTop = anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMax, 0f));
			Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
			Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldTop);
			Vector2 local;
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screen, cam, out local);
			root.anchorMin = root.anchorMax = new Vector2(0.5f, 0.5f);
			root.pivot = new Vector2(0.5f, 0f);
			root.anchoredPosition = local + offset;
		}
	}
}
