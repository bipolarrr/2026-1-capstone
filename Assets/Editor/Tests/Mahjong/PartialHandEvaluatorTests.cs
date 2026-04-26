using System.Collections.Generic;
using NUnit.Framework;
using Mahjong;

namespace MahjongTests
{
	public class PartialHandEvaluatorTests
	{
		static Tile T(Suit s, int v) => new Tile(s, v);

		[Test]
		public void Empty_GivesZero()
		{
			var b = PartialHandEvaluator.Evaluate(new List<Tile>());
			Assert.AreEqual(0, b.TotalMelds);
			Assert.AreEqual(0, b.Pair);
		}

		[Test]
		public void TwoShuntsuOnePair_IsRecognized()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,3),
				T(Suit.Pin,4),T(Suit.Pin,5),T(Suit.Pin,6),
				T(Suit.Sou,7),T(Suit.Sou,7)
			};
			var b = PartialHandEvaluator.Evaluate(hand);
			Assert.AreEqual(2, b.Shuntsu);
			Assert.AreEqual(1, b.Pair);
		}

		[Test]
		public void Koutsu_IsPreferredOverPair_WhenScoreIsHigher()
		{
			var hand = new List<Tile> { T(Suit.Pin,5),T(Suit.Pin,5),T(Suit.Pin,5) };
			var b = PartialHandEvaluator.Evaluate(hand);
			Assert.AreEqual(1, b.Koutsu);
			Assert.AreEqual(0, b.Pair);
		}

		[Test]
		public void FourTiles_FormsKantsu()
		{
			var hand = new List<Tile> { T(Suit.Sou,3),T(Suit.Sou,3),T(Suit.Sou,3),T(Suit.Sou,3) };
			var b = PartialHandEvaluator.Evaluate(hand);
			Assert.AreEqual(1, b.Kantsu);
		}
	}
}
