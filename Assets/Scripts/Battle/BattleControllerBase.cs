using System.Collections;
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
	public const float EnemyVisualFramePadding = 8f;
	public const float EnemyVisualUiPadding = 12f;
	public const float EnemyInfoPanelHeight = 66f;
	public const float EnemyInfoMinWidth = 150f;
	public const float EnemyInfoMaxWidth = 330f;
	public const float EnemyHpMinWidth = 104f;
	public const float EnemyHpMaxWidth = 280f;
	const float EnemyHpBarHeight = 18f;
	const float EnemyCanvasEdgePadding = 8f;

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
	[SerializeField] protected Image[] enemyIdleProjectiles;
	[SerializeField] protected float enemyDeathGroundY = 0.5f;

	[Header("HUD / Log / VFX (공통)")]
	[SerializeField] protected HeartDisplay heartDisplay;
	[SerializeField] protected BattleLog battleLog;
	[SerializeField] protected BattleDamageVFX vfx;
	[SerializeField] protected BattleAnimations battleAnims;

	protected List<EnemyInfo> enemies = new List<EnemyInfo>();
	protected int targetIndex;
	Coroutine[] debugMobSpriteRoutines;
	Vector3[] debugMobHomeLocalPositions;
	bool[] debugMobHomeCaptured;

	protected StageData ActiveStage => GameSessionManager.CurrentStage;

	protected void EnsureSessionBattleEnemies(string logPrefix)
	{
		if (GameSessionManager.HasBattleEnemies)
			return;

		Debug.LogWarning($"[{logPrefix}] BattleEnemies가 비어 있음 — 기본 적 생성");
		GenerateDefaultEnemies();
	}

	protected void LoadSessionEnemiesSnapshot()
	{
		enemies = GameSessionManager.SnapshotBattleEnemies();
		targetIndex = FindFirstAliveEnemyIndex(enemies);
	}

	protected int FindFirstAliveEnemyIndex(List<EnemyInfo> source)
	{
		if (source == null)
			return 0;

		for (int i = 0; i < source.Count; i++)
		{
			if (source[i] != null && source[i].IsAlive)
				return i;
		}
		return 0;
	}

	protected void LogBattleIntro()
	{
		if (battleLog == null)
			return;

		battleLog.Clear();
		var lines = new List<string>
		{
			GameSessionManager.IsBossBattle
				? "<color=#FF5555>강적과 조우했다!</color>"
				: "적과 조우했다!"
		};
		foreach (var e in enemies)
			lines.Add($"{e.name} <color=#FFD94A>{e.RankStars}</color> <color=#AAAAAA>(HP {e.maxHp})</color>");
		battleLog.AddEntry(string.Join("\n", lines), BattleEventPresentation.LogAndPopup);
	}

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
		var stage = ActiveStage;
		if (stage == null || stage.mobPool == null || stage.mobPool.Count == 0)
		{
			Debug.LogWarning($"[{GetType().Name}] GenerateDefaultEnemies: 활성 스테이지에 mobPool이 비어있음");
			return;
		}
		var bundle = FindBundle(stage.id);
		int count = Mathf.Min(4, stage.mobPool.Count);
		var generated = new List<EnemyInfo>(count);
		for (int i = 0; i < count; i++)
		{
			var def = stage.mobPool[i];
			int hp = Random.Range(def.hpMin, def.hpMax + 1);
			Sprite spr = (bundle != null && bundle.mobSprites != null && i < bundle.mobSprites.Length)
				? bundle.mobSprites[i]
				: null;
			generated.Add(new EnemyInfo(def.name, hp, def.rank, def.themeColor, spr));
		}
		GameSessionManager.PrepareBattleEnemies(generated, false);
	}

	// ── 적 표시 ─────────────────────────────────────────────────

	/// <summary>몹 이름으로 MobDef 조회. 없으면 기본 앵커 반환.</summary>
	protected MobDef TryGetMobDef(string mobName)
	{
		var stage = ActiveStage;
		if (stage == null) return null;
		return stage.FindMob(mobName);
	}

	protected string ResolveEnemyDiceProfileId(string enemyName)
	{
		var stage = ActiveStage;
		if (stage == null)
			return EnemyDiceProfile.DefaultId;

		if (GameSessionManager.IsBossBattle
			&& stage.boss != null
			&& stage.boss.name == enemyName
			&& !string.IsNullOrWhiteSpace(stage.boss.enemyDiceProfileId))
			return stage.boss.enemyDiceProfileId;

		var def = stage.FindMob(enemyName);
		if (def != null && !string.IsNullOrWhiteSpace(def.enemyDiceProfileId))
			return def.enemyDiceProfileId;

		return EnemyDiceStyleResolver.ResolveProfileId(enemyName);
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
			ApplyMobAnchors(i, e);

			if (enemyNames != null && i < enemyNames.Length && enemyNames[i] != null)
				enemyNames[i].text = $"{e.name}  <color=#FFD94A>{e.RankStars}</color>";

			UpdateEnemyHp(i);

			if (targetMarkers != null && i < targetMarkers.Length && targetMarkers[i] != null)
				targetMarkers[i].gameObject.SetActive(i == targetIndex && e.IsAlive);
			if (deadOverlays != null && i < deadOverlays.Length && deadOverlays[i] != null)
				deadOverlays[i].gameObject.SetActive(ShouldShowDeadOverlay(i, e));
		}
		RefreshEnemyVisualLayouts();
		CaptureDebugMobHomePositions();
	}

	void LateUpdate()
	{
		RefreshEnemyVisualLayouts();
	}

	void ApplyEnemyVisual(int i, EnemyInfo e)
	{
		if (enemyBodies == null || i >= enemyBodies.Length || enemyBodies[i] == null) return;
		var def = TryGetMobDef(e.name);
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
		{
			enemyAnimators[i].Bind(animSet, e.sprite);
			enemyAnimators[i].SetAttackVisualOverride(
				def != null ? def.attackVisualScaleMultiplier : 1f,
				def != null ? def.attackVisualOffset : Vector2.zero,
				def != null && def.attackUseFullTextureFrames);
		}

		ApplyEnemyIdleProjectile(i, e);
	}

	protected EnemySpriteAnimationSet ResolveEnemyAnimationSet(string mobName)
	{
		var stage = ActiveStage;
		if (stage == null) return null;
		var bundle = FindBundle(stage.id);
		if (GameSessionManager.IsBossBattle && stage.boss != null && stage.boss.name == mobName)
			return bundle != null ? bundle.bossAnimation : null;
		int mobIndex = stage.IndexOfMob(mobName);
		if (bundle == null || bundle.mobAnimations == null)
			return null;
		if (mobIndex < 0 || mobIndex >= bundle.mobAnimations.Length)
			return null;
		return bundle.mobAnimations[mobIndex];
	}

	protected bool HasEnemyDeathAnimation(int i, EnemyInfo e)
	{
		if (e == null || e.IsAlive)
			return false;
		if (enemyAnimators == null || i < 0 || i >= enemyAnimators.Length)
			return false;
		return enemyAnimators[i] != null && enemyAnimators[i].HasDeathAnimation;
	}

	protected bool ShouldShowDeadOverlay(int i, EnemyInfo e)
	{
		return e != null && !e.IsAlive && !HasEnemyDeathAnimation(i, e);
	}

	protected Sprite ResolveEnemyProjectileSprite(string mobName)
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

	protected Sprite ResolveEnemyAttackVfxSprite(string mobName)
	{
		var stage = ActiveStage;
		if (stage == null) return null;
		var bundle = FindBundle(stage.id);
		int mobIndex = stage.IndexOfMob(mobName);
		if (bundle == null || bundle.mobAttackVfxSprites == null)
			return null;
		if (mobIndex < 0 || mobIndex >= bundle.mobAttackVfxSprites.Length)
			return null;
		return bundle.mobAttackVfxSprites[mobIndex];
	}

	void ApplyEnemyIdleProjectile(int i, EnemyInfo e)
	{
		if (enemyIdleProjectiles == null || i < 0 || i >= enemyIdleProjectiles.Length)
			return;
		var projectile = enemyIdleProjectiles[i];
		if (projectile == null)
			return;

		var def = TryGetMobDef(e.name);
		Sprite sprite = ResolveEnemyProjectileSprite(e.name);
		bool show = e.IsAlive && def != null && !string.IsNullOrEmpty(def.projectileSpritePath) && sprite != null;
		projectile.gameObject.SetActive(show);
		var follower = projectile.GetComponent<EnemyProjectileAttachmentFollower>();
		if (follower != null)
			follower.SetFollowing(show);
		if (!show)
			return;

		projectile.sprite = sprite;
		projectile.color = Color.white;
		projectile.preserveAspect = true;
		projectile.raycastTarget = false;
		var rt = projectile.rectTransform;
		if (follower != null)
		{
			follower.ApplyPose(enemyAnimators != null && i < enemyAnimators.Length && enemyAnimators[i] != null
				? enemyAnimators[i].CurrentFrameNormalized
				: 0f);
		}
		else
		{
			rt.anchorMin = new Vector2(-0.02f, 0.48f);
			rt.anchorMax = new Vector2(0.62f, 0.59f);
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			rt.localRotation = Quaternion.identity;
			rt.localScale = Vector3.one;
		}
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

	protected void PlayEnemyDamagedFeedback(int index)
	{
		PlayEnemyHitAnimation(index);
		if (battleAnims == null || enemyBodies == null || index < 0 || index >= enemyBodies.Length)
			return;
		battleAnims.FlashDamage(enemyBodies[index]);
	}

	void ApplyMobAnchors(int i, EnemyInfo e)
	{
		var bodyRt = (enemyBodies != null && i < enemyBodies.Length && enemyBodies[i] != null)
			? enemyBodies[i].rectTransform : null;

		if (GameSessionManager.IsBossBattle && i == 0)
		{
			// 보스는 슬롯을 중앙 전체로 확장하고 플레이어 쪽을 보도록 좌우 반전한다.
			var rt = enemyPanels[i].GetComponent<RectTransform>();
			rt.anchorMin = new Vector2(0.25f, 0f);
			rt.anchorMax = new Vector2(0.75f, 1f);
			rt.offsetMin = Vector2.zero;
			rt.offsetMax = Vector2.zero;
			if (bodyRt != null)
			{
				bodyRt.anchorMin = new Vector2(0.05f, 0f);
				bodyRt.anchorMax = new Vector2(0.95f, 0.90f);
				bodyRt.offsetMin = Vector2.zero;
				bodyRt.offsetMax = Vector2.zero;
				bodyRt.pivot = new Vector2(0.5f, 0.5f);
				bodyRt.anchoredPosition = Vector2.zero;
				bodyRt.localRotation = Quaternion.identity;
				bodyRt.localScale = ResolveEnemyBodyDisplayScale(true, i, bodyRt.localScale);
			}
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
			bodyRt.pivot = new Vector2(0.5f, 0.5f);
			bodyRt.anchoredPosition = Vector2.zero;
			bodyRt.localRotation = Quaternion.identity;
			bodyRt.localScale = ResolveEnemyBodyDisplayScale(false, i, bodyRt.localScale);
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

	public static Vector3 ResolveEnemyBodyDisplayScale(bool bossBattle, int enemyIndex, Vector3 currentScale)
	{
		float x = Mathf.Approximately(currentScale.x, 0f) ? 1f : Mathf.Abs(currentScale.x);
		float y = Mathf.Approximately(currentScale.y, 0f) ? 1f : Mathf.Abs(currentScale.y);
		float z = Mathf.Approximately(currentScale.z, 0f) ? 1f : Mathf.Abs(currentScale.z);

		if (bossBattle && enemyIndex == 0)
			x = -x;

		return new Vector3(x, y, z);
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
		RefreshEnemyLifeVisual(i, e);
		RefreshEnemyVisualLayout(i);
	}

	protected void RefreshAllEnemyHp()
	{
		for (int i = 0; i < enemies.Count; i++) UpdateEnemyHp(i);
	}

	protected void RefreshEnemyLifeVisual(int i, EnemyInfo e)
	{
		if (e == null) return;

		bool alive = e.IsAlive;
		if (enemyIdleProjectiles != null && i >= 0 && i < enemyIdleProjectiles.Length && enemyIdleProjectiles[i] != null)
		{
			bool showProjectile = alive && ResolveEnemyProjectileSprite(e.name) != null;
			enemyIdleProjectiles[i].gameObject.SetActive(showProjectile);
			var follower = enemyIdleProjectiles[i].GetComponent<EnemyProjectileAttachmentFollower>();
			if (follower != null)
				follower.SetFollowing(showProjectile);
		}
		if (deadOverlays != null && i >= 0 && i < deadOverlays.Length && deadOverlays[i] != null)
			deadOverlays[i].gameObject.SetActive(ShouldShowDeadOverlay(i, e));
		if (targetMarkers != null && i >= 0 && i < targetMarkers.Length && targetMarkers[i] != null && !alive)
			targetMarkers[i].gameObject.SetActive(false);

		if (!alive)
			ApplyEnemyDeathPose(i, e);
	}

	void ApplyEnemyDeathPose(int i, EnemyInfo e)
	{
		if (enemyBodies == null || i < 0 || i >= enemyBodies.Length || enemyBodies[i] == null)
			return;

		var body = enemyBodies[i];
		if (enemyAnimators != null && i < enemyAnimators.Length && enemyAnimators[i] != null)
		{
			var animator = enemyAnimators[i];
			if (animator.HasDeathAnimation)
			{
				if (animator.IsDeathLocked)
					return;
				ApplyMobAnchors(i, e);
				var deathBodyRt = body.rectTransform;
				deathBodyRt.pivot = new Vector2(0.5f, 0.5f);
				deathBodyRt.anchoredPosition = Vector2.zero;
				deathBodyRt.sizeDelta = Vector2.zero;
				deathBodyRt.localRotation = Quaternion.identity;
				body.preserveAspect = true;
				body.color = Color.white;
				var deathDef = TryGetMobDef(e.name);
				var fallPlan = ResolveDeathFallPlan(deathBodyRt, deathDef);
				animator.PlayDeathAndHold(e.sprite, fallPlan.fallToGround,
					fallPlan.targetAnchorMin, fallPlan.targetAnchorMax);
				return;
			}
			animator.StopOnFirstIdle(e.sprite);
		}
		else if (e.sprite != null)
			body.sprite = e.sprite;

		body.preserveAspect = body.sprite != null;
		if (body.sprite != null)
			body.color = Color.white;

		var bodyRt = body.rectTransform;
		var def = TryGetMobDef(e.name);
		float xMin = def != null ? def.bodyXMin : 0.05f;
		float xMax = def != null ? def.bodyXMax : 0.95f;
		float yMin = def != null ? def.bodyYMin : 0.00f;
		float yMax = def != null ? def.bodyYMax : 0.75f;
		float xCenter = (xMin + xMax) * 0.5f;
		float widthNormalized = Mathf.Max(0.05f, xMax - xMin);
		float heightNormalized = Mathf.Max(0.05f, yMax - yMin);
		float slotWidth = 150f;
		float slotHeight = 150f;
		if (enemyPanels != null && i < enemyPanels.Length && enemyPanels[i] != null)
		{
			var slotRt = enemyPanels[i].GetComponent<RectTransform>();
			if (slotRt != null)
			{
				slotWidth = Mathf.Max(1f, slotRt.rect.width);
				slotHeight = Mathf.Max(1f, slotRt.rect.height);
			}
		}

		float groundYInParent = GameSessionManager.IsBossBattle && i == 0 ? enemyDeathGroundY : 0f;
		bodyRt.anchorMin = new Vector2(xCenter, groundYInParent);
		bodyRt.anchorMax = new Vector2(xCenter, groundYInParent);
		bodyRt.pivot = new Vector2(0.5f, 0f);
		bodyRt.anchoredPosition = Vector2.zero;
		bodyRt.sizeDelta = new Vector2(slotWidth * widthNormalized, slotHeight * heightNormalized);
		bodyRt.localRotation = Quaternion.Euler(0f, 0f, -90f);
	}

	(bool fallToGround, Vector2 targetAnchorMin, Vector2 targetAnchorMax) ResolveDeathFallPlan(
		RectTransform bodyRt, MobDef deathDef)
	{
		Vector2 targetMin = bodyRt != null ? bodyRt.anchorMin : Vector2.zero;
		Vector2 targetMax = bodyRt != null ? bodyRt.anchorMax : Vector2.zero;
		bool fallToGround = deathDef != null && deathDef.deathFallToGround;
		if (!fallToGround)
			return (false, targetMin, targetMax);

		float height = Mathf.Max(0.05f, targetMax.y - targetMin.y);
		targetMin.y = 0f;
		targetMax.y = height;
		return (true, targetMin, targetMax);
	}

	protected IEnumerator WaitForEnemyDeathAnimations(float maxWaitSeconds)
	{
		float startedAt = Time.time;
		while (Time.time - startedAt < maxWaitSeconds)
		{
			bool anyPlaying = false;
			if (enemyAnimators != null)
			{
				for (int i = 0; i < enemyAnimators.Length; i++)
				{
					if (enemyAnimators[i] != null && enemyAnimators[i].IsDeathAnimationPlaying)
					{
						anyPlaying = true;
						break;
					}
				}
			}

			if (!anyPlaying)
				yield break;
			yield return null;
		}
	}

	protected void RefreshTargetMarkers()
	{
		if (targetMarkers == null) return;
		RefreshEnemyVisualLayouts();
		for (int i = 0; i < targetMarkers.Length && i < enemies.Count; i++)
		{
			if (targetMarkers[i] != null)
				targetMarkers[i].gameObject.SetActive(i == targetIndex && enemies[i].IsAlive);
		}
	}

	protected void RefreshEnemyVisualLayouts()
	{
		if (enemyPanels == null || enemyBodies == null)
			return;

		int count = Mathf.Min(enemyPanels.Length, enemyBodies.Length);
		for (int i = 0; i < count; i++)
			RefreshEnemyVisualLayout(i);
	}

	protected void RefreshEnemyVisualLayout(int index)
	{
		if (enemyPanels == null || enemyBodies == null)
			return;
		if (index < 0 || index >= enemyPanels.Length || index >= enemyBodies.Length)
			return;
		if (enemyPanels[index] == null || enemyBodies[index] == null)
			return;
		if (!enemyPanels[index].activeSelf)
			return;

		var slot = enemyPanels[index].GetComponent<RectTransform>();
		if (slot == null)
			return;
		if (!EnemyVisualBoundsResolver.TryResolveBoundsIn(enemyBodies[index], slot, out var visualBounds))
			return;

		ApplyEnemyVisualFrameLayout(index, slot, visualBounds);
		ApplyEnemyInfoPanelLayout(index, slot, visualBounds);
	}

	void ApplyEnemyVisualFrameLayout(int index, RectTransform slot, EnemyVisualBounds visualBounds)
	{
		Rect frameRect = visualBounds.Padded(EnemyVisualFramePadding).rect;
		if (targetMarkers != null && index < targetMarkers.Length && targetMarkers[index] != null)
		{
			var markerRt = targetMarkers[index].rectTransform;
			SetRectInParentLocal(markerRt, slot, frameRect, new Vector2(0.5f, 0.5f));
		}

		if (deadOverlays != null && index < deadOverlays.Length && deadOverlays[index] != null)
		{
			var deadRt = deadOverlays[index].rectTransform;
			SetRectInParentLocal(deadRt, slot, frameRect, new Vector2(0.5f, 0.5f));
		}
	}

	void ApplyEnemyInfoPanelLayout(int index, RectTransform slot, EnemyVisualBounds visualBounds)
	{
		if (enemyNames == null || index >= enemyNames.Length || enemyNames[index] == null)
			return;

		var infoRt = enemyNames[index].transform.parent as RectTransform;
		if (infoRt == null)
			return;

		float hpWidth = Mathf.Clamp(visualBounds.width, EnemyHpMinWidth, EnemyHpMaxWidth);
		float infoWidth = Mathf.Clamp(Mathf.Max(hpWidth + 28f, visualBounds.width + 24f),
			EnemyInfoMinWidth, EnemyInfoMaxWidth);
		Rect infoRect = Rect.MinMaxRect(
			visualBounds.center.x - infoWidth * 0.5f,
			visualBounds.yMax + EnemyVisualUiPadding,
			visualBounds.center.x + infoWidth * 0.5f,
			visualBounds.yMax + EnemyVisualUiPadding + EnemyInfoPanelHeight);
		infoRect = ClampRectToCanvasInParent(infoRect, slot);
		SetRectInParentLocal(infoRt, slot, infoRect, new Vector2(0.5f, 0f));
		ApplyInfoPanelChildLayout(index, hpWidth);
	}

	void ApplyInfoPanelChildLayout(int index, float hpWidth)
	{
		if (enemyNames != null && index < enemyNames.Length && enemyNames[index] != null)
		{
			var nameRt = enemyNames[index].rectTransform;
			nameRt.anchorMin = new Vector2(0f, 0.333f);
			nameRt.anchorMax = Vector2.one;
			nameRt.offsetMin = Vector2.zero;
			nameRt.offsetMax = Vector2.zero;
		}

		RectTransform hpBgRt = null;
		if (enemyHpFills != null && index < enemyHpFills.Length && enemyHpFills[index] != null)
			hpBgRt = enemyHpFills[index].transform.parent as RectTransform;
		if (hpBgRt != null)
		{
			hpBgRt.anchorMin = new Vector2(0.5f, 0.1665f);
			hpBgRt.anchorMax = new Vector2(0.5f, 0.1665f);
			hpBgRt.pivot = new Vector2(0.5f, 0.5f);
			hpBgRt.anchoredPosition = Vector2.zero;
			hpBgRt.sizeDelta = new Vector2(hpWidth, EnemyHpBarHeight);
		}

		if (enemyHpTexts != null && index < enemyHpTexts.Length && enemyHpTexts[index] != null)
		{
			var hpTextRt = enemyHpTexts[index].rectTransform;
			hpTextRt.anchorMin = new Vector2(0f, 0f);
			hpTextRt.anchorMax = new Vector2(1f, 0.333f);
			hpTextRt.offsetMin = Vector2.zero;
			hpTextRt.offsetMax = Vector2.zero;
		}
	}

	static Rect ClampRectToCanvasInParent(Rect rect, RectTransform parent)
	{
		if (parent == null)
			return rect;

		var canvas = parent.GetComponentInParent<Canvas>();
		var canvasRt = canvas != null ? canvas.transform as RectTransform : null;
		if (canvasRt == null)
			return rect;
		if (!EnemyVisualBoundsResolver.TryResolveRectTransformBoundsIn(canvasRt, parent, out var canvasBounds))
			return rect;

		Rect allowed = canvasBounds.rect;
		allowed.xMin += EnemyCanvasEdgePadding;
		allowed.xMax -= EnemyCanvasEdgePadding;
		allowed.yMin += EnemyCanvasEdgePadding;
		allowed.yMax -= EnemyCanvasEdgePadding;
		if (allowed.width <= 0f || allowed.height <= 0f)
			return rect;

		if (rect.width <= allowed.width)
		{
			if (rect.xMin < allowed.xMin)
				rect.x += allowed.xMin - rect.xMin;
			if (rect.xMax > allowed.xMax)
				rect.x -= rect.xMax - allowed.xMax;
		}

		if (rect.height <= allowed.height)
		{
			if (rect.yMin < allowed.yMin)
				rect.y += allowed.yMin - rect.yMin;
			if (rect.yMax > allowed.yMax)
				rect.y -= rect.yMax - allowed.yMax;
		}
		return rect;
	}

	static void SetRectInParentLocal(RectTransform target, RectTransform parent, Rect rect, Vector2 pivot)
	{
		if (target == null || parent == null)
			return;

		target.anchorMin = new Vector2(0.5f, 0.5f);
		target.anchorMax = new Vector2(0.5f, 0.5f);
		target.pivot = pivot;
		target.sizeDelta = new Vector2(Mathf.Max(1f, rect.width), Mathf.Max(1f, rect.height));
		Vector2 pivotPoint = new Vector2(
			Mathf.Lerp(rect.xMin, rect.xMax, pivot.x),
			Mathf.Lerp(rect.yMin, rect.yMax, pivot.y));
		target.anchoredPosition = pivotPoint - parent.rect.center;
		target.localRotation = Quaternion.identity;
		target.localScale = Vector3.one;
	}

	protected string DebugPlayBattleSprite(string target, int objectIndex, string spriteKind,
		PlayerBodyAnimator playerBodyAnimator, float loopSeconds)
	{
		if (string.IsNullOrWhiteSpace(target))
			return "[오류] sprite 대상이 비어 있습니다. 현재 지원: player, mob";

		switch (target.Trim().ToLowerInvariant())
		{
			case "player":
				return DebugPlayPlayerSprite(playerBodyAnimator, spriteKind, loopSeconds);
			case "mob":
				return DebugPlayMobSprite(objectIndex, spriteKind);
			default:
				return $"[오류] 알 수 없는 sprite 대상: {target}. 현재 지원: player, mob";
		}
	}

	string DebugPlayPlayerSprite(PlayerBodyAnimator playerBodyAnimator, string spriteKind, float loopSeconds)
	{
		if (playerBodyAnimator == null)
			return "[오류] PlayerBodyAnimator 미할당.";

		return playerBodyAnimator.TryPlayDebugSpriteKind(spriteKind, out string message, loopSeconds)
			? $"[Debug] {message}"
			: $"[오류] {message}";
	}

	string DebugPlayMobSprite(int index, string spriteKind)
	{
		if (enemies == null || index < 0 || index >= enemies.Count)
			return $"[오류] mob 인덱스 범위 밖: {index} (유효: 0~{(enemies != null ? enemies.Count - 1 : -1)})";
		if (enemyAnimators == null || index >= enemyAnimators.Length || enemyAnimators[index] == null)
			return $"[오류] mob {index} EnemySpriteAnimator 미할당.";

		string normalizedSpriteKind = NormalizeSpriteKind(spriteKind);
		if (normalizedSpriteKind == "attack")
			return DebugPlayMobAttackSprite(index);

		StopDebugMobSpriteRoutine(index, resetLocalPosition: true);
		var enemy = enemies[index];
		if (normalizedSpriteKind == "death")
		{
			ApplyMobAnchors(index, enemy);
			if (enemyBodies != null && index < enemyBodies.Length && enemyBodies[index] != null)
			{
				var body = enemyBodies[index];
				var bodyRt = body.rectTransform;
				bodyRt.pivot = new Vector2(0.5f, 0.5f);
				bodyRt.anchoredPosition = Vector2.zero;
				bodyRt.sizeDelta = Vector2.zero;
				bodyRt.localRotation = Quaternion.identity;
				body.preserveAspect = true;
				body.color = Color.white;
			}
		}
		if (normalizedSpriteKind == "death"
			&& deadOverlays != null
			&& index < deadOverlays.Length
			&& deadOverlays[index] != null)
		{
			deadOverlays[index].gameObject.SetActive(false);
		}

		Sprite fallbackSprite = enemy != null ? enemy.sprite : null;
		if (normalizedSpriteKind == "death")
		{
			var bodyRt = enemyBodies != null && index < enemyBodies.Length && enemyBodies[index] != null
				? enemyBodies[index].rectTransform
				: null;
			var fallPlan = ResolveDeathFallPlan(bodyRt, enemy != null ? TryGetMobDef(enemy.name) : null);
			return enemyAnimators[index].PlayDebugDeathAndHold(fallbackSprite, fallPlan.fallToGround,
				fallPlan.targetAnchorMin, fallPlan.targetAnchorMax) != null
				? $"[Debug] mob {index}: mob death 재생"
				: $"[오류] mob {index}: Death 애니메이션이 없습니다.";
		}

		return enemyAnimators[index].TryPlayDebugSpriteKind(spriteKind, fallbackSprite, out string message)
			? $"[Debug] mob {index}: {message}"
			: $"[오류] mob {index}: {message}";
	}

	string DebugPlayMobAttackSprite(int index)
	{
		if (enemyPanels == null || index >= enemyPanels.Length || enemyPanels[index] == null)
			return $"[오류] mob {index} enemyPanel 미할당.";
		if (enemyBodies == null || index >= enemyBodies.Length || enemyBodies[index] == null)
			return $"[오류] mob {index} enemyBody 미할당.";

		var slot = enemyPanels[index].GetComponent<RectTransform>();
		var body = enemyBodies[index].rectTransform;
		var playerRt = playerBody != null ? playerBody.rectTransform : null;
		var enemy = enemies[index];
		var def = ResolveDebugAttackMobDef(enemy);
		var rangeType = EnemyAttackPositionResolver.ResolveRangeType(def);
		if (!enemyAnimators[index].HasAttackSprites && rangeType != EnemyAttackRangeType.Unique)
			return $"[오류] mob {index}: attack 스프라이트가 없습니다.";

		if (battleAnims == null)
		{
			Sprite fallbackSprite = enemy != null ? enemy.sprite : null;
			return enemyAnimators[index].TryPlayDebugSpriteKind("attack", fallbackSprite, out string message)
				? $"[Debug] mob {index}: {message} (BattleAnimations 미할당으로 위치 이동 생략)"
				: $"[오류] mob {index}: {message}";
		}

		if (playerRt == null && rangeType != EnemyAttackRangeType.Ranged && rangeType != EnemyAttackRangeType.Unique)
		{
			Sprite fallbackSprite = enemy != null ? enemy.sprite : null;
			return enemyAnimators[index].TryPlayDebugSpriteKind("attack", fallbackSprite, out string message)
				? $"[Debug] mob {index}: {message} (playerBody 미할당으로 위치 이동 생략)"
				: $"[오류] mob {index}: {message}";
		}

		StopDebugMobSpriteRoutine(index, resetLocalPosition: true);
		EnsureDebugMobHome(index, slot);
		slot.localPosition = debugMobHomeLocalPositions[index];

		var positionPlan = EnemyAttackPositionResolver.Resolve(slot, body, playerRt, def);
		debugMobSpriteRoutines[index] = StartCoroutine(DebugPlayMobAttackRoutine(
			index,
			enemyAnimators[index],
			slot,
			body,
			enemy != null ? enemy.sprite : null,
			def,
			positionPlan));

		return $"[Debug] mob {index}: attack 재생 range={positionPlan.rangeType}, stand={FormatVector(positionPlan.standWorldPosition)}";
	}

	MobDef ResolveDebugAttackMobDef(EnemyInfo enemy)
	{
		if (enemy == null)
			return null;

		var def = TryGetMobDef(enemy.name);
		if (def != null)
			return def;

		if (GameSessionManager.IsBossBattle)
		{
			return new MobDef
			{
				name = enemy.name,
				attackRangeType = EnemyAttackRangeType.Ranged,
			};
		}

		return null;
	}

	IEnumerator DebugPlayMobAttackRoutine(int index, EnemySpriteAnimator animator, RectTransform slot, RectTransform body,
		Sprite fallbackSprite, MobDef def, EnemyAttackPositionPlan positionPlan)
	{
		float approachDuration = def != null && def.attackApproachDuration > 0f ? def.attackApproachDuration : 0.4f;
		float retreatDuration = def != null && def.attackRetreatDuration > 0f ? def.attackRetreatDuration : 0.5f;

		yield return battleAnims.WalkTo(slot, positionPlan.standWorldPosition, approachDuration);

		Coroutine attackRoutine = animator != null && animator.HasAttackSprites ? animator.PlayAttack() : null;
		if (positionPlan.rangeType == EnemyAttackRangeType.Unique && body != null)
			yield return battleAnims.JumpInPlace(body, 34f, 0.36f);
		if (attackRoutine != null)
			yield return new WaitWhile(() => animator != null && animator.IsActionPlaying);

		if (animator != null)
			animator.ReturnToIdle(fallbackSprite);

		yield return battleAnims.WalkBack(slot, positionPlan.homeLocalPosition, retreatDuration);

		if (debugMobSpriteRoutines != null && index >= 0 && index < debugMobSpriteRoutines.Length)
			debugMobSpriteRoutines[index] = null;
	}

	void CaptureDebugMobHomePositions()
	{
		if (enemyPanels == null)
			return;

		EnsureDebugMobStorage(enemyPanels.Length);
		for (int i = 0; i < enemyPanels.Length; i++)
		{
			var slot = enemyPanels[i] != null ? enemyPanels[i].GetComponent<RectTransform>() : null;
			if (slot == null)
				continue;
			debugMobHomeLocalPositions[i] = slot.localPosition;
			debugMobHomeCaptured[i] = true;
		}
	}

	void EnsureDebugMobHome(int index, RectTransform slot)
	{
		EnsureDebugMobStorage(index + 1);
		if (!debugMobHomeCaptured[index])
		{
			debugMobHomeLocalPositions[index] = slot != null ? slot.localPosition : Vector3.zero;
			debugMobHomeCaptured[index] = true;
		}
	}

	void EnsureDebugMobStorage(int size)
	{
		if (size <= 0)
			return;
		if (debugMobSpriteRoutines != null && debugMobSpriteRoutines.Length >= size)
			return;

		var oldRoutines = debugMobSpriteRoutines;
		var oldHomes = debugMobHomeLocalPositions;
		var oldCaptured = debugMobHomeCaptured;
		debugMobSpriteRoutines = new Coroutine[size];
		debugMobHomeLocalPositions = new Vector3[size];
		debugMobHomeCaptured = new bool[size];
		if (oldRoutines == null)
			return;

		int copyCount = Mathf.Min(oldRoutines.Length, size);
		for (int i = 0; i < copyCount; i++)
		{
			debugMobSpriteRoutines[i] = oldRoutines[i];
			debugMobHomeLocalPositions[i] = oldHomes[i];
			debugMobHomeCaptured[i] = oldCaptured[i];
		}
	}

	void StopDebugMobSpriteRoutine(int index, bool resetLocalPosition)
	{
		if (debugMobSpriteRoutines != null && index >= 0 && index < debugMobSpriteRoutines.Length
			&& debugMobSpriteRoutines[index] != null)
		{
			StopCoroutine(debugMobSpriteRoutines[index]);
			debugMobSpriteRoutines[index] = null;
		}

		if (!resetLocalPosition || enemyPanels == null || index < 0 || index >= enemyPanels.Length
			|| enemyPanels[index] == null || debugMobHomeCaptured == null
			|| index >= debugMobHomeCaptured.Length || !debugMobHomeCaptured[index])
		{
			return;
		}

		var slot = enemyPanels[index].GetComponent<RectTransform>();
		if (slot != null)
			slot.localPosition = debugMobHomeLocalPositions[index];
	}

	static string NormalizeSpriteKind(string spriteKind)
	{
		return string.IsNullOrWhiteSpace(spriteKind)
			? ""
			: spriteKind.Trim().Replace("_", "").Replace("-", "").ToLowerInvariant();
	}

	static string FormatVector(Vector3 value)
	{
		return $"({value.x:0.##}, {value.y:0.##}, {value.z:0.##})";
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
