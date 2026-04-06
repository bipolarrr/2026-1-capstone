using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// 메인 메뉴 전체 흐름 관리 (진입 연출, 씬 전환 연결점)
/// </summary>
public class MainMenuController : MonoBehaviour
{
	[Header("진입 연출 대상")]
	[SerializeField] private CanvasGroup logoGroup;
	[SerializeField] private CanvasGroup menuButtonsGroup;

	[Header("진입 연출 설정")]
	[SerializeField] private float introDuration = 0.6f;
	[SerializeField] private float staggerDelay = 0.15f;

	void Start()
	{
		StartCoroutine(PlayIntro());
	}

	private IEnumerator PlayIntro()
	{
		SetGroupAlpha(logoGroup, 0f);
		SetGroupAlpha(menuButtonsGroup, 0f);

		yield return new WaitForSeconds(0.1f);

		yield return StartCoroutine(FadeIn(logoGroup, introDuration));
		yield return new WaitForSeconds(staggerDelay);
		yield return StartCoroutine(FadeIn(menuButtonsGroup, introDuration));
	}

	private IEnumerator FadeIn(CanvasGroup group, float duration)
	{
		if (group == null)
			yield break;
		float elapsed = 0f;
		group.alpha = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			group.alpha = Mathf.Clamp01(elapsed / duration);
			yield return null;
		}
		group.alpha = 1f;
	}

	private void SetGroupAlpha(CanvasGroup group, float alpha)
	{
		if (group != null)
			group.alpha = alpha;
	}

	/// <summary>Play 버튼에서 호출. 캐릭터 선택 씬으로 이동.</summary>
	public void LoadGameScene()
	{
		SceneManager.LoadScene("CharacterSelect");
	}
}
