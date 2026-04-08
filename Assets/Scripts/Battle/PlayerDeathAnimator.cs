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

	/// <summary>
	/// 사망 애니메이션을 재생한다.
	/// facingRight가 true이면 Y축 180도 회전(좌우반전)으로 오른쪽을 바라보고,
	/// false이면 원본 방향(왼쪽)을 유지한다.
	/// </summary>
	public IEnumerator PlayDeathSequence(bool facingRight)
	{
		if (deathSprites == null || deathSprites.Length == 0 || playerBody == null)
		{
			Debug.LogWarning("[PlayerDeathAnimator] deathSprites 또는 playerBody가 설정되지 않음");
			yield break;
		}

		// 호흡 애니메이션 정지
		var spriteAnim = playerBody.GetComponent<SpriteAnimator>();
		if (spriteAnim != null)
			spriteAnim.enabled = false;

		// 캐릭터 방향에 맞게 Y축 쿼터니언 회전으로 좌우반전
		// 스프라이트 원본이 왼쪽을 향하므로 오른쪽 전환 시 Y축 180도 회전
		float yAngle = facingRight ? 180f : 0f;
		playerBody.rectTransform.localRotation = Quaternion.Euler(0f, yAngle, 0f);

		// 스프라이트 시퀀스 재생 (30fps 기준)
		float frameDuration = 1f / 30f;
		for (int i = 0; i < deathSprites.Length; i++)
		{
			if (deathSprites[i] != null)
				playerBody.sprite = deathSprites[i];
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
}
