using System.Collections.Generic;

/// <summary>
/// 확정된 주사위 결과값 집합을 분석해 "어떤 주사위가 족보 달성/근접에 결정적인가"를 판정한다.
/// 두구두구 강조 연출용 — DamageCalculator의 실제 판정 로직과는 독립적.
/// </summary>
public static class ComboProximity
{
	/// <summary>
	/// values[i]가 결정적이면 mask[i] = true.
	/// 판정 기준:
	///   1) 같은 눈이 3개 이상 → 해당 눈의 주사위들 (3-of-a-kind 이상)
	///   2) 길이 4 이상의 연속 → 해당 런에 속하는 주사위 각 눈당 1개
	///   3) 풀하우스 (트리플 + 페어) → 트리플과 페어 모두
	///   그 외 → 전부 false.
	/// </summary>
	public static bool[] ComputeEmphasis(int[] values)
	{
		bool[] mask = new bool[values.Length];
		if (values.Length == 0) return mask;

		int[] counts = new int[7];
		for (int i = 0; i < values.Length; i++)
		{
			int v = values[i];
			if (v >= 1 && v <= 6) counts[v]++;
		}

		int maxCount = 0;
		int maxFace = 0;
		int secondMaxCount = 0;
		int secondMaxFace = 0;
		for (int f = 1; f <= 6; f++)
		{
			if (counts[f] > maxCount)
			{
				secondMaxCount = maxCount;
				secondMaxFace  = maxFace;
				maxCount = counts[f];
				maxFace  = f;
			}
			else if (counts[f] > secondMaxCount)
			{
				secondMaxCount = counts[f];
				secondMaxFace  = f;
			}
		}

		// 풀하우스: 3+2
		if (maxCount >= 3 && secondMaxCount >= 2)
		{
			for (int i = 0; i < values.Length; i++)
				if (values[i] == maxFace || values[i] == secondMaxFace)
					mask[i] = true;
			return mask;
		}

		// 3개 이상 같은 눈
		if (maxCount >= 3)
		{
			for (int i = 0; i < values.Length; i++)
				if (values[i] == maxFace)
					mask[i] = true;
			return mask;
		}

		// 연속 4개 이상
		var distinctSet = new SortedSet<int>();
		for (int i = 0; i < values.Length; i++)
			if (values[i] >= 1 && values[i] <= 6)
				distinctSet.Add(values[i]);

		int[] distinct = new int[distinctSet.Count];
		distinctSet.CopyTo(distinct);

		int bestLen = 1, bestStart = 0;
		int curLen = 1, curStart = 0;
		for (int i = 1; i < distinct.Length; i++)
		{
			if (distinct[i] == distinct[i - 1] + 1)
			{
				curLen++;
				if (curLen > bestLen)
				{
					bestLen = curLen;
					bestStart = curStart;
				}
			}
			else
			{
				curLen = 1;
				curStart = i;
			}
		}

		if (bestLen >= 4)
		{
			int lo = distinct[bestStart];
			int hi = distinct[bestStart + bestLen - 1];
			// 런에 속한 각 눈의 첫 주사위만 강조 (중복 눈 나머지는 제외)
			for (int f = lo; f <= hi; f++)
			{
				for (int i = 0; i < values.Length; i++)
				{
					if (values[i] == f)
					{
						mask[i] = true;
						break;
					}
				}
			}
			return mask;
		}

		return mask;
	}

	/// <summary>편의 헬퍼: 마스크에 true가 하나라도 있는지.</summary>
	public static bool HasAny(bool[] mask)
	{
		for (int i = 0; i < mask.Length; i++)
			if (mask[i]) return true;
		return false;
	}

	/// <summary>
	/// "역까지 한 장 남은" 상태에서 "결정적 주사위"(= 값을 바꾸면 가장 큰 등급 업그레이드가
	/// 가능한 비홀드 주사위)의 인덱스를 반환. 없으면 -1.
	///
	/// 동점 규칙: 동일한 최고 업그레이드 등급이 여러 주사위에서 가능하면 **가장 왼쪽**을 선택.
	/// 다른 모든 주사위 = "족보에 해당하는 부분" → 호출자가 이쪽을 먼저 정지시키면 됨.
	/// </summary>
	public static int FindDecisiveDieIndex(int[] values, bool[] heldMask)
	{
		if (values == null || values.Length != 5) return -1;

		int currentRank = ComputeComboRank(values);
		if (currentRank >= ComboRankYacht) return -1;

		int bestIdx  = -1;
		int bestRank = currentRank;
		int[] tmp    = new int[values.Length];

		for (int i = 0; i < values.Length; i++)
		{
			bool held = heldMask != null && i < heldMask.Length && heldMask[i];
			if (held) continue;

			int localBest = currentRank;
			for (int face = 1; face <= 6; face++)
			{
				if (face == values[i]) continue;
				System.Array.Copy(values, tmp, values.Length);
				tmp[i] = face;
				int rank = ComputeComboRank(tmp);
				if (rank > localBest) localBest = rank;
			}

			// 엄격한 > 조건이므로 동점 시 먼저 찾은(왼쪽) 인덱스가 유지됨
			if (localBest > bestRank)
			{
				bestRank = localBest;
				bestIdx  = i;
			}
		}

		return bestIdx;
	}

	/// <summary>
	/// 역까지 한 장 남은 상태인지(= 어떤 비홀드 주사위 1개 교체로 등급 업그레이드가 가능한지).
	/// 세부 인덱스가 필요하면 FindDecisiveDieIndex를 사용할 것.
	/// </summary>
	public static bool IsOneAwayFromCombo(int[] values, bool[] heldMask)
	{
		return FindDecisiveDieIndex(values, heldMask) >= 0;
	}

	/// <summary>
	/// "5개 전부를 사용하는 족보"인지. 이 집합만 Case A/B 연출 대상이 된다.
	/// Small Straight(4개)와 Four of a Kind(4개)는 5구성이 아니라 "승격 여지가 있는 중간 상태".
	/// </summary>
	public static bool IsFiveDiceCombo(int rank)
	{
		return rank == ComboRankYacht
		    || rank == ComboRankLargeStraight
		    || rank == ComboRankFullHouse;
	}

	/// <summary>
	/// 사전 확정된 plan을 분석해 멈추기 흐름(Case A/B/C)을 결정한다. 결과는 DiceStopProfile로 묶여
	/// DiceRollDirector의 멈추기 시퀀스에 전달된다.
	///
	///   Case A (AlreadyCombo): plan이 이미 5구성 족보(Yacht/LS/FH)를 이룸. stable = 족보 참여,
	///     decisive = 그 외 비홀드(없으면 드라마용으로 가장 왼쪽 하나를 돌림). 보정 없음.
	///
	///   Case B (OneAway): 비홀드 1장 교체로 "5구성 족보"에 도달 가능한 경우. SS/4oK 자체는 5구성이
	///     아니므로 여기로 들어온다 — 보정 실패 시 원래 값이 유지되어 중간 족보(SS/4oK)를 손해보지 않음.
	///     bestReachableRank를 먼저 구하고 그 최고치를 달성 가능한 모든 인덱스를 decisive로 표시.
	///
	///   Case C (None): 위 둘 다 아님 — "5구성 족보로 갈 수 없음". SS/4oK면 중간 족보로 그대로 유지,
	///     아예 꽝이면 그냥 순차 정지. 나와라! 버튼 미노출.
	/// </summary>
	/// <summary>
	/// preferredRank: 파워업 등으로 "이 등급을 우선 타겟팅하라"는 힌트 (0 = 우선순위 없음).
	/// 유효하려면 preferredRank > 현재 rank여야 하며, 1장 교체로 실제 도달 가능해야 한다.
	/// 두 조건을 만족하면 최고 등급이 아니라 preferredRank를 targetRank로 고정한다.
	/// 힌트가 도달 불가면 기본 규칙(최고 등급)으로 폴백.
	/// 예: "Full House 완성률 +X%" 파워업 → preferredRank = ComboRankFullHouse.
	/// </summary>
	public static DiceStopProfile ComputeStopProfile(int[] plan, bool[] heldMask, int preferredRank = 0)
	{
		int length = plan != null ? plan.Length : 0;
		var profile = DiceStopProfile.CreateEmpty(length);
		if (length == 0) return profile;

		bool IsHeld(int i) => heldMask != null && i < heldMask.Length && heldMask[i];

		int rank = ComputeComboRank(plan);
		profile.plannedRank = rank;

		// ── Case A: 이미 5구성 족보 완성 (Yacht/LS/FH) ──
		if (IsFiveDiceCombo(rank))
		{
			bool[] comboMask = ComputeComboMembershipMask(plan, rank);

			for (int i = 0; i < length; i++)
			{
				if (IsHeld(i)) continue;
				if (comboMask[i]) profile.stableMask[i]   = true;
				else              profile.decisiveMask[i] = true;
			}

			// 드라마용 flicker 대상이 없으면(= 모든 비홀드가 족보 참여) 가장 왼쪽 비홀드 하나를 decisive로 돌린다.
			bool hasDecisive = false;
			for (int i = 0; i < length; i++) if (profile.decisiveMask[i]) { hasDecisive = true; break; }
			if (!hasDecisive)
			{
				for (int i = 0; i < length; i++)
				{
					if (profile.stableMask[i])
					{
						profile.stableMask[i]   = false;
						profile.decisiveMask[i] = true;
						break;
					}
				}
			}

			profile.scenario        = DiceStopCase.AlreadyCombo;
			profile.showComeOut = true;
			profile.applyBoost  = false;
			profile.targetRank  = rank;
			return profile;
		}

		// ── Case B: 1장 교체로 현재보다 높은 등급에 도달 가능 ──
		// 1차 패스: 도달 가능한 모든 상위 등급을 수집. preferredRank가 포함되면 우선 채택.
		int bestReachableRank = 0;
		bool preferredReachable = false;
		int[] tmp = new int[length];
		for (int i = 0; i < length; i++)
		{
			if (IsHeld(i)) continue;
			for (int face = 1; face <= 6; face++)
			{
				if (face == plan[i]) continue;
				System.Array.Copy(plan, tmp, length);
				tmp[i] = face;
				int r = ComputeComboRank(tmp);
				if (r <= rank) continue;
				if (r > bestReachableRank) bestReachableRank = r;
				if (preferredRank > 0 && r == preferredRank) preferredReachable = true;
			}
		}

		// 파워업 힌트가 도달 가능하면 최고 등급 대신 그것을 타겟으로 고정.
		int targetRank = (preferredReachable && preferredRank > rank) ? preferredRank : bestReachableRank;

		if (targetRank > 0)
		{
			// 2차 패스: targetRank 도달 가능한 단일 인덱스를 decisive, 나머지 비홀드는 stable.
			// Case B는 "1장 교체"이므로 decisive는 반드시 1개.
			// 후보가 여러 개일 때는 나머지 stable이 가장 높은 부분 족보를 이루는 인덱스를 선택.
			int bestIdx = -1;
			int bestStableSubRank = -1;
			for (int i = 0; i < length; i++)
			{
				if (IsHeld(i)) continue;
				bool canReach = false;
				for (int face = 1; face <= 6; face++)
				{
					if (face == plan[i]) continue;
					System.Array.Copy(plan, tmp, length);
					tmp[i] = face;
					if (ComputeComboRank(tmp) == targetRank) { canReach = true; break; }
				}
				if (!canReach) continue;

				// 이 인덱스를 decisive로 뺐을 때 나머지의 부분 족보 등급을 평가
				int[] rest = new int[length - 1];
				int ri = 0;
				for (int j = 0; j < length; j++)
				{
					if (j == i) continue;
					rest[ri++] = plan[j];
				}
				int subRank = ComputeComboRank(rest);
				if (subRank > bestStableSubRank || (subRank == bestStableSubRank && bestIdx < 0))
				{
					bestStableSubRank = subRank;
					bestIdx = i;
				}
			}

			if (bestIdx >= 0)
			{
				for (int i = 0; i < length; i++)
				{
					if (IsHeld(i)) continue;
					if (i == bestIdx) profile.decisiveMask[i] = true;
					else              profile.stableMask[i]   = true;
				}
			}

			profile.scenario        = DiceStopCase.OneAway;
			profile.showComeOut = true;
			profile.applyBoost  = true;
			profile.targetRank  = targetRank;
			return profile;
		}

		// ── Case C: 5구성 족보로 갈 수 없음 ──
		// SS나 4oK가 여기로 들어오면 그대로 유지(보정 기회 없음), 꽝이면 그냥 꽝.
		for (int i = 0; i < length; i++)
		{
			if (IsHeld(i)) continue;
			profile.stableMask[i] = true;
		}
		profile.scenario = DiceStopCase.None;
		return profile;
	}

	/// <summary>
	/// plan에서 "현재 득점되는 족보에 실제로 참여하는" 주사위의 마스크를 돌려준다.
	/// rank는 호출자가 이미 계산한 값(ComputeComboRank 결과). ComputeComboRank와 판정 규칙이 일치해야 함.
	/// </summary>
	static bool[] ComputeComboMembershipMask(int[] values, int rank)
	{
		bool[] mask = new bool[values.Length];

		switch (rank)
		{
			case ComboRankYacht:
			case ComboRankLargeStraight:
			case ComboRankFullHouse:
				// 모든 5개가 족보에 참여
				for (int i = 0; i < values.Length; i++) mask[i] = true;
				return mask;

			case ComboRankFourOfAKind:
			{
				// 4개 이상 같은 눈의 face를 찾아 그 값을 가진 주사위만 true
				int[] counts = new int[7];
				for (int i = 0; i < values.Length; i++)
					if (values[i] >= 1 && values[i] <= 6) counts[values[i]]++;

				int targetFace = 0;
				for (int f = 1; f <= 6; f++)
					if (counts[f] >= 4) { targetFace = f; break; }

				if (targetFace > 0)
				{
					int marked = 0;
					for (int i = 0; i < values.Length && marked < 4; i++)
					{
						if (values[i] == targetFace) { mask[i] = true; marked++; }
					}
				}
				return mask;
			}

			case ComboRankSmallStraight:
			{
				// 4연속 run을 찾아 해당 눈당 첫 주사위 한 개씩만 true (총 4개)
				var distinctSet = new SortedSet<int>();
				for (int i = 0; i < values.Length; i++)
					if (values[i] >= 1 && values[i] <= 6) distinctSet.Add(values[i]);
				int[] distinct = new int[distinctSet.Count];
				distinctSet.CopyTo(distinct);

				int bestLen = 1, bestStart = 0;
				int curLen = 1, curStart = 0;
				for (int i = 1; i < distinct.Length; i++)
				{
					if (distinct[i] == distinct[i - 1] + 1)
					{
						curLen++;
						if (curLen > bestLen) { bestLen = curLen; bestStart = curStart; }
					}
					else { curLen = 1; curStart = i; }
				}

				if (bestLen >= 4)
				{
					int lo = distinct[bestStart];
					int hi = distinct[bestStart + bestLen - 1];
					// run에 속한 눈마다 가장 왼쪽 주사위 한 개씩만 마크
					for (int f = lo; f <= hi; f++)
					{
						for (int i = 0; i < values.Length; i++)
						{
							if (values[i] == f) { mask[i] = true; break; }
						}
					}
				}
				return mask;
			}
		}

		return mask;
	}

	/// <summary>
	/// 사전 확정 plan에 대해 두구두구 강조 연출을 활성화할지 결정.
	/// 조건: Case B(OneAway)이면서 decisive 인덱스가 1 또는 2개. 이 경우 "1/6 이상의 확률로
	/// 1~2개의 주사위가 족보냐 아니냐를 결정"한다는 스펙을 만족한다.
	///   n=1: 해당 1장이 목표 face로 떨어질 확률 ≥ 1/6 (최소 1개 이상의 face가 decisive 조건을 만족하므로).
	///   n=2: 두 주사위 중 하나라도 목표 face에 떨어지면 되므로 ≥ 1 - (5/6)^2 > 1/6.
	///   n>2 또는 Case A/None: 스펙 상 제외 (Case A는 이미 확정, None은 해당 없음).
	/// </summary>
	public static bool ShouldEmphasize(DiceStopProfile profile, int[] plan)
	{
		if (profile.scenario != DiceStopCase.OneAway) return false;
		if (profile.decisiveMask == null) return false;

		int count = 0;
		for (int i = 0; i < profile.decisiveMask.Length; i++)
			if (profile.decisiveMask[i]) count++;

		return count >= 1 && count <= 2;
	}

	public const int ComboRankNone         = 0;
	public const int ComboRankSmallStraight = 1;
	public const int ComboRankFullHouse    = 2;
	public const int ComboRankLargeStraight = 3;
	public const int ComboRankFourOfAKind  = 4;
	public const int ComboRankYacht        = 5;

	/// <summary>
	/// 주어진 값 배열의 최고 족보 등급을 반환. 0 = 없음, 5 = Yacht.
	/// DamageCalculator와 동일한 판정 기준을 사용.
	/// </summary>
	public static int ComputeComboRank(int[] values)
	{
		if (values == null || values.Length == 0) return ComboRankNone;

		int[] counts = new int[7];
		for (int i = 0; i < values.Length; i++)
			if (values[i] >= 1 && values[i] <= 6) counts[values[i]]++;

		int maxCount = 0;
		for (int f = 1; f <= 6; f++)
			if (counts[f] > maxCount) maxCount = counts[f];

		if (maxCount == 5) return ComboRankYacht;
		if (maxCount == 4) return ComboRankFourOfAKind;

		int[] sorted = (int[])values.Clone();
		System.Array.Sort(sorted);

		// Large Straight = 5개 모두 distinct + 연속 5
		if (maxCount == 1 && DamageCalculator.HasRun(sorted, 5))
			return ComboRankLargeStraight;

		if (DamageCalculator.IsFullHouse(counts))
			return ComboRankFullHouse;

		if (DamageCalculator.HasRun(sorted, 4))
			return ComboRankSmallStraight;

		return ComboRankNone;
	}

	/// <summary>Rank → 표시명. DamageCalculator.Calculate와 동일한 명칭을 사용.</summary>
	public static string GetComboName(int rank)
	{
		switch (rank)
		{
			case ComboRankYacht:         return "YACHT";
			case ComboRankFourOfAKind:   return "Four of a Kind";
			case ComboRankLargeStraight: return "Large Straight";
			case ComboRankFullHouse:     return "Full House";
			case ComboRankSmallStraight: return "Small Straight";
			default:                     return "";
		}
	}

	/// <summary>
	/// "나와라!" 단계에서 플레이어에게 어떤 눈이 필요한지 계산한다.
	/// decisive 인덱스를 하나씩 훑으며, 그 자리에 어떤 face(1~6)를 넣으면 profile.targetRank를
	/// 달성할 수 있는지 수집해 정렬된 리스트로 돌려준다.
	///   Case A(이미 완성): decisive die의 현재 plan 값만 반환 (드라마 연출용).
	///   Case B(1장 차이):  decisive die 각각에 대해 targetRank 도달 face들을 합집합으로 반환.
	/// 반환 리스트는 중복 없이 오름차순. profile.showComeOut가 false면 빈 리스트.
	/// </summary>
	public static List<int> GetDecisiveTargetFaces(int[] plan, bool[] heldMask, DiceStopProfile profile)
	{
		var result = new List<int>();
		if (plan == null || plan.Length == 0 || !profile.showComeOut) return result;
		if (profile.decisiveMask == null) return result;

		bool IsHeld(int i) => heldMask != null && i < heldMask.Length && heldMask[i];
		var set = new SortedSet<int>();

		if (profile.scenario == DiceStopCase.AlreadyCombo)
		{
			// 이미 족보를 이룬 상태 — decisive die의 현재 계획 face를 그대로 노출.
			for (int i = 0; i < plan.Length && i < profile.decisiveMask.Length; i++)
			{
				if (IsHeld(i)) continue;
				if (!profile.decisiveMask[i]) continue;
				int v = plan[i];
				if (v >= 1 && v <= 6) set.Add(v);
			}
		}
		else if (profile.scenario == DiceStopCase.OneAway)
		{
			int target = profile.targetRank;
			if (target <= 0) return result;

			int[] tmp = new int[plan.Length];
			for (int i = 0; i < plan.Length && i < profile.decisiveMask.Length; i++)
			{
				if (IsHeld(i)) continue;
				if (!profile.decisiveMask[i]) continue;

				for (int face = 1; face <= 6; face++)
				{
					if (face == plan[i]) continue;
					System.Array.Copy(plan, tmp, plan.Length);
					tmp[i] = face;
					if (ComputeComboRank(tmp) == target)
						set.Add(face);
				}
			}
		}

		result.AddRange(set);
		return result;
	}

	/// <summary>
	/// Case B(OneAway)에서 decisive die를 재굴림할 때 도달 가능한 모든 상위 족보를
	/// (rank 내림차순, rank 동률 시 face 오름차순)으로 반환한다.
	/// 각 엔트리는 (rank, 정렬된 face 리스트). Case A/None이거나 showComeOut이 false면 빈 리스트.
	/// GetDecisiveTargetFaces(targetRank 한정)의 상위 집합: 보정은 targetRank에만 걸리지만,
	/// 유저에게는 "노려볼 수 있는 모든 족보"를 함께 안내하기 위해 사용.
	/// </summary>
	public static List<(int rank, List<int> faces)> GetAllReachableCombos(
		int[] plan, bool[] heldMask, DiceStopProfile profile)
	{
		var result = new List<(int rank, List<int> faces)>();
		if (plan == null || plan.Length == 0 || !profile.showComeOut) return result;
		if (profile.scenario != DiceStopCase.OneAway) return result;
		if (profile.decisiveMask == null) return result;

		bool IsHeld(int i) => heldMask != null && i < heldMask.Length && heldMask[i];

		int currentRank = ComputeComboRank(plan);
		var buckets = new Dictionary<int, SortedSet<int>>();

		int[] tmp = new int[plan.Length];
		for (int i = 0; i < plan.Length && i < profile.decisiveMask.Length; i++)
		{
			if (IsHeld(i)) continue;
			if (!profile.decisiveMask[i]) continue;

			for (int face = 1; face <= 6; face++)
			{
				if (face == plan[i]) continue;
				System.Array.Copy(plan, tmp, plan.Length);
				tmp[i] = face;
				int r = ComputeComboRank(tmp);
				if (r <= currentRank) continue;

				if (!buckets.TryGetValue(r, out var set))
				{
					set = new SortedSet<int>();
					buckets[r] = set;
				}
				set.Add(face);
			}
		}

		var ranks = new List<int>(buckets.Keys);
		ranks.Sort((a, b) => b.CompareTo(a));
		foreach (int r in ranks)
		{
			var faces = new List<int>(buckets[r]);
			result.Add((r, faces));
		}
		return result;
	}
}
