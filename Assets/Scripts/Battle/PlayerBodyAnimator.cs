using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 바디 스프라이트 애니메이션.
/// 상태형 스프라이트는 현재 상태에 맞춰 상시 재생하고, 이벤트형 스프라이트는 한 번 재생 후 상태형으로 복귀한다.
/// </summary>
public class PlayerBodyAnimator : MonoBehaviour
{
	enum BodyState
	{
		Idle,
		LowHp,
	}

	readonly struct LoopingSpriteMotion
	{
		public readonly Sprite[] sprites;
		public readonly int entryEndFrame;
		public readonly int loopStartFrame;
		public readonly int loopEndFrame;
		public readonly bool loopPingPong;
		public readonly bool exitReverseEntry;
		public readonly string label;

		public LoopingSpriteMotion(Sprite[] sprites, int entryEndFrame, int loopStartFrame, int loopEndFrame,
			bool loopPingPong, bool exitReverseEntry, string label)
		{
			this.sprites = sprites;
			this.entryEndFrame = entryEndFrame;
			this.loopStartFrame = loopStartFrame;
			this.loopEndFrame = loopEndFrame;
			this.loopPingPong = loopPingPong;
			this.exitReverseEntry = exitReverseEntry;
			this.label = label;
		}
	}

	// MaxRedSlots(5) × 2 = 10 하프하트 기준 20%
	const int LowHpHalfHeartThreshold = 2;
	const float DebugLoopSectionSeconds = 3f;
	const float LoopUntilStopped = -1f;

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
	[SerializeField] Sprite[] attackDisplaySprites;
	[SerializeField] Sprite[] deathDisplaySprites;
	[SerializeField] int smallHitFrameStep = 1;
	[SerializeField] int strongHitFrameStep = 1;
	[SerializeField] int defenseEntryEndFrame = 40;
	[SerializeField] int defenseLoopStartFrame = 40;
	[SerializeField] int defenseLoopEndFrame = 104;
	[SerializeField] int lowHpIntroEndFrame = 94;
	[SerializeField] int lowHpLoopStartFrame = 41;
	[SerializeField] int lowHpLoopEndFrame = 94;

	public Sprite[] IdleSprites => idleSprites;
	public Sprite[] LowHpSprites => lowHpSprites;
	public Sprite[] SmallHitSprites => smallHitSprites;
	public Sprite[] StrongHitSprites => strongHitSprites;
	public Sprite[] DefenseSprites => defenseSprites;
	public Sprite[] JumpSprites => jumpSprites;
	public Sprite[] DebuffSprites => debuffSprites;
	public float ActionFrameRate => frameRate;
	public int SmallHitFrameCount => smallHitSprites != null ? smallHitSprites.Length : 0;
	public int StrongHitFrameCount => strongHitSprites != null ? strongHitSprites.Length : 0;
	public bool IsActionPlaying => actionRoutine != null;
	public bool IsDefenseSessionActive => defenseSessionActive;

	float timer;
	int frame;
	BodyState autoState;
	bool autoStateInitialized;
	bool lowHpIntroPlaying;
	int lowHpLoopDirection = 1;
	bool autoPaused;
	Coroutine actionRoutine;
	bool defenseSessionActive;
	bool defenseSessionEnding;
	Vector2 baseBodySize;
	bool hasBaseBodySize;

	void Awake()
	{
		CaptureBaseBodySize();
	}

	void Update()
	{
		if (autoPaused || playerBody == null)
			return;

		EnsureAutoState(ResolveAutoState());
		AdvanceAutoState(Time.deltaTime);
		SetCurrentAutoStateSprite();
	}

	BodyState ResolveAutoState()
	{
		return LowHpActiveCheck() ? BodyState.LowHp : BodyState.Idle;
	}

	void EnsureAutoState(BodyState targetState)
	{
		if (autoStateInitialized && autoState == targetState)
			return;

		autoStateInitialized = true;
		autoState = targetState;
		timer = 0f;
		frame = 0;
		lowHpLoopDirection = 1;
		lowHpIntroPlaying = autoState == BodyState.LowHp;

		SetCurrentAutoStateSprite();
	}

	void AdvanceAutoState(float deltaTime)
	{
		var clip = CurrentAutoSprites();
		if (clip == null || clip.Length == 0)
			return;

		float effectiveRate = autoState == BodyState.LowHp ? frameRate : idleFrameRate;
		timer += deltaTime;
		float step = 1f / Mathf.Max(1f, effectiveRate);
		while (timer >= step)
		{
			timer -= step;
			AdvanceAutoFrame(clip.Length);
		}
	}

	void AdvanceAutoFrame(int clipLength)
	{
		if (autoState != BodyState.LowHp)
		{
			frame = (frame + 1) % clipLength;
			return;
		}

		int introEnd = ClampedLowHpIntroEnd(clipLength);
		int loopStart = ClampedLowHpLoopStart(clipLength);
		int loopEnd = ClampedLowHpLoopEnd(clipLength);

		if (lowHpIntroPlaying)
		{
			frame++;
			if (frame <= introEnd)
				return;

			lowHpIntroPlaying = false;
			frame = loopStart;
			lowHpLoopDirection = 1;
			return;
		}

		frame += lowHpLoopDirection;
		if (frame >= loopEnd)
		{
			frame = loopEnd;
			lowHpLoopDirection = -1;
		}
		else if (frame <= loopStart)
		{
			frame = loopStart;
			lowHpLoopDirection = 1;
		}
	}

	Sprite[] CurrentAutoSprites()
	{
		return autoState == BodyState.LowHp ? lowHpSprites : idleSprites;
	}

	void SetCurrentAutoStateSprite()
	{
		var clip = CurrentAutoSprites();
		if (clip == null || clip.Length == 0)
			return;

		frame = Mathf.Clamp(frame, 0, clip.Length - 1);
		SetBodySprite(clip[frame]);
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
		EnsureAutoState(ResolveAutoState());
		SetCurrentAutoStateSprite();
	}

	public void SetSprite(Sprite sprite)
	{
		SetBodySprite(sprite);
	}

	public Coroutine PlaySmallHit()
	{
		if (smallHitSprites == null || smallHitSprites.Length == 0 || playerBody == null)
			return null;

		return PlaySteppedAction(smallHitSprites, smallHitFrameStep);
	}

	public Coroutine PlayStrongHit()
	{
		return PlaySteppedAction(strongHitSprites, strongHitFrameStep);
	}

	public Coroutine PlayHitByDamage(int damageHalfHearts)
	{
		return damageHalfHearts >= 4 ? PlayStrongHit() : PlaySmallHit();
	}

	public Coroutine PlayHitByEnemyRank(int enemyRank, int damageHalfHearts = 0)
	{
		if (UsesSmallHitByEnemyRank(enemyRank, damageHalfHearts))
			return PlaySmallHit();

		return PlayHitByDamage(damageHalfHearts);
	}

	public int ResolveHitFrameCountByEnemyRank(int enemyRank, int damageHalfHearts = 0)
	{
		return UsesSmallHitByEnemyRank(enemyRank, damageHalfHearts)
			? SmallHitFrameCount
			: StrongHitFrameCount;
	}

	public static bool UsesSmallHitByEnemyRank(int enemyRank, int damageHalfHearts = 0)
	{
		if (enemyRank >= 1 && enemyRank <= 3)
			return true;

		return damageHalfHearts < 4;
	}

	public Coroutine PlayDefense()
	{
		return PlaySteppedAction(defenseSprites, 1);
	}

	public Coroutine BeginDefenseSession()
	{
		return PlayLoopingMotion(DefenseMotion(), LoopUntilStopped, useDefenseEndSignal: true);
	}

	public Coroutine EndDefenseSession()
	{
		if (!defenseSessionActive)
			return null;

		defenseSessionEnding = true;
		return actionRoutine;
	}

	public bool TryPlayDebugSpriteKind(string spriteKind, out string message, float loopSeconds = DebugLoopSectionSeconds)
	{
		message = null;
		if (string.IsNullOrWhiteSpace(spriteKind))
		{
			message = "spriteKind가 비어 있습니다.";
			return false;
		}

		switch (NormalizeSpriteKind(spriteKind))
		{
			case "idle":
				StopCurrentAction();
				ResumeAuto();
				message = "player idle 자동 루프 재개";
				return true;
			case "lowhp":
				return TryPlayDebugLoopingMotion(LowHpMotion(), loopSeconds, out message);
			case "smallhit":
				return PlaySmallHit() != null
					? Succeed("player smallhit 재생", out message)
					: Fail("SmallHit 스프라이트가 없습니다.", out message);
			case "stronghit":
				return PlayStrongHit() != null
					? Succeed("player stronghit 재생", out message)
					: Fail("StrongHit 스프라이트가 없습니다.", out message);
			case "jump":
				return PlayJump(0.6f) != null
					? Succeed("player jump 재생", out message)
					: Fail("Jump 스프라이트가 없습니다.", out message);
			case "defense":
				return TryPlayDebugLoopingMotion(DefenseMotion(), loopSeconds, out message);
			case "debuff":
				return TryPlayDebugAction(debuffSprites, 1, "player debuff", out message);
			case "death":
				return PlayDebugDeathDisplay() != null
					? Succeed("player death 재생", out message)
					: Fail("Death 스프라이트가 없습니다.", out message);
			default:
				message = $"알 수 없는 player spriteKind: {spriteKind}. 사용 가능: idle, lowhp, smallhit, stronghit, jump, defense, debuff, death";
				return false;
		}
	}

	public Coroutine PlayJump(float duration)
	{
		if (jumpSprites == null || jumpSprites.Length == 0 || playerBody == null)
			return null;

		return PlayTimedAction(jumpSprites, duration);
	}

	Coroutine PlaySteppedAction(Sprite[] sprites, int frameStep)
	{
		if (sprites == null || sprites.Length == 0 || playerBody == null)
			return null;

		StopCurrentAction();

		actionRoutine = StartCoroutine(PlaySteppedActionRoutine(sprites, frameStep));
		return actionRoutine;
	}

	IEnumerator PlaySteppedActionRoutine(Sprite[] sprites, int frameStep)
	{
		PauseAuto();

		float frameDuration = 1f / Mathf.Max(1f, frameRate);
		int step = Mathf.Max(1, frameStep);
		for (int i = 0; i < sprites.Length; i += step)
		{
			SetBodySprite(sprites[i]);
			yield return new WaitForSeconds(frameDuration);
		}

		ResumeAuto();
		actionRoutine = null;
	}

	Coroutine PlayTimedAction(Sprite[] sprites, float duration)
	{
		if (sprites == null || sprites.Length == 0 || playerBody == null)
			return null;

		StopCurrentAction();

		actionRoutine = StartCoroutine(PlayTimedActionRoutine(sprites, duration));
		return actionRoutine;
	}

	IEnumerator PlayTimedActionRoutine(Sprite[] sprites, float duration)
	{
		PauseAuto();

		float safeDuration = Mathf.Max(0.01f, duration);
		float elapsed = 0f;
		while (elapsed < safeDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / safeDuration);
			int index = Mathf.Clamp(Mathf.FloorToInt(t * sprites.Length), 0, sprites.Length - 1);
			SetBodySprite(sprites[index]);
			yield return null;
		}

		ResumeAuto();
		actionRoutine = null;
	}

	Coroutine PlayDebugDeathDisplay()
	{
		if (deathDisplaySprites == null || deathDisplaySprites.Length == 0 || playerBody == null)
			return null;

		StopCurrentAction();
		actionRoutine = StartCoroutine(PlayDebugDeathDisplayRoutine());
		return actionRoutine;
	}

	IEnumerator PlayDebugDeathDisplayRoutine()
	{
		PauseAuto();

		float frameDuration = 1f / Mathf.Max(1f, frameRate);
		for (int i = 0; i < deathDisplaySprites.Length; i++)
		{
			SetBodySprite(deathDisplaySprites[i]);
			yield return new WaitForSeconds(frameDuration);
		}

		Sprite last = LastSprite(deathDisplaySprites);
		if (last != null)
			SetBodySprite(last);
		actionRoutine = null;
	}

	bool TryPlayDebugAction(Sprite[] sprites, int frameStep, string label, out string message)
	{
		if (PlaySteppedAction(sprites, frameStep) == null)
			return Fail($"{label} 스프라이트가 없습니다.", out message);

		return Succeed($"{label} 재생", out message);
	}

	bool TryPlayDebugLoopingMotion(LoopingSpriteMotion motion, float loopSeconds, out string message)
	{
		float resolvedLoopSeconds = loopSeconds > 0f ? loopSeconds : DebugLoopSectionSeconds;
		if (PlayLoopingMotion(motion, resolvedLoopSeconds, useDefenseEndSignal: false) == null)
			return Fail($"{motion.label} 스프라이트가 없습니다.", out message);

		return Succeed($"{motion.label} 반복구간 {resolvedLoopSeconds:0.##}초 재생", out message);
	}

	Coroutine PlayLoopingMotion(LoopingSpriteMotion motion, float loopSeconds, bool useDefenseEndSignal)
	{
		if (motion.sprites == null || motion.sprites.Length == 0 || playerBody == null)
			return null;

		StopCurrentAction();
		if (useDefenseEndSignal)
		{
			defenseSessionActive = true;
			defenseSessionEnding = false;
		}

		actionRoutine = StartCoroutine(PlayLoopingMotionRoutine(motion, loopSeconds, useDefenseEndSignal));
		return actionRoutine;
	}

	IEnumerator PlayLoopingMotionRoutine(LoopingSpriteMotion motion, float loopSeconds, bool useDefenseEndSignal)
	{
		PauseAuto();

		float frameDuration = 1f / Mathf.Max(1f, frameRate);
		int entryEnd = Mathf.Clamp(motion.entryEndFrame, 0, motion.sprites.Length - 1);
		int loopEnd = Mathf.Clamp(motion.loopEndFrame, 0, motion.sprites.Length - 1);
		int loopStart = Mathf.Clamp(motion.loopStartFrame, 0, loopEnd);

		for (int i = 0; i <= entryEnd; i++)
		{
			SetBodySprite(motion.sprites[i]);
			yield return new WaitForSeconds(frameDuration);
		}

		float loopStartedAt = Time.time;
		int loopFrame = loopStart;
		int loopDirection = 1;
		while (ShouldContinueLoop(loopStartedAt, loopSeconds, useDefenseEndSignal))
		{
			SetBodySprite(motion.sprites[loopFrame]);
			yield return new WaitForSeconds(frameDuration);
			AdvanceLoopFrame(motion, loopStart, loopEnd, ref loopFrame, ref loopDirection);
		}

		if (motion.exitReverseEntry)
		{
			for (int i = entryEnd; i >= 0; i--)
			{
				SetBodySprite(motion.sprites[i]);
				yield return new WaitForSeconds(frameDuration);
			}
		}

		if (useDefenseEndSignal)
		{
			defenseSessionActive = false;
			defenseSessionEnding = false;
		}
		ResumeAuto();
		actionRoutine = null;
	}

	bool ShouldContinueLoop(float loopStartedAt, float loopSeconds, bool useDefenseEndSignal)
	{
		if (useDefenseEndSignal)
			return !defenseSessionEnding;
		return Time.time - loopStartedAt < Mathf.Max(0.01f, loopSeconds);
	}

	static void AdvanceLoopFrame(LoopingSpriteMotion motion, int loopStart, int loopEnd,
		ref int loopFrame, ref int loopDirection)
	{
		if (loopEnd <= loopStart)
		{
			loopFrame = loopStart;
			return;
		}

		if (!motion.loopPingPong)
		{
			loopFrame++;
			if (loopFrame > loopEnd)
				loopFrame = loopStart;
			return;
		}

		loopFrame += loopDirection;
		if (loopFrame >= loopEnd)
		{
			loopFrame = loopEnd;
			loopDirection = -1;
		}
		else if (loopFrame <= loopStart)
		{
			loopFrame = loopStart;
			loopDirection = 1;
		}
	}

	int ClampedLowHpIntroEnd(int clipLength)
	{
		return Mathf.Clamp(lowHpIntroEndFrame, 0, Mathf.Max(0, clipLength - 1));
	}

	int ClampedLowHpLoopStart(int clipLength)
	{
		int loopEnd = ClampedLowHpLoopEnd(clipLength);
		return Mathf.Clamp(lowHpLoopStartFrame, 0, loopEnd);
	}

	int ClampedLowHpLoopEnd(int clipLength)
	{
		return Mathf.Clamp(lowHpLoopEndFrame, 0, Mathf.Max(0, clipLength - 1));
	}

	LoopingSpriteMotion LowHpMotion()
	{
		return new LoopingSpriteMotion(
			lowHpSprites,
			lowHpIntroEndFrame,
			lowHpLoopStartFrame,
			lowHpLoopEndFrame,
			loopPingPong: true,
			exitReverseEntry: false,
			label: "player lowhp");
	}

	LoopingSpriteMotion DefenseMotion()
	{
		return new LoopingSpriteMotion(
			defenseSprites,
			defenseEntryEndFrame,
			defenseLoopStartFrame,
			defenseLoopEndFrame,
			loopPingPong: true,
			exitReverseEntry: true,
			label: "player defense");
	}

	static string NormalizeSpriteKind(string spriteKind)
	{
		return spriteKind.Trim().Replace("_", "").Replace("-", "").ToLowerInvariant();
	}

	static bool Succeed(string value, out string message)
	{
		message = value;
		return true;
	}

	static bool Fail(string value, out string message)
	{
		message = value;
		return false;
	}

	static Sprite LastSprite(Sprite[] sprites)
	{
		if (sprites == null) return null;
		for (int i = sprites.Length - 1; i >= 0; i--)
			if (sprites[i] != null) return sprites[i];
		return null;
	}

	void SetBodySprite(Sprite sprite)
	{
		if (playerBody == null || sprite == null)
			return;

		CaptureBaseBodySize();
		playerBody.sprite = sprite;
		ApplySpriteDisplaySize(sprite);
	}

	void CaptureBaseBodySize()
	{
		if (hasBaseBodySize || playerBody == null)
			return;

		baseBodySize = playerBody.rectTransform.sizeDelta;
		if (baseBodySize.y <= 0f)
			baseBodySize.y = 150f;
		hasBaseBodySize = true;
	}

	void ApplySpriteDisplaySize(Sprite sprite)
	{
		if (sprite == null || playerBody == null || sprite.rect.height <= 0f)
			return;

		float aspect = sprite.rect.width / sprite.rect.height;
		float height = baseBodySize.y;

		var rt = playerBody.rectTransform;
		rt.sizeDelta = new Vector2(height * aspect, height);
	}

	public void SkipCurrentAction()
	{
		StopCurrentAction();
	}

	void StopCurrentAction()
	{
		bool hadAction = actionRoutine != null;

		if (actionRoutine != null)
			StopCoroutine(actionRoutine);
		actionRoutine = null;
		defenseSessionActive = false;
		defenseSessionEnding = false;

		if (hadAction || autoPaused)
			ResumeAuto();
	}
}
