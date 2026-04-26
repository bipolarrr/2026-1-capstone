using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 플레이어 공격 시 PlayerBody 스프라이트 시퀀스와 투사체를 재생한다.
/// </summary>
public class PlayerAttackAnimator : MonoBehaviour
{
	[SerializeField] Image playerBody;
	[SerializeField] Image weaponProjectile;
	[SerializeField] Sprite[] attackSprites;
	[SerializeField] PlayerBodyAnimator bodyAnimator;
	[SerializeField] float frameRate = 45f;
	[SerializeField] int frameStep = 2;
	[SerializeField] float projectileStartRatio = 0.22f;
	[SerializeField] float projectileEndRatio = 0.78f;
	[SerializeField] float projectileArcHeight = 120f;
	[SerializeField] Vector2 projectileStartOffset = new Vector2(85f, 170f);
	[SerializeField] Vector2 projectileTargetOffset = new Vector2(0f, 95f);

	Coroutine currentAnim;
	RectTransform currentTarget;

	public Coroutine Play(RectTransform target)
	{
		if (attackSprites == null || attackSprites.Length == 0 || playerBody == null)
			return null;

		if (currentAnim != null)
		{
			StopCoroutine(currentAnim);
			if (bodyAnimator != null)
				bodyAnimator.ResumeAuto();
			if (weaponProjectile != null)
				weaponProjectile.gameObject.SetActive(false);
		}

		currentTarget = target;
		currentAnim = StartCoroutine(PlaySequence());
		return currentAnim;
	}

	IEnumerator PlaySequence()
	{
		if (bodyAnimator != null)
			bodyAnimator.PauseAuto();

		float frameDuration = 1f / Mathf.Max(1f, frameRate);
		int step = Mathf.Max(1, frameStep);
		int frameCount = Mathf.CeilToInt((float)attackSprites.Length / step);
		float duration = Mathf.Max(frameDuration, frameCount * frameDuration);

		RectTransform projectileRt = weaponProjectile != null ? weaponProjectile.rectTransform : null;
		bool showProjectile = projectileRt != null && currentTarget != null;
		Vector3 projectileStart = Vector3.zero;
		Vector3 projectileEnd = Vector3.zero;
		if (showProjectile)
		{
			projectileStart = playerBody.rectTransform.position + (Vector3)projectileStartOffset;
			projectileEnd = currentTarget.position + (Vector3)projectileTargetOffset;
			weaponProjectile.gameObject.SetActive(false);
		}

		float elapsed = 0f;
		int lastFrame = -1;
		while (elapsed < duration)
		{
			int sequenceFrame = Mathf.Clamp(Mathf.FloorToInt(elapsed / frameDuration), 0, frameCount - 1);
			int spriteIndex = Mathf.Clamp(sequenceFrame * step, 0, attackSprites.Length - 1);
			if (spriteIndex != lastFrame && attackSprites[spriteIndex] != null)
			{
				playerBody.sprite = attackSprites[spriteIndex];
				lastFrame = spriteIndex;
			}

			if (showProjectile)
				UpdateProjectile(projectileRt, projectileStart, projectileEnd, elapsed / duration);

			elapsed += Time.deltaTime;
			yield return null;
		}

		if (showProjectile)
			weaponProjectile.gameObject.SetActive(false);

		if (bodyAnimator != null)
			bodyAnimator.ResumeAuto();

		currentTarget = null;
		currentAnim = null;
	}

	void UpdateProjectile(RectTransform projectileRt, Vector3 start, Vector3 end, float sequenceT)
	{
		float startT = Mathf.Clamp01(projectileStartRatio);
		float endT = Mathf.Clamp(projectileEndRatio, startT + 0.01f, 1f);
		if (sequenceT < startT || sequenceT > endT)
		{
			weaponProjectile.gameObject.SetActive(false);
			return;
		}

		if (!weaponProjectile.gameObject.activeSelf)
			weaponProjectile.gameObject.SetActive(true);

		float t = Mathf.InverseLerp(startT, endT, sequenceT);
		float eased = t * t * (3f - 2f * t);
		Vector3 pos = Vector3.Lerp(start, end, eased);
		pos.y += Mathf.Sin(t * Mathf.PI) * projectileArcHeight;
		projectileRt.position = pos;
		projectileRt.localEulerAngles = new Vector3(0f, 0f, Mathf.Lerp(-30f, 390f, t));
	}
}
