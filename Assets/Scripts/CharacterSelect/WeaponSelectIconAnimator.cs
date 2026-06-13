using UnityEngine;
using UnityEngine.EventSystems;

public sealed class WeaponSelectIconAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
	public enum IconMode
	{
		Dice,
		Holdem,
		Mahjong,
	}

	[SerializeField] private IconMode mode;
	[SerializeField] private Transform[] diceTargets;
	[SerializeField] private RectTransform holdemBackCard;
	[SerializeField] private RectTransform holdemAceSpadesCard;
	[SerializeField] private RectTransform holdemKingHeartsCard;
	[SerializeField] private Vector2 holdemLeftCardTarget = new Vector2(-60f, 8f);
	[SerializeField] private Vector2 holdemRightCardTarget = new Vector2(60f, 8f);
	[SerializeField] private RectTransform mahjongTile3;
	[SerializeField] private RectTransform mahjongTile4;
	[SerializeField] private RectTransform mahjongRedFive;
	[SerializeField] private Vector2 mahjongTile3HoverPosition = new Vector2(-102f, 2f);
	[SerializeField] private Vector2 mahjongTile4HoverPosition = new Vector2(0f, 2f);
	[SerializeField] private Vector2 mahjongRedFiveTargetPosition = new Vector2(102f, 2f);

	const float HoverTransitionDuration = 0.26f;
	const float DiceBobAmplitude = 0.08f;
	const float DiceBobSpeed = 1.15f;

	bool isHovered;
	bool initialized;
	bool warnedMissingReferences;
	float hoverProgress;

	Vector3[] diceBasePositions;

	CanvasGroup holdemBackGroup;
	CanvasGroup holdemAceGroup;
	CanvasGroup holdemKingGroup;
	Vector2 holdemBackBasePosition;
	Vector2 holdemAceBasePosition;
	Vector2 holdemKingBasePosition;
	Vector3 holdemBackBaseScale;
	Vector3 holdemAceBaseScale;
	Vector3 holdemKingBaseScale;
	float holdemBackBaseRotation;
	float holdemAceBaseRotation;
	float holdemKingBaseRotation;

	CanvasGroup mahjongRedFiveGroup;
	Vector2 mahjongTile3IdlePosition;
	Vector2 mahjongTile4IdlePosition;
	Vector2 mahjongRedFiveOriginPosition;
	Vector3 mahjongTile3BaseScale;
	Vector3 mahjongTile4BaseScale;
	Vector3 mahjongRedFiveBaseScale;
	float mahjongTile3BaseRotation;
	float mahjongTile4BaseRotation;
	float mahjongRedFiveOriginRotation;

	void Awake()
	{
		EnsureInitialized();
		ApplyVisuals();
	}

	void OnEnable()
	{
		EnsureInitialized();
		ApplyVisuals();
	}

	void OnDisable()
	{
		isHovered = false;
		hoverProgress = 0f;
		ApplyVisuals();
	}

	void Update()
	{
		EnsureInitialized();

		float targetProgress = isHovered ? 1f : 0f;
		if (!Mathf.Approximately(hoverProgress, targetProgress))
		{
			float step = Time.unscaledDeltaTime / HoverTransitionDuration;
			hoverProgress = Mathf.MoveTowards(hoverProgress, targetProgress, step);
		}

		ApplyVisuals();
	}

	public void OnPointerEnter(PointerEventData eventData)
	{
		isHovered = true;
	}

	public void OnPointerExit(PointerEventData eventData)
	{
		isHovered = false;
	}

	void EnsureInitialized()
	{
		if (initialized)
			return;

		CacheDiceTargets();
		CacheHoldemTargets();
		CacheMahjongTargets();
		WarnIfMissingRequiredReferences();
		initialized = true;
	}

	void CacheDiceTargets()
	{
		if (diceTargets == null)
			return;

		diceBasePositions = new Vector3[diceTargets.Length];
		for (int i = 0; i < diceTargets.Length; i++)
		{
			if (diceTargets[i] != null)
				diceBasePositions[i] = diceTargets[i].localPosition;
		}
	}

	void CacheHoldemTargets()
	{
		holdemBackGroup = EnsureCanvasGroup(holdemBackCard);
		holdemAceGroup = EnsureCanvasGroup(holdemAceSpadesCard);
		holdemKingGroup = EnsureCanvasGroup(holdemKingHeartsCard);

		holdemBackBasePosition = GetAnchoredPosition(holdemBackCard);
		holdemAceBasePosition = GetAnchoredPosition(holdemAceSpadesCard);
		holdemKingBasePosition = GetAnchoredPosition(holdemKingHeartsCard);
		holdemBackBaseScale = GetLocalScale(holdemBackCard);
		holdemAceBaseScale = GetLocalScale(holdemAceSpadesCard);
		holdemKingBaseScale = GetLocalScale(holdemKingHeartsCard);
		holdemBackBaseRotation = GetLocalRotationZ(holdemBackCard);
		holdemAceBaseRotation = GetLocalRotationZ(holdemAceSpadesCard);
		holdemKingBaseRotation = GetLocalRotationZ(holdemKingHeartsCard);
	}

	void CacheMahjongTargets()
	{
		mahjongRedFiveGroup = EnsureCanvasGroup(mahjongRedFive);

		mahjongTile3IdlePosition = GetAnchoredPosition(mahjongTile3);
		mahjongTile4IdlePosition = GetAnchoredPosition(mahjongTile4);
		mahjongRedFiveOriginPosition = GetAnchoredPosition(mahjongRedFive);
		mahjongTile3BaseScale = GetLocalScale(mahjongTile3);
		mahjongTile4BaseScale = GetLocalScale(mahjongTile4);
		mahjongRedFiveBaseScale = GetLocalScale(mahjongRedFive);
		mahjongTile3BaseRotation = GetLocalRotationZ(mahjongTile3);
		mahjongTile4BaseRotation = GetLocalRotationZ(mahjongTile4);
		mahjongRedFiveOriginRotation = GetLocalRotationZ(mahjongRedFive);
	}

	void WarnIfMissingRequiredReferences()
	{
		if (warnedMissingReferences)
			return;

		bool missing = false;
		switch (mode)
		{
			case IconMode.Dice:
				missing = diceTargets == null || diceTargets.Length == 0;
				break;
			case IconMode.Holdem:
				missing = holdemBackCard == null || holdemAceSpadesCard == null || holdemKingHeartsCard == null;
				break;
			case IconMode.Mahjong:
				missing = mahjongTile3 == null || mahjongTile4 == null || mahjongRedFive == null;
				break;
		}

		if (missing)
			Debug.LogWarning($"[WeaponSelectIconAnimator] Missing references for {mode} icon on {name}");
		warnedMissingReferences = true;
	}

	void ApplyVisuals()
	{
		switch (mode)
		{
			case IconMode.Dice:
				ApplyDiceVisuals();
				break;
			case IconMode.Holdem:
				ApplyHoldemVisuals();
				break;
			case IconMode.Mahjong:
				ApplyMahjongVisuals();
				break;
		}
	}

	void ApplyDiceVisuals()
	{
		if (diceTargets == null || diceTargets.Length == 0)
			return;

		if (diceBasePositions == null || diceBasePositions.Length != diceTargets.Length)
			CacheDiceTargets();

		for (int i = 0; i < diceTargets.Length; i++)
		{
			var target = diceTargets[i];
			if (target == null)
				continue;

			if (isHovered && hoverProgress > 0.001f)
				target.Rotate(GetDiceRotationSpeed(i) * Time.unscaledDeltaTime, Space.Self);

			float phase = i * 1.7f;
			float bobOffset = Mathf.Sin(Time.unscaledTime * DiceBobSpeed + phase) * DiceBobAmplitude * hoverProgress;
			target.localPosition = diceBasePositions[i] + Vector3.up * bobOffset;
		}
	}

	void ApplyHoldemVisuals()
	{
		float t = Mathf.Clamp01(hoverProgress);
		float backT = Smooth01(Mathf.Clamp01(t / 0.55f));
		float faceT = Smooth01(Mathf.Clamp01((t - 0.35f) / 0.65f));
		float faceArc = Mathf.Sin(faceT * Mathf.PI) * 28f;

		Vector2 backPosition = holdemBackBasePosition + new Vector2(0f, 22f * backT);
		Vector3 backScale = new Vector3(
			holdemBackBaseScale.x * Mathf.Lerp(1f, 0.05f, backT),
			holdemBackBaseScale.y,
			holdemBackBaseScale.z);
		float backAlpha = 1f - Smooth01(Mathf.Clamp01((t - 0.42f) / 0.24f));
		SetRectState(holdemBackCard, holdemBackGroup, backPosition, backScale,
			holdemBackBaseRotation, backAlpha, t < 0.995f);

		Vector2 acePosition = Vector2.Lerp(holdemAceBasePosition, holdemLeftCardTarget, EaseOutCubic(faceT))
			+ Vector2.up * faceArc;
		Vector2 kingPosition = Vector2.Lerp(holdemKingBasePosition, holdemRightCardTarget, EaseOutCubic(faceT))
			+ Vector2.up * faceArc;
		Vector3 aceScale = new Vector3(
			holdemAceBaseScale.x * Mathf.Lerp(0.05f, 1f, EaseOutCubic(faceT)),
			holdemAceBaseScale.y,
			holdemAceBaseScale.z);
		Vector3 kingScale = new Vector3(
			holdemKingBaseScale.x * Mathf.Lerp(0.05f, 1f, EaseOutCubic(faceT)),
			holdemKingBaseScale.y,
			holdemKingBaseScale.z);

		SetRectState(holdemAceSpadesCard, holdemAceGroup, acePosition, aceScale,
			Mathf.Lerp(holdemAceBaseRotation, -10f, EaseOutCubic(faceT)), faceT, t > 0.001f);
		SetRectState(holdemKingHeartsCard, holdemKingGroup, kingPosition, kingScale,
			Mathf.Lerp(holdemKingBaseRotation, 10f, EaseOutCubic(faceT)), faceT, t > 0.001f);
	}

	void ApplyMahjongVisuals()
	{
		float t = Mathf.Clamp01(hoverProgress);
		float eased = EaseOutCubic(t);
		float drawEased = EaseOutBack(t);

		SetRectState(mahjongTile3, null,
			Vector2.Lerp(mahjongTile3IdlePosition, mahjongTile3HoverPosition, eased),
			mahjongTile3BaseScale,
			mahjongTile3BaseRotation,
			1f,
			true);
		SetRectState(mahjongTile4, null,
			Vector2.Lerp(mahjongTile4IdlePosition, mahjongTile4HoverPosition, eased),
			mahjongTile4BaseScale,
			mahjongTile4BaseRotation,
			1f,
			true);

		Vector2 redFivePosition = Vector2.LerpUnclamped(
			mahjongRedFiveOriginPosition,
			mahjongRedFiveTargetPosition,
			drawEased);
		Vector3 redFiveScale = mahjongRedFiveBaseScale * Mathf.Lerp(0.86f, 1f, eased);
		float redFiveRotation = Mathf.Lerp(mahjongRedFiveOriginRotation, 0f, eased);
		SetRectState(mahjongRedFive, mahjongRedFiveGroup, redFivePosition, redFiveScale,
			redFiveRotation, Smooth01(t), t > 0.001f);
	}

	static CanvasGroup EnsureCanvasGroup(RectTransform rectTransform)
	{
		if (rectTransform == null)
			return null;

		var group = rectTransform.GetComponent<CanvasGroup>();
		if (group == null)
			group = rectTransform.gameObject.AddComponent<CanvasGroup>();
		group.interactable = false;
		group.blocksRaycasts = false;
		return group;
	}

	static Vector3 GetDiceRotationSpeed(int index)
	{
		return new Vector3(
			16f + index * 5f,
			23f + index * 4f,
			10f - index * 2f) * 2f;
	}

	static void SetRectState(RectTransform rectTransform, CanvasGroup group,
		Vector2 anchoredPosition, Vector3 localScale, float rotationZ, float alpha, bool active)
	{
		if (rectTransform == null)
			return;

		if (rectTransform.gameObject.activeSelf != active)
			rectTransform.gameObject.SetActive(active);

		rectTransform.anchoredPosition = anchoredPosition;
		rectTransform.localScale = localScale;
		rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);

		if (group != null)
			group.alpha = alpha;
	}

	static Vector2 GetAnchoredPosition(RectTransform rectTransform)
	{
		return rectTransform != null ? rectTransform.anchoredPosition : Vector2.zero;
	}

	static Vector3 GetLocalScale(RectTransform rectTransform)
	{
		return rectTransform != null ? rectTransform.localScale : Vector3.one;
	}

	static float GetLocalRotationZ(RectTransform rectTransform)
	{
		return rectTransform != null ? rectTransform.localEulerAngles.z : 0f;
	}

	static float Smooth01(float t)
	{
		t = Mathf.Clamp01(t);
		return t * t * (3f - 2f * t);
	}

	static float EaseOutCubic(float t)
	{
		t = Mathf.Clamp01(t);
		return 1f - Mathf.Pow(1f - t, 3f);
	}

	static float EaseOutBack(float t)
	{
		t = Mathf.Clamp01(t);
		const float c1 = 1.70158f;
		const float c3 = c1 + 1f;
		return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
	}
}
