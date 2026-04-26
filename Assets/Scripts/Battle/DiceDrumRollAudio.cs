using UnityEngine;

/// <summary>
/// 두구두구 효과음 재생 훅. 현재는 스텁 — 나중에 AudioClip을 주입해 실제 재생.
/// 사용부는 Play/Stop만 호출하면 되도록 계약을 고정.
/// </summary>
public static class DiceDrumRollAudio
{
	private static AudioSource source;
	private static AudioClip   clip;

	/// <summary>나중에 씬 빌더나 컨트롤러가 호출해 오디오 리소스를 주입.</summary>
	public static void Configure(AudioSource audioSource, AudioClip drumRollClip)
	{
		source = audioSource;
		clip   = drumRollClip;
	}

	/// <summary>두구두구 시작. 리소스 미설정 시 조용히 no-op.</summary>
	public static void Play()
	{
		if (source == null || clip == null) return;
		if (source.isPlaying && source.clip == clip) return;
		source.clip = clip;
		source.loop = true;
		source.pitch = 2f;
		source.Play();
	}

	/// <summary>두구두구 정지. 리소스 미설정 시 조용히 no-op.</summary>
	public static void Stop()
	{
		if (source == null) return;
		if (source.isPlaying)
			source.Stop();
	}
}
