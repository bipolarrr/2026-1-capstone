using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Settings / Credits 공용 팝업 베이스.
/// 열고 닫을 때 scale + fade 연출 포함.
/// dimmer가 설정되면 팝업 뒤에 반투명 배경을 표시하고 뒤쪽 입력을 차단한다.
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class SimplePopup : MonoBehaviour
{
	[SerializeField] private float animDuration = 0.2f;
	[SerializeField] private Image dimmer;

	private CanvasGroup _canvasGroup;
	private RectTransform _rectTransform;
	private Coroutine _current;

	void Awake()
	{
		_canvasGroup = GetComponent<CanvasGroup>();
		_rectTransform = GetComponent<RectTransform>();
		ForceClose();
	}

	public void Open()
	{
		if (dimmer != null)
		{
			dimmer.gameObject.SetActive(true);
			dimmer.color = new Color(0f, 0f, 0f, 0f);
		}
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

		float dimmerStartAlpha = 0f;
		float dimmerEndAlpha = 0f;
		if (dimmer != null)
		{
			dimmerStartAlpha = dimmer.color.a;
			dimmerEndAlpha = opening ? 0.5f : 0f;
		}

		// 애니메이션 도중 버튼 입력으로 Open/Close 재진입 방지
		_canvasGroup.interactable = false;
		_canvasGroup.blocksRaycasts = false;

		while (elapsed < animDuration)
		{
			elapsed += Time.deltaTime;
			float t = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
			_canvasGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, t);
			_rectTransform.localScale = Vector3.Lerp(startScale, endScale, t);
			if (dimmer != null)
				dimmer.color = new Color(0f, 0f, 0f, Mathf.Lerp(dimmerStartAlpha, dimmerEndAlpha, t));
			yield return null;
		}

		_canvasGroup.alpha = endAlpha;
		_rectTransform.localScale = endScale;

		if (dimmer != null)
			dimmer.color = new Color(0f, 0f, 0f, dimmerEndAlpha);

		if (!opening)
		{
			gameObject.SetActive(false);
			if (dimmer != null)
				dimmer.gameObject.SetActive(false);
		}
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
		if (dimmer != null)
			dimmer.gameObject.SetActive(false);
	}
}
