using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public readonly struct EnemyLeapSlamMotionPlan
{
	public readonly Vector3 homeWorldPosition;
	public readonly Vector3 apexWorldPosition;
	public readonly Vector3 leapTargetWorldPosition;
	public readonly Vector3 impactWorldPosition;

	public EnemyLeapSlamMotionPlan(
		Vector3 homeWorldPosition,
		Vector3 apexWorldPosition,
		Vector3 leapTargetWorldPosition,
		Vector3 impactWorldPosition)
	{
		this.homeWorldPosition = homeWorldPosition;
		this.apexWorldPosition = apexWorldPosition;
		this.leapTargetWorldPosition = leapTargetWorldPosition;
		this.impactWorldPosition = impactWorldPosition;
	}
}

/// <summary>
/// 재사용 가능한 전투 애니메이션 유틸리티.
/// 각 메서드는 독립적인 코루틴으로, 호출부에서 조합하여 연출 시퀀스를 구성한다.
/// </summary>
public class BattleAnimations : MonoBehaviour
{
	readonly struct EnemyLeapSlamRestPose
	{
		public readonly Vector3 homeWorldPosition;
		public readonly Vector2 anchoredPosition;
		public readonly Vector3 localScale;
		public readonly int siblingIndex;
		public readonly Transform parent;
		public readonly Vector2 anchorMin;
		public readonly Vector2 anchorMax;
		public readonly Vector2 offsetMin;
		public readonly Vector2 offsetMax;
		public readonly Vector2 pivot;

		EnemyLeapSlamRestPose(RectTransform body)
		{
			homeWorldPosition = body.position;
			anchoredPosition = body.anchoredPosition;
			localScale = body.localScale;
			siblingIndex = body.GetSiblingIndex();
			parent = body.parent;
			anchorMin = body.anchorMin;
			anchorMax = body.anchorMax;
			offsetMin = body.offsetMin;
			offsetMax = body.offsetMax;
			pivot = body.pivot;
		}

		public static EnemyLeapSlamRestPose Capture(RectTransform body)
		{
			return new EnemyLeapSlamRestPose(body);
		}

		public bool MatchesLayout(RectTransform body)
		{
			return body != null
				&& body.parent == parent
				&& Approximately(body.anchorMin, anchorMin)
				&& Approximately(body.anchorMax, anchorMax)
				&& Approximately(body.pivot, pivot);
		}
	}

	static readonly Dictionary<RectTransform, EnemyLeapSlamRestPose> enemyLeapSlamRestPoses =
		new Dictionary<RectTransform, EnemyLeapSlamRestPose>();
	static readonly Dictionary<RectTransform, int> activeEnemyLeapSlamDebugSerials =
		new Dictionary<RectTransform, int>();
	static int slimeLeapDebugSerial;
	public static readonly Vector2 EnemyLeapSlamReferenceResolution = new Vector2(1920f, 1080f);
	public const float EnemyLeapSlamWindupDropNormalizedY = 0.004f;
	public const float EnemyLeapSlamLeapTargetOffsetNormalizedY = 0.033f;
	public const float EnemyLeapSlamApexOffsetNormalizedY = 0.070f;
	public const float EnemyLeapSlamImpactOffsetNormalizedY = -0.020f;
	public const float EnemyLeapSlamReturnBobNormalizedY = 0.010f;
	public static bool EnableEnemyLeapSlamDebugTrace { get; set; }

	// ── 피격 점멸 ──

	/// <summary>
	/// 대상 Image를 지정 색으로 점멸 후 원래 색으로 복귀.
	/// </summary>
	public Coroutine FlashHit(Image target, Color flashColor = default, float holdTime = 0.16f, float fadeTime = 0.30f)
	{
		if (target == null)
			return null;
		if (flashColor == default)
			flashColor = new Color(1f, 0.2f, 0.2f);
		return StartCoroutine(FlashHitRoutine(target, flashColor, holdTime, fadeTime));
	}

	public Coroutine FlashDamage(Image target, Color flashColor = default, float holdTime = 0.16f, float fadeTime = 0.30f)
	{
		return FlashHit(target, flashColor, holdTime, fadeTime);
	}

	IEnumerator FlashHitRoutine(Image target, Color flashColor, float holdTime, float fadeTime)
	{
		Color original = target.color;
		target.color = flashColor;
		yield return new WaitForSeconds(holdTime);

		float elapsed = 0f;
		while (elapsed < fadeTime)
		{
			elapsed += Time.deltaTime;
			target.color = Color.Lerp(flashColor, original, elapsed / fadeTime);
			yield return null;
		}
		target.color = original;
	}

	// ── 이동 애니메이션 ──

	/// <summary>
	/// 슬롯(패널) 전체를 대상 월드 위치까지 부드럽게 이동.
	/// 하위 UI(체력바, 이름, 랭크)가 함께 이동.
	/// </summary>
	public IEnumerator WalkTo(RectTransform slot, Vector3 targetWorldPos, float duration)
	{
		if (slot == null)
			yield break;

		Vector3 originalLocal = slot.localPosition;
		Vector3 targetLocal = WorldToLocal(slot, targetWorldPos);

		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = t * t * (3f - 2f * t); // smoothstep
			slot.localPosition = Vector3.Lerp(originalLocal, targetLocal, eased);
			yield return null;
		}
		slot.localPosition = targetLocal;
	}

	/// <summary>
	/// 슬롯을 원래 위치(localPosition = 저장값)로 복귀.
	/// </summary>
	public IEnumerator WalkBack(RectTransform slot, Vector3 originalLocalPos, float duration)
	{
		if (slot == null)
			yield break;

		Vector3 startLocal = slot.localPosition;

		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = t * t * (3f - 2f * t); // smoothstep
			slot.localPosition = Vector3.Lerp(startLocal, originalLocalPos, eased);
			yield return null;
		}
		slot.localPosition = originalLocalPos;
	}

	/// <summary>
	/// 제자리에서 바디(스프라이트)만 점프.
	/// </summary>
	public IEnumerator JumpInPlace(RectTransform body, float height = 30f, float duration = 0.3f)
	{
		if (body == null)
			yield break;

		if (EnableEnemyLeapSlamDebugTrace
			&& activeEnemyLeapSlamDebugSerials.TryGetValue(body, out int activeSerial))
		{
			Debug.LogWarning($"[SlimeLeapTrace] attackSerial={activeSerial} frame={Time.frameCount} " +
				$"JumpInPlace requested while leap-slam owns enemyBody={body.name} height={height:F3} duration={duration:F3}");
		}

		Vector2 originalPos = body.anchoredPosition;
		float halfDuration = duration * 0.5f;

		// 상승
		float elapsed = 0f;
		while (elapsed < halfDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / halfDuration);
			float eased = 1f - (1f - t) * (1f - t); // ease-out
			body.anchoredPosition = originalPos + Vector2.up * height * eased;
			yield return null;
		}

		// 하강
		elapsed = 0f;
		while (elapsed < halfDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / halfDuration);
			float eased = t * t; // ease-in
			body.anchoredPosition = originalPos + Vector2.up * height * (1f - eased);
			yield return null;
		}
		body.anchoredPosition = originalPos;
	}

	/// <summary>
	/// 현재 위치에서 대상으로 빠르게 돌진한 뒤 현재 위치로 복귀 (타격 연출).
	/// </summary>
	public IEnumerator QuickSlam(RectTransform slot, Vector3 targetWorldPos,
		float rushTime = 0.06f, float holdTime = 0.04f, float returnTime = 0.1f,
		System.Action onImpact = null)
	{
		if (slot == null)
			yield break;

		Vector3 beforeLocal = slot.localPosition;
		Vector3 targetLocal = WorldToLocal(slot, targetWorldPos);

		// 돌진 (ease-in)
		float elapsed = 0f;
		while (elapsed < rushTime)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / rushTime);
			slot.localPosition = Vector3.Lerp(beforeLocal, targetLocal, t * t);
			yield return null;
		}
		slot.localPosition = targetLocal;
		onImpact?.Invoke();

		yield return new WaitForSeconds(holdTime);

		// 복귀 (ease-out)
		elapsed = 0f;
		while (elapsed < returnTime)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / returnTime);
			float eased = 1f - (1f - t) * (1f - t);
			slot.localPosition = Vector3.Lerp(targetLocal, beforeLocal, eased);
			yield return null;
		}
		slot.localPosition = beforeLocal;
	}

	// ── 조합 시퀀스 ──

	/// <summary>
	/// 적 슬롯이 플레이어 앞까지 이동 → 슬램 타격 → 플레이어 빨갛게 점멸 → 원래 자리로 복귀.
	/// 일반 몹(non-boss) 근접 공격의 표준 시퀀스. DiceBattle·MahjongBattle 양쪽에서 재사용.
	/// </summary>
	public IEnumerator EnemyMeleeAttack(RectTransform slot, RectTransform enemyBody,
		RectTransform playerBodyRt, Image playerBodyImage, PlayerBodyAnimator playerBodyAnimator = null,
		int damageHalfHearts = 0, int enemyRank = 0, EnemyAttackPositionPlan? positionPlan = null,
		System.Action onImpact = null)
	{
		if (slot == null || playerBodyRt == null)
			yield break;

		Vector3 slotOriginalLocal = positionPlan.HasValue
			? positionPlan.Value.homeLocalPosition
			: slot.localPosition;

		Vector3 frontWorld = positionPlan.HasValue
			? positionPlan.Value.standWorldPosition
			: EnemyAttackPositionResolver.Resolve(slot, enemyBody, playerBodyRt, null).standWorldPosition;

		yield return StartCoroutine(WalkTo(slot, frontWorld, 0.4f));

		Vector3 slamTarget = positionPlan.HasValue
			? positionPlan.Value.impactWorldPosition
			: new Vector3(playerBodyRt.position.x, slot.position.y, slot.position.z);
		Coroutine hitRoutine = null;
		bool impactTriggered = false;
		yield return StartCoroutine(QuickSlam(slot, slamTarget, onImpact: () =>
		{
			if (impactTriggered)
				return;
			impactTriggered = true;
			onImpact?.Invoke();
			if (playerBodyImage == null)
				return;
			hitRoutine = playerBodyAnimator != null
				? playerBodyAnimator.PlayHitByEnemyRank(enemyRank, damageHalfHearts)
				: null;
			FlashDamage(playerBodyImage);
		}));

		if (hitRoutine != null)
			yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
		else if (playerBodyImage != null && playerBodyAnimator == null)
			yield return new WaitForSeconds(0.15f);

		yield return StartCoroutine(WalkBack(slot, slotOriginalLocal, 0.5f));
	}

	public IEnumerator EnemyLeapSlamAttack(RectTransform enemyBody, RectTransform playerBodyRt,
		Image playerBodyImage = null, PlayerBodyAnimator playerBodyAnimator = null,
		int damageHalfHearts = 0, int enemyRank = 0,
		EnemySpriteAnimator enemyAnimator = null, Sprite idleFallbackSprite = null,
		System.Action onImpact = null, string debugTraceMode = null)
	{
		if (enemyBody == null || playerBodyRt == null)
			yield break;

		const float windupTime = 0.14f;
		const float leapTime = 0.30f;
		const float slamTime = 0.10f;
		const float impactHoldTime = 0.11f;
		const float returnTime = 0.42f;
		const int returnBounceCount = 4;

		EnemyLeapSlamRestPose restPose = GetOrCaptureEnemyLeapSlamRestPose(enemyBody);
		RestoreEnemyLeapSlamBody(enemyBody, restPose);

		Vector3 homeWorld = enemyBody.position;
		Vector3 originalLocalScale = restPose.localScale;
		float canvasReferenceScale = ResolveCanvasReferenceScale(playerBodyRt);
		Rect playerWorldRect = RectWorldRect(playerBodyRt);
		var plan = ResolveEnemyLeapSlamMotionPlan(homeWorld, playerWorldRect, canvasReferenceScale);
		float windupDrop = EnemyLeapSlamReferenceYOffset(EnemyLeapSlamWindupDropNormalizedY, canvasReferenceScale);
		float returnBobHeight = EnemyLeapSlamReferenceYOffset(EnemyLeapSlamReturnBobNormalizedY, canvasReferenceScale);
		bool impactTriggered = false;
		int impactCallbackCount = 0;
		Coroutine hitRoutine = null;
		enemyAnimator?.StopOnFirstIdle(idleFallbackSprite);
		EnemyLeapSlamDebugTrace trace = BeginEnemyLeapSlamDebugTrace(
			enemyBody,
			playerBodyRt,
			enemyAnimator,
			debugTraceMode,
			homeWorld,
			playerWorldRect,
			ResolveCanvasReferenceSize(playerBodyRt),
			canvasReferenceScale,
			plan,
			returnBobHeight);
		if (trace != null)
		{
			if (activeEnemyLeapSlamDebugSerials.TryGetValue(enemyBody, out int activeSerial))
			{
				Debug.LogWarning($"[SlimeLeapTrace] attackSerial={trace.Serial} frame={Time.frameCount} " +
					$"enemyBody={enemyBody.name} previousActiveSerial={activeSerial}");
			}
			activeEnemyLeapSlamDebugSerials[enemyBody] = trace.Serial;
		}

		try
		{
			if (enemyBody.parent != null)
				enemyBody.SetAsLastSibling();

			enemyAnimator?.PlayAttack();
			trace?.LogActionStarted();

			Vector3 squashScale = ScaleBy(originalLocalScale, 1.14f, 0.82f);
			Vector3 impactSquashScale = ScaleBy(originalLocalScale, 1.26f, 0.62f);
			Vector3 windupWorld = homeWorld + Vector3.down * windupDrop;
			float elapsed = 0f;
			while (elapsed < windupTime)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / windupTime);
				float eased = SmoothStep(t);
				enemyBody.position = Vector3.Lerp(homeWorld, windupWorld, eased);
				enemyBody.localScale = Vector3.Lerp(originalLocalScale, squashScale, eased);
				trace?.ObserveAfterSet("windup", t);
				yield return null;
				trace?.LogPositionDriftBeforeNextSet("windup");
			}

			Vector3 stretchScale = ScaleBy(originalLocalScale, 0.92f, 1.08f);
			enemyBody.position = windupWorld;
			trace?.ObserveAfterSet("leap", 0f);
			elapsed = 0f;
			while (elapsed < leapTime)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / leapTime);
				enemyBody.position = ParabolicArc(
					windupWorld,
					plan.leapTargetWorldPosition,
					plan.apexWorldPosition.y,
					t);
				Vector3 launchBaseScale = Vector3.Lerp(squashScale, originalLocalScale, SmoothStep(t));
				enemyBody.localScale = Vector3.Lerp(launchBaseScale, stretchScale, Mathf.Sin(t * Mathf.PI) * 0.65f);
				trace?.ObserveAfterSet("leap", t);
				yield return null;
				trace?.LogPositionDriftBeforeNextSet("leap");
			}

			enemyBody.position = plan.leapTargetWorldPosition;
			enemyBody.localScale = originalLocalScale;
			trace?.ObserveAfterSet("leap", 1f);

			trace?.LogMarker("slam start");
			elapsed = 0f;
			while (elapsed < slamTime)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / slamTime);
				float eased = t * t;
				enemyBody.position = Vector3.Lerp(plan.leapTargetWorldPosition, plan.impactWorldPosition, eased);
				enemyBody.localScale = Vector3.Lerp(stretchScale, originalLocalScale, t);
				trace?.ObserveAfterSet("slam", t);
				yield return null;
				trace?.LogPositionDriftBeforeNextSet("slam");
			}

			enemyBody.position = plan.impactWorldPosition;
			enemyBody.localScale = impactSquashScale;
			trace?.ObserveAfterSet("impact", 1f);
			if (!impactTriggered)
			{
				impactTriggered = true;
				impactCallbackCount++;
				trace?.LogImpactCallback(impactCallbackCount);
				onImpact?.Invoke();
				if (playerBodyImage != null)
				{
					hitRoutine = playerBodyAnimator != null
						? playerBodyAnimator.PlayHitByEnemyRank(enemyRank, damageHalfHearts)
						: null;
					FlashDamage(playerBodyImage);
				}
			}

			yield return new WaitForSeconds(impactHoldTime);

			elapsed = 0f;
			while (elapsed < returnTime)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / returnTime);
				float eased = SmoothStep(t);
				Vector3 basePosition = Vector3.Lerp(plan.impactWorldPosition, homeWorld, eased);
				float bob = Mathf.Sin(t * Mathf.PI * returnBounceCount) * (1f - t);
				basePosition.y += Mathf.Max(0f, bob) * returnBobHeight;
				enemyBody.position = basePosition;
				enemyBody.localScale = Vector3.Lerp(originalLocalScale, squashScale, Mathf.Max(0f, -bob) * 0.28f);
				trace?.ObserveAfterSet("return", t);
				yield return null;
				trace?.LogPositionDriftBeforeNextSet("return");
			}

			RestoreEnemyLeapSlamBody(enemyBody, restPose);
			trace?.ObserveAfterSet("return end", 1f);
			if (enemyAnimator != null)
				enemyAnimator.ReturnToIdle(idleFallbackSprite);

			if (hitRoutine != null)
				yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
			else if (playerBodyImage != null && playerBodyAnimator == null)
				yield return new WaitForSeconds(0.15f);
			trace?.LogSummary(impactCallbackCount);
		}
		finally
		{
			RestoreEnemyLeapSlamBody(enemyBody, restPose);
			if (enemyAnimator != null)
				enemyAnimator.ReturnToIdle(idleFallbackSprite);
			trace?.ObserveAfterSet("finally", 1f);
			if (trace != null)
			{
				if (activeEnemyLeapSlamDebugSerials.TryGetValue(enemyBody, out int activeSerial)
					&& activeSerial == trace.Serial)
				{
					activeEnemyLeapSlamDebugSerials.Remove(enemyBody);
				}
			}
		}
	}

	public static EnemyLeapSlamMotionPlan ResolveEnemyLeapSlamMotionPlanFromBody(
		RectTransform enemyBody,
		RectTransform playerBodyRt)
	{
		if (enemyBody == null || playerBodyRt == null)
			return default;

		EnemyLeapSlamRestPose restPose = GetOrCaptureEnemyLeapSlamRestPose(enemyBody);
		RestoreEnemyLeapSlamBody(enemyBody, restPose);
		return ResolveEnemyLeapSlamMotionPlan(
			enemyBody.position,
			RectWorldRect(playerBodyRt),
			ResolveCanvasReferenceScale(playerBodyRt));
	}

	public static EnemyLeapSlamMotionPlan ResolveEnemyLeapSlamMotionPlan(
		Vector3 homeWorldPosition,
		Rect playerWorldRect,
		float canvasReferenceScale = 1f)
	{
		float playerTop = playerWorldRect.yMax;
		float playerCenterX = playerWorldRect.center.x;
		float leapTargetY = playerTop + EnemyLeapSlamReferenceYOffset(
			EnemyLeapSlamLeapTargetOffsetNormalizedY,
			canvasReferenceScale);
		float apexY = Mathf.Max(homeWorldPosition.y, leapTargetY)
			+ EnemyLeapSlamReferenceYOffset(EnemyLeapSlamApexOffsetNormalizedY, canvasReferenceScale);
		float impactY = playerTop + EnemyLeapSlamReferenceYOffset(
			EnemyLeapSlamImpactOffsetNormalizedY,
			canvasReferenceScale);

		return new EnemyLeapSlamMotionPlan(
			homeWorldPosition,
			new Vector3(Mathf.Lerp(homeWorldPosition.x, playerCenterX, 0.5f), apexY, homeWorldPosition.z),
			new Vector3(playerCenterX, leapTargetY, homeWorldPosition.z),
			new Vector3(playerCenterX, impactY, homeWorldPosition.z));
	}

	public static bool ShouldUseSlimeLeapSlam(EnemyInfo enemy, MobDef def)
	{
		if (enemy == null || def == null)
			return false;
		if (EnemyAttackPositionResolver.ResolveRangeType(def) != EnemyAttackRangeType.Unique)
			return false;
		if (!string.IsNullOrWhiteSpace(def.projectileSpritePath) || !string.IsNullOrWhiteSpace(def.attackVfxSpritePath))
			return false;
		if (!string.IsNullOrWhiteSpace(def.uniqueAttackProfileId))
			return false;

		bool hasSlimeProfile = string.Equals(
			def.enemyDiceProfileId,
			EnemyDiceProfile.SlimeId,
			System.StringComparison.OrdinalIgnoreCase);
		if (!hasSlimeProfile)
			return false;

		bool nameMatches = string.Equals(enemy.name, "슬라임", System.StringComparison.Ordinal)
			|| string.Equals(enemy.name, "Slime", System.StringComparison.OrdinalIgnoreCase);
		return nameMatches
			|| ContainsSlimePathSegment(def.spritePath)
			|| ContainsSlimePathSegment(def.idleSpriteFolderPath)
			|| ContainsSlimePathSegment(def.attackSpriteFolderPath);
	}

	sealed class EnemyLeapSlamDebugTrace
	{
		static readonly float[] LeapSampleTargets = { 0f, 0.25f, 0.5f, 0.75f, 1f };

		readonly RectTransform enemyBody;
		readonly EnemySpriteAnimator enemyAnimator;
		readonly Image enemyImage;
		readonly string mode;
		readonly Vector3 homeWorld;
		readonly Rect playerWorldRect;
		readonly Vector2 canvasReferenceSize;
		readonly float canvasReferenceScale;
		readonly EnemyLeapSlamMotionPlan plan;
		readonly float returnBobHeight;
		readonly float startY;
		int nextLeapSampleIndex;
		float observedMaxY;
		float impactY;
		float endY;
		float lastSetY;
		bool hasLastSetY;
		string lastSetLabel;

		public EnemyLeapSlamDebugTrace(
			int serial,
			RectTransform enemyBody,
			EnemySpriteAnimator enemyAnimator,
			Image enemyImage,
			string mode,
			Vector3 homeWorld,
			Rect playerWorldRect,
			Vector2 canvasReferenceSize,
			float canvasReferenceScale,
			EnemyLeapSlamMotionPlan plan,
			float returnBobHeight)
		{
			Serial = serial;
			this.enemyBody = enemyBody;
			this.enemyAnimator = enemyAnimator;
			this.enemyImage = enemyImage;
			this.mode = string.IsNullOrWhiteSpace(mode) ? "Unknown" : mode;
			this.homeWorld = homeWorld;
			this.playerWorldRect = playerWorldRect;
			this.canvasReferenceSize = canvasReferenceSize;
			this.canvasReferenceScale = canvasReferenceScale;
			this.plan = plan;
			this.returnBobHeight = returnBobHeight;
			startY = enemyBody != null ? enemyBody.position.y : 0f;
			observedMaxY = startY;
			impactY = startY;
			endY = startY;
			LogStart();
		}

		public int Serial { get; }

		public void LogActionStarted()
		{
			Debug.Log(FormatPrefix("action start")
				+ $" sprite={SpriteName()} frame={FrameInfo()} positionY={PositionY():F3} localScale={LocalScale()}");
		}

		public void ObserveAfterSet(string label, float t)
		{
			if (enemyBody == null)
				return;

			float currentY = enemyBody.position.y;
			observedMaxY = Mathf.Max(observedMaxY, currentY);
			lastSetY = currentY;
			hasLastSetY = true;
			lastSetLabel = label;
			if (label == "impact")
				impactY = currentY;
			if (label == "return end" || label == "finally")
				endY = currentY;

			if (label == "leap")
				LogLeapSample(t, currentY);
			else if (label == "impact" || label == "return end")
				LogMarker(label);
		}

		public void LogPositionDriftBeforeNextSet(string afterLabel)
		{
			if (!hasLastSetY || enemyBody == null)
				return;

			float currentY = enemyBody.position.y;
			if (Mathf.Abs(currentY - lastSetY) < 0.05f)
				return;

			Debug.LogWarning(FormatPrefix("position overwritten")
				+ $" after={afterLabel} lastSet={lastSetLabel} expectedY={lastSetY:F3} actualY={currentY:F3} " +
				$"delta={currentY - lastSetY:F3} anchoredY={enemyBody.anchoredPosition.y:F3}");
			observedMaxY = Mathf.Max(observedMaxY, currentY);
			lastSetY = currentY;
		}

		public void LogMarker(string label)
		{
			float currentY = PositionY();
			observedMaxY = Mathf.Max(observedMaxY, currentY);
			Debug.Log(FormatPrefix(label)
				+ $" positionY={currentY:F3} observedMaxY={observedMaxY:F3} sprite={SpriteName()} " +
				$"frame={FrameInfo()} localScale={LocalScale()}");
		}

		public void LogImpactCallback(int callbackCount)
		{
			Debug.Log(FormatPrefix("impact callback")
				+ $" callbackCount={callbackCount} positionY={PositionY():F3} observedMaxY={observedMaxY:F3}");
		}

		public void LogSummary(int impactCallbackCount)
		{
			endY = PositionY();
			Debug.Log(FormatPrefix("summary")
				+ $" calculatedApexY={plan.apexWorldPosition.y:F3} observedRootMaxY={observedMaxY:F3} " +
				$"startY={startY:F3} impactY={impactY:F3} endY={endY:F3} " +
				$"impactCallbackCount={impactCallbackCount} sprite={SpriteName()} frame={FrameInfo()}");
		}

		void LogStart()
		{
			Debug.Log(FormatPrefix("start")
				+ $" homeY={homeWorld.y:F3} playerCenterY={playerWorldRect.center.y:F3} " +
				$"playerTopY={playerWorldRect.yMax:F3} canvasSize={canvasReferenceSize.x:F1}x{canvasReferenceSize.y:F1} " +
				$"scaleY={canvasReferenceScale:F3} calculatedApexY={plan.apexWorldPosition.y:F3} " +
				$"impactY={plan.impactWorldPosition.y:F3} returnBobY={returnBobHeight:F3} " +
				$"startPositionY={startY:F3} localScale={LocalScale()} sprite={SpriteName()} frame={FrameInfo()}");
		}

		void LogLeapSample(float t, float currentY)
		{
			while (nextLeapSampleIndex < LeapSampleTargets.Length
				&& t + 0.0001f >= LeapSampleTargets[nextLeapSampleIndex])
			{
				float sampleT = LeapSampleTargets[nextLeapSampleIndex];
				Debug.Log(FormatPrefix($"leap t={sampleT:F2}")
					+ $" actualT={t:F3} positionY={currentY:F3} observedMaxY={observedMaxY:F3} " +
					$"sprite={SpriteName()} frame={FrameInfo()} localScale={LocalScale()}");
				nextLeapSampleIndex++;
			}
		}

		string FormatPrefix(string eventName)
		{
			return $"[SlimeLeapTrace] attackSerial={Serial} frame={Time.frameCount} mode={mode} " +
				$"enemyBody={BodyName()} event=\"{eventName}\"";
		}

		string BodyName()
		{
			return enemyBody != null ? enemyBody.name : "null";
		}

		string SpriteName()
		{
			Sprite sprite = enemyImage != null ? enemyImage.sprite : null;
			return sprite != null ? sprite.name : "null";
		}

		string FrameInfo()
		{
			return enemyAnimator != null
				? $"{enemyAnimator.CurrentFrameIndex}/{enemyAnimator.CurrentFrameCount}"
				: "unknown";
		}

		string LocalScale()
		{
			Vector3 scale = enemyBody != null ? enemyBody.localScale : Vector3.zero;
			return $"({scale.x:F3},{scale.y:F3},{scale.z:F3})";
		}

		float PositionY()
		{
			return enemyBody != null ? enemyBody.position.y : 0f;
		}
	}

	/// <summary>
	/// 적 바디의 활 위치에서 플레이어 상체 쪽으로 발사체를 날린다.
	/// 발사체 스프라이트는 오른쪽을 향한 원본을 기준으로 진행 방향에 맞춰 회전한다.
	/// </summary>
	public IEnumerator EnemyProjectileAttack(Image projectileImage, RectTransform shooterBody,
		RectTransform playerBodyRt, Image playerBodyImage, PlayerBodyAnimator playerBodyAnimator = null,
		PlayerJumpAnimator jumpAnimator = null, bool blocked = false, int damageHalfHearts = 0,
		float duration = 0.42f, EnemyProjectileAttachmentFollower attachmentFollower = null,
		int enemyRank = 0, Vector3? projectileStartWorld = null, Vector3? projectileEndWorld = null,
		System.Action onImpact = null)
	{
		if (projectileImage == null || shooterBody == null || playerBodyRt == null)
			yield break;

		RectTransform projectileRt = projectileImage.rectTransform;
		Vector3 start = projectileStartWorld.HasValue
			? projectileStartWorld.Value
			: (attachmentFollower != null && attachmentFollower.TryGetReleaseWorldPosition(out var releasePosition)
				? releasePosition
				: RectPointWorld(shooterBody, 0.08f, 0.55f));
		Vector3 end = projectileEndWorld ?? RectPointWorld(playerBodyRt, 0.62f, 0.58f);
		Vector3 delta = end - start;
		float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

		projectileRt.position = start;
		projectileRt.localRotation = Quaternion.Euler(0f, 0f, angle);
		projectileImage.gameObject.SetActive(true);

		Coroutine defenseRoutine = null;
		bool defenseSessionActive = playerBodyAnimator != null && playerBodyAnimator.IsDefenseSessionActive;
		if (blocked)
		{
			defenseRoutine = !defenseSessionActive && playerBodyAnimator != null ? playerBodyAnimator.PlayDefense() : null;
			if (defenseRoutine == null && jumpAnimator != null && !defenseSessionActive)
				jumpAnimator.Play();
		}

		float elapsed = 0f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.Clamp01(elapsed / duration);
			float eased = t * t * (3f - 2f * t);
			Vector3 pos = Vector3.Lerp(start, end, eased);
			pos.y += Mathf.Sin(t * Mathf.PI) * 14f;
			projectileRt.position = pos;
			yield return null;
		}

		projectileImage.gameObject.SetActive(false);
		onImpact?.Invoke();

		if (!blocked && playerBodyImage != null)
		{
			Coroutine hitRoutine = playerBodyAnimator != null
				? playerBodyAnimator.PlayHitByEnemyRank(enemyRank, damageHalfHearts)
				: null;
			FlashDamage(playerBodyImage);
			if (hitRoutine != null)
				yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
			else
				yield return new WaitForSeconds(0.15f);
		}
		else if (blocked && playerBodyImage != null)
		{
			FlashHit(playerBodyImage, new Color(0.35f, 0.85f, 1f), 0.14f, 0.24f);
			yield return new WaitForSeconds(0.12f);
		}
		else if (defenseSessionActive)
		{
			yield return new WaitForSeconds(0.12f);
		}
		else if (defenseRoutine != null)
		{
			yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
		}
		else if (jumpAnimator != null)
		{
			yield return new WaitWhile(() => jumpAnimator.IsPlaying);
		}
	}

	// ── 유틸 ──

	static EnemyLeapSlamDebugTrace BeginEnemyLeapSlamDebugTrace(
		RectTransform enemyBody,
		RectTransform playerBodyRt,
		EnemySpriteAnimator enemyAnimator,
		string mode,
		Vector3 homeWorld,
		Rect playerWorldRect,
		Vector2 canvasReferenceSize,
		float canvasReferenceScale,
		EnemyLeapSlamMotionPlan plan,
		float returnBobHeight)
	{
		if (!EnableEnemyLeapSlamDebugTrace)
			return null;

		int serial = ++slimeLeapDebugSerial;
		var enemyImage = enemyBody != null ? enemyBody.GetComponent<Image>() : null;
		return new EnemyLeapSlamDebugTrace(
			serial,
			enemyBody,
			enemyAnimator,
			enemyImage,
			mode,
			homeWorld,
			playerWorldRect,
			canvasReferenceSize,
			canvasReferenceScale,
			plan,
			returnBobHeight);
	}

	/// <summary>월드 좌표를 slot의 부모 기준 로컬 좌표로 변환.</summary>
	static Vector3 WorldToLocal(RectTransform slot, Vector3 worldPos)
	{
		Vector3 delta = worldPos - slot.position;
		Vector3 localDelta = slot.parent != null
			? slot.parent.InverseTransformVector(delta)
			: delta;
		return slot.localPosition + localDelta;
	}

	static float SmoothStep(float t)
	{
		return t * t * (3f - 2f * t);
	}

	static Vector3 ScaleBy(Vector3 scale, float xMultiplier, float yMultiplier)
	{
		return new Vector3(scale.x * xMultiplier, scale.y * yMultiplier, scale.z);
	}

	static EnemyLeapSlamRestPose GetOrCaptureEnemyLeapSlamRestPose(RectTransform enemyBody)
	{
		if (enemyLeapSlamRestPoses.TryGetValue(enemyBody, out var restPose)
			&& restPose.MatchesLayout(enemyBody))
		{
			return restPose;
		}

		restPose = EnemyLeapSlamRestPose.Capture(enemyBody);
		enemyLeapSlamRestPoses[enemyBody] = restPose;
		return restPose;
	}

	static Vector3 ParabolicArc(Vector3 start, Vector3 end, float apexY, float t)
	{
		Vector3 position = Vector3.Lerp(start, end, t);
		float midpointY = Mathf.Lerp(start.y, end.y, 0.5f);
		float arcHeight = Mathf.Max(0f, apexY - midpointY);
		position.y += 4f * t * (1f - t) * arcHeight;
		return position;
	}

	static Rect RectWorldRect(RectTransform rt)
	{
		if (EnemyVisualBoundsResolver.TryResolveWorldBounds(rt, out Rect bounds))
			return bounds;
		return Rect.zero;
	}

	static bool ContainsSlimePathSegment(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
			return false;
		string normalized = path.Replace('\\', '/');
		return normalized.IndexOf("/Slime/", System.StringComparison.OrdinalIgnoreCase) >= 0
			|| normalized.EndsWith("/Slime", System.StringComparison.OrdinalIgnoreCase);
	}

	static bool Approximately(Vector2 a, Vector2 b)
	{
		return Mathf.Abs(a.x - b.x) < 0.001f
			&& Mathf.Abs(a.y - b.y) < 0.001f;
	}

	static float ResolveCanvasReferenceScale(RectTransform source)
	{
		var scaler = source != null ? source.GetComponentInParent<CanvasScaler>() : null;
		if (scaler == null || scaler.referenceResolution.y <= 0f)
			return 1f;
		return Mathf.Max(0.01f, scaler.referenceResolution.y / EnemyLeapSlamReferenceResolution.y);
	}

	static Vector2 ResolveCanvasReferenceSize(RectTransform source)
	{
		var scaler = source != null ? source.GetComponentInParent<CanvasScaler>() : null;
		if (scaler == null || scaler.referenceResolution.x <= 0f || scaler.referenceResolution.y <= 0f)
			return EnemyLeapSlamReferenceResolution;
		return scaler.referenceResolution;
	}

	static float EnemyLeapSlamReferenceYOffset(float normalizedY, float canvasReferenceScale)
	{
		return EnemyLeapSlamReferenceResolution.y * normalizedY * Mathf.Max(0.01f, canvasReferenceScale);
	}

	static void RestoreEnemyLeapSlamBody(RectTransform enemyBody, EnemyLeapSlamRestPose restPose)
	{
		if (enemyBody == null)
			return;

		enemyBody.localScale = restPose.localScale;
		enemyBody.offsetMin = restPose.offsetMin;
		enemyBody.offsetMax = restPose.offsetMax;
		enemyBody.anchoredPosition = restPose.anchoredPosition;
		enemyBody.position = restPose.homeWorldPosition;
		if (enemyBody.parent == null)
			return;

		int restoredIndex = Mathf.Clamp(restPose.siblingIndex, 0, enemyBody.parent.childCount - 1);
		enemyBody.SetSiblingIndex(restoredIndex);
	}

	static Vector3 RectPointWorld(RectTransform rt, float normalizedX, float normalizedY)
	{
		return EnemyVisualBoundsResolver.ResolveWorldPoint(rt, normalizedX, normalizedY,
			rt != null ? rt.position : Vector3.zero);
	}
}
