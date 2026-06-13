using System.Collections.Generic;
using NUnit.Framework;
using Holdem;

namespace HoldemTests
{
	public class HoldemRoundStateTests
	{
		[Test]
		public void InitialStage_RevealsExactlyOneCommunityCard()
		{
			var state = new HoldemRoundState(10);

			Assert.AreEqual(HoldemRevealStage.Stage1, state.Stage);
			Assert.AreEqual(1, state.RevealedCommunityCount);
			Assert.AreEqual(3, state.GetVisibleCards().Count);
		}

		[Test]
		public void NextTurnAfterConfirmedAttack_RevealsExactlyThreeCommunityCards()
		{
			var state = new HoldemRoundState(11);

			state.ConfirmAttack();
			var result = state.BeginNextPlayerAttackTurn();

			Assert.IsTrue(result.Advanced);
			Assert.IsTrue(result.RevealedAdditionalCards);
			Assert.IsFalse(result.ReplacedCommunity);
			Assert.AreEqual(1, result.PreviousRevealedCommunityCount);
			Assert.AreEqual(3, result.RevealedCommunityCount);
			Assert.AreEqual(HoldemRevealStage.Stage2, state.Stage);
			Assert.AreEqual(3, state.RevealedCommunityCount);
			Assert.AreEqual(5, state.GetVisibleCards().Count);
		}

		[Test]
		public void SecondNextTurnAfterConfirmedAttack_RevealsExactlyFiveCommunityCards()
		{
			var state = new HoldemRoundState(12);

			state.ConfirmAttack();
			state.BeginNextPlayerAttackTurn();
			state.ConfirmAttack();
			var result = state.BeginNextPlayerAttackTurn();

			Assert.IsTrue(result.Advanced);
			Assert.IsTrue(result.RevealedAdditionalCards);
			Assert.IsFalse(result.ReplacedCommunity);
			Assert.AreEqual(3, result.PreviousRevealedCommunityCount);
			Assert.AreEqual(5, result.RevealedCommunityCount);
			Assert.AreEqual(HoldemRevealStage.Stage3, state.Stage);
			Assert.AreEqual(5, state.RevealedCommunityCount);
			Assert.AreEqual(7, state.GetVisibleCards().Count);
		}

		[Test]
		public void BeginNextPlayerAttackTurn_WithoutConfirmedAttack_NoOps()
		{
			var state = new HoldemRoundState(13);

			var result = state.BeginNextPlayerAttackTurn();

			Assert.IsFalse(result.Advanced);
			Assert.AreEqual(HoldemRevealStage.Stage1, state.Stage);
			Assert.AreEqual(1, state.RevealedCommunityCount);
		}

		[Test]
		public void HoleCard0_CanRedrawExactlyTwoTimes()
		{
			var state = new HoldemRoundState(14);

			Assert.IsTrue(state.RedrawHoleCard(0));
			Assert.IsTrue(state.RedrawHoleCard(0));
			Assert.IsFalse(state.RedrawHoleCard(0));
			Assert.AreEqual(0, state.HoleRedrawsRemaining[0]);
		}

		[Test]
		public void HoleCard1_CanRedrawExactlyTwoTimesIndependently()
		{
			var state = new HoldemRoundState(15);

			Assert.IsTrue(state.RedrawHoleCard(0));
			Assert.IsTrue(state.RedrawHoleCard(1));
			Assert.IsTrue(state.RedrawHoleCard(1));

			Assert.AreEqual(1, state.HoleRedrawsRemaining[0]);
			Assert.AreEqual(0, state.HoleRedrawsRemaining[1]);
			Assert.IsFalse(state.RedrawHoleCard(1));
		}

		[Test]
		public void CommunityRedraw_CanBeUsedExactlyOnce()
		{
			var state = new HoldemRoundState(16);

			Assert.IsTrue(state.RedrawCommunity());
			Assert.IsFalse(state.RedrawCommunity());
			Assert.AreEqual(0, state.CommunityRedrawsRemaining);
		}

		[Test]
		public void CommunityRedraw_PreservesHoleCards()
		{
			var state = new HoldemRoundState(17);
			var first = state.HoleCards[0];
			var second = state.HoleCards[1];

			state.RedrawCommunity();

			Assert.AreEqual(first, state.HoleCards[0]);
			Assert.AreEqual(second, state.HoleCards[1]);
		}

		[Test]
		public void RedrawnAwayCards_DoNotReappearInSameRound()
		{
			var state = new HoldemRoundState(18);
			var excluded = new HashSet<HoldemCard>
			{
				state.HoleCards[0],
				state.CommunityCards[0],
				state.CommunityCards[1],
				state.CommunityCards[2],
				state.CommunityCards[3],
				state.CommunityCards[4],
			};

			state.RedrawHoleCard(0);
			state.RedrawCommunity();

			Assert.IsFalse(excluded.Contains(state.HoleCards[0]));
			for (int i = 0; i < state.CommunityCards.Length; i++)
				Assert.IsFalse(excluded.Contains(state.CommunityCards[i]));
		}

		[Test]
		public void VisibleCards_HaveNoDuplicates()
		{
			var state = new HoldemRoundState(19);
			state.RedrawHoleCard(0);
			state.RedrawHoleCard(1);
			state.RedrawCommunity();
			state.ConfirmAttack();
			state.BeginNextPlayerAttackTurn();
			state.ConfirmAttack();
			state.BeginNextPlayerAttackTurn();

			var seen = new HashSet<HoldemCard>();
			foreach (var card in state.GetVisibleCards())
				Assert.IsTrue(seen.Add(card), $"Duplicate card: {card}");
		}

		[Test]
		public void AttackConfirmation_DisablesFurtherRedrawUntilNextTurn()
		{
			var state = new HoldemRoundState(20);

			state.ConfirmAttack();

			Assert.IsFalse(state.RedrawHoleCard(0));
			Assert.IsFalse(state.RedrawCommunity());
			Assert.IsTrue(state.BeginNextPlayerAttackTurn().Advanced);
			Assert.IsFalse(state.AttackConfirmed);
		}

		[Test]
		public void FullCommunityNextTurn_ReplacesCommunityAndKeepsHoleCards()
		{
			var state = new HoldemRoundState(21);
			state.ConfirmAttack();
			state.BeginNextPlayerAttackTurn();
			state.ConfirmAttack();
			state.BeginNextPlayerAttackTurn();

			var firstHole = state.HoleCards[0];
			var secondHole = state.HoleCards[1];
			var previousCommunity = new HoldemCard[state.CommunityCards.Length];
			for (int i = 0; i < previousCommunity.Length; i++)
				previousCommunity[i] = state.CommunityCards[i];

			state.ConfirmAttack();
			var result = state.BeginNextPlayerAttackTurn();

			Assert.IsTrue(result.Advanced);
			Assert.IsFalse(result.RevealedAdditionalCards);
			Assert.IsTrue(result.ReplacedCommunity);
			Assert.AreEqual(5, result.PreviousRevealedCommunityCount);
			Assert.AreEqual(5, result.RevealedCommunityCount);
			Assert.AreEqual(firstHole, state.HoleCards[0]);
			Assert.AreEqual(secondHole, state.HoleCards[1]);
			Assert.AreEqual(HoldemRevealStage.Stage3, state.Stage);
			Assert.AreEqual(7, state.GetVisibleCards().Count);
			Assert.IsFalse(SameCards(previousCommunity, state.CommunityCards));
		}

		[Test]
		public void FullCommunityReplacement_HasNoDuplicateVisibleCards()
		{
			var state = new HoldemRoundState(22);
			state.ConfirmAttack();
			state.BeginNextPlayerAttackTurn();
			state.ConfirmAttack();
			state.BeginNextPlayerAttackTurn();
			state.ConfirmAttack();
			state.BeginNextPlayerAttackTurn();

			var seen = new HashSet<HoldemCard>();
			foreach (var card in state.GetVisibleCards())
				Assert.IsTrue(seen.Add(card), $"Duplicate card: {card}");
		}

		[Test]
		public void NextPlayerAttackTurn_ResetsPerTurnRedraws()
		{
			var state = new HoldemRoundState(23);
			state.RedrawHoleCard(0);
			state.RedrawHoleCard(0);
			state.RedrawCommunity();

			Assert.AreEqual(0, state.HoleRedrawsRemaining[0]);
			Assert.AreEqual(0, state.CommunityRedrawsRemaining);

			state.ConfirmAttack();
			state.BeginNextPlayerAttackTurn();

			Assert.AreEqual(2, state.HoleRedrawsRemaining[0]);
			Assert.AreEqual(2, state.HoleRedrawsRemaining[1]);
			Assert.AreEqual(1, state.CommunityRedrawsRemaining);
		}

		static bool SameCards(HoldemCard[] first, HoldemCard[] second)
		{
			if (first == null || second == null || first.Length != second.Length)
				return false;
			for (int i = 0; i < first.Length; i++)
			{
				if (first[i] != second[i])
					return false;
			}
			return true;
		}
	}
}
