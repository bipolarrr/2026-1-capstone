using System;
using System.Collections.Generic;

namespace Holdem
{
	public readonly struct HoldemDefenseChallenge
	{
		public readonly HoldemCard EnemyAttackCard;
		public readonly IReadOnlyList<HoldemCard> DefenseCards;

		public HoldemDefenseChallenge(HoldemCard enemyAttackCard, IReadOnlyList<HoldemCard> defenseCards)
		{
			EnemyAttackCard = enemyAttackCard;
			DefenseCards = defenseCards;
		}
	}

	public readonly struct HoldemDefenseResult
	{
		public readonly HoldemCard EnemyAttackCard;
		public readonly HoldemCard ChosenDefenseCard;
		public readonly bool Blocked;

		public HoldemDefenseResult(HoldemCard enemyAttackCard, HoldemCard chosenDefenseCard, bool blocked)
		{
			EnemyAttackCard = enemyAttackCard;
			ChosenDefenseCard = chosenDefenseCard;
			Blocked = blocked;
		}
	}

	public static class HoldemDefenseResolver
	{
		public static HoldemDefenseChallenge GenerateChallenge(int enemyRank, Random random)
		{
			if (random == null)
				throw new ArgumentNullException(nameof(random));

			var enemyAttack = GenerateEnemyAttackCard(enemyRank, random);
			var defenseCards = GenerateDefenseCards(enemyAttack, random);
			return new HoldemDefenseChallenge(enemyAttack, defenseCards);
		}

		public static HoldemCard GenerateEnemyAttackCard(int enemyRank, Random random)
		{
			if (random == null)
				throw new ArgumentNullException(nameof(random));

			var pool = GetEnemyAttackRankPool(enemyRank);
			HoldemRank rank = pool[random.Next(pool.Count)];
			HoldemSuit suit = (HoldemSuit)random.Next(0, 4);
			return new HoldemCard(rank, suit);
		}

		public static List<HoldemCard> GenerateDefenseCards(HoldemCard enemyAttackCard, Random random)
		{
			if (random == null)
				throw new ArgumentNullException(nameof(random));

			var deck = HoldemDeck.CreateShuffled(random);
			deck.RemoveUndrawn(enemyAttackCard);
			return deck.Draw(5);
		}

		public static HoldemDefenseResult Resolve(HoldemCard enemyAttackCard, HoldemCard chosenDefenseCard)
		{
			return new HoldemDefenseResult(
				enemyAttackCard,
				chosenDefenseCard,
				chosenDefenseCard.Rank >= enemyAttackCard.Rank);
		}

		public static IReadOnlyList<HoldemRank> GetEnemyAttackRankPool(int enemyRank)
		{
			switch (ClampEnemyRank(enemyRank))
			{
				case 1:
					return new[]
					{
						HoldemRank.Two, HoldemRank.Three, HoldemRank.Four, HoldemRank.Five,
						HoldemRank.Six, HoldemRank.Seven, HoldemRank.Eight,
					};
				case 2:
					return new[]
					{
						HoldemRank.Three, HoldemRank.Four, HoldemRank.Five, HoldemRank.Six,
						HoldemRank.Seven, HoldemRank.Eight, HoldemRank.Nine,
					};
				case 3:
					return new[]
					{
						HoldemRank.Four, HoldemRank.Five, HoldemRank.Six, HoldemRank.Seven,
						HoldemRank.Eight, HoldemRank.Nine, HoldemRank.Ten, HoldemRank.Jack,
					};
				case 4:
					return new[]
					{
						HoldemRank.Five, HoldemRank.Six, HoldemRank.Seven, HoldemRank.Eight,
						HoldemRank.Nine, HoldemRank.Ten, HoldemRank.Jack, HoldemRank.Queen,
					};
				default:
					return new[]
					{
						HoldemRank.Six, HoldemRank.Seven, HoldemRank.Eight, HoldemRank.Nine,
						HoldemRank.Ten, HoldemRank.Jack, HoldemRank.Queen, HoldemRank.King,
						HoldemRank.Ace,
					};
			}
		}

		static int ClampEnemyRank(int enemyRank)
		{
			if (enemyRank < 1)
				return 1;
			if (enemyRank > 5)
				return 5;
			return enemyRank;
		}
	}
}
