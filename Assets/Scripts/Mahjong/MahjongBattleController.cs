using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

namespace Mahjong
{
	/// <summary>
	/// 마작 전투 씬 컨트롤러. 1쯔모-1버림 단순 모델.
	/// 버림 즉시 적 OnPlayerDiscard 통지 → 발동 시 데미지. 일반 버림 턴에서는 공격한 적만 Reroll.
	/// DiceBattle 패턴 미러: 기본 적 세팅, 고정 4슬롯 적 UI, 스테이지 배경, 디버그 훅.
	/// </summary>
	public class MahjongBattleController : BattleControllerBase, IBattleDebugTarget
	{
		const int EnemySlotCount = 4;
		private const int TENPAI_COUNTER_DAMAGE_HALF_HEARTS = 4;
		private const int IISHANTEN_COUNTER_DAMAGE_HALF_HEARTS = 2;

		[Header("도라 / 버림 / 손패")]
		[SerializeField] Transform doraIndicatorRoot;
		[SerializeField] Transform handTilesRoot;
		[SerializeField] Transform drawTileSlot;
		[SerializeField] Transform discardRoot;
		[SerializeField] GameObject tilePrefab;
		[SerializeField] MahjongTileSpriteDatabase tileSprites;

		[Header("Buttons")]
		[SerializeField] Button kanButton;
		[SerializeField] Button riichiButton;
		[SerializeField] Button tempButton1;
		[SerializeField] Button tempButton2;
		[SerializeField] Button cancelButton;

		[SerializeField] Button[] enemyPanelButtons = new Button[EnemySlotCount];

		[Header("UI 인터랙션")]
		[SerializeField] MahjongDrawTileAnimator drawAnimator;
		[SerializeField] MahjongWaitInfoPanel waitInfoPanel;

		[Header("적 공격 연출")]
		[SerializeField] EnemyWaitTilesDisplay[] waitDisplays = new EnemyWaitTilesDisplay[EnemySlotCount];
		[SerializeField] RonSpeechBubble ronBubble;
		[SerializeField] MahjongIntuitionConfig intuitionConfig;
		[SerializeField] Image enemyProjectile;
		[SerializeField] EnemyAttackProjectileVfx attackProjectileVfx;
		[SerializeField, Range(0f, 1f)] float enemyTsumoChancePerTurn = 0.02f;
		[SerializeField, Range(0f, 1f)] float rank3WaitRevealChancePerTurn = 0.05f;
		[SerializeField] PlayerBodyAnimator playerBodyAnimator;
		[SerializeField] PlayerAttackAnimator attackAnimator;
		[SerializeField] BattleBottomFocusController bottomFocus;

		MahjongMatchState state;
		readonly List<EnemyMahjongState> enemyStates = new List<EnemyMahjongState>();
		readonly List<MahjongTileVisual> handVisuals = new List<MahjongTileVisual>();
		readonly List<MahjongWaitRevealDecision> lastWaitRevealDecisions = new List<MahjongWaitRevealDecision>();
		readonly Dictionary<int, MahjongDiscardDanger> currentDiscardDangerByTileIndex = new Dictionary<int, MahjongDiscardDanger>();
		readonly Dictionary<int, MahjongTenpaiDiscardOption> currentTenpaiDiscardOptionsByTileIndex = new Dictionary<int, MahjongTenpaiDiscardOption>();
		readonly List<MahjongDangerSource> dangerSourceScratch = new List<MahjongDangerSource>();
		MahjongRiichiAvailability currentRiichiAvailability = MahjongRiichiPolicy.Evaluate(null, false, canAct: false);
		MahjongTileVisual drawVisual;

		bool battleEnded;
		bool animating;

		static readonly Color TenpaiHintTint = new Color(0.78f, 0.94f, 1f, 1f);
		static readonly Color TenpaiHintBorder = new Color(0.45f, 0.86f, 1f, 0.95f);
		static readonly Color DangerHintTint = new Color(1f, 0.74f, 0.62f, 1f);
		static readonly Color DangerHintBorder = new Color(1f, 0.42f, 0.24f, 0.95f);
		static readonly Color LethalHintTint = new Color(1f, 0.48f, 0.48f, 1f);
		static readonly Color LethalHintBorder = new Color(1f, 0.12f, 0.12f, 0.98f);

		enum MahjongPlayerAttackKind
		{
			Partial,
			FullWin,
			Counter
		}

		readonly struct PartialAttackPreview
		{
			public readonly bool CanAttack;
			public readonly PartialBreakdown Breakdown;
			public readonly int DamageHalfHearts;

			public PartialAttackPreview(bool canAttack, PartialBreakdown breakdown, int damageHalfHearts)
			{
				CanAttack = canAttack;
				Breakdown = breakdown;
				DamageHalfHearts = damageHalfHearts;
			}
		}

		void Start()
		{
			HideUnusedTemporaryButton();

			// VFX 카메라 초기화 (DiceBattle 동일)
			vfx?.Init(Camera.main?.transform);

			// 직접 실행 시 세션 기본값 보장
			if (GameSessionManager.PlayerHearts.TotalHalfHearts == 0)
			{
				Debug.LogWarning("[MahjongBattle] PlayerHearts가 비어 있음 — 리셋");
				GameSessionManager.PlayerHearts.Reset();
			}
			EnsureSessionBattleEnemies("MahjongBattle");
			LoadSessionEnemiesSnapshot();

			ApplyStageBackground();

			int seed = (int)(Time.realtimeSinceStartup * 1000f);
			state = new MahjongMatchState(seed);

			BuildEnemyStates(seed);
			BuildDoraIndicators();
			SetupEnemyDisplay();
			InitWaitDisplays();
			RefreshWaitDisplaysForNextTurn(rollRank3Reveal: false);
			heartDisplay?.Refresh(GameSessionManager.PlayerHearts);
			if (bottomFocus != null)
			{
				bottomFocus.Bind(battleLog);
				bottomFocus.ShowInput();
			}

			WireEnemyPanelButtons();
			LogBattleIntro();
			DrawNextTurn();
			RefreshHandUI();
			RefreshButtons();
		}

		// ── 적 상태 / UI 바인딩 ───────────────────────────────────────

		void BuildEnemyStates(int seed)
		{
			enemyStates.Clear();
			lastWaitRevealDecisions.Clear();
			currentDiscardDangerByTileIndex.Clear();
			currentTenpaiDiscardOptionsByTileIndex.Clear();
			var dora = new List<Tile>();
			foreach (var t in state.Wall.GetDoraTiles()) dora.Add(t);

			for (int i = 0; i < enemies.Count; i++)
			{
				int rank = Mathf.Clamp(enemies[i].rank, 1, 3); // 4·5는 현재 범위 밖 → 1~3로 제한
				enemyStates.Add(new EnemyMahjongState(rank, seed + i * 1000 + 7, dora));
				lastWaitRevealDecisions.Add(default);
			}
		}

		void WireEnemyPanelButtons()
		{
			for (int i = 0; i < EnemySlotCount; i++)
			{
				if (enemyPanelButtons[i] == null) continue;
				int captured = i;
				enemyPanelButtons[i].onClick.RemoveAllListeners();
				enemyPanelButtons[i].onClick.AddListener(() => OnEnemyPanelClicked(captured));
			}
		}

		public override void OnEnemyPanelClicked(int index)
		{
			if (index < 0 || index >= enemies.Count) return;
			if (!enemies[index].IsAlive) return;
			targetIndex = index;
			RefreshTargetMarkers();
		}

		// ── 도라 / 손패 / 버림 UI ─────────────────────────────────────

		void BuildDoraIndicators()
		{
			ClearChildren(doraIndicatorRoot);
			if (doraIndicatorRoot == null || tilePrefab == null) return;
			foreach (var t in state.Wall.DoraIndicators)
				SpawnTileVisual(doraIndicatorRoot, t, null);
		}

		void RefreshHandUI()
		{
			ClearChildren(handTilesRoot);
			handVisuals.Clear();
			foreach (var t in state.PlayerHand.Closed)
			{
				var v = SpawnTileVisual(handTilesRoot, t, OnTileClicked);
				handVisuals.Add(v);
			}
			ClearChildren(drawTileSlot);
			drawVisual = null;
			if (state.PlayerHand.Draw.HasValue)
			{
				drawVisual = SpawnTileVisual(drawTileSlot, state.PlayerHand.Draw.Value, OnTileClicked);
				if (drawVisual != null && drawAnimator != null)
					drawAnimator.Play(drawVisual.transform as RectTransform);
			}
			RefreshReadabilitySnapshots();
			ApplyHandReadabilityHints();
			RefreshPartialAttackPreview();
		}

		MahjongTileVisual SpawnTileVisual(Transform parent, Tile tile, System.Action<MahjongTileVisual> click)
		{
			if (parent == null || tilePrefab == null) return null;
			var go = Instantiate(tilePrefab, parent);
			var v = go.GetComponent<MahjongTileVisual>();
			if (v != null)
			{
				v.SetSpriteDatabase(tileSprites);
				v.Bind(tile, click);
			}
			return v;
		}

		void OnTileClicked(MahjongTileVisual v)
		{
			if (battleEnded || animating || v == null) return;
			DiscardTile(v.Data);
		}

		void InitWaitDisplays()
		{
			for (int i = 0; i < waitDisplays.Length; i++)
			{
				if (waitDisplays[i] == null) continue;
				bool alive = i < enemies.Count && enemies[i].IsAlive;
				waitDisplays[i].gameObject.SetActive(alive);
				if (alive) waitDisplays[i].Init(tileSprites);
			}
		}

		MahjongTileVisual AppendDiscardVisual(Tile t)
		{
			if (discardRoot == null) return null;
			return SpawnTileVisual(discardRoot, t, null);
		}

		// ── 턴 흐름 ──────────────────────────────────────────────────

		void DiscardTile(Tile tile)
		{
			if (animating || battleEnded) return;
			if (!state.DiscardFromHand(tile))
			{
				Debug.LogWarning($"[MahjongBattle] 버릴 수 없는 패: {tile}");
				return;
			}
			StartCoroutine(DiscardRoutine(tile));
		}

		IEnumerator DiscardRoutine(Tile tile)
		{
			animating = true;
			SetActionButtonsInteractable(false);
			try
			{
				var discardVisual = AppendDiscardVisual(tile);
				RefreshHandUI(); // 손패에서 버린 패를 즉시 제거 (쯔모는 아직 빈 슬롯)

				for (int i = 0; i < enemyStates.Count; i++)
				{
					if (!enemies[i].IsAlive) continue;
					var waitSnapshot = GetEnemyWaitSnapshot(i);
					var trig = enemyStates[i].OnPlayerDiscard(tile, waitSnapshot);
					if (trig == null) continue;
					yield return PlayEnemyAttackSequence(i, discardVisual, trig, counterDiscardedTile: tile);
					discardVisual = null; // 첫 트리거에만 마킹
					if (battleEnded) yield break;
					if (!GameSessionManager.IsPlayerAlive)
					{
						Defeat();
						yield break;
					}
				}

				yield return ResolveEnemyTsumoAttacks();
				if (battleEnded) yield break;
				if (!GameSessionManager.IsPlayerAlive)
				{
					Defeat();
					yield break;
				}

				RefreshWaitDisplaysForNextTurn(rollRank3Reveal: true);

				if (battleEnded) yield break;
				DrawNextTurn();
				RefreshHandUI();
				RefreshButtons();
				yield return CheckImmediateWinRoutine();
			}
			finally
			{
				animating = false;
				if (!battleEnded)
				{
					SetActionButtonsInteractable(true);
					RefreshButtons();
				}
			}
		}

		IEnumerator PlayEnemyAttackSequence(
			int i,
			MahjongTileVisual discardVisual,
			EnemyTriggerResult trig,
			string speechText = "론!",
			Tile? counterDiscardedTile = null)
		{
			if (TryResolveNeedTileCounter(counterDiscardedTile, out var counterResult))
			{
				yield return PlayNeedTileCounterFeedback();
				ApplyNeedTileCounterImpact(i, counterResult);
			}
			else
			{
				int resolvedDamage = ResolveEnemyAttackDamage(trig.DamageHalfHearts);

				// ① 대기 조합 공개 애니메이션 (Combo/Shuntsu 형태별로 다른 연출)
				if (i < waitDisplays.Length && waitDisplays[i] != null)
					yield return waitDisplays[i].RevealAnimated(trig.HitGroup);

				// ② 말풍선 "론!" / "쯔모!"
				var slotRt = i < enemyPanels.Length && enemyPanels[i] != null
					? enemyPanels[i].GetComponent<RectTransform>() : null;
				if (ronBubble != null && slotRt != null)
					yield return ronBubble.ShowRoutine(slotRt, speechText, 0.9f);
				else
					yield return new WaitForSeconds(0.9f);

				// ③ 근접 공격 (BattleAnimations.EnemyMeleeAttack 재사용)
				var bodyRt = i < enemyBodies.Length && enemyBodies[i] != null ? enemyBodies[i].rectTransform : null;
				var playerRt = playerBody != null ? playerBody.rectTransform : null;
				var def = TryGetMobDef(enemies[i].name);
				var positionPlan = EnemyAttackPositionResolver.Resolve(slotRt, bodyRt, playerRt, def);
				var rangeType = EnemyAttackPositionResolver.ResolveRangeType(def);
				bool shouldPlayDraculaLaser = DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
					GameSessionManager.IsBossBattle,
					GameSessionManager.CurrentStageId,
					enemies[i],
					ActiveStage != null ? ActiveStage.boss : null,
					ActiveStage != null && ActiveStage.boss != null ? ActiveStage.boss.enemyDiceProfileId : null);
				bool impactApplied = false;
				System.Action applyImpact = () =>
				{
					if (impactApplied)
						return;
					impactApplied = true;
					ApplyEnemyAttackImpact(i, discardVisual, trig, speechText);
				};
				if (shouldPlayDraculaLaser && bodyRt != null && playerRt != null)
				{
					yield return PlayDraculaLaserEnemyAttack(i, bodyRt, resolvedDamage, applyImpact);
				}
				else if (BattleAnimations.ShouldUseSlimeLeapSlam(enemies[i], def)
					&& battleAnims != null
					&& bodyRt != null
					&& playerRt != null)
				{
					EnemySpriteAnimator animator = enemyAnimators != null && i < enemyAnimators.Length
						? enemyAnimators[i]
						: null;
					yield return battleAnims.EnemyLeapSlamAttack(
							bodyRt,
							playerRt,
							playerBody,
							playerBodyAnimator,
							resolvedDamage,
							enemies[i].rank,
							animator,
							enemies[i].sprite,
							applyImpact,
							debugTraceMode: "Mahjong");
				}
				else if (HasUniqueAttackMotion(def) && battleAnims != null && slotRt != null && playerRt != null)
				{
					yield return PlayUniqueEnemyAttack(i, def, positionPlan, resolvedDamage, applyImpact);
				}
				else
				{
					bool isUniqueRange = rangeType == EnemyAttackRangeType.Unique;
					if (!isUniqueRange)
						PlayEnemyAttackAnimation(i);
					if (UsesProjectileAttack(enemies[i]) && battleAnims != null && bodyRt != null && playerRt != null)
					{
						yield return battleAnims.EnemyProjectileAttack(
							enemyProjectile,
							bodyRt,
							playerRt,
							playerBody,
							playerBodyAnimator,
							null,
							false,
							resolvedDamage,
							attachmentFollower: GetAttachmentFollower(i),
							enemyRank: enemies[i].rank,
							projectileStartWorld: positionPlan.projectileStartWorldPosition,
							projectileEndWorld: positionPlan.projectileEndWorldPosition,
							onImpact: applyImpact);
					}
					else if (rangeType != EnemyAttackRangeType.Ranged && battleAnims != null && slotRt != null && playerRt != null)
					{
						if (HasAttackProjectileVfx(def))
							yield return PlayEnemyAttackProjectileVfx(i, bodyRt, resolvedDamage, applyImpact);
						else if (isUniqueRange)
							yield return PlayUniqueRangeEnemyAttack(i, bodyRt, resolvedDamage, applyImpact);
						else
						{
							yield return battleAnims.EnemyMeleeAttack(
								slotRt, bodyRt, playerRt, playerBody, playerBodyAnimator, resolvedDamage,
								enemies[i].rank, positionPlan, applyImpact);
						}
					}
				}

				if (!impactApplied)
					applyImpact();
			}

			// 플레이어 사망 시 뒷정리 애니메이션 생략.
			if (!GameSessionManager.IsPlayerAlive) yield break;
			if (battleEnded) yield break;
			if (i >= enemies.Count || !enemies[i].IsAlive)
			{
				SetLastWaitRevealDecision(i, default);
				if (i < waitDisplays.Length && waitDisplays[i] != null)
					waitDisplays[i].gameObject.SetActive(false);
				RefreshReadabilitySnapshots();
				ApplyHandReadabilityHints();
				yield break;
			}

			// ⑤ 공격한 적만 Reroll. 공격하지 않은 적의 대기는 다음 공격 전까지 유지한다.
			enemyStates[i].Reroll(GetCurrentDora());
			SetLastWaitRevealDecision(i, default);
			RefreshReadabilitySnapshots();
			ApplyHandReadabilityHints();
			if (i < waitDisplays.Length && waitDisplays[i] != null)
				yield return waitDisplays[i].HoldFadeAndRefresh(enemyStates[i].Group1);
		}

		bool TryResolveNeedTileCounter(Tile? discardedTile, out MahjongNeedTileCounterResult counterResult)
		{
			counterResult = MahjongNeedTileCounterResult.None;
			if (!discardedTile.HasValue || state == null || state.PlayerHand == null)
				return false;

			counterResult = MahjongNeedTileCounterPolicy.Evaluate(
				state.PlayerHand.Closed,
				discardedTile.Value,
				state.PlayerHand.Ankans);
			return counterResult != MahjongNeedTileCounterResult.None;
		}

		IEnumerator PlayNeedTileCounterFeedback()
		{
			battleLog?.AddEntry(
				$"<color=#99FFCC><size=120%>COUNTER!</size></color>\n역공격! 필요한 패를 노린 적을 받아쳤다.",
				BattleEventPresentation.LogAndPopup);

			var playerRt = playerBody != null ? playerBody.rectTransform : null;
			if (ronBubble != null && playerRt != null)
				yield return ronBubble.ShowRoutine(playerRt, "COUNTER!", 0.55f);
		}

		void ApplyNeedTileCounterImpact(int enemyIndex, MahjongNeedTileCounterResult counterResult)
		{
			if (enemyIndex < 0 || enemyIndex >= enemies.Count)
				return;

			int damage = CounterDamageFor(counterResult);
			if (damage <= 0)
				return;

			DamageEnemyByMahjongAttack(enemyIndex, damage, MahjongPlayerAttackKind.Counter);
			vfx?.Shake(Mathf.Clamp(damage * 0.45f, 4f, 12f));
			RefreshTargetMarkers();
			CheckVictory();
		}

		static int CounterDamageFor(MahjongNeedTileCounterResult counterResult)
		{
			switch (counterResult)
			{
				case MahjongNeedTileCounterResult.Tenpai:
					return TENPAI_COUNTER_DAMAGE_HALF_HEARTS;
				case MahjongNeedTileCounterResult.Iishanten:
					return IISHANTEN_COUNTER_DAMAGE_HALF_HEARTS;
				default:
					return 0;
			}
		}

		int ResolveEnemyAttackDamage(int incomingDamage)
		{
			return MahjongDamageTable.ApplyPowerUpsToEnemyDamage(incomingDamage, GameSessionManager.PowerUps);
		}

		void ApplyEnemyAttackImpact(int enemyIndex, MahjongTileVisual discardVisual, EnemyTriggerResult trig, string speechText)
		{
			if (enemyIndex < 0 || enemyIndex >= enemies.Count || trig == null)
				return;

			int resolvedDamage = ResolveEnemyAttackDamage(trig.DamageHalfHearts);
			string attackLabel = speechText == "쯔모!" ? "쯔모" : "론";
			battleLog?.AddEntry($"적 {enemies[enemyIndex].name} {attackLabel}! {FormatCombo(trig.HitGroup)} 데미지 {resolvedDamage}.",
				BattleEventPresentation.LogAndAnimation);
			GameSessionManager.TakePlayerDamage(resolvedDamage);
			heartDisplay?.Refresh(GameSessionManager.PlayerHearts);

			if (discardVisual == null)
				return;

			if (!SameCanonicalTile(discardVisual.Data, trig.TriggeringTile))
			{
				Debug.LogWarning(
					$"[MahjongBattle] Ron marker target mismatch. discard={discardVisual.Data}, trigger={trig.TriggeringTile}");
				return;
			}

			discardVisual.MarkAsShot();
			var tip = discardVisual.DiscardTooltip;
			if (tip != null)
				tip.Init(enemies[enemyIndex].name, trig.HitGroup, resolvedDamage, waitInfoPanel);
		}

		static bool SameCanonicalTile(Tile left, Tile right)
		{
			int leftKind = TileIndex.Of(left);
			return leftKind >= 0 && leftKind == TileIndex.Of(right);
		}

		IEnumerator ResolveEnemyTsumoAttacks()
		{
			for (int i = 0; i < enemyStates.Count; i++)
			{
				if (!enemies[i].IsAlive) continue;
				var trig = enemyStates[i].TryTsumo(enemyTsumoChancePerTurn);
				if (trig == null) continue;
				yield return PlayEnemyAttackSequence(i, null, trig, "쯔모!");
				if (battleEnded) yield break;
				if (!GameSessionManager.IsPlayerAlive) yield break;
			}
		}

		void TryRevealIntuition(int enemyIndex)
		{
			if (intuitionConfig == null) return;
			if (enemyIndex < 0 || enemyIndex >= enemyStates.Count) return;
			bool forced = intuitionConfig.ConsumeForcedReveal();
			bool roll = Random.value < intuitionConfig.CurrentChance;
			if (!forced && !roll) return;

			var s = enemyStates[enemyIndex];
			var g = (Random.value < 0.5f) ? s.Group1 : s.Group2;
			int slotSide = Random.value < 0.5f ? 0 : 1;
			if (enemyIndex < waitDisplays.Length && waitDisplays[enemyIndex] != null)
				waitDisplays[enemyIndex].RevealIntuition(g, slotSide);
			battleLog?.AddEntry($"<color=#99FFCC>적 {enemies[enemyIndex].name}의 대기패를 직감했다!</color>");
		}

		void RefreshWaitDisplaysForNextTurn(bool rollRank3Reveal)
		{
			for (int i = 0; i < waitDisplays.Length; i++)
				ApplyWaitDisplayPolicy(i, rollRank3Reveal);
			RefreshReadabilitySnapshots();
			ApplyHandReadabilityHints();
		}

		void ApplyWaitDisplayPolicy(int enemyIndex, bool rollRank3Reveal)
		{
			if (enemyIndex < 0 || enemyIndex >= waitDisplays.Length) return;
			var display = waitDisplays[enemyIndex];
			if (display == null)
			{
				SetLastWaitRevealDecision(enemyIndex, default);
				return;
			}
			bool alive = enemyIndex < enemies.Count && enemies[enemyIndex].IsAlive;
			display.gameObject.SetActive(alive);
			if (!alive || enemyIndex >= enemyStates.Count)
			{
				SetLastWaitRevealDecision(enemyIndex, default);
				return;
			}

			int starRank = enemies[enemyIndex].rank;
			var previousDecision = GetLastWaitRevealDecisionOrHidden(enemyIndex);
			if (previousDecision.ShowGroup1Need || previousDecision.ShowGroup2Need)
			{
				var persistedDecision = new MahjongWaitRevealDecision(
					showGroup1Need: previousDecision.ShowGroup1Need,
					showGroup1Shape: false,
					showGroup2Need: previousDecision.ShowGroup2Need,
					showGroup2Shape: previousDecision.ShowGroup2Shape,
					newlyRevealedThisTurn: false);
				SetLastWaitRevealDecision(enemyIndex, persistedDecision);
				var persistedSnapshot = GetEnemyWaitSnapshot(enemyIndex);
				if (persistedSnapshot.TryGetPrimaryDisplayGroup(out var persistedGroup))
					display.RevealWaitTile(persistedGroup);
				return;
			}

			float rank3RevealRoll = MahjongWaitRevealPolicy.NeedsRandomRoll(starRank, alive, rollRank3Reveal)
				? Random.value
				: 1f;
			var decision = MahjongWaitRevealPolicy.Evaluate(
				starRank,
				alive,
				rollRank3Reveal,
				rank3WaitRevealChancePerTurn,
				rank3RevealRoll);
			SetLastWaitRevealDecision(enemyIndex, decision);
			var snapshot = GetEnemyWaitSnapshot(enemyIndex);
			bool hasRevealedDisplay = snapshot.TryGetPrimaryDisplayGroup(out var displayGroup);
			if (hasRevealedDisplay)
			{
				display.RevealWaitTile(displayGroup);
				if (decision.NewlyRevealedThisTurn)
					battleLog?.AddEntry($"<color=#99FFCC>적 {enemies[enemyIndex].name}의 대기패를 직감했다!</color>");
				return;
			}
			display.ShowBacked(displayGroup);
		}

		void SetLastWaitRevealDecision(int enemyIndex, MahjongWaitRevealDecision decision)
		{
			if (enemyIndex < 0 || enemyIndex >= enemyStates.Count)
				return;
			while (lastWaitRevealDecisions.Count < enemyStates.Count)
				lastWaitRevealDecisions.Add(default);
			lastWaitRevealDecisions[enemyIndex] = decision;
		}

		MahjongWaitRevealDecision GetLastWaitRevealDecisionOrHidden(int enemyIndex)
		{
			if (enemyIndex < 0 || enemyIndex >= lastWaitRevealDecisions.Count)
				return default;
			return lastWaitRevealDecisions[enemyIndex];
		}

		EnemyWaitSnapshot GetEnemyWaitSnapshot(int enemyIndex)
		{
			if (enemyIndex < 0 || enemyIndex >= enemyStates.Count)
				return EnemyWaitSnapshot.Empty;

			bool alive = enemyIndex < enemies.Count && enemies[enemyIndex].IsAlive;
			var decision = GetLastWaitRevealDecisionOrHidden(enemyIndex);
			return enemyStates[enemyIndex].CreateWaitSnapshot(decision, alive);
		}

		void RefreshReadabilitySnapshots()
		{
			RefreshDiscardDangerSnapshot();
			RefreshTenpaiDiscardSnapshot();
		}

		void RefreshDiscardDangerSnapshot()
		{
			currentDiscardDangerByTileIndex.Clear();
			dangerSourceScratch.Clear();
			if (state == null || state.PlayerHand == null)
				return;

			for (int i = 0; i < enemyStates.Count; i++)
			{
				bool alive = i < enemies.Count && enemies[i].IsAlive;
				if (!alive)
					continue;
				MahjongDangerSourceBuilder.AppendSources(
					dangerSourceScratch,
					GetEnemyWaitSnapshot(i),
					enemyStates[i].Rank);
			}

			int playerHalfHearts = GameSessionManager.PlayerHearts.TotalHalfHearts;
			var candidateKinds = new HashSet<int>();
			foreach (var tile in state.PlayerHand.Closed)
				AddDiscardDangerSnapshot(candidateKinds, tile, playerHalfHearts);
			if (state.PlayerHand.Draw.HasValue)
				AddDiscardDangerSnapshot(candidateKinds, state.PlayerHand.Draw.Value, playerHalfHearts);
		}

		void AddDiscardDangerSnapshot(HashSet<int> candidateKinds, Tile tile, int playerHalfHearts)
		{
			int tileKind = TileIndex.Of(tile);
			if (tileKind < 0 || !candidateKinds.Add(tileKind))
				return;

			currentDiscardDangerByTileIndex[tileKind] = MahjongDangerEvaluator.Evaluate(
				TileIndex.FromIndex(tileKind),
				dangerSourceScratch,
				playerHalfHearts);
		}

		void RefreshTenpaiDiscardSnapshot()
		{
			currentTenpaiDiscardOptionsByTileIndex.Clear();
			if (state == null || state.PlayerHand == null)
				return;

			var concealedPlusDraw = state.PlayerHand.ConcealedFourteen();
			int expectedCount = 14 - state.PlayerHand.Ankans.Count * 3;
			if (concealedPlusDraw.Count != expectedCount)
				return;

			var options = MahjongTenpaiPolicy.EvaluateDiscardOptions(concealedPlusDraw, state.PlayerHand.Ankans);
			for (int i = 0; i < options.Count; i++)
				currentTenpaiDiscardOptionsByTileIndex[options[i].DiscardTileKind] = options[i];
		}

		void ApplyHandReadabilityHints()
		{
			for (int i = 0; i < handVisuals.Count; i++)
				ApplyTileReadabilityHint(handVisuals[i]);
			ApplyTileReadabilityHint(drawVisual);
		}

		void ApplyTileReadabilityHint(MahjongTileVisual visual)
		{
			if (visual == null)
				return;

			int tileKind = TileIndex.Of(visual.Data);
			var lines = new List<string>(3);
			bool tenpaiAfterDiscard = false;
			if (currentTenpaiDiscardOptionsByTileIndex.TryGetValue(tileKind, out var tenpaiOption)
				&& tenpaiOption.IsTenpaiAfterDiscard)
			{
				tenpaiAfterDiscard = true;
				lines.Add($"버리면 텐파이: {FormatWaitTileKinds(tenpaiOption.AfterDiscard.WaitTileKinds)}");
			}

			bool visibleDanger = false;
			bool lethalDanger = false;
			if (currentDiscardDangerByTileIndex.TryGetValue(tileKind, out var danger))
			{
				visibleDanger = danger.VisibleHitCount > 0;
				lethalDanger = danger.Level == MahjongDangerLevel.Lethal;
				if (visibleDanger)
				{
					string label = lethalDanger ? "치명 위험" : "위험";
					lines.Add($"{label}: 론 {danger.VisibleHitCount}개 / 피해 {danger.VisibleDamageHalfHearts}");
				}
				else if (danger.HiddenSourceCount > 0)
				{
					lines.Add($"비공개 대기 {danger.HiddenSourceCount}개");
				}
			}

			if (lines.Count == 0)
			{
				visual.ClearReadabilityHint();
				return;
			}
			if (!visibleDanger && !tenpaiAfterDiscard)
			{
				visual.ClearReadabilityHint();
				visual.SetReadabilityHintText(string.Join("\n", lines), TenpaiHintBorder, false);
				return;
			}

			Color tint = Color.white;
			Color border = TenpaiHintBorder;
			bool showBorder = tenpaiAfterDiscard;
			if (visibleDanger)
			{
				tint = lethalDanger ? LethalHintTint : DangerHintTint;
				border = lethalDanger ? LethalHintBorder : DangerHintBorder;
				showBorder = true;
			}
			else if (tenpaiAfterDiscard)
			{
				tint = TenpaiHintTint;
				border = TenpaiHintBorder;
			}

			visual.SetReadabilityHint(string.Join("\n", lines), tint, border, showBorder);
		}

		void SetActionButtonsInteractable(bool on)
		{
			if (kanButton != null) kanButton.interactable = on;
			if (riichiButton != null) riichiButton.interactable = on;
			if (tempButton1 != null) tempButton1.interactable = on;
			if (tempButton2 != null) tempButton2.interactable = on;
			// 취소 버튼은 항상 사용 가능.
		}

		void HideUnusedTemporaryButton()
		{
			if (tempButton2 == null) return;
			tempButton2.interactable = false;
			tempButton2.gameObject.SetActive(false);
		}

		void RerollAllEnemies()
		{
			var dora = new List<Tile>();
			foreach (var t in state.Wall.GetDoraTiles()) dora.Add(t);
			for (int i = 0; i < enemyStates.Count; i++)
			{
				enemyStates[i].Reroll(dora);
				SetLastWaitRevealDecision(i, default);
			}
			RefreshReadabilitySnapshots();
			ApplyHandReadabilityHints();
		}

		List<Tile> GetCurrentDora()
		{
			var dora = new List<Tile>();
			foreach (var t in state.Wall.GetDoraTiles()) dora.Add(t);
			return dora;
		}

		void DrawNextTurn()
		{
			if (!state.DrawNext())
			{
				battleLog?.AddEntry("패산 소진 — 유국", BattleEventPresentation.LogAndAnimation);
				Defeat(); // 1차: 유국 = 패배 취급
				RefreshReadabilitySnapshots();
				return;
			}
			RefreshReadabilitySnapshots();
		}

		void CheckImmediateWin()
		{
			if (battleEnded || animating)
				return;

			StartCoroutine(CheckImmediateWinButtonRoutine());
		}

		IEnumerator CheckImmediateWinButtonRoutine()
		{
			animating = true;
			SetActionButtonsInteractable(false);
			try
			{
				yield return CheckImmediateWinRoutine();
			}
			finally
			{
				animating = false;
				if (!battleEnded)
				{
					SetActionButtonsInteractable(true);
					RefreshButtons();
				}
			}
		}

		IEnumerator CheckImmediateWinRoutine()
		{
			if (battleEnded) yield break;
			if (state == null || state.PlayerHand == null) yield break;
			var fourteen = state.PlayerHand.ConcealedFourteen();
			int expectedCount = 14 - state.PlayerHand.Ankans.Count * 3;
			if (fourteen.Count != expectedCount) yield break;
			var dora = new List<Tile>();
			foreach (var t in state.Wall.GetDoraTiles()) dora.Add(t);
			var winning = state.PlayerHand.Draw ?? fourteen[fourteen.Count - 1];
			var best = BestHandPicker.Pick(fourteen, state.PlayerHand.Ankans, winning, true, state.RiichiDeclared, dora);
			if (best == null || !best.Yaku.HasAnyYaku) yield break;

			int baseDamage = MahjongDamageTable.GetWinDamageHalfHearts(best.Yaku);
			int dmg = MahjongDamageTable.ScalePlayerWinDamageForBattle(baseDamage);
			dmg = MahjongDamageTable.ApplyPowerUpsToPlayerWinBattleDamage(dmg, GameSessionManager.PowerUps);
			battleLog?.AddEntry(
				$"<color=#FFD94A><size=130%>TSUMO! 화료!</size></color> {FormatWinValue(best.Yaku)} | 역: {FormatYakuSummary(best.Yaku)} | 전체 피해 {dmg}",
				BattleEventPresentation.LogAndPopup);
			yield return PlayPlayerWinAnnouncement();
			AudioManager.Play("Player_Attack_Big");
			yield return PlayPlayerAttackAnimation(() => ApplyAoeDamage(dmg, MahjongPlayerAttackKind.FullWin));
		}

		// ── 버튼 ──────────────────────────────────────────────────────

		public void OnDeclareRiichi()
		{
			RefreshRiichiAvailabilitySnapshot();
			if (!currentRiichiAvailability.CanDeclareRiichi) return;
			state.RiichiDeclared = true;
			battleLog?.AddEntry("리치 선언");
			RefreshButtons();
		}

		public void OnDeclareKan()
		{
			if (battleEnded || animating) return;
			foreach (var k in state.PlayerHand.AnkanCandidates())
			{
				if (state.PlayerHand.DeclareAnkan(k))
				{
					battleLog?.AddEntry($"안깡: {MahjongTileVisual.LabelFor(k)}");
					if (state.Wall.TryDrawRinshan(out var rinshan))
						state.PlayerHand.SetDraw(rinshan);
					RefreshHandUI();
					RefreshButtons();
					CheckImmediateWin();
					return;
				}
			}
		}

		/// <summary>중간 포기 공격 — 임시버튼1에 연결.</summary>
		public void OnPartialAttack()
		{
			if (battleEnded || animating) return;
			var preview = CalculatePartialAttackPreview();
			if (!preview.CanAttack) return;
			StartCoroutine(PartialAttackRoutine(preview));
		}

		IEnumerator PartialAttackRoutine(PartialAttackPreview preview)
		{
			var b = preview.Breakdown;
			int dmg = preview.DamageHalfHearts;
			animating = true;
			SetActionButtonsInteractable(false);
			try
			{
				battleLog?.AddEntry($"<color=#B7D7FF>중간 포기 공격</color>: {FormatPartialBreakdown(b)} | 전체 피해 {dmg}",
				BattleEventPresentation.LogAndAnimation);
				AudioManager.Play("Player_Attack_Small");
				yield return PlayPlayerAttackAnimation(() => ApplyAoeDamage(dmg, MahjongPlayerAttackKind.Partial));
				if (!battleEnded)
					ResetMatchAfterPartial();
			}
			finally
			{
				animating = false;
				if (!battleEnded)
				{
					SetActionButtonsInteractable(true);
					RefreshButtons();
				}
			}
		}

		public void CancelBattle()
		{
			if (battleEnded) return;
			battleEnded = true;
			GameSessionManager.CancelBattle();
			battleLog?.AddEntry("전투 취소", BattleEventPresentation.LogAndAnimation);
			Invoke(nameof(GotoExplore), 0.3f);
		}

		void RefreshButtons()
		{
			RefreshRiichiAvailabilitySnapshot();
			var partialPreview = RefreshPartialAttackPreview();

			if (kanButton != null)
			{
				bool any = false;
				if (state != null && state.PlayerHand != null)
					foreach (var _ in state.PlayerHand.AnkanCandidates()) { any = true; break; }
				kanButton.interactable = any;
			}
			if (riichiButton != null)
				riichiButton.interactable = currentRiichiAvailability.CanDeclareRiichi;
			if (tempButton1 != null)
				tempButton1.interactable = partialPreview.CanAttack && !battleEnded && !animating;
		}

		PartialAttackPreview RefreshPartialAttackPreview()
		{
			var preview = CalculatePartialAttackPreview();
			if (tempButton1 == null)
				return preview;

			string damageText = preview.CanAttack ? preview.DamageHalfHearts.ToString() : "-";
			SetButtonLabel(tempButton1, $"중간공격\n예상 피해 {damageText}");
			return preview;
		}

		PartialAttackPreview CalculatePartialAttackPreview()
		{
			if (state == null || state.PlayerHand == null)
				return new PartialAttackPreview(false, null, 0);

			var tiles = state.PlayerHand.ConcealedFourteen();
			int expectedCount = 14 - state.PlayerHand.Ankans.Count * 3;
			if (tiles.Count != expectedCount)
				return new PartialAttackPreview(false, null, 0);

			var breakdown = PartialHandEvaluator.Evaluate(tiles, state.PlayerHand.Ankans);
			int damage = MahjongDamageTable.GetPartialDamageHalfHearts(breakdown);
			damage = MahjongDamageTable.ApplyPowerUpsToPartialDamage(damage, GameSessionManager.PowerUps);
			return new PartialAttackPreview(true, breakdown, damage);
		}

		static void SetButtonLabel(Button button, string text)
		{
			if (button == null)
				return;
			var label = button.GetComponentInChildren<TMP_Text>();
			if (label == null)
				return;

			label.text = text;
			label.enableAutoSizing = true;
			label.fontSizeMin = 14f;
			label.fontSizeMax = 22f;
			label.textWrappingMode = TextWrappingModes.Normal;
			label.alignment = TextAlignmentOptions.Center;
		}

		void RefreshRiichiAvailabilitySnapshot()
		{
			if (state == null || state.PlayerHand == null)
			{
				currentRiichiAvailability = MahjongRiichiPolicy.Evaluate(null, false, canAct: false);
				return;
			}

			var concealedPlusDraw = state.PlayerHand.ConcealedFourteen();
			int expectedCount = 14 - state.PlayerHand.Ankans.Count * 3;
			if (concealedPlusDraw.Count != expectedCount)
			{
				currentRiichiAvailability = MahjongRiichiPolicy.Evaluate(null, state.RiichiDeclared, canAct: false);
				return;
			}

			var discardOptions = MahjongTenpaiPolicy.EvaluateDiscardOptions(concealedPlusDraw, state.PlayerHand.Ankans);
			currentRiichiAvailability = MahjongRiichiPolicy.Evaluate(
				discardOptions,
				state.RiichiDeclared,
				canAct: !battleEnded && !animating);
		}

		// ── 데미지 적용 / 승패 ─────────────────────────────────────────

		void ApplyAoeDamage(int half, MahjongPlayerAttackKind attackKind)
		{
			if (half <= 0) return;
			for (int i = 0; i < enemies.Count; i++)
			{
				if (!enemies[i].IsAlive) continue;
				DamageEnemyByMahjongAttack(i, half, attackKind);
			}
			if (vfx != null)
				vfx.Shake(attackKind == MahjongPlayerAttackKind.FullWin
					? Mathf.Clamp(half * 0.65f, 10f, 26f)
					: Mathf.Clamp(half * 0.45f, 4f, 12f));
			RefreshTargetMarkers();
			CheckVictory();
			if (!battleEnded) RefreshWaitDisplaysForNextTurn(rollRank3Reveal: false);
		}

		void DamageEnemyByMahjongAttack(int enemyIndex, int damage, MahjongPlayerAttackKind attackKind)
		{
			if (enemyIndex < 0 || enemyIndex >= enemies.Count || damage <= 0)
				return;

			bool wasAlive = enemies[enemyIndex].IsAlive;
			enemies[enemyIndex].TakeDamage(damage);
			vfx?.SpawnDamageText(enemyIndex, damage);
			PlayEnemyDamagedFeedback(enemyIndex);
			UpdateEnemyHp(enemyIndex);

			string damageColor;
			string attackLabel;
			switch (attackKind)
			{
				case MahjongPlayerAttackKind.FullWin:
					damageColor = "#FFD94A";
					attackLabel = "화료";
					break;
				case MahjongPlayerAttackKind.Counter:
					damageColor = "#99FFCC";
					attackLabel = "COUNTER!";
					break;
				default:
					damageColor = "#B7D7FF";
					attackLabel = "중간공격";
					break;
			}
			battleLog?.AddEntry($"  → {enemies[enemyIndex].name}: <color={damageColor}>{attackLabel} {damage}</color> 피해");

			if (wasAlive && !enemies[enemyIndex].IsAlive)
			{
				battleLog?.AddEntry($"  <color=#FF8888>{enemies[enemyIndex].name} 처치!</color>");
				AudioManager.Play("Enemy_Die");
			}
		}

		IEnumerator PlayPlayerAttackAnimation(System.Action onImpact = null)
		{
			bool impactApplied = false;
			System.Action applyImpact = () =>
			{
				if (impactApplied)
					return;
				impactApplied = true;
				onImpact?.Invoke();
			};

			if (attackAnimator == null)
			{
				applyImpact();
				yield break;
			}

			Coroutine attackRoutine = attackAnimator.Play(ResolvePlayerAttackVisualTarget(), applyImpact);
			if (attackRoutine != null)
				yield return attackRoutine;
			if (!impactApplied)
				applyImpact();
		}

		RectTransform ResolvePlayerAttackVisualTarget()
		{
			int visualTarget = -1;
			if (targetIndex >= 0 && targetIndex < enemies.Count && enemies[targetIndex].IsAlive)
				visualTarget = targetIndex;
			else
				visualTarget = FindFirstAliveEnemyIndex(enemies);

			if (visualTarget < 0 || visualTarget >= enemies.Count || !enemies[visualTarget].IsAlive)
				return null;
			if (enemyBodies == null || visualTarget >= enemyBodies.Length || enemyBodies[visualTarget] == null)
				return null;
			return enemyBodies[visualTarget].rectTransform;
		}

		void ResetMatchAfterPartial()
		{
			int seed = (int)(Time.realtimeSinceStartup * 1000f) ^ 0x55aa;
			state = new MahjongMatchState(seed);
			BuildDoraIndicators();
			RerollAllEnemies();
			DrawNextTurn();
			if (battleEnded)
				return;
			RefreshWaitDisplaysForNextTurn(rollRank3Reveal: false);
			RefreshHandUI();
			RefreshButtons();
		}

		void CheckVictory()
		{
			bool anyAlive = false;
			foreach (var e in enemies) if (e.IsAlive) { anyAlive = true; break; }
			if (anyAlive) return;

			battleEnded = true;
			CompleteVictorySessionState();
			battleLog?.AddEntry("승리!", BattleEventPresentation.LogAndAnimation);
			StartCoroutine(VictoryRoutine());
		}

		IEnumerator VictoryRoutine()
		{
			float waitStartedAt = Time.time;
			yield return WaitForEnemyDeathAnimations(5.2f);
			float remaining = 1.0f - (Time.time - waitStartedAt);
			if (remaining > 0f)
				yield return new WaitForSeconds(remaining);
			GotoExplore();
		}

		void CompleteVictorySessionState()
		{
			GameSessionManager.CompleteBattleWon();
		}

		void Defeat()
		{
			if (battleEnded) return;
			battleEnded = true;
			battleLog?.AddEntry("패배", BattleEventPresentation.LogAndAnimation);
			Invoke(nameof(GotoMainMenu), 1.0f);
		}

		void GotoExplore() => SceneManager.LoadScene("GameExploreScene");
		void GotoMainMenu() => SceneManager.LoadScene("MainMenu");

		IEnumerator PlayPlayerWinAnnouncement()
		{
			var playerRt = playerBody != null ? playerBody.rectTransform : null;
			if (ronBubble != null && playerRt != null)
			{
				yield return ronBubble.ShowRoutine(playerRt, "TSUMO!\n화료", 0.85f);
				yield break;
			}
			yield return new WaitForSeconds(0.25f);
		}

		static string FormatPartialBreakdown(PartialBreakdown b)
		{
			if (b == null)
				return "조합 없음";
			return $"슌쯔 {b.Shuntsu}, 커쯔 {b.Koutsu}, 깡 {b.Kantsu}, 머리 {b.Pair}";
		}

		static string FormatWinValue(YakuResult yaku)
		{
			if (yaku == null)
				return "";
			if (yaku.YakumanMultiplier > 0)
				return yaku.YakumanMultiplier > 1 ? $"{yaku.YakumanMultiplier}배 역만" : "역만";
			return $"{yaku.TotalHan}한";
		}

		static string FormatYakuSummary(YakuResult yaku)
		{
			if (yaku == null)
				return "-";

			var parts = new List<string>();
			if (yaku.Hits != null)
			{
				for (int i = 0; i < yaku.Hits.Count; i++)
				{
					var hit = yaku.Hits[i];
					parts.Add(hit.IsYakuman
						? $"{YakuLabel(hit.Id)} 역만"
						: $"{YakuLabel(hit.Id)} {hit.Han}한");
				}
			}
			if (yaku.DoraCount > 0)
				parts.Add($"도라 {yaku.DoraCount}");
			return parts.Count > 0 ? string.Join(", ", parts) : "-";
		}

		static string YakuLabel(YakuId id)
		{
			switch (id)
			{
				case YakuId.Riichi: return "리치";
				case YakuId.MenzenTsumo: return "멘젠쯔모";
				case YakuId.Pinfu: return "핑후";
				case YakuId.Tanyao: return "탕야오";
				case YakuId.Iipeikou: return "이페코";
				case YakuId.YakuhaiHaku: return "역패 백";
				case YakuId.YakuhaiHatsu: return "역패 발";
				case YakuId.YakuhaiChun: return "역패 중";
				case YakuId.SanshokuDoujun: return "삼색동순";
				case YakuId.Ittsu: return "일기통관";
				case YakuId.Chanta: return "찬타";
				case YakuId.Chiitoitsu: return "치또이츠";
				case YakuId.Toitoi: return "또이또이";
				case YakuId.SanAnkou: return "삼암각";
				case YakuId.SanshokuDoukou: return "삼색동각";
				case YakuId.SanKantsu: return "삼깡쯔";
				case YakuId.Shousangen: return "소삼원";
				case YakuId.Honroutou: return "혼노두";
				case YakuId.Ryanpeikou: return "량페코";
				case YakuId.Junchan: return "준찬타";
				case YakuId.Honitsu: return "혼일색";
				case YakuId.Chinitsu: return "청일색";
				case YakuId.Kokushi: return "국사무쌍";
				case YakuId.Suuankou: return "사암각";
				case YakuId.Daisangen: return "대삼원";
				case YakuId.Tsuuiisou: return "자일색";
				case YakuId.Ryuuiisou: return "녹일색";
				case YakuId.Chinroutou: return "청노두";
				case YakuId.Daisuushii: return "대사희";
				case YakuId.Shousuushii: return "소사희";
				case YakuId.ChuurenPoutou: return "구련보등";
				case YakuId.Suukantsu: return "사깡쯔";
				default: return id.ToString();
			}
		}

		static string FormatWaitTileKinds(IReadOnlyList<int> waitTileKinds)
		{
			if (waitTileKinds == null || waitTileKinds.Count == 0)
				return "-";

			int shown = Mathf.Min(waitTileKinds.Count, 4);
			var labels = new List<string>(shown + 1);
			for (int i = 0; i < shown; i++)
				labels.Add(MahjongTileVisual.LabelFor(TileIndex.FromIndex(waitTileKinds[i])));
			if (waitTileKinds.Count > shown)
				labels.Add($"+{waitTileKinds.Count - shown}");
			return string.Join("/", labels);
		}

		static string FormatCombo(WaitGroup g)
		{
			string Label(Tile t) => $"[{MahjongTileVisual.LabelFor(t)}]";
			switch (g.Type)
			{
				case EnemyComboType.Shuntsu:
					return $"{Label(g.Slot1)}{Label(g.NeedTile)}{Label(g.Slot2)}의 슌쯔!";
				case EnemyComboType.Koutsu:
					return $"{Label(g.NeedTile)}{Label(g.NeedTile)}{Label(g.NeedTile)}의 커쯔!";
				case EnemyComboType.Toitsu:
					return $"{Label(g.NeedTile)}{Label(g.NeedTile)}의 또이츠!";
				default:
					return "";
			}
		}

		bool UsesProjectileAttack(EnemyInfo enemy)
		{
			if (enemy == null)
				return false;
			var def = TryGetMobDef(enemy.name);
			return def != null
				&& EnemyAttackPositionResolver.ResolveRangeType(def) == EnemyAttackRangeType.Ranged
				&& !string.IsNullOrEmpty(def.projectileSpritePath);
		}

		static bool HasAttackProjectileVfx(MobDef def)
		{
			return def != null && !string.IsNullOrWhiteSpace(def.attackVfxSpritePath);
		}

		IEnumerator PlayEnemyAttackProjectileVfx(int enemyIndex, RectTransform bodyRt,
			int damageHalfHearts, System.Action onImpact = null)
		{
			EnemyInfo enemy = enemyIndex >= 0 && enemyIndex < enemies.Count ? enemies[enemyIndex] : null;
			Sprite vfxSprite = enemy != null ? ResolveEnemyAttackVfxSprite(enemy.name) : null;
			Coroutine vfxRoutine = null;
			if (attackProjectileVfx != null && vfxSprite != null)
				vfxRoutine = attackProjectileVfx.Play(vfxSprite, bodyRt);
			else if (vfxSprite == null)
				Debug.LogWarning($"[EnemyAttackProjectileVfx] {enemy?.name ?? "적"} 공격 VFX 스프라이트를 찾을 수 없습니다.");
			else
				Debug.LogWarning("[EnemyAttackProjectileVfx] attackProjectileVfx 컴포넌트가 없어 VFX를 생략합니다.");

			if (vfxRoutine != null)
				yield return vfxRoutine;

			onImpact?.Invoke();
			if (battleAnims != null && playerBody != null)
			{
				Coroutine hitRoutine = playerBodyAnimator != null
					? playerBodyAnimator.PlayHitByEnemyRank(enemy != null ? enemy.rank : 0, damageHalfHearts)
					: null;
				battleAnims.FlashDamage(playerBody);
				if (hitRoutine != null)
					yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
			}
		}

		IEnumerator PlayDraculaLaserEnemyAttack(int enemyIndex, RectTransform bodyRt,
			int damageHalfHearts, System.Action onImpact = null)
		{
			EnemyInfo enemy = enemyIndex >= 0 && enemyIndex < enemies.Count ? enemies[enemyIndex] : null;
			Coroutine feedbackRoutine = null;
			bool impactResolved = false;
			System.Action resolveImpact = () =>
			{
				if (impactResolved)
					return;
				impactResolved = true;
				onImpact?.Invoke();
				if (battleAnims != null && playerBody != null)
				{
					feedbackRoutine = playerBodyAnimator != null
						? playerBodyAnimator.PlayHitByEnemyRank(enemy != null ? enemy.rank : 0, damageHalfHearts)
						: null;
					battleAnims.FlashDamage(playerBody);
				}
			};

			Coroutine laserRoutine = null;
			if (attackProjectileVfx != null && playerBody != null)
				laserRoutine = attackProjectileVfx.PlayDraculaLaser(bodyRt, playerBody.rectTransform, resolveImpact);
			else
				Debug.LogWarning("[DraculaLaserAttackVfx] attackProjectileVfx 또는 playerBody가 없어 레이저 VFX를 생략합니다.");

			if (laserRoutine != null)
				yield return laserRoutine;
			else
				resolveImpact();

			if (feedbackRoutine != null)
				yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
		}

		IEnumerator PlayUniqueEnemyAttack(int enemyIndex, MobDef def,
			EnemyAttackPositionPlan positionPlan, int damageHalfHearts, System.Action onImpact = null)
		{
			var slotRt = enemyIndex < enemyPanels.Length && enemyPanels[enemyIndex] != null
				? enemyPanels[enemyIndex].GetComponent<RectTransform>()
				: null;
			if (battleAnims == null || slotRt == null)
				yield break;

			yield return battleAnims.WalkTo(
				slotRt,
				positionPlan.standWorldPosition,
				def != null && def.attackApproachDuration > 0f ? def.attackApproachDuration : 0.4f);

			EnemySpriteAnimator animator = enemyAnimators != null && enemyIndex < enemyAnimators.Length
				? enemyAnimators[enemyIndex]
				: null;
			Coroutine attackRoutine = animator != null ? animator.PlayAttack() : null;
			if (attackRoutine != null)
				yield return new WaitWhile(() => animator != null && animator.IsActionPlaying);

			onImpact?.Invoke();
			if (battleAnims != null && playerBody != null)
			{
				Coroutine hitRoutine = playerBodyAnimator != null
					? playerBodyAnimator.PlayHitByEnemyRank(enemies[enemyIndex].rank, damageHalfHearts)
					: null;
				battleAnims.FlashDamage(playerBody);
				if (hitRoutine != null)
					yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
			}

			if (animator != null)
				animator.ReturnToIdle(enemies[enemyIndex].sprite);

			yield return battleAnims.WalkBack(
				slotRt,
				positionPlan.homeLocalPosition,
				def != null && def.attackRetreatDuration > 0f ? def.attackRetreatDuration : 0.5f);
		}

		IEnumerator PlayUniqueRangeEnemyAttack(int enemyIndex, RectTransform bodyRt, int damageHalfHearts, System.Action onImpact = null)
		{
			Coroutine attackRoutine = PlayEnemyAttackAnimation(enemyIndex);
			if (battleAnims != null && bodyRt != null)
				yield return battleAnims.JumpInPlace(bodyRt, 34f, 0.36f);
			if (attackRoutine != null)
				yield return new WaitWhile(() =>
					enemyAnimators != null
					&& enemyIndex >= 0
					&& enemyIndex < enemyAnimators.Length
					&& enemyAnimators[enemyIndex] != null
					&& enemyAnimators[enemyIndex].IsActionPlaying);

			onImpact?.Invoke();
			if (battleAnims != null && playerBody != null)
			{
				Coroutine hitRoutine = playerBodyAnimator != null
					? playerBodyAnimator.PlayHitByEnemyRank(enemies[enemyIndex].rank, damageHalfHearts)
					: null;
				battleAnims.FlashDamage(playerBody);
				if (hitRoutine != null)
					yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
			}
		}

		static bool HasUniqueAttackMotion(MobDef def)
		{
			return def != null && !string.IsNullOrWhiteSpace(def.uniqueAttackProfileId);
		}

		EnemyProjectileAttachmentFollower GetAttachmentFollower(int enemyIndex)
		{
			if (enemyIdleProjectiles == null || enemyIndex < 0 || enemyIndex >= enemyIdleProjectiles.Length)
				return null;
			var projectile = enemyIdleProjectiles[enemyIndex];
			return projectile != null ? projectile.GetComponent<EnemyProjectileAttachmentFollower>() : null;
		}

		// ── 디버그 훅 (IBattleDebugTarget) ─────────────────────────────

		public string DebugKillPlayer()
		{
			GameSessionManager.PlayerHearts.TakeDamage(GameSessionManager.PlayerHearts.TotalHalfHearts);
			heartDisplay?.Refresh(GameSessionManager.PlayerHearts);
			Defeat();
			return "[Debug] 플레이어 즉사";
		}

		public string DebugKillAllEnemies()
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				enemies[i].hp = 0;
				UpdateEnemyHp(i);
				if (deadOverlays[i] != null)
					deadOverlays[i].gameObject.SetActive(ShouldShowDeadOverlay(i, enemies[i]));
			}
			RefreshTargetMarkers();
			CheckVictory();
			return "[Debug] 모든 적 즉사";
		}

		public string DebugKillEnemies(int[] indices)
		{
			if (indices == null || indices.Length == 0) return "[Debug] 인덱스 없음";
			var killed = new List<int>();
			foreach (var idx in indices)
			{
				if (idx < 0 || idx >= enemies.Count) continue;
				enemies[idx].hp = 0;
				UpdateEnemyHp(idx);
				if (deadOverlays[idx] != null)
					deadOverlays[idx].gameObject.SetActive(ShouldShowDeadOverlay(idx, enemies[idx]));
				killed.Add(idx);
			}
			RefreshTargetMarkers();
			CheckVictory();
			return $"[Debug] 적 즉사: [{string.Join(",", killed)}]";
		}

		public string DebugPlaySprite(string target, int objectIndex, string spriteKind, float loopSeconds)
		{
			return DebugPlayBattleSprite(target, objectIndex, spriteKind, playerBodyAnimator, loopSeconds);
		}

		static void ClearChildren(Transform t)
		{
			if (t == null) return;
			for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
		}
	}
}
