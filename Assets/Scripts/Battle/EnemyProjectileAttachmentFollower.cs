using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 해골 idle 화살을 적 스프라이트 애니메이션 프레임 진행도와 투사체 발사 상태에 맞춰 보정한다.
/// </summary>
public class EnemyProjectileAttachmentFollower : MonoBehaviour
{
	[SerializeField] EnemySpriteAnimator animator;
	[SerializeField] Image projectileImage;
	[SerializeField] Vector2 size = new Vector2(132f, 32f);
	[SerializeField] AnimationCurve normalizedX = AnimationCurve.EaseInOut(0f, 0.24f, 1f, 0.37f);
	[SerializeField] AnimationCurve normalizedY = AnimationCurve.EaseInOut(0f, 0.57f, 1f, 0.50f);
	[SerializeField] AnimationCurve rotation = AnimationCurve.EaseInOut(0f, -5f, 1f, 7f);
	[SerializeField] AnimationCurve scaleX = AnimationCurve.EaseInOut(0f, 0.82f, 1f, 1.08f);
	[SerializeField, Range(0f, 1f)] float releasePointOnArrow = 0.12f;
	[SerializeField] string flightProjectileObjectName = "EnemyProjectile";
	[SerializeField] float flightAcquireDistance = 96f;
	[SerializeField] Vector2 fallingNormalizedPosition = new Vector2(0.44f, 0.18f);
	[SerializeField] float fallingRotation = -68f;
	[SerializeField] float fallingScaleX = 0.96f;

	static EnemyProjectileAttachmentFollower activeFlightOwner;

	RectTransform projectileRt;
	RectTransform activeFlightProjectileRt;
	bool following;
	bool hiddenByFlightProjectile;
	bool hasEverAttached;
	bool forceFalling;
	bool restoringAfterDisable;
	bool applicationQuitting;

	public bool IsFollowing => following && isActiveAndEnabled && projectileRt != null && !forceFalling;

	void Awake()
	{
		CacheReferences();
	}

	void OnDisable()
	{
		if (!ShouldRestoreForDeathDisable())
			return;

		restoringAfterDisable = true;
		gameObject.SetActive(true);
		restoringAfterDisable = false;
		SetFalling(true);
	}

	void OnApplicationQuit()
	{
		applicationQuitting = true;
	}

	void LateUpdate()
	{
		CacheReferences();
		if (projectileRt == null)
			return;

		float frameT = animator != null ? animator.CurrentFrameNormalized : 0f;
		if (ShouldUseFallingPose())
		{
			ApplyFallingPose(frameT);
			SetProjectileImageVisible(!hiddenByFlightProjectile);
			return;
		}

		if (following)
			ApplyPose(frameT);
		UpdateFlightProjectileSuppression();
	}

	public void SetFollowing(bool value)
	{
		CacheReferences();
		if (!value && forceFalling)
		{
			following = false;
			ApplyFallingPose(animator != null ? animator.CurrentFrameNormalized : 1f);
			SetProjectileImageVisible(true);
			return;
		}

		forceFalling = false;
		following = value && projectileRt != null && projectileImage != null;
		if (following)
		{
			hasEverAttached = true;
			ApplyPose(animator != null ? animator.CurrentFrameNormalized : 0f);
		}
		else
		{
			ReleaseFlightOwnership();
		}
		SetProjectileImageVisible(following && !hiddenByFlightProjectile);
	}

	public void SetFalling(bool value)
	{
		CacheReferences();
		forceFalling = value && projectileRt != null && projectileImage != null;
		if (forceFalling)
		{
			hasEverAttached = true;
			following = false;
			ReleaseFlightOwnership();
			ApplyFallingPose(animator != null ? animator.CurrentFrameNormalized : 1f);
		}
		SetProjectileImageVisible((following || forceFalling) && !hiddenByFlightProjectile);
	}

	public bool TryGetReleaseWorldPosition(out Vector3 position)
	{
		position = Vector3.zero;
		if (!IsFollowing)
			return false;

		float localX = Mathf.Lerp(projectileRt.rect.xMin, projectileRt.rect.xMax, releasePointOnArrow);
		position = projectileRt.TransformPoint(new Vector3(localX, 0f, 0f));
		return true;
	}

	public void ApplyPose(float normalizedFrame)
	{
		CacheReferences();
		if (projectileRt == null)
			return;

		float t = Mathf.Clamp01(normalizedFrame);
		var parentRt = projectileRt.parent as RectTransform;
		Rect parentRect = parentRt != null
			? EnemyVisualBoundsResolver.ResolveRenderedLocalRect(parentRt)
			: new Rect(0f, 0f, 1f, 1f);

		ApplyRectPose(
			parentRect,
			Evaluate(normalizedX, t),
			Evaluate(normalizedY, t),
			Evaluate(rotation, t),
			Mathf.Max(0.01f, Evaluate(scaleX, t)));
	}

	void ApplyFallingPose(float normalizedFrame)
	{
		float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalizedFrame));
		var parentRt = projectileRt != null ? projectileRt.parent as RectTransform : null;
		Rect parentRect = parentRt != null
			? EnemyVisualBoundsResolver.ResolveRenderedLocalRect(parentRt)
			: new Rect(0f, 0f, 1f, 1f);

		float idleX = Evaluate(normalizedX, t);
		float idleY = Evaluate(normalizedY, t);
		float idleRot = Evaluate(rotation, t);
		float idleScale = Mathf.Max(0.01f, Evaluate(scaleX, t));

		ApplyRectPose(
			parentRect,
			Mathf.Lerp(idleX, fallingNormalizedPosition.x, t),
			Mathf.Lerp(idleY, fallingNormalizedPosition.y, t),
			Mathf.LerpAngle(idleRot, fallingRotation, t),
			Mathf.Lerp(idleScale, fallingScaleX, t));
	}

	void ApplyRectPose(Rect parentRect, float normalizedPosX, float normalizedPosY, float zRotation, float localScaleX)
	{
		projectileRt.anchorMin = new Vector2(0.5f, 0.5f);
		projectileRt.anchorMax = new Vector2(0.5f, 0.5f);
		projectileRt.pivot = new Vector2(0.5f, 0.5f);
		projectileRt.sizeDelta = size;
		projectileRt.anchoredPosition = new Vector2(
			Mathf.Lerp(parentRect.xMin, parentRect.xMax, normalizedPosX),
			Mathf.Lerp(parentRect.yMin, parentRect.yMax, normalizedPosY));
		projectileRt.localRotation = Quaternion.Euler(0f, 0f, zRotation);
		projectileRt.localScale = new Vector3(Mathf.Max(0.01f, localScaleX), 1f, 1f);
	}

	void UpdateFlightProjectileSuppression()
	{
		if (!following || projectileImage == null || projectileRt == null)
		{
			ClearFlightSuppression();
			return;
		}

		if (activeFlightOwner != null && activeFlightOwner != this)
		{
			SetProjectileImageVisible(true);
			return;
		}

		if (activeFlightOwner == this)
		{
			if (activeFlightProjectileRt != null && activeFlightProjectileRt.gameObject.activeInHierarchy)
			{
				SetProjectileImageVisible(false);
				return;
			}
			ClearFlightSuppression();
			SetProjectileImageVisible(true);
			return;
		}

		var flight = FindActiveFlightProjectile();
		if (flight == null)
		{
			SetProjectileImageVisible(true);
			return;
		}

		if (!TryGetReleaseWorldPosition(out var releasePosition))
			return;
		if (Vector3.Distance(releasePosition, flight.position) > Mathf.Max(1f, flightAcquireDistance))
			return;

		activeFlightOwner = this;
		activeFlightProjectileRt = flight;
		hiddenByFlightProjectile = true;
		SynchronizeFlightProjectileVisual(flight, releasePosition);
		SetProjectileImageVisible(false);
	}

	RectTransform FindActiveFlightProjectile()
	{
		if (string.IsNullOrWhiteSpace(flightProjectileObjectName))
			return null;
		var go = GameObject.Find(flightProjectileObjectName);
		if (go == null || go == gameObject || !go.activeInHierarchy)
			return null;
		return go.GetComponent<RectTransform>();
	}

	void SynchronizeFlightProjectileVisual(RectTransform flight, Vector3 releasePosition)
	{
		if (flight == null)
			return;

		flight.position = releasePosition;
		flight.rotation = projectileRt.rotation;
		var flightImage = flight.GetComponent<Image>();
		if (flightImage != null && projectileImage != null)
		{
			flightImage.sprite = projectileImage.sprite;
			flightImage.color = projectileImage.color;
			flightImage.preserveAspect = projectileImage.preserveAspect;
		}
	}

	bool ShouldUseFallingPose()
	{
		return forceFalling
			|| (hasEverAttached
				&& animator != null
				&& (animator.IsDeathAnimationPlaying || animator.IsDeathLocked));
	}

	bool ShouldRestoreForDeathDisable()
	{
		if (!Application.isPlaying || applicationQuitting || restoringAfterDisable || !hasEverAttached)
			return false;
		if (projectileImage == null || projectileImage.sprite == null)
			return false;
		if (animator == null || !animator.HasDeathAnimation)
			return false;
		var parent = transform.parent;
		return parent != null && parent.gameObject.activeInHierarchy;
	}

	void ClearFlightSuppression()
	{
		if (activeFlightOwner == this)
			activeFlightOwner = null;
		activeFlightProjectileRt = null;
		hiddenByFlightProjectile = false;
	}

	void ReleaseFlightOwnership()
	{
		ClearFlightSuppression();
		SetProjectileImageVisible(following || forceFalling);
	}

	void SetProjectileImageVisible(bool visible)
	{
		if (projectileImage != null)
			projectileImage.enabled = visible;
	}

	void CacheReferences()
	{
		if (projectileImage == null)
			projectileImage = GetComponent<Image>();
		if (projectileRt == null && projectileImage != null)
			projectileRt = projectileImage.rectTransform;
	}

	static float Evaluate(AnimationCurve curve, float t)
	{
		return curve != null && curve.length > 0 ? curve.Evaluate(t) : 0f;
	}
}