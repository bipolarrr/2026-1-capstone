using System.Reflection;
using NUnit.Framework;

namespace HoldemTests
{
	public class HoldemRoutingTests
	{
		[Test]
		public void HoldemRoutesToHoldemBattleScene()
		{
			Assert.AreEqual("HoldemBattleScene", Resolve(CharacterType.Holdem));
		}

		[Test]
		public void DiceAndMahjongRouting_Unchanged()
		{
			Assert.AreEqual("DiceBattleScene", Resolve(CharacterType.Dice));
			Assert.AreEqual("MahjongBattleScene", Resolve(CharacterType.Mahjong));
		}

		static string Resolve(CharacterType character)
		{
			var method = typeof(GameExploreController).GetMethod(
				"ResolveBattleSceneName",
				BindingFlags.Static | BindingFlags.NonPublic);
			Assert.NotNull(method);
			return (string)method.Invoke(null, new object[] { character });
		}
	}
}
