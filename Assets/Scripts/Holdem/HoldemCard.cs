using System;

namespace Holdem
{
	[Serializable]
	public readonly struct HoldemCard : IEquatable<HoldemCard>, IComparable<HoldemCard>
	{
		public readonly HoldemRank Rank;
		public readonly HoldemSuit Suit;

		public HoldemCard(HoldemRank rank, HoldemSuit suit)
		{
			Rank = rank;
			Suit = suit;
		}

		public int CompareTo(HoldemCard other)
		{
			int rankCompare = Rank.CompareTo(other.Rank);
			return rankCompare != 0 ? rankCompare : Suit.CompareTo(other.Suit);
		}

		public bool Equals(HoldemCard other)
		{
			return Rank == other.Rank && Suit == other.Suit;
		}

		public override bool Equals(object obj)
		{
			return obj is HoldemCard other && Equals(other);
		}

		public override int GetHashCode()
		{
			return ((int)Rank * 397) ^ (int)Suit;
		}

		public override string ToString()
		{
			return ToDisplayString();
		}

		public string ToDisplayString()
		{
			return $"{RankLabel(Rank)}{SuitSymbol(Suit)}";
		}

		public static string RankLabel(HoldemRank rank)
		{
			switch (rank)
			{
				case HoldemRank.Ace: return "A";
				case HoldemRank.King: return "K";
				case HoldemRank.Queen: return "Q";
				case HoldemRank.Jack: return "J";
				case HoldemRank.Ten: return "10";
				default: return ((int)rank).ToString();
			}
		}

		public static string SuitSymbol(HoldemSuit suit)
		{
			switch (suit)
			{
				case HoldemSuit.Clubs: return "♣";
				case HoldemSuit.Diamonds: return "♦";
				case HoldemSuit.Hearts: return "♥";
				case HoldemSuit.Spades: return "♠";
				default: return "?";
			}
		}

		public static bool operator ==(HoldemCard left, HoldemCard right) => left.Equals(right);
		public static bool operator !=(HoldemCard left, HoldemCard right) => !left.Equals(right);
	}
}
