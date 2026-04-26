using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 회피(완벽 방어) 시 점프 스프라이트 + 발 밑 효과 + 수직 이동을 동기 재생한다.
/// QuickSlam과 병렬로 실행하므로 duration을 QuickSlam 길이에 맞춰 튜닝한다.
/// </summary>
public class PlayerJumpAnimator : MonoBehaviour
{
	[SerializeField] Image playerBody;
	[SerializeField] Image belowEffect;
	[SerializeField] PlayerBodyAnimator bodyAnimator;
	[SerializeField] Sprite[] jumpSprites;
	[SerializeField] Sprite[] belowSprites;

	// QuickSlam = 0.06(돌진) + 0.04(멈춤) + 0.1(복귀) = 0.2초.
	// 점프는 살짝 더 길게 잡아 적이 슬램 위치에 있을 때 플레이어가 위에 있도록 한다.
	[SerializeField] float jumpDuration = 0.3f;
	[SerializeField] float jumpHeight = 120f;

	Coroutine currentAnim;

	public bool IsPlaying => currentAnim != null;

	public void Play()
	{
		if (playerBody == null || jumpSprites == null || jumpSprites.Length == 0)
			return;

		if (currentAnim != null)
		{
			StopCoroutine(currentAnim);
			currentAnim = null;
		}

		currentAnim = StartCoroutine(PlaySequence());
	}

	IEnumerator PlaySequence()
	{
		if (bodyAnimator != null)
			bodyAnimator.PauseAuto();

		if (belowEffect != null)
			belowEffect.enabled = true;

		RectTransform bodyRt = playerBody.rectTransform;
		Vector2 originalPos = bodyRt.anchoredPosition;

		int bodyLen = jumpSprites.Length;
		int belowLen = belowSprites?.Length ?? 0;

		float elapsed = 0f;
		while (elapsed < jumpDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / jumpDuration);

			// 스프라이트 시퀀스 — 시간 t(0~1)로 프레임 인덱스 매핑
			int bodyIdx = Mathf.Clamp(Mathf.FloorToInt(t * bodyLen), 0, bodyLen - 1);
			if (jumpSprites[bodyIdx] != null)
				playerBody.sprite = jumpSprites[bodyIdx];

			if (belowEffect != null && belowLen > 0)
			{
				int belowIdx = Mathf.Clamp(Mathf.FloorToInt(t * belowLen), 0, belowLen - 1);
				if (belowSprites[belowIdx] != null)
					belowEffect.sprite = belowSprites[belowIdx];
			}

			// 수직 포물선 — sin 곡선으로 t=0에서 0, t=0.5에서 최대, t=1에서 0
			float lift = Mathf.Sin(t * Mathf.PI) * jumpHeight;
			bodyRt.anchoredPosition = originalPos + new Vector2(0f, lift);

			yield return null;
		}

		bodyRt.anchoredPosition = originalPos;

		if (belowEffect != null)
		{
			belowEffect.enabled = false;
			belowEffect.sprite = null;
		}

		if (bodyAnimator != null)
			bodyAnimator.ResumeAuto();

		currentAnim = null;
	}
}
