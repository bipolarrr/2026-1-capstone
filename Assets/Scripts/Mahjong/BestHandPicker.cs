using System.Collections.Generic;

namespace Mahjong
{
	public class BestHandResult
	{
		public WinningHand Hand;
		public YakuResult Yaku;
	}

	/// <summary>
	/// 닫힌 손패+쯔모(=14장) + 안깡 + 도라 컨텍스트에서 최고 한수 분해 선택.
	/// </summary>
	public static class BestHandPicker
	{
		public static BestHandResult Pick(List<Tile> concealedFourteen, IReadOnlyList<Meld> ankans, Tile winningTile, bool tsumoWin, bool riichi, IReadOnlyList<Tile> doraTiles)
		{
			var decompositions = HandDecomposer.Enumerate(concealedFourteen, ankans, winningTile, tsumoWin);
			if (decompositions.Count == 0) return null;

			BestHandResult best = null;
			foreach (var hand in decompositions)
			{
				int dora = CountDora(hand, ankans, concealedFourteen, doraTiles);
				var yaku = YakuEvaluator.Evaluate(hand, riichi, dora);
				if (!yaku.HasAnyYaku) continue;
				if (best == null || Better(yaku, best.Yaku))
					best = new BestHandResult { Hand = hand, Yaku = yaku };
			}
			return best;
		}

		static bool Better(YakuResult a, YakuResult b)
		{
			if (a.YakumanMultiplier != b.YakumanMultiplier) return a.YakumanMultiplier > b.YakumanMultiplier;
			return a.TotalHan > b.TotalHan;
		}

		static int CountDora(WinningHand hand, IReadOnlyList<Meld> ankans, List<Tile> concealedFourteen, IReadOnlyList<Tile> doraTiles)
		{
			if (doraTiles == null) return 0;
			int count = 0;
			// 모든 손패(닫힌 + 안깡 4장)에 대해 도라 종류 일치 카운트, 빨간5는 1장씩 추가
			foreach (var t in concealedFourteen)
			{
				foreach (var d in doraTiles) if (t.SameKind(d)) count++;
				if (t.IsRedFive) count++;
			}
			if (ankans != null)
			{
				foreach (var meld in ankans)
				{
					foreach (var t in meld.Tiles)
					{
						foreach (var d in doraTiles) if (t.SameKind(d)) count++;
					}
				}
			}
			return count;
		}
	}
}
