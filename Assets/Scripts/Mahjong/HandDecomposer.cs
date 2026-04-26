using System.Collections.Generic;

namespace Mahjong
{
	/// <summary>
	/// 14장(또는 14-깡보정) 손패에서 가능한 모든 화료 분해 열거.
	/// 입력은 손패(닫힌+쯔모) + 안깡 리스트.
	/// 안깡은 평가 시 면자 4개 중 하나로 자동 포함.
	/// </summary>
	public static class HandDecomposer
	{
		public static List<WinningHand> Enumerate(List<Tile> concealedFourteen, IReadOnlyList<Meld> ankans, Tile winningTile, bool tsumo)
		{
			var results = new List<WinningHand>();
			var counts = TileIndex.Counts(concealedFourteen);
			int closedCount = concealedFourteen.Count;
			int ankanCount = ankans?.Count ?? 0;
			int meldsNeeded = 4 - ankanCount;

			// 七対子: 14장 닫힌 + 안깡 0개일 때만
			if (closedCount == 14 && ankanCount == 0)
			{
				int pairs = 0;
				bool valid = true;
				var pairList = new List<Tile>();
				for (int i = 0; i < TileIndex.Count; i++)
				{
					if (counts[i] == 0) continue;
					if (counts[i] == 2) { pairs++; pairList.Add(TileIndex.FromIndex(i)); }
					else { valid = false; break; }
				}
				if (valid && pairs == 7)
				{
					var w = new WinningHand
					{
						Shape = WinShape.Chiitoitsu,
						Pair = pairList[0],
						Pairs = pairList,
						TsumoWin = tsumo,
						MenzenClosed = true,
						WinningTile = winningTile
					};
					results.Add(w);
				}

				// 国士無双: 13요구패 각 1 + 1요구패 추가
				if (TryKokushi(counts, winningTile, tsumo, out var k))
					results.Add(k);
			}

			// 표준형: pair 후보 각각에 대해 melds 분해 시도
			for (int pairIdx = 0; pairIdx < TileIndex.Count; pairIdx++)
			{
				if (counts[pairIdx] < 2) continue;
				counts[pairIdx] -= 2;
				var seenSets = new HashSet<string>();
				var meldsAcc = new List<Meld>();
				DecomposeStandard(counts, 0, meldsNeeded, meldsAcc, results, TileIndex.FromIndex(pairIdx), winningTile, tsumo, ankans, seenSets);
				counts[pairIdx] += 2;
			}
			return results;
		}

		static bool TryKokushi(int[] counts, Tile winningTile, bool tsumo, out WinningHand w)
		{
			w = null;
			int[] terminalsHonors = { 0, 8, 9, 17, 18, 26, 27, 28, 29, 30, 31, 32, 33 };
			int total = 0, pairFound = 0;
			foreach (int idx in terminalsHonors)
			{
				if (counts[idx] == 0) return false;
				if (counts[idx] >= 2) pairFound++;
				total += counts[idx];
			}
			// 다른 인덱스 0이어야 함
			for (int i = 0; i < TileIndex.Count; i++)
			{
				bool isTermHonor = false;
				foreach (var t in terminalsHonors) if (t == i) { isTermHonor = true; break; }
				if (!isTermHonor && counts[i] != 0) return false;
			}
			if (total != 14 || pairFound != 1) return false;
			w = new WinningHand
			{
				Shape = WinShape.Kokushi,
				WinningTile = winningTile,
				TsumoWin = tsumo,
				MenzenClosed = true
			};
			return true;
		}

		static void DecomposeStandard(int[] counts, int startIdx, int meldsNeeded,
			List<Meld> acc, List<WinningHand> results, Tile pair, Tile winningTile, bool tsumo,
			IReadOnlyList<Meld> ankans, HashSet<string> seenSets)
		{
			if (acc.Count == meldsNeeded)
			{
				bool empty = true;
				for (int i = 0; i < TileIndex.Count; i++) if (counts[i] != 0) { empty = false; break; }
				if (!empty) return;

				// 중복 분해 제거 (정렬 키)
				var key = MeldKey(acc, pair, ankans);
				if (!seenSets.Add(key)) return;

				var w = new WinningHand
				{
					Shape = WinShape.Standard,
					Pair = pair,
					Melds = new List<Meld>(acc),
					WinningTile = winningTile,
					TsumoWin = tsumo,
					AnkanCount = ankans?.Count ?? 0,
					MenzenClosed = true // 본 게임은 호출 없음 → 항상 멘젠
				};
				if (ankans != null) foreach (var a in ankans) w.Melds.Add(a);
				results.Add(w);
				return;
			}

			int idx = startIdx;
			while (idx < TileIndex.Count && counts[idx] == 0) idx++;
			if (idx >= TileIndex.Count) return;

			// 커쯔 시도
			if (counts[idx] >= 3)
			{
				counts[idx] -= 3;
				acc.Add(new Meld(MeldKind.Koutsu, TileIndex.FromIndex(idx)));
				DecomposeStandard(counts, idx, meldsNeeded, acc, results, pair, winningTile, tsumo, ankans, seenSets);
				acc.RemoveAt(acc.Count - 1);
				counts[idx] += 3;
			}

			// 슌쯔 시도 (수패 + value <= 7)
			if (TileIndex.IsNumber(idx) && TileIndex.NumberValue(idx) <= 7)
			{
				if (counts[idx] >= 1 && counts[idx + 1] >= 1 && counts[idx + 2] >= 1)
				{
					counts[idx]--; counts[idx + 1]--; counts[idx + 2]--;
					acc.Add(new Meld(MeldKind.Shuntsu, TileIndex.FromIndex(idx)));
					DecomposeStandard(counts, idx, meldsNeeded, acc, results, pair, winningTile, tsumo, ankans, seenSets);
					acc.RemoveAt(acc.Count - 1);
					counts[idx]++; counts[idx + 1]++; counts[idx + 2]++;
				}
			}
		}

		static string MeldKey(List<Meld> acc, Tile pair, IReadOnlyList<Meld> ankans)
		{
			var keys = new List<string>(acc.Count + (ankans?.Count ?? 0));
			foreach (var m in acc) keys.Add($"{(int)m.Kind}-{TileIndex.Of(m.First)}");
			if (ankans != null) foreach (var m in ankans) keys.Add($"K-{TileIndex.Of(m.First)}");
			keys.Sort();
			return $"P{TileIndex.Of(pair)}|{string.Join(",", keys)}";
		}
	}
}
