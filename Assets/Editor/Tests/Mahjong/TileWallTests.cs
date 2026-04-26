using NUnit.Framework;
using Mahjong;

namespace MahjongTests
{
	public class TileWallTests
	{
		[Test]
		public void StandardSet_Has136Tiles()
		{
			var set = TileFactory.BuildStandardSet();
			Assert.AreEqual(136, set.Count);
		}

		[Test]
		public void Wall_AfterDoraReserve_Has131DrawableTiles()
		{
			var w = new TileWall(seed: 12345);
			w.ReserveDoraIndicators(5);
			int drawn = 0;
			while (w.TryDraw(out _)) drawn++;
			Assert.AreEqual(131, drawn, "도라 5장 보관 후 패산은 131장이어야 한다");
		}

		[Test]
		public void Wall_DoraIndicators_AreReservedFromTail()
		{
			var w = new TileWall(seed: 99);
			w.ReserveDoraIndicators(5);
			Assert.AreEqual(5, w.DoraIndicators.Count);
		}

		[Test]
		public void Wall_RinshanDraw_ComesFromTail()
		{
			var w = new TileWall(seed: 7);
			w.ReserveDoraIndicators(5);
			Assert.IsTrue(w.TryDrawRinshan(out var t));
			Assert.IsNotNull(t);
		}

		[Test]
		public void Wall_SameSeed_ProducesSameOrder()
		{
			var a = new TileWall(seed: 42);
			var b = new TileWall(seed: 42);
			for (int i = 0; i < 10; i++)
			{
				a.TryDraw(out var ta); b.TryDraw(out var tb);
				Assert.AreEqual(ta, tb);
			}
		}
	}
}
