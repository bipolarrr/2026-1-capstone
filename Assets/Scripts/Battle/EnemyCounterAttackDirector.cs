using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 적 반격(공격 연출 → 주사위 굴림 → 방어 페이즈 게이트 → 슬램/회피)의 전체 흐름과
/// 관련 UI(적 주사위 오버레이/결과 텍스트/주사위 얼굴 표시)를 소유한다.
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
		public float diceOverlayFlyDuration;
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
			diceOverlayFlyDuration = 0.8f,
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
	[SerializeField] GameObject[] enemyDiceFaceContainers;
	[SerializeField] Sprite[] diceFaceSprites;
	[SerializeField] PlayerJumpAnimator jumpAnimator;

	// ── 런타임에 BSC가 Bind로 주입 ─────────────────────────────────
	BattleControllerContext ctx;

	// ── 방어 페이즈 상태 (BSC에서 이동) ────────────────────────────
	bool isDefensePhase;
	bool defenseConfirmed;
	int currentDefenseEnemyIndex;
	bool lastDefenseBlocked;
	int lastDefenseDamage;
	int defenseRollsMax;

	public bool IsDefensePhase => isDefensePhase;
	public int CurrentDefenseEnemyIndex => currentDefenseEnemyIndex;
	public int DefenseRollsMax => defenseRollsMax;

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
		// 레거시 오버레이는 시작 시 항상 비활성 (원본 BSC.Start 동작 보존).
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

		// 안전망: 다음 라운드 진입 시 잔존 주사위 얼굴 표시가 있으면 강제로 숨긴다(원본 동작 보존).
		if (enemyDiceFaceContainers != null)
		{
			foreach (var c in enemyDiceFaceContainers)
				if (c != null) c.SetActive(false);
		}
		HideEnemyDiceOverlay();
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

			var attackProfile = ResolveAttackMotionProfile(enemies[i]);

			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry($"<color=#FF6666>{enemies[i].name}의 공격!</color>");

			AudioManager.Play(enemies[i].rank >= 4 ? "Enemy45_Attack" : "Enemy123_Attack");
			if (ctx.enemyAnimators != null && i < ctx.enemyAnimators.Length && ctx.enemyAnimators[i] != null)
				ctx.enemyAnimators[i].PlayAttack();

			bool isMelee = ctx.battleAnims != null && !GameSessionManager.IsBossBattle
				&& i < ctx.enemyPanels.Length && ctx.enemyPanels[i] != null && ctx.playerBody != null;

			RectTransform slotRt = null;
			Vector3 slotOriginalLocal = Vector3.zero;

			if (isMelee)
			{
				slotRt = ctx.enemyPanels[i].GetComponent<RectTransform>();
				slotOriginalLocal = slotRt.localPosition;

				// ① 플레이어 앞까지 이동 — 주사위 2개 정도 거리를 남긴다.
				Vector3 slotWorld = slotRt.position;
				Vector3 playerWorld = ctx.playerBody.rectTransform.position;
				float scale = ctx.enemyBodies[i].rectTransform.lossyScale.x;
				float bodyWidth = ctx.enemyBodies[i].rectTransform.rect.width * scale;
				float diceGap = 42f * scale * 2.4f;
				Vector3 playerFrontWorld = new Vector3(
					playerWorld.x + bodyWidth + diceGap,
					slotWorld.y, slotWorld.z);
				yield return StartCoroutine(ctx.battleAnims.WalkTo(slotRt, playerFrontWorld, attackProfile.approachDuration));

				// ② 제자리 점프
				if (i < ctx.enemyBodies.Length && ctx.enemyBodies[i] != null)
					yield return StartCoroutine(ctx.battleAnims.JumpInPlace(
						ctx.enemyBodies[i].rectTransform,
						duration: attackProfile.anticipationJumpDuration));
			}
			else if (i < ctx.enemyBodies.Length && ctx.enemyBodies[i] != null)
			{
				// 보스: 제자리 점프만
				yield return StartCoroutine(EnemyJumpAnimation(ctx.enemyBodies[i].rectTransform, attackProfile));
			}

			// ── 적 주사위 굴림 연출 ──
			EnemyDiceResult result = null;
			RectTransform enemyRt = (slotRt != null) ? slotRt : (i < ctx.enemyPanels.Length ? ctx.enemyPanels[i].GetComponent<RectTransform>() : null);
			yield return StartCoroutine(AnimateEnemyDiceRoll3D(i, enemies[i].rank, enemyRt, attackProfile, r => result = r));

			enemies[i].lastDiceResult = result;

			if (i < enemyDiceResultTexts.Length && enemyDiceResultTexts[i] != null)
			{
				if (result.hasCombo)
					enemyDiceResultTexts[i].text = $"<color=#FFD94A>{result.comboName}</color>";
				else
					enemyDiceResultTexts[i].text = "";
			}

			if (ctx.battleLog != null)
			{
				string diceStr = $"[{string.Join(", ", result.values)}]";
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
			defenseRollsMax = (enemies[i].rank >= 4) ? 3 : 1;
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

			if (ctx.battleLog != null)
			{
				if (result.hasCombo)
					ctx.battleLog.AddEntry($"<color=#FFD94A>{enemies[i].name}의 {result.comboName}을(를) 막아라! 3회 굴림!</color>");
				else
					ctx.battleLog.AddEntry($"{enemies[i].name}의 공격을 방어하자! 1회 굴림!");
			}

			while (!defenseConfirmed)
				yield return null;

			// ── ④⑤ 방어 결과 후속 연출 ──
			if (isMelee && slotRt != null)
			{
				if (!lastDefenseBlocked)
				{
					Vector3 slamTarget = new Vector3(ctx.playerBody.rectTransform.position.x, slotRt.position.y, slotRt.position.z);
					yield return StartCoroutine(ctx.battleAnims.QuickSlam(
						slotRt, slamTarget,
						rushTime: attackProfile.slamRushTime,
						holdTime: attackProfile.slamHoldTime,
						returnTime: attackProfile.slamReturnTime));
					if (ctx.battleAnims != null && ctx.playerBody != null)
					{
						ctx.playerBodyAnimator?.PlayHitByDamage(lastDefenseDamage);
						ctx.battleAnims.FlashHit(
							ctx.playerBody,
							holdTime: attackProfile.hitFlashHoldTime,
							fadeTime: attackProfile.hitFlashFadeTime);
					}
					yield return new WaitForSeconds(attackProfile.hitPause);
				}
				else
				{
					// 완벽방어: 적 돌진 + 플레이어 회피 점프 병렬
					Vector3 slamTarget = new Vector3(ctx.playerBody.rectTransform.position.x, slotRt.position.y, slotRt.position.z);
					if (jumpAnimator != null)
						jumpAnimator.Play();
					yield return StartCoroutine(ctx.battleAnims.QuickSlam(
						slotRt, slamTarget,
						rushTime: attackProfile.slamRushTime,
						holdTime: attackProfile.slamHoldTime,
						returnTime: attackProfile.slamReturnTime));
					if (jumpAnimator != null)
						yield return new WaitWhile(() => jumpAnimator.IsPlaying);
				}
				yield return StartCoroutine(ctx.battleAnims.WalkBack(slotRt, slotOriginalLocal, attackProfile.retreatDuration));
			}
			else if (!lastDefenseBlocked)
			{
				if (i < ctx.enemyBodies.Length && ctx.enemyBodies[i] != null)
					yield return StartCoroutine(EnemySlamAnimation(ctx.enemyBodies[i].rectTransform, attackProfile));
				if (ctx.battleAnims != null && ctx.playerBody != null)
				{
					ctx.playerBodyAnimator?.PlayHitByDamage(lastDefenseDamage);
					ctx.battleAnims.FlashHit(
						ctx.playerBody,
						holdTime: attackProfile.hitFlashHoldTime,
						fadeTime: attackProfile.hitFlashFadeTime);
				}
			}

			// 플레이어 사망 시 중단
			if (!GameSessionManager.IsPlayerAlive)
			{
				ctx.startPlayerDefeatedRoutine?.Invoke();
				yield break;
			}

			// 이 적의 주사위 결과 초기화
			if (i < enemyDiceResultTexts.Length && enemyDiceResultTexts[i] != null)
				enemyDiceResultTexts[i].text = "";
			HideEnemyDiceFaces(i);
			HideEnemyDiceOverlay();

			yield return new WaitForSeconds(attackProfile.postEnemyPause);
		}

		// 모든 적의 공격이 끝남 — 다음 라운드 버튼 표시
		isDefensePhase = false;
		if (ctx.confirmButton != null) ctx.confirmButton.gameObject.SetActive(false);
		if (ctx.rollButton != null) ctx.rollButton.interactable = false;
		if (ctx.nextRoundButton != null) ctx.nextRoundButton.gameObject.SetActive(true);
	}

	/// <summary>
	/// 굴림 정착 후 BSC.HandleRollSettled에서 호출. 자동 확정 조건(rank &lt; 4 또는 완벽 방어)을
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

		bool autoConfirm = enemies[ci].rank < 4;
		if (!autoConfirm)
		{
			int[] vals = ctx.diceDirector.ReadFinalValues();
			var defense = DefenseCalculator.Evaluate(vals, enemies[ci].lastDiceResult);
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

		if (ctx.battleLog != null)
			ctx.battleLog.AddEntry($"도박사의 방어 주사위: [{string.Join(", ", playerValues)}]");

		var result = enemies[i].lastDiceResult;
		var defense = DefenseCalculator.Evaluate(playerValues, result);

		// 랭크 1~3: 비례 방어 없음 (완벽 방어 or 전부 피격)
		float reduction = enemies[i].rank >= 4 ? defense.reductionRate : (defense.blocked ? 1f : 0f);
		int baseDmg = DefenseCalculator.CalculateEnemyDamage(enemies[i].rank, result.damageMultiplier);
		int finalDmg = Mathf.Max(0, Mathf.CeilToInt(baseDmg * (1f - reduction)));

		lastDefenseBlocked = defense.blocked || finalDmg <= 0;
		lastDefenseDamage = finalDmg;

		if (defense.blocked)
		{
			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry($"  <color=#55FF55>{enemies[i].name}: 완벽 방어!</color>");
			AudioManager.Play("Player_PerfectDefense");
		}
		else if (finalDmg <= 0)
		{
			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry($"  <color=#55FF55>{enemies[i].name}: 데미지 0!</color>");
			AudioManager.Play("Player_PerfectDefense");
		}
		else
		{
			if (ctx.battleLog != null)
			{
				if (enemies[i].rank >= 4 && reduction > 0f)
					ctx.battleLog.AddEntry($"  <color=#FFAA44>{enemies[i].name}: {Mathf.RoundToInt(reduction * 100)}% 방어 → {FormatHalf(finalDmg)} 데미지</color>");
				else
					ctx.battleLog.AddEntry($"  <color=#FF6666>{enemies[i].name}: 방어 실패! ({FormatHalf(finalDmg)} 데미지)</color>");
			}

			if (enemies[i].rank >= 4 && reduction > 0f)
				AudioManager.Play("Player_PartialDefense");

			bool revived = GameSessionManager.TakePlayerDamage(finalDmg);
			ctx.updatePlayerHud?.Invoke();
			AudioManager.Play("Gauge_Empty");
			if (GameSessionManager.IsPlayerAlive && GameSessionManager.PlayerHearts.TotalHalfHearts <= 2)
				AudioManager.Play("Alert_LowHP");

			if (revived)
			{
				if (ctx.hud != null)
					ctx.hud.FlashRevive();
				if (ctx.battleLog != null)
					ctx.battleLog.AddEntry("<color=#55FF88>부활의 부적이 빛난다! 데미지 무효화!</color>");
			}
		}

		enemies[i].lastDiceResult = null;

		if (!GameSessionManager.IsPlayerAlive)
		{
			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry("<color=#FF3333>도박사가 쓰러졌다...</color>");
		}
		else
		{
			if (ctx.battleLog != null)
				ctx.battleLog.AddEntry($"  도박사 하트: {FormatHalf(GameSessionManager.PlayerHearts.TotalHalfHearts)}");
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
			ctx.hud.ShowDefensePreview("<color=#55FF55>방어 성공!</color>");
		}
		else if (enemies[i].rank >= 4)
		{
			if (enemyResult.hasCombo)
			{
				ctx.hud.ShowDefensePreview($"<color=#FF6666>{enemyResult.comboName}을(를) 만들어라!</color>");
			}
			else
			{
				int matched = DefenseCalculator.CountMatches(playerValues, enemyResult.values);
				int remaining = enemyResult.values.Length - matched;
				if (remaining > 0)
					ctx.hud.ShowDefensePreview($"<color=#FFAA44>남은 눈: {remaining}개</color>");
				else
					ctx.hud.ShowDefensePreview("<color=#55FF55>방어 성공!</color>");
			}
		}
		else
		{
			ctx.hud.ShowDefensePreview($"<color=#FF6666>{enemies[i].name}: 방어 실패</color>");
		}
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

	// ── 적 3D 주사위 연출 ───────────────────────────────────────
	/// <summary>
	/// 적 주사위 굴림 3D 연출.
	///   1) EnemyDiceRoller.PlaceForCount — rank 개수에 맞춰 아레나 정적 배치
	///   2) UI 오버레이(RawImage → EnemyDiceCamera RT)를 적 좌측 하단에 띄움
	///   3) 오버레이를 적과 플레이어 중간점까지 선형 이동
	///   4) 도착 순간 RollForEnemy 실제 굴림 → settle
	///   5) 결과 콜백. Rank ≥ 4만 DrumRoll.
	/// </summary>
	IEnumerator AnimateEnemyDiceRoll3D(int enemyIndex, int rank, RectTransform enemyRt,
		EnemyAttackMotionProfile profile, System.Action<EnemyDiceResult> onComplete)
	{
		if (enemyDiceRoller != null)
			enemyDiceRoller.PlaceForCount(rank);

		bool overlayReady = enemyDiceOverlay != null && enemyRt != null && ctx.playerBody != null;
		Vector3 startWorld = Vector3.zero, endWorld = Vector3.zero;
		if (overlayReady)
		{
			var rect = enemyRt.rect;
			startWorld = enemyRt.TransformPoint(new Vector3(rect.xMin, rect.yMin, 0f));
			Vector3 enemyCenter  = enemyRt.position;
			Vector3 playerCenter = ctx.playerBody.rectTransform.position;
			endWorld = (enemyCenter + playerCenter) * 0.5f;
			endWorld.y = startWorld.y;

			enemyDiceOverlay.gameObject.SetActive(true);
			enemyDiceOverlay.position = startWorld;

			float flyDuration = profile.diceOverlayFlyDuration;
			float t = 0f;
			while (t < flyDuration)
			{
				t += Time.deltaTime;
				float k = Mathf.Clamp01(t / flyDuration);
				enemyDiceOverlay.position = Vector3.Lerp(startWorld, endWorld, k);
				yield return null;
			}
			enemyDiceOverlay.position = endWorld;
		}

		EnemyDiceResult rolled = null;
		if (enemyDiceRoller != null)
		{
			bool done = false;
			enemyDiceRoller.RollForEnemy(rank, r => { rolled = r; done = true; });
			while (!done) yield return null;
		}
		else
		{
			// 폴백: 물리 주사위가 없으면 즉시 랜덤 생성
			int count = Mathf.Max(1, rank);
			int[] vals = new int[count];
			for (int d = 0; d < count; d++) vals[d] = DiceRandomizer.Next();
			var (_, cn, _, _) = DamageCalculator.Calculate(
				PlayerAttackPipeline.PadToFive(vals), new List<PowerUpType>());
			bool hasCombo = count >= 4 && !string.IsNullOrEmpty(cn);
			if (!hasCombo) cn = "";
			rolled = new EnemyDiceResult
			{
				values = vals,
				comboName = cn,
				damageMultiplier = EnemyDiceResult.GetMultiplier(cn),
				hasCombo = hasCombo
			};
		}

		Debug.Log($"[EnemyDice] roll enemy={enemyIndex} rank={rank} values=[{string.Join(",", rolled.values)}] combo=\"{rolled.comboName}\"");
		onComplete?.Invoke(rolled);
	}

	void HideEnemyDiceOverlay()
	{
		if (enemyDiceOverlay != null)
			enemyDiceOverlay.gameObject.SetActive(false);
	}

	void ShowEnemyDiceFaces(int enemyIndex, int[] values)
	{
		if (enemyDiceFaceContainers == null || diceFaceSprites == null)
			return;
		if (enemyIndex < 0 || enemyIndex >= enemyDiceFaceContainers.Length)
			return;

		var container = enemyDiceFaceContainers[enemyIndex];
		if (container == null) return;
		container.SetActive(true);

		for (int d = 0; d < container.transform.childCount; d++)
		{
			var child = container.transform.GetChild(d);
			if (d < values.Length)
			{
				child.gameObject.SetActive(true);
				var img = child.GetComponent<Image>();
				if (img != null && values[d] >= 1 && values[d] <= 6)
					img.sprite = diceFaceSprites[values[d] - 1];
			}
			else
			{
				child.gameObject.SetActive(false);
			}
		}
	}

	void HideEnemyDiceFaces(int enemyIndex)
	{
		if (enemyDiceFaceContainers == null) return;
		if (enemyIndex < 0 || enemyIndex >= enemyDiceFaceContainers.Length) return;
		var container = enemyDiceFaceContainers[enemyIndex];
		if (container != null) container.SetActive(false);
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

	IEnumerator EnemySlamAnimation(RectTransform body, EnemyAttackMotionProfile profile)
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
	public EnemySpriteAnimator[] enemyAnimators;
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
}
