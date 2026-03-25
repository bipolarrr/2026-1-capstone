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

	// 실제 오디오 시스템 연결 전까지 값만 보관
	private float _bgmVolume = 0.8f;
	private float _sfxVolume = 0.8f;

	void OnEnable()
	{
		// 팝업이 열릴 때마다 현재 값 동기화
		if (bgmSlider != null)
		{
			bgmSlider.SetValueWithoutNotify(_bgmVolume);
			UpdateBGMLabel(_bgmVolume);
		}
		if (sfxSlider != null)
		{
			sfxSlider.SetValueWithoutNotify(_sfxVolume);
			UpdateSFXLabel(_sfxVolume);
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
		_bgmVolume = value;
		UpdateBGMLabel(value);
		Debug.Log($"BGM Volume: {value:F2}");
		// TODO: AudioManager.Instance.SetBGMVolume(value);
	}

	public void OnSFXChanged(float value)
	{
		_sfxVolume = value;
		UpdateSFXLabel(value);
		Debug.Log($"SFX Volume: {value:F2}");
		// TODO: AudioManager.Instance.SetSFXVolume(value);
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
