using UnityEngine;

namespace Mahjong
{
	/// <summary>
	/// "적 대기패 직감" 발동 확률/보정을 모아둔 런타임 설정.
	/// 확률은 버림 1회당 적 1명 기준. 향후 파워업/아이템이 이 값을 증감시킬 수 있도록 분리.
	/// 하드코딩 대신 이 컴포넌트를 통해 조회·수정한다.
	/// </summary>
	public class MahjongIntuitionConfig : MonoBehaviour
	{
		[SerializeField, Range(0f, 1f)] float baseRevealChancePerDiscard = 0.02f;

		/// <summary>파워업/아이템에서 런타임에 가산하는 보너스. 기본 0.</summary>
		public float BonusRevealChance { get; set; } = 0f;

		/// <summary>확정 공개(디버그/보장 효과)를 한 번 소모하는 플래그.</summary>
		public bool ConsumeForcedReveal()
		{
			if (forcedRevealTokens <= 0) return false;
			forcedRevealTokens--;
			return true;
		}

		int forcedRevealTokens = 0;

		public void GrantForcedReveal(int count = 1) => forcedRevealTokens += Mathf.Max(0, count);

		public float CurrentChance => Mathf.Clamp01(baseRevealChancePerDiscard + BonusRevealChance);
	}
}
