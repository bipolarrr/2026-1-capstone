/// <summary>
/// 사전에 뽑힌 주사위 결과값을 기반으로 "멈추기" 흐름을 결정짓는 프로필.
/// BattleSceneController가 StopByProfileRoutine에서 소비하고, ComboProximity.ComputeStopProfile이 생성한다.
///
/// 케이스 구분:
///   AlreadyCombo  — 이미 족보가 완성된 상태. 컴보 구성 주사위는 stable로 먼저 멈추고,
///                   나머지(혹은 임의의 1개)가 decisive로 flicker 연출. 보정 없음.
///   OneAway       — 족보까지 한 장 부족한 상태. 결정적 주사위 집합(decisive)은 마지막에 flicker하고,
///                   "나와라!" 시점에 ComboFortune.TryBoost가 decisive 이외는 잠근 채로 호출된다.
///   None          — 두 경우 모두 아님. stable=비홀드 전부, decisive=비어있음, 나와라 버튼도 미노출.
/// </summary>
public enum DiceStopCase { None, AlreadyCombo, OneAway }

public struct DiceStopProfile
{
	public DiceStopCase scenario;

	/// <summary>먼저 멈출 주사위(플래그=true). 길이는 dice 배열과 동일(5).</summary>
	public bool[] stableMask;

	/// <summary>마지막에 flicker 연출할 주사위. 여러 개면 동시에 flicker.</summary>
	public bool[] decisiveMask;

	/// <summary>"나와라!" 버튼을 노출할지 여부(Case A || Case B).</summary>
	public bool showComeOut;

	/// <summary>나와라! 시점에 ComboFortune.TryBoost를 호출할지 여부(Case B 전용).</summary>
	public bool applyBoost;

	/// <summary>ComputeComboRank(plan) — 로그/디버그용.</summary>
	public int plannedRank;

	/// <summary>"나와라!" 시점에 노리는 최종 족보 랭크. Case A: plannedRank 그대로. Case B: bestReachableRank.</summary>
	public int targetRank;

	public static DiceStopProfile CreateEmpty(int length)
	{
		return new DiceStopProfile
		{
			scenario         = DiceStopCase.None,
			stableMask   = new bool[length],
			decisiveMask = new bool[length],
			showComeOut  = false,
			applyBoost   = false,
			plannedRank  = 0,
			targetRank   = 0,
		};
	}
}
