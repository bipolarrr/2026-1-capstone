using NUnit.Framework;

namespace MahjongTests
{
	public class DefenseCalculatorTests
	{
		[Test]
		public void NoComboEnemyDamage_UsesHalfRankMultiplier()
		{
			Assert.AreEqual(2, DefenseCalculator.CalculateEnemyDamage(3, EnemyDiceResult.GetMultiplier("")));
			Assert.AreEqual(3, DefenseCalculator.CalculateEnemyDamage(5, EnemyDiceResult.GetMultiplier(null)));
		}
	}
}
