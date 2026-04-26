using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 바디 자동 스프라이트 애니메이션.
/// HP 20% 이하면 LowHP 루프, 그 외에는 Idle 루프를 재생한다.
/// Roll/Death 애니메이션은 PauseAuto()로 일시정지 후 재생이 끝나면 ResumeAuto() 호출.
/// SmallHit/StrongHit/Defense/Jump/Debuff 스프라이트는 빌더에서 주입해 두며,
/// 현재는 자동 재생 대상이 아니라 외부에서 접근만 가능하다.
/// </summary>
public class PlayerBodyAnimator : MonoBehaviour
{
	// MaxRedSlots(5) × 2 = 10 하프하트 기준 20%
	const int LowHpHalfHeartThreshold = 2;

	[SerializeField] Image playerBody;
	[SerializeField] float frameRate = 30f;
	[SerializeField] float idleFrameRate = 6.5f;
	[SerializeField] Sprite[] idleSprites;
	[SerializeField] Sprite[] lowHpSprites;
	[SerializeField] Sprite[] smallHitSprites;
	[SerializeField] Sprite[] strongHitSprites;
	[SerializeField] Sprite[] defenseSprites;
	[SerializeField] Sprite[] jumpSprites;
	[SerializeField] Sprite[] debuffSprites;
	[SerializeField] float smallHitScaleMultiplier = 1.5f;
	[SerializeField] bool smallHitHorizontalFlip = true;
	[SerializeField] int smallHitFrameStep = 2;
	[SerializeField] float strongHitScaleMultiplier = 1.41f;
	[SerializeField] bool strongHitHorizontalFlip = true;
	[SerializeField] int strongHitFrameStep = 1;

	public Sprite[] IdleSprites => idleSprites;
	public Sprite[] LowHpSprites => lowHpSprites;
	public Sprite[] SmallHitSprites => smallHitSprites;
	public Sprite[] StrongHitSprites => strongHitSprites;
	public Sprite[] DefenseSprites => defenseSprites;
	public Sprite[] JumpSprites => jumpSprites;
	public Sprite[] DebuffSprites => debuffSprites;
	public bool IsHorizontallyFlipped { get; private set; }

	float timer;
	int frame;
	bool lowHpActive;
	bool autoPaused;
	Coroutine hitRoutine;
	Vector3 originalHitScale;
	Vector3 originalHitEulerAngles;
	bool hasHitTransform;

	void Update()
	{
		if (autoPaused || playerBody == null)
			return;

		bool shouldLow = LowHpActiveCheck();
		if (shouldLow != lowHpActive)
		{
			lowHpActive = shouldLow;
			frame = 0;
			timer = 0f;
		}

		var clip = lowHpActive ? lowHpSprites : idleSprites;
		if (clip == null || clip.Length == 0)
			return;

		float effectiveRate = lowHpActive ? frameRate : idleFrameRate;
		timer += Time.deltaTime;
		float step = 1f / Mathf.Max(1f, effectiveRate);
		while (timer >= step)
		{
			timer -= step;
			frame = (frame + 1) % clip.Length;
		}

		if (clip[frame] != null)
			playerBody.sprite = clip[frame];
	}

	bool LowHpActiveCheck()
	{
		if (lowHpSprites == null || lowHpSprites.Length == 0)
			return false;
		var hearts = GameSessionManager.PlayerHearts;
		if (hearts == null)
			return false;
		if (!GameSessionManager.IsPlayerAlive)
			return false;
		return hearts.TotalHalfHearts <= LowHpHalfHeartThreshold;
	}

	public void PauseAuto()
	{
		autoPaused = true;
	}

	public void ResumeAuto()
	{
		autoPaused = false;
		frame = 0;
		timer = 0f;
		lowHpActive = LowHpActiveCheck();
	}

	public void SetHorizontalFlip(bool flipped)
	{
		IsHorizontallyFlipped = flipped;
		transform.localEulerAngles = flipped ? new Vector3(0f, 180f, 0f) : Vector3.zero;
	}

	public Coroutine PlaySmallHit()
	{
		if (smallHitSprites == null || smallHitSprites.Length == 0 || playerBody == null)
			return null;

		return PlayHit(smallHitSprites, smallHitScaleMultiplier, smallHitHorizontalFlip, smallHitFrameStep);
	}

	public Coroutine PlayStrongHit()
	{
		return PlayHit(strongHitSprites, strongHitScaleMultiplier, strongHitHorizontalFlip, strongHitFrameStep);
	}

	public Coroutine PlayHitByDamage(int damageHalfHearts)
	{
		return damageHalfHearts >= 4 ? PlayStrongHit() : PlaySmallHit();
	}

	Coroutine PlayHit(Sprite[] sprites, float scaleMultiplier, bool horizontalFlip, int frameStep)
	{
		if (sprites == null || sprites.Length == 0 || playerBody == null)
			return null;

		if (hitRoutine != null)
		{
			StopCoroutine(hitRoutine);
			RestoreHitTransform();
		}

		hitRoutine = StartCoroutine(PlayHitRoutine(sprites, scaleMultiplier, horizontalFlip, frameStep));
		return hitRoutine;
	}

	IEnumerator PlayHitRoutine(Sprite[] sprites, float scaleMultiplier, bool horizontalFlip, int frameStep)
	{
		PauseAuto();

		originalHitScale = transform.localScale;
		originalHitEulerAngles = transform.localEulerAngles;
		hasHitTransform = true;
		transform.localScale = originalHitScale * scaleMultiplier;
		if (horizontalFlip)
			transform.localEulerAngles = new Vector3(0f, 180f, 0f);

		float frameDuration = 1f / Mathf.Max(1f, frameRate);
		int step = Mathf.Max(1, frameStep);
		for (int i = 0; i < sprites.Length; i += step)
		{
			if (sprites[i] != null)
				playerBody.sprite = sprites[i];
			yield return new WaitForSeconds(frameDuration);
		}

		RestoreHitTransform();
		ResumeAuto();
		hitRoutine = null;
	}

	void RestoreHitTransform()
	{
		if (!hasHitTransform)
			return;

		transform.localScale = originalHitScale;
		transform.localEulerAngles = originalHitEulerAngles;
		hasHitTransform = false;
	}
}
