using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;

public class GameExploreController : MonoBehaviour
{
	// ── HUD ──
	[SerializeField] HeartDisplay heartDisplay;
	[SerializeField] TMP_Text powerUpText;

	// ── 이동 연출 ──
	[SerializeField] Image playerBody;
	[SerializeField] TMP_Text playerNameText;
	[SerializeField] RectTransform scrollingRoot;

	// ── 조우 패널 ──
	[SerializeField] CanvasGroup encounterPanel;
	[SerializeField] TMP_Text encounterTitle;
	[SerializeField] TMP_Text itemEncounterTitle;

	// ── 전투 조우 ──
	[SerializeField] CanvasGroup combatGroup;
	[SerializeField] GameObject[] enemySlots;
	[SerializeField] Image[] enemyBodies;
	[SerializeField] TMP_Text[] enemyNames;
	[SerializeField] Image[] enemyHpFills;
	[SerializeField] TMP_Text[] enemyHpTexts;
	[SerializeField] Button fightButton;
	[SerializeField] Button fleeButton;

	// ── 스테이지별 스프라이트 번들 (씬 빌더가 편집 시점에 채움) ──
	[SerializeField] StageSpriteBundle[] stageBundles;
	[SerializeField] Image backgroundImage; // 스테이지 전환 시 배경 교체용

	// ── 아이템 조우 ──
	[SerializeField] CanvasGroup itemGroup;
	[SerializeField] Button[] itemButtons;
	[SerializeField] TMP_Text[] itemTitles;
	[SerializeField] TMP_Text[] itemDescs;

	// ── 승리 패널 ──
	[SerializeField] CanvasGroup victoryPanel;

	const float WalkDuration = 2.5f;
	const float ScrollSpeed = 120f;
	const float BobAmplitude = 6f;
	const float BobSpeed = 3f;
	const float EnemyBobAmplitude = 3f;
	const float EnemyBobBaseSpeed = 2.2f;

	enum ExploreState { Walking, Encounter }

	ExploreState state;
	float walkTimer;
	Vector3 playerBasePos;

	int activeEnemyCount;

	StageData ActiveStage => GameSessionManager.CurrentStage;

	StageSpriteBundle FindBundle(string stageId)
	{
		if (stageBundles == null) return null;
		for (int i = 0; i < stageBundles.Length; i++)
			if (stageBundles[i] != null && stageBundles[i].stageId == stageId)
				return stageBundles[i];
		return null;
	}

	void ApplyStageVisuals()
	{
		var stage = ActiveStage;
		if (stage == null || backgroundImage == null) return;
		var bundle = FindBundle(stage.id);
		if (bundle != null && bundle.background != null)
		{
			backgroundImage.sprite = bundle.background;
			backgroundImage.color  = Color.white;
		}
		else
		{
			backgroundImage.sprite = null;
			backgroundImage.color  = stage.themeColor;
		}

		ApplyPlayerGroundOffset();
	}

	bool playerAnchorShifted;

	void ApplyPlayerGroundOffset()
	{
		if (playerAnchorShifted || playerBody == null) return;
		var stage = ActiveStage;
		if (stage == null) return;
		float delta = stage.playerGroundYOffset;
		if (Mathf.Abs(delta) < 0.0001f) { playerAnchorShifted = true; return; }
		var rt = playerBody.rectTransform;
		rt.anchorMin = new Vector2(rt.anchorMin.x, rt.anchorMin.y + delta);
		rt.anchorMax = new Vector2(rt.anchorMax.x, rt.anchorMax.y + delta);
		playerAnchorShifted = true;
	}

	static void ShowPanel(CanvasGroup cg, bool show)
	{
		cg.alpha = show ? 1f : 0f;
		cg.blocksRaycasts = show;
		cg.interactable = show;
	}

	void Start()
	{
		// StartNewGame를 거치지 않고 직접 씬을 열었거나, 신규 게임 시작 시 HP 보장
		if (!GameSessionManager.IsPlayerAlive
			|| (GameSessionManager.CurrentEventIndex == 0 && GameSessionManager.LastBattleResult == BattleResult.None))
			GameSessionManager.PlayerHearts.Reset();

		ShowPanel(encounterPanel, false);
		ShowPanel(combatGroup, false);
		ShowPanel(itemGroup, false);
		ShowPanel(victoryPanel, false);

		playerBasePos = playerBody.rectTransform.anchoredPosition;
		UpdateHUD();
		ApplyStageVisuals();

		Debug.Log($"[Explore] Start stage={GameSessionManager.CurrentStageId} lastBattle={GameSessionManager.LastBattleResult} eventIdx={GameSessionManager.CurrentEventIndex} hearts={GameSessionManager.PlayerHearts.TotalHalfHearts}");

		// CurrentEventIndex 증가는 이 씬에서 일원화: 전투 승리, 아이템 선택 모두 여기서 처리
		switch (GameSessionManager.LastBattleResult)
		{
			case BattleResult.Won:
				GameSessionManager.LastBattleResult = BattleResult.None;
				GameSessionManager.CurrentEventIndex++;
				StartWalking();
				break;
			case BattleResult.Cancelled:
				GameSessionManager.LastBattleResult = BattleResult.None;
				ShowCurrentEncounter();
				break;
			default:
				StartWalking();
				break;
		}
	}

	void Update()
	{
		// 적 바운스: 조우 중이면 항상 적용
		if (state == ExploreState.Encounter)
			BobEnemies();

		if (state != ExploreState.Walking)
			return;

		walkTimer += Time.deltaTime;

		if (scrollingRoot != null)
		{
			Vector2 pos = scrollingRoot.anchoredPosition;
			pos.x -= ScrollSpeed * Time.deltaTime;
			if (pos.x < -600f)
				pos.x += 600f;
			scrollingRoot.anchoredPosition = pos;
		}

		Vector2 p = playerBasePos;
		p.y += Mathf.Sin(Time.time * BobSpeed) * BobAmplitude;
		playerBody.rectTransform.anchoredPosition = p;

		if (walkTimer >= WalkDuration)
			TriggerNextEvent();
	}

	void BobEnemies()
	{
		for (int i = 0; i < activeEnemyCount; i++)
		{
			if (enemyBodies[i] == null)
				continue;
			float speed = EnemyBobBaseSpeed + i * 0.3f;
			float phase = i * 1.2f;
			float offset = Mathf.Sin(Time.time * speed + phase) * EnemyBobAmplitude;
			var rt = enemyBodies[i].rectTransform;
			rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, offset);
		}
	}

	void StartWalking()
	{
		state = ExploreState.Walking;
		walkTimer = 0f;
		ShowPanel(encounterPanel, false);
		ShowPanel(combatGroup, false);
	}

	void TriggerNextEvent()
	{
		state = ExploreState.Encounter;
		var stage = ActiveStage;
		int idx = GameSessionManager.CurrentEventIndex;

		if (stage == null || stage.rounds == null || idx >= stage.rounds.Count)
		{
			ShowVictory();
			return;
		}

		ShowCurrentEncounter();
	}

	void ShowCurrentEncounter()
	{
		state = ExploreState.Encounter;
		var stage = ActiveStage;
		int idx = GameSessionManager.CurrentEventIndex;
		if (stage == null || stage.rounds == null || idx >= stage.rounds.Count)
		{
			ShowVictory();
			return;
		}

		StageRoundType evt = stage.rounds[idx];
		Debug.Log($"[Explore] ShowCurrentEncounter stage={stage.id} roundIdx={idx} type={evt}");

		switch (evt)
		{
			case StageRoundType.NormalCombat:
				SetupCombatEncounter(false);
				break;
			case StageRoundType.ItemBox:
				ShowPanel(encounterPanel, true);
				SetupItemEncounter();
				break;
			case StageRoundType.BossCombat:
				SetupCombatEncounter(true);
				break;
		}
	}

	bool currentEncounterIsBoss;

	void SetupCombatEncounter(bool isBoss)
	{
		currentEncounterIsBoss = isBoss;
		ShowPanel(combatGroup, true);
		ShowPanel(itemGroup, false);

		var stage = ActiveStage;
		var bundle = stage != null ? FindBundle(stage.id) : null;

		if (isBoss)
		{
			encounterTitle.text = "보스 등장!";
			if (GameSessionManager.BattleEnemies.Count == 0 && stage != null && stage.boss != null)
			{
				Sprite bossSpr = bundle != null ? bundle.bossSprite : null;
				GameSessionManager.BattleEnemies.Clear();
				GameSessionManager.BattleEnemies.Add(
					new EnemyInfo(stage.boss.name, stage.boss.hp, stage.boss.rank,
						stage.boss.themeColor, bossSpr));
			}
		}
		else
		{
			encounterTitle.text = "적을 만났다!";
			if (GameSessionManager.BattleEnemies.Count == 0)
				GenerateNormalEnemies();
		}

		RefreshEnemySlots();
	}

	void GenerateNormalEnemies()
	{
		GameSessionManager.BattleEnemies.Clear();
		var stage = ActiveStage;
		if (stage == null || stage.mobPool == null || stage.mobPool.Count == 0)
		{
			Debug.LogWarning("[Explore] GenerateNormalEnemies: 활성 스테이지에 mobPool이 비어있음");
			return;
		}

		var bundle = FindBundle(stage.id);

		int countMin = Mathf.Max(1, stage.normalEnemyCountMin);
		int countMaxExclusive = Mathf.Max(countMin + 1, stage.normalEnemyCountMax + 1);
		int count = Random.Range(countMin, countMaxExclusive);
		count = Mathf.Min(count, stage.mobPool.Count); // 풀 크기보다 많이 뽑지 않음
		count = Mathf.Min(count, enemySlots != null ? enemySlots.Length : count);

		// 풀에서 중복 없이 count개 인덱스 추첨
		var available = new List<int>(stage.mobPool.Count);
		for (int k = 0; k < stage.mobPool.Count; k++) available.Add(k);

		for (int i = 0; i < count; i++)
		{
			int pick = Random.Range(0, available.Count);
			int idx = available[pick];
			available.RemoveAt(pick);

			var def = stage.mobPool[idx];
			int hp = Random.Range(def.hpMin, def.hpMax + 1);
			Sprite spr = (bundle != null && bundle.mobSprites != null && idx < bundle.mobSprites.Length)
				? bundle.mobSprites[idx]
				: null;
			GameSessionManager.BattleEnemies.Add(
				new EnemyInfo(def.name, hp, def.rank, def.themeColor, spr));
		}
		Debug.Log($"[Explore] GenerateNormalEnemies stage={stage.id} count={count} [{string.Join(",", GameSessionManager.BattleEnemies.ConvertAll(e => $"{e.name}(hp{e.hp} rank{e.rank})"))}]");
	}

	/// <summary>싸운다 버튼 — 전투 컨텍스트만 설정하고 BattleScene으로 진입</summary>
	public void OnFightClicked()
	{
		var stage = ActiveStage;
		int idx = GameSessionManager.CurrentEventIndex;
		GameSessionManager.IsBossBattle = stage != null && stage.rounds != null
			&& idx >= 0 && idx < stage.rounds.Count
			&& stage.rounds[idx] == StageRoundType.BossCombat;
		Debug.Log($"[Explore] OnFightClicked stage={stage?.id} boss={GameSessionManager.IsBossBattle} enemies={GameSessionManager.BattleEnemies.Count}");
		AudioManager.Play("UI_OK");
		AudioManager.Play("Transition_3");
		AudioManager.Play("Environment_Desert");
		SceneManager.LoadScene(ResolveBattleSceneName(GameSessionManager.SelectedCharacter));
	}

	/// <summary>도망 버튼 — 조우를 회피하고 다시 걷기 (같은 이벤트 재조우)</summary>
	public void OnFleeClicked()
	{
		Debug.Log("[Explore] OnFleeClicked → 도망, 다시 걷기");
		AudioManager.Play("UI_Back_NO");
		ShowPanel(combatGroup, false);
		StartWalking();
	}

	void RefreshEnemySlots()
	{
		var enemies = GameSessionManager.BattleEnemies;
		int count = Mathf.Min(enemies.Count, enemySlots.Length);
		activeEnemyCount = count;
		float slotWidth = 0.24f;
		float gap = 0.01f;
		float totalWidth = count * slotWidth + Mathf.Max(0, count - 1) * gap;
		float startX = (1f - totalWidth) / 2f;

		for (int i = 0; i < enemySlots.Length; i++)
		{
			if (i < count)
			{
				enemySlots[i].SetActive(true);
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
				enemyNames[i].transform.parent.gameObject.SetActive(false);
				enemyHpFills[i].fillAmount = (float)enemies[i].hp / enemies[i].maxHp;
				enemyHpTexts[i].text = $"HP {enemies[i].hp}  <color=#FFD94A>{enemies[i].RankStars}</color>";

				// 슬롯 배치
				var rt = enemySlots[i].GetComponent<RectTransform>();
				float x0 = startX + i * (slotWidth + gap);
				rt.anchorMin = new Vector2(x0, 0f);
				rt.anchorMax = new Vector2(x0 + slotWidth, 1f);
				rt.offsetMin = Vector2.zero;
				rt.offsetMax = Vector2.zero;

				// 몹별 바디 크기/높이 차등 적용 (보스는 기본 대형)
				var bodyRt = enemyBodies[i].rectTransform;
				if (currentEncounterIsBoss)
				{
					bodyRt.anchorMin = new Vector2(0.05f, 0.00f);
					bodyRt.anchorMax = new Vector2(0.95f, 0.90f);
					bodyRt.localScale = new Vector3(-1f, 1f, 1f);
				}
				else
				{
					// 스테이지 mobPool에서 이름으로 앵커 조회
					float yMin = 0.00f, yMax = 0.75f;
					var stage = ActiveStage;
					if (stage != null)
					{
						var def = stage.FindMob(enemies[i].name);
						if (def != null) { yMin = def.bodyYMin; yMax = def.bodyYMax; }
					}
					bodyRt.anchorMin = new Vector2(0.05f, yMin);
					bodyRt.anchorMax = new Vector2(0.95f, yMax);
					bodyRt.localScale = Vector3.one;
				}
				bodyRt.offsetMin = Vector2.zero;
				bodyRt.offsetMax = Vector2.zero;
			}
			else
			{
				enemySlots[i].SetActive(false);
			}
		}
	}

	void SetupItemEncounter()
	{
		ShowPanel(combatGroup, false);
		ShowPanel(itemGroup, true);
		itemEncounterTitle.text = "아이템 상자!";

		string[] titles =
		{
			"홀짝 특화",
			"올인 전략",
			"부활의 부적"
		};
		string[] descs =
		{
			"홀수 눈만 또는 짝수 눈만으로\n점수를 내면 데미지 2배",
			"족보 데미지 절반,\n족보 아닐 시 데미지 2배",
			"사망에 이르는 데미지를\n1회 무효화 (일회성)"
		};
		PowerUpType[] types =
		{
			PowerUpType.OddEvenDouble,
			PowerUpType.AllOrNothing,
			PowerUpType.ReviveOnce
		};

		for (int i = 0; i < itemButtons.Length; i++)
		{
			itemTitles[i].text = titles[i];
			itemDescs[i].text = descs[i];

			int captured = i;
			itemButtons[i].onClick.RemoveAllListeners();
			itemButtons[i].onClick.AddListener(() => OnItemSelected(types[captured]));
		}
	}

	void OnItemSelected(PowerUpType type)
	{
		if (!GameSessionManager.HasPowerUp(type))
			GameSessionManager.PowerUps.Add(type);

		Debug.Log($"[Explore] OnItemSelected type={type} powerUps=[{string.Join(",", GameSessionManager.PowerUps)}]");
		AudioManager.Play("UI_Purchase_OK_LockIn");
		AudioManager.Play("Player_EarnDrop");
		GameSessionManager.CurrentEventIndex++;
		UpdateHUD();
		StartWalking();
	}

	void ShowVictory()
	{
		Debug.Log($"[Explore] ShowVictory hearts={GameSessionManager.PlayerHearts.TotalHalfHearts} powerUps={GameSessionManager.PowerUps.Count}");
		state = ExploreState.Encounter;
		ShowPanel(encounterPanel, false);
		ShowPanel(victoryPanel, true);
	}

	public void OnReturnToMainMenu()
	{
		Debug.Log("[Explore] OnReturnToMainMenu → MainMenu");
		AudioManager.Play("Transition_2_Quit");
		SceneManager.LoadScene("MainMenu");
	}

	void UpdateHUD()
	{
		if (heartDisplay != null)
			heartDisplay.Refresh(GameSessionManager.PlayerHearts);

		var pups = GameSessionManager.PowerUps;
		if (pups.Count == 0)
		{
			powerUpText.text = "";
		}
		else
		{
			var sb = new System.Text.StringBuilder();
			foreach (var p in pups)
			{
				if (sb.Length > 0)
					sb.Append("  ");
				sb.Append(PowerUpDisplayName(p));
			}
			powerUpText.text = sb.ToString();
		}
	}

	static string PowerUpDisplayName(PowerUpType type)
	{
		switch (type)
		{
			case PowerUpType.OddEvenDouble: return "[홀짝 특화]";
			case PowerUpType.AllOrNothing:  return "[올인 전략]";
			case PowerUpType.ReviveOnce:    return "[부활 부적]";
			default: return type.ToString();
		}
	}

	static string ResolveBattleSceneName(CharacterType character)
	{
		switch (character)
		{
			case CharacterType.Mahjong: return "MahjongBattleScene";
			case CharacterType.Holdem:  return "DiceBattleScene"; // 홀덤 씬 미구현 → 임시로 주사위
			default:                    return "DiceBattleScene";
		}
	}
}
