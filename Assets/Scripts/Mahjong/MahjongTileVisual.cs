using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Mahjong
{
	/// <summary>
	/// 손패 1장의 UI 표현. 배경 색상으로 수트, 라벨로 값. 클릭 시 컨트롤러에 알림.
	/// </summary>
	public class MahjongTileVisual : MonoBehaviour, IPointerClickHandler
	{
		public Tile Data { get; private set; }
		[SerializeField] Image background;
		[SerializeField] TMP_Text label;
		[SerializeField] Image redMarker;
		[SerializeField] GameObject skullOverlay;
		[SerializeField] MahjongTileHoverEffect hoverEffect;
		[SerializeField] MahjongDiscardHoverTooltip discardTooltip;

		public MahjongDiscardHoverTooltip DiscardTooltip => discardTooltip;
		public GameObject SkullOverlay => skullOverlay;

		// 런타임에 컨트롤러가 스폰 후 설정. 스폰 시 DB가 있으면 Sou는 슬라이스 스프라이트, 나머지는 플레이스홀더+라벨.
		MahjongTileSpriteDatabase spriteDb;
		System.Action<MahjongTileVisual> onClick;

		public void SetSpriteDatabase(MahjongTileSpriteDatabase db) { spriteDb = db; }

		public void Bind(Tile tile, System.Action<MahjongTileVisual> clickHandler)
		{
			Data = tile;
			onClick = clickHandler;
			ApplyVisual(tile);
			if (redMarker != null) redMarker.gameObject.SetActive(tile.IsRedFive);
			// 클릭 가능한 타일(손패/쯔모)만 호버 lift 활성. 버림패/도라/미리보기는 비활성.
			if (hoverEffect != null) hoverEffect.enabled = (clickHandler != null);
			// 툴팁은 컨트롤러가 트리거 시점에 활성화.
			if (discardTooltip != null) discardTooltip.enabled = false;
			if (skullOverlay != null) skullOverlay.SetActive(false);
		}

		public void MarkAsShot()
		{
			if (skullOverlay != null) skullOverlay.SetActive(true);
		}

		void ApplyVisual(Tile tile)
		{
			if (background == null) return;

			Sprite suitSprite = null;
			if (spriteDb != null)
			{
				switch (tile.Suit)
				{
					case Suit.Man: suitSprite = spriteDb.GetMan(tile.Value); break;
					case Suit.Pin: suitSprite = spriteDb.GetPin(tile.Value); break;
					case Suit.Sou: suitSprite = spriteDb.GetSou(tile.Value); break;
				}
			}

			if (suitSprite != null)
			{
				// 수패 이미지가 있으면 그대로 표시 — 라벨/색상 오버레이 제거.
				background.sprite = suitSprite;
				background.color = Color.white;
				background.preserveAspect = true;
				if (label != null) label.text = "";
			}
			else
			{
				// 플레이스홀더: w.png 배경 + 한글 라벨. DB가 없으면 색상 폴백.
				Sprite ph = spriteDb != null ? spriteDb.Placeholder : null;
				if (ph != null)
				{
					background.sprite = ph;
					background.color = Color.white;
					background.preserveAspect = true;
				}
				else
				{
					background.sprite = null;
					background.color = SuitColor(tile.Suit);
				}
				if (label != null) label.text = LabelFor(tile);
			}
		}

		public void OnPointerClick(PointerEventData eventData)
		{
			onClick?.Invoke(this);
		}

		public static Color SuitColor(Suit s)
		{
			switch (s)
			{
				case Suit.Man: return new Color(0.85f, 0.30f, 0.25f);
				case Suit.Pin: return new Color(0.25f, 0.45f, 0.85f);
				case Suit.Sou: return new Color(0.30f, 0.70f, 0.35f);
				case Suit.Wind: return new Color(0.55f, 0.40f, 0.75f);
				case Suit.Dragon: return new Color(0.85f, 0.75f, 0.30f);
			}
			return Color.gray;
		}

		public static string LabelFor(Tile t)
		{
			switch (t.Suit)
			{
				case Suit.Man: return $"{t.Value}만";
				case Suit.Pin: return $"{t.Value}통";
				case Suit.Sou: return $"{t.Value}삭";
				case Suit.Wind: return new[] { "동", "남", "서", "북" }[Mathf.Clamp(t.Value - 1, 0, 3)];
				case Suit.Dragon: return new[] { "백", "발", "중" }[Mathf.Clamp(t.Value - 1, 0, 2)];
			}
			return "?";
		}
	}
}
