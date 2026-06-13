using System;
using System.Collections.Generic;
using NUnit.Framework;
using Holdem;
using Holdem.UI;
using UnityEngine;

namespace HoldemTests
{
	public class HoldemDefenseResolverTests
	{
		static HoldemCard C(HoldemRank rank, HoldemSuit suit) => new HoldemCard(rank, suit);

		[Test]
		public void ChosenCardEqualToEnemyCard_Blocks()
		{
			var result = HoldemDefenseResolver.Resolve(
				C(HoldemRank.Nine, HoldemSuit.Clubs),
				C(HoldemRank.Nine, HoldemSuit.Hearts));

			Assert.IsTrue(result.Blocked);
		}

		[Test]
		public void ChosenCardHigherThanEnemyCard_Blocks()
		{
			var result = HoldemDefenseResolver.Resolve(
				C(HoldemRank.Nine, HoldemSuit.Clubs),
				C(HoldemRank.Queen, HoldemSuit.Hearts));

			Assert.IsTrue(result.Blocked);
		}

		[Test]
		public void ChosenCardLowerThanEnemyCard_Fails()
		{
			var result = HoldemDefenseResolver.Resolve(
				C(HoldemRank.Jack, HoldemSuit.Clubs),
				C(HoldemRank.Four, HoldemSuit.Hearts));

			Assert.IsFalse(result.Blocked);
		}

		[Test]
		public void EnemyCard_IsExcludedFromDefenseCardPool()
		{
			var enemy = C(HoldemRank.Ace, HoldemSuit.Spades);
			var cards = HoldemDefenseResolver.GenerateDefenseCards(enemy, new System.Random(21));

			CollectionAssert.DoesNotContain(cards, enemy);
		}

		[Test]
		public void FiveDefenseCards_AreUnique()
		{
			var cards = HoldemDefenseResolver.GenerateDefenseCards(
				C(HoldemRank.Ace, HoldemSuit.Spades),
				new System.Random(22));
			var seen = new HashSet<HoldemCard>();

			Assert.AreEqual(5, cards.Count);
			foreach (var card in cards)
				Assert.IsTrue(seen.Add(card), $"Duplicate card: {card}");
		}

		[Test]
		public void RankPoolPerEnemyRank_MatchesSpecification()
		{
			CollectionAssert.AreEqual(
				new[] { HoldemRank.Two, HoldemRank.Three, HoldemRank.Four, HoldemRank.Five, HoldemRank.Six, HoldemRank.Seven, HoldemRank.Eight },
				HoldemDefenseResolver.GetEnemyAttackRankPool(1));
			CollectionAssert.AreEqual(
				new[] { HoldemRank.Three, HoldemRank.Four, HoldemRank.Five, HoldemRank.Six, HoldemRank.Seven, HoldemRank.Eight, HoldemRank.Nine },
				HoldemDefenseResolver.GetEnemyAttackRankPool(2));
			CollectionAssert.AreEqual(
				new[] { HoldemRank.Four, HoldemRank.Five, HoldemRank.Six, HoldemRank.Seven, HoldemRank.Eight, HoldemRank.Nine, HoldemRank.Ten, HoldemRank.Jack },
				HoldemDefenseResolver.GetEnemyAttackRankPool(3));
			CollectionAssert.AreEqual(
				new[] { HoldemRank.Five, HoldemRank.Six, HoldemRank.Seven, HoldemRank.Eight, HoldemRank.Nine, HoldemRank.Ten, HoldemRank.Jack, HoldemRank.Queen },
				HoldemDefenseResolver.GetEnemyAttackRankPool(4));
			CollectionAssert.AreEqual(
				new[] { HoldemRank.Six, HoldemRank.Seven, HoldemRank.Eight, HoldemRank.Nine, HoldemRank.Ten, HoldemRank.Jack, HoldemRank.Queen, HoldemRank.King, HoldemRank.Ace },
				HoldemDefenseResolver.GetEnemyAttackRankPool(5));
		}

		[Test]
		public void GeneratedEnemyAttackCard_StaysWithinRankBand()
		{
			var random = new System.Random(23);

			for (int enemyRank = 1; enemyRank <= 5; enemyRank++)
			{
				var pool = new HashSet<HoldemRank>(HoldemDefenseResolver.GetEnemyAttackRankPool(enemyRank));
				for (int i = 0; i < 100; i++)
				{
					var card = HoldemDefenseResolver.GenerateEnemyAttackCard(enemyRank, random);
					Assert.IsTrue(pool.Contains(card.Rank), $"Enemy rank {enemyRank} generated {card}");
				}
			}
		}

		[Test]
		public void DefenseBlocked_SuppressesEnemyAttackPresentation()
		{
			Assert.IsFalse(HoldemBattleController.ShouldPlayEnemyAttackPresentation(true));
			Assert.IsTrue(HoldemBattleController.ShouldPlayEnemyAttackPresentation(false));
		}

		[Test]
		public void CardDisplaySprite_CropsOnlyTopSourcePixels()
		{
			var texture = new Texture2D(12, 16, TextureFormat.RGBA32, false);
			var sprite = Sprite.Create(
				texture,
				new Rect(0f, 0f, 12f, 16f),
				new Vector2(0.5f, 0.5f),
				100f,
				0,
				SpriteMeshType.FullRect);
			Sprite cropped = null;

			try
			{
				cropped = HoldemCardView.GetDisplaySprite(sprite);

				Assert.AreEqual(12f, cropped.rect.width);
				Assert.AreEqual(12f, cropped.rect.height);
				Assert.AreEqual(0f, cropped.rect.x);
				Assert.AreEqual(0f, cropped.rect.y);
				Assert.AreSame(cropped, HoldemCardView.GetDisplaySprite(sprite));
			}
			finally
			{
				if (cropped != null && cropped != sprite)
					UnityEngine.Object.DestroyImmediate(cropped);
				UnityEngine.Object.DestroyImmediate(sprite);
				UnityEngine.Object.DestroyImmediate(texture);
			}
		}
	}
}
