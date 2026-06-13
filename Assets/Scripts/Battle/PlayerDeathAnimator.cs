using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 플레이어 사망 시 스프라이트 시퀀스 재생 + 화면 어두워짐 + 메인메뉴 전환.
/// 빌더에서 playerBody Image, deathSprites 배열, screenDimmer Image를 주입받는다.
/// </summary>
public class PlayerDeathAnimator : MonoBehaviour
{
	[SerializeField] Image playerBody;
	[SerializeField] Sprite[] deathSprites;
	[SerializeField] Image screenDimmer;
	[SerializeField] PlayerBodyAnimator bodyAnimator;
	[SerializeField] float frameRate = 30f;

	/// <summary>
	/// 사망 애니메이션을 재생한다.
	/// </summary>
	public IEnumerator PlayDeathSequence()
	{
		if (deathSprites == null || deathSprites.Length == 0 || playerBody == null)
		{
			Debug.LogWarning("[PlayerDeathAnimator] deathSprites 또는 playerBody가 설정되지 않음");
			yield break;
		}

		// 바디 자동 애니메이션 정지
		if (bodyAnimator != null)
			bodyAnimator.PauseAuto();

		float frameDuration = 1f / Mathf.Max(1f, frameRate);
		for (int i = 0; i < deathSprites.Length; i++)
		{
			if (deathSprites[i] != null)
				SetPlayerSprite(deathSprites[i]);
			yield return new WaitForSeconds(frameDuration);
		}

		// 화면 어두워짐
		if (screenDimmer != null)
		{
			screenDimmer.gameObject.SetActive(true);
			float fadeDuration = 1.5f;
			float elapsed = 0f;
			while (elapsed < fadeDuration)
			{
				elapsed += Time.deltaTime;
				float alpha = Mathf.Lerp(0f, 0.85f, elapsed / fadeDuration);
				screenDimmer.color = new Color(0f, 0f, 0f, alpha);
				yield return null;
			}
			screenDimmer.color = new Color(0f, 0f, 0f, 0.85f);
		}

		yield return new WaitForSeconds(0.5f);
		SceneManager.LoadScene("MainMenu");
	}

	void SetPlayerSprite(Sprite sprite)
	{
		if (bodyAnimator != null)
			bodyAnimator.SetSprite(sprite);
		else if (playerBody != null && sprite != null)
			playerBody.sprite = sprite;
	}
}
