using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AnimationDebugSceneController : BattleControllerBase
{
	const float DefaultPlayerLoopSeconds = 3f;

	static readonly string[] PlayerSequence =
	{
		"idle",
		"lowhp",
		"jump",
		"defense",
		"smallhit",
		"stronghit",
		"debuff",
		"attack",
		"death",
	};

	static readonly string[] EnemySequence =
	{
		"idle",
		"attack",
		"hit",
		"death",
	};

	[SerializeField] PlayerBodyAnimator playerBodyAnimator;
	[SerializeField] PlayerAttackAnimator attackAnimator;
	[SerializeField] Image enemyProjectile;
	[SerializeField] EnemyAttackProjectileVfx attackProjectileVfx;
	[SerializeField] BattleBottomFocusController bottomFocus;
	[SerializeField] TMP_Text selectedLabel;
	[SerializeField] TMP_Text detailsLabel;
	[SerializeField] TMP_Text statusLabel;
	[SerializeField] Button previousButton;
	[SerializeField] Button nextButton;

	List<AnimationDebugCatalogEntry> catalog;
	int selectedIndex;
	Vector2 playerBaseAnchorMin;
	Vector2 playerBaseAnchorMax;
	bool playerBaseAnchorsCaptured;
	Coroutine playAllRoutine;
	Coroutine enemyAttackRoutine;
	bool applyingSelection;

	void Awake()
	{
		CapturePlayerBaseAnchors();
	}

	void Start()
	{
		if (GameSessionManager.PlayerHearts.TotalHalfHearts == 0)
			GameSessionManager.PlayerHearts.Reset();

		CapturePlayerBaseAnchors();
		catalog = AnimationDebugCatalog.BuildFromRegisteredStages();
		selectedIndex = Mathf.Clamp(selectedIndex, 0, Mathf.Max(0, catalog.Count - 1));
		ApplySelection("Ready");
	}

	public void SelectPrevious()
	{
		if (catalog == null || catalog.Count == 0)
			return;

		selectedIndex = (selectedIndex - 1 + catalog.Count) % catalog.Count;
		ApplySelection("Ready");
	}

	public void SelectNext()
	{
		if (catalog == null || catalog.Count == 0)
			return;

		selectedIndex = (selectedIndex + 1) % catalog.Count;
		ApplySelection("Ready");
	}

	public void ResetSelected()
	{
		ApplySelection("Reset");
	}

	public void PlayPlayerIdle() => PlayPlayerDebugMotion("idle");
	public void PlayPlayerLowHp() => PlayPlayerDebugMotion("lowhp");
	public void PlayPlayerJump() => PlayPlayerDebugMotion("jump");
	public void PlayPlayerDefense() => PlayPlayerDebugMotion("defense");
	public void PlayPlayerSmallHit() => PlayPlayerDebugMotion("smallhit");
	public void PlayPlayerStrongHit() => PlayPlayerDebugMotion("stronghit");
	public void PlayPlayerDebuff() => PlayPlayerDebugMotion("debuff");
	public void PlayPlayerDeath() => PlayPlayerDebugMotion("death");
	public void PlayEnemyIdle() => PlayEnemyDebugMotion("idle");
	public void PlayEnemyHit() => PlayEnemyDebugMotion("hit");
	public void PlayEnemyDeath() => PlayEnemyDebugMotion("death");

	public void PlayPlayerFullWeaponAttack()
	{
		StopRunningSequences(resetVisuals: true);
		StartPlayerAttackMotion();
	}

	public void PlayEnemyAttack()
	{
		StopRunningSequences(resetVisuals: true);
		enemyAttackRoutine = StartCoroutine(PlayEnemyAttackRoutine());
	}

	public void PlayAllSelectedMotions()
	{
		StopRunningSequences(resetVisuals: true);
		playAllRoutine = StartCoroutine(PlayAllSelectedMotionsRoutine());
	}

	void ApplySelection(string status)
	{
		applyingSelection = true;
		StopRunningSequences(resetVisuals: false);
		applyingSelection = false;

		if (catalog == null || catalog.Count == 0)
		{
			SetText(selectedLabel, "No registered stage mobs or bosses");
			SetText(detailsLabel, "");
			SetStatus("Catalog is empty");
			SetNavigationEnabled(false);
			return;
		}

		var entry = catalog[selectedIndex];
		GameSessionManager.CurrentStageId = entry.StageId;
		GameSessionManager.PrepareBattleEnemy(entry.CreateEnemyInfo(ResolveEntrySprite(entry)), entry.IsBoss);
		LoadSessionEnemiesSnapshot();
		targetIndex = 0;

		ApplyDebugStageBackground(entry.Stage);
		ApplyDebugPlayerGroundOffset(entry.Stage);
		SetupEnemyDisplay();
		RefreshTargetMarkers();
		UpdateSelectedText(entry);
		SetNavigationEnabled(catalog.Count > 1);
		SetStatus(status);
	}

	void CapturePlayerBaseAnchors()
	{
		if (playerBaseAnchorsCaptured || playerBody == null)
			return;

		var rt = playerBody.rectTransform;
		playerBaseAnchorMin = rt.anchorMin;
		playerBaseAnchorMax = rt.anchorMax;
		playerBaseAnchorsCaptured = true;
	}

	void ApplyDebugStageBackground(StageData stage)
	{
		if (fightBackgroundImage == null || stage == null)
			return;

		var bundle = FindBundle(stage.id);
		if (bundle != null && bundle.background != null)
		{
			fightBackgroundImage.sprite = bundle.background;
			fightBackgroundImage.color = Color.white;
			return;
		}

		fightBackgroundImage.sprite = null;
		fightBackgroundImage.color = stage.themeColor;
	}

	void ApplyDebugPlayerGroundOffset(StageData stage)
	{
		if (playerBody == null || stage == null)
			return;

		CapturePlayerBaseAnchors();
		if (!playerBaseAnchorsCaptured)
			return;

		var rt = playerBody.rectTransform;
		float offset = stage.playerGroundYOffset;
		rt.anchorMin = new Vector2(playerBaseAnchorMin.x, playerBaseAnchorMin.y + offset);
		rt.anchorMax = new Vector2(playerBaseAnchorMax.x, playerBaseAnchorMax.y + offset);
		rt.offsetMin = Vector2.zero;
		rt.offsetMax = Vector2.zero;
	}

	Sprite ResolveEntrySprite(AnimationDebugCatalogEntry entry)
	{
		if (entry == null)
			return null;

		var bundle = FindBundle(entry.StageId);
		if (entry.IsBoss)
			return bundle != null ? bundle.bossSprite : null;

		if (bundle == null || bundle.mobSprites == null)
			return null;
		if (entry.MobIndex < 0 || entry.MobIndex >= bundle.mobSprites.Length)
			return null;
		return bundle.mobSprites[entry.MobIndex];
	}

	void UpdateSelectedText(AnimationDebugCatalogEntry entry)
	{
		string kind = entry.IsBoss ? "Boss" : "Mob";
		SetText(selectedLabel,
			$"{selectedIndex + 1} / {catalog.Count}  {entry.StageDisplayName} [{entry.StageId}]  {kind}: {entry.EntityName}");

		string hp = entry.IsBoss ? entry.HpMax.ToString() : $"{entry.HpMin}-{entry.HpMax}";
		string range = entry.IsBoss
			? "BossDefault"
			: EnemyAttackPositionResolver.ResolveRangeType(entry.MobDefinition).ToString();
		string projectile = string.IsNullOrEmpty(entry.ProjectileSpritePath) ? "none" : entry.ProjectileSpritePath;
		string vfx = string.IsNullOrEmpty(entry.AttackVfxSpritePath) ? "none" : entry.AttackVfxSpritePath;
		string attack = string.IsNullOrEmpty(entry.AttackSpriteFolderPath) ? "idle fallback when empty" : entry.AttackSpriteFolderPath;
		string hit = string.IsNullOrEmpty(entry.HitSpriteFolderPath) ? "idle fallback when empty" : entry.HitSpriteFolderPath;

		SetText(detailsLabel,
			$"Rank {entry.Rank}  HP {hp}  Range {range}\n" +
			$"Idle: {DisplayPath(entry.IdleSpriteFolderPath)}\n" +
			$"Attack: {DisplayPath(attack)}\n" +
			$"Hit: {DisplayPath(hit)}\n" +
			$"Death: {DisplayPath(entry.DeathAnimationClipPath ?? entry.DeathSpriteFolderPath)}\n" +
			$"Projectile: {DisplayPath(projectile)}\n" +
			$"Attack VFX: {DisplayPath(vfx)}");
	}

	static string DisplayPath(string value)
	{
		return string.IsNullOrEmpty(value) ? "none" : value;
	}

	void PlayPlayerDebugMotion(string kind)
	{
		StopRunningSequences(resetVisuals: true);
		string message = DebugPlayBattleSprite("player", -1, kind, playerBodyAnimator, DefaultPlayerLoopSeconds);
		SetStatus(message);
	}

	void PlayEnemyDebugMotion(string kind)
	{
		StopRunningSequences(resetVisuals: true);
		string message = DebugPlayBattleSprite("mob", 0, kind, playerBodyAnimator, -1f);
		SetStatus(message);
	}

	void StartPlayerAttackMotion()
	{
		if (attackAnimator == null)
		{
			SetStatus("[Error] PlayerAttackAnimator is not assigned.");
			return;
		}

		var target = ActiveEnemyBody();
		Coroutine routine = attackAnimator.Play(target);
		SetStatus(routine != null
			? "[Debug] player full weapon attack"
			: "[Error] player attack sprites are missing.");
	}

	IEnumerator PlayEnemyAttackRoutine()
	{
		var enemy = ActiveEnemy();
		var slot = ActiveEnemySlot();
		var body = ActiveEnemyBody();
		if (enemy == null || slot == null || body == null)
		{
			SetStatus("[Error] selected enemy is not ready.");
			enemyAttackRoutine = null;
			yield break;
		}

		var def = ResolveSelectedAttackDef(enemy);
		var positionPlan = EnemyAttackPositionResolver.Resolve(
			slot,
			body,
			playerBody != null ? playerBody.rectTransform : null,
			def);
		var rangeType = EnemyAttackPositionResolver.ResolveRangeType(def);
		var animator = ActiveEnemyAnimator();
		bool hasProjectile = def != null
			&& !string.IsNullOrEmpty(def.projectileSpritePath)
			&& ResolveEnemyProjectileSprite(enemy.name) != null;
		bool hasAttackVfx = def != null
			&& !string.IsNullOrEmpty(def.attackVfxSpritePath)
			&& ResolveEnemyAttackVfxSprite(enemy.name) != null;
		bool shouldPlayDraculaLaser = ShouldPlayDraculaLaserPreview(ActiveCatalogEntry(), enemy);

		SetStatus($"[Debug] enemy attack range={rangeType}");
		if (shouldPlayDraculaLaser && playerBody != null)
		{
			SetStatus("[Debug] boss attack Dracula laser");
			yield return StartCoroutine(PlayDraculaLaserEnemyAttack(enemy, body));
		}
		else if (rangeType == EnemyAttackRangeType.Ranged && hasProjectile)
		{
			yield return StartCoroutine(PlayRangedEnemyAttack(enemy, body, positionPlan));
		}
		else if (BattleAnimations.ShouldUseSlimeLeapSlam(enemy, def)
			&& battleAnims != null
			&& playerBody != null)
		{
			SetStatus("[Debug] enemy attack range=Unique slime leap-slam");
			yield return StartCoroutine(PlaySlimeLeapSlamEnemyAttack(enemy, body));
		}
		else if (rangeType == EnemyAttackRangeType.Unique)
		{
			yield return StartCoroutine(PlayUniqueEnemyAttack(enemy, body, hasAttackVfx));
		}
		else if (rangeType == EnemyAttackRangeType.Ranged)
		{
			if (animator != null)
				animator.PlayAttack();
			if (battleAnims != null)
				yield return StartCoroutine(battleAnims.JumpInPlace(body, 18f, 0.28f));
			yield return StartCoroutine(PlayPlayerPreviewHit(enemy));
		}
		else
		{
			yield return StartCoroutine(PlayMovingEnemyAttack(enemy, slot, body, def, positionPlan));
		}

		if (animator != null)
			animator.ReturnToIdle(enemy.sprite);
		enemyAttackRoutine = null;
	}

	IEnumerator PlayDraculaLaserEnemyAttack(EnemyInfo enemy, RectTransform body)
	{
		var animator = ActiveEnemyAnimator();
		if (animator != null)
			animator.PlayAttack();

		Coroutine feedback = null;
		bool impactResolved = false;
		System.Action resolveImpact = () =>
		{
			if (impactResolved)
				return;
			impactResolved = true;
			feedback = StartCoroutine(PlayPlayerPreviewHit(enemy));
		};

		var laserVfx = EnsureAttackProjectileVfx();
		Coroutine laserRoutine = laserVfx != null
			? laserVfx.PlayDraculaLaser(body, playerBody.rectTransform, resolveImpact)
			: null;
		if (laserRoutine != null)
			yield return laserRoutine;
		else
		{
			SetStatus("[Warning] Dracula laser VFX is not assigned; using hit preview only.");
			resolveImpact();
		}

		if (feedback != null)
			yield return feedback;
	}

	IEnumerator PlayMovingEnemyAttack(EnemyInfo enemy, RectTransform slot, RectTransform body,
		MobDef def, EnemyAttackPositionPlan positionPlan)
	{
		var animator = ActiveEnemyAnimator();
		float approach = def != null && def.attackApproachDuration > 0f ? def.attackApproachDuration : 0.4f;
		float retreat = def != null && def.attackRetreatDuration > 0f ? def.attackRetreatDuration : 0.5f;

		if (battleAnims != null)
			yield return StartCoroutine(battleAnims.WalkTo(slot, positionPlan.standWorldPosition, approach));
		if (animator != null)
			animator.PlayAttack();

		if (battleAnims != null)
		{
			Coroutine feedback = null;
			bool useProfiledHitDelay = TryResolvePreviewHitTiming(
				def,
				animator,
				enemy,
				out float hitStartDelay,
				out float visualImpactDelay);
			if (useProfiledHitDelay)
				feedback = StartCoroutine(PlayProfiledPlayerPreviewHit(
					enemy,
					hitStartDelay,
					visualImpactDelay));

			yield return StartCoroutine(battleAnims.QuickSlam(
				slot,
				positionPlan.impactWorldPosition,
				onImpact: useProfiledHitDelay
					? (System.Action)null
					: () => feedback = StartCoroutine(PlayPlayerPreviewHit(enemy))));
			if (feedback != null)
				yield return feedback;
			yield return StartCoroutine(battleAnims.WalkBack(slot, positionPlan.homeLocalPosition, retreat));
		}
		else
		{
			yield return new WaitForSeconds(0.5f);
		}
	}

	IEnumerator PlayRangedEnemyAttack(EnemyInfo enemy, RectTransform body, EnemyAttackPositionPlan positionPlan)
	{
		var animator = ActiveEnemyAnimator();
		if (battleAnims != null)
			yield return StartCoroutine(battleAnims.JumpInPlace(body, 18f, 0.24f));
		if (animator != null)
			animator.PlayAttack();

		Sprite projectileSprite = ResolveEnemyProjectileSprite(enemy.name);
		if (enemyProjectile != null && projectileSprite != null && battleAnims != null && playerBody != null)
		{
			enemyProjectile.sprite = projectileSprite;
			enemyProjectile.color = Color.white;
			enemyProjectile.preserveAspect = true;
			yield return StartCoroutine(battleAnims.EnemyProjectileAttack(
				enemyProjectile,
				body,
				playerBody.rectTransform,
				playerBody,
				playerBodyAnimator,
				null,
				blocked: false,
				damageHalfHearts: 2,
				attachmentFollower: GetAttachmentFollower(),
				enemyRank: enemy.rank,
				projectileStartWorld: positionPlan.projectileStartWorldPosition,
				projectileEndWorld: positionPlan.projectileEndWorldPosition));
		}
		else
		{
			yield return StartCoroutine(PlayPlayerPreviewHit(enemy));
		}
	}

	IEnumerator PlaySlimeLeapSlamEnemyAttack(EnemyInfo enemy, RectTransform body)
	{
		var animator = ActiveEnemyAnimator();
		bool previousTrace = BattleAnimations.EnableEnemyLeapSlamDebugTrace;
		BattleAnimations.EnableEnemyLeapSlamDebugTrace = true;
		try
		{
			yield return StartCoroutine(battleAnims.EnemyLeapSlamAttack(
				body,
				playerBody.rectTransform,
				playerBody,
				playerBodyAnimator,
				damageHalfHearts: 2,
				enemyRank: enemy != null ? enemy.rank : 0,
				enemyAnimator: animator,
				idleFallbackSprite: enemy != null ? enemy.sprite : null,
				debugTraceMode: "Debug"));
		}
		finally
		{
			BattleAnimations.EnableEnemyLeapSlamDebugTrace = previousTrace;
		}
	}

	IEnumerator PlayUniqueEnemyAttack(EnemyInfo enemy, RectTransform body, bool hasAttackVfx)
	{
		var animator = ActiveEnemyAnimator();
		if (animator != null)
			animator.PlayAttack();

		if (hasAttackVfx)
		{
			Sprite vfxSprite = ResolveEnemyAttackVfxSprite(enemy.name);
			Coroutine vfxRoutine = attackProjectileVfx != null
				? attackProjectileVfx.Play(vfxSprite, body)
				: null;
			if (vfxRoutine != null)
				yield return vfxRoutine;
			yield return StartCoroutine(PlayPlayerPreviewHit(enemy));
			yield break;
		}

		if (battleAnims != null)
		{
			yield return StartCoroutine(battleAnims.JumpInPlace(body, 34f, 0.36f));
			yield return StartCoroutine(PlayPlayerPreviewHit(enemy));
		}
		else
		{
			yield return new WaitForSeconds(0.5f);
		}
	}

	IEnumerator PlayPlayerPreviewHit(EnemyInfo enemy)
	{
		Coroutine hitRoutine = StartPlayerPreviewHitMotion(enemy);
		FlashPlayerPreviewDamage();

		if (hitRoutine != null)
			yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
		else
			yield return new WaitForSeconds(0.18f);
	}

	IEnumerator PlayProfiledPlayerPreviewHit(
		EnemyInfo enemy,
		float hitStartDelaySeconds,
		float visualImpactDelaySeconds)
	{
		float hitStartDelay = Mathf.Max(0f, hitStartDelaySeconds);
		float impactDelay = Mathf.Max(0f, visualImpactDelaySeconds);

		if (hitStartDelay > 0f)
			yield return new WaitForSeconds(hitStartDelay);

		Coroutine hitRoutine = StartPlayerPreviewHitMotion(enemy);

		float remainingUntilImpact = Mathf.Max(0f, impactDelay - hitStartDelay);
		if (remainingUntilImpact > 0f)
			yield return new WaitForSeconds(remainingUntilImpact);

		FlashPlayerPreviewDamage();

		if (hitRoutine != null)
			yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
		else
			yield return new WaitForSeconds(0.18f);
	}

	bool TryResolvePreviewHitTiming(
		MobDef def,
		EnemySpriteAnimator animator,
		EnemyInfo enemy,
		out float hitStartDelaySeconds,
		out float visualImpactDelaySeconds)
	{
		hitStartDelaySeconds = 0f;
		visualImpactDelaySeconds = 0f;
		var timing = def != null ? def.attackTimingProfile : null;
		if (!EnemyAttackTiming.HasUsableProfile(timing))
			return false;

		float enemyAttackDuration = ResolvePreviewAttackDurationSeconds(def, animator);
		if (enemyAttackDuration <= 0f)
			return false;

		int enemyRank = enemy != null ? enemy.rank : 0;
		const int previewDamageHalfHearts = 2;
		float playerHitDuration = ResolvePreviewPlayerHitDurationSeconds(enemyRank, previewDamageHalfHearts);
		float reactionNormalizedTime = EnemyAttackTiming.ResolvePlayerReactionNormalizedTime(
			timing,
			enemyRank,
			previewDamageHalfHearts);
		visualImpactDelaySeconds = EnemyAttackTiming.ComputeImpactDelay(
			enemyAttackDuration,
			timing.impactNormalizedTime);
		hitStartDelaySeconds = EnemyAttackTiming.ComputePlayerHitStartDelay(
			enemyAttackDuration,
			timing.impactNormalizedTime,
			playerHitDuration,
			reactionNormalizedTime);
		return true;
	}

	float ResolvePreviewAttackDurationSeconds(MobDef def, EnemySpriteAnimator animator)
	{
		if (animator != null && animator.AttackDurationSeconds > 0f)
			return animator.AttackDurationSeconds;

		if (def == null || def.attackSpriteFrameCount <= 0 || def.attackFrameRate <= 0f)
			return 0f;

		return def.attackSpriteFrameCount / Mathf.Max(1f, def.attackFrameRate);
	}

	float ResolvePreviewPlayerHitDurationSeconds(int enemyRank, int damageHalfHearts)
	{
		if (playerBodyAnimator == null)
			return 0f;

		int frameCount = playerBodyAnimator.ResolveHitFrameCountByEnemyRank(enemyRank, damageHalfHearts);
		if (frameCount <= 0 || playerBodyAnimator.ActionFrameRate <= 0f)
			return 0f;

		return frameCount / Mathf.Max(1f, playerBodyAnimator.ActionFrameRate);
	}

	Coroutine StartPlayerPreviewHitMotion(EnemyInfo enemy)
	{
		return playerBodyAnimator != null
			? playerBodyAnimator.PlayHitByEnemyRank(enemy != null ? enemy.rank : 0, 2)
			: null;
	}

	void FlashPlayerPreviewDamage()
	{
		if (battleAnims != null && playerBody != null)
			battleAnims.FlashDamage(playerBody);
	}

	IEnumerator PlayAllSelectedMotionsRoutine()
	{
		SetStatus("[Debug] play all selected motions");
		for (int i = 0; i < PlayerSequence.Length; i++)
			yield return StartCoroutine(PlayPlayerSequenceMotion(PlayerSequence[i]));

		for (int i = 0; i < EnemySequence.Length; i++)
			yield return StartCoroutine(PlayEnemySequenceMotion(EnemySequence[i]));

		SetStatus("[Debug] play all complete");
		playAllRoutine = null;
	}

	IEnumerator PlayPlayerSequenceMotion(string kind)
	{
		if (kind == "attack")
		{
			if (attackAnimator != null)
			{
				Coroutine routine = attackAnimator.Play(ActiveEnemyBody());
				if (routine != null)
					yield return routine;
			}
			yield break;
		}

		DebugPlayBattleSprite("player", -1, kind, playerBodyAnimator, DefaultPlayerLoopSeconds);
		yield return new WaitForSeconds(PlayerMotionWaitSeconds(kind));
	}

	IEnumerator PlayEnemySequenceMotion(string kind)
	{
		if (kind == "attack")
		{
			enemyAttackRoutine = StartCoroutine(PlayEnemyAttackRoutine());
			yield return enemyAttackRoutine;
			yield break;
		}

		DebugPlayBattleSprite("mob", 0, kind, playerBodyAnimator, -1f);
		yield return new WaitForSeconds(EnemyMotionWaitSeconds(kind));
	}

	float PlayerMotionWaitSeconds(string kind)
	{
		switch (kind)
		{
			case "lowhp":
			case "defense":
				return DefaultPlayerLoopSeconds + 0.2f;
			case "death":
				return 2.2f;
			case "debuff":
				return 1.4f;
			default:
				return 0.9f;
		}
	}

	float EnemyMotionWaitSeconds(string kind)
	{
		switch (kind)
		{
			case "death":
				return 2.0f;
			default:
				return 0.9f;
		}
	}

	EnemyInfo ActiveEnemy()
	{
		return enemies != null && enemies.Count > 0 ? enemies[0] : null;
	}

	AnimationDebugCatalogEntry ActiveCatalogEntry()
	{
		if (catalog == null || selectedIndex < 0 || selectedIndex >= catalog.Count)
			return null;
		return catalog[selectedIndex];
	}

	RectTransform ActiveEnemySlot()
	{
		return enemyPanels != null && enemyPanels.Length > 0 && enemyPanels[0] != null
			? enemyPanels[0].GetComponent<RectTransform>()
			: null;
	}

	RectTransform ActiveEnemyBody()
	{
		return enemyBodies != null && enemyBodies.Length > 0 && enemyBodies[0] != null
			? enemyBodies[0].rectTransform
			: null;
	}

	EnemySpriteAnimator ActiveEnemyAnimator()
	{
		return enemyAnimators != null && enemyAnimators.Length > 0 ? enemyAnimators[0] : null;
	}

	EnemyProjectileAttachmentFollower GetAttachmentFollower()
	{
		return enemyIdleProjectiles != null
			&& enemyIdleProjectiles.Length > 0
			&& enemyIdleProjectiles[0] != null
			? enemyIdleProjectiles[0].GetComponent<EnemyProjectileAttachmentFollower>()
			: null;
	}

	MobDef ResolveSelectedAttackDef(EnemyInfo enemy)
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

	void StopRunningSequences(bool resetVisuals)
	{
		bool stoppedEnemyAttack = enemyAttackRoutine != null;
		if (playAllRoutine != null)
			StopCoroutine(playAllRoutine);
		if (enemyAttackRoutine != null)
			StopCoroutine(enemyAttackRoutine);
		if (attackProjectileVfx != null)
			attackProjectileVfx.StopAndHide();
		playAllRoutine = null;
		enemyAttackRoutine = null;

		if (resetVisuals && stoppedEnemyAttack && !applyingSelection)
			ApplySelection("Ready");
	}

	public static bool ShouldPlayDraculaLaserPreview(AnimationDebugCatalogEntry entry, EnemyInfo enemy)
	{
		if (entry == null)
			return false;
		return DraculaLaserAttackVfx.ShouldPlayForCurrentAttack(
			entry.IsBoss,
			entry.StageId,
			enemy,
			entry.BossDefinition,
			entry.BossDefinition != null ? entry.BossDefinition.enemyDiceProfileId : null);
	}

	EnemyAttackProjectileVfx EnsureAttackProjectileVfx()
	{
		if (attackProjectileVfx != null)
			return attackProjectileVfx;

		attackProjectileVfx = GetComponent<EnemyAttackProjectileVfx>();
		if (attackProjectileVfx == null)
			attackProjectileVfx = gameObject.AddComponent<EnemyAttackProjectileVfx>();
		return attackProjectileVfx;
	}

	void SetNavigationEnabled(bool enabled)
	{
		if (previousButton != null)
			previousButton.interactable = enabled;
		if (nextButton != null)
			nextButton.interactable = enabled;
	}

	void SetStatus(string value)
	{
		SetText(statusLabel, value);
	}

	static void SetText(TMP_Text target, string value)
	{
		if (target != null)
			target.text = value ?? "";
	}
}
