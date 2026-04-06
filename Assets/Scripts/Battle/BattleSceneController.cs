using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class BattleSceneController : MonoBehaviour
{
	[SerializeField] YachtDie[] dice;
	[SerializeField] DiceViewportInteraction viewportInteraction;

	[SerializeField] GameObject[] enemyPanels;
	[SerializeField] Image[] enemyBodies;
	[SerializeField] TMP_Text[] enemyNames;
	[SerializeField] Image[] enemyHpFills;
	[SerializeField] TMP_Text[] enemyHpTexts;
	[SerializeField] Image[] targetMarkers;
	[SerializeField] TMP_Text[] deadOverlays;

	[SerializeField] BattleDamageVFX vfx;
	[SerializeField] BattleLog battleLog;

	[SerializeField] Button rollButton;
	[SerializeField] Button confirmButton;
	[SerializeField] Button cancelButton;
	[SerializeField] Button nextRoundButton;

	[SerializeField] TMP_Text rollsText;
	[SerializeField] TMP_Text damagePreviewText;
	[SerializeField] TMP_Text playerHpText;
	[SerializeField] Image playerHpFill;

	int rollsRemaining;
	bool roundConfirmed;
	bool hasRolledOnce;
	int activeDiceCount;
	Vector3[] homePositions;
	[SerializeField] Vector3 vaultCenter;

	// 홀드된 주사위를 고른 순서대로 추적 — Vault 내 왼쪽부터 채움
	List<int> heldOrder = new List<int>();

	// (미사용 — /setdice가 즉시 Vault에 배치하므로 예약 불필요)

	List<EnemyInfo> enemies;
	int targetIndex;

	// (적 반격 데미지는 EnemyInfo.attack에서 개별 관리)

	void Start()
	{
		// 원본 보존: 전투 취소 시 GameSessionManager의 적 데이터를 오염시키지 않기 위해 딥카피
		enemies = new List<EnemyInfo>();
		foreach (var e in GameSessionManager.BattleEnemies)
			enemies.Add(e.Clone());
		// 첫 번째 살아있는 적을 자동 선택
		targetIndex = 0;
		for (int i = 0; i < enemies.Count; i++)
		{
			if (enemies[i].IsAlive)
			{
				targetIndex = i;
				break;
			}
		}

		if (enemies == null || enemies.Count == 0)
			Debug.LogError("[Battle] Start: BattleEnemies가 비어 있음");
		else
			Debug.Log($"[Battle] Start enemies={enemies.Count} target={targetIndex} boss={GameSessionManager.IsBossBattle} hp={GameSessionManager.PlayerHp}");

		var mainCamTransform = Camera.main?.transform;
		if (mainCamTransform == null)
			Debug.LogWarning("[Battle] MainCamera를 찾을 수 없음 — 화면 흔들림 비활성화");
		vfx.Init(mainCamTransform);

		InitDice();
		SetupEnemyDisplay();
		UpdatePlayerHUD();

		rollsRemaining = 3;
		hasRolledOnce = false;
		roundConfirmed = false;

		cancelButton.gameObject.SetActive(true);
		confirmButton.gameObject.SetActive(false);
		nextRoundButton.gameObject.SetActive(false);
		UpdateRollsText();
		damagePreviewText.text = "";

		// 전투 개시 로그
		if (battleLog != null)
		{
			battleLog.Clear();
			if (GameSessionManager.IsBossBattle)
				battleLog.AddEntry("<color=#FF5555>— 보스 전투 개시! —</color>");
			else
				battleLog.AddEntry("— 전투 개시! —");
			foreach (var e in enemies)
				battleLog.AddEntry($"  {e.name} <color=#AAAAAA>(HP {e.maxHp} / ATK {e.attack})</color>");
		}
	}

	void InitDice()
	{
		homePositions = new Vector3[dice.Length];
		for (int i = 0; i < dice.Length; i++)
		{
			homePositions[i] = dice[i].transform.position;
			dice[i].OnSettled += HandleDieSettled;
		}

		viewportInteraction.OnHoverEnter += HandleHoverEnter;
		viewportInteraction.OnHoverExit += HandleHoverExit;
		viewportInteraction.OnClicked += OnDieClicked;
	}

	void OnDestroy()
	{
		if (dice != null)
		{
			foreach (var die in dice)
			{
				if (die != null)
					die.OnSettled -= HandleDieSettled;
			}
		}

		if (viewportInteraction != null)
		{
			viewportInteraction.OnHoverEnter -= HandleHoverEnter;
			viewportInteraction.OnHoverExit -= HandleHoverExit;
			viewportInteraction.OnClicked -= OnDieClicked;
		}
	}

	void HandleDieSettled(YachtDie die, int result)
	{
		OnDieSettled();
	}

	void HandleHoverEnter(YachtDie die)
	{
		die.SetHovered(true);
	}

	void HandleHoverExit(YachtDie die)
	{
		die.SetHovered(false);
	}

	void SetupEnemyDisplay()
	{
		for (int i = 0; i < enemyPanels.Length; i++)
		{
			if (i < enemies.Count)
			{
				enemyPanels[i].SetActive(true);
				if (enemies[i].sprite != null)
				{
					enemyBodies[i].sprite = enemies[i].sprite;
					enemyBodies[i].color = Color.white;
					enemyBodies[i].preserveAspect = true;
				}
				else
				{
					enemyBodies[i].sprite = null;
					enemyBodies[i].color = enemies[i].color;
					enemyBodies[i].preserveAspect = false;
				}
				enemyNames[i].text = $"{enemies[i].name}  <color=#FF6666>ATK {enemies[i].attack}</color>";
				UpdateEnemyHp(i);
				targetMarkers[i].gameObject.SetActive(i == targetIndex);

				// 보스전: 패널 1개를 가운데 크게 배치 + 좌우반전
				if (GameSessionManager.IsBossBattle && i == 0)
				{
					var rt = enemyPanels[i].GetComponent<RectTransform>();
					rt.anchorMin = new Vector2(0.25f, 0f);
					rt.anchorMax = new Vector2(0.75f, 1f);
					rt.offsetMin = Vector2.zero;
					rt.offsetMax = Vector2.zero;
					enemyBodies[i].rectTransform.localScale = new Vector3(-1f, 1f, 1f);
				}
			}
			else
			{
				enemyPanels[i].SetActive(false);
			}
		}
	}

	// ── 적 타겟 선택 (패널 클릭) ──
	// PersistentListener는 int 파라미터 직접 바인딩 불가 → 인덱스별 래퍼 메서드 필요
	public void OnEnemyPanel0Clicked() => OnEnemyPanelClicked(0);
	public void OnEnemyPanel1Clicked() => OnEnemyPanelClicked(1);
	public void OnEnemyPanel2Clicked() => OnEnemyPanelClicked(2);
	public void OnEnemyPanel3Clicked() => OnEnemyPanelClicked(3);

	public void OnEnemyPanelClicked(int index)
	{
		if (index < 0 || index >= enemies.Count)
			return;
		if (!enemies[index].IsAlive)
			return;
		if (roundConfirmed)
			return;

		Debug.Log($"[Battle] OnEnemyPanelClicked → 타겟 변경: idx={index} \"{enemies[index].name}\"");
		targetIndex = index;
		RefreshTargetMarkers();
		UpdateDamagePreview();
	}

	// RollDice, ConfirmScore, CancelBattle, NextRound은 GameBattleSceneBuilder에서 PersistentListener로 연결됨

	Vector3 VaultSlotPosition(int slot)
	{
		return new Vector3(vaultCenter.x + (-2f + slot * 1f), vaultCenter.y, vaultCenter.z);
	}

	/// <summary>홀드 순서에 따라 Vault 내 모든 주사위를 왼쪽부터 재배치.</summary>
	void RearrangeVault()
	{
		for (int slot = 0; slot < heldOrder.Count; slot++)
		{
			int dieIdx = heldOrder[slot];
			dice[dieIdx].transform.position = VaultSlotPosition(slot);
		}
	}

	void OnDieClicked(YachtDie die)
	{
		if (roundConfirmed || rollsRemaining >= 3)
			return;
		if (System.Array.Exists(dice, d => d.IsRolling))
			return;

		int idx = System.Array.IndexOf(dice, die);
		if (idx < 0)
			return;

		if (!die.IsHeld)
		{
			// 홀드: 순서 리스트 끝에 추가 → 다음 빈 슬롯에 배치
			heldOrder.Add(idx);
			die.SetHeld(true, VaultSlotPosition(heldOrder.Count - 1));
		}
		else
		{
			// 홀드 해제: 순서 리스트에서 제거 → 나머지 좌측으로 재정렬
			heldOrder.Remove(idx);
			die.SetHeld(false, homePositions[idx]);
			RearrangeVault();
		}
		UpdateDamagePreview();
	}

	public void RollDice()
	{
		if (rollsRemaining <= 0 || roundConfirmed)
			return;

		// 홀드되지 않은 주사위가 있는지 먼저 확인
		int toRoll = 0;
		for (int i = 0; i < dice.Length; i++)
		{
			if (!dice[i].IsHeld)
				toRoll++;
		}

		if (toRoll == 0)
		{
			Debug.LogWarning("[Battle] RollDice: 모든 주사위 홀드 — 롤 취소");
			return;
		}

		hasRolledOnce = true;
		cancelButton.gameObject.SetActive(false);
		rollsRemaining--;
		activeDiceCount = toRoll;
		Debug.Log($"[Battle] RollDice rollsRemaining={rollsRemaining}");

		for (int i = 0; i < dice.Length; i++)
		{
			if (!dice[i].IsHeld)
				dice[i].Roll();
		}

		rollButton.interactable = false;
		confirmButton.gameObject.SetActive(false);
		UpdateRollsText();
	}

	void OnDieSettled()
	{
		activeDiceCount--;
		if (activeDiceCount > 0)
			return;

		// 모든 주사위 정지
		int[] vals = ReadDiceValues();
		Debug.Log($"[Battle] AllDiceSettled values=[{string.Join(",", vals)}] rollsRemaining={rollsRemaining}");
		rollButton.interactable = rollsRemaining > 0;
		confirmButton.gameObject.SetActive(true);
		UpdateDamagePreview();
	}

	int[] ReadDiceValues()
	{
		int[] values = new int[dice.Length];
		for (int i = 0; i < dice.Length; i++)
			values[i] = dice[i].Result;
		return values;
	}

	public void ConfirmScore()
	{
		if (roundConfirmed)
			return;
		roundConfirmed = true;

		int[] values = ReadDiceValues();
		var (damage, comboName, shake, splashRatio) = DamageCalculator.Calculate(values, GameSessionManager.PowerUps);
		int splash = Mathf.FloorToInt(damage * splashRatio);
		Debug.Log($"[Battle] ConfirmScore combo=\"{comboName}\" damage={damage} splash={splash} target={targetIndex}");

		// 전투 로그: 공격 선언
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

		if (battleLog != null)
		{
			battleLog.AddEntry($"  → {enemies[targetIndex].name}에게 <color=#FFD94A>{damage}</color> 데미지!");
			if (targetWasAlive && !enemies[targetIndex].IsAlive)
				battleLog.AddEntry($"  <color=#FF8888>{enemies[targetIndex].name} 처치!</color>");
		}

		// 스플래시 (데미지 적용 후 로그 — 이미 죽은 적 제외)
		if (splash > 0)
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				if (i != targetIndex && enemies[i].IsAlive)
				{
					bool wasAlive = enemies[i].IsAlive;
					enemies[i].TakeDamage(splash);
					vfx.SpawnDamageText(i, splash);

					if (battleLog != null)
					{
						battleLog.AddEntry($"  → {enemies[i].name}에게 <color=#AAAAAA>{splash}</color> 스플래시!");
						if (wasAlive && !enemies[i].IsAlive)
							battleLog.AddEntry($"  <color=#FF8888>{enemies[i].name} 처치!</color>");
					}
				}
			}
		}

		RefreshAllEnemyHp();

		if (shake > 0f)
			vfx.Shake(shake);

		// 전부 처치 확인
		bool allDead = true;
		foreach (var e in enemies)
		{
			if (e.IsAlive)
			{
				allDead = false;
				break;
			}
		}

		confirmButton.gameObject.SetActive(false);
		rollButton.interactable = false;
		damagePreviewText.text = !string.IsNullOrEmpty(comboName)
			? $"{comboName}! {damage} 데미지"
			: $"{damage} 데미지";

		if (allDead)
		{
			if (battleLog != null)
				battleLog.AddEntry("<color=#55FF55>모든 적을 처치했다!</color>");
			Debug.Log("[Battle] AllEnemiesDead → BattleWon");
			StartCoroutine(BattleWonRoutine());
		}
		else
		{
			// 타겟이 죽었으면 다음 살아있는 적으로 변경
			if (!enemies[targetIndex].IsAlive)
			{
				for (int i = 0; i < enemies.Count; i++)
				{
					if (enemies[i].IsAlive)
					{
						targetIndex = i;
						break;
					}
				}
				RefreshTargetMarkers();
			}

			// 적 반격 (순차 연출)
			StartCoroutine(EnemyCounterAttackRoutine());
		}
	}

	public void NextRound()
	{
		rollsRemaining = 3;
		roundConfirmed = false;

		for (int i = 0; i < dice.Length; i++)
		{
			if (dice[i].IsHeld)
				dice[i].SetHeld(false, homePositions[i]);
		}
		heldOrder.Clear();

		hasRolledOnce = false;
		nextRoundButton.gameObject.SetActive(false);
		rollButton.interactable = true;
		confirmButton.gameObject.SetActive(false);
		damagePreviewText.text = "";
		UpdateRollsText();

		if (battleLog != null)
			battleLog.AddEntry("<color=#AAAAAA>── 다음 라운드 ──</color>");
	}

	IEnumerator EnemyCounterAttackRoutine()
	{
		int aliveCount = 0;
		foreach (var e in enemies)
		{
			if (e.IsAlive)
				aliveCount++;
		}

		if (aliveCount <= 0)
		{
			nextRoundButton.gameObject.SetActive(true);
			yield break;
		}

		yield return new WaitForSeconds(0.5f);

		for (int i = 0; i < enemies.Count; i++)
		{
			if (!enemies[i].IsAlive)
				continue;

			// 로그: 공격 선언
			if (battleLog != null)
				battleLog.AddEntry($"<color=#FF6666>{enemies[i].name}의 공격!</color>");

			// 적 바디 내려찍기 연출
			if (i < enemyBodies.Length && enemyBodies[i] != null)
				yield return StartCoroutine(EnemySlamAnimation(enemyBodies[i].rectTransform));

			// 데미지 적용
			bool revived = GameSessionManager.TakePlayerDamage(enemies[i].attack);
			UpdatePlayerHUD();

			if (battleLog != null)
				battleLog.AddEntry($"  도박사에게 <color=#FF6666>{enemies[i].attack}</color> 데미지!");

			if (revived)
			{
				damagePreviewText.text = "부활 패시브 발동!";
				damagePreviewText.color = new Color(0.3f, 1f, 0.5f);
				StartCoroutine(FlashReviveText());
				if (battleLog != null)
					battleLog.AddEntry("<color=#55FF88>부활의 부적이 빛난다! 데미지 무효화!</color>");
			}

			// 사망 체크
			if (GameSessionManager.PlayerHp <= 0)
			{
				if (battleLog != null)
				{
					battleLog.AddEntry($"  도박사 HP: 0 / {GameSessionManager.PlayerMaxHp}");
					battleLog.AddEntry("<color=#FF3333>도박사가 쓰러졌다...</color>");
				}
				StartCoroutine(PlayerDefeatedRoutine());
				yield break;
			}

			yield return new WaitForSeconds(0.3f);
		}

		if (battleLog != null)
			battleLog.AddEntry($"  도박사 HP: {GameSessionManager.PlayerHp} / {GameSessionManager.PlayerMaxHp}");

		Debug.Log($"[Battle] EnemyCounterAttack done playerHp={GameSessionManager.PlayerHp}");
		nextRoundButton.gameObject.SetActive(true);
	}

	IEnumerator EnemySlamAnimation(RectTransform body)
	{
		Vector2 originalPos = body.anchoredPosition;
		float slamDistance = 40f;
		float slamDownTime = 0.1f;
		float holdTime = 0.08f;
		float returnTime = 0.15f;

		// 내려찍기
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

		// 복귀
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

	IEnumerator PlayerDefeatedRoutine()
	{
		Debug.Log("[Battle] PlayerDefeated → MainMenu");
		damagePreviewText.text = "패배...";
		rollButton.interactable = false;
		confirmButton.gameObject.SetActive(false);
		nextRoundButton.gameObject.SetActive(false);
		yield return new WaitForSeconds(2f);
		SceneManager.LoadScene("MainMenu");
	}

	IEnumerator BattleWonRoutine()
	{
		yield return new WaitForSeconds(1.2f);
		GameSessionManager.LastBattleResult = BattleResult.Won;
		GameSessionManager.BattleEnemies.Clear();
		SceneManager.LoadScene("GameExploreScene");
	}

	public void CancelBattle()
	{
		if (hasRolledOnce)
			return;
		if (battleLog != null)
			battleLog.AddEntry("<color=#AAAAAA>도박사가 전투를 회피했다.</color>");
		Debug.Log("[Battle] CancelBattle → GameExploreScene");
		GameSessionManager.LastBattleResult = BattleResult.Cancelled;
		SceneManager.LoadScene("GameExploreScene");
	}

	void UpdateEnemyHp(int i)
	{
		var e = enemies[i];
		enemyHpFills[i].fillAmount = (float)e.hp / e.maxHp;
		enemyHpTexts[i].text = $"{e.hp} / {e.maxHp}";

		if (i < deadOverlays.Length && deadOverlays[i] != null)
			deadOverlays[i].gameObject.SetActive(!e.IsAlive);
	}

	void RefreshAllEnemyHp()
	{
		for (int i = 0; i < enemies.Count; i++)
		{
			if (i < enemyPanels.Length)
				UpdateEnemyHp(i);
		}
	}

	void RefreshTargetMarkers()
	{
		for (int i = 0; i < enemyPanels.Length; i++)
		{
			if (i < enemies.Count)
				targetMarkers[i].gameObject.SetActive(i == targetIndex);
		}
	}

	void UpdateRollsText()
	{
		rollsText.text = $"남은 굴림: {rollsRemaining}";
	}

	void UpdateDamagePreview()
	{
		if (!hasRolledOnce)
		{
			damagePreviewText.text = "";
			return;
		}

		int[] values = ReadDiceValues();
		var (damage, comboName, _, _) = DamageCalculator.Calculate(values, GameSessionManager.PowerUps);
		if (!string.IsNullOrEmpty(comboName))
			damagePreviewText.text = $"예상: {comboName} → {damage}";
		else
			damagePreviewText.text = $"예상: {damage}";
	}

	void UpdatePlayerHUD()
	{
		float ratio = (float)GameSessionManager.PlayerHp / GameSessionManager.PlayerMaxHp;
		playerHpFill.fillAmount = ratio;
		playerHpText.text = $"HP {GameSessionManager.PlayerHp} / {GameSessionManager.PlayerMaxHp}";
	}

	IEnumerator FlashReviveText()
	{
		float elapsed = 0f;
		float duration = 1.5f;
		Vector3 baseScale = damagePreviewText.transform.localScale;
		damagePreviewText.transform.localScale = baseScale * 1.5f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / duration;
			damagePreviewText.transform.localScale = Vector3.Lerp(baseScale * 1.5f, baseScale, t);
			damagePreviewText.color = Color.Lerp(new Color(0.3f, 1f, 0.5f), Color.white, t);
			yield return null;
		}
		damagePreviewText.transform.localScale = baseScale;
		damagePreviewText.color = Color.white;
	}

	// ── 디버그 명령 ──

	public string DebugSetDice(int[] values)
	{
		if (values == null || values.Length != dice.Length)
			return $"[오류] 주사위 {dice.Length}개의 값이 필요합니다.";

		bool anyRolling = false;
		foreach (var d in dice)
		{
			if (d.IsRolling)
			{
				anyRolling = true;
				break;
			}
		}

		if (anyRolling)
			return "[오류] 주사위가 굴러가는 중에는 사용할 수 없습니다.";

		if (roundConfirmed)
			return "[오류] 주사위를 굴릴 수 있는 상황이 아닙니다. 다음 턴 버튼을 눌러주세요.";

		// 굴림 횟수 1 소진 (0이어도 강제 진행)
		if (rollsRemaining > 0)
			rollsRemaining--;
		hasRolledOnce = true;
		cancelButton.gameObject.SetActive(false);

		// 모든 주사위를 강제값으로 설정하고 Vault에 배치
		heldOrder.Clear();
		for (int i = 0; i < dice.Length; i++)
		{
			if (dice[i].IsHeld)
				dice[i].SetHeld(false, homePositions[i]);
			dice[i].ForceResultWithRotation(values[i]);
			heldOrder.Add(i);
			dice[i].SetHeld(true, VaultSlotPosition(i));
		}

		UpdateRollsText();
		rollButton.interactable = rollsRemaining > 0;
		confirmButton.gameObject.SetActive(true);
		roundConfirmed = false;
		UpdateDamagePreview();
		return $"주사위 강제 적용 → Vault 저장: [{string.Join(", ", values)}] (남은 굴림: {rollsRemaining})";
	}

	public string DebugKillPlayer()
	{
		if (GameSessionManager.PlayerHp <= 0)
			return "[무시] 플레이어가 이미 사망 상태입니다.";

		GameSessionManager.PlayerHp = 0;
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
			if (e.IsAlive)
			{
				anyAlive = true;
				break;
			}
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
			if (e.IsAlive)
			{
				allDead = false;
				break;
			}
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
					if (enemies[i].IsAlive)
					{
						targetIndex = i;
						break;
					}
				}
			}
			RefreshTargetMarkers();
		}

		return sb.ToString().TrimEnd();
	}
}
