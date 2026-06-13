using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 해골 idle 화살을 적 스프라이트 애니메이션 프레임 진행도에 맞춰 보정한다.
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

	RectTransform projectileRt;
	bool following;

	public bool IsFollowing => following && isActiveAndEnabled && projectileRt != null;

	void Awake()
	{
		CacheReferences();
	}

	void LateUpdate()
	{
		if (!IsFollowing)
			return;
		ApplyPose(animator != null ? animator.CurrentFrameNormalized : 0f);
	}

	public void SetFollowing(bool value)
	{
		CacheReferences();
		following = value && projectileRt != null && projectileImage != null;
		if (following)
			ApplyPose(animator != null ? animator.CurrentFrameNormalized : 0f);
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

		projectileRt.anchorMin = new Vector2(0.5f, 0.5f);
		projectileRt.anchorMax = new Vector2(0.5f, 0.5f);
		projectileRt.pivot = new Vector2(0.5f, 0.5f);
		projectileRt.sizeDelta = size;
		projectileRt.anchoredPosition = new Vector2(
			Mathf.Lerp(parentRect.xMin, parentRect.xMax, Evaluate(normalizedX, t)),
			Mathf.Lerp(parentRect.yMin, parentRect.yMax, Evaluate(normalizedY, t)));
		projectileRt.localRotation = Quaternion.Euler(0f, 0f, Evaluate(rotation, t));
		projectileRt.localScale = new Vector3(Mathf.Max(0.01f, Evaluate(scaleX, t)), 1f, 1f);
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
