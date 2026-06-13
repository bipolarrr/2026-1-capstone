using System;
using System.Collections.Generic;

namespace Holdem
{
	public sealed class HoldemDeck
	{
		readonly List<HoldemCard> cards;
		int nextIndex;

		public int Remaining => cards.Count - nextIndex;

		public HoldemDeck(IEnumerable<HoldemCard> cards)
		{
			if (cards == null)
				throw new ArgumentNullException(nameof(cards));

			this.cards = new List<HoldemCard>(cards);
			nextIndex = 0;
		}

		public static HoldemDeck CreateShuffled(int seed)
		{
			return CreateShuffled(new Random(seed));
		}

		public static HoldemDeck CreateShuffled(Random random)
		{
			if (random == null)
				throw new ArgumentNullException(nameof(random));

			var cards = CreateStandardCards();
			Shuffle(cards, random);
			return new HoldemDeck(cards);
		}

		public static List<HoldemCard> CreateStandardCards()
		{
			var result = new List<HoldemCard>(52);
			for (int suit = (int)HoldemSuit.Clubs; suit <= (int)HoldemSuit.Spades; suit++)
			{
				for (int rank = (int)HoldemRank.Two; rank <= (int)HoldemRank.Ace; rank++)
					result.Add(new HoldemCard((HoldemRank)rank, (HoldemSuit)suit));
			}
			return result;
		}

		public HoldemCard Draw()
		{
			if (nextIndex >= cards.Count)
				throw new InvalidOperationException("Hold'em deck is empty.");

			return cards[nextIndex++];
		}

		public List<HoldemCard> Draw(int count)
		{
			if (count < 0)
				throw new ArgumentOutOfRangeException(nameof(count));

			var result = new List<HoldemCard>(count);
			for (int i = 0; i < count; i++)
				result.Add(Draw());
			return result;
		}

		public bool RemoveUndrawn(HoldemCard card)
		{
			for (int i = nextIndex; i < cards.Count; i++)
			{
				if (cards[i] != card)
					continue;

				cards.RemoveAt(i);
				return true;
			}
			return false;
		}

		static void Shuffle(List<HoldemCard> cards, Random random)
		{
			for (int i = cards.Count - 1; i > 0; i--)
			{
				int j = random.Next(i + 1);
				var temp = cards[i];
				cards[i] = cards[j];
				cards[j] = temp;
			}
		}
	}
}
