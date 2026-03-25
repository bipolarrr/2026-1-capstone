using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

/// <summary>
/// 우측 캐릭터: idle 둥실 애니메이션 + 클릭 이스터에그 반응
/// </summary>
public class CharacterEasterEggController : MonoBehaviour,
	IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
{
	[Header("말풍선 오브젝트 (TMP_Text 포함된 패널)")]
	[SerializeField] private GameObject speechBubble;
	[SerializeField] private TMP_Text speechText;
	[SerializeField] private float speechDuration = 2.5f;

	[Header("Idle 설정")]
	[SerializeField] private float idleAmplitude = 8f;   // 픽셀 단위
	[SerializeField] private float idleSpeed = 1.2f;

	[Header("Hover 강조")]
	[SerializeField] private float hoverScale = 1.06f;

	// ─── 랜덤 반응 ───────────────────────────────────────────
	private static readonly string[] Reactions =
	{
		"건드리지 마!",
		"플레이 버튼은 저쪽이야!",
		"비밀을 찾았네?",
		"...왜 때려요?",
		"나는 그냥 서 있는 중이라고요.",
		"한 번 더 누르면 어떻게 될지 몰라~",
	};

	private int _clickCount = 0;
	private Coroutine _speechCoroutine;
	private Coroutine _bounceCoroutine;
	private RectTransform _rectTransform;
	private Vector2 _originPos;
	private Vector3 _baseScale;

	void Awake()
	{
		_rectTransform = GetComponent<RectTransform>();
		if (_rectTransform != null)
			_originPos = _rectTransform.anchoredPosition;
		else
			_originPos = Vector2.zero;
		_baseScale = transform.localScale;

		if (speechBubble != null)
			speechBubble.SetActive(false);
	}

	void Update()
	{
		if (_rectTransform == null)
			return;
		// Idle: 사인파로 위아래 이동
		float offset = Mathf.Sin(Time.time * idleSpeed) * idleAmplitude;
		_rectTransform.anchoredPosition = _originPos + new Vector2(0f, offset);
	}

	// ─── Pointer 이벤트 ──────────────────────────────────────
	public void OnPointerEnter(PointerEventData _)
	{
		StopScaleTween();
		StartCoroutine(ScaleTo(transform.localScale,
			_baseScale * hoverScale, 0.12f));
	}

	public void OnPointerExit(PointerEventData _)
	{
		StopScaleTween();
		StartCoroutine(ScaleTo(transform.localScale, _baseScale, 0.12f));
	}

	public void OnPointerClick(PointerEventData _)
	{
		_clickCount++;
		ShowSpeech(PickReaction());
		if (_bounceCoroutine != null)
			StopCoroutine(_bounceCoroutine);
		_bounceCoroutine = StartCoroutine(BounceScale());
	}

	// ─── 내부 로직 ───────────────────────────────────────────
	private string PickReaction()
	{
		// 연속 클릭 시 순차 순환, 일반 시 랜덤
		if (_clickCount >= 3)
			return Reactions[(_clickCount - 1) % Reactions.Length];
		return Reactions[Random.Range(0, Reactions.Length)];
	}

	private void ShowSpeech(string message)
	{
		if (speechBubble == null)
			return;
		if (speechText != null)
			speechText.text = message;
		speechBubble.SetActive(true);

		if (_speechCoroutine != null)
			StopCoroutine(_speechCoroutine);
		_speechCoroutine = StartCoroutine(HideSpeechAfter(speechDuration));
	}

	private IEnumerator HideSpeechAfter(float delay)
	{
		yield return new WaitForSeconds(delay);
		if (speechBubble != null)
			speechBubble.SetActive(false);
	}

	private IEnumerator BounceScale()
	{
		float dur = 0.12f;
		float punch = 1.25f;
		yield return StartCoroutine(ScaleTo(transform.localScale,
			_baseScale * punch, dur * 0.4f));
		yield return StartCoroutine(ScaleTo(transform.localScale,
			_baseScale, dur * 0.6f));
	}

	private Coroutine _scaleTween;
	private void StopScaleTween()
	{
		if (_scaleTween != null)
			StopCoroutine(_scaleTween);
	}

	private IEnumerator ScaleTo(Vector3 from, Vector3 to, float dur)
	{
		float elapsed = 0f;
		while (elapsed < dur)
		{
			elapsed += Time.deltaTime;
			transform.localScale = Vector3.Lerp(from, to,
				Mathf.SmoothStep(0f, 1f, elapsed / dur));
			yield return null;
		}
		transform.localScale = to;
	}
}
