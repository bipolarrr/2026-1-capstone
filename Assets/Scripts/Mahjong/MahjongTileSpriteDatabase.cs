using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Mahjong
{
	/// <summary>
	/// 마작 타일 배경 스프라이트 저장소 (ScriptableObject 에셋).
	/// - placeholderBackground: 수패 스프라이트가 비어있는 슬롯 폴백 (예: w.png)
	/// - manSprites[0..8]: 1만~9만 순서로 Inspector에서 드래그 (m_full.png 슬라이스)
	/// - pinSprites[0..8]: 1통~9통 순서 (p_full.png 슬라이스)
	/// - souSprites[0..8]: 1삭~9삭 순서 (s_full.png 슬라이스)
	/// - windTextures[0..3]: 동/남/서/북 단일 PNG
	/// - dragonTextures[0..2]: 백/발/중 단일 PNG
	/// - backTexture: 적 숨김 대기패용 뒷면 단일 PNG
	/// - red*FiveTexture: 적5만/적5통/적5삭 전용 단일 PNG
	/// 에셋을 만드는 법: Assets 우클릭 → Create → Mahjong → Tile Sprite Database
	/// </summary>
	[CreateAssetMenu(fileName = "MahjongTileSprites", menuName = "Mahjong/Tile Sprite Database")]
	public class MahjongTileSpriteDatabase : ScriptableObject
	{
		[SerializeField] Sprite placeholderBackground;
		[SerializeField] Sprite[] manSprites = new Sprite[9];
		[SerializeField] Sprite[] pinSprites = new Sprite[9];
		[SerializeField] Sprite[] souSprites = new Sprite[9];
		[SerializeField] Texture2D[] windTextures = new Texture2D[4];
		[SerializeField] Texture2D[] dragonTextures = new Texture2D[3];
		[SerializeField] Texture2D backTexture;
		[SerializeField] Texture2D redManFiveTexture;
		[SerializeField] Texture2D redPinFiveTexture;
		[SerializeField] Texture2D redSouFiveTexture;

		Sprite[] windSprites = new Sprite[4];
		Sprite[] dragonSprites = new Sprite[3];
		Sprite backSprite;
		Sprite redManFiveSprite;
		Sprite redPinFiveSprite;
		Sprite redSouFiveSprite;

		public Sprite Placeholder => placeholderBackground;

		public Sprite GetMan(int value) => GetFromArray(manSprites, value);
		public Sprite GetPin(int value) => GetFromArray(pinSprites, value);
		public Sprite GetSou(int value) => GetFromArray(souSprites, value);
		public Sprite GetWind(int value) => GetWindSprite(value);
		public Sprite GetDragon(int value) => GetDragonSprite(value);
		public Sprite GetBackSprite() => GetOrCreateSprite(GetBackTexture(), ref backSprite, "tile_back_acorn", FilterMode.Point);

		public Sprite GetSprite(Tile tile)
		{
			if (tile.IsRedFive && tile.Value == 5)
			{
				var redFiveSprite = GetRedFiveSprite(tile.Suit);
				if (redFiveSprite != null)
					return redFiveSprite;
			}

			switch (tile.Suit)
			{
				case Suit.Man: return GetMan(tile.Value);
				case Suit.Pin: return GetPin(tile.Value);
				case Suit.Sou: return GetSou(tile.Value);
				case Suit.Wind: return GetWind(tile.Value);
				case Suit.Dragon: return GetDragon(tile.Value);
				default: return null;
			}
		}

		public bool HasDedicatedRedFiveSprite(Tile tile)
		{
			return tile.IsRedFive && tile.Value == 5 && GetRedFiveSprite(tile.Suit) != null;
		}

		Sprite GetWindSprite(int value)
		{
			int index = value - 1;
			if (index < 0 || index >= 4)
				return null;
			EnsureHonorSpriteCaches();
			return GetOrCreateSprite(GetWindTexture(value), ref windSprites[index], WindSpriteName(value), FilterMode.Point);
		}

		Sprite GetDragonSprite(int value)
		{
			int index = value - 1;
			if (index < 0 || index >= 3)
				return null;
			EnsureHonorSpriteCaches();
			return GetOrCreateSprite(GetDragonTexture(value), ref dragonSprites[index], DragonSpriteName(value), FilterMode.Point);
		}

		Sprite GetRedFiveSprite(Suit suit)
		{
			switch (suit)
			{
				case Suit.Man:
					return GetOrCreateSprite(GetRedFiveTexture(suit), ref redManFiveSprite, "m_5_red", FilterMode.Bilinear);
				case Suit.Pin:
					return GetOrCreateSprite(GetRedFiveTexture(suit), ref redPinFiveSprite, "p_5_red", FilterMode.Bilinear);
				case Suit.Sou:
					return GetOrCreateSprite(GetRedFiveTexture(suit), ref redSouFiveSprite, "s_5_red", FilterMode.Point);
				default: return null;
			}
		}

		static Sprite GetFromArray(Sprite[] arr, int value)
		{
			int idx = value - 1;
			if (arr == null || idx < 0 || idx >= arr.Length) return null;
			return arr[idx];
		}

		Texture2D GetRedFiveTexture(Suit suit)
		{
			switch (suit)
			{
				case Suit.Man: return redManFiveTexture != null ? redManFiveTexture : GetEditorRedFiveTexture(suit);
				case Suit.Pin: return redPinFiveTexture != null ? redPinFiveTexture : GetEditorRedFiveTexture(suit);
				case Suit.Sou: return redSouFiveTexture != null ? redSouFiveTexture : GetEditorRedFiveTexture(suit);
				default: return null;
			}
		}

		Texture2D GetWindTexture(int value)
		{
			Texture2D texture = GetTextureFromArray(windTextures, value);
			return texture != null ? texture : GetEditorWindTexture(value);
		}

		Texture2D GetDragonTexture(int value)
		{
			Texture2D texture = GetTextureFromArray(dragonTextures, value);
			return texture != null ? texture : GetEditorDragonTexture(value);
		}

		Texture2D GetBackTexture()
		{
			return backTexture != null ? backTexture : GetEditorBackTexture();
		}

		static Texture2D GetTextureFromArray(Texture2D[] textures, int value)
		{
			int index = value - 1;
			if (textures == null || index < 0 || index >= textures.Length)
				return null;
			return textures[index];
		}

		void EnsureHonorSpriteCaches()
		{
			if (windSprites == null || windSprites.Length != 4)
				windSprites = new Sprite[4];
			if (dragonSprites == null || dragonSprites.Length != 3)
				dragonSprites = new Sprite[3];
		}

		static Sprite GetOrCreateSprite(Texture2D texture, ref Sprite cachedSprite, string spriteName, FilterMode filterMode)
		{
			if (texture == null)
				return null;
			if (cachedSprite != null && cachedSprite.texture == texture)
				return cachedSprite;

			texture.filterMode = filterMode;
			cachedSprite = Sprite.Create(
				texture,
				new Rect(0f, 0f, texture.width, texture.height),
				new Vector2(0.5f, 0.5f),
				100f,
				1,
				SpriteMeshType.FullRect);
			cachedSprite.name = spriteName;
			cachedSprite.hideFlags = HideFlags.HideAndDontSave;
			return cachedSprite;
		}

#if UNITY_EDITOR
		static Texture2D GetEditorWindTexture(int value)
		{
			switch (value)
			{
				case 1: return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/z_east.png");
				case 2: return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/z_south.png");
				case 3: return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/z_west.png");
				case 4: return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/z_north.png");
				default: return null;
			}
		}

		static Texture2D GetEditorDragonTexture(int value)
		{
			switch (value)
			{
				case 1: return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/z_white.png");
				case 2: return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/z_green.png");
				case 3: return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/z_red.png");
				default: return null;
			}
		}

		static Texture2D GetEditorBackTexture()
		{
			return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/tile_back_acorn.png");
		}

		Texture2D GetEditorRedFiveTexture(Suit suit)
		{
			switch (suit)
			{
				case Suit.Man:
					return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/m_5_red.png");
				case Suit.Pin:
					return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/p_5_red.png");
				case Suit.Sou:
					return AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Mahjong/s_5_red.png");
				default:
					return null;
			}
		}
#else
		static Texture2D GetEditorWindTexture(int value) => null;
		static Texture2D GetEditorDragonTexture(int value) => null;
		static Texture2D GetEditorBackTexture() => null;
		static Texture2D GetEditorRedFiveTexture(Suit suit) => null;
#endif

		static string WindSpriteName(int value)
		{
			switch (value)
			{
				case 1: return "z_east";
				case 2: return "z_south";
				case 3: return "z_west";
				case 4: return "z_north";
				default: return "z_unknown";
			}
		}

		static string DragonSpriteName(int value)
		{
			switch (value)
			{
				case 1: return "z_white";
				case 2: return "z_green";
				case 3: return "z_red";
				default: return "z_unknown";
			}
		}
	}
}
