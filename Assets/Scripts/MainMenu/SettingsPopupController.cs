using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Settings 팝업 내부 로직: BGM / SFX 슬라이더
/// </summary>
public class SettingsPopupController : MonoBehaviour
{
	[Header("슬라이더")]
	[SerializeField] private Slider bgmSlider;
	[SerializeField] private Slider sfxSlider;

	[Header("값 표시 레이블 (선택)")]
	[SerializeField] private TMP_Text bgmValueLabel;
	[SerializeField] private TMP_Text sfxValueLabel;

	void OnEnable()
	{
		AudioManager.LoadSettings();
		float bgmVolume = AudioManager.GetBgmVolume();
		float sfxVolume = AudioManager.GetSfxVolume();

		// 팝업이 열릴 때마다 현재 값 동기화
		if (bgmSlider != null)
		{
			bgmSlider.SetValueWithoutNotify(bgmVolume);
			UpdateBGMLabel(bgmVolume);
		}
		if (sfxSlider != null)
		{
			sfxSlider.SetValueWithoutNotify(sfxVolume);
			UpdateSFXLabel(sfxVolume);
		}
	}

	void Start()
	{
		if (bgmSlider != null)
			bgmSlider.onValueChanged.AddListener(OnBGMChanged);
		if (sfxSlider != null)
			sfxSlider.onValueChanged.AddListener(OnSFXChanged);
	}

	public void OnBGMChanged(float value)
	{
		AudioManager.SetBgmVolume(value);
		UpdateBGMLabel(value);
		Debug.Log($"[Settings] BGM 변경: {value:P0}");
	}

	public void OnSFXChanged(float value)
	{
		AudioManager.SetSfxVolume(value);
		UpdateSFXLabel(value);
		Debug.Log($"[Settings] SFX 변경: {value:P0}");
	}

	private void UpdateBGMLabel(float v)
	{
		if (bgmValueLabel != null)
			bgmValueLabel.text = Mathf.RoundToInt(v * 100) + "%";
	}

	private void UpdateSFXLabel(float v)
	{
		if (sfxValueLabel != null)
			sfxValueLabel.text = Mathf.RoundToInt(v * 100) + "%";
	}
}
