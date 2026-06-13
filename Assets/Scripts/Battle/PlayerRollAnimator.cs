using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주사위 굴림 시 플레이어 스프라이트 시퀀스를 재생한다.
/// 재생 전 PlayerBodyAnimator를 일시정지, Stop 호출 시 역재생 후 ResumeAuto로 복귀한다.
/// </summary>
public class PlayerRollAnimator : MonoBehaviour
{
	[SerializeField] Image playerBody;
	[SerializeField] Sprite[] rollSprites;
	[SerializeField] PlayerBodyAnimator bodyAnimator;
	[SerializeField] float bodyScaleMultiplier = 1f;
	[SerializeField] float frameRate = 30f;
	[SerializeField] int holdFrame = 42;

	Coroutine currentAnim;
	Vector3 originalBodyScale;
	bool hasOriginalBodyScale;

	/// <summary>
	/// 굴림 애니메이션을 시작한다. 42번 프레임까지 재생한 뒤 자세를 유지한다.
	/// </summary>
	public void Play()
	{
		if (rollSprites == null || rollSprites.Length == 0 || playerBody == null)
			return;

		StopCurrentAnimation(restoreScale: true);

		currentAnim = StartCoroutine(PlayLoop());
	}

	public void Stop()
	{
		if (rollSprites == null || rollSprites.Length == 0 || playerBody == null)
		{
			StopCurrentAnimation(restoreScale: true);
			RestoreBodyScale();
			if (bodyAnimator != null)
				bodyAnimator.ResumeAuto();
			return;
		}

		StopCurrentAnimation(restoreScale: false);
		currentAnim = StartCoroutine(ReverseToIdle());
	}

	IEnumerator PlayLoop()
	{
		if (bodyAnimator != null)
			bodyAnimator.PauseAuto();

		var bodyTransform = playerBody.rectTransform;
		originalBodyScale = bodyTransform.localScale;
		hasOriginalBodyScale = true;
		bodyTransform.localScale = originalBodyScale * bodyScaleMultiplier;

		float frameDuration = 1f / Mathf.Max(1f, frameRate);
		int lastFrame = LastRollFrame();
		for (int i = 0; i <= lastFrame; i++)
		{
			SetRollSprite(i);
			yield return new WaitForSeconds(frameDuration);
		}

		SetRollSprite(lastFrame);
		while (true)
			yield return null;
	}

	IEnumerator ReverseToIdle()
	{
		if (bodyAnimator != null)
			bodyAnimator.PauseAuto();

		EnsureScaledBody();

		float frameDuration = 1f / Mathf.Max(1f, frameRate);
		for (int i = LastRollFrame(); i >= 0; i--)
		{
			SetRollSprite(i);
			yield return new WaitForSeconds(frameDuration);
		}

		currentAnim = null;
		RestoreBodyScale();
		if (bodyAnimator != null)
			bodyAnimator.ResumeAuto();
	}

	void StopCurrentAnimation(bool restoreScale)
	{
		if (currentAnim == null)
			return;

		StopCoroutine(currentAnim);
		currentAnim = null;

		if (restoreScale)
			RestoreBodyScale();
	}

	void EnsureScaledBody()
	{
		if (playerBody == null)
			return;

		if (!hasOriginalBodyScale)
		{
			originalBodyScale = playerBody.rectTransform.localScale;
			hasOriginalBodyScale = true;
		}

		playerBody.rectTransform.localScale = originalBodyScale * bodyScaleMultiplier;
	}

	int LastRollFrame()
	{
		return Mathf.Clamp(holdFrame, 0, rollSprites.Length - 1);
	}

	void SetRollSprite(int index)
	{
		if (rollSprites[index] != null)
			SetPlayerSprite(rollSprites[index]);
	}

	void SetPlayerSprite(Sprite sprite)
	{
		if (bodyAnimator != null)
			bodyAnimator.SetSprite(sprite);
		else if (playerBody != null && sprite != null)
			playerBody.sprite = sprite;
	}

	void RestoreBodyScale()
	{
		if (!hasOriginalBodyScale || playerBody == null)
			return;

		playerBody.rectTransform.localScale = originalBodyScale;
		hasOriginalBodyScale = false;
	}
}
