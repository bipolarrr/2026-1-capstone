using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Mahjong
{
	/// <summary>
	/// 적 발 밑 대기패 UI. 3개 슬롯(A, B, Need) 레이아웃을 anchoredPosition 기반으로 직접 제어.
	/// 모든 슬롯은 root의 중앙(0.5, 0.5)에 앵커되어, 레이아웃이 항상 적을 기준으로 가운데 정렬.
	///
	/// 상태:
	///  - 뒷면(기본): 2장(A, B) 가까이 붙어 있음. Toitsu 그룹인 경우 A 1장만 중앙.
	///  - 공개(리빌): Combo/Shuntsu 형태에 따라 3가지 애니메이션.
	///
	/// 컨트롤러는 단계별로 호출:
	///  1) RevealAnimated(g)  — 공격 시 공개 애니메이션
	///  2) HoldFadeAndRefresh(next) — 보여주기 후 페이드 아웃/인, 새 그룹 뒷면으로 전환
	/// </summary>
	public class EnemyWaitTilesDisplay : MonoBehaviour
	{
		[SerializeField] RectTransform slotA;
		[SerializeField] RectTransform slotB;
		[SerializeField] RectTransform slotNeed;
		[SerializeField] Image imgA;
		[SerializeField] Image imgB;
		[SerializeField] Image imgNeed;
		[SerializeField] TMP_Text markA;
		[SerializeField] TMP_Text markB;
		[SerializeField] TMP_Text markNeed;
		[SerializeField] CanvasGroup group;

		[Header("Layout (px)")]
		[SerializeField] float tileWidth = 32f;
		[SerializeField] float closeGap = 2f;   // 기본 뒷면 상태 패 사이 여백
		[SerializeField] float spreadGap = 2f;  // 공개 시 3장 배치 여백

		[Header("Animation")]
		[SerializeField] float moveDuration = 0.22f;
		[SerializeField] float popDuration = 0.22f;
		[SerializeField] float fadeDuration = 0.25f;

		MahjongTileSpriteDatabase db;
		Sprite backSprite;
		Coroutine running;

		float CloseOffset => (tileWidth + closeGap) * 0.5f; // 2장 가까이 놓을 때 중심에서 offset
		float TileSpan => tileWidth + spreadGap;            // 3장 인접 배치 시 인접 중심 간 거리

		public void Init(MahjongTileSpriteDatabase database)
		{
			db = database;
			backSprite = db != null ? db.Placeholder : null;
			ApplyBackedLayout(null);
			if (group != null) group.alpha = 1f;
		}

		// ── 공개 API ─────────────────────────────────────────────

		/// <summary>공격 시점에 실제 대기 조합을 애니메이션으로 공개. Combo/Shuntsu 형태에 따라 3가지 연출.</summary>
		public Coroutine RevealAnimated(WaitGroup g)
		{
			if (running != null) StopCoroutine(running);
			running = StartCoroutine(RevealRoutine(g));
			return running;
		}

		/// <summary>공개 상태를 유지하다가 페이드아웃 → nextGroup 뒷면 → 페이드인. 컨트롤러가 리롤 직후 호출.</summary>
		public Coroutine HoldFadeAndRefresh(WaitGroup? nextGroup, float holdSeconds = 0.5f)
		{
			if (running != null) StopCoroutine(running);
			running = StartCoroutine(HoldFadeRoutine(nextGroup, holdSeconds));
			return running;
		}

		/// <summary>직감: 대기 그룹 중 한 장만 공개. 애니메이션 없이 상태만 전환.</summary>
		public void RevealIntuition(WaitGroup g, int slotSide)
		{
			if (g.Type == EnemyComboType.Toitsu)
			{
				// Toitsu는 원래 1장만 노출 — 그 1장을 공개.
				slotA.gameObject.SetActive(true);
				slotA.anchoredPosition = Vector2.zero;
				slotA.localScale = Vector3.one;
				SetTile(imgA, markA, g.Slot1, backed: false);
				slotB.gameObject.SetActive(false);
				slotNeed.gameObject.SetActive(false);
				return;
			}
			// 비-Toitsu: 2장 가까이 배치. 선택된 쪽 공개, 반대쪽 뒷면.
			slotA.gameObject.SetActive(true);
			slotB.gameObject.SetActive(true);
			slotNeed.gameObject.SetActive(false);
			slotA.anchoredPosition = new Vector2(-CloseOffset, 0f);
			slotB.anchoredPosition = new Vector2(+CloseOffset, 0f);
			slotA.localScale = slotB.localScale = Vector3.one;
			if (slotSide == 0)
			{
				SetTile(imgA, markA, g.Slot1, backed: false);
				SetTile(imgB, markB, default, backed: true);
			}
			else
			{
				SetTile(imgA, markA, default, backed: true);
				SetTile(imgB, markB, g.Slot2, backed: false);
			}
		}

		// ── 내부: 레이아웃/애니 ───────────────────────────────────

		void ApplyBackedLayout(WaitGroup? nextGroup)
		{
			slotNeed.gameObject.SetActive(false);
			slotA.localScale = Vector3.one;
			slotB.localScale = Vector3.one;
			slotNeed.localScale = Vector3.one;

			bool isToitsu = nextGroup.HasValue && nextGroup.Value.Type == EnemyComboType.Toitsu;
			if (isToitsu)
			{
				slotA.gameObject.SetActive(true);
				slotA.anchoredPosition = Vector2.zero;
				SetTile(imgA, markA, default, backed: true);
				slotB.gameObject.SetActive(false);
			}
			else
			{
				slotA.gameObject.SetActive(true);
				slotB.gameObject.SetActive(true);
				slotA.anchoredPosition = new Vector2(-CloseOffset, 0f);
				slotB.anchoredPosition = new Vector2(+CloseOffset, 0f);
				SetTile(imgA, markA, default, backed: true);
				SetTile(imgB, markB, default, backed: true);
			}
		}

		enum RevealShape { Toitsu, MiddleNeed, NeedLeft, NeedRight }

		static RevealShape Detect(WaitGroup g)
		{
			if (g.Type == EnemyComboType.Toitsu) return RevealShape.Toitsu;
			if (g.Type == EnemyComboType.Koutsu) return RevealShape.MiddleNeed;
			// Shuntsu — Slot1.Value < Slot2.Value 항상 성립(생성 로직).
			int s1 = g.Slot1.Value, s2 = g.Slot2.Value, n = g.NeedTile.Value;
			if (n > s1 && n < s2) return RevealShape.MiddleNeed; // kanchan
			if (n < s1) return RevealShape.NeedLeft;             // 양면/변짱 need=낮은쪽
			return RevealShape.NeedRight;                         // 양면/변짱 need=높은쪽
		}

		IEnumerator RevealRoutine(WaitGroup g)
		{
			var shape = Detect(g);
			switch (shape)
			{
				case RevealShape.Toitsu:     yield return RevealToitsu(g); break;
				case RevealShape.MiddleNeed: yield return RevealMiddle(g); break;
				case RevealShape.NeedLeft:   yield return RevealSide(g, needOnLeft: true); break;
				case RevealShape.NeedRight:  yield return RevealSide(g, needOnLeft: false); break;
			}
			running = null;
		}

		IEnumerator RevealMiddle(WaitGroup g)
		{
			// 현재 상태: A, B가 뒷면으로 가까이. 공개 후 양옆으로 벌리고 중앙에 Need가 쏙 들어감.
			slotA.gameObject.SetActive(true);
			slotB.gameObject.SetActive(true);
			SetTile(imgA, markA, g.Slot1, backed: false);
			SetTile(imgB, markB, g.Slot2, backed: false);

			var aTarget = new Vector2(-TileSpan, 0f);
			var bTarget = new Vector2(+TileSpan, 0f);
			var aCo = StartCoroutine(MoveTo(slotA, aTarget, moveDuration));
			var bCo = StartCoroutine(MoveTo(slotB, bTarget, moveDuration));
			yield return aCo; yield return bCo;

			SetTile(imgNeed, markNeed, g.NeedTile, backed: false);
			slotNeed.anchoredPosition = Vector2.zero;
			yield return PopIn(slotNeed, popDuration);
		}

		IEnumerator RevealSide(WaitGroup g, bool needOnLeft)
		{
			// 현재 상태: A, B 가까이. Need 쪽 반대로 둘 다 밀고, Need가 비워둔 자리에 쏙 들어감.
			slotA.gameObject.SetActive(true);
			slotB.gameObject.SetActive(true);
			SetTile(imgA, markA, g.Slot1, backed: false);
			SetTile(imgB, markB, g.Slot2, backed: false);

			Vector2 aTarget, bTarget, needPos;
			if (needOnLeft)
			{
				// [Need, A, B] = [-TileSpan, 0, +TileSpan]
				aTarget = Vector2.zero;
				bTarget = new Vector2(+TileSpan, 0f);
				needPos = new Vector2(-TileSpan, 0f);
			}
			else
			{
				// [A, B, Need]
				aTarget = new Vector2(-TileSpan, 0f);
				bTarget = Vector2.zero;
				needPos = new Vector2(+TileSpan, 0f);
			}
			var aCo = StartCoroutine(MoveTo(slotA, aTarget, moveDuration));
			var bCo = StartCoroutine(MoveTo(slotB, bTarget, moveDuration));
			yield return aCo; yield return bCo;

			SetTile(imgNeed, markNeed, g.NeedTile, backed: false);
			slotNeed.anchoredPosition = needPos;
			yield return PopIn(slotNeed, popDuration);
		}

		IEnumerator RevealToitsu(WaitGroup g)
		{
			// 현재 상태: Toitsu라면 A만 중앙(0). 비-Toitsu 뒷면에서 시작했다면 A(-offset), B(+offset).
			// 어느 경우든 A를 -CloseOffset으로 이동, B 숨김, Need를 +CloseOffset에 팝인.
			slotA.gameObject.SetActive(true);
			SetTile(imgA, markA, g.Slot1, backed: false);
			slotB.gameObject.SetActive(false);

			yield return MoveTo(slotA, new Vector2(-CloseOffset, 0f), moveDuration);

			SetTile(imgNeed, markNeed, g.NeedTile, backed: false);
			slotNeed.anchoredPosition = new Vector2(+CloseOffset, 0f);
			yield return PopIn(slotNeed, popDuration);
		}

		IEnumerator HoldFadeRoutine(WaitGroup? nextGroup, float holdSeconds)
		{
			yield return new WaitForSeconds(holdSeconds);
			yield return Fade(1f, 0f, fadeDuration);
			ApplyBackedLayout(nextGroup);
			yield return Fade(0f, 1f, fadeDuration);
			running = null;
		}

		// ── 트윈 ──────────────────────────────────────────────

		IEnumerator MoveTo(RectTransform rt, Vector2 target, float duration)
		{
			if (rt == null) yield break;
			Vector2 start = rt.anchoredPosition;
			float t = 0f;
			while (t < duration)
			{
				t += Time.deltaTime;
				float k = Mathf.Clamp01(t / duration);
				float e = k * k * (3f - 2f * k); // smoothstep
				rt.anchoredPosition = Vector2.LerpUnclamped(start, target, e);
				yield return null;
			}
			rt.anchoredPosition = target;
		}

		IEnumerator PopIn(RectTransform rt, float duration)
		{
			if (rt == null) yield break;
			rt.gameObject.SetActive(true);
			rt.localScale = Vector3.zero;
			float t = 0f;
			while (t < duration)
			{
				t += Time.deltaTime;
				float k = Mathf.Clamp01(t / duration);
				// back-out ease: 오버슈트 살짝
				float e = 1f - Mathf.Pow(1f - k, 3f);
				float overshoot = 1f + 0.12f * Mathf.Sin(k * Mathf.PI);
				rt.localScale = Vector3.one * (e * overshoot);
				yield return null;
			}
			rt.localScale = Vector3.one;
		}

		IEnumerator Fade(float from, float to, float duration)
		{
			if (group == null) yield break;
			float t = 0f;
			while (t < duration)
			{
				t += Time.deltaTime;
				group.alpha = Mathf.Lerp(from, to, Mathf.Clamp01(t / duration));
				yield return null;
			}
			group.alpha = to;
		}

		// ── 타일 시각 설정 ────────────────────────────────────

		void SetTile(Image img, TMP_Text mark, Tile t, bool backed)
		{
			if (img == null) return;
			if (backed)
			{
				img.sprite = backSprite;
				img.color = Color.white;
				img.preserveAspect = true;
				if (mark != null) { mark.text = "?"; mark.gameObject.SetActive(true); }
				return;
			}
			if (mark != null) mark.gameObject.SetActive(false);
			Sprite s = GetSpriteFor(t);
			if (s != null)
			{
				img.sprite = s;
				img.color = Color.white;
			}
			else
			{
				img.sprite = backSprite;
				img.color = Color.white;
				if (mark != null)
				{
					mark.text = MahjongTileVisual.LabelFor(t);
					mark.gameObject.SetActive(true);
				}
			}
			img.preserveAspect = true;
		}

		Sprite GetSpriteFor(Tile t)
		{
			if (db == null) return null;
			switch (t.Suit)
			{
				case Suit.Man: return db.GetMan(t.Value);
				case Suit.Pin: return db.GetPin(t.Value);
				case Suit.Sou: return db.GetSou(t.Value);
			}
			return null;
		}
	}
}
