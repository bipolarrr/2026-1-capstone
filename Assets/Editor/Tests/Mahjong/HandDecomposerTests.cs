using System.Collections.Generic;
using NUnit.Framework;
using Mahjong;

namespace MahjongTests
{
	public class HandDecomposerTests
	{
		static Tile T(Suit s, int v) => new Tile(s, v);

		[Test]
		public void Standard_FourShuntsuPair_IsRecognized()
		{
			// 만수 1-9 슌쯔 3개 + 통수 1-3 + 통수 1-1 머리 모자라; 다시 짜자.
			// 만 123 456 789 + 통 123 + 사 발發 머리
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,3),
				T(Suit.Man,4),T(Suit.Man,5),T(Suit.Man,6),
				T(Suit.Man,7),T(Suit.Man,8),T(Suit.Man,9),
				T(Suit.Pin,1),T(Suit.Pin,2),T(Suit.Pin,3),
				T(Suit.Dragon,2),T(Suit.Dragon,2)
			};
			var winning = T(Suit.Pin,3);
			var results = HandDecomposer.Enumerate(hand, null, winning, true);
			Assert.IsTrue(results.Exists(r => r.Shape == WinShape.Standard), "표준형 분해가 발견되어야 한다");
		}

		[Test]
		public void Chiitoitsu_SevenPairs_IsRecognized()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,1),
				T(Suit.Man,3),T(Suit.Man,3),
				T(Suit.Pin,2),T(Suit.Pin,2),
				T(Suit.Pin,5),T(Suit.Pin,5),
				T(Suit.Sou,4),T(Suit.Sou,4),
				T(Suit.Sou,7),T(Suit.Sou,7),
				T(Suit.Dragon,1),T(Suit.Dragon,1)
			};
			var results = HandDecomposer.Enumerate(hand, null, T(Suit.Dragon,1), true);
			Assert.IsTrue(results.Exists(r => r.Shape == WinShape.Chiitoitsu), "七対子 인식");
		}

		[Test]
		public void Kokushi_ThirteenOrphansPlusOne_IsRecognized()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,9),
				T(Suit.Pin,1),T(Suit.Pin,9),
				T(Suit.Sou,1),T(Suit.Sou,9),
				T(Suit.Wind,1),T(Suit.Wind,2),T(Suit.Wind,3),T(Suit.Wind,4),
				T(Suit.Dragon,1),T(Suit.Dragon,2),T(Suit.Dragon,3),
				T(Suit.Dragon,3) // 重複 = 머리
			};
			var results = HandDecomposer.Enumerate(hand, null, T(Suit.Dragon,3), true);
			Assert.IsTrue(results.Exists(r => r.Shape == WinShape.Kokushi), "国士無双 인식");
		}

		[Test]
		public void NotWinningHand_ReturnsEmpty()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,4),T(Suit.Man,7),
				T(Suit.Pin,3),T(Suit.Pin,6),T(Suit.Pin,9),
				T(Suit.Sou,2),T(Suit.Sou,5),T(Suit.Sou,8),
				T(Suit.Wind,1),T(Suit.Wind,3),
				T(Suit.Dragon,1),T(Suit.Dragon,3)
			};
			var results = HandDecomposer.Enumerate(hand, null, T(Suit.Man,1), true);
			Assert.AreEqual(0, results.Count);
		}

		[Test]
		public void ThirteenTiles_SinglePairWait_ReturnsTenpai()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,3),
				T(Suit.Man,4),T(Suit.Man,5),T(Suit.Man,6),
				T(Suit.Man,7),T(Suit.Man,8),T(Suit.Man,9),
				T(Suit.Pin,1),T(Suit.Pin,2),T(Suit.Pin,3),
				T(Suit.Wind,1)
			};

			var result = MahjongTenpaiPolicy.EvaluateThirteenTiles(hand, null);

			Assert.IsTrue(result.IsTenpai);
			Assert.AreEqual(1, result.WaitCount);
			AssertContainsWait(result, Suit.Wind, 1);
		}

		[Test]
		public void ThirteenTiles_RyanmenWait_ReturnsTwoWaitKinds()
		{
			var hand = ThreeMeldsPairPlus(T(Suit.Man,2), T(Suit.Man,3));

			var result = MahjongTenpaiPolicy.EvaluateThirteenTiles(hand, null);

			Assert.IsTrue(result.IsTenpai);
			Assert.AreEqual(2, result.WaitCount);
			AssertContainsWait(result, Suit.Man, 1);
			AssertContainsWait(result, Suit.Man, 4);
		}

		[Test]
		public void ThirteenTiles_ClosedWait_ReturnsSingleWaitKind()
		{
			var hand = ThreeMeldsPairPlus(T(Suit.Man,2), T(Suit.Man,4));

			var result = MahjongTenpaiPolicy.EvaluateThirteenTiles(hand, null);

			Assert.IsTrue(result.IsTenpai);
			Assert.AreEqual(1, result.WaitCount);
			AssertContainsWait(result, Suit.Man, 3);
		}

		[Test]
		public void ThirteenTiles_ChiitoitsuWait_ReturnsPairWait()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,1),
				T(Suit.Man,3),T(Suit.Man,3),
				T(Suit.Pin,2),T(Suit.Pin,2),
				T(Suit.Pin,5),T(Suit.Pin,5),
				T(Suit.Sou,4),T(Suit.Sou,4),
				T(Suit.Sou,7),T(Suit.Sou,7),
				T(Suit.Dragon,1)
			};

			var result = MahjongTenpaiPolicy.EvaluateThirteenTiles(hand, null);

			Assert.IsTrue(result.IsTenpai);
			AssertContainsWait(result, Suit.Dragon, 1);
		}

		[Test]
		public void ThirteenTiles_KokushiThirteenSidedWait_ReturnsThirteenWaitKinds()
		{
			var hand = KokushiThirteenKinds();

			var result = MahjongTenpaiPolicy.EvaluateThirteenTiles(hand, null);

			Assert.IsTrue(result.IsTenpai);
			Assert.AreEqual(13, result.WaitCount);
			foreach (var tile in hand)
				AssertContainsWait(result, tile.Suit, tile.Value);
		}

		[Test]
		public void ThirteenTiles_NotTenpai_ReturnsFalse()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,4),T(Suit.Man,7),
				T(Suit.Pin,3),T(Suit.Pin,6),T(Suit.Pin,9),
				T(Suit.Sou,2),T(Suit.Sou,5),T(Suit.Sou,8),
				T(Suit.Wind,1),T(Suit.Wind,3),
				T(Suit.Dragon,1)
			};

			var result = MahjongTenpaiPolicy.EvaluateThirteenTiles(hand, null);

			Assert.IsFalse(result.IsTenpai);
			Assert.AreEqual(0, result.WaitCount);
		}

		[Test]
		public void ThirteenTiles_DoesNotAllowFifthCopyAsWait()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,5),T(Suit.Man,5),T(Suit.Man,5),new Tile(Suit.Man,5,isRedFive:true),
				T(Suit.Pin,1),T(Suit.Pin,2),T(Suit.Pin,3),
				T(Suit.Pin,4),T(Suit.Pin,5),T(Suit.Pin,6),
				T(Suit.Sou,7),T(Suit.Sou,8),T(Suit.Sou,9)
			};

			var result = MahjongTenpaiPolicy.EvaluateThirteenTiles(hand, null);

			Assert.IsFalse(ContainsWait(result, Suit.Man, 5));
			Assert.IsFalse(result.IsTenpai);
		}

		[Test]
		public void FourteenTiles_DiscardFloatingTile_ProducesTenpaiOption()
		{
			var hand = FourteenWithFloatingDragon();

			var options = MahjongTenpaiPolicy.EvaluateDiscardOptions(hand, null);
			var option = OptionFor(options, Suit.Dragon, 1);

			Assert.IsTrue(option.IsTenpaiAfterDiscard);
			Assert.AreEqual(2, option.AfterDiscard.WaitCount);
			AssertContainsWait(option.AfterDiscard, Suit.Man, 1);
			AssertContainsWait(option.AfterDiscard, Suit.Man, 4);
		}

		[Test]
		public void FourteenTiles_DiscardUsefulTile_DoesNotProduceTenpai()
		{
			var hand = FourteenWithFloatingDragon();

			var options = MahjongTenpaiPolicy.EvaluateDiscardOptions(hand, null);
			var option = OptionFor(options, Suit.Man, 2);

			Assert.IsFalse(option.IsTenpaiAfterDiscard);
			Assert.AreEqual(0, option.AfterDiscard.WaitCount);
		}

		[Test]
		public void NeedTileCounter_TenpaiWaitTile_ReturnsTenpai()
		{
			var postDiscard = ThreeMeldsPairPlus(T(Suit.Man, 2), T(Suit.Man, 3));

			var result = MahjongNeedTileCounterPolicy.Evaluate(postDiscard, T(Suit.Man, 1), null);

			Assert.AreEqual(MahjongNeedTileCounterResult.Tenpai, result);
		}

		[Test]
		public void NeedTileCounter_TenpaiNonWaitTile_ReturnsNone()
		{
			var postDiscard = ThreeMeldsPairPlus(T(Suit.Man, 2), T(Suit.Man, 3));

			var result = MahjongNeedTileCounterPolicy.Evaluate(postDiscard, T(Suit.Dragon, 1), null);

			Assert.AreEqual(MahjongNeedTileCounterResult.None, result);
		}

		[Test]
		public void NeedTileCounter_IishantenAdvancingTile_ReturnsIishanten()
		{
			var postDiscard = FourteenWithFloatingDragon();
			RemoveFirstKind(postDiscard, T(Suit.Man, 2));

			var result = MahjongNeedTileCounterPolicy.Evaluate(postDiscard, T(Suit.Man, 2), null);

			Assert.AreEqual(MahjongNeedTileCounterResult.Iishanten, result);
		}

		[Test]
		public void NeedTileCounter_NonAdvancingTile_ReturnsNone()
		{
			var postDiscard = NoTenpaiThirteen();

			var result = MahjongNeedTileCounterPolicy.Evaluate(postDiscard, T(Suit.Dragon, 3), null);

			Assert.AreEqual(MahjongNeedTileCounterResult.None, result);
		}

		[Test]
		public void FourteenTiles_DuplicateDiscardKinds_AreDeduplicated()
		{
			var hand = FourteenWithFloatingDragon();

			var options = MahjongTenpaiPolicy.EvaluateDiscardOptions(hand, null);

			Assert.AreEqual(DistinctKindCount(hand), options.Count);
			Assert.AreEqual(1, CountOptionsFor(options, Suit.Wind, 1));
		}

		[Test]
		public void RedFiveAndNormalFive_AreSameKindForTenpai()
		{
			var hand = new List<Tile>
			{
				T(Suit.Man,5),new Tile(Suit.Man,5,isRedFive:true),
				T(Suit.Pin,1),T(Suit.Pin,2),T(Suit.Pin,3),
				T(Suit.Pin,4),T(Suit.Pin,5),T(Suit.Pin,6),
				T(Suit.Sou,7),T(Suit.Sou,8),T(Suit.Sou,9),
				T(Suit.Wind,1),T(Suit.Wind,1),
				T(Suit.Dragon,1)
			};

			var options = MahjongTenpaiPolicy.EvaluateDiscardOptions(hand, null);

			Assert.AreEqual(1, CountOptionsFor(options, Suit.Man, 5));
		}

		[Test]
		public void TenpaiPolicy_DoesNotMutateInput()
		{
			var hand = ThreeMeldsPairPlus(T(Suit.Man,2), T(Suit.Man,3));
			var before = new List<Tile>(hand);

			MahjongTenpaiPolicy.EvaluateThirteenTiles(hand, null);

			CollectionAssert.AreEqual(before, hand);
		}

		[Test]
		public void AnkanIncluded_StillDetectsTenpai()
		{
			var concealed = new List<Tile>
			{
				T(Suit.Pin,1),T(Suit.Pin,2),T(Suit.Pin,3),
				T(Suit.Sou,7),T(Suit.Sou,8),T(Suit.Sou,9),
				T(Suit.Wind,1),T(Suit.Wind,1),
				T(Suit.Man,2),T(Suit.Man,3)
			};
			var ankans = new List<Meld> { new Meld(MeldKind.Kantsu, T(Suit.Dragon,1)) };

			var result = MahjongTenpaiPolicy.EvaluateThirteenTiles(concealed, ankans);

			Assert.IsTrue(result.IsTenpai);
			Assert.AreEqual(2, result.WaitCount);
			AssertContainsWait(result, Suit.Man, 1);
			AssertContainsWait(result, Suit.Man, 4);
		}

		[Test]
		public void RiichiPolicy_AlreadyDeclared_ReturnsUnavailable()
		{
			var options = new List<MahjongTenpaiDiscardOption>
			{
				RiichiOption(Suit.Man, 1, tenpai: true)
			};

			var result = MahjongRiichiPolicy.Evaluate(options, alreadyRiichiDeclared: true);

			Assert.IsFalse(result.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.AlreadyDeclared, result.Reason);
			Assert.AreEqual(0, result.RiichiDiscardTileKinds.Count);
		}

		[Test]
		public void RiichiPolicy_CannotAct_ReturnsUnavailable()
		{
			var options = new List<MahjongTenpaiDiscardOption>
			{
				RiichiOption(Suit.Man, 1, tenpai: true)
			};

			var result = MahjongRiichiPolicy.Evaluate(options, alreadyRiichiDeclared: false, canAct: false);

			Assert.IsFalse(result.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.CannotAct, result.Reason);
			Assert.AreEqual(0, result.RiichiDiscardTileKinds.Count);
		}

		[Test]
		public void RiichiPolicy_TenpaiDiscardOption_ReturnsAvailable()
		{
			var options = new List<MahjongTenpaiDiscardOption>
			{
				RiichiOption(Suit.Man, 1, tenpai: false),
				RiichiOption(Suit.Man, 2, tenpai: true)
			};

			var result = MahjongRiichiPolicy.Evaluate(options, alreadyRiichiDeclared: false);

			Assert.IsTrue(result.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.Available, result.Reason);
			Assert.AreEqual(1, result.RiichiDiscardTileKinds.Count);
			Assert.AreEqual(TileIndex.Of(T(Suit.Man, 2)), result.RiichiDiscardTileKinds[0]);
		}

		[Test]
		public void RiichiPolicy_ReturnsOnlyTenpaiDiscardKinds()
		{
			var options = new List<MahjongTenpaiDiscardOption>
			{
				RiichiOption(Suit.Man, 1, tenpai: true),
				RiichiOption(Suit.Man, 2, tenpai: false),
				RiichiOption(Suit.Man, 3, tenpai: true)
			};

			var result = MahjongRiichiPolicy.Evaluate(options, alreadyRiichiDeclared: false);

			Assert.AreEqual(2, result.RiichiDiscardTileKinds.Count);
			Assert.AreEqual(TileIndex.Of(T(Suit.Man, 1)), result.RiichiDiscardTileKinds[0]);
			Assert.AreEqual(TileIndex.Of(T(Suit.Man, 3)), result.RiichiDiscardTileKinds[1]);
		}

		[Test]
		public void RiichiPolicy_NoTenpaiDiscardOption_ReturnsUnavailable()
		{
			var options = new List<MahjongTenpaiDiscardOption>
			{
				RiichiOption(Suit.Man, 1, tenpai: false),
				RiichiOption(Suit.Man, 2, tenpai: false)
			};

			var result = MahjongRiichiPolicy.Evaluate(options, alreadyRiichiDeclared: false);

			Assert.IsFalse(result.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.NoTenpaiDiscard, result.Reason);
			Assert.AreEqual(0, result.RiichiDiscardTileKinds.Count);
		}

		[Test]
		public void RiichiPolicy_DeduplicatesDiscardKinds()
		{
			var options = new List<MahjongTenpaiDiscardOption>
			{
				RiichiOption(Suit.Man, 5, tenpai: true),
				RiichiOption(Suit.Man, 5, tenpai: true),
				RiichiOption(Suit.Pin, 1, tenpai: true)
			};

			var result = MahjongRiichiPolicy.Evaluate(options, alreadyRiichiDeclared: false);

			Assert.AreEqual(2, result.RiichiDiscardTileKinds.Count);
			Assert.AreEqual(TileIndex.Of(T(Suit.Man, 5)), result.RiichiDiscardTileKinds[0]);
			Assert.AreEqual(TileIndex.Of(T(Suit.Pin, 1)), result.RiichiDiscardTileKinds[1]);
		}

		[Test]
		public void RiichiPolicy_PreservesInputOrder()
		{
			var options = new List<MahjongTenpaiDiscardOption>
			{
				RiichiOption(Suit.Sou, 7, tenpai: true),
				RiichiOption(Suit.Man, 2, tenpai: true),
				RiichiOption(Suit.Pin, 9, tenpai: true)
			};

			var result = MahjongRiichiPolicy.Evaluate(options, alreadyRiichiDeclared: false);

			Assert.AreEqual(TileIndex.Of(T(Suit.Sou, 7)), result.RiichiDiscardTileKinds[0]);
			Assert.AreEqual(TileIndex.Of(T(Suit.Man, 2)), result.RiichiDiscardTileKinds[1]);
			Assert.AreEqual(TileIndex.Of(T(Suit.Pin, 9)), result.RiichiDiscardTileKinds[2]);
		}

		[Test]
		public void RiichiPolicy_NullOrEmptyDiscardOptions_ReturnsNoTenpaiDiscard()
		{
			var nullResult = MahjongRiichiPolicy.Evaluate(null, alreadyRiichiDeclared: false);
			var emptyResult = MahjongRiichiPolicy.Evaluate(new List<MahjongTenpaiDiscardOption>(), alreadyRiichiDeclared: false);

			Assert.IsFalse(nullResult.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.NoTenpaiDiscard, nullResult.Reason);
			Assert.AreEqual(0, nullResult.RiichiDiscardTileKinds.Count);
			Assert.IsFalse(emptyResult.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.NoTenpaiDiscard, emptyResult.Reason);
			Assert.AreEqual(0, emptyResult.RiichiDiscardTileKinds.Count);
		}

		[Test]
		public void RiichiPolicy_DoesNotMutateInput()
		{
			var options = new List<MahjongTenpaiDiscardOption>
			{
				RiichiOption(Suit.Man, 1, tenpai: true),
				RiichiOption(Suit.Man, 2, tenpai: false),
				RiichiOption(Suit.Man, 1, tenpai: true)
			};
			var before = new List<MahjongTenpaiDiscardOption>(options);

			MahjongRiichiPolicy.Evaluate(options, alreadyRiichiDeclared: false);

			Assert.AreEqual(before.Count, options.Count);
			for (int i = 0; i < before.Count; i++)
			{
				Assert.AreEqual(before[i].DiscardTileKind, options[i].DiscardTileKind);
				Assert.AreEqual(before[i].IsTenpaiAfterDiscard, options[i].IsTenpaiAfterDiscard);
			}
		}

		static List<Tile> ThreeMeldsPairPlus(Tile a, Tile b)
		{
			return new List<Tile>
			{
				T(Suit.Pin,1),T(Suit.Pin,2),T(Suit.Pin,3),
				T(Suit.Pin,4),T(Suit.Pin,5),T(Suit.Pin,6),
				T(Suit.Sou,7),T(Suit.Sou,8),T(Suit.Sou,9),
				T(Suit.Wind,1),T(Suit.Wind,1),
				a,b
			};
		}

		static List<Tile> FourteenWithFloatingDragon()
		{
			var hand = ThreeMeldsPairPlus(T(Suit.Man,2), T(Suit.Man,3));
			hand.Add(T(Suit.Dragon,1));
			return hand;
		}

		static List<Tile> NoTenpaiThirteen()
		{
			return new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,4),T(Suit.Man,7),
				T(Suit.Pin,3),T(Suit.Pin,6),T(Suit.Pin,9),
				T(Suit.Sou,2),T(Suit.Sou,5),T(Suit.Sou,8),
				T(Suit.Wind,1),T(Suit.Wind,3),
				T(Suit.Dragon,1)
			};
		}

		static void RemoveFirstKind(List<Tile> tiles, Tile kind)
		{
			for (int i = 0; i < tiles.Count; i++)
			{
				if (!tiles[i].SameKind(kind))
					continue;
				tiles.RemoveAt(i);
				return;
			}
			Assert.Fail($"Tile kind not found: {kind}");
		}

		static List<Tile> KokushiThirteenKinds()
		{
			return new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,9),
				T(Suit.Pin,1),T(Suit.Pin,9),
				T(Suit.Sou,1),T(Suit.Sou,9),
				T(Suit.Wind,1),T(Suit.Wind,2),T(Suit.Wind,3),T(Suit.Wind,4),
				T(Suit.Dragon,1),T(Suit.Dragon,2),T(Suit.Dragon,3)
			};
		}

		static void AssertContainsWait(MahjongTenpaiResult result, Suit suit, int value)
		{
			Assert.IsTrue(ContainsWait(result, suit, value), $"Expected wait {suit} {value}");
		}

		static bool ContainsWait(MahjongTenpaiResult result, Suit suit, int value)
		{
			int kind = TileIndex.Of(T(suit, value));
			foreach (int wait in result.WaitTileKinds)
				if (wait == kind)
					return true;
			return false;
		}

		static MahjongTenpaiDiscardOption OptionFor(
			IReadOnlyList<MahjongTenpaiDiscardOption> options,
			Suit suit,
			int value)
		{
			int kind = TileIndex.Of(T(suit, value));
			foreach (var option in options)
				if (option.DiscardTileKind == kind)
					return option;
			Assert.Fail($"Discard option not found: {suit} {value}");
			return default;
		}

		static int CountOptionsFor(IReadOnlyList<MahjongTenpaiDiscardOption> options, Suit suit, int value)
		{
			int kind = TileIndex.Of(T(suit, value));
			int count = 0;
			foreach (var option in options)
				if (option.DiscardTileKind == kind)
					count++;
			return count;
		}

		static int DistinctKindCount(IReadOnlyList<Tile> tiles)
		{
			var kinds = new HashSet<int>();
			foreach (var tile in tiles)
				kinds.Add(TileIndex.Of(tile));
			return kinds.Count;
		}

		static MahjongTenpaiDiscardOption RiichiOption(Suit suit, int value, bool tenpai)
		{
			int discardKind = TileIndex.Of(T(suit, value));
			var waits = tenpai
				? new List<int> { TileIndex.Of(T(Suit.Dragon, 1)) }
				: new List<int>();
			return new MahjongTenpaiDiscardOption(discardKind, new MahjongTenpaiResult(waits));
		}
	}
}
