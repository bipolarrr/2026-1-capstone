using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class BattleSceneController : BattleControllerBase, IBattleDebugTarget
{
	[SerializeField] DiceRollDirector diceDirector;

	[SerializeField] Button rollButton;
	[SerializeField] Button confirmButton;
	[SerializeField] Button cancelButton;
	[SerializeField] Button nextRoundButton;

	[SerializeField] PlayerDeathAnimator deathAnimator;
	[SerializeField] PlayerRollAnimator rollAnimator;
	[SerializeField] PlayerAttackAnimator attackAnimator;
	[SerializeField] PlayerBodyAnimator playerBodyAnimator;

	// 분해된 협력 컴포넌트 — DiceBattleSceneBuilder가 AddComponent + SetField로 주입.
	[SerializeField] EnemyCounterAttackDirector counterAttackDirector;
	[SerializeField] BattleHudPresenter hud;
	[SerializeField] BattleBottomFocusController bottomFocus;

	bool roundConfirmed;

	void Start()
	{
		if (GameSessionManager.PlayerHearts.TotalHalfHearts == 0)
		{
			Debug.LogWarning("[Battle] PlayerHearts가 비어 있음 — 5하트(10반칸)로 초기화");
			GameSessionManager.PlayerHearts.Reset();
		}
		if (GameSessionManager.BattleEnemies.Count == 0)
		{
			Debug.LogWarning("[Battle] BattleEnemies가 비어 있음 — 기본 적 생성");
			GenerateDefaultEnemies();
		}

		// 원본 보존: 전투 취소 시 GameSessionManager의 적 데이터를 오염시키지 않기 위해 딥카피
		enemies = new List<EnemyInfo>();
		foreach (var e in GameSessionManager.BattleEnemies)
			enemies.Add(e.Clone());
		targetIndex = 0;
		for (int i = 0; i < enemies.Count; i++)
		{
			if (enemies[i].IsAlive)
			{
				targetIndex = i;
				break;
			}
		}

		if (enemies.Count == 0)
			Debug.LogError("[Battle] Start: BattleEnemies가 비어 있음");
		else
			Debug.Log($"[Battle] Start enemies={enemies.Count} target={targetIndex} boss={GameSessionManager.IsBossBattle} hearts={GameSessionManager.PlayerHearts.TotalHalfHearts}");

		var mainCamTransform = Camera.main?.transform;
		if (mainCamTransform == null)
			Debug.LogWarning("[Battle] MainCamera를 찾을 수 없음 — 화면 흔들림 비활성화");
		vfx.Init(mainCamTransform);

		ApplyStageBackground();
		SetupEnemyDisplay();
		UpdatePlayerHUD();
		if (bottomFocus != null)
		{
			bottomFocus.Bind(battleLog);
			bottomFocus.ShowInput();
		}

		// 적 반격 디렉터에 공유 상태 주입 — 빌더가 SerializeField는 이미 바인딩했고,
		// 런타임 협업에 필요한 참조만 여기서 전달한다.
		if (counterAttackDirector != null)
		{
			counterAttackDirector.Bind(new BattleControllerContext
			{
				enemies          = enemies,
				enemyPanels      = enemyPanels,
				enemyBodies      = enemyBodies,
				enemyAnimators   = enemyAnimators,
				playerBody       = playerBody,
				playerBodyAnimator = playerBodyAnimator,
				battleLog        = battleLog,
				battleAnims      = battleAnims,
				diceDirector     = diceDirector,
				rollButton       = rollButton,
				confirmButton    = confirmButton,
				nextRoundButton  = nextRoundButton,
				hud              = hud,
				updatePlayerHud  = UpdatePlayerHUD,
				setRoundConfirmed = v => roundConfirmed = v,
				startPlayerDefeatedRoutine = () => StartCoroutine(PlayerDefeatedRoutine())
			});
			counterAttackDirector.ResetOnNewTurn();
		}
		else
		{
			Debug.LogWarning("[Battle] counterAttackDirector 미할당 — 빌더가 SetField 못했을 수 있음");
		}

		if (diceDirector != null)
		{
			diceDirector.OnRollStarted    += HandleRollStarted;
			diceDirector.OnRollSettled    += HandleRollSettled;
			diceDirector.OnHoldChanged    += HandleHoldChanged;
			diceDirector.OnComeOutStarted += HandleComeOutStarted;
			diceDirector.BeginTurn(3);
			diceDirector.SetHoldInteractionEnabled(false); // 첫 롤 전에는 홀드 금지
		}

		roundConfirmed = false;

		cancelButton.gameObject.SetActive(true);
		confirmButton.gameObject.SetActive(false);
		nextRoundButton.gameObject.SetActive(false);
		if (hud != null)
		{
			hud.RefreshRollDots(diceDirector != null ? diceDirector.MaxRolls : 3,
			                    diceDirector != null ? diceDirector.RollsRemaining : 0);
			hud.ClearDamageText();
		}

		// 전투 개시 로그
		if (battleLog != null)
		{
			battleLog.Clear();
			if (GameSessionManager.IsBossBattle)
				battleLog.AddEntry("<color=#FF5555>— 보스 전투 개시! —</color>");
			else
				battleLog.AddEntry("— 전투 개시! —");
			foreach (var e in enemies)
				battleLog.AddEntry($"  {e.name} <color=#FFD94A>{e.RankStars}</color> <color=#AAAAAA>(HP {e.maxHp})</color>");
		}
	}

	void OnDestroy()
	{
		if (hud != null) hud.StopComboLabel();
		if (diceDirector != null)
		{
			diceDirector.OnRollStarted    -= HandleRollStarted;
			diceDirector.OnRollSettled    -= HandleRollSettled;
			diceDirector.OnHoldChanged    -= HandleHoldChanged;
			diceDirector.OnComeOutStarted -= HandleComeOutStarted;
		}
	}

	// ── 이벤트 허브(DiceRollDirector → BSC) ─────────────────────

	void HandleComeOutStarted(DiceStopProfile profile, int[] plan, bool[] heldMask)
	{
		string targetName = ComboProximity.GetComboName(profile.targetRank);
		if (string.IsNullOrEmpty(targetName)) return;

		if (profile.scenario == DiceStopCase.AlreadyCombo)
		{
			var (expectedDmg, _, _, _) =
				DamageCalculator.Calculate(plan, GameSessionManager.PowerUps);
			if (hud != null) hud.ShowComboLabel($"{targetName} ({expectedDmg})");
			AudioManager.Play("DIce_WakuWaku_Level3");
		}

		var faces = ComboProximity.GetDecisiveTargetFaces(plan, heldMask, profile);
		var combos = ComboProximity.GetAllReachableCombos(plan, heldMask, profile);

		var visibleStable = new List<int>();
		if (profile.stableMask != null && plan != null)
		{
			for (int i = 0; i < plan.Length && i < profile.stableMask.Length; i++)
			{
				bool isHeld = heldMask != null && i < heldMask.Length && heldMask[i];
				if (isHeld) continue;
				if (profile.stableMask[i]) visibleStable.Add(plan[i]);
			}
		}
		string currentName = ComboProximity.GetComboName(profile.plannedRank);
		string altDebug = "";
		if (combos.Count > 0)
		{
			var parts = new List<string>();
			foreach (var (r, fs) in combos)
				parts.Add($"{ComboProximity.GetComboName(r)}:[{string.Join(",", fs)}]");
			altDebug = $" | altCombos={{{string.Join(" ", parts)}}}";
		}
		Debug.Log($"[ComeOut] {(string.IsNullOrEmpty(currentName) ? "None" : currentName)} → {targetName} | visibleStable=[{string.Join(",", visibleStable)}] | faces=[{string.Join(",", faces)}]{altDebug}");

		if (battleLog == null) return;

		if (profile.scenario == DiceStopCase.OneAway && combos.Count >= 2)
		{
			var clauses = new List<string>();
			foreach (var (r, fs) in combos)
			{
				string name = ComboProximity.GetComboName(r);
				if (string.IsNullOrEmpty(name) || fs.Count == 0) continue;
				string fc = fs.Count == 1
					? $"[{fs[0]}]{ObjectParticle(fs[0])}"
					: $"[{string.Join(", ", fs)}] 중 하나를";
				clauses.Add($"{fc} 찾아 {name}를");
			}
			battleLog.AddEntry($"<color=#FFD94A>{string.Join(", ", clauses)} 완성시키자!</color>");
			return;
		}

		string faceClause;
		if (faces.Count == 0)
			faceClause = "필요한 눈을";
		else if (faces.Count == 1)
			faceClause = $"[{faces[0]}]{ObjectParticle(faces[0])}";
		else
			faceClause = $"[{string.Join(", ", faces)}] 중 하나를";

		battleLog.AddEntry($"<color=#FFD94A>{faceClause} 찾아 {targetName}를 완성시키자!</color>");
	}

	/// <summary>한국어 목적격 조사(을/를)를 주사위 눈 숫자 종성 여부로 선택.</summary>
	static string ObjectParticle(int face)
	{
		switch (face)
		{
			case 1:
			case 3:
			case 6:
				return "을";
			default:
				return "를";
		}
	}

	void HandleRollStarted()
	{
		if (hud != null) hud.StopComboLabel();
		cancelButton.gameObject.SetActive(false);
		confirmButton.gameObject.SetActive(false);
		if (rollAnimator != null) rollAnimator.Play();
		UpdateRollsText();
	}

	void HandleHoldChanged()
	{
		UpdateDamagePreview();
	}

	void HandleRollSettled()
	{
		UpdateRollsText();

		// 방어 페이즈 자동 확정 — 디렉터가 상태 소유
		if (counterAttackDirector != null && counterAttackDirector.IsDefensePhase)
		{
			if (counterAttackDirector.TryAutoConfirmDefenseOnRollSettled())
				return;
		}

		if (!roundConfirmed)
		{
			confirmButton.gameObject.SetActive(true);
			diceDirector.SetHoldInteractionEnabled(diceDirector.RollsRemaining > 0);
		}

		UpdateDamagePreview();
	}

	// ── 적 타겟 선택 ────────────────────────────────────────────

	public override void OnEnemyPanelClicked(int index)
	{
		if (roundConfirmed) return;
		if (index < 0 || index >= enemies.Count) return;
		if (!enemies[index].IsAlive) return;

		Debug.Log($"[Battle] OnEnemyPanelClicked → 타겟 변경: idx={index} \"{enemies[index].name}\"");
		targetIndex = index;
		RefreshTargetMarkers();
		UpdateDamagePreview();
	}

	// ── 버튼 콜백(빌더 PersistentListener 대상) ─────────────────

	public void RollDice()
	{
		if (roundConfirmed) return;
		if (diceDirector != null) diceDirector.OnRollButtonPressed();
	}

	public void OnComeOutClicked() { RollDice(); }

	int[] ReadDiceValues()
	{
		return diceDirector != null ? diceDirector.ReadFinalValues() : new int[5];
	}

	public void ConfirmScore()
	{
		if (counterAttackDirector != null && counterAttackDirector.IsDefensePhase)
		{
			counterAttackDirector.ConfirmDefense();
			return;
		}

		if (roundConfirmed)
			return;

		StartCoroutine(ConfirmScoreRoutine());
	}

	IEnumerator ConfirmScoreRoutine()
	{
		roundConfirmed = true;

		int[] values = ReadDiceValues();
		var attack = PlayerAttackPipeline.Resolve(values, GameSessionManager.PowerUps);
		int damage = attack.damage;
		int splash = attack.splashDamage;
		string comboName = attack.comboName;
		Debug.Log($"[Battle] ConfirmScore combo=\"{comboName}\" damage={damage} splash={splash} target={targetIndex}");

		AudioManager.Play(PlayerAttackPipeline.GetPlayerAttackClipName(comboName));

		confirmButton.gameObject.SetActive(false);
		rollButton.interactable = false;

		if (attackAnimator != null)
		{
			RectTransform targetBody = targetIndex >= 0 && targetIndex < enemyBodies.Length && enemyBodies[targetIndex] != null
				? enemyBodies[targetIndex].rectTransform
				: null;
			Coroutine attackAnim = attackAnimator.Play(targetBody);
			if (attackAnim != null)
				yield return attackAnim;
		}

		if (battleLog != null)
		{
			string diceStr = $"[{string.Join(", ", values)}]";
			if (!string.IsNullOrEmpty(comboName))
				battleLog.AddEntry($"<color=#FFD94A>도박사의 {comboName}!</color> {diceStr}");
			else
				battleLog.AddEntry($"도박사의 공격! {diceStr}");
		}

		// 대상에게 100%
		bool targetWasAlive = enemies[targetIndex].IsAlive;
		enemies[targetIndex].TakeDamage(damage);
		vfx.SpawnDamageText(targetIndex, damage);
		PlayEnemyHitAnimation(targetIndex);
		if (battleAnims != null && targetIndex < enemyBodies.Length && !EnemyHasHitAnimation(targetIndex))
		{
			battleAnims.FlashHit(enemyBodies[targetIndex]);
		}

		if (battleLog != null)
		{
			battleLog.AddEntry($"  → {enemies[targetIndex].name}에게 <color=#FFD94A>{damage}</color> 데미지!");
			if (targetWasAlive && !enemies[targetIndex].IsAlive)
				battleLog.AddEntry($"  <color=#FF8888>{enemies[targetIndex].name} 처치!</color>");
		}
		if (targetWasAlive && !enemies[targetIndex].IsAlive)
			AudioManager.Play("Enemy_Die");

		if (splash > 0)
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				if (i != targetIndex && enemies[i].IsAlive)
				{
					bool wasAlive = enemies[i].IsAlive;
					enemies[i].TakeDamage(splash);
					vfx.SpawnDamageText(i, splash);
					PlayEnemyHitAnimation(i);
					if (battleAnims != null && i < enemyBodies.Length && !EnemyHasHitAnimation(i))
					{
						battleAnims.FlashHit(enemyBodies[i]);
					}

					if (battleLog != null)
					{
						battleLog.AddEntry($"  → {enemies[i].name}에게 <color=#AAAAAA>{splash}</color> 스플래시!");
						if (wasAlive && !enemies[i].IsAlive)
							battleLog.AddEntry($"  <color=#FF8888>{enemies[i].name} 처치!</color>");
					}
					if (wasAlive && !enemies[i].IsAlive)
						AudioManager.Play("Enemy_Die");
				}
			}
		}

		RefreshAllEnemyHp();

		if (attack.shakeIntensity > 0f)
			vfx.Shake(attack.shakeIntensity);

		bool allDead = true;
		foreach (var e in enemies)
		{
			if (e.IsAlive) { allDead = false; break; }
		}

		if (hud != null)
		{
			hud.SetDamageResultText(!string.IsNullOrEmpty(comboName)
				? $"{comboName}! {damage} 데미지"
				: $"{damage} 데미지");
		}

		if (allDead)
		{
			if (battleLog != null)
				battleLog.AddEntry("<color=#55FF55>모든 적을 처치했다!</color>");
			Debug.Log("[Battle] AllEnemiesDead → BattleWon");
			StartCoroutine(BattleWonRoutine());
		}
		else
		{
			if (!enemies[targetIndex].IsAlive)
			{
				for (int i = 0; i < enemies.Count; i++)
				{
					if (enemies[i].IsAlive) { targetIndex = i; break; }
				}
				RefreshTargetMarkers();
			}

			if (counterAttackDirector != null)
				counterAttackDirector.StartCounterAttack();
		}
	}

	public void NextRound()
	{
		roundConfirmed = false;
		if (counterAttackDirector != null) counterAttackDirector.ResetOnNewTurn();

		if (diceDirector != null)
		{
			diceDirector.BeginTurn(3);
			diceDirector.SetHoldInteractionEnabled(false);
		}

		nextRoundButton.gameObject.SetActive(false);
		confirmButton.gameObject.SetActive(false);
		if (hud != null) hud.ClearDamageText();
		UpdateRollsText();

		if (battleLog != null)
			battleLog.AddEntry("<color=#AAAAAA>── 다음 라운드 ──</color>");
	}

	public void CancelBattle()
	{
		if (diceDirector != null && diceDirector.HasRolledOnce)
			return;
		if (battleLog != null)
			battleLog.AddEntry("<color=#AAAAAA>도박사가 전투를 회피했다.</color>");
		Debug.Log("[Battle] CancelBattle → GameExploreScene");
		AudioManager.Play("UI_Back_NO");
		AudioManager.Play("Transition_2");
		GameSessionManager.LastBattleResult = BattleResult.Cancelled;
		SceneManager.LoadScene("GameExploreScene");
	}

	IEnumerator PlayerDefeatedRoutine()
	{
		Debug.Log("[Battle] PlayerDefeated → MainMenu");
		if (hud != null) hud.SetDamageResultText("패배...");
		rollButton.interactable = false;
		confirmButton.gameObject.SetActive(false);
		nextRoundButton.gameObject.SetActive(false);

		AudioManager.Play("Player_Death");
		if (deathAnimator != null)
		{
			// 기존 플레이어 캐릭터는 Y축 180도 회전(좌우반전) → 오른쪽을 바라봄
			yield return deathAnimator.PlayDeathSequence(facingRight: true);
		}
		else
		{
			yield return new WaitForSeconds(2f);
			SceneManager.LoadScene("MainMenu");
		}
	}

	IEnumerator BattleWonRoutine()
	{
		yield return new WaitForSeconds(1.2f);
		GameSessionManager.LastBattleResult = BattleResult.Won;
		GameSessionManager.BattleEnemies.Clear();
		AudioManager.Play("Transition_2");
		SceneManager.LoadScene("GameExploreScene");
	}

	// ── HUD 갱신 래퍼 ────────────────────────────────────────────

	void UpdateRollsText()
	{
		if (hud == null) return;
		int total = diceDirector != null ? diceDirector.MaxRolls : 3;
		int remaining = diceDirector != null ? diceDirector.RollsRemaining : 0;
		hud.RefreshRollDots(total, remaining);
	}

	void UpdateDamagePreview()
	{
		if (diceDirector == null || !diceDirector.HasRolledOnce)
		{
			if (hud != null) hud.SetDamageResultText("");
			return;
		}

		int[] values = ReadDiceValues();

		if (counterAttackDirector != null && counterAttackDirector.IsDefensePhase)
		{
			counterAttackDirector.UpdateDefensePreview(values);
			return;
		}

		var (damage, comboName, _, _) = DamageCalculator.Calculate(values, GameSessionManager.PowerUps);
		if (hud != null)
		{
			hud.ShowAttackPreview(damage, comboName);
			if (!string.IsNullOrEmpty(comboName))
				hud.ShowComboLabel(comboName);
			else
				hud.StopComboLabel();
		}
	}

	void UpdatePlayerHUD()
	{
		if (heartDisplay != null)
			heartDisplay.Refresh(GameSessionManager.PlayerHearts);
	}

	// ── 디버그 명령(IBattleDebugTarget) ─────────────────────────

	public string DebugSetDice(int[] values)
	{
		if (diceDirector == null) return "[오류] DiceRollDirector 미할당.";
		if (values == null || values.Length != 5)
			return "[오류] 주사위 5개의 값이 필요합니다.";

		if (diceDirector.AnyDiceSpinning())
			return "[오류] 주사위가 굴러가는 중에는 사용할 수 없습니다.";

		if (roundConfirmed)
			return "[오류] 주사위를 굴릴 수 있는 상황이 아닙니다. 다음 턴 버튼을 눌러주세요.";

		cancelButton.gameObject.SetActive(false);
		diceDirector.ForceSetDiceToVault(values);

		UpdateRollsText();
		confirmButton.gameObject.SetActive(true);
		roundConfirmed = false;
		UpdateDamagePreview();
		return $"주사위 강제 적용 → Vault 저장: [{string.Join(", ", values)}] (남은 굴림: {diceDirector.RollsRemaining})";
	}

	public string DebugKillPlayer()
	{
		if (!GameSessionManager.IsPlayerAlive)
			return "[무시] 플레이어가 이미 사망 상태입니다.";

		GameSessionManager.PlayerHearts.TakeDamage(GameSessionManager.PlayerHearts.TotalHalfHearts);
		UpdatePlayerHUD();
		rollButton.interactable = false;
		confirmButton.gameObject.SetActive(false);
		nextRoundButton.gameObject.SetActive(false);
		StartCoroutine(PlayerDefeatedRoutine());
		return "플레이어 즉사 → 패배 처리 시작";
	}

	public string DebugKillAllEnemies()
	{
		bool anyAlive = false;
		foreach (var e in enemies)
		{
			if (e.IsAlive) { anyAlive = true; break; }
		}
		if (!anyAlive)
			return "[무시] 살아있는 적이 없습니다.";

		foreach (var e in enemies)
			e.hp = 0;
		RefreshAllEnemyHp();
		RefreshTargetMarkers();
		confirmButton.gameObject.SetActive(false);
		rollButton.interactable = false;
		nextRoundButton.gameObject.SetActive(false);
		StartCoroutine(BattleWonRoutine());
		return "모든 적 즉사 → 승리 처리 시작";
	}

	public string DebugKillEnemies(int[] indices)
	{
		var sb = new System.Text.StringBuilder();
		foreach (int idx in indices)
		{
			if (idx < 0 || idx >= enemies.Count)
			{
				sb.AppendLine($"  인덱스 {idx}: 범위 밖 (유효: 0~{enemies.Count - 1})");
				continue;
			}
			if (!enemies[idx].IsAlive)
			{
				sb.AppendLine($"  인덱스 {idx} ({enemies[idx].name}): 이미 사망");
				continue;
			}
			enemies[idx].hp = 0;
			sb.AppendLine($"  인덱스 {idx} ({enemies[idx].name}): 처치");
		}

		RefreshAllEnemyHp();

		bool allDead = true;
		foreach (var e in enemies)
		{
			if (e.IsAlive) { allDead = false; break; }
		}

		if (allDead)
		{
			confirmButton.gameObject.SetActive(false);
			rollButton.interactable = false;
			nextRoundButton.gameObject.SetActive(false);
			StartCoroutine(BattleWonRoutine());
			sb.AppendLine("  → 모든 적 사망, 승리 처리 시작");
		}
		else
		{
			if (!enemies[targetIndex].IsAlive)
			{
				for (int i = 0; i < enemies.Count; i++)
				{
					if (enemies[i].IsAlive) { targetIndex = i; break; }
				}
			}
			RefreshTargetMarkers();
		}

		return sb.ToString().TrimEnd();
	}
}
