using System.Collections.Generic;

namespace Mahjong
{
	/// <summary>표준 136장 세트 생성. 수패 3종×9×4 + 風4×4 + 三元3×4 = 136.</summary>
	public static class TileFactory
	{
		public const int StandardWallSize = 136;

		public static List<Tile> BuildStandardSet(bool useRedFive = true)
		{
			var list = new List<Tile>(StandardWallSize);
			AddSuit(list, Suit.Man, 9, useRedFive);
			AddSuit(list, Suit.Pin, 9, useRedFive);
			AddSuit(list, Suit.Sou, 9, useRedFive);
			AddSuit(list, Suit.Wind, 4, false);
			AddSuit(list, Suit.Dragon, 3, false);
			return list;
		}

		static void AddSuit(List<Tile> list, Suit suit, int maxValue, bool redFiveForNumberSuit)
		{
			for (int v = 1; v <= maxValue; v++)
			{
				for (int copy = 0; copy < 4; copy++)
				{
					bool red = redFiveForNumberSuit && v == 5 && copy == 0;
					list.Add(new Tile(suit, v, red));
				}
			}
		}
	}
}
