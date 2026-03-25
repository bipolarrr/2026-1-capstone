using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

/// <summary>
/// 캐릭터 선택 씬의 전체 흐름을 관리한다.
/// 좌우 선택 내비게이션, 미리보기 갱신, 시작/뒤로 버튼 처리를 담당한다.
/// </summary>
public class CharacterSelectController : MonoBehaviour
{
	[Header("캐릭터 데이터 (Inspector에서 3개 입력)")]
	[SerializeField] private CharacterData[] characters;

	[Header("미리보기 — 애니메이션 (애니메이터 할당 시 우선 사용)")]
	[SerializeField] private Animator worldPreviewAnimator;
	[SerializeField] private SpriteRenderer worldPreviewSpriteRenderer;

	[Header("미리보기 — 정지 이미지 / 플레이스홀더")]
	[SerializeField] private Image previewFallbackImage;

	[Header("정보 텍스트")]
	[SerializeField] private TMP_Text characterNameText;
	[SerializeField] private TMP_Text conceptDescriptionText;
	[SerializeField] private TMP_Text attackDescriptionText;

	[Header("미구현 캐릭터 팝업")]
	[SerializeField] private SimplePopup unavailablePopup;
	[SerializeField] private TMP_Text unavailableMessageText;

	private int currentIndex;

	private const string MainMenuSceneName = "MainMenu";
	private const string GameSceneName = "GameScene";

	void Start()
	{
		currentIndex = 0;
		UpdateDisplay();
	}

	// ─── 버튼 이벤트 진입점 ───────────────────────────────────────

	/// <summary>좌측 화살표 버튼에 연결</summary>
	public void SelectPrevious()
	{
		if (characters == null || characters.Length == 0)
			return;

		currentIndex = (currentIndex - 1 + characters.Length) % characters.Length;
		UpdateDisplay();
	}

	/// <summary>우측 화살표 버튼에 연결</summary>
	public void SelectNext()
	{
		if (characters == null || characters.Length == 0)
			return;

		currentIndex = (currentIndex + 1) % characters.Length;
		UpdateDisplay();
	}

	/// <summary>시작 버튼에 연결. 미구현 캐릭터 선택 시 팝업 표시.</summary>
	public void OnStartClicked()
	{
		if (characters == null || characters.Length == 0)
		{
			Debug.LogWarning("[CharacterSelectController] 캐릭터 데이터가 비어 있습니다.");
			return;
		}

		var selected = characters[currentIndex];

		if (!selected.isAvailable)
		{
			ShowUnavailablePopup(selected.unavailableMessage);
			return;
		}

		CharacterSelectionContext.SelectedCharacter = selected.characterType;
		SceneManager.LoadScene(GameSceneName);
	}

	/// <summary>뒤로 버튼에 연결. 메인메뉴로 복귀.</summary>
	public void OnBackClicked()
	{
		SceneManager.LoadScene(MainMenuSceneName);
	}

	// ─── 내부 로직 ───────────────────────────────────────────────

	private void ShowUnavailablePopup(string message)
	{
		if (unavailableMessageText != null)
			unavailableMessageText.text = string.IsNullOrEmpty(message) ? "아직 개발되지 않음" : message;

		if (unavailablePopup != null)
			unavailablePopup.Open();
		else
			Debug.LogWarning("[CharacterSelectController] unavailablePopup이 연결되지 않았습니다.");
	}

	private void UpdateDisplay()
	{
		if (characters == null || characters.Length == 0)
			return;

		var data = characters[currentIndex];

		UpdateInfoTexts(data);
		UpdatePreview(data);
	}

	private void UpdateInfoTexts(CharacterData data)
	{
		if (characterNameText != null)
			characterNameText.text = data.displayName;

		if (conceptDescriptionText != null)
			conceptDescriptionText.text = data.conceptDescription;

		if (attackDescriptionText != null)
			attackDescriptionText.text = data.attackDescription;
	}

	private void UpdatePreview(CharacterData data)
	{
		bool hasAnimatorController = data.previewAnimatorController != null && worldPreviewAnimator != null;

		if (hasAnimatorController)
		{
			worldPreviewAnimator.runtimeAnimatorController = data.previewAnimatorController;

			if (worldPreviewSpriteRenderer != null)
				worldPreviewSpriteRenderer.gameObject.SetActive(true);

			if (previewFallbackImage != null)
				previewFallbackImage.gameObject.SetActive(false);

			return;
		}

		// 애니메이터 없음 → 정지 이미지 또는 플레이스홀더 표시
		if (worldPreviewSpriteRenderer != null)
			worldPreviewSpriteRenderer.gameObject.SetActive(false);

		if (previewFallbackImage == null)
			return;

		previewFallbackImage.gameObject.SetActive(true);

		if (data.previewSprite != null)
		{
			previewFallbackImage.sprite = data.previewSprite;
			previewFallbackImage.color = Color.white;
		}
		else
		{
			previewFallbackImage.sprite = null;
			previewFallbackImage.color = data.previewFallbackColor;
		}
	}
}
