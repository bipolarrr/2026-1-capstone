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
	[SerializeField] Sprite[] mobSprites;
	[SerializeField] Sprite bossSprite;
	[SerializeField] Button fightButton;
	[SerializeField] Button fleeButton;

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

	static readonly EventType[] EventSequence =
	{
		EventType.NormalCombat,
		EventType.ItemBox,
		EventType.BossCombat
	};

	enum EventType { NormalCombat, ItemBox, BossCombat }

	ExploreState state;
	float walkTimer;
	Vector3 playerBasePos;

	static readonly string[] MobNames = { "슬라임", "고블린", "박쥐", "해골" };
	static readonly Color[] MobColors =
	{
		new Color(0.60f, 0.90f, 0.60f),
		new Color(0.95f, 0.75f, 0.50f),
		new Color(0.75f, 0.60f, 0.90f),
		new Color(0.85f, 0.85f, 0.85f)
	};

	// 몹별 바디 앵커: (yMin, yMax) — 슬롯 내 상대 좌표, 0이 바닥(초록 땅)
	// 0=슬라임(작고 바닥), 1=고블린(중간 바닥), 2=박쥐(공중 부유), 3=해골(고블린급 바닥)
	static readonly (float yMin, float yMax)[] MobBodyAnchors =
	{
		(0.00f, 0.40f),  // 슬라임: 납작, 바닥 밀착
		(0.00f, 0.75f),  // 고블린: 중간 키, 바닥
		(0.30f, 0.80f),  // 박쥐: 공중에 떠 있음
		(0.00f, 0.75f),  // 해골: 고블린과 비슷, 바닥
	};

	int activeEnemyCount;

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

		Debug.Log($"[Explore] Start lastBattle={GameSessionManager.LastBattleResult} eventIdx={GameSessionManager.CurrentEventIndex} hearts={GameSessionManager.PlayerHearts.TotalHalfHearts}");

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
		int idx = GameSessionManager.CurrentEventIndex;

		if (idx >= EventSequence.Length)
		{
			ShowVictory();
			return;
		}

		ShowCurrentEncounter();
	}

	void ShowCurrentEncounter()
	{
		state = ExploreState.Encounter;
		int idx = GameSessionManager.CurrentEventIndex;
		if (idx >= EventSequence.Length)
		{
			ShowVictory();
			return;
		}

		EventType evt = EventSequence[idx];
		Debug.Log($"[Explore] ShowCurrentEncounter eventIdx={idx} type={evt}");

		switch (evt)
		{
			case EventType.NormalCombat:
				SetupCombatEncounter(false);
				break;
			case EventType.ItemBox:
				ShowPanel(encounterPanel, true);
				SetupItemEncounter();
				break;
			case EventType.BossCombat:
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

		if (isBoss)
		{
			encounterTitle.text = "보스 등장!";
			if (GameSessionManager.BattleEnemies.Count == 0)
			{
				GameSessionManager.BattleEnemies.Clear();
				GameSessionManager.BattleEnemies.Add(
					new EnemyInfo("어둠의 지배자", GameSessionManager.BossHp, 5,
						new Color(0.95f, 0.45f, 0.45f), bossSprite));
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

	// ── 몹 밸런스 (하트 5개 = 10반칸 기준) ──
	// rank × 1반칸 = 기본 데미지, 족보 시 배율 증가
	// 슬라임 rank1=1반칸, 박쥐 rank3=3반칸 → 고랭크 먼저 잡는 것이 핵심
	static readonly (int hpMin, int hpMax, int rank)[] MobStatPool =
	{
		(30, 40, 1),   // 슬라임: 탱커 — HP 높고 rank 낮음
		(18, 25, 2),   // 고블린: 밸런스
		(10, 15, 3),   // 박쥐: 딜러 — HP 낮지만 rank 높음
		(22, 30, 2),   // 해골: 서브탱커
	};

	void GenerateNormalEnemies()
	{
		GameSessionManager.BattleEnemies.Clear();
		int count = Random.Range(2, 5);
		for (int i = 0; i < count; i++)
		{
			var stat = MobStatPool[i];
			int hp = Random.Range(stat.hpMin, stat.hpMax + 1);
			Sprite spr = (mobSprites != null && i < mobSprites.Length) ? mobSprites[i] : null;
			GameSessionManager.BattleEnemies.Add(
				new EnemyInfo(MobNames[i], hp, stat.rank, MobColors[i], spr));
		}
		Debug.Log($"[Explore] GenerateNormalEnemies count={count} stats=[{string.Join(",", GameSessionManager.BattleEnemies.ConvertAll(e => $"{e.name}(hp{e.hp} rank{e.rank})"))}]");
	}

	/// <summary>싸운다 버튼 — 전투 컨텍스트만 설정하고 BattleScene으로 진입</summary>
	public void OnFightClicked()
	{
		int idx = GameSessionManager.CurrentEventIndex;
		GameSessionManager.IsBossBattle = EventSequence[idx] == EventType.BossCombat;
		Debug.Log($"[Explore] OnFightClicked boss={GameSessionManager.IsBossBattle} enemies={GameSessionManager.BattleEnemies.Count}");
		SceneManager.LoadScene("GameBattleScene");
	}

	/// <summary>도망 버튼 — 조우를 회피하고 다시 걷기 (같은 이벤트 재조우)</summary>
	public void OnFleeClicked()
	{
		Debug.Log("[Explore] OnFleeClicked → 도망, 다시 걷기");
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
					int mobIdx = Mathf.Clamp(i, 0, MobBodyAnchors.Length - 1);
					bodyRt.anchorMin = new Vector2(0.05f, MobBodyAnchors[mobIdx].yMin);
					bodyRt.anchorMax = new Vector2(0.95f, MobBodyAnchors[mobIdx].yMax);
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
}
