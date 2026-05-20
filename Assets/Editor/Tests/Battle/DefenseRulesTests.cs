using NUnit.Framework;

namespace BattleTests
{
	public class DefenseRulesTests
	{
		[Test]
		public void NoComboDefense_BlocksOnlyWhenEnemyDiceAreSubset()
		{
			var enemy = new EnemyDiceResult
			{
				values = new[] { 2, 2, 5 },
				comboName = "",
				hasCombo = false,
				damageMultiplier = EnemyDiceResult.GetMultiplier("")
			};

			var blocked = DefenseCalculator.Evaluate(new[] { 1, 2, 2, 4, 5 }, enemy);
			Assert.IsTrue(blocked.blocked);
			Assert.AreEqual(1f, blocked.reductionRate);

			var failed = DefenseCalculator.Evaluate(new[] { 1, 2, 4, 5, 6 }, enemy);
			Assert.IsFalse(failed.blocked);
			Assert.AreEqual(0f, failed.reductionRate);
		}

		[Test]
		public void ComboDefense_MatchesComboName()
		{
			var enemy = new EnemyDiceResult
			{
				values = new[] { 2, 3, 4, 5 },
				comboName = "Small Straight",
				hasCombo = true,
				damageMultiplier = EnemyDiceResult.GetMultiplier("Small Straight")
			};

			Assert.IsTrue(DefenseCalculator.Evaluate(new[] { 1, 2, 3, 4, 6 }, enemy).blocked);
			Assert.IsFalse(DefenseCalculator.Evaluate(new[] { 1, 1, 3, 4, 6 }, enemy).blocked);
		}

		[Test]
		public void DefenseRollCount_UsesEnemyComboPresence()
		{
			Assert.AreEqual(1, EnemyCounterAttackDirector.GetDefenseRollCount(new EnemyDiceResult { hasCombo = false }));
			Assert.AreEqual(3, EnemyCounterAttackDirector.GetDefenseRollCount(new EnemyDiceResult { hasCombo = true }));
		}
	}
}
