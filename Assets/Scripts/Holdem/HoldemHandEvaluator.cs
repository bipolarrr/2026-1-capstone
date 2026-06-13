using System;
using System.Collections.Generic;

namespace Holdem
{
	public static class HoldemHandEvaluator
	{
		readonly struct RankGroup
		{
			public readonly HoldemRank Rank;
			public readonly int Count;

			public RankGroup(HoldemRank rank, int count)
			{
				Rank = rank;
				Count = count;
			}
		}

		public static HoldemHandResult Evaluate(IReadOnlyList<HoldemCard> cards)
		{
			if (cards == null)
				throw new ArgumentNullException(nameof(cards));
			if (cards.Count < 5)
				throw new ArgumentException("At least five cards are required for full Hold'em evaluation.", nameof(cards));
			if (cards.Count == 5)
				return EvaluateFive(cards);

			HoldemHandResult best = null;
			int n = cards.Count;
			for (int a = 0; a < n - 4; a++)
			for (int b = a + 1; b < n - 3; b++)
			for (int c = b + 1; c < n - 2; c++)
			for (int d = c + 1; d < n - 1; d++)
			for (int e = d + 1; e < n; e++)
			{
				var five = new[]
				{
					cards[a], cards[b], cards[c], cards[d], cards[e],
				};
				var result = EvaluateFive(five);
				if (best == null || result.CompareTo(best) > 0)
					best = result;
			}

			return best;
		}

		static HoldemHandResult EvaluateFive(IReadOnlyList<HoldemCard> cards)
		{
			bool flush = IsFlush(cards);
			bool straight = TryGetStraightHigh(cards, out HoldemRank straightHigh);
			if (flush && straight)
			{
				if (straightHigh == HoldemRank.Ace && ContainsRank(cards, HoldemRank.Ten))
					return Result(HoldemHandRank.RoyalFlush, new[] { HoldemRank.Ace }, cards);
				return Result(HoldemHandRank.StraightFlush, new[] { straightHigh }, cards);
			}

			var groups = BuildRankGroups(cards);
			if (groups[0].Count == 4)
			{
				var kickers = new List<HoldemRank> { groups[0].Rank };
				kickers.Add(HighestRankExcept(cards, groups[0].Rank));
				return Result(HoldemHandRank.FourOfAKind, kickers, cards);
			}

			if (groups[0].Count == 3 && groups.Count > 1 && groups[1].Count == 2)
				return Result(HoldemHandRank.FullHouse, new[] { groups[0].Rank, groups[1].Rank }, cards);

			if (flush)
				return Result(HoldemHandRank.Flush, SortedRanksDescending(cards), cards);

			if (straight)
				return Result(HoldemHandRank.Straight, new[] { straightHigh }, cards);

			if (groups[0].Count == 3)
			{
				var tie = new List<HoldemRank> { groups[0].Rank };
				tie.AddRange(SortedRanksExceptDescending(cards, groups[0].Rank));
				return Result(HoldemHandRank.ThreeOfAKind, tie, cards);
			}

			if (groups[0].Count == 2 && groups.Count > 1 && groups[1].Count == 2)
			{
				HoldemRank highPair = groups[0].Rank > groups[1].Rank ? groups[0].Rank : groups[1].Rank;
				HoldemRank lowPair = groups[0].Rank > groups[1].Rank ? groups[1].Rank : groups[0].Rank;
				var tie = new List<HoldemRank> { highPair, lowPair };
				tie.Add(HighestRankExcept(cards, highPair, lowPair));
				return Result(HoldemHandRank.TwoPair, tie, cards);
			}

			if (groups[0].Count == 2)
			{
				var tie = new List<HoldemRank> { groups[0].Rank };
				tie.AddRange(SortedRanksExceptDescending(cards, groups[0].Rank));
				return Result(HoldemHandRank.OnePair, tie, cards);
			}

			return Result(HoldemHandRank.HighCard, SortedRanksDescending(cards), cards);
		}

		static HoldemHandResult Result(HoldemHandRank rank, IEnumerable<HoldemRank> tieBreakers,
			IEnumerable<HoldemCard> cards)
		{
			return new HoldemHandResult(rank, tieBreakers, cards);
		}

		static bool IsFlush(IReadOnlyList<HoldemCard> cards)
		{
			HoldemSuit suit = cards[0].Suit;
			for (int i = 1; i < cards.Count; i++)
			{
				if (cards[i].Suit != suit)
					return false;
			}
			return true;
		}

		static bool TryGetStraightHigh(IReadOnlyList<HoldemCard> cards, out HoldemRank high)
		{
			var present = new bool[15];
			for (int i = 0; i < cards.Count; i++)
				present[(int)cards[i].Rank] = true;

			for (int candidate = (int)HoldemRank.Ace; candidate >= (int)HoldemRank.Six; candidate--)
			{
				bool ok = true;
				for (int offset = 0; offset < 5; offset++)
					ok &= present[candidate - offset];
				if (ok)
				{
					high = (HoldemRank)candidate;
					return true;
				}
			}

			if (present[(int)HoldemRank.Ace]
				&& present[(int)HoldemRank.Two]
				&& present[(int)HoldemRank.Three]
				&& present[(int)HoldemRank.Four]
				&& present[(int)HoldemRank.Five])
			{
				high = HoldemRank.Five;
				return true;
			}

			high = HoldemRank.Two;
			return false;
		}

		static bool ContainsRank(IReadOnlyList<HoldemCard> cards, HoldemRank rank)
		{
			for (int i = 0; i < cards.Count; i++)
			{
				if (cards[i].Rank == rank)
					return true;
			}
			return false;
		}

		static List<RankGroup> BuildRankGroups(IReadOnlyList<HoldemCard> cards)
		{
			var counts = new Dictionary<HoldemRank, int>();
			for (int i = 0; i < cards.Count; i++)
			{
				if (!counts.ContainsKey(cards[i].Rank))
					counts[cards[i].Rank] = 0;
				counts[cards[i].Rank]++;
			}

			var groups = new List<RankGroup>();
			foreach (var pair in counts)
				groups.Add(new RankGroup(pair.Key, pair.Value));
			groups.Sort((a, b) =>
			{
				int countCompare = b.Count.CompareTo(a.Count);
				return countCompare != 0 ? countCompare : b.Rank.CompareTo(a.Rank);
			});
			return groups;
		}

		static List<HoldemRank> SortedRanksDescending(IReadOnlyList<HoldemCard> cards)
		{
			var ranks = new List<HoldemRank>(cards.Count);
			for (int i = 0; i < cards.Count; i++)
				ranks.Add(cards[i].Rank);
			ranks.Sort((a, b) => b.CompareTo(a));
			return ranks;
		}

		static List<HoldemRank> SortedRanksExceptDescending(IReadOnlyList<HoldemCard> cards,
			params HoldemRank[] excluded)
		{
			var ranks = new List<HoldemRank>();
			for (int i = 0; i < cards.Count; i++)
			{
				if (Contains(excluded, cards[i].Rank))
					continue;
				ranks.Add(cards[i].Rank);
			}
			ranks.Sort((a, b) => b.CompareTo(a));
			return ranks;
		}

		static HoldemRank HighestRankExcept(IReadOnlyList<HoldemCard> cards,
			params HoldemRank[] excluded)
		{
			HoldemRank highest = HoldemRank.Two;
			bool found = false;
			for (int i = 0; i < cards.Count; i++)
			{
				if (Contains(excluded, cards[i].Rank))
					continue;
				if (!found || cards[i].Rank > highest)
					highest = cards[i].Rank;
				found = true;
			}
			return highest;
		}

		static bool Contains(HoldemRank[] values, HoldemRank value)
		{
			if (values == null)
				return false;
			for (int i = 0; i < values.Length; i++)
			{
				if (values[i] == value)
					return true;
			}
			return false;
		}
	}
}
