using System.Collections.Generic;
using NUnit.Framework;
using Mahjong;

namespace MahjongTests
{
	public class HandDecomposerTests
	{
		static Tile T(Suit s, int v) => new Tile(s, v);

		[Test]
		public void Standard_FourShuntsuPair_IsRecognized()
		{
			// 만수 1-9 슌쯔 3개 + 통수 1-3 + 통수 1-1 머리 모자라; 다시 짜자.
			// 만 123 456 789 + 통 123 + 사 발發 머리
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,3),
				T(Suit.Man,4),T(Suit.Man,5),T(Suit.Man,6),
				T(Suit.Man,7),T(Suit.Man,8),T(Suit.Man,9),
				T(Suit.Pin,1),T(Suit.Pin,2),T(Suit.Pin,3),
				T(Suit.Dragon,2),T(Suit.Dragon,2)
			};
			var winning = T(Suit.Pin,3);
			var results = HandDecomposer.Enumerate(hand, null, winning, true);
			Assert.IsTrue(results.Exists(r => r.Shape == WinShape.Standard), "표준형 분해가 발견되어야 한다");
		}

		[Test]
		public void Chiitoitsu_SevenPairs_IsRecognized()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,1),
				T(Suit.Man,3),T(Suit.Man,3),
				T(Suit.Pin,2),T(Suit.Pin,2),
				T(Suit.Pin,5),T(Suit.Pin,5),
				T(Suit.Sou,4),T(Suit.Sou,4),
				T(Suit.Sou,7),T(Suit.Sou,7),
				T(Suit.Dragon,1),T(Suit.Dragon,1)
			};
			var results = HandDecomposer.Enumerate(hand, null, T(Suit.Dragon,1), true);
			Assert.IsTrue(results.Exists(r => r.Shape == WinShape.Chiitoitsu), "七対子 인식");
		}

		[Test]
		public void Kokushi_ThirteenOrphansPlusOne_IsRecognized()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,9),
				T(Suit.Pin,1),T(Suit.Pin,9),
				T(Suit.Sou,1),T(Suit.Sou,9),
				T(Suit.Wind,1),T(Suit.Wind,2),T(Suit.Wind,3),T(Suit.Wind,4),
				T(Suit.Dragon,1),T(Suit.Dragon,2),T(Suit.Dragon,3),
				T(Suit.Dragon,3) // 重複 = 머리
			};
			var results = HandDecomposer.Enumerate(hand, null, T(Suit.Dragon,3), true);
			Assert.IsTrue(results.Exists(r => r.Shape == WinShape.Kokushi), "国士無双 인식");
		}

		[Test]
		public void NotWinningHand_ReturnsEmpty()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,4),T(Suit.Man,7),
				T(Suit.Pin,3),T(Suit.Pin,6),T(Suit.Pin,9),
				T(Suit.Sou,2),T(Suit.Sou,5),T(Suit.Sou,8),
				T(Suit.Wind,1),T(Suit.Wind,3),
				T(Suit.Dragon,1),T(Suit.Dragon,3)
			};
			var results = HandDecomposer.Enumerate(hand, null, T(Suit.Man,1), true);
			Assert.AreEqual(0, results.Count);
		}
	}
}
