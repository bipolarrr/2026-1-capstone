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

		[Test]
		public void EnemyDicePresentationMode_UsesViewportForRankOneToThree()
		{
			Assert.AreEqual(EnemyDicePresentationMode.Viewport,
				EnemyCounterAttackDirector.ResolveDicePresentationMode(1, true));
			Assert.AreEqual(EnemyDicePresentationMode.Viewport,
				EnemyCounterAttackDirector.ResolveDicePresentationMode(3, true));
		}

		[Test]
		public void EnemyDicePresentationMode_UsesCenterPopupForRankFourAndFive()
		{
			Assert.AreEqual(EnemyDicePresentationMode.CenterPopup,
				EnemyCounterAttackDirector.ResolveDicePresentationMode(4, true));
			Assert.AreEqual(EnemyDicePresentationMode.CenterPopup,
				EnemyCounterAttackDirector.ResolveDicePresentationMode(5, true));
		}

		[Test]
		public void EnemyDicePresentationMode_DoesNotChangeDefenseEvaluation()
		{
			var enemy = new EnemyDiceResult
			{
				values = new[] { 2, 3, 4, 5 },
				comboName = "Small Straight",
				hasCombo = true,
				damageMultiplier = EnemyDiceResult.GetMultiplier("Small Straight")
			};

			var mode = EnemyCounterAttackDirector.ResolveDicePresentationMode(4, true);
			var defense = DefenseCalculator.Evaluate(new[] { 1, 2, 3, 4, 6 }, enemy);

			Assert.AreEqual(EnemyDicePresentationMode.CenterPopup, mode);
			Assert.IsTrue(defense.blocked);
			Assert.AreEqual(0, DefenseCalculator.CalculateEnemyDamage(4, enemy.damageMultiplier)
				* (defense.blocked ? 0 : 1));
		}
	}
}
