using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public enum EnemyDicePresentationMode
{
	Viewport,
	CenterPopup
}

public static class EnemyAttackTiming
{
	public const string GoblinEnemyId = "goblin";
	public const int GoblinMeleeAttackFrameCount = 50;
	public const int GoblinMeleeVisualImpactFrame = 23;

	// SpritePipelineWork/impact_timing_audit_20260613_01:
	// Goblin Attack frames 22-24 are the post-windup forward contact; use frame 23.
	// Player SmallHit first meaningful reaction is frame 1/134; StrongHit is frame 3/47.
	public const float GoblinMeleeImpactNormalizedTime = 23f / 50f;
	public const int PlayerSmallHitFrameCount = 134;
	public const int PlayerSmallHitReactionOnsetFrame = 1;
	public const float PlayerSmallHitReactionNormalizedTime = 1f / 134f;
	public const int PlayerStrongHitFrameCount = 47;
	public const int PlayerStrongHitReactionOnsetFrame = 3;
	public const float PlayerStrongHitReactionNormalizedTime = 3f / 47f;

	public static float ComputePlayerHitStartDelay(
		float enemyAttackDuration,
		float enemyImpactNormalizedTime,
		float playerHitDuration,
		float playerReactionNormalizedTime)
	{
		float impactDelay = ComputeImpactDelay(enemyAttackDuration, enemyImpactNormalizedTime);
		if (!IsUsablePositive(playerHitDuration) || !IsUsableNormalized(playerReactionNormalizedTime))
			return impactDelay;

		float reactionLead = playerHitDuration * Mathf.Clamp01(playerReactionNormalizedTime);
		if (float.IsNaN(reactionLead) || float.IsInfinity(reactionLead))
			reactionLead = 0f;

		return Mathf.Max(0f, impactDelay - reactionLead);
	}

	public static float ComputeImpactDelay(float enemyAttackDuration, float enemyImpactNormalizedTime)
	{
		if (!IsUsablePositive(enemyAttackDuration) || !IsUsableNormalized(enemyImpactNormalizedTime))
			return 0f;

		return enemyAttackDuration * Mathf.Clamp01(enemyImpactNormalizedTime);
	}

	public static bool HasUsableProfile(EnemyAttackTimingProfile profile)
	{
		return profile != null
			&& IsUsableNormalized(profile.impactNormalizedTime)
			&& profile.impactNormalizedTime > 0f
			&& profile.impactNormalizedTime <= 1f;
	}

	public static float ResolvePlayerReactionNormalizedTime(
		EnemyAttackTimingProfile profile,
		int enemyRank,
		int damageHalfHearts)
	{
		if (profile == null)
			return 0f;

		if (PlayerBodyAnimator.UsesSmallHitByEnemyRank(enemyRank, damageHalfHearts))
			return Mathf.Max(0f, profile.playerSmallHitReactionNormalizedTime);

		return Mathf.Max(0f, profile.playerStrongHitReactionNormalizedTime);
	}

	static bool IsUsablePositive(float value)
	{
		return value > 0f && !float.IsNaN(value) && !float.IsInfinity(value);
	}

	static bool IsUsableNormalized(float value)
	{
		return !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0f;
	}
}

/// <summary>
/// 적 반격(공격 연출 → 주사위 굴림 → 방어 페이즈 게이트 → 슬램/회피)의 전체 흐름과
/// 관련 UI(적 주사위 오버레이/결과 텍스트)를 소유한다.
///
/// BattleSceneController가 세션 상태(enemies, HUD, 버튼 등)를 BattleControllerContext로
/// 런타임에 주입한다. Scene 파일에 정적으로 저장되는 레퍼런스는 전부 private SerializeField이며
/// DiceBattleSceneBuilder가 SceneBuilderUtility.SetField로 연결한다. Inspector 수동 와이어링 없음.
/// </summary>
public class EnemyCounterAttackDirector : MonoBehaviour
{
	struct EnemyAttackMotionProfile
	{
		public float openingDelay;
		public float approachDuration;
		public float anticipationJumpDuration;
		public float preDefensePause;
		public float slamRushTime;
		public float slamHoldTime;
		public float slamReturnTime;
		public float hitFlashHoldTime;
		public float hitFlashFadeTime;
		public float hitPause;
		public float retreatDuration;
		public float postEnemyPause;
		public float bossJumpUpTime;
		public float bossSlamDownTime;
		public float bossHoldTime;
		public float bossReturnTime;

		public static EnemyAttackMotionProfile Default => new EnemyAttackMotionProfile
		{
			openingDelay = 1.0f,
			approachDuration = 1.4f,
			anticipationJumpDuration = 0.7f,
			preDefensePause = 0.6f,
			slamRushTime = 0.28f,
			slamHoldTime = 0.12f,
			slamReturnTime = 0.34f,
			hitFlashHoldTime = 0.32f,
			hitFlashFadeTime = 0.6f,
			hitPause = 0.35f,
			retreatDuration = 1.0f,
			postEnemyPause = 0.6f,
			bossJumpUpTime = 0.3f,
			bossSlamDownTime = 0.2f,
			bossHoldTime = 0.16f,
			bossReturnTime = 0.3f,
		};

		public static EnemyAttackMotionProfile BossDefault
		{
			get
			{
				var profile = Default;
				profile.openingDelay = 1.1f;
				profile.anticipationJumpDuration = 0.8f;
				profile.slamRushTime = 0.36f;
				profile.slamHoldTime = 0.18f;
				profile.slamReturnTime = 0.42f;
				return profile;
			}
		}
	}

	// ── 빌더가 SetField로 주입하는 씬 오브젝트 참조 ─────────────────
	[SerializeField] EnemyDiceRoller enemyDiceRoller;
	[SerializeField] GameObject enemyDicePopup;     // 레거시 — 현재는 표시하지 않음
	[SerializeField] RectTransform enemyDiceOverlay; // 3D 주사위 RenderTexture UI 오버레이
	[SerializeField] TMP_Text[] enemyDiceResultTexts;
	[SerializeField] PlayerJumpAnimator jumpAnimator;
	[SerializeField] Image enemyProjectile;
	[SerializeField] EnemyAttackProjectileVfx attackProjectileVfx;

	const int CenterPopupEnemyRankThreshold = 4;
	RectTransform enemyDicePopupPanel;
	TMP_Text enemyDicePopupTitle;
	TMP_Text enemyDicePopupCombo;
	TMP_Text[] enemyDicePopupValueTexts;

	// ── 런타임에 BSC가 Bind로 주입 ─────────────────────────────────
	BattleControllerContext ctx;

	// ── 방어 페이즈 상태 (BSC에서 이동) ────────────────────────────
	bool isDefensePhase;
	bool defenseConfirmed;
	int currentDefenseEnemyIndex;
	bool lastDefenseBlocked;
	int lastDefenseDamage;
	bool lastDefenseDamageApplied;
	int defenseRollsMax;

	public bool IsDefensePhase => isDefensePhase;
	public int CurrentDefenseEnemyIndex => currentDefenseEnemyIndex;
	public int DefenseRollsMax => defenseRollsMax;

	public static int GetDefenseRollCount(EnemyDiceResult result)
	{
		return result != null && result.hasCombo ? 3 : 1;
	}

	public static EnemyDicePresentationMode ResolveDicePresentationMode(
		int enemyRank, bool shouldShowEnemyDiceResult)
	{
		return shouldShowEnemyDiceResult && enemyRank >= CenterPopupEnemyRankThreshold
			? EnemyDicePresentationMode.CenterPopup
			: EnemyDicePresentationMode.Viewport;
	}

	/// <summary>
	/// BSC.Start에서 1회 호출. 공유 상태 접근에 필요한 최소한의 콜백/참조를 주입한다.
	/// 이 컨텍스트 객체는 빌더가 건드리지 않으므로 Inspector 노출 대상이 아니다.
	/// </summary>
	public void Bind(BattleControllerContext context)
	{
		ctx = context;
	}

	void Awake()
	{
		// 레거시 팝업/오버레이는 시작 시 항상 비활성 (원본 BSC.Start 동작 보존).
		if (enemyDicePopup != null)
			enemyDicePopup.SetActive(false);
	}

	// ── 방어 페이즈 진입 초기화(BSC도 참조 후 접근) ───────────────
	public void ResetOnNewTurn()
	{
		isDefensePhase = false;
		defenseConfirmed = false;
		lastDefenseBlocked = false;
		lastDefenseDamage = 0;
		lastDefenseDamageApplied = false;
		if (ctx != null && ctx.playerBodyAnimator != null && ctx.playerBodyAnimator.IsDefenseSessionActive)
			ctx.playerBodyAnimator.SkipCurrentAction();

		HideEnemyDicePresentation();
	}

	// ── 방어 결과 질의 ────────────────────────────────────────────
	public bool LastDefenseBlocked => lastDefenseBlocked;
	public int LastDefenseDamage => lastDefenseDamage;

	/// <summary>
	/// BSC가 공격 확정 후 호출. 내부에서 StartCoroutine을 수행하여 이후의 모든 중첩 코루틴이
	/// 디렉터 MonoBehaviour 생명주기에 귀속되도록 한다(원본이 단일 MB였던 것과 동일한 보호 범위).
	/// </summary>
	public Coroutine StartCounterAttack()
	{
		return StartCoroutine(RunCounterAttack());
	}

	/// <summary>
	/// 적 반격 시퀀스 전체. 기존 BattleSceneController.EnemyCounterAttackRoutine 본문과 동일.
	/// </summary>
	IEnumerator RunCounterAttack()
	{
		var enemies = ctx.enemies;
		int aliveCount = 0;
		foreach (var e in enemies)
		{
			if (e.IsAlive)
				aliveCount++;
		}

		if (aliveCount <= 0)
		{
			if (ctx.nextRoundButton != null) ctx.nextRoundButton.gameObject.SetActive(true);
			yield break;
		}

		var baseProfile = ResolveAttackMotionProfile(null);
		yield return new WaitForSeconds(baseProfile.openingDelay);

		for (int i = 0; i < enemies.Count; i++)
		{
			if (!enemies[i].IsAlive)
				continue;

			var def = ResolveMobDef(enemies[i]);
			var attackProfile = ResolveAttackMotionProfile(enemies[i]);

			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry($"<color=#FF6666>{enemies[i].name}의 공격!</color>",
					BattleEventPresentation.LogAndAnimation);

			AudioManager.Play(enemies[i].rank >= 4 ? "Enemy45_Attack" : "Enemy123_Attack");
			bool hasUniqueAttackMotion = HasUniqueAttackMotion(def);

			RectTransform slotRt = i < ctx.enemyPanels.Length && ctx.enemyPanels[i] != null
				? ctx.enemyPanels[i].GetComponent<RectTransform>()
				: null;
			RectTransform bodyRt = i < ctx.enemyBodies.Length && ctx.enemyBodies[i] != null
				? ctx.enemyBodies[i].rectTransform
				: null;
			RectTransform playerRt = ctx.playerBody != null ? ctx.playerBody.rectTransform : null;
			var positionPlan = EnemyAttackPositionResolver.Resolve(slotRt, bodyRt, playerRt, def);
			var rangeType = EnemyAttackPositionResolver.ResolveRangeType(def);
			bool isProjectile = UsesProjectileAttack(enemies[i]);
			bool hasAttackProjectileVfx = HasAttackProjectileVfx(def);
			bool isMelee = rangeType != EnemyAttackRangeType.Ranged
				&& rangeType != EnemyAttackRangeType.Unique
				&& !hasUniqueAttackMotion
				&& ctx.battleAnims != null
				&& !GameSessionManager.IsBossBattle && slotRt != null && ctx.playerBody != null;
			bool isSlimeLeapSlam = BattleAnimations.ShouldUseSlimeLeapSlam(enemies[i], def)
				&& ctx.battleAnims != null
				&& bodyRt != null
				&& playerRt != null;
			bool shouldPlayDraculaLaser = DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
				GameSessionManager.IsBossBattle,
				GameSessionManager.CurrentStageId,
				enemies[i],
				GameSessionManager.CurrentStage != null ? GameSessionManager.CurrentStage.boss : null,
				ResolveEnemyDiceProfileId(enemies[i]));

			if (!hasUniqueAttackMotion && !isMelee && !isSlimeLeapSlam)
				PlayEnemyAttackClip(i);

			if (isMelee)
			{
				yield return StartCoroutine(ctx.battleAnims.WalkTo(
					slotRt,
					positionPlan.standWorldPosition,
					AttackApproachDuration(def, attackProfile)));

				// ② 제자리 점프
				if (bodyRt != null)
					yield return StartCoroutine(ctx.battleAnims.JumpInPlace(
						bodyRt,
						duration: attackProfile.anticipationJumpDuration));
			}
			else if (isProjectile && bodyRt != null && ctx.battleAnims != null)
			{
				yield return StartCoroutine(ctx.battleAnims.JumpInPlace(
					bodyRt,
					height: 18f,
					duration: Mathf.Max(0.24f, attackProfile.anticipationJumpDuration * 0.45f)));
			}
			else if (rangeType == EnemyAttackRangeType.Unique
				&& bodyRt != null
				&& ctx.battleAnims != null
				&& !isSlimeLeapSlam
				&& !hasAttackProjectileVfx)
			{
				yield return StartCoroutine(ctx.battleAnims.JumpInPlace(
					bodyRt,
					height: 24f,
					duration: attackProfile.anticipationJumpDuration));
			}
			else if (!hasUniqueAttackMotion && bodyRt != null && rangeType != EnemyAttackRangeType.Unique)
			{
				// 보스: 제자리 점프만
				yield return StartCoroutine(EnemyJumpAnimation(bodyRt, attackProfile));
			}

			// ── 적 주사위 굴림 연출 ──
			EnemyDiceResult result = null;
			string diceProfileId = ResolveEnemyDiceProfileId(enemies[i]);
			yield return StartCoroutine(AnimateEnemyDiceRoll3D(
				i, enemies[i].rank, diceProfileId, slotRt, r => result = r));
			if (result == null)
			{
				Debug.LogWarning($"[EnemyDice] {enemies[i].name} 결과 없음 — 물리 롤러 씬 wiring 확인 필요");
				yield break;
			}

			enemies[i].lastDiceResult = result;
			string diceStr = FormatDiceValues(result.values);

			if (enemyDiceResultTexts != null && i < enemyDiceResultTexts.Length && enemyDiceResultTexts[i] != null)
			{
				if (result.hasCombo)
					enemyDiceResultTexts[i].text = $"<color=#FFD94A>{result.comboName}</color> {diceStr}";
				else
					enemyDiceResultTexts[i].text = diceStr;
			}

			if (ctx.battleLog != null)
			{
				if (result.hasCombo)
					ctx.battleLog.AddEntry($"  <color=#FFD94A>{enemies[i].name}의 {result.comboName}!</color> {diceStr}");
				else
					ctx.battleLog.AddEntry($"  {enemies[i].name}이(가) 주사위를 굴렸다: {diceStr}");
			}

			yield return new WaitForSeconds(attackProfile.preDefensePause);

			// ── ③ 이 적에 대한 방어 페이즈 ──
			currentDefenseEnemyIndex = i;
			isDefensePhase = true;
			defenseConfirmed = false;
			lastDefenseBlocked = true;
			lastDefenseDamage = 0;
			lastDefenseDamageApplied = true;
			defenseRollsMax = GetDefenseRollCount(result);
			ctx.setRoundConfirmed?.Invoke(false);

			if (ctx.diceDirector != null)
			{
				ctx.diceDirector.BeginTurn(defenseRollsMax, DiceRollDirector.TurnMode.Defense);
				ctx.diceDirector.SetHoldInteractionEnabled(false);
			}

			if (ctx.confirmButton != null) ctx.confirmButton.gameObject.SetActive(false);
			if (ctx.hud != null) ctx.hud.ClearDamageText();

			if (ctx.hud != null && ctx.diceDirector != null)
				ctx.hud.RefreshRollDots(ctx.diceDirector.MaxRolls, ctx.diceDirector.RollsRemaining);

			if (ctx.playerBodyAnimator != null)
				ctx.playerBodyAnimator.BeginDefenseSession();

			string defensePrompt = BuildDefensePrompt(enemies[i], result);
			if (ctx.hud != null)
				ctx.hud.ShowDefensePreview(BuildDefenseTargetText(enemies[i], result));

			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry(defensePrompt, BattleEventPresentation.LogAndPopup);

			while (!defenseConfirmed)
				yield return null;

			if (!lastDefenseBlocked)
				yield return StartCoroutine(EndPlayerDefenseSession());

			// ── ④⑤ 방어 결과 후속 연출 ──
			if (shouldPlayDraculaLaser && bodyRt != null && playerRt != null)
			{
				yield return StartCoroutine(PlayDraculaLaserAttack(
					i,
					enemies[i],
					bodyRt,
					lastDefenseBlocked,
					lastDefenseDamage,
					attackProfile));
			}
			else if (hasUniqueAttackMotion && slotRt != null)
			{
				yield return StartCoroutine(PlayUniqueSpriteAttack(
					i,
					enemies[i],
					def,
					positionPlan,
					lastDefenseBlocked,
					lastDefenseDamage,
					attackProfile));
			}
			else if (isMelee && slotRt != null)
			{
				EnemySpriteAnimator meleeAnimator = ResolveEnemyAnimator(i);
				PlayEnemyAttackClip(i);
				Coroutine feedbackRoutine = null;
				if (!lastDefenseBlocked)
				{
					bool useProfiledHitDelay = TryResolveMeleeHitTiming(
						def,
						meleeAnimator,
						enemies[i],
						lastDefenseDamage,
						out float hitStartDelay,
						out float visualImpactDelay);
					if (useProfiledHitDelay)
						feedbackRoutine = StartCoroutine(PlayProfiledPlayerHitFeedback(
							i,
							enemies[i],
							lastDefenseDamage,
							attackProfile,
							hitStartDelay,
							visualImpactDelay));

					yield return StartCoroutine(ctx.battleAnims.QuickSlam(
						slotRt, positionPlan.impactWorldPosition,
						rushTime: attackProfile.slamRushTime,
						holdTime: attackProfile.slamHoldTime,
						returnTime: attackProfile.slamReturnTime,
						onImpact: useProfiledHitDelay ? (System.Action)null : () =>
						{
							ApplyPendingPlayerDamageAtImpact(i, lastDefenseDamage);
							feedbackRoutine = StartCoroutine(PlayPlayerHitFeedback(enemies[i], lastDefenseDamage, attackProfile));
						}));
				}
				else
				{
					bool useProfiledDefenseDelay = TryResolveMeleeImpactDelay(
						def,
						meleeAnimator,
						out float defenseFeedbackDelay);
					if (useProfiledDefenseDelay)
						feedbackRoutine = StartCoroutine(PlayDelayedPlayerDefenseFeedback(defenseFeedbackDelay));

					yield return StartCoroutine(ctx.battleAnims.QuickSlam(
						slotRt, positionPlan.impactWorldPosition,
						rushTime: attackProfile.slamRushTime,
						holdTime: attackProfile.slamHoldTime,
						returnTime: attackProfile.slamReturnTime,
						onImpact: useProfiledDefenseDelay ? (System.Action)null : () =>
						{
							feedbackRoutine = StartCoroutine(PlayPlayerDefenseFeedback());
						}));
				}
				if (feedbackRoutine != null)
					yield return feedbackRoutine;
				yield return StartCoroutine(ctx.battleAnims.WalkBack(
					slotRt,
					positionPlan.homeLocalPosition,
					AttackRetreatDuration(def, attackProfile)));
			}
			else if (isSlimeLeapSlam)
				{
					Coroutine feedbackRoutine = null;
					EnemySpriteAnimator animator = ctx.enemyAnimators != null && i < ctx.enemyAnimators.Length
						? ctx.enemyAnimators[i]
						: null;
					yield return StartCoroutine(ctx.battleAnims.EnemyLeapSlamAttack(
						bodyRt,
						playerRt,
						enemyAnimator: animator,
						idleFallbackSprite: enemies[i].sprite,
						onImpact: () =>
						{
							if (lastDefenseBlocked)
							{
								feedbackRoutine = StartCoroutine(PlayPlayerDefenseFeedback());
								return;
							}
							ApplyPendingPlayerDamageAtImpact(i, lastDefenseDamage);
							feedbackRoutine = StartCoroutine(PlayPlayerHitFeedback(enemies[i], lastDefenseDamage, attackProfile));
						},
						debugTraceMode: "Dice"));
				if (feedbackRoutine != null)
					yield return feedbackRoutine;
			}
			else if (rangeType == EnemyAttackRangeType.Unique && bodyRt != null)
			{
				if (hasAttackProjectileVfx)
				{
					yield return StartCoroutine(PlayUniqueAttackProjectileVfx(
						i,
						enemies[i],
						bodyRt,
						lastDefenseBlocked,
						lastDefenseDamage,
						attackProfile));
				}
				else
				{
					Coroutine feedbackRoutine = null;
					yield return StartCoroutine(EnemySlamAnimation(bodyRt, attackProfile, () =>
					{
						if (lastDefenseBlocked)
						{
							feedbackRoutine = StartCoroutine(PlayPlayerDefenseFeedback());
							return;
						}
						ApplyPendingPlayerDamageAtImpact(i, lastDefenseDamage);
						feedbackRoutine = StartCoroutine(PlayPlayerHitFeedback(enemies[i], lastDefenseDamage, attackProfile));
					}));
					if (feedbackRoutine != null)
						yield return feedbackRoutine;
				}
			}
			else if (isProjectile)
			{
				if (ctx.battleAnims != null && bodyRt != null && ctx.playerBody != null)
				{
					yield return StartCoroutine(ctx.battleAnims.EnemyProjectileAttack(
						enemyProjectile,
						bodyRt,
						ctx.playerBody.rectTransform,
						ctx.playerBody,
						ctx.playerBodyAnimator,
						jumpAnimator,
						lastDefenseBlocked,
						lastDefenseDamage,
						attachmentFollower: GetAttachmentFollower(i),
						enemyRank: enemies[i].rank,
						projectileStartWorld: positionPlan.projectileStartWorldPosition,
						projectileEndWorld: positionPlan.projectileEndWorldPosition,
						onImpact: () => ApplyPendingPlayerDamageAtImpact(i, lastDefenseDamage)));
				}
				else if (!lastDefenseBlocked && ctx.battleAnims != null && ctx.playerBody != null)
				{
					ApplyPendingPlayerDamageAtImpact(i, lastDefenseDamage);
					yield return StartCoroutine(PlayPlayerHitFeedback(enemies[i], lastDefenseDamage, attackProfile));
				}
			}
			else if (rangeType != EnemyAttackRangeType.Ranged && !lastDefenseBlocked)
			{
				Coroutine feedbackRoutine = null;
				if (i < ctx.enemyBodies.Length && ctx.enemyBodies[i] != null)
					yield return StartCoroutine(EnemySlamAnimation(ctx.enemyBodies[i].rectTransform, attackProfile, () =>
					{
						ApplyPendingPlayerDamageAtImpact(i, lastDefenseDamage);
						if (ctx.battleAnims != null && ctx.playerBody != null)
						{
							feedbackRoutine = StartCoroutine(PlayPlayerHitFeedback(enemies[i], lastDefenseDamage, attackProfile));
						}
					}));
				else
					ApplyPendingPlayerDamageAtImpact(i, lastDefenseDamage);
				if (ctx.battleAnims != null && ctx.playerBody != null)
				{
					if (feedbackRoutine == null)
						feedbackRoutine = StartCoroutine(PlayPlayerHitFeedback(enemies[i], lastDefenseDamage, attackProfile));
					if (feedbackRoutine != null)
						yield return feedbackRoutine;
				}
			}

			ApplyPendingPlayerDamageAtImpact(i, lastDefenseDamage);

			if (lastDefenseBlocked)
				yield return StartCoroutine(EndPlayerDefenseSession());

			// 플레이어 사망 시 중단
			if (!GameSessionManager.IsPlayerAlive)
			{
				ctx.startPlayerDefeatedRoutine?.Invoke();
				yield break;
			}

			// 이 적의 주사위 결과 초기화
			if (enemyDiceResultTexts != null && i < enemyDiceResultTexts.Length && enemyDiceResultTexts[i] != null)
				enemyDiceResultTexts[i].text = "";
			HideEnemyDicePresentation();

			yield return new WaitForSeconds(attackProfile.postEnemyPause);
		}

		// 모든 적의 공격이 끝남 — 다음 라운드 버튼 표시
		yield return StartCoroutine(EndPlayerDefenseSession());
		isDefensePhase = false;
		if (ctx.confirmButton != null) ctx.confirmButton.gameObject.SetActive(false);
		if (ctx.rollButton != null) ctx.rollButton.interactable = false;
		if (ctx.nextRoundButton != null) ctx.nextRoundButton.gameObject.SetActive(true);
	}

	Coroutine PlayEnemyAttackClip(int enemyIndex)
	{
		var animator = ResolveEnemyAnimator(enemyIndex);
		return animator != null && animator.HasAttackSprites ? animator.PlayAttack() : null;
	}

	EnemySpriteAnimator ResolveEnemyAnimator(int enemyIndex)
	{
		if (ctx.enemyAnimators == null || enemyIndex < 0 || enemyIndex >= ctx.enemyAnimators.Length)
			return null;

		return ctx.enemyAnimators[enemyIndex];
	}

	/// <summary>
	/// 굴림 정착 후 BSC.HandleRollSettled에서 호출. 자동 확정 조건(no-combo 또는 완벽 방어)을
	/// 판단하고 해당되면 즉시 ConfirmDefense를 수행한다.
	/// </summary>
	/// <returns>자동 확정해서 소비했으면 true. false면 호출자가 수동 확정 UI를 노출해야 한다.</returns>
	public bool TryAutoConfirmDefenseOnRollSettled()
	{
		if (!isDefensePhase) return false;
		int ci = currentDefenseEnemyIndex;
		var enemies = ctx.enemies;
		if (ci < 0 || ci >= enemies.Count) return false;
		if (!enemies[ci].IsAlive || enemies[ci].lastDiceResult == null) return false;

		var result = enemies[ci].lastDiceResult;
		bool autoConfirm = !result.hasCombo;
		if (!autoConfirm)
		{
			int[] vals = ctx.diceDirector.ReadFinalValues();
			var defense = DefenseCalculator.Evaluate(vals, result);
			autoConfirm = defense.blocked;
		}
		if (!autoConfirm) return false;

		ConfirmDefense();
		return true;
	}

	/// <summary>방어 확정 — 현재 공격 중인 적의 주사위 결과와 비교. 기존 로직 보존.</summary>
	public void ConfirmDefense()
	{
		if (!isDefensePhase || ctx.diceDirector == null || !ctx.diceDirector.HasRolledOnce)
			return;

		int i = currentDefenseEnemyIndex;
		var enemies = ctx.enemies;
		if (i < 0 || i >= enemies.Count || !enemies[i].IsAlive || enemies[i].lastDiceResult == null)
		{
			lastDefenseBlocked = true;
			lastDefenseDamage = 0;
			defenseConfirmed = true;
			return;
		}

		int[] playerValues = ctx.diceDirector.ReadFinalValues();
		var result = enemies[i].lastDiceResult;
		var defense = DefenseCalculator.Evaluate(playerValues, result);

		float reduction = defense.blocked ? 1f : 0f;
		int baseDmg = DefenseCalculator.CalculateEnemyDamage(enemies[i].rank, result.damageMultiplier);
		int finalDmg = Mathf.Max(0, Mathf.CeilToInt(baseDmg * (1f - reduction)));

		lastDefenseBlocked = defense.blocked || finalDmg <= 0;
		lastDefenseDamage = finalDmg;
		lastDefenseDamageApplied = lastDefenseBlocked;

		if (defense.blocked)
		{
			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry($"  <color=#55FF55>{enemies[i].name}: 완벽 방어!</color>",
					BattleEventPresentation.LogAndAnimation);
			AudioManager.Play("Player_PerfectDefense");
		}
		else if (finalDmg <= 0)
		{
			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry($"  <color=#55FF55>{enemies[i].name}: 데미지 0!</color>",
					BattleEventPresentation.LogAndAnimation);
			AudioManager.Play("Player_PerfectDefense");
		}
		else
		{
			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry($"  <color=#FF6666>{enemies[i].name}: 방어 실패! ({FormatHalf(finalDmg)} 데미지)</color>",
					BattleEventPresentation.LogAndAnimation);
		}

		enemies[i].lastDiceResult = null;

		if (GameSessionManager.IsPlayerAlive)
		{
			Debug.Log($"[Battle] Defense vs {enemies[i].name} done hearts={GameSessionManager.PlayerHearts.TotalHalfHearts}");
		}

		if (ctx.rollButton != null) ctx.rollButton.interactable = false;
		if (ctx.confirmButton != null) ctx.confirmButton.gameObject.SetActive(false);

		defenseConfirmed = true;
	}

	/// <summary>
	/// 방어 페이즈의 데미지 프리뷰 문자열을 계산해 HUD에 설정. BSC.UpdateDamagePreview가 라우팅.
	/// </summary>
	public void UpdateDefensePreview(int[] playerValues)
	{
		if (ctx.hud == null) return;
		int i = currentDefenseEnemyIndex;
		var enemies = ctx.enemies;
		if (i < 0 || i >= enemies.Count || !enemies[i].IsAlive || enemies[i].lastDiceResult == null)
		{
			ctx.hud.ShowDefensePreview("");
			return;
		}
		var enemyResult = enemies[i].lastDiceResult;
		var defense = DefenseCalculator.Evaluate(playerValues, enemyResult);
		if (defense.blocked)
		{
			ctx.hud.ShowDefensePreview($"<color=#55FF55>방어 성공!</color> {BuildDefenseTargetText(enemies[i], enemyResult)}");
		}
		else if (enemyResult.hasCombo)
		{
			ctx.hud.ShowDefensePreview($"<color=#FF6666>{BuildDefenseTargetText(enemies[i], enemyResult)}</color>");
		}
		else
		{
			ctx.hud.ShowDefensePreview($"<color=#FF6666>{BuildDefenseTargetText(enemies[i], enemyResult)} / 방어 실패</color>");
		}
	}

	static string BuildDefensePrompt(EnemyInfo enemy, EnemyDiceResult result)
	{
		string enemyName = enemy != null ? enemy.name : "적";
		string diceStr = FormatDiceValues(result?.values);
		if (result != null && result.hasCombo)
			return $"<color=#FFD94A>{result.comboName} {diceStr}을(를) 만들어 {enemyName}의 공격을 방어하자! 3회 굴림!</color>";
		return $"{diceStr}{ObjectParticleForDiceValues(result?.values)} 찾아 {enemyName}의 공격을 방어하자! 1회 굴림!";
	}

	static string BuildDefenseTargetText(EnemyInfo enemy, EnemyDiceResult result)
	{
		string enemyName = enemy != null ? enemy.name : "적";
		string diceStr = FormatDiceValues(result?.values);
		if (result != null && result.hasCombo)
			return $"목표: {result.comboName} {diceStr} / {enemyName}";
		return $"목표: {diceStr} / {enemyName}";
	}

	static string FormatDiceValues(int[] values)
	{
		if (values == null || values.Length == 0)
			return "[]";
		return $"[{string.Join(", ", values)}]";
	}

	static string ObjectParticleForDiceValues(int[] values)
	{
		if (values == null || values.Length != 1)
			return "을";

		switch (values[0])
		{
			case 1:
			case 3:
			case 6:
				return "을";
			default:
				return "를";
		}
	}

	IEnumerator PlayUniqueSpriteAttack(int enemyIndex, EnemyInfo enemy, MobDef def,
		EnemyAttackPositionPlan positionPlan, bool blocked, int damageHalfHearts,
		EnemyAttackMotionProfile profile)
	{
		if (ctx.battleAnims == null || ctx.enemyPanels == null || enemyIndex < 0 || enemyIndex >= ctx.enemyPanels.Length)
			yield break;
		var slot = ctx.enemyPanels[enemyIndex] != null
			? ctx.enemyPanels[enemyIndex].GetComponent<RectTransform>()
			: null;
		if (slot == null)
			yield break;

		yield return StartCoroutine(ctx.battleAnims.WalkTo(
			slot,
			positionPlan.standWorldPosition,
			AttackApproachDuration(def, profile)));

		EnemySpriteAnimator animator = null;
		if (ctx.enemyAnimators != null && enemyIndex < ctx.enemyAnimators.Length)
			animator = ctx.enemyAnimators[enemyIndex];

		Coroutine attackRoutine = animator != null ? animator.PlayAttack() : null;
		if (attackRoutine != null)
			yield return new WaitWhile(() => animator != null && animator.IsActionPlaying);

		if (blocked)
		{
			yield return StartCoroutine(PlayPlayerDefenseFeedback());
		}
		else
		{
			ApplyPendingPlayerDamageAtImpact(enemyIndex, damageHalfHearts);
			yield return StartCoroutine(PlayPlayerHitFeedback(enemy, damageHalfHearts, profile));
		}

		if (animator != null)
			animator.ReturnToIdle(enemy != null ? enemy.sprite : null);

		yield return StartCoroutine(ctx.battleAnims.WalkBack(
			slot,
			positionPlan.homeLocalPosition,
			AttackRetreatDuration(def, profile)));
	}

	IEnumerator PlayUniqueAttackProjectileVfx(int enemyIndex, EnemyInfo enemy, RectTransform bodyRt,
		bool blocked, int damageHalfHearts, EnemyAttackMotionProfile profile)
	{
		Sprite vfxSprite = ResolveAttackVfxSprite(enemy);
		Coroutine vfxRoutine = null;
		if (attackProjectileVfx != null && vfxSprite != null)
			vfxRoutine = attackProjectileVfx.Play(vfxSprite, bodyRt);
		else if (vfxSprite == null)
			Debug.LogWarning($"[EnemyAttackProjectileVfx] {enemy?.name ?? "적"} 공격 VFX 스프라이트를 찾을 수 없습니다.");
		else
			Debug.LogWarning("[EnemyAttackProjectileVfx] attackProjectileVfx 컴포넌트가 없어 VFX를 생략합니다.");

		if (vfxRoutine != null)
			yield return vfxRoutine;

		if (blocked)
		{
			yield return StartCoroutine(PlayPlayerDefenseFeedback());
			yield break;
		}

		ApplyPendingPlayerDamageAtImpact(enemyIndex, damageHalfHearts);
		yield return StartCoroutine(PlayPlayerHitFeedback(enemy, damageHalfHearts, profile));
	}

	IEnumerator PlayProfiledPlayerHitFeedback(
		int enemyIndex,
		EnemyInfo enemy,
		int damageHalfHearts,
		EnemyAttackMotionProfile profile,
		float hitStartDelaySeconds,
		float visualImpactDelaySeconds)
	{
		float hitStartDelay = Mathf.Max(0f, hitStartDelaySeconds);
		float impactDelay = Mathf.Max(0f, visualImpactDelaySeconds);

		if (hitStartDelay > 0f)
			yield return new WaitForSeconds(hitStartDelay);

		Coroutine hitRoutine = StartPlayerHitAnimation(enemy, damageHalfHearts);

		float remainingUntilImpact = Mathf.Max(0f, impactDelay - hitStartDelay);
		if (remainingUntilImpact > 0f)
			yield return new WaitForSeconds(remainingUntilImpact);

		ApplyPendingPlayerDamageAtImpact(enemyIndex, damageHalfHearts);
		FlashPlayerDamage(profile);

		if (hitRoutine != null)
			yield return new WaitWhile(() => ctx.playerBodyAnimator != null && ctx.playerBodyAnimator.IsActionPlaying);
		else
			yield return new WaitForSeconds(profile.hitPause);
	}

	IEnumerator PlayDelayedPlayerDefenseFeedback(float delaySeconds)
	{
		if (delaySeconds > 0f)
			yield return new WaitForSeconds(delaySeconds);

		yield return StartCoroutine(PlayPlayerDefenseFeedback());
	}

	bool TryResolveMeleeHitTiming(
		MobDef def,
		EnemySpriteAnimator animator,
		EnemyInfo enemy,
		int damageHalfHearts,
		out float hitStartDelaySeconds,
		out float visualImpactDelaySeconds)
	{
		hitStartDelaySeconds = 0f;
		visualImpactDelaySeconds = 0f;
		var timing = def != null ? def.attackTimingProfile : null;
		if (!EnemyAttackTiming.HasUsableProfile(timing))
			return false;

		float enemyAttackDuration = ResolveMeleeAttackDurationSeconds(def, animator);
		if (enemyAttackDuration <= 0f)
			return false;

		visualImpactDelaySeconds = EnemyAttackTiming.ComputeImpactDelay(
			enemyAttackDuration,
			timing.impactNormalizedTime);
		float playerHitDuration = ResolvePlayerHitDurationSeconds(
			enemy != null ? enemy.rank : 0,
			damageHalfHearts);
		float reactionNormalizedTime = EnemyAttackTiming.ResolvePlayerReactionNormalizedTime(
			timing,
			enemy != null ? enemy.rank : 0,
			damageHalfHearts);
		hitStartDelaySeconds = EnemyAttackTiming.ComputePlayerHitStartDelay(
			enemyAttackDuration,
			timing.impactNormalizedTime,
			playerHitDuration,
			reactionNormalizedTime);
		return true;
	}

	bool TryResolveMeleeImpactDelay(MobDef def, EnemySpriteAnimator animator, out float delaySeconds)
	{
		delaySeconds = 0f;
		var timing = def != null ? def.attackTimingProfile : null;
		if (!EnemyAttackTiming.HasUsableProfile(timing))
			return false;

		float enemyAttackDuration = ResolveMeleeAttackDurationSeconds(def, animator);
		if (enemyAttackDuration <= 0f)
			return false;

		delaySeconds = EnemyAttackTiming.ComputeImpactDelay(
			enemyAttackDuration,
			timing.impactNormalizedTime);
		return true;
	}

	float ResolveMeleeAttackDurationSeconds(MobDef def, EnemySpriteAnimator animator)
	{
		if (animator != null && animator.AttackDurationSeconds > 0f)
			return animator.AttackDurationSeconds;

		if (def == null || def.attackSpriteFrameCount <= 0 || def.attackFrameRate <= 0f)
			return 0f;

		return def.attackSpriteFrameCount / Mathf.Max(1f, def.attackFrameRate);
	}

	float ResolvePlayerHitDurationSeconds(int enemyRank, int damageHalfHearts)
	{
		if (ctx == null || ctx.playerBodyAnimator == null)
			return 0f;

		int frameCount = ctx.playerBodyAnimator.ResolveHitFrameCountByEnemyRank(enemyRank, damageHalfHearts);
		if (frameCount <= 0 || ctx.playerBodyAnimator.ActionFrameRate <= 0f)
			return 0f;

		return frameCount / Mathf.Max(1f, ctx.playerBodyAnimator.ActionFrameRate);
	}

	IEnumerator PlayPlayerDefenseFeedback()
	{
		bool defenseSessionActive = ctx.playerBodyAnimator != null && ctx.playerBodyAnimator.IsDefenseSessionActive;
		Coroutine defenseRoutine = !defenseSessionActive && ctx.playerBodyAnimator != null
			? ctx.playerBodyAnimator.PlayDefense()
			: null;
		if (ctx.battleAnims != null && ctx.playerBody != null)
			ctx.battleAnims.FlashHit(ctx.playerBody, new Color(0.35f, 0.85f, 1f), 0.14f, 0.24f);

		if (defenseSessionActive)
			yield return new WaitForSeconds(0.18f);
		else if (defenseRoutine != null)
			yield return new WaitWhile(() => ctx.playerBodyAnimator != null && ctx.playerBodyAnimator.IsActionPlaying);
		else
			yield return new WaitForSeconds(0.18f);
	}

	void ApplyPendingPlayerDamageAtImpact(int enemyIndex, int damageHalfHearts)
	{
		if (lastDefenseDamageApplied || lastDefenseBlocked || damageHalfHearts <= 0)
			return;

		lastDefenseDamageApplied = true;
		bool revived = GameSessionManager.TakePlayerDamage(damageHalfHearts);
		ctx.updatePlayerHud?.Invoke();
		AudioManager.Play("Gauge_Empty");
		if (GameSessionManager.IsPlayerAlive && GameSessionManager.PlayerHearts.TotalHalfHearts <= 2)
			AudioManager.Play("Alert_LowHP");

		if (revived)
		{
			if (ctx.hud != null)
				ctx.hud.FlashRevive();
			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry("<color=#55FF88>부활의 부적이 빛난다! 데미지 무효화!</color>",
					BattleEventPresentation.LogAndAnimation);
		}

		if (!GameSessionManager.IsPlayerAlive)
		{
			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry("<color=#FF3333>도박사가 쓰러졌다...</color>",
					BattleEventPresentation.LogAndAnimation);
		}
		else if (ctx.enemies != null && enemyIndex >= 0 && enemyIndex < ctx.enemies.Count)
		{
			Debug.Log($"[Battle] Defense impact from {ctx.enemies[enemyIndex].name} hearts={GameSessionManager.PlayerHearts.TotalHalfHearts}");
		}
	}

	IEnumerator EndPlayerDefenseSession()
	{
		if (ctx == null || ctx.playerBodyAnimator == null || !ctx.playerBodyAnimator.IsDefenseSessionActive)
			yield break;

		ctx.playerBodyAnimator.EndDefenseSession();
		yield return new WaitWhile(() =>
			ctx != null
			&& ctx.playerBodyAnimator != null
			&& ctx.playerBodyAnimator.IsDefenseSessionActive);
	}

	IEnumerator PlayPlayerHitFeedback(EnemyInfo enemy, int damageHalfHearts, EnemyAttackMotionProfile profile)
	{
		if (ctx.battleAnims == null || ctx.playerBody == null)
			yield break;

		Coroutine hitRoutine = StartPlayerHitAnimation(enemy, damageHalfHearts);
		FlashPlayerDamage(profile);
		if (hitRoutine != null)
			yield return new WaitWhile(() => ctx.playerBodyAnimator != null && ctx.playerBodyAnimator.IsActionPlaying);
		else
			yield return new WaitForSeconds(profile.hitPause);
	}

	Coroutine StartPlayerHitAnimation(EnemyInfo enemy, int damageHalfHearts)
	{
		return ctx.playerBodyAnimator != null
			? ctx.playerBodyAnimator.PlayHitByEnemyRank(enemy != null ? enemy.rank : 0, damageHalfHearts)
			: null;
	}

	void FlashPlayerDamage(EnemyAttackMotionProfile profile)
	{
		if (ctx.battleAnims == null || ctx.playerBody == null)
			return;

		ctx.battleAnims.FlashDamage(
			ctx.playerBody,
			holdTime: profile.hitFlashHoldTime,
			fadeTime: profile.hitFlashFadeTime);
	}

	IEnumerator PlayDraculaLaserAttack(int enemyIndex, EnemyInfo enemy, RectTransform bodyRt,
		bool blocked, int damageHalfHearts, EnemyAttackMotionProfile profile)
	{
		Coroutine feedbackRoutine = null;
		bool impactResolved = false;
		System.Action resolveImpact = () =>
		{
			if (impactResolved)
				return;
			impactResolved = true;
			if (blocked)
			{
				feedbackRoutine = StartCoroutine(PlayPlayerDefenseFeedback());
				return;
			}

			ApplyPendingPlayerDamageAtImpact(enemyIndex, damageHalfHearts);
			feedbackRoutine = StartCoroutine(PlayPlayerHitFeedback(enemy, damageHalfHearts, profile));
		};

		Coroutine laserRoutine = null;
		if (attackProjectileVfx != null && ctx.playerBody != null)
			laserRoutine = attackProjectileVfx.PlayDraculaLaser(bodyRt, ctx.playerBody.rectTransform, resolveImpact);
		else
			Debug.LogWarning("[DraculaLaserAttackVfx] attackProjectileVfx 또는 playerBody가 없어 레이저 VFX를 생략합니다.");

		if (laserRoutine != null)
			yield return laserRoutine;
		else
			resolveImpact();

		if (feedbackRoutine != null)
			yield return feedbackRoutine;
	}

	MobDef ResolveMobDef(EnemyInfo enemy)
	{
		if (enemy == null)
			return null;
		return ctx?.findMobDef?.Invoke(enemy.name);
	}

	string ResolveEnemyDiceProfileId(EnemyInfo enemy)
	{
		if (enemy == null)
			return EnemyDiceProfile.DefaultId;

		string profileId = ctx?.findEnemyDiceProfileId?.Invoke(enemy.name);
		return string.IsNullOrWhiteSpace(profileId)
			? EnemyDiceStyleResolver.ResolveProfileId(enemy)
			: profileId;
	}

	static bool HasUniqueAttackMotion(MobDef def)
	{
		return def != null && !string.IsNullOrWhiteSpace(def.uniqueAttackProfileId);
	}

	static bool HasAttackProjectileVfx(MobDef def)
	{
		return def != null && !string.IsNullOrWhiteSpace(def.attackVfxSpritePath);
	}

	Sprite ResolveAttackVfxSprite(EnemyInfo enemy)
	{
		if (enemy == null)
			return null;
		return ctx?.findEnemyAttackVfxSprite?.Invoke(enemy.name);
	}

	static float AttackApproachDuration(MobDef def, EnemyAttackMotionProfile profile)
	{
		return def != null && def.attackApproachDuration > 0f
			? def.attackApproachDuration
			: profile.approachDuration;
	}

	static float AttackRetreatDuration(MobDef def, EnemyAttackMotionProfile profile)
	{
		return def != null && def.attackRetreatDuration > 0f
			? def.attackRetreatDuration
			: profile.retreatDuration;
	}

	EnemyAttackMotionProfile ResolveAttackMotionProfile(EnemyInfo enemy)
	{
		if (GameSessionManager.IsBossBattle)
			return EnemyAttackMotionProfile.BossDefault;

		var profile = EnemyAttackMotionProfile.Default;
		if (enemy == null)
			return profile;

		// 향후 몹별 공격 애니메이션 id가 생기면 이 메서드에서 프로필을 선택한다.
		// 예: enemy.name 또는 MobDef의 future attackMotionId 기준으로 switch.
		return profile;
	}

	bool UsesProjectileAttack(EnemyInfo enemy)
	{
		var def = ResolveMobDef(enemy);
		return def != null
			&& EnemyAttackPositionResolver.ResolveRangeType(def) == EnemyAttackRangeType.Ranged
			&& !string.IsNullOrEmpty(def.projectileSpritePath);
	}

	EnemyProjectileAttachmentFollower GetAttachmentFollower(int enemyIndex)
	{
		if (ctx?.enemyIdleProjectiles == null || enemyIndex < 0 || enemyIndex >= ctx.enemyIdleProjectiles.Length)
			return null;
		var projectile = ctx.enemyIdleProjectiles[enemyIndex];
		return projectile != null ? projectile.GetComponent<EnemyProjectileAttachmentFollower>() : null;
	}

	// ── 적 주사위 결과 표시 ─────────────────────────────────────
	/// <summary>
	/// 적 주사위 굴림 연출.
	///   1) EnemyDiceRoller.PlaceForCount — rank 개수에 맞춰 아레나 정적 배치
	///   2) rank 1~3은 UI 오버레이(RawImage → EnemyDiceCamera RT)를 몹 머리 위에 고정 표시
	///      rank 4~5는 정착 후 중앙 팝업으로 결과 표시
	///   3) RollForEnemy 실제 굴림 → settle
	///   4) 결과 콜백. Rank ≥ 4만 DrumRoll.
	/// </summary>
	IEnumerator AnimateEnemyDiceRoll3D(int enemyIndex, int rank, string diceProfileId,
		RectTransform enemyRt, System.Action<EnemyDiceResult> onComplete)
	{
		var presentationMode = ResolveDicePresentationMode(rank, true);
		var diceProfile = enemyDiceRoller != null
			? enemyDiceRoller.ResolveProfile(diceProfileId)
			: EnemyDiceProfile.CreateDefault();

		if (enemyDiceRoller != null)
			enemyDiceRoller.PlaceForCount(rank, diceProfileId);

		bool overlayReady = presentationMode == EnemyDicePresentationMode.Viewport
			&& enemyDiceOverlay != null
			&& enemyRt != null;
		if (overlayReady)
		{
			enemyDiceOverlay.gameObject.SetActive(true);
			PositionEnemyDiceOverlay(enemyIndex, enemyRt, diceProfile);
		}
		else
		{
			HideEnemyDiceOverlay();
		}

		EnemyDiceResult rolled = null;
		if (enemyDiceRoller != null)
		{
			bool done = false;
			enemyDiceRoller.RollForEnemy(rank, diceProfileId, r => { rolled = r; done = true; });
			while (!done) yield return null;
		}
		else
		{
			Debug.LogWarning("[EnemyDice] enemyDiceRoller 미할당 — 물리 롤러 씬 wiring 확인 필요");
		}

		if (rolled != null)
		{
			if (presentationMode == EnemyDicePresentationMode.CenterPopup)
				ShowEnemyDiceCenterPopup(rank, rolled);
			Debug.Log($"[EnemyDice] roll enemy={enemyIndex} rank={rank} profile={diceProfileId} values=[{string.Join(",", rolled.values)}] combo=\"{rolled.comboName}\"");
		}
		onComplete?.Invoke(rolled);
	}

	void ShowEnemyDiceCenterPopup(int rank, EnemyDiceResult result)
	{
		if (result == null || result.values == null || result.values.Length == 0)
			return;

		EnsureEnemyDiceCenterPopup();
		if (enemyDicePopup == null || enemyDicePopupValueTexts == null)
			return;

		enemyDicePopupTitle.text = $"적 {rank}성 주사위 결과";
		enemyDicePopupCombo.text = result.hasCombo && !string.IsNullOrEmpty(result.comboName)
			? $"<color=#FFD94A>{result.comboName}</color>"
			: "방어 목표";

		int count = Mathf.Min(result.values.Length, enemyDicePopupValueTexts.Length);
		for (int i = 0; i < enemyDicePopupValueTexts.Length; i++)
		{
			var text = enemyDicePopupValueTexts[i];
			if (text == null)
				continue;

			bool active = i < count;
			text.transform.parent.gameObject.SetActive(active);
			text.text = active ? result.values[i].ToString() : "";
		}

		enemyDicePopup.SetActive(true);
	}

	void EnsureEnemyDiceCenterPopup()
	{
		if (enemyDicePopup == null || enemyDicePopupPanel != null)
			return;

		var root = enemyDicePopup.GetComponent<RectTransform>();
		if (root == null)
			root = enemyDicePopup.AddComponent<RectTransform>();
		root.anchorMin = Vector2.zero;
		root.anchorMax = Vector2.one;
		root.offsetMin = Vector2.zero;
		root.offsetMax = Vector2.zero;
		root.pivot = new Vector2(0.5f, 0.5f);

		var canvasGroup = enemyDicePopup.GetComponent<CanvasGroup>();
		if (canvasGroup == null)
			canvasGroup = enemyDicePopup.AddComponent<CanvasGroup>();
		canvasGroup.interactable = false;
		canvasGroup.blocksRaycasts = false;

		var panelGo = new GameObject("CenterPanel");
		panelGo.transform.SetParent(enemyDicePopup.transform, false);
		enemyDicePopupPanel = panelGo.AddComponent<RectTransform>();
		enemyDicePopupPanel.anchorMin = new Vector2(0.5f, 0.5f);
		enemyDicePopupPanel.anchorMax = new Vector2(0.5f, 0.5f);
		enemyDicePopupPanel.pivot = new Vector2(0.5f, 0.5f);
		enemyDicePopupPanel.anchoredPosition = Vector2.zero;
		enemyDicePopupPanel.sizeDelta = new Vector2(620f, 260f);

		var panelImage = panelGo.AddComponent<Image>();
		panelImage.color = new Color(0.08f, 0.10f, 0.16f, 0.96f);
		panelImage.raycastTarget = false;

		var panelOutline = panelGo.AddComponent<Outline>();
		panelOutline.effectColor = new Color(1f, 0.85f, 0.30f, 0.85f);
		panelOutline.effectDistance = new Vector2(3f, -3f);

		enemyDicePopupTitle = CreatePopupText(enemyDicePopupPanel, "Title",
			new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
			new Vector2(0f, -34f), new Vector2(560f, 44f),
			"적 주사위 결과", 30f, Color.white, TextAlignmentOptions.Center);
		enemyDicePopupTitle.fontStyle = FontStyles.Bold;

		enemyDicePopupCombo = CreatePopupText(enemyDicePopupPanel, "Combo",
			new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
			new Vector2(0f, -78f), new Vector2(560f, 36f),
			"방어 목표", 24f, new Color(1f, 0.86f, 0.34f), TextAlignmentOptions.Center);
		enemyDicePopupCombo.fontStyle = FontStyles.Bold;

		var rowGo = new GameObject("DiceRow");
		rowGo.transform.SetParent(enemyDicePopupPanel, false);
		var row = rowGo.AddComponent<RectTransform>();
		row.anchorMin = new Vector2(0.5f, 0f);
		row.anchorMax = new Vector2(0.5f, 0f);
		row.pivot = new Vector2(0.5f, 0f);
		row.anchoredPosition = new Vector2(0f, 28f);
		row.sizeDelta = new Vector2(540f, 96f);

		var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
		layout.childAlignment = TextAnchor.MiddleCenter;
		layout.childControlHeight = false;
		layout.childControlWidth = false;
		layout.childForceExpandHeight = false;
		layout.childForceExpandWidth = false;
		layout.spacing = 14f;
		layout.padding = new RectOffset(0, 0, 0, 0);

		enemyDicePopupValueTexts = new TMP_Text[5];
		for (int i = 0; i < enemyDicePopupValueTexts.Length; i++)
		{
			var cellGo = new GameObject($"DieValue{i}");
			cellGo.transform.SetParent(row, false);
			var cellRt = cellGo.AddComponent<RectTransform>();
			cellRt.sizeDelta = new Vector2(82f, 82f);

			var layoutElement = cellGo.AddComponent<LayoutElement>();
			layoutElement.preferredWidth = 82f;
			layoutElement.preferredHeight = 82f;

			var cellImage = cellGo.AddComponent<Image>();
			cellImage.color = new Color(0.94f, 0.91f, 0.82f, 1f);
			cellImage.raycastTarget = false;

			var outline = cellGo.AddComponent<Outline>();
			outline.effectColor = new Color(0f, 0f, 0f, 0.65f);
			outline.effectDistance = new Vector2(2f, -2f);

			var valueText = CreatePopupText(cellRt, "Value",
				Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero,
				"", 52f, new Color(0.08f, 0.08f, 0.10f), TextAlignmentOptions.Center);
			valueText.fontStyle = FontStyles.Bold;
			enemyDicePopupValueTexts[i] = valueText;
		}
	}

	TMP_Text CreatePopupText(RectTransform parent, string name,
		Vector2 anchorMin, Vector2 anchorMax, Vector2 anchoredPosition, Vector2 sizeDelta,
		string text, float fontSize, Color color, TextAlignmentOptions alignment)
	{
		var go = new GameObject(name);
		go.transform.SetParent(parent, false);
		var label = go.AddComponent<TextMeshProUGUI>();
		var rt = label.rectTransform;
		rt.anchorMin = anchorMin;
		rt.anchorMax = anchorMax;
		rt.pivot = new Vector2(0.5f, 0.5f);
		rt.anchoredPosition = anchoredPosition;
		rt.sizeDelta = sizeDelta;
		label.text = text;
		label.fontSize = fontSize;
		label.color = color;
		label.alignment = alignment;
		label.raycastTarget = false;
		label.enableWordWrapping = false;
		return label;
	}

	void PositionEnemyDiceOverlay(int enemyIndex, RectTransform enemyRt, EnemyDiceProfile profile)
	{
		if (enemyDiceOverlay == null || enemyRt == null)
			return;

		float aspect = profile != null && profile.overlayAspect > 0f ? profile.overlayAspect : 16f / 9f;
		float minHeight = Mathf.Max(1f, profile != null ? profile.overlayMinHeight : 112f);
		float maxHeight = Mathf.Max(minHeight, profile != null ? profile.overlayMaxHeight : 112f);
		float headGap = profile != null ? profile.overlayHeadGap : 8f;
		float overlayHeight = maxHeight;
		enemyDiceOverlay.sizeDelta = new Vector2(overlayHeight * aspect, overlayHeight);
		StretchEnemyDiceViewportToOverlay();

		RectTransform reference = ResolveEnemyInfoPanel(enemyIndex) ?? enemyRt;
		var parent = enemyDiceOverlay.parent as RectTransform;
		Bounds bounds = parent != null
			? RectTransformUtility.CalculateRelativeRectTransformBounds(parent, reference)
			: new Bounds(reference.position, new Vector3(reference.rect.width, reference.rect.height, 0f));

		Vector3 topCenter = bounds.center + new Vector3(0f, bounds.extents.y + overlayHeight * 0.5f + headGap, 0f);
		if (parent != null)
		{
			enemyDiceOverlay.localPosition = topCenter;
		}
		else
		{
			enemyDiceOverlay.position = topCenter;
		}
	}

	void StretchEnemyDiceViewportToOverlay()
	{
		var rawImage = enemyDiceOverlay.GetComponentInChildren<RawImage>(true);
		if (rawImage == null)
			return;

		var rt = rawImage.rectTransform;
		rt.anchorMin = Vector2.zero;
		rt.anchorMax = Vector2.one;
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	RectTransform ResolveEnemyInfoPanel(int enemyIndex)
	{
		if (ctx?.enemyNames == null || enemyIndex < 0 || enemyIndex >= ctx.enemyNames.Length)
			return null;

		var name = ctx.enemyNames[enemyIndex];
		return name != null ? name.transform.parent as RectTransform : null;
	}

	void HideEnemyDiceOverlay()
	{
		if (enemyDiceOverlay != null)
			enemyDiceOverlay.gameObject.SetActive(false);
	}

	void HideEnemyDiceCenterPopup()
	{
		if (enemyDicePopup != null)
			enemyDicePopup.SetActive(false);
	}

	void HideEnemyDicePresentation()
	{
		HideEnemyDiceOverlay();
		HideEnemyDiceCenterPopup();
	}

	// ── 보스/무리 공용 애니메이션 ─────────────────────────────────
	IEnumerator EnemyJumpAnimation(RectTransform body, EnemyAttackMotionProfile profile)
	{
		Vector2 originalPos = body.anchoredPosition;
		float jumpHeight = 30f;
		float jumpUpTime = profile.bossJumpUpTime;
		float slamDistance = 40f;
		float slamDownTime = profile.bossSlamDownTime;
		float holdTime = profile.bossHoldTime;
		float returnTime = profile.bossReturnTime;

		float elapsed = 0f;
		while (elapsed < jumpUpTime)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / jumpUpTime;
			body.anchoredPosition = originalPos + Vector2.up * jumpHeight * t;
			yield return null;
		}

		elapsed = 0f;
		Vector2 topPos = originalPos + Vector2.up * jumpHeight;
		float totalDown = jumpHeight + slamDistance;
		while (elapsed < slamDownTime)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / slamDownTime;
			body.anchoredPosition = topPos + Vector2.down * totalDown * t;
			yield return null;
		}
		body.anchoredPosition = originalPos + Vector2.down * slamDistance;

		yield return new WaitForSeconds(holdTime);

		elapsed = 0f;
		Vector2 slamPos = body.anchoredPosition;
		while (elapsed < returnTime)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / returnTime;
			body.anchoredPosition = Vector2.Lerp(slamPos, originalPos, t);
			yield return null;
		}
		body.anchoredPosition = originalPos;
	}

	IEnumerator EnemySlamAnimation(RectTransform body, EnemyAttackMotionProfile profile, System.Action onImpact = null)
	{
		Vector2 originalPos = body.anchoredPosition;
		float slamDistance = 40f;
		float slamDownTime = profile.slamRushTime;
		float holdTime = profile.slamHoldTime;
		float returnTime = profile.slamReturnTime;

		float elapsed = 0f;
		while (elapsed < slamDownTime)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / slamDownTime;
			body.anchoredPosition = originalPos + Vector2.down * slamDistance * t;
			yield return null;
		}
		body.anchoredPosition = originalPos + Vector2.down * slamDistance;
		onImpact?.Invoke();

		yield return new WaitForSeconds(holdTime);

		elapsed = 0f;
		Vector2 slamPos = body.anchoredPosition;
		while (elapsed < returnTime)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / returnTime;
			body.anchoredPosition = Vector2.Lerp(slamPos, originalPos, t);
			yield return null;
		}
		body.anchoredPosition = originalPos;
	}

	/// <summary>반칸 수를 "N칸" 또는 "N칸 반" 형태로 표시.</summary>
	static string FormatHalf(int halfHearts)
	{
		int full = halfHearts / 2;
		bool half = halfHearts % 2 != 0;
		if (half)
			return full > 0 ? $"{full}칸 반" : "반 칸";
		return $"{full}칸";
	}
}

/// <summary>
/// BSC가 EnemyCounterAttackDirector에게 주입하는 런타임 공유 상태.
/// MonoBehaviour 의존성을 건드리지 않고 순수 C# 객체로 생성·전달된다.
/// 빌더가 다루지 않으므로 SerializeField로 노출할 필요 없음.
/// </summary>
public sealed class BattleControllerContext
{
	public List<EnemyInfo> enemies;
	public GameObject[] enemyPanels;
	public Image[] enemyBodies;
	public Image[] enemyIdleProjectiles;
	public EnemySpriteAnimator[] enemyAnimators;
	public TMP_Text[] enemyNames;
	public Image playerBody;
	public PlayerBodyAnimator playerBodyAnimator;
	public BattleLog battleLog;
	public BattleAnimations battleAnims;
	public DiceRollDirector diceDirector;
	public Button rollButton;
	public Button confirmButton;
	public Button nextRoundButton;
	public BattleHudPresenter hud;

	public Action updatePlayerHud;            // BSC의 UpdatePlayerHUD 래핑
	public Action<bool> setRoundConfirmed;    // BSC의 roundConfirmed 플래그 쓰기
	public Action startPlayerDefeatedRoutine; // BSC.StartCoroutine(PlayerDefeatedRoutine())
	public Func<string, MobDef> findMobDef;
	public Func<string, string> findEnemyDiceProfileId;
	public Func<string, Sprite> findEnemyAttackVfxSprite;
}
