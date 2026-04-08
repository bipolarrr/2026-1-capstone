using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주사위 굴림 시 플레이어 스프라이트 시퀀스를 재생한다.
/// 재생 완료 후 idle 스프라이트로 복귀하고 SpriteAnimator 호흡을 재개한다.
/// </summary>
public class PlayerRollAnimator : MonoBehaviour
{
	[SerializeField] Image playerBody;
	[SerializeField] Sprite idleSprite;
	[SerializeField] Sprite[] rollSprites;

	Coroutine currentAnim;

	/// <summary>
	/// 굴림 애니메이션을 재생한다. 이미 재생 중이면 재시작.
	/// </summary>
	public void Play()
	{
		if (rollSprites == null || rollSprites.Length == 0 || playerBody == null)
			return;

		if (currentAnim != null)
			StopCoroutine(currentAnim);

		currentAnim = StartCoroutine(PlaySequence());
	}

	IEnumerator PlaySequence()
	{
		// 호흡 애니메이션 일시 정지
		var spriteAnim = playerBody.GetComponent<SpriteAnimator>();
		if (spriteAnim != null)
			spriteAnim.enabled = false;

		// 준비 동작 생략 (프레임 55부터), 2배속 재생
		int startFrame = Mathf.Min(55, rollSprites.Length - 1);
		float frameDuration = 1f / 30f;
		for (int i = startFrame; i < rollSprites.Length; i += 2)
		{
			if (rollSprites[i] != null)
				playerBody.sprite = rollSprites[i];
			yield return new WaitForSeconds(frameDuration);
		}

		// idle 복귀
		if (idleSprite != null)
			playerBody.sprite = idleSprite;

		// 호흡 애니메이션 재개
		if (spriteAnim != null)
			spriteAnim.enabled = true;

		currentAnim = null;
	}
}
