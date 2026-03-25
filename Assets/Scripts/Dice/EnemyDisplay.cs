// Assets/Scripts/Dice/EnemyDisplay.cs
// 더미 적 — 체력바 + 데미지 텍스트 애니메이션 + 히트 흔들림.

using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class EnemyDisplay : MonoBehaviour
{
	[SerializeField] private Image hpFill;
	[SerializeField] private TMP_Text hpText;
	[SerializeField] private RectTransform damageSpawnArea;
	[SerializeField] private RectTransform shakeTarget;

	[Header("설정")]
	[SerializeField] private int maxHp = 999;
	[SerializeField] private TMP_FontAsset damageFont;

	private int currentHp;
	private Coroutine shakeCoroutine;

	private void Start()
	{
		currentHp = maxHp;
		UpdateHpUI();
	}

	public void TakeDamage(int damage, float shakeIntensity = 0f)
	{
		SpawnDamageText(damage);
		currentHp = Mathf.Max(0, currentHp - damage);
		UpdateHpUI();

		if (shakeIntensity > 0f && shakeTarget != null)
		{
			if (shakeCoroutine != null)
				StopCoroutine(shakeCoroutine);
			shakeCoroutine = StartCoroutine(ShakeRoutine(shakeIntensity));
		}
	}

	private void UpdateHpUI()
	{
		if (hpFill != null)
			hpFill.fillAmount = (float)currentHp / maxHp;
		if (hpText != null)
			hpText.text = $"{currentHp} / {maxHp}";
	}

	private void SpawnDamageText(int damage)
	{
		var damageObject = new GameObject("DmgText");
		var rectTransform = damageObject.AddComponent<RectTransform>();
		rectTransform.SetParent(damageSpawnArea, false);
		rectTransform.anchoredPosition = new Vector2(Random.Range(-20f, 20f), 0f);
		rectTransform.sizeDelta = new Vector2(200f, 30f);

		var textMesh = damageObject.AddComponent<TextMeshProUGUI>();
		textMesh.text = $"-{damage}";
		textMesh.fontSize = 36;
		textMesh.color = Color.red;
		textMesh.fontStyle = FontStyles.Bold;
		textMesh.alignment = TextAlignmentOptions.Center;
		textMesh.enableWordWrapping = false;
		if (damageFont != null)
			textMesh.font = damageFont;

		StartCoroutine(AnimateDamageText(rectTransform, textMesh));
	}

	private IEnumerator AnimateDamageText(RectTransform rectTransform, TMP_Text textMesh)
	{
		float duration = 1.5f;
		float t = 0f;
		Vector2 startPosition = rectTransform.anchoredPosition;
		Color startColor = textMesh.color;

		while (t < duration)
		{
			t += Time.deltaTime;
			float progress = t / duration;
			rectTransform.anchoredPosition = startPosition + new Vector2(0f, progress * 80f);
			textMesh.color = new Color(startColor.r, startColor.g, startColor.b, 1f - progress);
			yield return null;
		}

		Destroy(rectTransform.gameObject);
	}

	private IEnumerator ShakeRoutine(float intensity)
	{
		Vector2 origin = shakeTarget.anchoredPosition;
		float duration = 0.15f + intensity * 0.01f;
		float t = 0f;

		while (t < duration)
		{
			t += Time.deltaTime;
			float decay = 1f - (t / duration);
			float offsetX = Random.Range(-intensity, intensity) * decay;
			float offsetY = Random.Range(-intensity, intensity) * decay;
			shakeTarget.anchoredPosition = origin + new Vector2(offsetX, offsetY);
			yield return null;
		}

		shakeTarget.anchoredPosition = origin;
		shakeCoroutine = null;
	}
}
