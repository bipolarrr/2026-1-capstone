using System.Collections.Generic;
using NUnit.Framework;
using Holdem;

namespace HoldemTests
{
	public class HoldemHandEvaluatorTests
	{
		static HoldemCard C(HoldemRank rank, HoldemSuit suit) => new HoldemCard(rank, suit);

		[Test]
		public void RoyalFlush_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Ten, HoldemSuit.Spades),
				C(HoldemRank.Jack, HoldemSuit.Spades),
				C(HoldemRank.Queen, HoldemSuit.Spades),
				C(HoldemRank.King, HoldemSuit.Spades),
				C(HoldemRank.Ace, HoldemSuit.Spades),
			});

			Assert.AreEqual(HoldemHandRank.RoyalFlush, result.Rank);
		}

		[Test]
		public void StraightFlush_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Five, HoldemSuit.Hearts),
				C(HoldemRank.Six, HoldemSuit.Hearts),
				C(HoldemRank.Seven, HoldemSuit.Hearts),
				C(HoldemRank.Eight, HoldemSuit.Hearts),
				C(HoldemRank.Nine, HoldemSuit.Hearts),
			});

			Assert.AreEqual(HoldemHandRank.StraightFlush, result.Rank);
		}

		[Test]
		public void FourOfAKind_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Nine, HoldemSuit.Clubs),
				C(HoldemRank.Nine, HoldemSuit.Diamonds),
				C(HoldemRank.Nine, HoldemSuit.Hearts),
				C(HoldemRank.Nine, HoldemSuit.Spades),
				C(HoldemRank.Two, HoldemSuit.Clubs),
			});

			Assert.AreEqual(HoldemHandRank.FourOfAKind, result.Rank);
		}

		[Test]
		public void FullHouse_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Queen, HoldemSuit.Clubs),
				C(HoldemRank.Queen, HoldemSuit.Diamonds),
				C(HoldemRank.Queen, HoldemSuit.Hearts),
				C(HoldemRank.Four, HoldemSuit.Spades),
				C(HoldemRank.Four, HoldemSuit.Clubs),
			});

			Assert.AreEqual(HoldemHandRank.FullHouse, result.Rank);
		}

		[Test]
		public void Flush_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Two, HoldemSuit.Diamonds),
				C(HoldemRank.Five, HoldemSuit.Diamonds),
				C(HoldemRank.Seven, HoldemSuit.Diamonds),
				C(HoldemRank.Jack, HoldemSuit.Diamonds),
				C(HoldemRank.King, HoldemSuit.Diamonds),
			});

			Assert.AreEqual(HoldemHandRank.Flush, result.Rank);
		}

		[Test]
		public void Straight_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Six, HoldemSuit.Clubs),
				C(HoldemRank.Seven, HoldemSuit.Diamonds),
				C(HoldemRank.Eight, HoldemSuit.Hearts),
				C(HoldemRank.Nine, HoldemSuit.Spades),
				C(HoldemRank.Ten, HoldemSuit.Clubs),
			});

			Assert.AreEqual(HoldemHandRank.Straight, result.Rank);
			Assert.AreEqual(HoldemRank.Ten, result.TieBreakers[0]);
		}

		[Test]
		public void AceLowStraight_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Ace, HoldemSuit.Clubs),
				C(HoldemRank.Two, HoldemSuit.Diamonds),
				C(HoldemRank.Three, HoldemSuit.Hearts),
				C(HoldemRank.Four, HoldemSuit.Spades),
				C(HoldemRank.Five, HoldemSuit.Clubs),
			});

			Assert.AreEqual(HoldemHandRank.Straight, result.Rank);
			Assert.AreEqual(HoldemRank.Five, result.TieBreakers[0]);
		}

		[Test]
		public void ThreeOfAKind_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Seven, HoldemSuit.Clubs),
				C(HoldemRank.Seven, HoldemSuit.Diamonds),
				C(HoldemRank.Seven, HoldemSuit.Spades),
				C(HoldemRank.King, HoldemSuit.Hearts),
				C(HoldemRank.Two, HoldemSuit.Hearts),
			});

			Assert.AreEqual(HoldemHandRank.ThreeOfAKind, result.Rank);
		}

		[Test]
		public void TwoPair_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Eight, HoldemSuit.Clubs),
				C(HoldemRank.Eight, HoldemSuit.Diamonds),
				C(HoldemRank.Three, HoldemSuit.Spades),
				C(HoldemRank.Three, HoldemSuit.Hearts),
				C(HoldemRank.Ace, HoldemSuit.Hearts),
			});

			Assert.AreEqual(HoldemHandRank.TwoPair, result.Rank);
		}

		[Test]
		public void OnePair_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Jack, HoldemSuit.Clubs),
				C(HoldemRank.Jack, HoldemSuit.Diamonds),
				C(HoldemRank.Three, HoldemSuit.Spades),
				C(HoldemRank.Seven, HoldemSuit.Hearts),
				C(HoldemRank.Ace, HoldemSuit.Hearts),
			});

			Assert.AreEqual(HoldemHandRank.OnePair, result.Rank);
		}

		[Test]
		public void HighCard_IsRecognized()
		{
			var result = HoldemHandEvaluator.Evaluate(new[]
			{
				C(HoldemRank.Two, HoldemSuit.Clubs),
				C(HoldemRank.Five, HoldemSuit.Diamonds),
				C(HoldemRank.Nine, HoldemSuit.Spades),
				C(HoldemRank.Jack, HoldemSuit.Hearts),
				C(HoldemRank.Ace, HoldemSuit.Hearts),
			});

			Assert.AreEqual(HoldemHandRank.HighCard, result.Rank);
			Assert.AreEqual(HoldemRank.Ace, result.PrimaryRank);
		}

		[Test]
		public void BestFiveOutOfSeven_ChoosesStrongerHand()
		{
			var result = HoldemHandEvaluator.Evaluate(new List<HoldemCard>
			{
				C(HoldemRank.Queen, HoldemSuit.Clubs),
				C(HoldemRank.Queen, HoldemSuit.Diamonds),
				C(HoldemRank.Queen, HoldemSuit.Hearts),
				C(HoldemRank.Four, HoldemSuit.Spades),
				C(HoldemRank.Four, HoldemSuit.Clubs),
				C(HoldemRank.Ace, HoldemSuit.Spades),
				C(HoldemRank.King, HoldemSuit.Spades),
			});

			Assert.AreEqual(HoldemHandRank.FullHouse, result.Rank);
			Assert.AreEqual(HoldemRank.Queen, result.TieBreakers[0]);
		}
	}
}
