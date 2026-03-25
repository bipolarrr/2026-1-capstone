using UnityEngine;

/// <summary>
/// Play / Settings / Credits 버튼 클릭 이벤트 처리
/// </summary>
public class MainMenuButtonHandler : MonoBehaviour
{
	[Header("컨트롤러 참조")]
	[SerializeField] private MainMenuController menuController;

	[Header("팝업 참조")]
	[SerializeField] private SimplePopup settingsPopup;
	[SerializeField] private SimplePopup creditsPopup;

	// ─── 버튼 OnClick 이벤트에 연결 ──────────────────────────
	public void OnPlayClicked()
	{
		if (menuController != null)
			menuController.LoadGameScene();
		else
			Debug.Log("Play clicked");
	}

	public void OnSettingsClicked()
	{
		if (settingsPopup != null)
			settingsPopup.Open();
		else
			Debug.LogWarning("Settings popup not assigned.");
	}

	public void OnCreditsClicked()
	{
		if (creditsPopup != null)
			creditsPopup.Open();
		else
			Debug.LogWarning("Credits popup not assigned.");
	}
}
