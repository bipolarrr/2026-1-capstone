using System.Collections.Generic;

namespace Mahjong
{
	/// <summary>34종 패 인덱스. 0-8 m, 9-17 p, 18-26 s, 27-30 풍(東南西北), 31-33 三元(白發中).</summary>
	public static class TileIndex
	{
		public const int Count = 34;

		public static int Of(Tile t)
		{
			switch (t.Suit)
			{
				case Suit.Man: return t.Value - 1;
				case Suit.Pin: return 9 + t.Value - 1;
				case Suit.Sou: return 18 + t.Value - 1;
				case Suit.Wind: return 27 + t.Value - 1;
				case Suit.Dragon: return 31 + t.Value - 1;
			}
			return -1;
		}

		public static Tile FromIndex(int i)
		{
			if (i < 9) return new Tile(Suit.Man, i + 1);
			if (i < 18) return new Tile(Suit.Pin, i - 9 + 1);
			if (i < 27) return new Tile(Suit.Sou, i - 18 + 1);
			if (i < 31) return new Tile(Suit.Wind, i - 27 + 1);
			return new Tile(Suit.Dragon, i - 31 + 1);
		}

		public static bool IsHonor(int i) => i >= 27;
		public static bool IsDragon(int i) => i >= 31;
		public static bool IsWind(int i) => i >= 27 && i <= 30;
		public static bool IsTerminalOrHonor(int i) => IsHonor(i) || IsTerminal(i);
		public static bool IsTerminal(int i)
		{
			if (i >= 27) return false;
			int v = (i % 9) + 1;
			return v == 1 || v == 9;
		}
		public static bool IsNumber(int i) => i < 27;
		public static int Suit3(int i) => i < 27 ? i / 9 : -1; // 0=m 1=p 2=s, -1=honor
		public static int NumberValue(int i) => i < 27 ? (i % 9) + 1 : 0;

		public static int[] Counts(IList<Tile> tiles)
		{
			var c = new int[Count];
			foreach (var t in tiles) c[Of(t)]++;
			return c;
		}
	}
}
