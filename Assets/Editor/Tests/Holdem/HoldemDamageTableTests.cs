using NUnit.Framework;
using Holdem;

namespace HoldemTests
{
	public class HoldemDamageTableTests
	{
		static HoldemHandResult Result(HoldemHandRank rank, HoldemRank primary = HoldemRank.Ace)
		{
			return new HoldemHandResult(rank, new[] { primary }, null);
		}

		[Test]
		public void StageMultipliers_DefaultValues()
		{
			Assert.AreEqual(2.4f, HoldemDamageTable.GetStageMultiplier(1));
			Assert.AreEqual(1.6f, HoldemDamageTable.GetStageMultiplier(2));
			Assert.AreEqual(1.0f, HoldemDamageTable.GetStageMultiplier(3));
		}

		[Test]
		public void HighCard_IsSingleTargetOnly()
		{
			Assert.IsFalse(HoldemDamageTable.IsAoe(HoldemHandRank.HighCard));
			Assert.AreEqual(HoldemDamageTargetMode.SingleTarget,
				HoldemDamageTable.Calculate(Result(HoldemHandRank.HighCard), 1).TargetMode);
		}

		[Test]
		public void OnePairOrBetter_IsAoe()
		{
			Assert.IsTrue(HoldemDamageTable.IsAoe(HoldemHandRank.OnePair));
			Assert.IsTrue(HoldemDamageTable.IsAoe(HoldemHandRank.RoyalFlush));
			Assert.AreEqual(HoldemDamageTargetMode.Aoe,
				HoldemDamageTable.Calculate(Result(HoldemHandRank.OnePair), 1).TargetMode);
		}

		[Test]
		public void BaseHandDamage_MatchesTable()
		{
			Assert.AreEqual(7, HoldemDamageTable.GetBaseHandDamage(HoldemHandRank.OnePair));
			Assert.AreEqual(11, HoldemDamageTable.GetBaseHandDamage(HoldemHandRank.TwoPair));
			Assert.AreEqual(15, HoldemDamageTable.GetBaseHandDamage(HoldemHandRank.ThreeOfAKind));
			Assert.AreEqual(20, HoldemDamageTable.GetBaseHandDamage(HoldemHandRank.Straight));
			Assert.AreEqual(22, HoldemDamageTable.GetBaseHandDamage(HoldemHandRank.Flush));
			Assert.AreEqual(27, HoldemDamageTable.GetBaseHandDamage(HoldemHandRank.FullHouse));
			Assert.AreEqual(36, HoldemDamageTable.GetBaseHandDamage(HoldemHandRank.FourOfAKind));
			Assert.AreEqual(45, HoldemDamageTable.GetBaseHandDamage(HoldemHandRank.StraightFlush));
			Assert.AreEqual(50, HoldemDamageTable.GetBaseHandDamage(HoldemHandRank.RoyalFlush));
		}

		[Test]
		public void PostMultiplierDamageCap_IsEighty()
		{
			Assert.AreEqual(80, HoldemDamageTable.DamageCap);
			Assert.AreEqual(80, HoldemDamageTable.Calculate(Result(HoldemHandRank.RoyalFlush), 1).Damage);
		}

		[Test]
		public void Stage2RoyalFlush_CapsAtEighty()
		{
			Assert.AreEqual(80, HoldemDamageTable.Calculate(Result(HoldemHandRank.RoyalFlush), 2).Damage);
		}

		[Test]
		public void HighCardBonus_UsesPrimaryRank()
		{
			Assert.AreEqual(16, HoldemDamageTable.Calculate(Result(HoldemHandRank.HighCard, HoldemRank.Ace), 1).Damage);
			Assert.AreEqual(13, HoldemDamageTable.Calculate(Result(HoldemHandRank.HighCard, HoldemRank.Jack), 1).Damage);
			Assert.AreEqual(12, HoldemDamageTable.Calculate(Result(HoldemHandRank.HighCard, HoldemRank.Ten), 1).Damage);
		}
	}
}
