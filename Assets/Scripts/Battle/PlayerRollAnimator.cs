using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주사위 굴림 시 플레이어 스프라이트 시퀀스를 재생한다.
/// 재생 전 PlayerBodyAnimator를 일시정지, 완료 후 ResumeAuto로 복귀한다.
/// </summary>
public class PlayerRollAnimator : MonoBehaviour
{
	[SerializeField] Image playerBody;
	[SerializeField] Sprite[] rollSprites;
	[SerializeField] PlayerBodyAnimator bodyAnimator;
	[SerializeField] float bodyScaleMultiplier = 1f;
	[SerializeField] float frameRate = 30f;

	Coroutine currentAnim;
	Vector3 originalBodyScale;
	bool hasOriginalBodyScale;

	/// <summary>
	/// 굴림 애니메이션을 재생한다. 이미 재생 중이면 재시작.
	/// </summary>
	public void Play()
	{
		if (rollSprites == null || rollSprites.Length == 0 || playerBody == null)
			return;

		if (currentAnim != null)
		{
			StopCoroutine(currentAnim);
			RestoreBodyScale();
			if (bodyAnimator != null)
				bodyAnimator.ResumeAuto();
		}

		currentAnim = StartCoroutine(PlaySequence());
	}

	IEnumerator PlaySequence()
	{
		if (bodyAnimator != null)
			bodyAnimator.PauseAuto();

		var bodyTransform = playerBody.rectTransform;
		originalBodyScale = bodyTransform.localScale;
		hasOriginalBodyScale = true;
		bodyTransform.localScale = originalBodyScale * bodyScaleMultiplier;

		// 준비 동작 생략 (프레임 55부터), 한 프레임씩 건너뛰어 재생
		int startFrame = Mathf.Min(55, rollSprites.Length - 1);
		float frameDuration = 1f / Mathf.Max(1f, frameRate);
		for (int i = startFrame; i < rollSprites.Length; i += 2)
		{
			if (rollSprites[i] != null)
				playerBody.sprite = rollSprites[i];
			yield return new WaitForSeconds(frameDuration);
		}

		if (bodyAnimator != null)
			bodyAnimator.ResumeAuto();

		RestoreBodyScale();
		currentAnim = null;
	}

	void RestoreBodyScale()
	{
		if (!hasOriginalBodyScale || playerBody == null)
			return;

		playerBody.rectTransform.localScale = originalBodyScale;
		hasOriginalBodyScale = false;
	}
}
