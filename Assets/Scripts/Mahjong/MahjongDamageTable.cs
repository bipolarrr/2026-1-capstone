namespace Mahjong
{
	/// <summary>
	/// 데미지는 절반하트 정수 단위.
	/// 100% AOE = 야쿠만(16). 1한=1, 2한=1.5(올림 2), 3한=2.5(올림 3), 만관(4-5한)=4·6, 배만=8, 삼배만=12, 카운트 야쿠만=16.
	/// 한수에 도라가 더해진 TotalHan으로 룩업.
	/// </summary>
	public static class MahjongDamageTable
	{
		public const int FullAoeHalfHearts = 16;

		public static int GetWinDamageHalfHearts(YakuResult yaku)
		{
			if (yaku == null || !yaku.HasAnyYaku) return 0;
			if (yaku.YakumanMultiplier > 0)
				return FullAoeHalfHearts * yaku.YakumanMultiplier;

			int han = yaku.TotalHan;
			if (han <= 0) return 0;
			if (han >= 13) return FullAoeHalfHearts;     // 카운트 야쿠만
			if (han >= 11) return 12;                     // 삼배만
			if (han >= 8) return 8;                       // 배만
			if (han >= 6) return 6;                       // 跳満
			if (han >= 5) return 4;                       // 満貫
			if (han >= 4) return 4;
			if (han == 3) return 3;
			if (han == 2) return 2;
			return 1;
		}

		/// <summary>중간 포기(50% 위력) 데미지. 멘츠 0.5 + 머리 0.25 + 깡 +0.25 (절반하트 단위, 올림).</summary>
		public static int GetPartialDamageHalfHearts(PartialBreakdown b)
		{
			float v = (b.Shuntsu + b.Koutsu) * 0.5f + b.Pair * 0.25f + b.Kantsu * 0.75f;
			v *= 0.5f; // 중간 포기 50% 위력 패널티
			int rounded = (int)System.Math.Ceiling(v);
			return rounded < 0 ? 0 : rounded;
		}

		public static int GetAoeDamageOnNonTarget(int targetDamage) => targetDamage; // 100%/50% 모두 모든 적에 동일 적용
	}
}
