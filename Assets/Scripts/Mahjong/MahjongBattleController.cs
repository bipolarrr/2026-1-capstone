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
	/// 버림 즉시 적 OnPlayerDiscard 통지 → 발동 시 데미지. 매 턴 후 적 Reroll.
	/// DiceBattle 패턴 미러: 기본 적 세팅, 고정 4슬롯 적 UI, 스테이지 배경, 디버그 훅.
	/// </summary>
	public class MahjongBattleController : BattleControllerBase, IBattleDebugTarget
	{
		const int EnemySlotCount = 4;

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
		[SerializeField] PlayerBodyAnimator playerBodyAnimator;
		[SerializeField] BattleBottomFocusController bottomFocus;

		MahjongMatchState state;
		readonly List<EnemyMahjongState> enemyStates = new List<EnemyMahjongState>();
		readonly List<MahjongTileVisual> handVisuals = new List<MahjongTileVisual>();
		MahjongTileVisual drawVisual;

		bool battleEnded;
		bool animating;

		void Start()
		{
			// VFX 카메라 초기화 (DiceBattle 동일)
			vfx?.Init(Camera.main?.transform);

			// 직접 실행 시 세션 기본값 보장
			if (GameSessionManager.PlayerHearts.TotalHalfHearts == 0)
			{
				Debug.LogWarning("[MahjongBattle] PlayerHearts가 비어 있음 — 리셋");
				GameSessionManager.PlayerHearts.Reset();
			}
			if (GameSessionManager.BattleEnemies.Count == 0)
			{
				Debug.LogWarning("[MahjongBattle] BattleEnemies 비어 있음 — 기본 적 생성");
				GenerateDefaultEnemies();
			}

			// 딥카피 (취소 시 원본 보호)
			enemies.Clear();
			foreach (var e in GameSessionManager.BattleEnemies)
				enemies.Add(e.Clone());

			targetIndex = 0;
			for (int i = 0; i < enemies.Count; i++)
				if (enemies[i].IsAlive) { targetIndex = i; break; }

			ApplyStageBackground();

			int seed = (int)(Time.realtimeSinceStartup * 1000f);
			state = new MahjongMatchState(seed);

			BuildEnemyStates(seed);
			BuildDoraIndicators();
			SetupEnemyDisplay();
			InitWaitDisplays();
			heartDisplay?.Refresh(GameSessionManager.PlayerHearts);
			RefreshHandUI();
			if (bottomFocus != null)
			{
				bottomFocus.Bind(battleLog);
				bottomFocus.ShowInput();
			}

			WireEnemyPanelButtons();
			LogIntro();
			DrawNextTurn();
			RefreshButtons();
		}

		// ── 적 상태 / UI 바인딩 ───────────────────────────────────────

		void BuildEnemyStates(int seed)
		{
			enemyStates.Clear();
			var dora = new List<Tile>();
			foreach (var t in state.Wall.GetDoraTiles()) dora.Add(t);

			for (int i = 0; i < enemies.Count; i++)
			{
				int rank = Mathf.Clamp(enemies[i].rank, 1, 3); // 4·5는 현재 범위 밖 → 1~3로 제한
				enemyStates.Add(new EnemyMahjongState(rank, seed + i * 1000 + 7, dora));
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
			battleLog?.AddEntry($"타겟 변경: {enemies[index].name}");
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
				battleLog?.AddEntry($"버림: {MahjongTileVisual.LabelFor(tile)}");
				RefreshHandUI(); // 손패에서 버린 패를 즉시 제거 (쯔모는 아직 빈 슬롯)

				var triggered = new HashSet<int>();
				for (int i = 0; i < enemyStates.Count; i++)
				{
					if (!enemies[i].IsAlive) continue;
					var trig = enemyStates[i].OnPlayerDiscard(tile);
					if (trig == null) continue;
					triggered.Add(i);
					yield return PlayEnemyAttackSequence(i, discardVisual, trig);
					discardVisual = null; // 첫 트리거에만 마킹
					if (battleEnded) yield break;
					if (!GameSessionManager.IsPlayerAlive)
					{
						Defeat();
						yield break;
					}
				}

				// 매 턴 후 Reroll — 공격한 적은 시퀀스 내에서 이미 리롤됨, 나머지만 리롤.
				RerollEnemiesExcept(triggered);

				// 공격하지 않은 적에 대해 직감 체크
				for (int i = 0; i < enemyStates.Count; i++)
				{
					if (!enemies[i].IsAlive || triggered.Contains(i)) continue;
					TryRevealIntuition(i);
				}

				if (battleEnded) yield break;
				DrawNextTurn();
				RefreshHandUI();
				RefreshButtons();
				CheckImmediateWin();
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

		IEnumerator PlayEnemyAttackSequence(int i, MahjongTileVisual discardVisual, EnemyTriggerResult trig)
		{
			// ① 대기 조합 공개 애니메이션 (Combo/Shuntsu 형태별로 다른 연출)
			if (i < waitDisplays.Length && waitDisplays[i] != null)
				yield return waitDisplays[i].RevealAnimated(trig.HitGroup);

			// ② 말풍선 "론!"
			var slotRt = i < enemyPanels.Length && enemyPanels[i] != null
				? enemyPanels[i].GetComponent<RectTransform>() : null;
			if (ronBubble != null && slotRt != null)
				yield return ronBubble.ShowRoutine(slotRt, "론!", 0.9f);
			else
				yield return new WaitForSeconds(0.9f);

			// ③ 근접 공격 (BattleAnimations.EnemyMeleeAttack 재사용)
			var bodyRt = i < enemyBodies.Length && enemyBodies[i] != null ? enemyBodies[i].rectTransform : null;
			var playerRt = playerBody != null ? playerBody.rectTransform : null;
			PlayEnemyAttackAnimation(i);
			if (battleAnims != null && slotRt != null && playerRt != null)
				yield return battleAnims.EnemyMeleeAttack(
					slotRt, bodyRt, playerRt, playerBody, playerBodyAnimator, trig.DamageHalfHearts);

			// ④ 피해 적용 + 버림패에 해골/툴팁 마킹
			battleLog?.AddEntry($"적 {enemies[i].name} 발동! {FormatCombo(trig.HitGroup)} 데미지 {trig.DamageHalfHearts}.");
			GameSessionManager.TakePlayerDamage(trig.DamageHalfHearts);
			heartDisplay?.Refresh(GameSessionManager.PlayerHearts);

			if (discardVisual != null)
			{
				discardVisual.MarkAsShot();
				var tip = discardVisual.DiscardTooltip;
				if (tip != null)
					tip.Init(enemies[i].name, trig.HitGroup, trig.DamageHalfHearts, waitInfoPanel);
			}

			// 플레이어 사망 시 뒷정리 애니메이션 생략.
			if (!GameSessionManager.IsPlayerAlive) yield break;

			// ⑤ 이 적만 먼저 Reroll → 새 조합 기준으로 HoldFadeAndRefresh. 바깥의 RerollEnemiesExcept가 이 적을 건너뜀.
			enemyStates[i].Reroll(GetCurrentDora());
			if (i < waitDisplays.Length && waitDisplays[i] != null)
				yield return waitDisplays[i].HoldFadeAndRefresh(enemyStates[i].Group1);
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

		void SetActionButtonsInteractable(bool on)
		{
			if (kanButton != null) kanButton.interactable = on;
			if (riichiButton != null) riichiButton.interactable = on;
			if (tempButton1 != null) tempButton1.interactable = on;
			if (tempButton2 != null) tempButton2.interactable = on;
			// 취소 버튼은 항상 사용 가능.
		}

		void RerollAllEnemies()
		{
			var dora = new List<Tile>();
			foreach (var t in state.Wall.GetDoraTiles()) dora.Add(t);
			foreach (var e in enemyStates) e.Reroll(dora);
		}

		void RerollEnemiesExcept(HashSet<int> skip)
		{
			var dora = new List<Tile>();
			foreach (var t in state.Wall.GetDoraTiles()) dora.Add(t);
			for (int i = 0; i < enemyStates.Count; i++)
			{
				if (skip != null && skip.Contains(i)) continue;
				enemyStates[i].Reroll(dora);
			}
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
				battleLog?.AddEntry("패산 소진 — 유국");
				Defeat(); // 1차: 유국 = 패배 취급
			}
		}

		void CheckImmediateWin()
		{
			if (battleEnded) return;
			var fourteen = state.PlayerHand.ConcealedFourteen();
			if (fourteen.Count != 14) return;
			var dora = new List<Tile>();
			foreach (var t in state.Wall.GetDoraTiles()) dora.Add(t);
			var winning = state.PlayerHand.Draw ?? fourteen[fourteen.Count - 1];
			var best = BestHandPicker.Pick(fourteen, state.PlayerHand.Ankans, winning, true, state.RiichiDeclared, dora);
			if (best == null || !best.Yaku.HasAnyYaku) return;

			int dmg = MahjongDamageTable.GetWinDamageHalfHearts(best.Yaku);
			battleLog?.AddEntry($"화료! 한수={best.Yaku.TotalHan} 데미지={dmg} (AOE)");
			ApplyAoeDamage(dmg);
		}

		// ── 버튼 ──────────────────────────────────────────────────────

		public void OnDeclareRiichi()
		{
			if (battleEnded || state.RiichiDeclared) return;
			state.RiichiDeclared = true;
			battleLog?.AddEntry("리치 선언");
			RefreshButtons();
		}

		public void OnDeclareKan()
		{
			if (battleEnded) return;
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
			if (battleEnded) return;
			var tiles = state.PlayerHand.ConcealedFourteen();
			var b = PartialHandEvaluator.Evaluate(tiles, state.PlayerHand.Ankans);
			int dmg = MahjongDamageTable.GetPartialDamageHalfHearts(b);
			battleLog?.AddEntry($"중간 포기 공격: 멘츠={b.TotalMelds} 머리={b.Pair} 데미지={dmg}");
			ApplyAoeDamage(dmg);
			ResetMatchAfterPartial();
		}

		public void OnTempButton2() { }

		public void CancelBattle()
		{
			if (battleEnded) return;
			battleEnded = true;
			GameSessionManager.LastBattleResult = BattleResult.Cancelled;
			battleLog?.AddEntry("전투 취소");
			Invoke(nameof(GotoExplore), 0.3f);
		}

		void RefreshButtons()
		{
			if (kanButton != null)
			{
				bool any = false;
				foreach (var _ in state.PlayerHand.AnkanCandidates()) { any = true; break; }
				kanButton.interactable = any;
			}
			if (riichiButton != null)
				riichiButton.interactable = !state.RiichiDeclared;
		}

		// ── 데미지 적용 / 승패 ─────────────────────────────────────────

		void ApplyAoeDamage(int half)
		{
			if (half <= 0) return;
			for (int i = 0; i < enemies.Count; i++)
			{
				if (!enemies[i].IsAlive) continue;
				enemies[i].hp = Mathf.Max(0, enemies[i].hp - half);
				PlayEnemyHitAnimation(i);
				if (battleAnims != null && i < enemyBodies.Length && !EnemyHasHitAnimation(i))
					battleAnims.FlashHit(enemyBodies[i]);
				UpdateEnemyHp(i);
				if (!enemies[i].IsAlive && deadOverlays[i] != null)
					deadOverlays[i].gameObject.SetActive(true);
			}
			RefreshTargetMarkers();
			CheckVictory();
		}

		void ResetMatchAfterPartial()
		{
			int seed = (int)(Time.realtimeSinceStartup * 1000f) ^ 0x55aa;
			state = new MahjongMatchState(seed);
			BuildDoraIndicators();
			RerollAllEnemies();
			RefreshHandUI();
			RefreshButtons();
		}

		void CheckVictory()
		{
			bool anyAlive = false;
			foreach (var e in enemies) if (e.IsAlive) { anyAlive = true; break; }
			if (anyAlive) return;

			battleEnded = true;
			GameSessionManager.LastBattleResult = BattleResult.Won;
			battleLog?.AddEntry("승리!");
			Invoke(nameof(GotoExplore), 1.0f);
		}

		void Defeat()
		{
			if (battleEnded) return;
			battleEnded = true;
			battleLog?.AddEntry("패배");
			Invoke(nameof(GotoMainMenu), 1.0f);
		}

		void GotoExplore() => SceneManager.LoadScene("GameExploreScene");
		void GotoMainMenu() => SceneManager.LoadScene("MainMenu");

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

		void LogIntro()
		{
			if (battleLog == null) return;
			battleLog.Clear();
			if (GameSessionManager.IsBossBattle)
				battleLog.AddEntry("<color=#FF5555>— 보스 전투 개시! (마작) —</color>");
			else
				battleLog.AddEntry("— 마작 전투 개시 —");
			foreach (var e in enemies)
				battleLog.AddEntry($"  {e.name} <color=#FFD94A>{e.RankStars}</color> <color=#AAAAAA>(HP {e.maxHp})</color>");
			battleLog.AddEntry($"도라 인디케이터 {state.Wall.DoraIndicators.Count}장 공개");
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
				if (deadOverlays[i] != null) deadOverlays[i].gameObject.SetActive(true);
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
				if (deadOverlays[idx] != null) deadOverlays[idx].gameObject.SetActive(true);
				killed.Add(idx);
			}
			RefreshTargetMarkers();
			CheckVictory();
			return $"[Debug] 적 즉사: [{string.Join(",", killed)}]";
		}

		static void ClearChildren(Transform t)
		{
			if (t == null) return;
			for (int i = t.childCount - 1; i >= 0; i--) UnityEngine.Object.Destroy(t.GetChild(i).gameObject);
		}
	}
}
