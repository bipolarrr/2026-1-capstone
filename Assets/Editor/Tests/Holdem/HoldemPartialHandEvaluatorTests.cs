using NUnit.Framework;
using Holdem;

namespace HoldemTests
{
	public class HoldemPartialHandEvaluatorTests
	{
		static HoldemCard C(HoldemRank rank, HoldemSuit suit) => new HoldemCard(rank, suit);

		[Test]
		public void ThreeVisibleCards_HighCard()
		{
			var result = HoldemPartialHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Ace, HoldemSuit.Spades),
				C(HoldemRank.Nine, HoldemSuit.Hearts),
				C(HoldemRank.Four, HoldemSuit.Clubs),
			});

			Assert.AreEqual(HoldemHandRank.HighCard, result.Rank);
			Assert.AreEqual(HoldemRank.Ace, result.PrimaryRank);
		}

		[Test]
		public void ThreeVisibleCards_OnePair()
		{
			var result = HoldemPartialHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.King, HoldemSuit.Spades),
				C(HoldemRank.King, HoldemSuit.Hearts),
				C(HoldemRank.Four, HoldemSuit.Clubs),
			});

			Assert.AreEqual(HoldemHandRank.OnePair, result.Rank);
			Assert.AreEqual(HoldemRank.King, result.PrimaryRank);
		}

		[Test]
		public void ThreeVisibleCards_ThreeOfAKind()
		{
			var result = HoldemPartialHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Seven, HoldemSuit.Spades),
				C(HoldemRank.Seven, HoldemSuit.Hearts),
				C(HoldemRank.Seven, HoldemSuit.Clubs),
			});

			Assert.AreEqual(HoldemHandRank.ThreeOfAKind, result.Rank);
		}

		[Test]
		public void ThreeVisibleCards_NeverReportsStraightFlushOrFullHouse()
		{
			var result = HoldemPartialHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Ten, HoldemSuit.Spades),
				C(HoldemRank.Jack, HoldemSuit.Spades),
				C(HoldemRank.Queen, HoldemSuit.Spades),
			});

			Assert.AreEqual(HoldemHandRank.HighCard, result.Rank);
			Assert.AreNotEqual(HoldemHandRank.FullHouse, result.Rank);
			Assert.Less(result.Rank, HoldemHandRank.Straight);
		}
	}
}
