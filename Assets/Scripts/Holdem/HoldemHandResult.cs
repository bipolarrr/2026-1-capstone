using System;
using System.Collections.Generic;

namespace Holdem
{
	public sealed class HoldemHandResult : IComparable<HoldemHandResult>
	{
		readonly List<HoldemRank> tieBreakers;
		readonly List<HoldemCard> cards;

		public HoldemHandRank Rank { get; }
		public IReadOnlyList<HoldemRank> TieBreakers => tieBreakers;
		public IReadOnlyList<HoldemCard> Cards => cards;
		public HoldemRank PrimaryRank => tieBreakers.Count > 0 ? tieBreakers[0] : HoldemRank.Two;
		public string DisplayName => HoldemHandRankNames.DisplayName(Rank);
		public string KoreanName => HoldemHandRankNames.KoreanName(Rank);

		public HoldemHandResult(HoldemHandRank rank, IEnumerable<HoldemRank> tieBreakers,
			IEnumerable<HoldemCard> cards)
		{
			Rank = rank;
			this.tieBreakers = tieBreakers != null
				? new List<HoldemRank>(tieBreakers)
				: new List<HoldemRank>();
			this.cards = cards != null
				? new List<HoldemCard>(cards)
				: new List<HoldemCard>();
			this.cards.Sort((a, b) => b.CompareTo(a));
		}

		public int CompareTo(HoldemHandResult other)
		{
			if (other == null)
				return 1;

			int rankCompare = Rank.CompareTo(other.Rank);
			if (rankCompare != 0)
				return rankCompare;

			int count = Math.Max(tieBreakers.Count, other.tieBreakers.Count);
			for (int i = 0; i < count; i++)
			{
				int left = i < tieBreakers.Count ? (int)tieBreakers[i] : 0;
				int right = i < other.tieBreakers.Count ? (int)other.tieBreakers[i] : 0;
				int cmp = left.CompareTo(right);
				if (cmp != 0)
					return cmp;
			}

			int cardCount = Math.Max(cards.Count, other.cards.Count);
			for (int i = 0; i < cardCount; i++)
			{
				int left = i < cards.Count ? (int)cards[i].Rank * 10 + (int)cards[i].Suit : 0;
				int right = i < other.cards.Count ? (int)other.cards[i].Rank * 10 + (int)other.cards[i].Suit : 0;
				int cmp = left.CompareTo(right);
				if (cmp != 0)
					return cmp;
			}

			return 0;
		}
	}

	public static class HoldemHandRankNames
	{
		public static string DisplayName(HoldemHandRank rank)
		{
			switch (rank)
			{
				case HoldemHandRank.RoyalFlush: return "Royal Flush";
				case HoldemHandRank.StraightFlush: return "Straight Flush";
				case HoldemHandRank.FourOfAKind: return "Four of a Kind";
				case HoldemHandRank.FullHouse: return "Full House";
				case HoldemHandRank.Flush: return "Flush";
				case HoldemHandRank.Straight: return "Straight";
				case HoldemHandRank.ThreeOfAKind: return "Three of a Kind";
				case HoldemHandRank.TwoPair: return "Two Pair";
				case HoldemHandRank.OnePair: return "One Pair";
				default: return "High Card";
			}
		}

		public static string KoreanName(HoldemHandRank rank)
		{
			switch (rank)
			{
				case HoldemHandRank.RoyalFlush: return "로열 플러시";
				case HoldemHandRank.StraightFlush: return "스트레이트 플러시";
				case HoldemHandRank.FourOfAKind: return "포카드";
				case HoldemHandRank.FullHouse: return "풀하우스";
				case HoldemHandRank.Flush: return "플러시";
				case HoldemHandRank.Straight: return "스트레이트";
				case HoldemHandRank.ThreeOfAKind: return "트리플";
				case HoldemHandRank.TwoPair: return "투페어";
				case HoldemHandRank.OnePair: return "원페어";
				default: return "하이카드";
			}
		}
	}
}
