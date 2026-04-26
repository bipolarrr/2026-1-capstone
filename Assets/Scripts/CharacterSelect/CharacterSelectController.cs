using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.InputSystem;
using TMPro;

/// <summary>
/// 컷씬 슬라이드 진행 + 무기 선택을 관리한다.
/// 화면 클릭으로 다음 슬라이드, Skip 버튼 또는 Space 길게 누르기로 무기 선택으로 직행.
/// </summary>
public class CharacterSelectController : MonoBehaviour
{
	[Header("슬라이드 데이터")]
	[SerializeField] private CutsceneSlide[] slides;

	[Header("UI 참조")]
	[SerializeField] private TMP_Text subtitleText;
	[SerializeField] private GameObject clickCatcher;
	[SerializeField] private GameObject skipButton;
	[SerializeField] private CanvasGroup fadeGroup;

	private int currentSlideIndex;
	private bool isTransitioning;

	private float spaceHeldTime;
	private const float SpaceSkipThreshold = 0.5f;
	private const float FadeDuration = 0.15f;

	private const string MainMenuSceneName = "MainMenu";
	private const string GameSceneName = "GameExploreScene";

	void Start()
	{
		currentSlideIndex = 0;
		ShowSlide(0);
	}

	void Update()
	{
		var kb = Keyboard.current;
		if (kb == null) return;

		if (kb.spaceKey.isPressed)
		{
			spaceHeldTime += Time.deltaTime;
			if (spaceHeldTime >= SpaceSkipThreshold)
			{
				spaceHeldTime = 0f;
				SkipToWeaponSelect();
			}
		}

		if (kb.spaceKey.wasReleasedThisFrame)
			spaceHeldTime = 0f;
	}

	// ─── 버튼 이벤트 진입점 ───────────────────────────────────────

	/// <summary>전체 화면 클릭 캐쳐에 연결 — 다음 슬라이드로 진행</summary>
	public void AdvanceSlide()
	{
		if (isTransitioning) return;
		if (slides == null || slides.Length == 0) return;

		// 현재 슬라이드가 무기 선택이면 클릭 진행 불가
		if (slides[currentSlideIndex].isWeaponSelect) return;

		int nextIndex = currentSlideIndex + 1;
		if (nextIndex >= slides.Length) return;

		AudioManager.Play("UI_Click");
		StartCoroutine(TransitionToSlide(nextIndex));
	}

	/// <summary>Skip 버튼에 연결 — 마지막 슬라이드(무기 선택)로 즉시 이동</summary>
	public void SkipToWeaponSelect()
	{
		if (isTransitioning) return;
		if (slides == null || slides.Length == 0) return;

		int weaponSlideIndex = slides.Length - 1;
		if (currentSlideIndex == weaponSlideIndex) return;

		AudioManager.Play("UI_Click");
		StartCoroutine(TransitionToSlide(weaponSlideIndex));
	}

	/// <summary>뒤로 버튼에 연결 — 메인메뉴로 복귀</summary>
	public void OnBackClicked()
	{
		Debug.Log("[Cutscene] OnBackClicked → MainMenu");
		AudioManager.Play("UI_Back_NO");
		AudioManager.Play("Transition_2_Quit");
		SceneManager.LoadScene(MainMenuSceneName);
	}

	// ─── 무기 선택 진입점 (persistent listener용 개별 메서드) ──────

	public void OnWeaponSelected_Mahjong()
	{
		SelectWeaponAndStart(CharacterType.Mahjong);
	}

	public void OnWeaponSelected_Holdem()
	{
		SelectWeaponAndStart(CharacterType.Holdem);
	}

	public void OnWeaponSelected_Dice()
	{
		SelectWeaponAndStart(CharacterType.Dice);
	}

	// ─── 내부 로직 ───────────────────────────────────────────────

	private void SelectWeaponAndStart(CharacterType type)
	{
		Debug.Log($"[Cutscene] 무기 선택: {type} → {GameSceneName}");
		AudioManager.Play("UI_OK");
		AudioManager.Play("Transition_2");
		CharacterSelectionContext.SelectedCharacter = type;
		GameSessionManager.StartNewGame(type);
		SceneManager.LoadScene(GameSceneName);
	}

	private void ShowSlide(int index)
	{
		if (slides == null) return;

		currentSlideIndex = index;

		// 모든 슬라이드 topContent 비활성화
		for (int i = 0; i < slides.Length; i++)
		{
			if (slides[i].topContent != null)
				slides[i].topContent.SetActive(i == index);
		}

		// 자막 설정
		if (subtitleText != null)
			subtitleText.text = slides[index].subtitleText ?? "";

		// 무기 선택 슬라이드에서는 클릭 캐쳐 비활성화
		bool isWeapon = slides[index].isWeaponSelect;
		if (clickCatcher != null)
			clickCatcher.SetActive(!isWeapon);

		// 무기 선택 슬라이드에서는 Skip 버튼 숨기기
		if (skipButton != null)
			skipButton.SetActive(!isWeapon);
	}

	private IEnumerator TransitionToSlide(int targetIndex)
	{
		isTransitioning = true;

		// 페이드 아웃
		if (fadeGroup != null)
		{
			float elapsed = 0f;
			while (elapsed < FadeDuration)
			{
				elapsed += Time.deltaTime;
				fadeGroup.alpha = Mathf.Lerp(1f, 0f, elapsed / FadeDuration);
				yield return null;
			}
			fadeGroup.alpha = 0f;
		}

		ShowSlide(targetIndex);

		// 페이드 인
		if (fadeGroup != null)
		{
			float elapsed = 0f;
			while (elapsed < FadeDuration)
			{
				elapsed += Time.deltaTime;
				fadeGroup.alpha = Mathf.Lerp(0f, 1f, elapsed / FadeDuration);
				yield return null;
			}
			fadeGroup.alpha = 1f;
		}

		isTransitioning = false;
	}
}
