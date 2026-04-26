using System.Collections.Generic;

namespace Mahjong
{
	public class PartialBreakdown
	{
		public int Shuntsu;
		public int Koutsu;
		public int Kantsu;
		public int Pair;
		public int TotalMelds => Shuntsu + Koutsu + Kantsu;
	}

	/// <summary>
	/// 중간 포기 공격용. 현재 손패에서 최대 (멘츠 수 + 머리) 조합을 그리디 + 백트래킹으로 선택.
	/// </summary>
	public static class PartialHandEvaluator
	{
		public static PartialBreakdown Evaluate(List<Tile> tiles, IReadOnlyList<Meld> ankans = null)
		{
			var counts = TileIndex.Counts(tiles);
			var best = new PartialBreakdown();
			Search(counts, 0, new PartialBreakdown(), best, false);
			if (ankans != null) best.Kantsu += ankans.Count;
			return best;
		}

		static void Search(int[] counts, int startIdx, PartialBreakdown cur, PartialBreakdown best, bool pairUsed)
		{
			if (Score(cur) > Score(best))
			{
				best.Shuntsu = cur.Shuntsu;
				best.Koutsu = cur.Koutsu;
				best.Kantsu = cur.Kantsu;
				best.Pair = cur.Pair;
			}

			int idx = startIdx;
			while (idx < TileIndex.Count && counts[idx] == 0) idx++;
			if (idx >= TileIndex.Count) return;

			// 깡(4장)
			if (counts[idx] >= 4)
			{
				counts[idx] -= 4; cur.Kantsu++;
				Search(counts, idx, cur, best, pairUsed);
				cur.Kantsu--; counts[idx] += 4;
			}
			// 커쯔(3장)
			if (counts[idx] >= 3)
			{
				counts[idx] -= 3; cur.Koutsu++;
				Search(counts, idx, cur, best, pairUsed);
				cur.Koutsu--; counts[idx] += 3;
			}
			// 슌쯔
			if (TileIndex.IsNumber(idx) && TileIndex.NumberValue(idx) <= 7
				&& counts[idx] >= 1 && counts[idx + 1] >= 1 && counts[idx + 2] >= 1)
			{
				counts[idx]--; counts[idx + 1]--; counts[idx + 2]--;
				cur.Shuntsu++;
				Search(counts, idx, cur, best, pairUsed);
				cur.Shuntsu--;
				counts[idx]++; counts[idx + 1]++; counts[idx + 2]++;
			}
			// 머리(2장) — 한 번만
			if (!pairUsed && counts[idx] >= 2)
			{
				counts[idx] -= 2; cur.Pair++;
				Search(counts, idx + 1, cur, best, true);
				cur.Pair--; counts[idx] += 2;
			}
			// 이 idx 패스
			int saved = counts[idx];
			counts[idx] = 0;
			Search(counts, idx + 1, cur, best, pairUsed);
			counts[idx] = saved;
		}

		static int Score(PartialBreakdown b) => b.TotalMelds * 4 + b.Pair * 1;
	}
}
