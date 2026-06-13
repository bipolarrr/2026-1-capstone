using System;

namespace Holdem
{
	public enum HoldemDamageTargetMode
	{
		SingleTarget,
		Aoe,
	}

	public readonly struct HoldemDamagePreview
	{
		public readonly int Damage;
		public readonly float StageMultiplier;
		public readonly HoldemDamageTargetMode TargetMode;

		public bool IsAoe => TargetMode == HoldemDamageTargetMode.Aoe;

		public HoldemDamagePreview(int damage, float stageMultiplier, HoldemDamageTargetMode targetMode)
		{
			Damage = damage;
			StageMultiplier = stageMultiplier;
			TargetMode = targetMode;
		}
	}

	public static class HoldemDamageTable
	{
		public const float Stage1Multiplier = 2.4f;
		public const float Stage2Multiplier = 1.6f;
		public const float Stage3Multiplier = 1.0f;

		public const float DebugStage1Multiplier = 3.0f;
		public const float DebugStage2Multiplier = 2.0f;
		public const float DebugStage3Multiplier = 1.0f;

		public const int DamageCap = 80;

		public static float GetStageMultiplier(int stage)
		{
			switch (stage)
			{
				case 1: return Stage1Multiplier;
				case 2: return Stage2Multiplier;
				case 3: return Stage3Multiplier;
				default: throw new ArgumentOutOfRangeException(nameof(stage), "Hold'em stage must be 1, 2, or 3.");
			}
		}

		public static bool IsAoe(HoldemHandRank rank)
		{
			return rank >= HoldemHandRank.OnePair;
		}

		public static HoldemDamageTargetMode GetTargetMode(HoldemHandRank rank)
		{
			return IsAoe(rank) ? HoldemDamageTargetMode.Aoe : HoldemDamageTargetMode.SingleTarget;
		}

		public static int GetBaseHandDamage(HoldemHandRank rank)
		{
			switch (rank)
			{
				case HoldemHandRank.OnePair: return 7;
				case HoldemHandRank.TwoPair: return 11;
				case HoldemHandRank.ThreeOfAKind: return 15;
				case HoldemHandRank.Straight: return 20;
				case HoldemHandRank.Flush: return 22;
				case HoldemHandRank.FullHouse: return 27;
				case HoldemHandRank.FourOfAKind: return 36;
				case HoldemHandRank.StraightFlush: return 45;
				case HoldemHandRank.RoyalFlush: return 50;
				default: return 0;
			}
		}

		public static HoldemDamagePreview Calculate(HoldemHandResult hand, int stage)
		{
			if (hand == null)
				throw new ArgumentNullException(nameof(hand));

			float multiplier = GetStageMultiplier(stage);
			int damage = hand.Rank == HoldemHandRank.HighCard
				? CalculateHighCardDamage(hand.PrimaryRank, multiplier)
				: RoundToInt(GetBaseHandDamage(hand.Rank) * multiplier);

			if (damage > DamageCap)
				damage = DamageCap;

			return new HoldemDamagePreview(damage, multiplier, GetTargetMode(hand.Rank));
		}

		static int CalculateHighCardDamage(HoldemRank highCard, float multiplier)
		{
			int damage = RoundToInt(5f * multiplier);
			switch (highCard)
			{
				case HoldemRank.Ace: return damage + 4;
				case HoldemRank.King: return damage + 3;
				case HoldemRank.Queen: return damage + 2;
				case HoldemRank.Jack: return damage + 1;
				default: return damage;
			}
		}

		static int RoundToInt(float value)
		{
			return (int)Math.Round(value, MidpointRounding.AwayFromZero);
		}
	}
}
