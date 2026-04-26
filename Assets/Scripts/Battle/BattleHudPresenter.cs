using System.Collections;
using UnityEngine;
using TMPro;

/// <summary>
/// 전투 씬의 작은 텍스트 HUD를 담당. 굴림 점(●○), 데미지 프리뷰, 콤보 라벨(RainbowCycle),
/// 부활 플래시 등 순수 UI 표현 영역만 캡슐화한다.
///
/// 빌더가 scene root에 AddComponent하고 SceneBuilderUtility.SetField로 세 개의 TMP_Text를
/// private SerializeField에 주입한다. Inspector 수동 와이어링 불필요.
///
/// BattleSceneController는 판단(족보가 있느냐, 방어 페이즈냐)을 직접 수행한 뒤
/// 이 Presenter의 명시적 API(ShowAttackPreview/ShowDefensePreview 등)를 호출한다.
/// </summary>
public class BattleHudPresenter : MonoBehaviour
{
	[SerializeField] TMP_Text rollDotsText;
	[SerializeField] TMP_Text damagePreviewText;
	[SerializeField] TMP_Text comboLabel;

	Coroutine comboLabelCo;
	Coroutine reviveFlashCo;

	// ── 굴림 점 ──
	public void RefreshRollDots(int maxRolls, int remaining)
	{
		if (rollDotsText == null) return;
		var sb = new System.Text.StringBuilder();
		for (int i = 0; i < maxRolls; i++)
		{
			if (i > 0) sb.Append(' ');
			sb.Append(i < remaining ? '●' : '○');
		}
		rollDotsText.text = sb.ToString();
	}

	// ── 데미지 프리뷰(공격/방어/결과) ──
	public void ShowAttackPreview(int damage, string comboName)
	{
		if (damagePreviewText == null) return;
		damagePreviewText.text = !string.IsNullOrEmpty(comboName)
			? $"예상: {comboName} → {damage}"
			: $"예상: {damage}";
	}

	/// <summary>방어 페이즈용 자유 문자열 프리뷰. 색/문구는 호출자가 결정.</summary>
	public void ShowDefensePreview(string richText)
	{
		if (damagePreviewText == null) return;
		damagePreviewText.text = richText ?? "";
	}

	public void SetDamageResultText(string richText)
	{
		if (damagePreviewText == null) return;
		damagePreviewText.text = richText ?? "";
	}

	public void ClearDamageText()
	{
		if (damagePreviewText == null) return;
		damagePreviewText.text = "";
		damagePreviewText.color = Color.white;
		damagePreviewText.transform.localScale = Vector3.one;
	}

	// ── 콤보 라벨(무지개 사이클) ──
	public void ShowComboLabel(string text)
	{
		if (comboLabel == null) return;
		if (comboLabelCo != null) StopCoroutine(comboLabelCo);
		comboLabel.text = text;
		comboLabel.gameObject.SetActive(true);
		comboLabelCo = StartCoroutine(RainbowCycle(comboLabel));
	}

	public void StopComboLabel()
	{
		if (comboLabel == null) return;
		if (comboLabelCo != null) { StopCoroutine(comboLabelCo); comboLabelCo = null; }
		comboLabel.gameObject.SetActive(false);
	}

	IEnumerator RainbowCycle(TMP_Text txt)
	{
		float t = 0f;
		while (txt != null && txt.isActiveAndEnabled)
		{
			t += Time.deltaTime * 0.9f;
			float h = Mathf.Repeat(t, 1f);
			txt.color = Color.HSVToRGB(h, 0.85f, 1f);
			yield return null;
		}
	}

	// ── 부활 플래시 ──
	public void FlashRevive()
	{
		if (damagePreviewText == null) return;
		damagePreviewText.text = "부활 패시브 발동!";
		damagePreviewText.color = new Color(0.3f, 1f, 0.5f);
		if (reviveFlashCo != null) StopCoroutine(reviveFlashCo);
		reviveFlashCo = StartCoroutine(FlashReviveRoutine());
	}

	IEnumerator FlashReviveRoutine()
	{
		float elapsed = 0f;
		float duration = 1.5f;
		Vector3 baseScale = damagePreviewText.transform.localScale;
		damagePreviewText.transform.localScale = baseScale * 1.5f;
		while (elapsed < duration)
		{
			elapsed += Time.deltaTime;
			float t = elapsed / duration;
			damagePreviewText.transform.localScale = Vector3.Lerp(baseScale * 1.5f, baseScale, t);
			damagePreviewText.color = Color.Lerp(new Color(0.3f, 1f, 0.5f), Color.white, t);
			yield return null;
		}
		damagePreviewText.transform.localScale = baseScale;
		damagePreviewText.color = Color.white;
		reviveFlashCo = null;
	}

	void OnDestroy()
	{
		// 코루틴은 MonoBehaviour가 파괴되면 자동 중단되지만, 명시적으로 상태 해제.
		comboLabelCo = null;
		reviveFlashCo = null;
	}
}
