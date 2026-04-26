using UnityEngine;

namespace Mahjong
{
	/// <summary>
	/// 마작 타일 배경 스프라이트 저장소 (ScriptableObject 에셋).
	/// - placeholderBackground: 수패 스프라이트가 비어있는 슬롯 폴백 (예: w.png)
	/// - manSprites[0..8]: 1만~9만 순서로 Inspector에서 드래그 (m_full.png 슬라이스)
	/// - pinSprites[0..8]: 1통~9통 순서 (p_full.png 슬라이스)
	/// - souSprites[0..8]: 1삭~9삭 순서 (s_full.png 슬라이스)
	/// 에셋을 만드는 법: Assets 우클릭 → Create → Mahjong → Tile Sprite Database
	/// </summary>
	[CreateAssetMenu(fileName = "MahjongTileSprites", menuName = "Mahjong/Tile Sprite Database")]
	public class MahjongTileSpriteDatabase : ScriptableObject
	{
		[SerializeField] Sprite placeholderBackground;
		[SerializeField] Sprite[] manSprites = new Sprite[9];
		[SerializeField] Sprite[] pinSprites = new Sprite[9];
		[SerializeField] Sprite[] souSprites = new Sprite[9];

		public Sprite Placeholder => placeholderBackground;

		public Sprite GetMan(int value) => GetFromArray(manSprites, value);
		public Sprite GetPin(int value) => GetFromArray(pinSprites, value);
		public Sprite GetSou(int value) => GetFromArray(souSprites, value);

		static Sprite GetFromArray(Sprite[] arr, int value)
		{
			int idx = value - 1;
			if (arr == null || idx < 0 || idx >= arr.Length) return null;
			return arr[idx];
		}
	}
}
