using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Mahjong
{
	/// <summary>
	/// 쏘인 버림패 호버 시 표시되는 툴팁 패널.
	/// 적 이름, 대기 조합(슬롯1·슬롯2 + 필요 패), 입은 피해(반하트 수)를 보여준다.
	/// 대기 조합 패는 작은 스프라이트로 렌더 (소형 셀 사이즈).
	/// </summary>
	public class MahjongWaitInfoPanel : MonoBehaviour
	{
		[SerializeField] RectTransform root;
		[SerializeField] TMP_Text headerText;
		[SerializeField] TMP_Text damageText;
		[SerializeField] Transform tilesRoot;            // 작은 타일 3개 + 화살표가 들어가는 컨테이너
		[SerializeField] TMP_Text arrowText;             // "→"
		[SerializeField] GameObject tilePrefab;
		[SerializeField] MahjongTileSpriteDatabase tileSprites;
		[SerializeField] Vector2 offset = new Vector2(0f, 80f); // 타일 위쪽으로 띄움
		[SerializeField] float tileScale = 0.6f;

		Canvas parentCanvas;

		void Awake()
		{
			parentCanvas = GetComponentInParent<Canvas>();
			if (root == null) root = transform as RectTransform;
		}

		public void Show(RectTransform anchor, string enemyName, WaitGroup wait, int damageHalfHearts)
		{
			if (root == null) return;
			gameObject.SetActive(true);

			if (headerText != null) headerText.text = enemyName;
			if (damageText != null)
			{
				int full = damageHalfHearts / 2;
				int half = damageHalfHearts % 2;
				string h = half == 0 ? $"{full} 하트" : $"{full}.5 하트";
				damageText.text = $"피해: {h}";
			}

			BuildTiles(wait);
			PositionAtAnchor(anchor);
		}

		public void Hide()
		{
			gameObject.SetActive(false);
		}

		void BuildTiles(WaitGroup wait)
		{
			if (tilesRoot == null || tilePrefab == null) return;

			for (int i = tilesRoot.childCount - 1; i >= 0; i--)
			{
				var c = tilesRoot.GetChild(i);
				if (c == arrowText?.transform) continue;
				Object.Destroy(c.gameObject);
			}

			SpawnSmall(wait.Slot1);
			bool isToitsu = wait.Type == EnemyComboType.Toitsu;
			if (!isToitsu) SpawnSmall(wait.Slot2);
			if (arrowText != null)
			{
				arrowText.transform.SetParent(tilesRoot, false);
				arrowText.transform.SetSiblingIndex(isToitsu ? 1 : 2);
				arrowText.gameObject.SetActive(true);
			}
			SpawnSmall(wait.NeedTile);
		}

		void SpawnSmall(Tile t)
		{
			var go = Instantiate(tilePrefab, tilesRoot);
			var v = go.GetComponent<MahjongTileVisual>();
			if (v != null)
			{
				v.SetSpriteDatabase(tileSprites);
				v.Bind(t, null);
				// 호버/툴팁 컴포넌트는 작은 미리보기에서 비활성.
				var hover = go.GetComponent<MahjongTileHoverEffect>();
				if (hover != null) hover.enabled = false;
				var tip = go.GetComponent<MahjongDiscardHoverTooltip>();
				if (tip != null) tip.enabled = false;
			}
			go.transform.localScale = Vector3.one * tileScale;
		}

		void PositionAtAnchor(RectTransform anchor)
		{
			if (anchor == null || parentCanvas == null) return;

			// anchor 월드 위치를 캔버스 로컬로 변환.
			var canvasRT = parentCanvas.transform as RectTransform;
			Vector3 worldPos = anchor.TransformPoint(new Vector3(anchor.rect.center.x, anchor.rect.yMax, 0f));
			Vector2 local;
			Camera cam = parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : parentCanvas.worldCamera;
			Vector2 screen = RectTransformUtility.WorldToScreenPoint(cam, worldPos);
			RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRT, screen, cam, out local);

			var rt = root;
			rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
			rt.pivot = new Vector2(0.5f, 0f);
			rt.anchoredPosition = local + offset;

			// 화면 밖 클램프.
			Vector2 size = rt.rect.size;
			Vector2 canvasSize = canvasRT.rect.size;
			float halfW = size.x * 0.5f;
			float minX = -canvasSize.x * 0.5f + halfW;
			float maxX = canvasSize.x * 0.5f - halfW;
			float maxY = canvasSize.y * 0.5f - size.y;
			var p = rt.anchoredPosition;
			p.x = Mathf.Clamp(p.x, minX, maxX);
			p.y = Mathf.Min(p.y, maxY);
			rt.anchoredPosition = p;
		}
	}
}
