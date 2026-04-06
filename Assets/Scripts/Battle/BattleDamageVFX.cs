using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 전투 데미지 텍스트 부유 연출 및 카메라 셰이크.
/// </summary>
public class BattleDamageVFX : MonoBehaviour
{
	[SerializeField] RectTransform damageSpawnParent;
	[SerializeField] GameObject[] enemyPanels;
	[SerializeField] Transform mainCameraTransform;

	// 씬 빌더 시점이 아닌 런타임 Start()에서 Camera.main을 캐싱하여 전달받는다
	public void Init(Transform cameraTransform)
	{
		mainCameraTransform = cameraTransform;
	}

	public void SpawnDamageText(int enemyIdx, int damage)
	{
		if (damageSpawnParent == null)
			return;

		GameObject go = new GameObject("DmgText");
		go.transform.SetParent(damageSpawnParent, false);

		TMP_Text txt = go.AddComponent<TextMeshProUGUI>();
		txt.text = $"-{damage}";
		txt.fontSize = 32;
		txt.color = new Color(1f, 0.3f, 0.3f);
		txt.alignment = TextAlignmentOptions.Center;
		txt.fontStyle = FontStyles.Bold;
		txt.raycastTarget = false;

		RectTransform rt = go.GetComponent<RectTransform>();
		rt.sizeDelta = new Vector2(200, 50);

		float xOffset = Random.Range(-30f, 30f);
		if (enemyIdx < enemyPanels.Length && enemyPanels[enemyIdx].activeSelf)
		{
			RectTransform panelRt = enemyPanels[enemyIdx].GetComponent<RectTransform>();
			Vector3 worldCenter = panelRt.TransformPoint(Vector3.zero);
			Vector3 localInSpawn = damageSpawnParent.InverseTransformPoint(worldCenter);
			rt.anchoredPosition = new Vector2(localInSpawn.x + xOffset, 0);
		}
		else
		{
			rt.anchoredPosition = new Vector2(xOffset, 0);
		}

		StartCoroutine(FloatDamageText(go, txt));
	}

	public void Shake(float intensity)
	{
		StartCoroutine(ShakeRoutine(intensity));
	}

	IEnumerator FloatDamageText(GameObject go, TMP_Text txt)
	{
		float duration = 1.2f;
		float elapsed = 0f;
		Vector2 start = go.GetComponent<RectTransform>().anchoredPosition;

		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / duration;
			RectTransform rt = go.GetComponent<RectTransform>();
			rt.anchoredPosition = start + Vector2.up * (60f * t);
			txt.color = new Color(1f, 0.3f, 0.3f, 1f - t);
			yield return null;
		}

		Destroy(go);
	}

	IEnumerator ShakeRoutine(float intensity)
	{
		if (mainCameraTransform == null)
			yield break;
		Vector3 original = mainCameraTransform.localPosition;
		float duration = 0.15f + intensity * 0.01f;
		float elapsed = 0f;

		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float decay = Mathf.Exp(-elapsed / duration * 4f);
			float x = Random.Range(-1f, 1f) * intensity * 0.01f * decay;
			float y = Random.Range(-1f, 1f) * intensity * 0.01f * decay;
			mainCameraTransform.localPosition = original + new Vector3(x, y, 0f);
			yield return null;
		}

		mainCameraTransform.localPosition = original;
	}
}
