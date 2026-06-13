using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 전역 효과음 재생 허브. 씬 빌더가 부트 오브젝트에 부착하고 클립을 주입하면,
/// 런타임 코드는 AudioManager.Play("이름")로 이름만 보고 재생할 수 있다.
/// DiceDrumRollAudio 패턴과 동일한 스태틱 파사드 + MonoBehaviour 레지스터 구조.
/// </summary>
public class AudioManager : MonoBehaviour
{
	[SerializeField] AudioClip[] clips;
	[SerializeField] AudioSource source;
	[SerializeField] AudioSource drumRollSource;
	[SerializeField] AudioClip drumRollClip;

	/// <summary>프로젝트 전역 기본 볼륨. 모든 AudioSource/PlayOneShot은 AudioListener를 통해 곱해진다.</summary>
	public const float GlobalVolume = 0.02f;
	const string SfxVolumeKey = "Settings.SfxVolume";
	const string BgmVolumeKey = "Settings.BgmVolume";
	const float DefaultSfxVolume = 0.8f;
	const float DefaultBgmVolume = 0.8f;

	static AudioManager instance;
	static readonly Dictionary<string, AudioClip> lookup = new Dictionary<string, AudioClip>();
	static float sfxVolume = DefaultSfxVolume;
	static float bgmVolume = DefaultBgmVolume;

	[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
	static void ApplyGlobalVolume()
	{
		LoadSettings();
		AudioListener.volume = GlobalVolume;
	}

	void Awake()
	{
		instance = this;
		lookup.Clear();
		if (clips != null)
		{
			foreach (var c in clips)
			{
				if (c == null) continue;
				lookup[c.name] = c;
			}
		}
		if (source == null)
			source = GetComponent<AudioSource>();
		ApplySfxVolumeToSources();
		if (drumRollSource != null && drumRollClip != null)
			DiceDrumRollAudio.Configure(drumRollSource, drumRollClip);
	}

	void OnDestroy()
	{
		if (instance == this)
		{
			instance = null;
			lookup.Clear();
		}
	}

	public static void Play(string clipName)
	{
		Play(clipName, 1f, 1f);
	}

	public static void Play(string clipName, float pitch)
	{
		Play(clipName, pitch, 1f);
	}

	public static void Play(string clipName, float pitch, float volume)
	{
		if (instance == null || instance.source == null) return;
		if (string.IsNullOrEmpty(clipName)) return;
		if (!lookup.TryGetValue(clipName, out var clip) || clip == null)
		{
			Debug.LogWarning($"[AudioManager] clip \"{clipName}\" 미등록");
			return;
		}
		var src = instance.source;
		src.pitch = pitch;
		src.PlayOneShot(clip, Mathf.Clamp01(volume));
	}

	public static void LoadSettings()
	{
		sfxVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, DefaultSfxVolume));
		bgmVolume = Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, DefaultBgmVolume));
		ApplySfxVolumeToSources();
	}

	public static void SetSfxVolume(float value)
	{
		sfxVolume = Mathf.Clamp01(value);
		PlayerPrefs.SetFloat(SfxVolumeKey, sfxVolume);
		PlayerPrefs.Save();
		ApplySfxVolumeToSources();
	}

	public static float GetSfxVolume()
	{
		return sfxVolume;
	}

	public static void SetBgmVolume(float value)
	{
		bgmVolume = Mathf.Clamp01(value);
		PlayerPrefs.SetFloat(BgmVolumeKey, bgmVolume);
		PlayerPrefs.Save();
	}

	public static float GetBgmVolume()
	{
		return bgmVolume;
	}

	static void ApplySfxVolumeToSources()
	{
		if (instance == null) return;
		if (instance.source != null)
			instance.source.volume = sfxVolume;
		if (instance.drumRollSource != null)
			instance.drumRollSource.volume = sfxVolume;
	}
}
