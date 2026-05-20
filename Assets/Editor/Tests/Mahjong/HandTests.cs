using NUnit.Framework;
using Mahjong;

namespace MahjongTests
{
	public class HandTests
	{
		[Test]
		public void DeclareAnkan_WithLessThanFourTiles_DoesNotMutateHand()
		{
			var hand = new Hand();
			var oneMan = new Tile(Suit.Man, 1);
			var twoMan = new Tile(Suit.Man, 2);
			var tiles = new[]
			{
				oneMan, oneMan, oneMan,
				twoMan, twoMan, twoMan,
				new Tile(Suit.Man, 3),
				new Tile(Suit.Man, 4),
				new Tile(Suit.Man, 5),
				new Tile(Suit.Man, 6),
				new Tile(Suit.Man, 7),
				new Tile(Suit.Man, 8),
				new Tile(Suit.Man, 9)
			};

			hand.DealInitial(tiles);
			hand.SetDraw(new Tile(Suit.Pin, 1));

			Assert.IsFalse(hand.CanDeclareAnkan(oneMan));
			Assert.IsFalse(hand.DeclareAnkan(oneMan));
			Assert.AreEqual(13, hand.Closed.Count);
			Assert.AreEqual(new Tile(Suit.Pin, 1), hand.Draw);
			Assert.AreEqual(0, hand.Ankans.Count);
		}

		[Test]
		public void DeclareAnkan_WithFourTiles_RemovesTilesAndAddsMeld()
		{
			var hand = new Hand();
			var oneMan = new Tile(Suit.Man, 1);
			var tiles = new[]
			{
				oneMan, oneMan, oneMan,
				new Tile(Suit.Man, 2),
				new Tile(Suit.Man, 3),
				new Tile(Suit.Man, 4),
				new Tile(Suit.Man, 5),
				new Tile(Suit.Man, 6),
				new Tile(Suit.Man, 7),
				new Tile(Suit.Man, 8),
				new Tile(Suit.Man, 9),
				new Tile(Suit.Pin, 1),
				new Tile(Suit.Pin, 2)
			};

			hand.DealInitial(tiles);
			hand.SetDraw(oneMan);

			Assert.IsTrue(hand.CanDeclareAnkan(oneMan));
			Assert.IsTrue(hand.DeclareAnkan(oneMan));
			Assert.AreEqual(10, hand.Closed.Count);
			Assert.IsNull(hand.Draw);
			Assert.AreEqual(1, hand.Ankans.Count);
			Assert.AreEqual(MeldKind.Kantsu, hand.Ankans[0].Kind);
			Assert.AreEqual(oneMan, hand.Ankans[0].First);
		}
	}
}
