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

	public void OnPlayClicked()
	{
		if (menuController != null)
		{
			Debug.Log("[MainMenu] OnPlayClicked → CharacterSelect");
			menuController.LoadGameScene();
		}
		else
		{
			Debug.LogError("[MainMenu] OnPlayClicked 실패: menuController가 null");
		}
	}

	public void OnSettingsClicked()
	{
		if (settingsPopup != null)
		{
			Debug.Log("[MainMenu] OnSettingsClicked → 설정 팝업 열기");
			settingsPopup.Open();
		}
		else
		{
			Debug.LogWarning("[MainMenu] OnSettingsClicked 실패: settingsPopup이 null");
		}
	}

	public void OnCreditsClicked()
	{
		if (creditsPopup != null)
		{
			Debug.Log("[MainMenu] OnCreditsClicked → 크레딧 팝업 열기");
			creditsPopup.Open();
		}
		else
		{
			Debug.LogWarning("[MainMenu] OnCreditsClicked 실패: creditsPopup이 null");
		}
	}
}
