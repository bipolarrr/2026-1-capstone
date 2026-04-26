using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class EnemySpriteAnimator : MonoBehaviour
{
	[SerializeField] Image targetImage;
	[SerializeField] float idleFrameRate = 12f;
	[SerializeField] float actionFrameRate = 18f;
	[SerializeField] Sprite[] idleSprites;
	[SerializeField] Sprite[] attackSprites;
	[SerializeField] Sprite[] hitSprites;

	Coroutine idleRoutine;
	Coroutine actionRoutine;
	bool idlePaused;
	int idleIndex;
	int idleDirection = 1;

	public bool HasHitSprites => hitSprites != null && hitSprites.Length > 0;
	public bool HasAttackSprites => attackSprites != null && attackSprites.Length > 0;

	void Awake()
	{
		if (targetImage == null)
			targetImage = GetComponent<Image>();
	}

	void OnEnable()
	{
		StartIdle();
	}

	void OnDisable()
	{
		StopAllAnimation();
	}

	public void Bind(EnemySpriteAnimationSet set, Sprite fallbackSprite)
	{
		idleSprites = set != null ? set.idleSprites : null;
		attackSprites = set != null ? set.attackSprites : null;
		hitSprites = set != null ? set.hitSprites : null;
		idleIndex = 0;
		idleDirection = 1;

		if (targetImage != null)
		{
			Sprite first = FirstSprite(idleSprites);
			targetImage.sprite = first != null ? first : fallbackSprite;
		}

		StartIdle();
	}

	public Coroutine PlayAttack()
	{
		return PlayOneShot(attackSprites);
	}

	public Coroutine PlayHit()
	{
		return PlayOneShot(hitSprites);
	}

	void StartIdle()
	{
		if (!isActiveAndEnabled)
			return;
		if (idleRoutine != null)
			StopCoroutine(idleRoutine);
		idlePaused = false;
		if (idleSprites != null && idleSprites.Length > 0 && targetImage != null)
			idleRoutine = StartCoroutine(IdleLoop());
		else
			idleRoutine = null;
	}

	Coroutine PlayOneShot(Sprite[] sprites)
	{
		if (sprites == null || sprites.Length == 0 || targetImage == null || !isActiveAndEnabled)
			return null;
		if (actionRoutine != null)
			StopCoroutine(actionRoutine);
		actionRoutine = StartCoroutine(PlayOneShotRoutine(sprites));
		return actionRoutine;
	}

	IEnumerator IdleLoop()
	{
		float delay = 1f / Mathf.Max(1f, idleFrameRate);
		while (true)
		{
			if (!idlePaused && idleSprites != null && idleSprites.Length > 0)
			{
				var sprite = idleSprites[idleIndex % idleSprites.Length];
				if (sprite != null)
					targetImage.sprite = sprite;
				AdvancePingPongIdleIndex();
			}
			yield return new WaitForSeconds(delay);
		}
	}

	void AdvancePingPongIdleIndex()
	{
		if (idleSprites == null || idleSprites.Length <= 1)
		{
			idleIndex = 0;
			idleDirection = 1;
			return;
		}

		idleIndex += idleDirection;
		if (idleIndex >= idleSprites.Length)
		{
			idleDirection = -1;
			idleIndex = idleSprites.Length - 2;
		}
		else if (idleIndex < 0)
		{
			idleDirection = 1;
			idleIndex = 1;
		}
	}

	IEnumerator PlayOneShotRoutine(Sprite[] sprites)
	{
		idlePaused = true;
		float delay = 1f / Mathf.Max(1f, actionFrameRate);
		for (int i = 0; i < sprites.Length; i++)
		{
			if (sprites[i] != null)
				targetImage.sprite = sprites[i];
			yield return new WaitForSeconds(delay);
		}
		actionRoutine = null;
		idlePaused = false;
	}

	void StopAllAnimation()
	{
		if (idleRoutine != null)
			StopCoroutine(idleRoutine);
		if (actionRoutine != null)
			StopCoroutine(actionRoutine);
		idleRoutine = null;
		actionRoutine = null;
		idlePaused = false;
	}

	static Sprite FirstSprite(Sprite[] sprites)
	{
		if (sprites == null) return null;
		for (int i = 0; i < sprites.Length; i++)
			if (sprites[i] != null) return sprites[i];
		return null;
	}
}
