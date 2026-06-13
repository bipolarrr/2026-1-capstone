using NUnit.Framework;
using Mahjong;
using System.Collections.Generic;

namespace MahjongTests
{
	public class DamageTableTests
	{
		[Test]
		public void OneHan_GivesOneHalfHeart()
		{
			var y = new YakuResult { TotalHan = 1 };
			y.Hits.Add(new YakuHit(YakuId.Pinfu, 1));
			Assert.AreEqual(1, MahjongDamageTable.GetWinDamageHalfHearts(y));
		}

		[Test]
		public void Mangan_FiveHan_GivesFour()
		{
			var y = new YakuResult { TotalHan = 5 };
			y.Hits.Add(new YakuHit(YakuId.Riichi, 1));
			Assert.AreEqual(4, MahjongDamageTable.GetWinDamageHalfHearts(y));
		}

		[Test]
		public void Yakuman_GivesFullSixteen()
		{
			var y = new YakuResult { YakumanMultiplier = 1, TotalHan = 13 };
			y.Hits.Add(new YakuHit(YakuId.Suuankou, 13, true));
			Assert.AreEqual(MahjongDamageTable.FullAoeHalfHearts, MahjongDamageTable.GetWinDamageHalfHearts(y));
		}

		[Test]
		public void DoubleYakuman_GivesThirtyTwo()
		{
			var y = new YakuResult { YakumanMultiplier = 2, TotalHan = 26 };
			y.Hits.Add(new YakuHit(YakuId.Daisuushii, 26, true));
			Assert.AreEqual(MahjongDamageTable.FullAoeHalfHearts * 2, MahjongDamageTable.GetWinDamageHalfHearts(y));
		}

		[TestCase(1, 3)]
		[TestCase(3, 9)]
		[TestCase(4, 12)]
		[TestCase(16, 48)]
		[TestCase(32, 96)]
		public void PlayerWinBattleDamage_ScalesBaseWinDamageByThree(int baseDamage, int expected)
		{
			Assert.AreEqual(expected, MahjongDamageTable.ScalePlayerWinDamageForBattle(baseDamage));
		}

		[TestCase(0)]
		[TestCase(-1)]
		public void PlayerWinBattleDamage_NonPositiveInput_StaysZero(int baseDamage)
		{
			Assert.AreEqual(0, MahjongDamageTable.ScalePlayerWinDamageForBattle(baseDamage));
		}

		[Test]
		public void MahjongYakuFocus_IncreasesPlayerWinBattleDamage()
		{
			int scaled = MahjongDamageTable.ScalePlayerWinDamageForBattle(4);

			int boosted = MahjongDamageTable.ApplyPowerUpsToPlayerWinBattleDamage(
				scaled,
				new List<PowerUpType> { PowerUpType.MahjongYakuFocus });

			Assert.AreEqual(15, boosted);
		}

		[Test]
		public void NoYaku_GivesZero()
		{
			var y = new YakuResult();
			Assert.AreEqual(0, MahjongDamageTable.GetWinDamageHalfHearts(y));
		}

		[Test]
		public void Partial_ThreeMeldsOnePair_GivesOne()
		{
			var b = new PartialBreakdown { Shuntsu = 2, Koutsu = 1, Pair = 1 };
			// (2+1)*0.5 + 0.25 = 1.75 → ×0.5 = 0.875 → ceil 1
			Assert.AreEqual(1, MahjongDamageTable.GetPartialDamageHalfHearts(b));
		}

		[Test]
		public void Partial_FourMeldsOnePair_KangAdded()
		{
			var b = new PartialBreakdown { Shuntsu = 2, Koutsu = 1, Kantsu = 1, Pair = 1 };
			// (2+1)*0.5 + 0.25 + 1*0.75 = 2.5 → ×0.5 = 1.25 → ceil 2
			Assert.AreEqual(2, MahjongDamageTable.GetPartialDamageHalfHearts(b));
		}

		[Test]
		public void MahjongPartialFocus_IncreasesPositivePartialDamage()
		{
			int boosted = MahjongDamageTable.ApplyPowerUpsToPartialDamage(
				1,
				new List<PowerUpType> { PowerUpType.MahjongPartialFocus });

			Assert.AreEqual(2, boosted);
		}

		[Test]
		public void MahjongSafetyCharm_ReducesEnemyDamage()
		{
			int reduced = MahjongDamageTable.ApplyPowerUpsToEnemyDamage(
				3,
				new List<PowerUpType> { PowerUpType.MahjongSafetyCharm });

			Assert.AreEqual(2, reduced);
		}
	}
}
