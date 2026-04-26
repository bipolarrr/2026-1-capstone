using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using Mahjong;

namespace MahjongTests
{
	public class YakuEvaluatorTests
	{
		static Tile T(Suit s, int v) => new Tile(s, v);

		static BestHandResult Pick(List<Tile> hand, Tile winning, bool tsumo, bool riichi, IReadOnlyList<Tile> dora = null)
		{
			return BestHandPicker.Pick(hand, null, winning, tsumo, riichi, dora);
		}

		[Test]
		public void Pinfu_AllShuntsu_NonYakuhaiPair()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,2),T(Suit.Man,3),T(Suit.Man,4),
				T(Suit.Pin,3),T(Suit.Pin,4),T(Suit.Pin,5),
				T(Suit.Sou,2),T(Suit.Sou,3),T(Suit.Sou,4),
				T(Suit.Sou,6),T(Suit.Sou,7),T(Suit.Sou,8),
				T(Suit.Pin,7),T(Suit.Pin,7)
			};
			var r = Pick(hand, T(Suit.Sou,8), true, false);
			Assert.IsNotNull(r);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.Pinfu), "핀후 인식");
		}

		[Test]
		public void Tanyao_NoTerminalsOrHonors()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,2),T(Suit.Man,3),T(Suit.Man,4),
				T(Suit.Pin,3),T(Suit.Pin,4),T(Suit.Pin,5),
				T(Suit.Sou,2),T(Suit.Sou,3),T(Suit.Sou,4),
				T(Suit.Sou,5),T(Suit.Sou,5),T(Suit.Sou,5),
				T(Suit.Pin,7),T(Suit.Pin,7)
			};
			var r = Pick(hand, T(Suit.Sou,5), true, false);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.Tanyao));
		}

		[Test]
		public void YakuhaiChun_DragonRedTriplet()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,2),T(Suit.Man,3),T(Suit.Man,4),
				T(Suit.Pin,3),T(Suit.Pin,4),T(Suit.Pin,5),
				T(Suit.Sou,7),T(Suit.Sou,8),T(Suit.Sou,9),
				T(Suit.Dragon,3),T(Suit.Dragon,3),T(Suit.Dragon,3),
				T(Suit.Pin,7),T(Suit.Pin,7)
			};
			var r = Pick(hand, T(Suit.Dragon,3), true, false);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.YakuhaiChun));
		}

		[Test]
		public void Toitoi_AllTriplets()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,2),T(Suit.Man,2),T(Suit.Man,2),
				T(Suit.Pin,5),T(Suit.Pin,5),T(Suit.Pin,5),
				T(Suit.Sou,7),T(Suit.Sou,7),T(Suit.Sou,7),
				T(Suit.Wind,1),T(Suit.Wind,1),T(Suit.Wind,1),
				T(Suit.Dragon,2),T(Suit.Dragon,2)
			};
			var r = Pick(hand, T(Suit.Dragon,2), true, false);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.Toitoi));
		}

		[Test]
		public void SanshokuDoujun_SameStartAcrossThreeSuits()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,3),T(Suit.Man,4),T(Suit.Man,5),
				T(Suit.Pin,3),T(Suit.Pin,4),T(Suit.Pin,5),
				T(Suit.Sou,3),T(Suit.Sou,4),T(Suit.Sou,5),
				T(Suit.Pin,7),T(Suit.Pin,8),T(Suit.Pin,9),
				T(Suit.Pin,2),T(Suit.Pin,2)
			};
			var r = Pick(hand, T(Suit.Sou,5), true, false);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.SanshokuDoujun));
		}

		[Test]
		public void Ittsu_OneTwoThree_FourFiveSix_SevenEightNine_SameSuit()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,3),
				T(Suit.Man,4),T(Suit.Man,5),T(Suit.Man,6),
				T(Suit.Man,7),T(Suit.Man,8),T(Suit.Man,9),
				T(Suit.Pin,3),T(Suit.Pin,4),T(Suit.Pin,5),
				T(Suit.Sou,2),T(Suit.Sou,2)
			};
			var r = Pick(hand, T(Suit.Pin,5), true, false);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.Ittsu));
		}

		[Test]
		public void Chinitsu_SingleSuitOnly()
		{
			var hand = new List<Tile>
			{
				T(Suit.Sou,1),T(Suit.Sou,2),T(Suit.Sou,3),
				T(Suit.Sou,4),T(Suit.Sou,5),T(Suit.Sou,6),
				T(Suit.Sou,7),T(Suit.Sou,8),T(Suit.Sou,9),
				T(Suit.Sou,3),T(Suit.Sou,4),T(Suit.Sou,5),
				T(Suit.Sou,2),T(Suit.Sou,2)
			};
			var r = Pick(hand, T(Suit.Sou,5), true, false);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.Chinitsu));
		}

		[Test]
		public void Chiitoitsu_SevenPairs_HasYaku()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,2),T(Suit.Man,2),
				T(Suit.Man,5),T(Suit.Man,5),
				T(Suit.Pin,3),T(Suit.Pin,3),
				T(Suit.Pin,8),T(Suit.Pin,8),
				T(Suit.Sou,4),T(Suit.Sou,4),
				T(Suit.Sou,6),T(Suit.Sou,6),
				T(Suit.Wind,2),T(Suit.Wind,2)
			};
			var r = Pick(hand, T(Suit.Wind,2), true, false);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.Chiitoitsu));
		}

		[Test]
		public void Kokushi_IsYakuman()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,9),
				T(Suit.Pin,1),T(Suit.Pin,9),
				T(Suit.Sou,1),T(Suit.Sou,9),
				T(Suit.Wind,1),T(Suit.Wind,2),T(Suit.Wind,3),T(Suit.Wind,4),
				T(Suit.Dragon,1),T(Suit.Dragon,2),T(Suit.Dragon,3),
				T(Suit.Dragon,3)
			};
			var r = Pick(hand, T(Suit.Dragon,3), true, false);
			Assert.IsTrue(r.Yaku.YakumanMultiplier >= 1, "国士無双은 야쿠만이어야 한다");
		}

		[Test]
		public void Daisangen_AllThreeDragonTriplets_IsYakuman()
		{
			var hand = new List<Tile>
			{
				T(Suit.Dragon,1),T(Suit.Dragon,1),T(Suit.Dragon,1),
				T(Suit.Dragon,2),T(Suit.Dragon,2),T(Suit.Dragon,2),
				T(Suit.Dragon,3),T(Suit.Dragon,3),T(Suit.Dragon,3),
				T(Suit.Man,2),T(Suit.Man,3),T(Suit.Man,4),
				T(Suit.Pin,5),T(Suit.Pin,5)
			};
			var r = Pick(hand, T(Suit.Dragon,3), true, false);
			Assert.IsTrue(r.Yaku.YakumanMultiplier >= 1);
		}

		[Test]
		public void Honitsu_OneSuitPlusHonors()
		{
			var hand = new List<Tile>
			{
				T(Suit.Sou,1),T(Suit.Sou,2),T(Suit.Sou,3),
				T(Suit.Sou,4),T(Suit.Sou,5),T(Suit.Sou,6),
				T(Suit.Sou,7),T(Suit.Sou,8),T(Suit.Sou,9),
				T(Suit.Dragon,1),T(Suit.Dragon,1),T(Suit.Dragon,1),
				T(Suit.Wind,2),T(Suit.Wind,2)
			};
			var r = Pick(hand, T(Suit.Wind,2), true, false);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.Honitsu));
		}

		[Test]
		public void Riichi_AddsHan()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,2),T(Suit.Man,3),T(Suit.Man,4),
				T(Suit.Pin,3),T(Suit.Pin,4),T(Suit.Pin,5),
				T(Suit.Sou,2),T(Suit.Sou,3),T(Suit.Sou,4),
				T(Suit.Sou,6),T(Suit.Sou,7),T(Suit.Sou,8),
				T(Suit.Pin,7),T(Suit.Pin,7)
			};
			var r = Pick(hand, T(Suit.Sou,8), true, true);
			Assert.IsTrue(r.Yaku.Hits.Any(h => h.Id == YakuId.Riichi));
		}
	}
}
