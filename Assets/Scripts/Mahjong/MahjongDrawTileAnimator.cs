using System.Collections;
using UnityEngine;

namespace Mahjong
{
	/// <summary>
	/// 새로 뽑은 쯔모패가 화면 아래에서 위(레이아웃 기본 위치)로 슬라이드 업.
	/// 이징은 인스펙터 노출 AnimationCurve로 조절.
	/// </summary>
	public class MahjongDrawTileAnimator : MonoBehaviour
	{
		[SerializeField] AnimationCurve curve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);
		[SerializeField] float duration = 0.28f;
		[SerializeField] float startOffsetY = -600f; // 픽셀 (캔버스 좌표). 음수 = 화면 아래.

		Coroutine running;

		public void Play(RectTransform target)
		{
			if (target == null) return;
			if (!isActiveAndEnabled) { Snap(target); return; }
			if (running != null) StopCoroutine(running);
			running = StartCoroutine(SlideUp(target));
		}

		IEnumerator SlideUp(RectTransform target)
		{
			// 한 프레임 대기: 부모 GridLayoutGroup이 anchoredPosition을 세팅한 직후의 값을 기준으로.
			yield return null;
			if (target == null) yield break;

			Vector2 finalPos = target.anchoredPosition;
			Vector2 startPos = finalPos + new Vector2(0f, startOffsetY);
			target.anchoredPosition = startPos;

			float t = 0f;
			while (t < duration)
			{
				if (target == null) yield break;
				t += Time.unscaledDeltaTime;
				float k = duration > 0f ? Mathf.Clamp01(t / duration) : 1f;
				float e = curve.Evaluate(k);
				target.anchoredPosition = Vector2.LerpUnclamped(startPos, finalPos, e);
				yield return null;
			}
			if (target != null) target.anchoredPosition = finalPos;
			running = null;
		}

		static void Snap(RectTransform target)
		{
			// 컴포넌트 비활성 시 그냥 그대로 둠.
		}
	}
}
