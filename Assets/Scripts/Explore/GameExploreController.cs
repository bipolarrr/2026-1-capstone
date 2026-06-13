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
	[SerializeField] Image[] enemyIdleProjectiles;
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

	// ── 노드 지도 ──
	[SerializeField] CanvasGroup mapGraphGroup;
	[SerializeField] TMP_Text mapTitle;
	[SerializeField] TMP_Text mapProgressText;
	[SerializeField] ScrollRect mapScrollRect;
	[SerializeField] RectTransform mapViewport;
	[SerializeField] RectTransform mapContent;
	[SerializeField] RectTransform mapNodeRoot;
	[SerializeField] RectTransform mapConnectionRoot;
	[SerializeField] RectTransform playerMapMarker;
	[SerializeField] RectTransform[] mapNodeRects;
	[SerializeField] ExploreMapNodeDisplay[] mapNodeViews;
	[SerializeField] Graphic[] mapNodeGraphics;
	[SerializeField] Button[] mapNodeButtons;
	[SerializeField] TMP_Text[] mapNodeIconLabels;
	[SerializeField] Image[] mapNodeIconImages;
	[SerializeField] ExploreMapNodeHoverEffect[] mapNodeHoverEffects;
	[SerializeField] TMP_Text[] mapNodeTitles;
	[SerializeField] TMP_Text[] mapNodeDescs;
	[SerializeField] Image[] mapConnectionLines;
	[SerializeField] ExploreMapConnectionLineDisplay[] mapConnectionLineViews;
	[SerializeField] ExploreMapMarkerDisplay playerMapMarkerView;
	[SerializeField] Sprite mapBossIconSprite;
	[SerializeField] Sprite mapShopIconSprite;
	[SerializeField] Sprite mapHealIconSprite;
	[SerializeField] Sprite mapCombatIconSprite;

	const float WalkDuration = 2.5f;
	const float MapTransitionDuration = 0.18f;
	const float ScrollSpeed = 120f;
	const float BobAmplitude = 6f;
	const float BobSpeed = 3f;
	const float EnemyBobAmplitude = 3f;
	const float EnemyBobBaseSpeed = 2.2f;
	const string StartMapNodeId = "r0l1";
	static readonly Vector2 PlayerLocationMarkerSize = new Vector2(54f, 50f);
	static readonly Vector2 PlayerLocationMarkerOffset = new Vector2(0f, 52f);

	enum ExploreState { Walking, Map, Encounter }

	ExploreState state;
	float walkTimer;
	Vector3 playerBasePos;

	int activeEnemyCount;
	bool isMapMode;
	bool hasSelectedMapNode;
	bool hasActiveMapPresentation;
	ExploreMapNodeKind activeEncounterKind;
	ExploreMapPresentation activeMapPresentation;
	ExploreMapLayoutResult activeMapLayout;
	Coroutine mapTransitionRoutine;

	StageData ActiveStage => GameSessionManager.CurrentStage;

	StageSpriteBundle FindBundle(string stageId)
	{
		if (stageBundles == null) return null;
		for (int i = 0; i < stageBundles.Length; i++)
			if (stageBundles[i] != null && stageBundles[i].stageId == stageId)
				return stageBundles[i];
		return null;
	}

	Sprite ResolveEnemyProjectileSprite(string mobName)
	{
		var stage = ActiveStage;
		if (stage == null) return null;
		var bundle = FindBundle(stage.id);
		int mobIndex = stage.IndexOfMob(mobName);
		if (bundle == null || bundle.mobProjectileSprites == null)
			return null;
		if (mobIndex < 0 || mobIndex >= bundle.mobProjectileSprites.Length)
			return null;
		return bundle.mobProjectileSprites[mobIndex];
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
		if (cg == null)
			return;

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

		EnsureExploreMapRouteState(
			GameSessionManager.CurrentEventIndex == 0
			&& GameSessionManager.LastBattleResult == BattleResult.None);

		ShowPanel(encounterPanel, false);
		ShowPanel(combatGroup, false);
		ShowPanel(itemGroup, false);
		ShowPanel(victoryPanel, false);
		SetMapDisplayVisible(false, false);

		playerBasePos = playerBody.rectTransform.anchoredPosition;
		UpdateHUD();
		ApplyStageVisuals();

		Debug.Log($"[Explore] Start stage={GameSessionManager.CurrentStageId} lastBattle={GameSessionManager.LastBattleResult} eventIdx={GameSessionManager.CurrentEventIndex} hearts={GameSessionManager.PlayerHearts.TotalHalfHearts}");

		// CurrentEventIndex 증가는 이 씬에서 일원화: 전투 승리, 아이템 선택 모두 여기서 처리
		switch (GameSessionManager.LastBattleResult)
		{
			case BattleResult.Won:
				GameSessionManager.ResetBattleResult();
				CompleteSelectedEncounterProgress();
				ShowMapMode();
				break;
			case BattleResult.Cancelled:
				GameSessionManager.ResetBattleResult();
				ShowPendingEncounterOrMap();
				break;
			default:
				ShowMapMode();
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
		isMapMode = false;
		state = ExploreState.Walking;
		walkTimer = 0f;
		ShowPanel(encounterPanel, false);
		ShowPanel(combatGroup, false);
		ShowPanel(itemGroup, false);
		SetMapDisplayVisible(false, true);
	}

	void ShowMapMode()
	{
		EnsureExploreMapRouteState(false);
		if (IsStageComplete())
		{
			ShowVictory();
			return;
		}

		if (!HasMapDisplayWiring())
		{
			StartWalking();
			return;
		}

		isMapMode = true;
		hasSelectedMapNode = false;
		state = ExploreState.Map;
		walkTimer = 0f;
		activeEnemyCount = 0;
		ShowPanel(encounterPanel, false);
		ShowPanel(combatGroup, false);
		ShowPanel(itemGroup, false);
		ShowPanel(victoryPanel, false);
		GameSessionManager.ClearBattleEnemies();
		GameSessionManager.IsBossBattle = false;

		if (!RefreshMapDisplay())
		{
			StartWalking();
			return;
		}

		SetMapDisplayVisible(true, true);
	}

	void TriggerNextEvent()
	{
		isMapMode = false;
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
		var stage = ActiveStage;
		int idx = GameSessionManager.CurrentEventIndex;
		if (stage == null || stage.rounds == null || idx >= stage.rounds.Count)
		{
			ShowVictory();
			return;
		}

		StageRoundType evt = stage.rounds[idx];
		Debug.Log($"[Explore] ShowCurrentEncounter stage={stage.id} roundIdx={idx} type={evt}");
		ShowEncounterMode(MapKindFromRoundType(evt), "");
	}

	void ShowPendingEncounterOrMap()
	{
		if (TryResolvePendingMapNode(out var node))
		{
			ShowEncounterMode(node.Kind, node.NodeId);
			return;
		}

		ShowCurrentEncounter();
	}

	void ShowEncounterMode(ExploreMapNodeKind nodeKind, string nodeId)
	{
		isMapMode = false;
		state = ExploreState.Encounter;
		hasSelectedMapNode = !string.IsNullOrEmpty(nodeId);
		activeEncounterKind = nodeKind;
		SetMapDisplayVisible(false, true);
		ShowPanel(victoryPanel, false);
		ShowPanel(encounterPanel, false);
		ShowPanel(combatGroup, false);
		ShowPanel(itemGroup, false);

		if (IsStageFinaleBossEncounter(nodeKind))
		{
			ShowStageFinale();
			return;
		}

		switch (nodeKind)
		{
			case ExploreMapNodeKind.Combat:
				SetupCombatEncounter(false);
				break;
			case ExploreMapNodeKind.Boss:
				SetupCombatEncounter(true);
				break;
			case ExploreMapNodeKind.Heal:
				CompleteHealEncounter();
				break;
			case ExploreMapNodeKind.Shop:
			case ExploreMapNodeKind.Loot:
				ShowPanel(encounterPanel, true);
				SetupItemEncounter(nodeKind);
				break;
			default:
				ShowMapMode();
				break;
		}
	}

	void CompleteHealEncounter()
	{
		GameSessionManager.PlayerHearts.HealRed(4);
		Debug.Log($"[Explore] CompleteHealEncounter hearts={GameSessionManager.PlayerHearts.TotalHalfHearts}");
		AudioManager.Play("Player_EarnDrop");
		CompleteSelectedEncounterProgress();
		UpdateHUD();
		ShowMapMode();
	}

	bool IsStageFinaleBossEncounter(ExploreMapNodeKind nodeKind)
	{
		if (nodeKind != ExploreMapNodeKind.Boss)
			return false;

		var stage = ActiveStage;
		int idx = GameSessionManager.CurrentEventIndex;
		if (stage == null
			|| stage.id != Stage2Cave.Id
			|| stage.rounds == null
			|| idx < 0
			|| idx >= stage.rounds.Count
			|| idx != stage.rounds.Count - 1
			|| stage.rounds[idx] != StageRoundType.BossCombat)
			return false;

		return !StageRegistry.TryGetNextStage(stage.id, out _);
	}

	void ShowStageFinale()
	{
		Debug.Log($"[Explore] ShowStageFinale stage={GameSessionManager.CurrentStageId} roundIdx={GameSessionManager.CurrentEventIndex}");
		CompleteSelectedEncounterProgress();
		UpdateHUD();
		ShowVictory();
	}

	bool CompleteSelectedEncounterProgress()
	{
		string completedStageId = GameSessionManager.CurrentStageId;
		bool completedBossEncounter = IsCurrentEncounterBoss();
		CommitPendingMapSelection();
		GameSessionManager.CurrentEventIndex++;
		GameSessionManager.PendingExploreMapNodeId = "";
		GameSessionManager.ClearBattleEnemies();
		GameSessionManager.IsBossBattle = false;
		hasSelectedMapNode = false;

		if (completedBossEncounter
			&& IsStageComplete()
			&& StageRegistry.TryGetNextStage(completedStageId, out var nextStage))
		{
			StartNextStage(nextStage);
			return true;
		}

		return false;
	}

	bool IsCurrentEncounterBoss()
	{
		if (TryResolvePendingMapNode(out var pendingNode))
			return pendingNode.Kind == ExploreMapNodeKind.Boss;

		var stage = ActiveStage;
		int idx = GameSessionManager.CurrentEventIndex;
		return stage != null
			&& stage.rounds != null
			&& idx >= 0
			&& idx < stage.rounds.Count
			&& stage.rounds[idx] == StageRoundType.BossCombat;
	}

	void StartNextStage(StageData nextStage)
	{
		if (nextStage == null || string.IsNullOrEmpty(nextStage.id))
			return;

		string previousStageId = GameSessionManager.CurrentStageId;
		GameSessionManager.CurrentStageId = nextStage.id;
		GameSessionManager.CurrentEventIndex = 0;
		GameSessionManager.ResetExploreMapRoute();
		GameSessionManager.ClearBattleEnemies();
		GameSessionManager.IsBossBattle = false;
		activeEnemyCount = 0;
		activeEncounterKind = ExploreMapNodeKind.Start;
		hasSelectedMapNode = false;
		hasActiveMapPresentation = false;
		playerAnchorShifted = false;
		ApplyStageVisuals();
		Debug.Log($"[Explore] Stage transition {previousStageId} -> {nextStage.id}");
	}

	void CommitPendingMapSelection()
	{
		if (string.IsNullOrEmpty(GameSessionManager.PendingExploreMapNodeId))
			return;

		GameSessionManager.CurrentExploreMapNodeId = GameSessionManager.PendingExploreMapNodeId;
		GameSessionManager.PendingExploreMapNodeId = "";
	}

	bool TryResolvePendingMapNode(out ExploreMapNodeView node)
	{
		node = default;
		string pendingNodeId = GameSessionManager.PendingExploreMapNodeId;
		if (string.IsNullOrEmpty(pendingNodeId))
			return false;

		if (!TryBuildMapPresentationForCurrentRoute(out var presentation))
			return false;

		int nodeIndex = presentation.FindNodeIndex(pendingNodeId);
		if (nodeIndex < 0)
			return false;

		node = presentation.GetNode(nodeIndex);
		return node.IsSelectable || node.IsReachable;
	}

	void EnsureExploreMapRouteState(bool resetRoute)
	{
		if (resetRoute)
		{
			GameSessionManager.CurrentExploreMapNodeId = StartMapNodeId;
			GameSessionManager.PendingExploreMapNodeId = "";
		}

		if (string.IsNullOrEmpty(GameSessionManager.CurrentExploreMapNodeId))
			GameSessionManager.CurrentExploreMapNodeId = ResolveCurrentMapNodeId();
	}

	bool IsStageComplete()
	{
		var stage = ActiveStage;
		return stage == null
			|| stage.rounds == null
			|| GameSessionManager.CurrentEventIndex >= stage.rounds.Count;
	}

	static ExploreMapNodeKind MapKindFromRoundType(StageRoundType roundType)
	{
		switch (roundType)
		{
			case StageRoundType.NormalCombat:
				return ExploreMapNodeKind.Combat;
			case StageRoundType.BossCombat:
				return ExploreMapNodeKind.Boss;
			default:
				return ExploreMapNodeKind.Loot;
		}
	}

	static string ResolveItemEncounterTitle(ExploreMapNodeKind nodeKind)
	{
		switch (nodeKind)
		{
			case ExploreMapNodeKind.Shop:
				return "보급 선택";
			case ExploreMapNodeKind.Loot:
				return "아이템 상자!";
			default:
				return "보급 선택";
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
			if (!GameSessionManager.HasBattleEnemies && stage != null && stage.boss != null)
			{
				Sprite bossSpr = bundle != null ? bundle.bossSprite : null;
				GameSessionManager.PrepareBattleEnemy(
					new EnemyInfo(stage.boss.name, stage.boss.hp, stage.boss.rank,
						stage.boss.themeColor, bossSpr),
					true);
			}
		}
		else
		{
			encounterTitle.text = "적을 만났다!";
			if (!GameSessionManager.HasBattleEnemies)
				GenerateNormalEnemies();
		}

		RefreshEnemySlots();
	}

	void GenerateNormalEnemies()
	{
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

		var generated = new List<EnemyInfo>(count);
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
			generated.Add(new EnemyInfo(def.name, hp, def.rank, def.themeColor, spr));
		}
		GameSessionManager.PrepareBattleEnemies(generated, false);
		Debug.Log($"[Explore] GenerateNormalEnemies stage={stage.id} count={count} [{string.Join(",", generated.ConvertAll(e => $"{e.name}(hp{e.hp} rank{e.rank})"))}]");
	}

	/// <summary>싸운다 버튼 — 전투 컨텍스트만 설정하고 BattleScene으로 진입</summary>
	public void OnFightClicked()
	{
		var stage = ActiveStage;
		int idx = GameSessionManager.CurrentEventIndex;
		if (hasSelectedMapNode
			&& activeEncounterKind != ExploreMapNodeKind.Combat
			&& activeEncounterKind != ExploreMapNodeKind.Boss)
		{
			Debug.LogWarning($"[Explore] OnFightClicked ignored for non-combat map node kind={activeEncounterKind}");
			return;
		}

		bool isBossBattle = hasSelectedMapNode
			? activeEncounterKind == ExploreMapNodeKind.Boss
			: stage != null && stage.rounds != null
				&& idx >= 0 && idx < stage.rounds.Count
				&& stage.rounds[idx] == StageRoundType.BossCombat;
		if (GameSessionManager.IsBossBattle != isBossBattle)
			GameSessionManager.PrepareBattleEnemies(GameSessionManager.SnapshotBattleEnemies(), isBossBattle);
		Debug.Log($"[Explore] OnFightClicked stage={stage?.id} boss={GameSessionManager.IsBossBattle} enemies={GameSessionManager.BattleEnemyCount}");
		AudioManager.Play("UI_OK");
		AudioManager.Play("Transition_3");
		AudioManager.Play("Environment_Desert");
		SceneManager.LoadScene(ResolveBattleSceneName(GameSessionManager.SelectedCharacter));
	}

	/// <summary>도망 버튼 — 조우를 회피하고 다시 걷기 (같은 이벤트 재조우)</summary>
	public void OnFleeClicked()
	{
		Debug.Log("[Explore] OnFleeClicked → 도망, 지도 복귀");
		AudioManager.Play("UI_Back_NO");
		ShowPanel(combatGroup, false);
		GameSessionManager.PendingExploreMapNodeId = "";
		GameSessionManager.ClearBattleEnemies();
		GameSessionManager.IsBossBattle = false;
		ShowMapMode();
	}

	void RefreshEnemySlots()
	{
		var enemies = GameSessionManager.SnapshotBattleEnemies();
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
				ApplyEnemyIdleProjectile(i, enemies[i]);

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
				if (enemyIdleProjectiles != null && i < enemyIdleProjectiles.Length && enemyIdleProjectiles[i] != null)
					enemyIdleProjectiles[i].gameObject.SetActive(false);
			}
		}
	}

	void ApplyEnemyIdleProjectile(int index, EnemyInfo enemy)
	{
		if (enemyIdleProjectiles == null || index < 0 || index >= enemyIdleProjectiles.Length)
			return;
		var projectile = enemyIdleProjectiles[index];
		if (projectile == null)
			return;

		var stage = ActiveStage;
		var def = stage != null ? stage.FindMob(enemy.name) : null;
		Sprite sprite = ResolveEnemyProjectileSprite(enemy.name);
		bool show = !currentEncounterIsBoss && def != null &&
			!string.IsNullOrEmpty(def.projectileSpritePath) && sprite != null;
		projectile.gameObject.SetActive(show);
		if (!show)
			return;

		projectile.sprite = sprite;
		projectile.color = Color.white;
		projectile.preserveAspect = true;
		projectile.raycastTarget = false;
		var rt = projectile.rectTransform;
		rt.anchorMin = new Vector2(-0.02f, 0.48f);
		rt.anchorMax = new Vector2(0.62f, 0.59f);
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
		rt.localRotation = Quaternion.identity;
		rt.localScale = Vector3.one;
	}

	void SetupItemEncounter(ExploreMapNodeKind nodeKind)
	{
		ShowPanel(combatGroup, false);
		ShowPanel(itemGroup, true);
		if (itemEncounterTitle != null)
			itemEncounterTitle.text = ResolveItemEncounterTitle(nodeKind);

		var options = PowerUpRewardCatalog.GetOptionsForEvent(
			GameSessionManager.SelectedCharacter,
			GameSessionManager.CurrentStageId,
			GameSessionManager.CurrentEventIndex);

		int buttonCount = itemButtons != null ? itemButtons.Length : 0;
		for (int i = 0; i < buttonCount; i++)
		{
			var button = itemButtons[i];
			if (button == null)
				continue;

			bool hasOption = i < options.Count
				&& options[i].IsImplemented
				&& options[i].IsSelectable;

			button.onClick.RemoveAllListeners();
			button.interactable = hasOption;
			button.gameObject.SetActive(hasOption);
			SetItemText(itemTitles, i, hasOption ? options[i].Title : "");
			SetItemText(itemDescs, i, hasOption ? options[i].Description : "");

			if (!hasOption)
				continue;

			PowerUpType capturedType = options[i].Type;
			button.onClick.AddListener(() => OnItemSelected(capturedType));
		}
	}

	static void SetItemText(TMP_Text[] texts, int index, string value)
	{
		if (texts == null || index < 0 || index >= texts.Length || texts[index] == null)
			return;
		texts[index].text = value;
	}

	void OnItemSelected(PowerUpType type)
	{
		GameSessionManager.AddPowerUp(type);

		Debug.Log($"[Explore] OnItemSelected type={type} powerUps=[{string.Join(",", GameSessionManager.PowerUps)}]");
		AudioManager.Play("UI_Purchase_OK_LockIn");
		AudioManager.Play("Player_EarnDrop");
		CompleteSelectedEncounterProgress();
		UpdateHUD();
		ShowMapMode();
	}

	void ShowVictory()
	{
		Debug.Log($"[Explore] ShowVictory hearts={GameSessionManager.PlayerHearts.TotalHalfHearts} powerUps={GameSessionManager.PowerUps.Count}");
		isMapMode = false;
		state = ExploreState.Encounter;
		SetMapDisplayVisible(false, true);
		ShowPanel(encounterPanel, false);
		ShowPanel(combatGroup, false);
		ShowPanel(itemGroup, false);
		ShowPanel(victoryPanel, true);
	}

	bool RefreshMapDisplay()
	{
		if (!HasMapDisplayWiring())
		{
			hasActiveMapPresentation = false;
			SetMapDisplayVisible(false, false);
			return false;
		}

		if (!TryBuildMapPresentationForCurrentRoute(out var presentation))
		{
			hasActiveMapPresentation = false;
			SetMapDisplayVisible(false, false);
			return false;
		}

		activeMapPresentation = presentation;
		hasActiveMapPresentation = true;
		if (!string.IsNullOrEmpty(presentation.CurrentNodeId))
			GameSessionManager.CurrentExploreMapNodeId = presentation.CurrentNodeId;
		ApplyMapPresentation(presentation);
		return true;
	}

	bool TryBuildMapPresentationForCurrentRoute(out ExploreMapPresentation presentation)
	{
		var stage = ActiveStage;
		int currentEventIndex = Mathf.Clamp(
			GameSessionManager.CurrentEventIndex,
			0,
			ExploreMapPresentationPolicy.EventCount - 1);
		string stageMapTitle = ResolveStageMapTitle(stage);
		int seed = BuildMapSeed(stage);
		string currentNodeId = ResolveCurrentMapNodeId();
		return ExploreMapPresentationPolicy.TryBuildRandom(stageMapTitle, currentEventIndex, seed, currentNodeId, out presentation)
			|| ExploreMapPresentationPolicy.TryBuild(stageMapTitle, currentEventIndex, currentNodeId, out presentation);
	}

	static string ResolveStageMapTitle(StageData stage)
	{
		if (stage == null)
			return "";
		if (!string.IsNullOrEmpty(stage.mapTitle))
			return stage.mapTitle;
		return stage.displayName ?? "";
	}

	bool HasMapDisplayWiring()
	{
		return mapGraphGroup != null
			&& mapNodeRoot != null
			&& mapConnectionRoot != null
			&& mapNodeRects != null
			&& mapNodeRects.Length > 0;
	}

	void SetMapDisplayVisible(bool show, bool animate)
	{
		if (mapGraphGroup == null)
			return;

		if (mapTransitionRoutine != null)
		{
			StopCoroutine(mapTransitionRoutine);
			mapTransitionRoutine = null;
		}

		mapGraphGroup.blocksRaycasts = show;
		mapGraphGroup.interactable = show;
		if (show)
			SetMapGraphContentScale(animate ? Vector3.one * 0.96f : Vector3.one);

		if (animate && Application.isPlaying && gameObject.activeInHierarchy)
		{
			mapTransitionRoutine = StartCoroutine(AnimateMapGraphVisibility(show));
			return;
		}

		mapGraphGroup.alpha = show ? 1f : 0f;
		SetMapGraphContentScale(Vector3.one);
	}

	IEnumerator AnimateMapGraphVisibility(bool show)
	{
		float startAlpha = mapGraphGroup.alpha;
		float targetAlpha = show ? 1f : 0f;
		Vector3 startScale = ResolveMapGraphContentScale();
		Vector3 targetScale = Vector3.one;
		float timer = 0f;

		while (timer < MapTransitionDuration)
		{
			timer += Time.unscaledDeltaTime;
			float t = Mathf.Clamp01(timer / MapTransitionDuration);
			float eased = 1f - (1f - t) * (1f - t);
			mapGraphGroup.alpha = Mathf.Lerp(startAlpha, targetAlpha, eased);
			SetMapGraphContentScale(Vector3.Lerp(startScale, targetScale, eased));
			yield return null;
		}

		mapGraphGroup.alpha = targetAlpha;
		SetMapGraphContentScale(Vector3.one);
		mapTransitionRoutine = null;
	}

	void SetMapGraphContentScale(Vector3 scale)
	{
		var target = mapContent != null ? mapContent : mapNodeRoot;
		if (target != null)
			target.localScale = scale;
	}

	Vector3 ResolveMapGraphContentScale()
	{
		var target = mapContent != null ? mapContent : mapNodeRoot;
		return target != null ? target.localScale : Vector3.one;
	}

	void ApplyMapPresentation(ExploreMapPresentation presentation)
	{
		activeMapPresentation = presentation;
		if (mapTitle != null)
			mapTitle.text = presentation.StageTitle;
		if (mapProgressText != null)
			mapProgressText.text = presentation.ProgressText;

		activeMapLayout = BuildMapLayout(presentation);
		ConfigureMapContentLayout(activeMapLayout);
		ApplyMapConnectionsWithLayout(presentation, activeMapLayout);
		ApplyMapNodesWithLayout(presentation, activeMapLayout);
		ApplyPlayerMapMarker(presentation);
		ScrollToCurrentNode(true);
	}

	void ApplyMapConnections(ExploreMapPresentation presentation)
	{
		ApplyMapConnectionsWithLayout(presentation, ResolveMapLayout(presentation, activeMapLayout));
	}

	void ApplyMapConnectionsWithLayout(ExploreMapPresentation presentation, ExploreMapLayoutResult layout)
	{
		if (mapConnectionLines == null)
			return;

		for (int i = 0; i < mapConnectionLines.Length; i++)
		{
			var line = mapConnectionLines[i];
			if (line == null)
				continue;

			bool hasConnection = i < presentation.ConnectionCount;
			if (!hasConnection)
			{
				line.raycastTarget = false;
				line.gameObject.SetActive(false);
				continue;
			}

			var connection = presentation.GetConnection(i);
			var fromNode = presentation.GetNode(connection.FromNodeIndex);
			var toNode = presentation.GetNode(connection.ToNodeIndex);
			bool hasFromLayout = layout.TryGetNode(connection.FromNodeIndex, out var fromLayout);
			bool hasToLayout = layout.TryGetNode(connection.ToNodeIndex, out var toLayout);
			var lineView = ResolveMapConnectionLineView(i);
			if (lineView != null)
			{
				if (hasFromLayout && hasToLayout)
					lineView.Show(fromNode, toNode, fromLayout.Center, toLayout.Center);
				else
					lineView.Show(fromNode, toNode, mapConnectionRoot);
				continue;
			}

			if (hasFromLayout && hasToLayout)
				ApplyLegacyMapLine(line, fromNode, toNode, fromLayout.Center, toLayout.Center);
			else
				ApplyLegacyMapLine(line, fromNode, toNode);
		}
	}

	void ApplyLegacyMapLine(Image line, ExploreMapNodeView fromNode, ExploreMapNodeView toNode)
	{
		if (line == null)
			return;

		line.gameObject.SetActive(true);
		line.raycastTarget = false;
		line.color = ResolveLegacyMapConnectionColor(fromNode, toNode);
		ApplyLegacyMapLineLayout(
			line.rectTransform,
			ExploreMapLayout.ResolveNodeCenter(fromNode),
			ExploreMapLayout.ResolveNodeCenter(toNode));
	}

	void ApplyLegacyMapLine(Image line, ExploreMapNodeView fromNode, ExploreMapNodeView toNode, Vector2 fromPosition, Vector2 toPosition)
	{
		if (line == null)
			return;

		line.gameObject.SetActive(true);
		line.raycastTarget = false;
		line.color = ResolveLegacyMapConnectionColor(fromNode, toNode);
		ApplyLegacyMapLineLayoutContent(line.rectTransform, fromPosition, toPosition);
	}

	void ApplyLegacyMapLineLayout(RectTransform line, Vector2 from, Vector2 to)
	{
		if (line == null)
			return;

		Vector2 midpoint = (from + to) * 0.5f;
		line.anchorMin = midpoint;
		line.anchorMax = midpoint;
		line.pivot = new Vector2(0.5f, 0.5f);
		line.anchoredPosition = Vector2.zero;
		Vector2 rootSize = ResolveMapConnectionRootSize();
		Vector2 scaledDelta = new Vector2((to.x - from.x) * rootSize.x, (to.y - from.y) * rootSize.y);
		line.sizeDelta = new Vector2(Mathf.Max(8f, scaledDelta.magnitude), 5f);
		line.localRotation = Quaternion.Euler(
			0f,
			0f,
			Mathf.Atan2(scaledDelta.y, scaledDelta.x) * Mathf.Rad2Deg);
	}

	void ApplyLegacyMapLineLayoutContent(RectTransform line, Vector2 from, Vector2 to)
	{
		if (line == null)
			return;

		Vector2 midpoint = (from + to) * 0.5f;
		Vector2 delta = to - from;
		line.anchorMin = Vector2.zero;
		line.anchorMax = Vector2.zero;
		line.pivot = new Vector2(0.5f, 0.5f);
		line.anchoredPosition = midpoint;
		line.sizeDelta = new Vector2(Mathf.Max(8f, delta.magnitude), 5f);
		line.localRotation = Quaternion.Euler(
			0f,
			0f,
			Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
		line.localScale = Vector3.one;
	}

	Vector2 ResolveMapConnectionRootSize()
	{
		Rect rect = mapConnectionRoot != null ? mapConnectionRoot.rect : default;
		float width = rect.width > 0f ? rect.width : 900f;
		float height = rect.height > 0f ? rect.height : 820f;
		return new Vector2(width, height);
	}

	ExploreMapLayoutResult ResolveMapLayout(ExploreMapPresentation presentation, ExploreMapLayoutResult layout)
	{
		if (layout.NodeCount == presentation.NodeCount)
			return layout;
		return BuildMapLayout(presentation);
	}

	ExploreMapLayoutResult BuildMapLayout(ExploreMapPresentation presentation)
	{
		Vector2 viewportSize = ResolveMapViewportSize();
		var config = ExploreMapLayout.CreateDefaultConfig(viewportSize);
		var layout = ExploreMapLayout.Build(presentation, config);
		if (layout.HasOverlaps)
			Debug.LogWarning($"[Explore] Map layout still has {layout.OverlapCount} overlapping node bounds after spacing resolution.");
		return layout;
	}

	Vector2 ResolveMapViewportSize()
	{
		Canvas.ForceUpdateCanvases();
		Rect rect = mapViewport != null ? mapViewport.rect : default;
		if (rect.width > 1f && rect.height > 1f)
			return rect.size;

		rect = mapContent != null && mapContent.parent is RectTransform parentRect ? parentRect.rect : default;
		if (rect.width > 1f && rect.height > 1f)
			return rect.size;

		rect = mapNodeRoot != null ? mapNodeRoot.rect : default;
		if (rect.width > 1f && rect.height > 1f)
			return rect.size;

		return ResolveMapConnectionRootSize();
	}

	void ConfigureMapContentLayout(ExploreMapLayoutResult layout)
	{
		float contentHeight = Mathf.Max(layout.ContentHeight, ResolveMapViewportSize().y);
		if (mapContent != null)
		{
			// MapContent is top-stretched for ScrollRect, while graph children use bottom-left anchored content coordinates.
			mapContent.anchorMin = new Vector2(0f, 1f);
			mapContent.anchorMax = new Vector2(1f, 1f);
			mapContent.pivot = new Vector2(0.5f, 1f);
			mapContent.anchoredPosition = Vector2.zero;
			mapContent.sizeDelta = new Vector2(0f, contentHeight);
		}

		ConfigureMapContentLayer(mapConnectionRoot);
		ConfigureMapContentLayer(mapNodeRoot);

		if (mapScrollRect != null)
		{
			mapScrollRect.horizontal = false;
			mapScrollRect.vertical = true;
			mapScrollRect.movementType = ScrollRect.MovementType.Clamped;
			mapScrollRect.content = mapContent;
			if (mapViewport != null)
				mapScrollRect.viewport = mapViewport;
		}
	}

	static void ConfigureMapContentLayer(RectTransform layer)
	{
		if (layer == null)
			return;

		layer.anchorMin = Vector2.zero;
		layer.anchorMax = Vector2.one;
		layer.pivot = new Vector2(0.5f, 0.5f);
		layer.offsetMin = Vector2.zero;
		layer.offsetMax = Vector2.zero;
		layer.localRotation = Quaternion.identity;
		layer.localScale = Vector3.one;
	}

	void ApplyMapNodes(ExploreMapPresentation presentation)
	{
		ApplyMapNodesWithLayout(presentation, ResolveMapLayout(presentation, activeMapLayout));
	}

	void ApplyMapNodesWithLayout(ExploreMapPresentation presentation, ExploreMapLayoutResult layout)
	{
		if (mapNodeRects == null)
			return;

		int nodeCount = presentation.NodeCount;
		for (int i = 0; i < mapNodeRects.Length; i++)
		{
			var nodeRoot = mapNodeRects[i];
			if (nodeRoot == null)
				continue;

			bool hasNode = i < nodeCount;
			Button button = ResolveMapNodeButton(i, nodeRoot);
			if (!hasNode)
			{
				var display = ResolveMapNodeDisplay(i, nodeRoot);
				if (display != null)
				{
					display.Hide();
					button = display.Button;
				}
				else
				{
					SetNodeRaycastTargets(nodeRoot, button, false);
					SetMapNodeHoverEnabled(i, nodeRoot, false);
					nodeRoot.gameObject.SetActive(false);
				}
				if (button != null)
					button.onClick.RemoveAllListeners();
				continue;
			}

			var node = presentation.GetNode(i);
			bool hasLayout = layout.TryGetNode(i, out var nodeLayout);
			var nodeDisplay = ResolveMapNodeDisplay(i, nodeRoot);
			if (nodeDisplay != null)
			{
				if (hasLayout)
					nodeDisplay.Show(node, ResolveMapNodeIconSprite(node.Kind), nodeLayout.Center);
				else
					nodeDisplay.Show(node, ResolveMapNodeIconSprite(node.Kind));
				ConfigureMapNodeButton(i, nodeDisplay.Button, node);
			}
			else
			{
				if (hasLayout)
					ApplyLegacyMapNodeLayout(nodeRoot, node, nodeLayout.Center);
				else
					ApplyLegacyMapNodeLayout(nodeRoot, node);
				ApplyLegacyMapNodeText(i, node);
				ApplyLegacyMapNodeVisual(i, nodeRoot, node);
				nodeRoot.gameObject.SetActive(true);
				ConfigureMapNodeButton(i, button, node);
				SetNodeRaycastTargets(nodeRoot, button, node.IsSelectable);
				SetMapNodeHoverEnabled(i, nodeRoot, node.IsSelectable);
			}
		}
	}

	void ConfigureMapNodeButton(int index, Button button, ExploreMapNodeView node)
	{
		if (button == null)
			return;

		ConfigureMapScrollForwarding(button);
		button.onClick.RemoveAllListeners();
		if (!node.IsSelectable)
			return;

		int capturedIndex = index;
		button.onClick.AddListener(() => OnMapNodeSelected(capturedIndex));
	}

	void ConfigureMapScrollForwarding(Button button)
	{
		if (button == null || mapScrollRect == null)
			return;

		var hoverEffect = button.GetComponent<ExploreMapNodeHoverEffect>();
		if (hoverEffect != null)
			hoverEffect.SetScrollTarget(mapScrollRect);
	}

	void OnMapNodeSelected(int presentationNodeIndex)
	{
		if (!TrySelectMapNodeForCurrentPresentation(presentationNodeIndex, out var node))
			return;

		Debug.Log($"[Explore] OnMapNodeSelected node={node.NodeId} kind={node.Kind} row={node.Row}");
		AudioManager.Play("UI_OK");
		ShowEncounterMode(node.Kind, node.NodeId);
	}

	bool TrySelectMapNodeForCurrentPresentation(int presentationNodeIndex, out ExploreMapNodeView node)
	{
		node = default;
		if (!isMapMode || !hasActiveMapPresentation)
			return false;
		if (presentationNodeIndex < 0 || presentationNodeIndex >= activeMapPresentation.NodeCount)
			return false;

		node = activeMapPresentation.GetNode(presentationNodeIndex);
		if (!node.IsSelectable)
			return false;

		GameSessionManager.PendingExploreMapNodeId = node.NodeId;
		hasSelectedMapNode = true;
		activeEncounterKind = node.Kind;
		return true;
	}

	void ApplyLegacyMapNodeLayout(RectTransform nodeRoot, ExploreMapNodeView node)
	{
		Vector2 center = ExploreMapLayout.ResolveNodeCenter(node);
		nodeRoot.anchorMin = center;
		nodeRoot.anchorMax = center;
		nodeRoot.pivot = new Vector2(0.5f, 0.5f);
		nodeRoot.sizeDelta = ExploreMapLayout.ResolveNodeSize(node.Kind);
		nodeRoot.anchoredPosition = Vector2.zero;
	}

	void ApplyLegacyMapNodeLayout(RectTransform nodeRoot, ExploreMapNodeView node, Vector2 contentPosition)
	{
		nodeRoot.anchorMin = Vector2.zero;
		nodeRoot.anchorMax = Vector2.zero;
		nodeRoot.pivot = new Vector2(0.5f, 0.5f);
		nodeRoot.sizeDelta = ExploreMapLayout.ResolveNodeSize(node.Kind);
		nodeRoot.anchoredPosition = contentPosition;
		nodeRoot.localRotation = Quaternion.identity;
		nodeRoot.localScale = Vector3.one;
	}

	void ApplyLegacyMapNodeText(int index, ExploreMapNodeView node)
	{
		SetMapNodeText(mapNodeIconLabels, index, node.IconLabel, false);
		SetMapNodeText(mapNodeTitles, index, node.Title, node.IsCurrent);
		SetMapNodeText(mapNodeDescs, index, node.Description, false);
	}

	static void SetMapNodeText(TMP_Text[] labels, int index, string value, bool visible)
	{
		if (labels == null || index < 0 || index >= labels.Length || labels[index] == null)
			return;
		labels[index].text = value;
		labels[index].fontSize = 42f;
		labels[index].raycastTarget = false;
		labels[index].gameObject.SetActive(visible);
	}

	void ApplyLegacyMapNodeVisual(int index, RectTransform nodeRoot, ExploreMapNodeView node)
	{
		Graphic graphic = ResolveMapNodeGraphic(index, nodeRoot);
		if (graphic != null)
		{
			var rootGraphic = nodeRoot != null ? nodeRoot.GetComponent<Graphic>() : null;
			graphic.color = graphic == rootGraphic ? Color.clear : ExploreMapNodeDisplay.ResolveFillColor(node);
			graphic.raycastTarget = false;
		}

		Image iconImage = ResolveMapNodeIconImage(index);
		if (iconImage == null)
			return;

		Sprite iconSprite = ResolveMapNodeIconSprite(node.Kind);
		iconImage.sprite = iconSprite;
		iconImage.color = iconSprite != null ? ExploreMapNodeDisplay.ResolveIconColor(node) : Color.clear;
		iconImage.preserveAspect = true;
		iconImage.raycastTarget = false;
	}

	void ApplyPlayerMapMarker(ExploreMapPresentation presentation)
	{
		var marker = ResolvePlayerMapMarkerRect();
		if (marker == null)
			return;

		int nodeIndex = ResolveCurrentMapMarkerNodeIndex(presentation, out string currentNodeId);
		if (nodeIndex < 0)
		{
			HidePlayerMapMarker();
			Debug.LogWarning($"[Explore] Player location marker hidden: current node id '{currentNodeId}' was not found in the current map presentation.");
			return;
		}

		var node = presentation.GetNode(nodeIndex);
		var nodeRoot = ResolveMapNodeRect(nodeIndex);
		if (nodeRoot != null)
			ApplyPlayerMapMarkerToNode(marker, nodeRoot);
		else
			ApplyLegacyPlayerMapMarker(marker, node);
	}

	int ResolveCurrentMapMarkerNodeIndex(ExploreMapPresentation presentation, out string currentNodeId)
	{
		currentNodeId = !string.IsNullOrEmpty(GameSessionManager.CurrentExploreMapNodeId)
			? GameSessionManager.CurrentExploreMapNodeId
			: presentation.CurrentNodeId;

		if (!string.IsNullOrEmpty(currentNodeId))
			return presentation.FindNodeIndex(currentNodeId);

		for (int i = 0; i < presentation.NodeCount; i++)
		{
			if (!presentation.GetNode(i).IsCurrent)
				continue;
			currentNodeId = presentation.GetNode(i).NodeId;
			return i;
		}

		currentNodeId = "(empty)";
		return -1;
	}

	RectTransform ResolveMapNodeRect(int index)
	{
		if (mapNodeRects == null || index < 0 || index >= mapNodeRects.Length)
			return null;
		return mapNodeRects[index];
	}

	void ApplyPlayerMapMarkerToNode(RectTransform marker, RectTransform nodeRoot)
	{
		marker.SetParent(nodeRoot, false);
		marker.anchorMin = new Vector2(0.5f, 0.5f);
		marker.anchorMax = new Vector2(0.5f, 0.5f);
		marker.pivot = new Vector2(0.5f, 0.5f);
		marker.sizeDelta = PlayerLocationMarkerSize;
		marker.anchoredPosition = PlayerLocationMarkerOffset;
		marker.localRotation = Quaternion.identity;
		marker.localScale = Vector3.one;
		ConfigurePlayerMapMarkerGraphics(marker);
		marker.gameObject.SetActive(true);
		marker.SetAsLastSibling();
	}

	void ApplyLegacyPlayerMapMarker(RectTransform marker, ExploreMapNodeView node)
	{
		Vector2 center = ExploreMapLayout.ResolveNodeCenter(node);
		marker.SetParent(mapNodeRoot, false);
		marker.anchorMin = center;
		marker.anchorMax = center;
		marker.pivot = new Vector2(0.5f, 0.5f);
		marker.sizeDelta = PlayerLocationMarkerSize;
		marker.anchoredPosition = PlayerLocationMarkerOffset;
		marker.localRotation = Quaternion.identity;
		marker.localScale = Vector3.one;
		ConfigurePlayerMapMarkerGraphics(marker);
		marker.gameObject.SetActive(true);
		marker.SetAsLastSibling();
	}

	void HidePlayerMapMarker()
	{
		var marker = ResolvePlayerMapMarkerRect();
		if (marker != null)
			marker.gameObject.SetActive(false);
	}

	void ScrollToCurrentNode(bool immediate)
	{
		if (mapScrollRect == null || mapContent == null || activeMapLayout.NodeCount <= 0)
			return;

		if (immediate || !Application.isPlaying || !gameObject.activeInHierarchy)
		{
			ApplyCurrentNodeScrollPosition();
			return;
		}

		StartCoroutine(ScrollToCurrentNodeNextFrame());
	}

	IEnumerator ScrollToCurrentNodeNextFrame()
	{
		yield return null;
		ApplyCurrentNodeScrollPosition();
	}

	void ApplyCurrentNodeScrollPosition()
	{
		if (mapScrollRect == null || activeMapLayout.NodeCount <= 0)
			return;

		Canvas.ForceUpdateCanvases();
		int nodeIndex = ResolveCurrentMapMarkerNodeIndex(activeMapPresentation, out _);
		if (nodeIndex < 0)
			return;

		float normalizedPosition = activeMapLayout.CalculateVerticalNormalizedPosition(
			nodeIndex,
			ResolveMapViewportSize().y);
		mapScrollRect.StopMovement();
		mapScrollRect.horizontalNormalizedPosition = 0f;
		mapScrollRect.verticalNormalizedPosition = normalizedPosition;
	}

	RectTransform ResolvePlayerMapMarkerRect()
	{
		if (playerMapMarker != null)
			return playerMapMarker;
		var markerView = ResolvePlayerMapMarkerView();
		return markerView != null ? markerView.transform as RectTransform : null;
	}

	static void ConfigurePlayerMapMarkerGraphics(RectTransform marker)
	{
		if (marker == null)
			return;

		var graphics = marker.GetComponentsInChildren<Graphic>(true);
		for (int i = 0; i < graphics.Length; i++)
			if (graphics[i] != null)
				graphics[i].raycastTarget = false;
	}

	ExploreMapNodeDisplay ResolveMapNodeDisplay(int index, RectTransform nodeRoot)
	{
		if (mapNodeViews != null && index >= 0 && index < mapNodeViews.Length && mapNodeViews[index] != null)
			return mapNodeViews[index];
		return nodeRoot != null ? nodeRoot.GetComponent<ExploreMapNodeDisplay>() : null;
	}

	ExploreMapConnectionLineDisplay ResolveMapConnectionLineView(int index)
	{
		if (mapConnectionLineViews != null && index >= 0 && index < mapConnectionLineViews.Length && mapConnectionLineViews[index] != null)
			return mapConnectionLineViews[index];
		if (mapConnectionLines == null || index < 0 || index >= mapConnectionLines.Length || mapConnectionLines[index] == null)
			return null;

		var lineView = mapConnectionLines[index].GetComponent<ExploreMapConnectionLineDisplay>();
		if (lineView == null)
			lineView = mapConnectionLines[index].gameObject.AddComponent<ExploreMapConnectionLineDisplay>();
		return lineView;
	}

	ExploreMapMarkerDisplay ResolvePlayerMapMarkerView()
	{
		if (playerMapMarkerView != null)
			return playerMapMarkerView;
		return playerMapMarker != null ? playerMapMarker.GetComponent<ExploreMapMarkerDisplay>() : null;
	}

	Button ResolveMapNodeButton(int index, RectTransform nodeRoot)
	{
		if (mapNodeButtons != null && index >= 0 && index < mapNodeButtons.Length && mapNodeButtons[index] != null)
			return mapNodeButtons[index];
		return nodeRoot != null ? nodeRoot.GetComponent<Button>() : null;
	}

	Image ResolveMapNodeIconImage(int index)
	{
		if (mapNodeIconImages == null || index < 0 || index >= mapNodeIconImages.Length)
			return null;
		return mapNodeIconImages[index];
	}

	ExploreMapNodeHoverEffect ResolveMapNodeHoverEffect(int index, RectTransform nodeRoot)
	{
		if (mapNodeHoverEffects != null && index >= 0 && index < mapNodeHoverEffects.Length && mapNodeHoverEffects[index] != null)
			return mapNodeHoverEffects[index];
		return nodeRoot != null ? nodeRoot.GetComponent<ExploreMapNodeHoverEffect>() : null;
	}

	Graphic ResolveMapNodeGraphic(int index, RectTransform nodeRoot)
	{
		if (mapNodeGraphics != null && index >= 0 && index < mapNodeGraphics.Length && mapNodeGraphics[index] != null)
			return mapNodeGraphics[index];
		return nodeRoot != null ? nodeRoot.GetComponent<Graphic>() : null;
	}

	void SetMapNodeHoverEnabled(int index, RectTransform nodeRoot, bool enabled)
	{
		var hoverEffect = ResolveMapNodeHoverEffect(index, nodeRoot);
		if (hoverEffect != null)
			hoverEffect.SetHoverEnabled(enabled);
	}

	static void SetNodeRaycastTargets(RectTransform nodeRoot, Button button, bool raycastTarget)
	{
		if (nodeRoot != null)
		{
			var graphics = nodeRoot.GetComponentsInChildren<Graphic>(true);
			for (int i = 0; i < graphics.Length; i++)
				if (graphics[i] != null)
					graphics[i].raycastTarget = false;
		}

		if (button == null)
			return;

		button.interactable = raycastTarget;
		if (button.targetGraphic != null)
			button.targetGraphic.raycastTarget = raycastTarget;
	}

	Sprite ResolveMapNodeIconSprite(ExploreMapNodeKind kind)
	{
		switch (kind)
		{
			case ExploreMapNodeKind.Boss:
				return mapBossIconSprite != null ? mapBossIconSprite : mapCombatIconSprite;
			case ExploreMapNodeKind.Shop:
				return mapShopIconSprite != null ? mapShopIconSprite : mapCombatIconSprite;
			case ExploreMapNodeKind.Heal:
				return mapHealIconSprite != null ? mapHealIconSprite : mapCombatIconSprite;
			case ExploreMapNodeKind.Loot:
				// Loot 전용 아이콘이 들어오기 전까지 상점 아이콘을 임시로 사용한다.
				return mapShopIconSprite != null ? mapShopIconSprite : mapCombatIconSprite;
			case ExploreMapNodeKind.Combat:
			case ExploreMapNodeKind.Start:
			default:
				return mapCombatIconSprite;
		}
	}

	static Color ResolveLegacyMapConnectionColor(ExploreMapNodeView fromNode, ExploreMapNodeView toNode)
	{
		return ExploreMapConnectionLineDisplay.ResolveConnectionColor(fromNode, toNode);
	}

	static string ResolveCurrentMapNodeId()
	{
		int currentEventIndex = Mathf.Clamp(
			GameSessionManager.CurrentEventIndex,
			ExploreMapPresentationPolicy.StartRow,
			ExploreMapPresentationPolicy.BossRow);
		string nodeId = GameSessionManager.CurrentExploreMapNodeId;
		if (NodeIdMatchesRow(nodeId, currentEventIndex))
			return nodeId;
		return BuildMapNodeId(currentEventIndex, 1);
	}

	static bool NodeIdMatchesRow(string nodeId, int row)
	{
		if (string.IsNullOrEmpty(nodeId))
			return false;
		return nodeId.StartsWith($"r{row}l", System.StringComparison.Ordinal);
	}

	static string BuildMapNodeId(int row, int lane)
	{
		return $"r{row}l{lane}";
	}

	static int BuildMapSeed(StageData stage)
	{
		string key = stage != null ? stage.id : "";
		int sessionSeed = GameSessionManager.ExploreMapSeed;
		if (string.IsNullOrEmpty(key))
			return sessionSeed != 0 ? sessionSeed : 1;

		unchecked
		{
			int hash = sessionSeed != 0 ? sessionSeed : 23;
			for (int i = 0; i < key.Length; i++)
				hash = hash * 31 + key[i];
			return hash != 0 ? hash : 1;
		}
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
		return $"[{PowerUpRewardCatalog.GetDisplayTitle(type)}]";
	}

	static string ResolveBattleSceneName(CharacterType character)
	{
		switch (character)
		{
			case CharacterType.Mahjong: return "MahjongBattleScene";
			case CharacterType.Holdem:  return "HoldemBattleScene";
			default:                    return "DiceBattleScene";
		}
	}
}

public enum ExploreMapNodeKind
{
	Start,
	Combat,
	Heal,
	Shop,
	Loot,
	Boss,
}

public readonly struct ExploreMapNodeView
{
	readonly string[] connectedNextNodeIds;

	public readonly string NodeId;
	public readonly int Row;
	public readonly int Lane;
	public readonly Vector2 NormalizedPosition;
	public readonly ExploreMapNodeKind Kind;
	public readonly string Title;
	public readonly string Description;
	public readonly string IconLabel;
	public readonly bool IsCurrent;
	public readonly bool IsReachable;
	public readonly bool IsSelectable;
	public readonly bool IsCompleted;

	public int ConnectedNextNodeCount => connectedNextNodeIds != null ? connectedNextNodeIds.Length : 0;

	public ExploreMapNodeView(
		string nodeId,
		int row,
		int lane,
		ExploreMapNodeKind kind,
		string title,
		string description,
		string iconLabel,
		bool isCurrent,
		bool isReachable,
		bool isSelectable,
		bool isCompleted,
		string[] connectedNextNodeIds)
		: this(
			nodeId,
			row,
			lane,
			ResolveDefaultNormalizedPosition(row, lane),
			kind,
			title,
			description,
			iconLabel,
			isCurrent,
			isReachable,
			isSelectable,
			isCompleted,
			connectedNextNodeIds)
	{
	}

	public ExploreMapNodeView(
		string nodeId,
		int row,
		int lane,
		Vector2 normalizedPosition,
		ExploreMapNodeKind kind,
		string title,
		string description,
		string iconLabel,
		bool isCurrent,
		bool isReachable,
		bool isSelectable,
		bool isCompleted,
		string[] connectedNextNodeIds)
	{
		NodeId = nodeId;
		Row = row;
		Lane = lane;
		NormalizedPosition = normalizedPosition;
		Kind = kind;
		Title = title;
		Description = description;
		IconLabel = iconLabel;
		IsCurrent = isCurrent;
		IsReachable = isReachable;
		IsSelectable = isSelectable;
		IsCompleted = isCompleted;
		this.connectedNextNodeIds = connectedNextNodeIds;
	}

	static Vector2 ResolveDefaultNormalizedPosition(int row, int lane)
	{
		return ExploreMapLayout.ResolveDefaultNodeCenter(row, lane);
	}

	public string GetConnectedNextNodeId(int index)
	{
		if (connectedNextNodeIds == null || index < 0 || index >= connectedNextNodeIds.Length)
			return "";
		return connectedNextNodeIds[index];
	}
}

public readonly struct ExploreMapConnection
{
	public readonly string FromNodeId;
	public readonly string ToNodeId;
	public readonly int FromNodeIndex;
	public readonly int ToNodeIndex;
	public readonly bool IsReachable;
	public readonly bool IsCompleted;

	public ExploreMapConnection(
		string fromNodeId,
		string toNodeId,
		int fromNodeIndex,
		int toNodeIndex,
		bool isReachable,
		bool isCompleted)
	{
		FromNodeId = fromNodeId;
		ToNodeId = toNodeId;
		FromNodeIndex = fromNodeIndex;
		ToNodeIndex = toNodeIndex;
		IsReachable = isReachable;
		IsCompleted = isCompleted;
	}
}

public readonly struct ExploreMapPresentation
{
	readonly ExploreMapNodeView[] nodes;
	readonly ExploreMapConnection[] connections;

	public readonly int CurrentRow;
	public readonly int CurrentEncounterRow;
	public readonly string CurrentNodeId;
	public readonly string StageTitle;
	public readonly string ProgressText;

	public int NodeCount => nodes != null ? nodes.Length : 0;
	public int ConnectionCount => connections != null ? connections.Length : 0;

	public ExploreMapPresentation(
		int currentRow,
		int currentEncounterRow,
		string stageTitle,
		string progressText,
		ExploreMapNodeView[] nodes,
		ExploreMapConnection[] connections)
		: this(currentRow, currentEncounterRow, "", stageTitle, progressText, nodes, connections)
	{
	}

	public ExploreMapPresentation(
		int currentRow,
		int currentEncounterRow,
		string currentNodeId,
		string stageTitle,
		string progressText,
		ExploreMapNodeView[] nodes,
		ExploreMapConnection[] connections)
	{
		CurrentRow = currentRow;
		CurrentEncounterRow = currentEncounterRow;
		CurrentNodeId = currentNodeId;
		StageTitle = stageTitle;
		ProgressText = progressText;
		this.nodes = nodes;
		this.connections = connections;
	}

	public ExploreMapNodeView GetNode(int index)
	{
		if (nodes == null || index < 0 || index >= nodes.Length)
			return default;
		return nodes[index];
	}

	public ExploreMapConnection GetConnection(int index)
	{
		if (connections == null || index < 0 || index >= connections.Length)
			return default;
		return connections[index];
	}

	public int FindNodeIndex(string nodeId)
	{
		if (nodes == null || string.IsNullOrEmpty(nodeId))
			return -1;
		for (int i = 0; i < nodes.Length; i++)
			if (nodes[i].NodeId == nodeId)
				return i;
		return -1;
	}

	public int FindConnectionIndex(string fromNodeId, string toNodeId)
	{
		if (connections == null || string.IsNullOrEmpty(fromNodeId) || string.IsNullOrEmpty(toNodeId))
			return -1;
		for (int i = 0; i < connections.Length; i++)
			if (connections[i].FromNodeId == fromNodeId && connections[i].ToNodeId == toNodeId)
				return i;
		return -1;
	}
}

public readonly struct ExploreMapGenerationConfig
{
	public readonly int Seed;
	public readonly int MinNodesPerMiddleRow;
	public readonly int MaxNodesPerMiddleRow;

	public ExploreMapGenerationConfig(
		int seed,
		int minNodesPerMiddleRow,
		int maxNodesPerMiddleRow)
	{
		Seed = seed;
		MinNodesPerMiddleRow = minNodesPerMiddleRow;
		MaxNodesPerMiddleRow = maxNodesPerMiddleRow;
	}

	public static ExploreMapGenerationConfig CreateDefault(int seed)
	{
		return new ExploreMapGenerationConfig(seed, 2, 3);
	}
}

public static class ExploreMapPresentationPolicy
{
	public const int StartRow = 0;
	public const int BossRow = 9;
	public const int RowCount = 10;
	public const int EventCount = 9;
	public const int MaxNodeCount = 26;
	public const int MaxConnectionCount = 55;
	public const int MinGeneratedNodesPerMiddleRow = 1;
	public const int MaxGeneratedNodesPerMiddleRow = 3;

	static readonly ExploreMapNodeTemplate[][] Rows =
	{
		new[] { new ExploreMapNodeTemplate(ExploreMapNodeKind.Start, 1, "출발", "탑 아래", "시작") },
		new[]
		{
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 0, "좁은 길", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 1, "어두운 길", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 2, "위험한 길", "전투", "전투"),
		},
		new[]
		{
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 0, "낡은 다리", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 1, "무너진 길", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 2, "가시 길", "전투", "전투"),
		},
		new[]
		{
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Heal, 0, "휴식터", "레드 하트 회복", "회복"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Loot, 1, "보급품", "파워업 선택", "보급"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Loot, 2, "보급 상자", "파워업 선택", "상자"),
		},
		new[]
		{
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 0, "안개 길", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 1, "낡은 계단", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 2, "울림의 길", "전투", "전투"),
		},
		new[]
		{
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Loot, 0, "숨겨진 상자", "파워업 선택", "상자"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Heal, 1, "회복 샘", "레드 하트 회복", "회복"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Loot, 2, "보급 꾸러미", "파워업 선택", "보급"),
		},
		new[]
		{
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 0, "갈림길 전투", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 1, "절벽 길", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 2, "매복 지점", "전투", "전투"),
		},
		new[]
		{
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Loot, 0, "마지막 보급", "파워업 선택", "보급"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Loot, 1, "마지막 상자", "파워업 선택", "상자"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Heal, 2, "작은 야영지", "레드 하트 회복", "회복"),
		},
		new[]
		{
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 0, "보스 전초전", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 1, "부서진 문", "전투", "전투"),
			new ExploreMapNodeTemplate(ExploreMapNodeKind.Combat, 2, "피 묻은 길", "전투", "전투"),
		},
		new[] { new ExploreMapNodeTemplate(ExploreMapNodeKind.Boss, 1, "보스", "해골 문양의 결전", "해골") },
	};

	public static bool TryBuild(string stageDisplayName, int currentEventIndex, out ExploreMapPresentation presentation)
	{
		return TryBuild(stageDisplayName, currentEventIndex, "", out presentation);
	}

	public static bool TryBuild(
		string stageDisplayName,
		int currentEventIndex,
		string currentNodeId,
		out ExploreMapPresentation presentation)
	{
		presentation = default;
		if (currentEventIndex < 0 || currentEventIndex >= EventCount)
			return false;

		int currentMarkerRow = Clamp(currentEventIndex, StartRow, BossRow - 1);
		int currentEncounterRow = Clamp(currentEventIndex + 1, StartRow + 1, BossRow);
		string resolvedCurrentNodeId = ResolveCurrentNodeId(Rows, currentMarkerRow, currentNodeId);
		var nodes = BuildNodes(Rows, currentMarkerRow, currentEncounterRow, resolvedCurrentNodeId);
		var connections = BuildConnections(nodes, currentMarkerRow, currentEncounterRow, resolvedCurrentNodeId);
		string stageTitle = stageDisplayName ?? "";
		string progressText = $"{currentEncounterRow} / {EventCount}";
		presentation = new ExploreMapPresentation(
			currentMarkerRow,
			currentEncounterRow,
			resolvedCurrentNodeId,
			stageTitle,
			progressText,
			nodes,
			connections);
		return true;
	}

	public static bool TryBuildRandom(string stageDisplayName, int currentEventIndex, int seed, out ExploreMapPresentation presentation)
	{
		return TryBuildRandom(stageDisplayName, currentEventIndex, seed, "", out presentation);
	}

	public static bool TryBuildRandom(
		string stageDisplayName,
		int currentEventIndex,
		int seed,
		string currentNodeId,
		out ExploreMapPresentation presentation)
	{
		return TryBuildRandom(stageDisplayName, currentEventIndex, ExploreMapGenerationConfig.CreateDefault(seed), currentNodeId, out presentation);
	}

	public static bool TryBuildRandom(
		string stageDisplayName,
		int currentEventIndex,
		ExploreMapGenerationConfig config,
		out ExploreMapPresentation presentation)
	{
		return TryBuildRandom(stageDisplayName, currentEventIndex, config, "", out presentation);
	}

	public static bool TryBuildRandom(
		string stageDisplayName,
		int currentEventIndex,
		ExploreMapGenerationConfig config,
		string currentNodeId,
		out ExploreMapPresentation presentation)
	{
		presentation = default;
		if (currentEventIndex < 0 || currentEventIndex >= EventCount)
			return false;
		if (!IsValidGenerationConfig(config))
			return false;

		int currentMarkerRow = Clamp(currentEventIndex, StartRow, BossRow - 1);
		int currentEncounterRow = Clamp(currentEventIndex + 1, StartRow + 1, BossRow);
		var random = new SeededMapRandom(config.Seed);
		var rows = BuildRandomRows(config, ref random);
		string resolvedCurrentNodeId = ResolveCurrentNodeId(rows, currentMarkerRow, currentNodeId);
		var nodes = BuildNodes(rows, currentMarkerRow, currentEncounterRow, resolvedCurrentNodeId);
		var connections = BuildConnections(nodes, currentMarkerRow, currentEncounterRow, resolvedCurrentNodeId);
		string stageTitle = stageDisplayName ?? "";
		string progressText = $"{currentEncounterRow} / {EventCount}";
		presentation = new ExploreMapPresentation(
			currentMarkerRow,
			currentEncounterRow,
			resolvedCurrentNodeId,
			stageTitle,
			progressText,
			nodes,
			connections);
		return true;
	}

	public static bool IsCombatRow(int row)
	{
		return row == 1 || row == 2 || row == 4 || row == 6 || row == 8;
	}

	public static bool IsUtilityRow(int row)
	{
		return row == 3 || row == 5 || row == 7;
	}

	static ExploreMapNodeView[] BuildNodes(
		ExploreMapNodeTemplate[][] rows,
		int currentMarkerRow,
		int currentEncounterRow,
		string currentNodeId)
	{
		var nodes = new List<ExploreMapNodeView>(MaxNodeCount);
		for (int row = 0; row < rows.Length; row++)
		{
			for (int i = 0; i < rows[row].Length; i++)
			{
				var template = rows[row][i];
				string nodeId = BuildNodeId(row, template.Lane);
				bool isCurrent = nodeId == currentNodeId;
				bool isReachable = row == currentEncounterRow
					&& IsConnectedFromCurrentNode(rows, currentMarkerRow, currentNodeId, nodeId);
				bool isSelectable = isReachable;
				bool isCompleted = row < currentMarkerRow;
				nodes.Add(new ExploreMapNodeView(
					nodeId,
					row,
					template.Lane,
					ResolveTemplatePosition(row, template),
					template.Kind,
					template.Title,
					template.Description,
					BuildMapNodeDisplayLabel(row, i),
					isCurrent,
					isReachable,
					isSelectable,
					isCompleted,
					ResolveConnectedNextNodeIds(rows, row, template)));
			}
		}
		return nodes.ToArray();
	}

	static string BuildMapNodeDisplayLabel(int row, int nodeIndex)
	{
		if (row == StartRow)
			return "시작";
		return $"{Clamp(row, 1, EventCount)}-{nodeIndex + 1}";
	}

	static ExploreMapConnection[] BuildConnections(
		ExploreMapNodeView[] nodes,
		int currentMarkerRow,
		int currentEncounterRow,
		string currentNodeId)
	{
		var connections = new List<ExploreMapConnection>(MaxConnectionCount);
		for (int fromIndex = 0; fromIndex < nodes.Length; fromIndex++)
		{
			var from = nodes[fromIndex];
			for (int i = 0; i < from.ConnectedNextNodeCount; i++)
			{
				string toNodeId = from.GetConnectedNextNodeId(i);
				int toIndex = FindNodeIndex(nodes, toNodeId);
				if (toIndex < 0)
					continue;

				var to = nodes[toIndex];
				bool isReachable = from.NodeId == currentNodeId && to.Row == currentEncounterRow;
				bool isCompleted = to.Row <= currentMarkerRow;
				connections.Add(new ExploreMapConnection(
					from.NodeId,
					to.NodeId,
					fromIndex,
					toIndex,
					isReachable,
					isCompleted));
			}
		}
		return connections.ToArray();
	}

	static string ResolveCurrentNodeId(
		ExploreMapNodeTemplate[][] rows,
		int currentMarkerRow,
		string requestedNodeId)
	{
		if (rows == null || currentMarkerRow < 0 || currentMarkerRow >= rows.Length)
			return "";

		if (!string.IsNullOrEmpty(requestedNodeId)
			&& FindTemplateIndexByNodeId(rows[currentMarkerRow], currentMarkerRow, requestedNodeId) >= 0)
			return requestedNodeId;

		int centerIndex = FindTemplateIndexByLane(rows[currentMarkerRow], 1);
		if (centerIndex >= 0)
			return BuildNodeId(currentMarkerRow, 1);
		if (rows[currentMarkerRow] == null || rows[currentMarkerRow].Length == 0)
			return "";
		return BuildNodeId(currentMarkerRow, rows[currentMarkerRow][0].Lane);
	}

	static bool IsConnectedFromCurrentNode(
		ExploreMapNodeTemplate[][] rows,
		int currentMarkerRow,
		string currentNodeId,
		string targetNodeId)
	{
		if (rows == null || currentMarkerRow < 0 || currentMarkerRow >= BossRow)
			return false;

		var currentRow = rows[currentMarkerRow];
		int currentIndex = FindTemplateIndexByNodeId(currentRow, currentMarkerRow, currentNodeId);
		if (currentIndex < 0)
			return false;

		var connectedNodeIds = ResolveConnectedNextNodeIds(rows, currentMarkerRow, currentRow[currentIndex]);
		for (int i = 0; i < connectedNodeIds.Length; i++)
			if (connectedNodeIds[i] == targetNodeId)
				return true;
		return false;
	}

	static Vector2 ResolveTemplatePosition(int row, ExploreMapNodeTemplate template)
	{
		if (template.HasCustomPosition)
			return template.Position;
		return ResolveDefaultMapNodeCenter(row, template.Lane);
	}

	static Vector2 ResolveDefaultMapNodeCenter(int row, int lane)
	{
		return ExploreMapLayout.ResolveDefaultNodeCenter(row, lane);
	}

	static ExploreMapNodeTemplate[][] BuildRandomRows(ExploreMapGenerationConfig config, ref SeededMapRandom random)
	{
		var rows = new ExploreMapNodeTemplate[RowCount][];
		rows[StartRow] = new[] { new ExploreMapNodeTemplate(ExploreMapNodeKind.Start, 1, "출발", "탑 아래", "시작") };
		for (int row = StartRow + 1; row < BossRow; row++)
			rows[row] = BuildRandomMiddleRow(row, config, ref random);
		rows[BossRow] = new[] { new ExploreMapNodeTemplate(ExploreMapNodeKind.Boss, 1, "보스", "해골 문양의 결전", "해골") };
		AssignRandomConnections(rows, ref random);
		return rows;
	}

	static ExploreMapNodeTemplate[] BuildRandomMiddleRow(
		int row,
		ExploreMapGenerationConfig config,
		ref SeededMapRandom random)
	{
		int nodeCount = random.Next(config.MinNodesPerMiddleRow, config.MaxNodesPerMiddleRow + 1);
		int[] lanes = BuildRandomLanes(nodeCount, ref random);
		var templates = new ExploreMapNodeTemplate[nodeCount];
		int utilityOffset = random.Next(0, 2);
		for (int i = 0; i < lanes.Length; i++)
		{
			ExploreMapNodeKind kind = IsUtilityRow(row)
				? BuildUtilityKind(i, utilityOffset)
				: ExploreMapNodeKind.Combat;
			Vector2 position = BuildRandomNodePosition(row, lanes[i], ref random);
			templates[i] = BuildGeneratedTemplate(row, lanes[i], kind, position);
		}
		return templates;
	}

	static int[] BuildRandomLanes(int nodeCount, ref SeededMapRandom random)
	{
		switch (nodeCount)
		{
			case 1:
				return new[] { 1 };
			case 2:
				return random.Next(0, 2) == 0
					? new[] { 0, 1 }
					: new[] { 1, 2 };
			default:
				return new[] { 0, 1, 2 };
		}
	}

	static ExploreMapNodeTemplate BuildGeneratedTemplate(
		int row,
		int lane,
		ExploreMapNodeKind kind,
		Vector2 position)
	{
		switch (kind)
		{
			case ExploreMapNodeKind.Heal:
				return new ExploreMapNodeTemplate(kind, lane, position, "회복터", "레드 하트 회복", "회복");
			case ExploreMapNodeKind.Shop:
				return new ExploreMapNodeTemplate(kind, lane, position, "보급", "파워업 선택", "보급");
			case ExploreMapNodeKind.Loot:
				return new ExploreMapNodeTemplate(kind, lane, position, "상자", "파워업 선택", "상자");
			default:
				return new ExploreMapNodeTemplate(kind, lane, position, "전투", "전투", "전투");
		}
	}

	static Vector2 BuildRandomNodePosition(int row, int lane, ref SeededMapRandom random)
	{
		Vector2 center = ResolveDefaultMapNodeCenter(row, lane);
		float xJitter = random.Next(-55, 56) / 1000f;
		float yJitter = random.Next(-18, 19) / 1000f;
		return new Vector2(
			Mathf.Clamp(center.x + xJitter, 0.13f, 0.87f),
			Mathf.Clamp(center.y + yJitter, 0.07f, 0.93f));
	}

	static ExploreMapNodeKind BuildUtilityKind(int nodeIndex, int offset)
	{
		switch ((nodeIndex + offset) % 2)
		{
			case 0: return ExploreMapNodeKind.Heal;
			default: return ExploreMapNodeKind.Loot;
		}
	}

	static void AssignRandomConnections(ExploreMapNodeTemplate[][] rows, ref SeededMapRandom random)
	{
		for (int row = StartRow; row < BossRow; row++)
		{
			var currentRow = rows[row];
			var nextRow = rows[row + 1];
			var connectedNextNodeIds = new List<string>[currentRow.Length];
			for (int i = 0; i < connectedNextNodeIds.Length; i++)
				connectedNextNodeIds[i] = new List<string>(nextRow.Length);

			for (int i = 0; i < currentRow.Length; i++)
			{
				int nextIndex = PickNearbyTemplateIndex(currentRow[i].Lane, nextRow, ref random);
				AddConnection(connectedNextNodeIds[i], BuildNodeId(row + 1, nextRow[nextIndex].Lane));
			}

			for (int nextIndex = 0; nextIndex < nextRow.Length; nextIndex++)
			{
				string nextNodeId = BuildNodeId(row + 1, nextRow[nextIndex].Lane);
				if (HasIncomingConnection(connectedNextNodeIds, nextNodeId))
					continue;

				int currentIndex = PickNearbyTemplateIndex(nextRow[nextIndex].Lane, currentRow, ref random);
				AddConnection(connectedNextNodeIds[currentIndex], nextNodeId);
			}

			for (int i = 0; i < currentRow.Length; i++)
			{
				for (int nextIndex = 0; nextIndex < nextRow.Length; nextIndex++)
				{
					if (System.Math.Abs(currentRow[i].Lane - nextRow[nextIndex].Lane) > 1)
						continue;
					if (random.Next(0, 100) >= 28)
						continue;

					AddConnection(connectedNextNodeIds[i], BuildNodeId(row + 1, nextRow[nextIndex].Lane));
				}
			}

			for (int i = 0; i < currentRow.Length; i++)
				rows[row][i] = currentRow[i].WithConnectedNextNodeIds(connectedNextNodeIds[i].ToArray());
		}
	}

	static bool HasIncomingConnection(List<string>[] connectedNextNodeIds, string nextNodeId)
	{
		for (int i = 0; i < connectedNextNodeIds.Length; i++)
		{
			for (int j = 0; j < connectedNextNodeIds[i].Count; j++)
				if (connectedNextNodeIds[i][j] == nextNodeId)
					return true;
		}
		return false;
	}

	static void AddConnection(List<string> connectedNextNodeIds, string nextNodeId)
	{
		for (int i = 0; i < connectedNextNodeIds.Count; i++)
			if (connectedNextNodeIds[i] == nextNodeId)
				return;
		connectedNextNodeIds.Add(nextNodeId);
	}

	static int PickNearbyTemplateIndex(int lane, ExploreMapNodeTemplate[] templates, ref SeededMapRandom random)
	{
		var nearbyIndices = new List<int>(templates.Length);
		for (int i = 0; i < templates.Length; i++)
			if (System.Math.Abs(lane - templates[i].Lane) <= 1)
				nearbyIndices.Add(i);

		if (nearbyIndices.Count == 0)
			return PickNearestTemplateIndex(lane, templates, ref random);
		return nearbyIndices[random.Next(0, nearbyIndices.Count)];
	}

	static int PickNearestTemplateIndex(int lane, ExploreMapNodeTemplate[] templates, ref SeededMapRandom random)
	{
		int nearestDistance = int.MaxValue;
		var nearestIndices = new List<int>(templates.Length);
		for (int i = 0; i < templates.Length; i++)
		{
			int distance = System.Math.Abs(lane - templates[i].Lane);
			if (distance < nearestDistance)
			{
				nearestDistance = distance;
				nearestIndices.Clear();
			}
			if (distance == nearestDistance)
				nearestIndices.Add(i);
		}
		return nearestIndices[random.Next(0, nearestIndices.Count)];
	}

	static int FindTemplateIndexByLane(ExploreMapNodeTemplate[] templates, int lane)
	{
		if (templates == null)
			return -1;
		for (int i = 0; i < templates.Length; i++)
			if (templates[i].Lane == lane)
				return i;
		return -1;
	}

	static int FindTemplateIndexByNodeId(ExploreMapNodeTemplate[] templates, int row, string nodeId)
	{
		if (templates == null || string.IsNullOrEmpty(nodeId))
			return -1;
		for (int i = 0; i < templates.Length; i++)
			if (BuildNodeId(row, templates[i].Lane) == nodeId)
				return i;
		return -1;
	}

	static bool IsValidGenerationConfig(ExploreMapGenerationConfig config)
	{
		return config.MinNodesPerMiddleRow >= MinGeneratedNodesPerMiddleRow
			&& config.MaxNodesPerMiddleRow <= MaxGeneratedNodesPerMiddleRow
			&& config.MinNodesPerMiddleRow <= config.MaxNodesPerMiddleRow;
	}

	static string[] ResolveConnectedNextNodeIds(ExploreMapNodeTemplate[][] rows, int row, ExploreMapNodeTemplate template)
	{
		if (template.ConnectedNextNodeIds != null)
			return CopyNodeIds(template.ConnectedNextNodeIds);
		return BuildConnectedNextNodeIds(rows, row, template.Lane);
	}

	static string[] CopyNodeIds(string[] nodeIds)
	{
		if (nodeIds == null || nodeIds.Length == 0)
			return new string[0];
		var copy = new string[nodeIds.Length];
		for (int i = 0; i < nodeIds.Length; i++)
			copy[i] = nodeIds[i];
		return copy;
	}

	static string[] BuildConnectedNextNodeIds(ExploreMapNodeTemplate[][] rows, int row, int lane)
	{
		if (row < StartRow || row >= BossRow)
			return new string[0];

		var currentRowNodes = rows[row];
		var nextRowNodes = rows[row + 1];
		var connected = new List<string>(3);
		for (int i = 0; i < nextRowNodes.Length; i++)
		{
			int nextLane = nextRowNodes[i].Lane;
			if (currentRowNodes.Length == 1 || nextRowNodes.Length == 1 || System.Math.Abs(lane - nextLane) <= 1)
				connected.Add(BuildNodeId(row + 1, nextLane));
		}
		return connected.ToArray();
	}

	static string BuildNodeId(int row, int lane)
	{
		return $"r{row}l{lane}";
	}

	static int FindNodeIndex(ExploreMapNodeView[] nodes, string nodeId)
	{
		for (int i = 0; i < nodes.Length; i++)
			if (nodes[i].NodeId == nodeId)
				return i;
		return -1;
	}

	static int Clamp(int value, int min, int max)
	{
		if (value < min)
			return min;
		if (value > max)
			return max;
		return value;
	}

	readonly struct ExploreMapNodeTemplate
	{
		public readonly ExploreMapNodeKind Kind;
		public readonly int Lane;
		public readonly Vector2 Position;
		public readonly bool HasCustomPosition;
		public readonly string Title;
		public readonly string Description;
		public readonly string IconLabel;
		public readonly string[] ConnectedNextNodeIds;

		public ExploreMapNodeTemplate(
			ExploreMapNodeKind kind,
			int lane,
			string title,
			string description,
			string iconLabel,
			string[] connectedNextNodeIds = null)
			: this(kind, lane, Vector2.zero, false, title, description, iconLabel, connectedNextNodeIds)
		{
		}

		public ExploreMapNodeTemplate(
			ExploreMapNodeKind kind,
			int lane,
			Vector2 position,
			string title,
			string description,
			string iconLabel,
			string[] connectedNextNodeIds = null)
			: this(kind, lane, position, true, title, description, iconLabel, connectedNextNodeIds)
		{
		}

		ExploreMapNodeTemplate(
			ExploreMapNodeKind kind,
			int lane,
			Vector2 position,
			bool hasCustomPosition,
			string title,
			string description,
			string iconLabel,
			string[] connectedNextNodeIds)
		{
			Kind = kind;
			Lane = lane;
			Position = position;
			HasCustomPosition = hasCustomPosition;
			Title = title;
			Description = description;
			IconLabel = iconLabel;
			ConnectedNextNodeIds = connectedNextNodeIds;
		}

		public ExploreMapNodeTemplate WithConnectedNextNodeIds(string[] connectedNextNodeIds)
		{
			return new ExploreMapNodeTemplate(
				Kind,
				Lane,
				Position,
				HasCustomPosition,
				Title,
				Description,
				IconLabel,
				connectedNextNodeIds);
		}
	}

	struct SeededMapRandom
	{
		uint state;

		public SeededMapRandom(int seed)
		{
			state = unchecked((uint)seed);
			if (state == 0u)
				state = 0x9E3779B9u;
		}

		public int Next(int minInclusive, int maxExclusive)
		{
			if (maxExclusive <= minInclusive)
				return minInclusive;
			uint range = (uint)(maxExclusive - minInclusive);
			return minInclusive + (int)(NextUInt() % range);
		}

		uint NextUInt()
		{
			unchecked
			{
				uint value = state;
				value ^= value << 13;
				value ^= value >> 17;
				value ^= value << 5;
				state = value;
				return value;
			}
		}
	}
}
