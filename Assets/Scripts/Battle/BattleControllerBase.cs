using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// 모든 무기 배틀 씬 컨트롤러의 공통 베이스. 적/플레이어 정보 표시·스테이지 배경·기본 적 생성을 담당한다.
/// 무기별 전투 규칙(주사위·마작 등)은 파생 클래스에 남긴다.
///
/// 파생 클래스 요구사항:
///   - 빌더는 아래 protected 필드 이름과 동일한 SerializeField 이름을 유지해야 한다.
///   - 빌더가 persistent listener로 호출하는 OnEnemyPanel0~3Clicked 래퍼는 여기서 제공.
/// </summary>
public abstract class BattleControllerBase : MonoBehaviour
{
	[Header("배경 / 스테이지 (공통)")]
	[SerializeField] protected Image fightBackgroundImage;
	[SerializeField] protected StageSpriteBundle[] stageBundles;
	[SerializeField] protected Image playerBody;

	[Header("적 UI — 고정 4슬롯 (공통)")]
	[SerializeField] protected GameObject[] enemyPanels;
	[SerializeField] protected Image[] enemyBodies;
	[SerializeField] protected TMP_Text[] enemyNames;
	[SerializeField] protected Image[] enemyHpFills;
	[SerializeField] protected TMP_Text[] enemyHpTexts;
	[SerializeField] protected Image[] targetMarkers;
	[SerializeField] protected TMP_Text[] deadOverlays;
	[SerializeField] protected EnemySpriteAnimator[] enemyAnimators;

	[Header("HUD / Log / VFX (공통)")]
	[SerializeField] protected HeartDisplay heartDisplay;
	[SerializeField] protected BattleLog battleLog;
	[SerializeField] protected BattleDamageVFX vfx;
	[SerializeField] protected BattleAnimations battleAnims;

	protected List<EnemyInfo> enemies = new List<EnemyInfo>();
	protected int targetIndex;

	protected StageData ActiveStage => GameSessionManager.CurrentStage;

	// ── 스테이지 번들/배경 ──────────────────────────────────────────

	protected StageSpriteBundle FindBundle(string stageId)
	{
		if (stageBundles == null) return null;
		for (int i = 0; i < stageBundles.Length; i++)
			if (stageBundles[i] != null && stageBundles[i].stageId == stageId)
				return stageBundles[i];
		return null;
	}

	protected void ApplyStageBackground()
	{
		if (fightBackgroundImage == null) return;
		var stage = ActiveStage;
		if (stage == null) return;
		var bundle = FindBundle(stage.id);
		if (bundle != null && bundle.background != null)
		{
			fightBackgroundImage.sprite = bundle.background;
			fightBackgroundImage.color  = Color.white;
		}
		else
		{
			fightBackgroundImage.sprite = null;
			fightBackgroundImage.color  = stage.themeColor;
		}

		ApplyPlayerGroundOffset();
	}

	bool playerAnchorShifted;

	/// <summary>
	/// 스테이지별 playerGroundYOffset을 playerBody 앵커에 1회만 가산.
	/// (Start 경로에서 여러 번 호출되어도 중복 이동되지 않도록 1-shot).
	/// </summary>
	protected void ApplyPlayerGroundOffset()
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

	/// <summary>Explore 경유 없이 씬을 직접 실행한 경우 활성 스테이지 mobPool로 기본 적 4마리 채우기.</summary>
	protected void GenerateDefaultEnemies()
	{
		GameSessionManager.BattleEnemies.Clear();
		var stage = ActiveStage;
		if (stage == null || stage.mobPool == null || stage.mobPool.Count == 0)
		{
			Debug.LogWarning($"[{GetType().Name}] GenerateDefaultEnemies: 활성 스테이지에 mobPool이 비어있음");
			return;
		}
		var bundle = FindBundle(stage.id);
		int count = Mathf.Min(4, stage.mobPool.Count);
		for (int i = 0; i < count; i++)
		{
			var def = stage.mobPool[i];
			int hp = Random.Range(def.hpMin, def.hpMax + 1);
			Sprite spr = (bundle != null && bundle.mobSprites != null && i < bundle.mobSprites.Length)
				? bundle.mobSprites[i]
				: null;
			GameSessionManager.BattleEnemies.Add(new EnemyInfo(def.name, hp, def.rank, def.themeColor, spr));
		}
	}

	// ── 적 표시 ─────────────────────────────────────────────────

	/// <summary>몹 이름으로 MobDef 조회. 없으면 기본 앵커 반환.</summary>
	protected MobDef TryGetMobDef(string mobName)
	{
		var stage = ActiveStage;
		if (stage == null) return null;
		return stage.FindMob(mobName);
	}

	/// <summary>
	/// 모든 적 슬롯에 대해 스프라이트/이름/HP/타겟마커/사망오버레이를 배치.
	/// 몹별 MobDef.bodyYMin/Max, borderThickness, infoPanelGap을 반영.
	/// 보스 배틀은 0번 슬롯을 중앙 전체로 확장.
	/// </summary>
	protected void SetupEnemyDisplay()
	{
		if (enemyPanels == null) return;
		for (int i = 0; i < enemyPanels.Length; i++)
		{
			bool active = i < enemies.Count;
			if (enemyPanels[i] != null) enemyPanels[i].SetActive(active);
			if (!active) continue;

			var e = enemies[i];
			ApplyEnemyVisual(i, e);

			if (enemyNames != null && i < enemyNames.Length && enemyNames[i] != null)
				enemyNames[i].text = $"{e.name}  <color=#FFD94A>{e.RankStars}</color>";

			UpdateEnemyHp(i);

			if (targetMarkers != null && i < targetMarkers.Length && targetMarkers[i] != null)
				targetMarkers[i].gameObject.SetActive(i == targetIndex && e.IsAlive);
			if (deadOverlays != null && i < deadOverlays.Length && deadOverlays[i] != null)
				deadOverlays[i].gameObject.SetActive(!e.IsAlive);

			ApplyMobAnchors(i, e);
		}
	}

	void ApplyEnemyVisual(int i, EnemyInfo e)
	{
		if (enemyBodies == null || i >= enemyBodies.Length || enemyBodies[i] == null) return;
		var animSet = ResolveEnemyAnimationSet(e.name);
		if (e.sprite != null)
		{
			enemyBodies[i].sprite = e.sprite;
			enemyBodies[i].color = Color.white;
			enemyBodies[i].preserveAspect = true;
		}
		else
		{
			enemyBodies[i].sprite = null;
			enemyBodies[i].color = e.color;
			enemyBodies[i].preserveAspect = false;
		}

		if (enemyAnimators != null && i < enemyAnimators.Length && enemyAnimators[i] != null)
			enemyAnimators[i].Bind(animSet, e.sprite);
	}

	protected EnemySpriteAnimationSet ResolveEnemyAnimationSet(string mobName)
	{
		var stage = ActiveStage;
		if (stage == null) return null;
		var bundle = FindBundle(stage.id);
		int mobIndex = stage.IndexOfMob(mobName);
		if (bundle == null || bundle.mobAnimations == null)
			return null;
		if (mobIndex < 0 || mobIndex >= bundle.mobAnimations.Length)
			return null;
		return bundle.mobAnimations[mobIndex];
	}

	protected Coroutine PlayEnemyAttackAnimation(int index)
	{
		if (enemyAnimators == null || index < 0 || index >= enemyAnimators.Length)
			return null;
		return enemyAnimators[index] != null ? enemyAnimators[index].PlayAttack() : null;
	}

	protected Coroutine PlayEnemyHitAnimation(int index)
	{
		if (enemyAnimators == null || index < 0 || index >= enemyAnimators.Length)
			return null;
		return enemyAnimators[index] != null ? enemyAnimators[index].PlayHit() : null;
	}

	protected bool EnemyHasHitAnimation(int index)
	{
		return enemyAnimators != null
			&& index >= 0
			&& index < enemyAnimators.Length
			&& enemyAnimators[index] != null
			&& enemyAnimators[index].HasHitSprites;
	}

	void ApplyMobAnchors(int i, EnemyInfo e)
	{
		var bodyRt = (enemyBodies != null && i < enemyBodies.Length && enemyBodies[i] != null)
			? enemyBodies[i].rectTransform : null;

		if (GameSessionManager.IsBossBattle && i == 0)
		{
			// 보스는 슬롯을 중앙 전체로 확장. 좌우반전은 스프라이트 에셋에서 처리.
			var rt = enemyPanels[i].GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(0.25f, 0f);
			rt.anchorMax = new Vector2(0.75f, 1f);
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			return;
		}

		var def = TryGetMobDef(e.name);
		float yMin = def != null ? def.bodyYMin : 0.00f;
		float yMax = def != null ? def.bodyYMax : 0.75f;
		float xMin = def != null ? def.bodyXMin : 0.05f;
		float xMax = def != null ? def.bodyXMax : 0.95f;
		float gap  = def != null ? def.infoPanelGap : 0.18f;
		float bt   = def != null ? def.borderThickness : 0.05f;

		if (bodyRt != null)
		{
			bodyRt.anchorMin = new Vector2(xMin, yMin);
			bodyRt.anchorMax = new Vector2(xMax, yMax);
			bodyRt.offsetMin = Vector2.zero;
			bodyRt.offsetMax = Vector2.zero;
		}

		if (targetMarkers != null && i < targetMarkers.Length && targetMarkers[i] != null)
		{
			var mRt = targetMarkers[i].rectTransform;
			mRt.anchorMin = new Vector2(xMin, yMin);
			mRt.anchorMax = new Vector2(xMax, yMax);
			mRt.offsetMin = Vector2.zero;
			mRt.offsetMax = Vector2.zero;
			ApplyBorderThickness(mRt, bt);
		}

		if (deadOverlays != null && i < deadOverlays.Length && deadOverlays[i] != null)
		{
			var dRt = deadOverlays[i].rectTransform;
			dRt.anchorMin = new Vector2(xMin, yMin);
			dRt.anchorMax = new Vector2(xMax, yMax);
			dRt.offsetMin = Vector2.zero;
			dRt.offsetMax = Vector2.zero;
		}

		if (enemyNames != null && i < enemyNames.Length && enemyNames[i] != null)
		{
			var infoRt = enemyNames[i].transform.parent as RectTransform;
			if (infoRt != null)
			{
				infoRt.anchorMin = new Vector2(0f, yMax);
				infoRt.anchorMax = new Vector2(1f, yMax + gap);
				infoRt.offsetMin = Vector2.zero;
				infoRt.offsetMax = Vector2.zero;
			}
		}
	}

	/// <summary>TargetMarker 하위 BorderTop/Bottom/Left/Right 이미지의 앵커를 thickness에 맞게 재계산.</summary>
	static void ApplyBorderThickness(RectTransform marker, float t)
	{
		if (marker == null) return;
		var top    = marker.Find("BorderTop")    as RectTransform;
		var bottom = marker.Find("BorderBottom") as RectTransform;
		var left   = marker.Find("BorderLeft")   as RectTransform;
		var right  = marker.Find("BorderRight")  as RectTransform;
		if (top != null)
		{
			top.anchorMin = new Vector2(0f, 1f - t);
			top.anchorMax = Vector2.one;
			top.offsetMin = Vector2.zero; top.offsetMax = Vector2.zero;
		}
		if (bottom != null)
		{
			bottom.anchorMin = Vector2.zero;
			bottom.anchorMax = new Vector2(1f, t);
			bottom.offsetMin = Vector2.zero; bottom.offsetMax = Vector2.zero;
		}
		if (left != null)
		{
			left.anchorMin = new Vector2(0f, t);
			left.anchorMax = new Vector2(t, 1f - t);
			left.offsetMin = Vector2.zero; left.offsetMax = Vector2.zero;
		}
		if (right != null)
		{
			right.anchorMin = new Vector2(1f - t, t);
			right.anchorMax = new Vector2(1f, 1f - t);
			right.offsetMin = Vector2.zero; right.offsetMax = Vector2.zero;
		}
	}

	protected void UpdateEnemyHp(int i)
	{
		if (i < 0 || i >= enemies.Count) return;
		var e = enemies[i];
		if (enemyHpFills != null && i < enemyHpFills.Length && enemyHpFills[i] != null)
			enemyHpFills[i].fillAmount = e.maxHp > 0 ? (float)e.hp / e.maxHp : 0f;
		if (enemyHpTexts != null && i < enemyHpTexts.Length && enemyHpTexts[i] != null)
			enemyHpTexts[i].text = $"{e.hp} / {e.maxHp}";
		if (deadOverlays != null && i < deadOverlays.Length && deadOverlays[i] != null)
			deadOverlays[i].gameObject.SetActive(!e.IsAlive);
	}

	protected void RefreshAllEnemyHp()
	{
		for (int i = 0; i < enemies.Count; i++) UpdateEnemyHp(i);
	}

	protected void RefreshTargetMarkers()
	{
		if (targetMarkers == null) return;
		for (int i = 0; i < targetMarkers.Length && i < enemies.Count; i++)
		{
			if (targetMarkers[i] != null)
				targetMarkers[i].gameObject.SetActive(i == targetIndex && enemies[i].IsAlive);
		}
	}

	// ── 적 패널 클릭 래퍼 (PersistentListener가 이름 기반으로 바인딩) ──
	public void OnEnemyPanel0Clicked() => OnEnemyPanelClicked(0);
	public void OnEnemyPanel1Clicked() => OnEnemyPanelClicked(1);
	public void OnEnemyPanel2Clicked() => OnEnemyPanelClicked(2);
	public void OnEnemyPanel3Clicked() => OnEnemyPanelClicked(3);

	/// <summary>기본 구현: 살아있는 적으로 타겟 변경. 파생 클래스가 필요하면 override.</summary>
	public virtual void OnEnemyPanelClicked(int index)
	{
		if (index < 0 || index >= enemies.Count) return;
		if (!enemies[index].IsAlive) return;
		targetIndex = index;
		RefreshTargetMarkers();
	}
}
