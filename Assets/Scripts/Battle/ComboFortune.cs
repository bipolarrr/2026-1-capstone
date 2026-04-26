using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "족보 완성" 직전에 굴림 결과값을 재작성할 수 있는 단일 훅 포인트.
/// 호출 순서: DiceRandomizer가 원시값을 뽑음 → ComboFortune.Plan 통과 → 주사위에 주입.
///
/// 현재는 입력을 그대로 통과시키는 no-op. 향후 파워업(예: "족보 완성 성공률 +X%",
/// "한 번에 하나의 눈을 교체", "꽝 금지" 등)이 이 단계에서 values를 수정하고
/// 결과 플래그(comboBoosted)를 true로 세우는 방식으로 삽입된다.
///
/// 이 모듈을 거치지 않고 주사위 값을 직접 넘기는 경로를 추가하지 말 것 — 훅을 우회하게 됨.
/// </summary>
public static class ComboFortune
{
	/// <summary>
	/// 계획된 굴림 결과. values는 길이 5, 인덱스는 dice 배열과 1:1.
	/// </summary>
	public struct Plan
	{
		/// <summary>최종 굴림 결과값. 홀드 주사위는 그 자리의 현재 Result 그대로.</summary>
		public int[] values;

		/// <summary>파워업에 의해 값이 족보 쪽으로 보정되었는지. 현재는 항상 false.</summary>
		public bool comboBoosted;

		/// <summary>보정이 발동한 경우의 라벨(로그·UI·연출 힌트용). 기본 null.</summary>
		public string boostReason;
	}

	/// <summary>
	/// 원시 굴림 결과(rawValues)를 받아 파워업을 적용한 최종 Plan을 돌려준다.
	/// heldMask[i]=true인 자리는 보정 대상에서 제외(이미 고정된 값).
	/// rawValues는 호출자 소유 — 이 함수는 새 배열을 반환하므로 원본을 훼손하지 않음.
	/// </summary>
	/// <summary>파워업이 없을 때의 "나와라!" 성공 기본 확률.</summary>
	public const float BaseBoostRate = 0.01f;

	/// <summary>
	/// 현재 파워업 구성에서 "우선 타겟팅할 족보 등급"을 반환. 0 = 우선순위 없음(최고 등급 타겟).
	/// ComboProximity.ComputeStopProfile의 preferredRank 파라미터로 전달된다.
	///
	/// 동점 규칙: 여러 파워업이 각자 다른 족보를 우선시하면 "먼저 일치한" 분기가 채택된다 —
	/// 앞쪽 if가 우선순위 상 상위다. 신규 파워업은 의도한 우선순위에 맞는 위치에 추가할 것.
	/// </summary>
	public static int GetPreferredTargetRank(List<PowerUpType> powerUps)
	{
		if (powerUps == null) return 0;

		// ───── 파워업 우선 타겟 슬롯 (상위 우선순위 먼저) ─────
		// 예: Full House 관련 파워업 — 동급 이상 후보가 있어도 FH를 먼저 노린다
		//   if (powerUps.Contains(PowerUpType.FullHouseMaster))
		//       return ComboProximity.ComboRankFullHouse;
		//
		// 예: Large Straight 관련 파워업
		//   if (powerUps.Contains(PowerUpType.StraightMaster))
		//       return ComboProximity.ComboRankLargeStraight;
		//
		// 신규 파워업은 이 블록에 추가. 반환되는 rank는 ComboProximity.ComboRank* 상수여야 하며,
		// 도달 불가하면 ComputeStopProfile이 자동으로 기본 규칙(최고 등급)으로 폴백한다.
		// ───────────────────────────────────────────────────

		return 0;
	}

	/// <summary>
	/// 원시 결과값을 Plan으로 감싸 반환. 이 단계에서는 보정이 적용되지 않음 (plan.comboBoosted == false).
	/// 실제 보정 확률 계산은 TryBoost에서 수행.
	/// </summary>
	public static Plan Apply(int[] rawValues, bool[] heldMask, List<PowerUpType> powerUps)
	{
		int[] copy = new int[rawValues.Length];
		for (int i = 0; i < rawValues.Length; i++)
			copy[i] = rawValues[i];

		return new Plan
		{
			values       = copy,
			comboBoosted = false,
			boostReason  = null,
		};
	}

	/// <summary>
	/// 현재 파워업 구성에 따른 "나와라!" 보정 성공 확률. 기본 5% + 파워업 가산.
	/// 향후 "특정 역 완성 확률 상향" 파워업은 여기서 누적.
	/// </summary>
	public static float ComputeBoostProbability(List<PowerUpType> powerUps)
	{
		float probability = BaseBoostRate;

		if (powerUps != null)
		{
			// ───── 파워업 확장 지점 ─────
			// 예: 특정 역 완성 확률 상향 (모든 역 공통)
			//   if (powerUps.Contains(PowerUpType.ComboLuck))      probability += 0.15f;
			//   if (powerUps.Contains(PowerUpType.ComboLuckPlus))  probability += 0.30f;
			//
			// 향후 "특정 역만 확률 가중"이 필요해지면 per-combo 가중치 테이블로 확장.
			// 현재 ForceNearestCombo는 "가장 강한 족보(Yacht/4oK)"로 수렴하므로,
			// per-combo 타겟을 쓰려면 ForceNearestCombo에 targetCombo 파라미터를 추가하고
			// 여기서 파워업별로 해당 타겟과 가중치를 누적하면 됨.
			// ───────────────────────────
		}

		return Mathf.Clamp01(probability);
	}

	/// <summary>
	/// "나와라!" 확률 판정. 여러 보정 효과를 우선순위대로 독립 판정하며, 하나라도 성공하면
	/// plan.values를 수정하고 반환. 파워업 확장 지점은 아래 주석 블록 참고.
	///
	/// 기본 효과: "1장 교체로 가능한 최고 등급 업그레이드"만 수행 — 상위 업그레이드 여지가
	/// 없으면 아무 것도 하지 않으므로 이미 족보인 상태는 절대 손상되지 않는다.
	///
	/// 호출 규약: 호출자는 "건드리면 안 되는 모든 주사위"를 heldMask에 true로 넘길 책임이 있다.
	/// BattleSceneController는 StopByProfileRoutine에서 decisiveMask 외 전부를 잠그는 방식으로
	/// "나와라! 세션 당 1장 이내"를 강제한다 — 이 덕분에 예컨대 Small Straight 한 장 빠짐
	/// (두 decisive 주사위) 상황이 보정을 통해 Large Straight로 올라가는 일이 발생하지 않는다.
	/// 향후 이 메소드에 추가되는 어떤 파워업 분기도 heldMask를 우회해서는 안 된다.
	/// </summary>
	public static Plan TryBoost(Plan plan, bool[] heldMask, List<PowerUpType> powerUps)
	{
		// ─────────────────────────────────────────────────────
		// 파워업 효과 슬롯 (독립 확률, 우선순위 높은 것부터 판정)
		// 새 파워업은 이 블록에 if-분기를 추가하는 방식으로 확장.
		//
		// 예1: "1% 확률로 Yacht 발동" 파워업
		//   if (powerUps != null && powerUps.Contains(PowerUpType.ForceYachtChance)
		//       && UnityEngine.Random.value < 0.01f)
		//   {
		//       ForceNearestCombo(plan.values, heldMask);
		//       plan.comboBoosted = true;
		//       plan.boostReason  = "요트 확정 (1%)";
		//       return plan;
		//   }
		//
		// 예2: "특정 족보 완성 확률 X%" 파워업 — ForceNearestCombo에 targetCombo 파라미터를
		//   추가한 뒤 여기서 호출
		// ─────────────────────────────────────────────────────

		// ── 기본 보정: 5% + 파워업 가산 확률로 "1장 교체 최고 업그레이드" 적용 ──
		float probability = ComputeBoostProbability(powerUps);
		if (UnityEngine.Random.value < probability)
		{
			if (TryUpgradeOneDie(plan.values, heldMask))
			{
				plan.comboBoosted = true;
				plan.boostReason  = $"행운의 보정 ({Mathf.RoundToInt(probability * 100f)}%)";
			}
			// 업그레이드 여지가 없으면 값 불변 — 이미 족보라면 그 족보가 그대로 유지됨
		}
		return plan;
	}

	/// <summary>
	/// 비홀드 주사위 중 하나를 다른 면으로 바꿔서 얻을 수 있는 "가장 높은 등급 업그레이드"를
	/// 찾아 values에 적용한다. 업그레이드가 적용되면 true, 아니면 false(values 불변).
	/// 동점 시 가장 왼쪽 주사위, 가장 작은 face 값을 선택.
	/// </summary>
	public static bool TryUpgradeOneDie(int[] values, bool[] heldMask)
	{
		if (values == null || values.Length == 0) return false;

		int currentRank = ComboProximity.ComputeComboRank(values);
		int bestRank = currentRank;
		int bestIdx  = -1;
		int bestFace = -1;
		int[] tmp = new int[values.Length];

		for (int i = 0; i < values.Length; i++)
		{
			bool held = heldMask != null && i < heldMask.Length && heldMask[i];
			if (held) continue;

			for (int face = 1; face <= 6; face++)
			{
				if (face == values[i]) continue;
				System.Array.Copy(values, tmp, values.Length);
				tmp[i] = face;
				int rank = ComboProximity.ComputeComboRank(tmp);
				if (rank > bestRank)
				{
					bestRank = rank;
					bestIdx  = i;
					bestFace = face;
				}
			}
		}

		if (bestIdx < 0) return false;
		values[bestIdx] = bestFace;
		return true;
	}

	/// <summary>
	/// 홀드되지 않은 주사위들을 가장 가까운 족보가 되도록 덮어쓴다.
	/// 현재 정책: 가장 많이 등장한 눈(동점 시 높은 쪽)으로 비홀드 주사위를 통일 → 4 of a Kind 또는 Yacht.
	/// 홀드된 주사위는 절대 건드리지 않는다.
	/// </summary>
	public static void ForceNearestCombo(int[] values, bool[] heldMask)
	{
		if (values == null || values.Length == 0) return;

		int[] counts = new int[7];
		for (int i = 0; i < values.Length; i++)
			if (values[i] >= 1 && values[i] <= 6) counts[values[i]]++;

		int targetFace = 6;
		int best = -1;
		for (int f = 6; f >= 1; f--)
		{
			if (counts[f] > best)
			{
				best = counts[f];
				targetFace = f;
			}
		}

		for (int i = 0; i < values.Length; i++)
		{
			bool held = heldMask != null && i < heldMask.Length && heldMask[i];
			if (held) continue;
			values[i] = targetFace;
		}
	}
}
