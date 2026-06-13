using System;
using System.Collections.Generic;

namespace Holdem
{
	public enum HoldemRevealStage
	{
		Stage1 = 1,
		Stage2 = 2,
		Stage3 = 3,
	}

	public readonly struct HoldemTurnAdvanceResult
	{
		public readonly bool Advanced;
		public readonly int PreviousRevealedCommunityCount;
		public readonly int RevealedCommunityCount;
		public readonly bool ReplacedCommunity;

		public bool RevealedAdditionalCards =>
			Advanced && !ReplacedCommunity && RevealedCommunityCount > PreviousRevealedCommunityCount;

		public HoldemTurnAdvanceResult(
			bool advanced,
			int previousRevealedCommunityCount,
			int revealedCommunityCount,
			bool replacedCommunity)
		{
			Advanced = advanced;
			PreviousRevealedCommunityCount = previousRevealedCommunityCount;
			RevealedCommunityCount = revealedCommunityCount;
			ReplacedCommunity = replacedCommunity;
		}

		public static HoldemTurnAdvanceResult NoChange(int revealedCommunityCount)
		{
			return new HoldemTurnAdvanceResult(false, revealedCommunityCount, revealedCommunityCount, false);
		}
	}

	public sealed class HoldemRoundState
	{
		const int HoleCardCount = 2;
		const int CommunityCardCount = 5;

		readonly Random random;
		HoldemDeck deck;
		int revealedCommunityCount;

		public HoldemCard[] HoleCards { get; } = new HoldemCard[HoleCardCount];
		public HoldemCard[] CommunityCards { get; } = new HoldemCard[CommunityCardCount];
		public int[] HoleRedrawsRemaining { get; } = new int[HoleCardCount];
		public int CommunityRedrawsRemaining { get; private set; }
		public bool AttackConfirmed { get; private set; }
		public int RevealedCommunityCount => revealedCommunityCount;
		public int StageNumber => (int)Stage;

		public HoldemRevealStage Stage
		{
			get
			{
				if (revealedCommunityCount <= 1)
					return HoldemRevealStage.Stage1;
				if (revealedCommunityCount <= 3)
					return HoldemRevealStage.Stage2;
				return HoldemRevealStage.Stage3;
			}
		}

		public HoldemRoundState()
			: this(Environment.TickCount)
		{
		}

		public HoldemRoundState(int seed)
		{
			random = new Random(seed);
			StartNewAttackRound();
		}

		public void StartNewAttackRound()
		{
			deck = HoldemDeck.CreateShuffled(random);
			for (int i = 0; i < HoleCardCount; i++)
				HoleCards[i] = deck.Draw();

			DealCommunityCards();
			ResetTurnActionLimits();
			revealedCommunityCount = 1;
			AttackConfirmed = false;
		}

		public IReadOnlyList<HoldemCard> GetVisibleCards()
		{
			var visible = new List<HoldemCard>(HoleCardCount + RevealedCommunityCount);
			visible.Add(HoleCards[0]);
			visible.Add(HoleCards[1]);
			for (int i = 0; i < RevealedCommunityCount; i++)
				visible.Add(CommunityCards[i]);
			return visible;
		}

		public HoldemHandResult EvaluateVisibleHand()
		{
			var visible = GetVisibleCards();
			return Stage == HoldemRevealStage.Stage1
				? HoldemPartialHandEvaluator.Evaluate(visible)
				: HoldemHandEvaluator.Evaluate(visible);
		}

		public HoldemTurnAdvanceResult BeginNextPlayerAttackTurn()
		{
			if (!AttackConfirmed)
				return HoldemTurnAdvanceResult.NoChange(RevealedCommunityCount);

			int previousRevealed = RevealedCommunityCount;
			bool replacedCommunity = false;

			if (revealedCommunityCount >= CommunityCardCount)
			{
				ReplaceCommunityCardsKeepingHoleCards();
				replacedCommunity = true;
			}
			else
			{
				revealedCommunityCount = Math.Min(CommunityCardCount, revealedCommunityCount + 2);
			}

			ResetTurnActionLimits();
			AttackConfirmed = false;
			return new HoldemTurnAdvanceResult(
				true,
				previousRevealed,
				RevealedCommunityCount,
				replacedCommunity);
		}

		public bool RedrawHoleCard(int index)
		{
			if (AttackConfirmed)
				return false;
			if (index < 0 || index >= HoleCardCount)
				return false;
			if (HoleRedrawsRemaining[index] <= 0)
				return false;

			HoleCards[index] = deck.Draw();
			HoleRedrawsRemaining[index]--;
			return true;
		}

		public bool RedrawCommunity()
		{
			if (AttackConfirmed)
				return false;
			if (CommunityRedrawsRemaining <= 0)
				return false;

			for (int i = 0; i < CommunityCardCount; i++)
				CommunityCards[i] = deck.Draw();
			CommunityRedrawsRemaining--;
			return true;
		}

		public void ConfirmAttack()
		{
			AttackConfirmed = true;
		}

		void DealCommunityCards()
		{
			for (int i = 0; i < CommunityCardCount; i++)
				CommunityCards[i] = deck.Draw();
		}

		void ReplaceCommunityCardsKeepingHoleCards()
		{
			deck = HoldemDeck.CreateShuffled(random);
			for (int i = 0; i < HoleCardCount; i++)
				deck.RemoveUndrawn(HoleCards[i]);

			DealCommunityCards();
			revealedCommunityCount = CommunityCardCount;
		}

		void ResetTurnActionLimits()
		{
			HoleRedrawsRemaining[0] = 2;
			HoleRedrawsRemaining[1] = 2;
			CommunityRedrawsRemaining = 1;
		}
	}
}
