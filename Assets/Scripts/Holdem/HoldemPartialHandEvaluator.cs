using System;
using System.Collections.Generic;

namespace Holdem
{
	public static class HoldemPartialHandEvaluator
	{
		public static HoldemHandResult Evaluate(IReadOnlyList<HoldemCard> cards)
		{
			if (cards == null)
				throw new ArgumentNullException(nameof(cards));
			if (cards.Count != 3)
				throw new ArgumentException("Stage 1 partial Hold'em evaluation requires exactly three visible cards.", nameof(cards));

			var counts = new Dictionary<HoldemRank, int>();
			for (int i = 0; i < cards.Count; i++)
			{
				if (!counts.ContainsKey(cards[i].Rank))
					counts[cards[i].Rank] = 0;
				counts[cards[i].Rank]++;
			}

			HoldemRank tripRank = HoldemRank.Two;
			HoldemRank pairRank = HoldemRank.Two;
			bool hasTrip = false;
			bool hasPair = false;
			foreach (var pair in counts)
			{
				if (pair.Value == 3)
				{
					tripRank = pair.Key;
					hasTrip = true;
				}
				else if (pair.Value == 2)
				{
					pairRank = pair.Key;
					hasPair = true;
				}
			}

			if (hasTrip)
				return new HoldemHandResult(HoldemHandRank.ThreeOfAKind, new[] { tripRank }, cards);

			if (hasPair)
			{
				HoldemRank kicker = HoldemRank.Two;
				for (int i = 0; i < cards.Count; i++)
				{
					if (cards[i].Rank != pairRank)
						kicker = cards[i].Rank;
				}
				return new HoldemHandResult(HoldemHandRank.OnePair, new[] { pairRank, kicker }, cards);
			}

			var ranks = new List<HoldemRank>(3);
			for (int i = 0; i < cards.Count; i++)
				ranks.Add(cards[i].Rank);
			ranks.Sort((a, b) => b.CompareTo(a));
			return new HoldemHandResult(HoldemHandRank.HighCard, ranks, cards);
		}
	}
}
