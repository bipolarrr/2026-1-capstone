using System.Collections.Generic;

public readonly struct PowerUpRewardOption
{
	public readonly PowerUpType Type;
	public readonly string Title;
	public readonly string Description;
	public readonly CharacterType IntendedCharacter;
	public readonly bool IsImplemented;
	public readonly bool IsSelectable;

	public PowerUpRewardOption(
		PowerUpType type,
		string title,
		string description,
		CharacterType intendedCharacter,
		bool isImplemented = true,
		bool isSelectable = true)
	{
		Type = type;
		Title = title;
		Description = description;
		IntendedCharacter = intendedCharacter;
		IsImplemented = isImplemented;
		IsSelectable = isSelectable;
	}
}

public static class PowerUpRewardCatalog
{
	public const int RewardSlotCount = 3;

	static readonly PowerUpRewardOption[] EmptyOptions = new PowerUpRewardOption[0];

	static readonly PowerUpRewardOption OddEvenDouble = new PowerUpRewardOption(
		PowerUpType.OddEvenDouble,
		"홀짝 특화",
		"홀수 눈만 또는 짝수 눈만으로\n점수를 내면 데미지 2배",
		CharacterType.Dice);

	static readonly PowerUpRewardOption AllOrNothing = new PowerUpRewardOption(
		PowerUpType.AllOrNothing,
		"올인 전략",
		"족보 데미지 절반,\n족보 아닐 시 데미지 2배",
		CharacterType.Dice);

	static readonly PowerUpRewardOption ReviveOnce = new PowerUpRewardOption(
		PowerUpType.ReviveOnce,
		"부활의 부적",
		"사망에 이르는 데미지를\n1회 무효화 (일회성)",
		CharacterType.Dice);

	static readonly PowerUpRewardOption MahjongPartialFocus = new PowerUpRewardOption(
		PowerUpType.MahjongPartialFocus,
		"조패 집중",
		"중간공격 피해 +1",
		CharacterType.Mahjong);

	static readonly PowerUpRewardOption MahjongYakuFocus = new PowerUpRewardOption(
		PowerUpType.MahjongYakuFocus,
		"역 집중",
		"화료 피해 25% 증가",
		CharacterType.Mahjong);

	static readonly PowerUpRewardOption MahjongSafetyCharm = new PowerUpRewardOption(
		PowerUpType.MahjongSafetyCharm,
		"안전 부적",
		"적 론/쯔모 피해 -1",
		CharacterType.Mahjong);

	static readonly PowerUpRewardOption[][] DiceStageSlots =
	{
		new[] { OddEvenDouble, AllOrNothing, ReviveOnce },
		new[] { AllOrNothing, ReviveOnce, OddEvenDouble },
		new[] { ReviveOnce, OddEvenDouble, AllOrNothing },
	};

	static readonly PowerUpRewardOption[][] MahjongStageSlots =
	{
		new[] { MahjongPartialFocus, MahjongYakuFocus, MahjongSafetyCharm },
		new[] { MahjongYakuFocus, MahjongSafetyCharm, MahjongPartialFocus },
		new[] { MahjongSafetyCharm, MahjongPartialFocus, MahjongYakuFocus },
	};

	public static IReadOnlyList<PowerUpRewardOption> GetOptions(
		CharacterType character,
		string stageId,
		int rewardSlotIndex)
	{
		if (!IsSupportedStage(stageId) || rewardSlotIndex < 0 || rewardSlotIndex >= RewardSlotCount)
			return EmptyOptions;

		switch (character)
		{
			case CharacterType.Dice:
				return DiceStageSlots[rewardSlotIndex];
			case CharacterType.Mahjong:
				return MahjongStageSlots[rewardSlotIndex];
			case CharacterType.Holdem:
			default:
				return EmptyOptions;
		}
	}

	public static IReadOnlyList<PowerUpRewardOption> GetOptionsForEvent(
		CharacterType character,
		string stageId,
		int eventIndex)
	{
		int rewardSlotIndex = ResolveRewardSlotIndex(stageId, eventIndex);
		return GetOptions(character, stageId, rewardSlotIndex);
	}

	public static int ResolveRewardSlotIndex(string stageId, int eventIndex)
	{
		var stage = StageRegistry.Get(stageId);
		if (stage == null || stage.rounds == null)
			return -1;
		if (eventIndex < 0 || eventIndex >= stage.rounds.Count)
			return -1;
		if (stage.rounds[eventIndex] != StageRoundType.ItemBox)
			return -1;

		int rewardSlotIndex = -1;
		for (int i = 0; i <= eventIndex; i++)
		{
			if (stage.rounds[i] == StageRoundType.ItemBox)
				rewardSlotIndex++;
		}
		return rewardSlotIndex;
	}

	public static string GetDisplayTitle(PowerUpType type)
	{
		for (int slot = 0; slot < RewardSlotCount; slot++)
		{
			if (TryFindTitle(DiceStageSlots[slot], type, out string title))
				return title;
			if (TryFindTitle(MahjongStageSlots[slot], type, out title))
				return title;
		}
		return type.ToString();
	}

	public static bool IsDiceOnly(PowerUpType type)
	{
		return type == PowerUpType.OddEvenDouble
			|| type == PowerUpType.AllOrNothing;
	}

	public static bool IsMahjongOnly(PowerUpType type)
	{
		return type == PowerUpType.MahjongPartialFocus
			|| type == PowerUpType.MahjongYakuFocus
			|| type == PowerUpType.MahjongSafetyCharm;
	}

	static bool IsSupportedStage(string stageId)
	{
		return stageId == Stage1Forest.Id
			|| stageId == Stage2Cave.Id;
	}

	static bool TryFindTitle(PowerUpRewardOption[] options, PowerUpType type, out string title)
	{
		for (int i = 0; i < options.Length; i++)
		{
			if (options[i].Type != type)
				continue;
			title = options[i].Title;
			return true;
		}
		title = "";
		return false;
	}
}
