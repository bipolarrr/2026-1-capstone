using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 재사용 가능한 전투 애니메이션 유틸리티.
/// 각 메서드는 독립적인 코루틴으로, 호출부에서 조합하여 연출 시퀀스를 구성한다.
/// </summary>
public class BattleAnimations : MonoBehaviour
{
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
		float rushTime = 0.06f, float holdTime = 0.04f, float returnTime = 0.1f)
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
		int damageHalfHearts = 0)
	{
		if (slot == null || playerBodyRt == null)
			yield break;

		Vector3 slotOriginalLocal = slot.localPosition;

		// 플레이어 앞까지 이동 (주사위 2개 정도의 간격 유지) — DiceBattle과 동일 공식.
		float scale = enemyBody != null ? enemyBody.lossyScale.x : 1f;
		float bodyWidth = enemyBody != null ? enemyBody.rect.width * scale : 0f;
		float gap = 42f * scale * 2.4f;
		Vector3 playerWorld = playerBodyRt.position;
		Vector3 slotWorld = slot.position;
		Vector3 frontWorld = new Vector3(playerWorld.x + bodyWidth + gap, slotWorld.y, slotWorld.z);

		yield return StartCoroutine(WalkTo(slot, frontWorld, 0.4f));

		// 슬램 타격
		Vector3 slamTarget = new Vector3(playerWorld.x, slot.position.y, slot.position.z);
		yield return StartCoroutine(QuickSlam(slot, slamTarget));

		if (playerBodyImage != null)
		{
			playerBodyAnimator?.PlayHitByDamage(damageHalfHearts);
			FlashHit(playerBodyImage);
		}

		yield return new WaitForSeconds(0.15f);

		yield return StartCoroutine(WalkBack(slot, slotOriginalLocal, 0.5f));
	}

	// ── 유틸 ──

	/// <summary>월드 좌표를 slot의 부모 기준 로컬 좌표로 변환.</summary>
	static Vector3 WorldToLocal(RectTransform slot, Vector3 worldPos)
	{
		Vector3 delta = worldPos - slot.position;
		Vector3 localDelta = slot.parent != null
			? slot.parent.InverseTransformVector(delta)
			: delta;
		return slot.localPosition + localDelta;
	}
}
