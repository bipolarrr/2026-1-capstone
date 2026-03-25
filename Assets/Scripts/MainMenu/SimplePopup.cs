using System.Collections;
using UnityEngine;

/// <summary>
/// Settings / Credits 공용 팝업 베이스.
/// 열고 닫을 때 scale + fade 연출 포함.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class SimplePopup : MonoBehaviour
{
	[SerializeField] private float animDuration = 0.2f;

	private CanvasGroup _canvasGroup;
	private RectTransform _rectTransform;
	private Coroutine _current;

	void Awake()
	{
		_canvasGroup = GetComponent<CanvasGroup>();
		_rectTransform = GetComponent<RectTransform>();
		// 시작 시 닫힌 상태
		ForceClose();
	}

	public void Open()
	{
		gameObject.SetActive(true);
		if (_current != null)
			StopCoroutine(_current);
		_current = StartCoroutine(Animate(true));
	}

	public void Close()
	{
		if (_current != null)
			StopCoroutine(_current);
		_current = StartCoroutine(Animate(false));
	}

	private IEnumerator Animate(bool opening)
	{
		float elapsed = 0f;
		float startAlpha = _canvasGroup.alpha;
		float endAlpha;
		if (opening)
			endAlpha = 1f;
		else
			endAlpha = 0f;
		Vector3 startScale = _rectTransform.localScale;
		Vector3 endScale;
		if (opening)
			endScale = Vector3.one;
		else
			endScale = new Vector3(0.85f, 0.85f, 1f);

		// 인터랙션 차단
		_canvasGroup.interactable = false;
		_canvasGroup.blocksRaycasts = false;

		while (elapsed < animDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
			_canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
			_rectTransform.localScale = Vector3.Lerp(startScale, endScale, t);
			yield return null;
		}

		_canvasGroup.alpha = endAlpha;
		_rectTransform.localScale = endScale;

		if (!opening)
			gameObject.SetActive(false);
		else
		{
			_canvasGroup.interactable = true;
			_canvasGroup.blocksRaycasts = true;
		}
	}

	private void ForceClose()
	{
		_canvasGroup.alpha = 0f;
		_canvasGroup.interactable = false;
		_canvasGroup.blocksRaycasts = false;
		gameObject.SetActive(false);
	}
}
