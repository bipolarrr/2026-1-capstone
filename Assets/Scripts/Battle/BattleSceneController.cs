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
	[SerializeField] Sprite[] mobSprites;

	[SerializeField] BattleDamageVFX vfx;
	[SerializeField] BattleLog battleLog;
	[SerializeField] BattleAnimations battleAnims;
	[SerializeField] Image playerBody;

	[SerializeField] Button rollButton;
	[SerializeField] Button confirmButton;
	[SerializeField] Button cancelButton;
	[SerializeField] Button nextRoundButton;

	[SerializeField] TMP_Text rollDotsText;
	[SerializeField] TMP_Text damagePreviewText;
	[SerializeField] HeartDisplay heartDisplay;

	// ── 적 주사위 시스템 ──
	[SerializeField] EnemyDiceRoller enemyDiceRoller;
	[SerializeField] GameObject enemyDicePopup;
	[SerializeField] TMP_Text[] enemyDiceResultTexts;
	[SerializeField] GameObject[] enemyDiceFaceContainers;
	[SerializeField] Sprite[] diceFaceSprites;
	[SerializeField] PlayerDeathAnimator deathAnimator;
	[SerializeField] PlayerRollAnimator rollAnimator;

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

	// ── 방어 페이즈 상태 ──
	bool isDefensePhase;
	int defenseRollsMax;
	int currentDefenseEnemyIndex;
	bool defenseConfirmed;
	bool lastDefenseBlocked;
	int lastDefenseDamage;
	// ── 몹 데이터 (GameExploreController와 동일) ──
	static readonly string[] MobNames = { "슬라임", "고블린", "박쥐", "해골" };
	static readonly Color[] MobColors =
	{
		new Color(0.60f, 0.90f, 0.60f),
		new Color(0.95f, 0.75f, 0.50f),
		new Color(0.75f, 0.60f, 0.90f),
		new Color(0.85f, 0.85f, 0.85f)
	};
	static readonly (int hpMin, int hpMax, int rank)[] MobStatPool =
	{
		(30, 40, 1),
		(18, 25, 2),
		(10, 15, 3),
		(22, 30, 2),
	};

	void GenerateDefaultEnemies()
	{
		GameSessionManager.BattleEnemies.Clear();
		int count = 4;
		for (int i = 0; i < count; i++)
		{
			var stat = MobStatPool[i];
			int hp = Random.Range(stat.hpMin, stat.hpMax + 1);
			Sprite spr = (mobSprites != null && i < mobSprites.Length) ? mobSprites[i] : null;
			GameSessionManager.BattleEnemies.Add(
				new EnemyInfo(MobNames[i], hp, stat.rank, MobColors[i], spr));
		}
		Debug.Log($"[Battle] 기본 적 생성 count={count} [{string.Join(", ", GameSessionManager.BattleEnemies.ConvertAll(e => $"{e.name}(hp{e.hp} rank{e.rank})"))}]");
	}

	void Start()
	{
		// 씬 직접 로딩 시 (Explore를 거치지 않은 경우) 기본 상태 자동 생성
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

		if (enemies.Count == 0)
			Debug.LogError("[Battle] Start: BattleEnemies가 비어 있음");
		else
			Debug.Log($"[Battle] Start enemies={enemies.Count} target={targetIndex} boss={GameSessionManager.IsBossBattle} hearts={GameSessionManager.PlayerHearts.TotalHalfHearts}");

		var mainCamTransform = Camera.main?.transform;
		if (mainCamTransform == null)
			Debug.LogWarning("[Battle] MainCamera를 찾을 수 없음 — 화면 흔들림 비활성화");
		vfx.Init(mainCamTransform);

		InitDice();
		SetupEnemyDisplay();
		UpdatePlayerHUD();

		isDefensePhase = false;
		rollsRemaining = 3;
		hasRolledOnce = false;
		roundConfirmed = false;

		cancelButton.gameObject.SetActive(true);
		confirmButton.gameObject.SetActive(false);
nextRoundButton.gameObject.SetActive(false);
		if (enemyDicePopup != null)
			enemyDicePopup.SetActive(false);
		if (enemyDiceFaceContainers != null)
		{
			foreach (var c in enemyDiceFaceContainers)
				if (c != null) c.SetActive(false);
		}
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
				battleLog.AddEntry($"  {e.name} <color=#FFD94A>{e.RankStars}</color> <color=#AAAAAA>(HP {e.maxHp})</color>");
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

	// 몹별 바디 앵커: (yMin, yMax) — 슬롯 내 상대 좌표, 0이 바닥(지면)
	// GameExploreController.MobBodyAnchors와 동일한 값 유지
	static readonly System.Collections.Generic.Dictionary<string, (float yMin, float yMax)> MobBodyAnchors = new()
	{
		{ "슬라임", (0.00f, 0.40f) },   // 납작, 바닥 밀착
		{ "고블린", (0.00f, 0.75f) },   // 중간 키, 바닥
		{ "박쥐",   (0.30f, 0.80f) },   // 공중 부유
		{ "해골",   (0.00f, 0.75f) },   // 고블린과 동일
	};

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
				enemyNames[i].text = $"{enemies[i].name}  <color=#FFD94A>{enemies[i].RankStars}</color>";
				UpdateEnemyHp(i);
				targetMarkers[i].gameObject.SetActive(i == targetIndex);

				// 몹별 바디 크기/높이 차등 적용
				var bodyRt = enemyBodies[i].rectTransform;
				if (GameSessionManager.IsBossBattle && i == 0)
				{
					var rt = enemyPanels[i].GetComponent<RectTransform>();
					rt.anchorMin = new Vector2(0.25f, 0f);
					rt.anchorMax = new Vector2(0.75f, 1f);
					rt.offsetMin = Vector2.zero;
					rt.offsetMax = Vector2.zero;
					bodyRt.localScale = new Vector3(-1f, 1f, 1f);
				}
				else if (MobBodyAnchors.TryGetValue(enemies[i].name, out var anchors))
				{
					bodyRt.anchorMin = new Vector2(0.05f, anchors.yMin);
					bodyRt.anchorMax = new Vector2(0.95f, anchors.yMax);
					bodyRt.offsetMin = Vector2.zero;
					bodyRt.offsetMax = Vector2.zero;

					// 타겟 마커·사망 오버레이를 바디와 동일 영역으로
					var markerRt = targetMarkers[i].rectTransform;
					markerRt.anchorMin = new Vector2(0.05f, anchors.yMin);
					markerRt.anchorMax = new Vector2(0.95f, anchors.yMax);
					markerRt.offsetMin = Vector2.zero;
					markerRt.offsetMax = Vector2.zero;

					var deadRt = deadOverlays[i].rectTransform;
					deadRt.anchorMin = new Vector2(0.05f, anchors.yMin);
					deadRt.anchorMax = new Vector2(0.95f, anchors.yMax);
					deadRt.offsetMin = Vector2.zero;
					deadRt.offsetMax = Vector2.zero;

					// InfoPanel(반투명 배경)을 바디 상단(머리 위)에 배치
					float infoPanelBottom = anchors.yMax;
					float infoPanelTop = infoPanelBottom + 0.18f;

					var infoPanelRt = enemyNames[i].transform.parent.GetComponent<RectTransform>();
					infoPanelRt.anchorMin = new Vector2(0.0f, infoPanelBottom);
					infoPanelRt.anchorMax = new Vector2(1.0f, infoPanelTop);
					infoPanelRt.offsetMin = Vector2.zero;
					infoPanelRt.offsetMax = Vector2.zero;
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
		return new Vector3(vaultCenter.x, vaultCenter.y, vaultCenter.z + (1.7f - slot * 0.85f));
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
			if (battleLog != null)
				battleLog.AddEntry("<color=#FF5555>모든 주사위를 저장하고 있다!</color>");
			return;
		}

		hasRolledOnce = true;
		cancelButton.gameObject.SetActive(false);
		rollsRemaining--;
		activeDiceCount = toRoll;
		Debug.Log($"[Battle] RollDice rollsRemaining={rollsRemaining}");

		if (rollAnimator != null)
			rollAnimator.Play();

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
		Debug.Log($"[Battle] AllDiceSettled values=[{string.Join(",", vals)}] rollsRemaining={rollsRemaining} defense={isDefensePhase}");
		rollButton.interactable = rollsRemaining > 0;

		if (isDefensePhase)
		{
			int ci = currentDefenseEnemyIndex;
			if (ci >= 0 && ci < enemies.Count && enemies[ci].IsAlive && enemies[ci].lastDiceResult != null)
			{
				// 랭크 < 4: 굴림 즉시 판정 (확정 버튼 불필요)
				// 완벽 방어: 기회가 남아도 즉시 확정
				bool autoConfirm = enemies[ci].rank < 4;
				if (!autoConfirm)
				{
					var defense = DefenseCalculator.Evaluate(vals, enemies[ci].lastDiceResult);
					autoConfirm = defense.blocked;
				}
				if (autoConfirm)
				{
					ConfirmDefense();
					return;
				}
			}
		}

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
		if (isDefensePhase)
		{
			ConfirmDefense();
			return;
		}

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
		if (battleAnims != null && targetIndex < enemyBodies.Length)
			battleAnims.FlashHit(enemyBodies[targetIndex]);

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
					if (battleAnims != null && i < enemyBodies.Length)
						battleAnims.FlashHit(enemyBodies[i]);

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
		isDefensePhase = false;
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
if (enemyDiceFaceContainers != null)
		{
			foreach (var c in enemyDiceFaceContainers)
				if (c != null) c.SetActive(false);
		}
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

		// ── 각 적이 순차적으로 공격 → 플레이어 방어 ──
		for (int i = 0; i < enemies.Count; i++)
		{
			if (!enemies[i].IsAlive)
				continue;

			if (battleLog != null)
				battleLog.AddEntry($"<color=#FF6666>{enemies[i].name}의 공격!</color>");

			// 일반 몹 여부 판별
			bool isMelee = battleAnims != null && !GameSessionManager.IsBossBattle
				&& i < enemyPanels.Length && enemyPanels[i] != null && playerBody != null;

			RectTransform slotRt = null;
			Vector3 slotOriginalLocal = Vector3.zero;

			if (isMelee)
			{
				slotRt = enemyPanels[i].GetComponent<RectTransform>();
				slotOriginalLocal = slotRt.localPosition;

				// ① 플레이어 앞까지 이동 (X좌표만 변경, Y는 몹 원래 높이 유지)
				// 몹 바디의 월드 폭만큼 간격을 두고 플레이어 오른쪽에 섬
				Vector3 slotWorld = slotRt.position;
				Vector3 playerWorld = playerBody.rectTransform.position;
				float bodyWidth = enemyBodies[i].rectTransform.rect.width
					* enemyBodies[i].rectTransform.lossyScale.x;
				Vector3 playerFrontWorld = new Vector3(playerWorld.x + bodyWidth, slotWorld.y, slotWorld.z);
				yield return StartCoroutine(battleAnims.WalkTo(slotRt, playerFrontWorld, 0.4f));

				// ② 제자리 점프
				if (i < enemyBodies.Length && enemyBodies[i] != null)
					yield return StartCoroutine(battleAnims.JumpInPlace(enemyBodies[i].rectTransform));
			}
			else if (i < enemyBodies.Length && enemyBodies[i] != null)
			{
				// 보스: 제자리 점프만
				yield return StartCoroutine(EnemyJumpAnimation(enemyBodies[i].rectTransform));
			}

			// 적 주사위 팝업 표시
			if (enemyDicePopup != null)
				enemyDicePopup.SetActive(true);

			// 적 주사위 굴림
			EnemyDiceResult result = null;
			if (enemyDiceRoller != null)
			{
				bool rolled = false;
				enemyDiceRoller.RollForEnemy(enemies[i].rank, r =>
				{
					result = r;
					rolled = true;
				});
				while (!rolled)
					yield return null;
			}
			else
			{
				// 폴백: 물리 주사위 없으면 랜덤 생성
				int[] vals = new int[enemies[i].rank];
				for (int d = 0; d < vals.Length; d++)
					vals[d] = Random.Range(1, 7);
				string combo = "";
				bool hasCombo = false;
				float mult = 0.5f;
				if (vals.Length >= 4)
				{
					var (_, cn, _, _) = DamageCalculator.Calculate(
						PadToFive(vals), new List<PowerUpType>());
					if (!string.IsNullOrEmpty(cn))
					{
						combo = cn;
						hasCombo = true;
						mult = EnemyDiceResult.GetMultiplier(cn);
					}
				}
				result = new EnemyDiceResult
				{
					values = vals,
					comboName = combo,
					damageMultiplier = mult,
					hasCombo = hasCombo
				};
			}

			// 팝업 닫기
			if (enemyDicePopup != null)
				enemyDicePopup.SetActive(false);

			enemies[i].lastDiceResult = result;

			// 결과 표시 (적 패널 위 — 족보 이름만, 눈은 주사위 이미지로)
			if (i < enemyDiceResultTexts.Length && enemyDiceResultTexts[i] != null)
			{
				if (result.hasCombo)
					enemyDiceResultTexts[i].text = $"<color=#FFD94A>{result.comboName}</color>";
				else
					enemyDiceResultTexts[i].text = "";
			}

			// 적 주사위 눈을 발 밑에 평면 표시
			ShowEnemyDiceFaces(i, result.values);

			if (battleLog != null)
			{
				string diceStr = $"[{string.Join(", ", result.values)}]";
				if (result.hasCombo)
					battleLog.AddEntry($"  <color=#FFD94A>{enemies[i].name}의 {result.comboName}!</color> {diceStr}");
				else
					battleLog.AddEntry($"  {enemies[i].name}이(가) 주사위를 굴렸다: {diceStr}");
			}

			yield return new WaitForSeconds(0.3f);

			// ── ③ 이 적에 대한 방어 페이즈 ──
			currentDefenseEnemyIndex = i;
			isDefensePhase = true;
			defenseConfirmed = false;
			lastDefenseBlocked = true;
			lastDefenseDamage = 0;
			defenseRollsMax = (enemies[i].rank >= 4) ? 3 : 1;
			rollsRemaining = defenseRollsMax;
			hasRolledOnce = false;
			roundConfirmed = false;

			// 플레이어 주사위 리셋
			for (int d = 0; d < dice.Length; d++)
			{
				if (dice[d].IsHeld)
					dice[d].SetHeld(false, homePositions[d]);
			}
			heldOrder.Clear();

			// UI 업데이트
			rollButton.interactable = true;
			confirmButton.gameObject.SetActive(false);
			damagePreviewText.text = "";

			UpdateRollsText();

			if (battleLog != null)
			{
				if (result.hasCombo)
					battleLog.AddEntry($"<color=#FFD94A>{enemies[i].name}의 {result.comboName}을(를) 막아라! 3회 굴림!</color>");
				else
					battleLog.AddEntry($"{enemies[i].name}의 공격을 방어하자! 1회 굴림!");
			}

			// 플레이어가 방어 굴림 + 확정할 때까지 대기
			while (!defenseConfirmed)
				yield return null;

			// ── ④⑤ 방어 결과에 따른 후속 애니메이션 ──
			if (isMelee && slotRt != null)
			{
				if (!lastDefenseBlocked)
				{
					// ⑤ 방어 실패: 플레이어 쪽으로 돌진 → 타격 → 플레이어 앞으로 복귀 → 걸어서 원래 자리
					Vector3 slamTarget = new Vector3(playerBody.rectTransform.position.x, slotRt.position.y, slotRt.position.z);
					yield return StartCoroutine(battleAnims.QuickSlam(slotRt, slamTarget));
					if (battleAnims != null && playerBody != null)
						battleAnims.FlashHit(playerBody);
					yield return new WaitForSeconds(0.15f);
				}
				// ④ 방어 성공이든 실패든, 원래 자리로 복귀
				yield return StartCoroutine(battleAnims.WalkBack(slotRt, slotOriginalLocal, 0.5f));
			}
			else if (!lastDefenseBlocked)
			{
				// 보스: 제자리 내려찍기
				if (i < enemyBodies.Length && enemyBodies[i] != null)
					yield return StartCoroutine(EnemySlamAnimation(enemyBodies[i].rectTransform));
				if (battleAnims != null && playerBody != null)
					battleAnims.FlashHit(playerBody);
			}

			// 플레이어 사망 시 중단
			if (!GameSessionManager.IsPlayerAlive)
			{
				StartCoroutine(PlayerDefeatedRoutine());
				yield break;
			}

			// 이 적의 주사위 결과 초기화
			if (i < enemyDiceResultTexts.Length && enemyDiceResultTexts[i] != null)
				enemyDiceResultTexts[i].text = "";
			HideEnemyDiceFaces(i);

			yield return new WaitForSeconds(0.3f);
		}

		// 모든 적의 공격이 끝남 — 다음 라운드 버튼 표시
		isDefensePhase = false;
		confirmButton.gameObject.SetActive(false);
		rollButton.interactable = false;
		nextRoundButton.gameObject.SetActive(true);
	}

	/// <summary>방어 확정 — 현재 공격 중인 적의 주사위 결과와 비교.</summary>
	public void ConfirmDefense()
	{
		if (!isDefensePhase || !hasRolledOnce)
			return;

		int i = currentDefenseEnemyIndex;
		if (i < 0 || i >= enemies.Count || !enemies[i].IsAlive || enemies[i].lastDiceResult == null)
		{
			lastDefenseBlocked = true;
			lastDefenseDamage = 0;
			defenseConfirmed = true;
			return;
		}

		int[] playerValues = ReadDiceValues();

		if (battleLog != null)
			battleLog.AddEntry($"도박사의 방어 주사위: [{string.Join(", ", playerValues)}]");

		var result = enemies[i].lastDiceResult;
		var defense = DefenseCalculator.Evaluate(playerValues, result);

		// 랭크 1~3: 비례 방어 없음 (완벽 방어 or 전부 피격)
		float reduction = enemies[i].rank >= 4 ? defense.reductionRate : (defense.blocked ? 1f : 0f);
		int baseDmg = DefenseCalculator.CalculateEnemyDamage(enemies[i].rank, result.damageMultiplier);
		int finalDmg = Mathf.Max(0, Mathf.CeilToInt(baseDmg * (1f - reduction)));

		// 방어 결과 기록 — EnemyCounterAttackRoutine이 참조
		lastDefenseBlocked = defense.blocked || finalDmg <= 0;
		lastDefenseDamage = finalDmg;

		if (defense.blocked)
		{
			if (battleLog != null)
				battleLog.AddEntry($"  <color=#55FF55>{enemies[i].name}: 완벽 방어!</color>");
		}
		else if (finalDmg <= 0)
		{
			if (battleLog != null)
				battleLog.AddEntry($"  <color=#55FF55>{enemies[i].name}: 데미지 0!</color>");
		}
		else
		{
			if (battleLog != null)
			{
				if (enemies[i].rank >= 4 && reduction > 0f)
					battleLog.AddEntry($"  <color=#FFAA44>{enemies[i].name}: {Mathf.RoundToInt(reduction * 100)}% 방어 → {FormatHalf(finalDmg)} 데미지</color>");
				else
					battleLog.AddEntry($"  <color=#FF6666>{enemies[i].name}: 방어 실패! ({FormatHalf(finalDmg)} 데미지)</color>");
			}

			bool revived = GameSessionManager.TakePlayerDamage(finalDmg);
			UpdatePlayerHUD();

			if (revived)
			{
				damagePreviewText.text = "부활 패시브 발동!";
				damagePreviewText.color = new Color(0.3f, 1f, 0.5f);
				StartCoroutine(FlashReviveText());
				if (battleLog != null)
					battleLog.AddEntry("<color=#55FF88>부활의 부적이 빛난다! 데미지 무효화!</color>");
			}
		}

		// 이 적의 주사위 결과 초기화
		enemies[i].lastDiceResult = null;

		if (!GameSessionManager.IsPlayerAlive)
		{
			if (battleLog != null)
				battleLog.AddEntry("<color=#FF3333>도박사가 쓰러졌다...</color>");
		}
		else
		{
			if (battleLog != null)
				battleLog.AddEntry($"  도박사 하트: {FormatHalf(GameSessionManager.PlayerHearts.TotalHalfHearts)}");
			Debug.Log($"[Battle] Defense vs {enemies[i].name} done hearts={GameSessionManager.PlayerHearts.TotalHalfHearts}");
		}

		// UI 정리
		rollButton.interactable = false;
		confirmButton.gameObject.SetActive(false);

		// 코루틴에 방어 완료 신호 (타격 연출은 EnemyCounterAttackRoutine에서)
		defenseConfirmed = true;
	}

	/// <summary>적 점프 애니메이션: 위로 → 내려찍기.</summary>
	IEnumerator EnemyJumpAnimation(RectTransform body)
	{
		Vector2 originalPos = body.anchoredPosition;
		float jumpHeight = 30f;
		float jumpUpTime = 0.15f;
		float slamDistance = 40f;
		float slamDownTime = 0.1f;
		float holdTime = 0.08f;
		float returnTime = 0.15f;

		// 위로 점프
		float elapsed = 0f;
		while (elapsed < jumpUpTime)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / jumpUpTime;
			body.anchoredPosition = originalPos + Vector2.up * jumpHeight * t;
			yield return null;
		}

		// 내려찍기
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
		if (rollDotsText == null) return;
		int total = 3;
		var sb = new System.Text.StringBuilder();
		for (int i = 0; i < total; i++)
		{
			if (i > 0) sb.Append(' ');
			sb.Append(i < rollsRemaining ? '●' : '○');
		}
		rollDotsText.text = sb.ToString();
	}

	void UpdateDamagePreview()
	{
		if (!hasRolledOnce)
		{
			damagePreviewText.text = "";
			return;
		}

		int[] values = ReadDiceValues();

		if (isDefensePhase)
		{
			// 방어 프리뷰: 현재 방어 대상 적과의 매칭 여부만 표시
			int i = currentDefenseEnemyIndex;
			if (i >= 0 && i < enemies.Count && enemies[i].IsAlive && enemies[i].lastDiceResult != null)
			{
				var defense = DefenseCalculator.Evaluate(values, enemies[i].lastDiceResult);
				if (defense.blocked)
					damagePreviewText.text = $"<color=#55FF55>{enemies[i].name}: 완벽 방어!</color>";
				else if (enemies[i].rank >= 4 && defense.reductionRate > 0f)
					damagePreviewText.text = $"<color=#FFAA44>{enemies[i].name}: {Mathf.RoundToInt(defense.reductionRate * 100)}% 방어</color>";
				else
					damagePreviewText.text = $"<color=#FF6666>{enemies[i].name}: 방어 실패</color>";
			}
			else
			{
				damagePreviewText.text = "";
			}
		}
		else
		{
			var (damage, comboName, _, _) = DamageCalculator.Calculate(values, GameSessionManager.PowerUps);
			if (!string.IsNullOrEmpty(comboName))
				damagePreviewText.text = $"예상: {comboName} → {damage}";
			else
				damagePreviewText.text = $"예상: {damage}";
		}
	}

	void UpdatePlayerHUD()
	{
		if (heartDisplay != null)
			heartDisplay.Refresh(GameSessionManager.PlayerHearts);
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

	/// <summary>DamageCalculator용 5개 배열 패딩.</summary>
	static int[] PadToFive(int[] values)
	{
		if (values.Length >= 5)
			return values;
		int[] padded = new int[5];
		for (int i = 0; i < values.Length; i++)
			padded[i] = values[i];
		return padded;
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
		if (enemyDiceFaceContainers[enemyIndex] != null)
			enemyDiceFaceContainers[enemyIndex].SetActive(false);
	}
}
