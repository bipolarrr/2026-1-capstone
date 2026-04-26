using NUnit.Framework;
using Mahjong;

namespace MahjongTests
{
	public class EnemyMahjongStateTests
	{
		[Test]
		public void SameSeed_ProducesSameGroups()
		{
			var a = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);
			var b = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);
			Assert.AreEqual(a.Group1.NeedTile, b.Group1.NeedTile);
			Assert.AreEqual(a.Group2.NeedTile, b.Group2.NeedTile);
			Assert.AreEqual(a.Group1.Type, b.Group1.Type);
		}

		[Test]
		public void OnDiscard_NonMatching_ReturnsNull()
		{
			var s = new EnemyMahjongState(rank: 1, seed: 50, doraTiles: null);
			// 거의 확률 0인 패: 무관한 다른 값
			var weird = new Tile(Suit.Dragon, 1);
			var trigger = s.OnPlayerDiscard(weird);
			// 우연히 백패가 needTile이면 null이 아닐 수 있음. 그 경우 통과 처리.
			if (trigger == null) Assert.Pass();
			else Assert.AreEqual(weird, s.Group1.NeedTile.SameKind(weird) ? s.Group1.NeedTile : s.Group2.NeedTile);
		}

		[Test]
		public void OnDiscard_Matching_ReturnsResult_WithRankScaledDamage()
		{
			var s = new EnemyMahjongState(rank: 3, seed: 9, doraTiles: null);
			var need = s.Group1.NeedTile;
			var r = s.OnPlayerDiscard(need);
			Assert.IsNotNull(r);
			Assert.AreEqual(3, r.RankUsed);
			Assert.IsTrue(r.DamageHalfHearts >= 1, "랭크3은 또이츠(0.25×3=ceil 1)에서도 최소 1 절반하트");
		}

		[Test]
		public void Reroll_ChangesGroupsForDifferentSeed()
		{
			var s1 = new EnemyMahjongState(rank: 2, seed: 1, doraTiles: null);
			var n1 = s1.Group1.NeedTile;
			var s2 = new EnemyMahjongState(rank: 2, seed: 2, doraTiles: null);
			var n2 = s2.Group1.NeedTile;
			// 다른 시드는 다른 결과(정확히 같을 확률 매우 낮음)
			Assert.IsTrue(!n1.Equals(n2) || !s1.Group1.Type.Equals(s2.Group1.Type));
		}
	}
}
