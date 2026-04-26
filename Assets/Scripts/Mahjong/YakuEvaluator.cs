using System.Collections.Generic;
using System.Linq;

namespace Mahjong
{
	public enum YakuId
	{
		Riichi, MenzenTsumo, Pinfu, Tanyao, Iipeikou, YakuhaiHaku, YakuhaiHatsu, YakuhaiChun,
		SanshokuDoujun, Ittsu, Chanta, Chiitoitsu, Toitoi, SanAnkou, SanshokuDoukou, SanKantsu, Shousangen, Honroutou,
		Ryanpeikou, Junchan, Honitsu,
		Chinitsu,
		// 役満
		Kokushi, Suuankou, Daisangen, Tsuuiisou, Ryuuiisou, Chinroutou, Daisuushii, Shousuushii, ChuurenPoutou, Suukantsu
	}

	public readonly struct YakuHit
	{
		public readonly YakuId Id;
		public readonly int Han;
		public readonly bool IsYakuman;
		public YakuHit(YakuId id, int han, bool yakuman = false) { Id = id; Han = han; IsYakuman = yakuman; }
	}

	public class YakuResult
	{
		public List<YakuHit> Hits = new List<YakuHit>();
		public int DoraCount;
		public int TotalHan;            // 야쿠만 아닐 때 한+도라 합
		public int YakumanMultiplier;   // 야쿠만 개수 (싱글=1, 더블=2, …)
		public bool HasAnyYaku => Hits.Any(h => h.IsYakuman) || Hits.Any(h => !h.IsYakuman);
	}

	/// <summary>
	/// 화료 분해 1개 + 외부 컨텍스트(리치, 도라 수)에서 야쿠 평가.
	/// 본 게임 단순화: 一発·자풍/장풍·天和·우라도라·海底/河底·嶺上開花·槍槓 미구현.
	/// </summary>
	public static class YakuEvaluator
	{
		public static YakuResult Evaluate(WinningHand hand, bool riichi, int doraCount)
		{
			var r = new YakuResult { DoraCount = doraCount };

			if (hand.Shape == WinShape.Kokushi)
			{
				r.Hits.Add(new YakuHit(YakuId.Kokushi, 13, true));
				FinalizeYakuman(r);
				return r;
			}
			if (hand.Shape == WinShape.Chiitoitsu)
			{
				r.Hits.Add(new YakuHit(YakuId.Chiitoitsu, 2));
				if (riichi) r.Hits.Add(new YakuHit(YakuId.Riichi, 1));
				if (hand.TsumoWin) r.Hits.Add(new YakuHit(YakuId.MenzenTsumo, 1));
				if (AllTanyao(hand)) r.Hits.Add(new YakuHit(YakuId.Tanyao, 1));
				if (Honroutou(hand)) r.Hits.Add(new YakuHit(YakuId.Honroutou, 2));
				if (Honitsu(hand, out _)) r.Hits.Add(new YakuHit(YakuId.Honitsu, 3));
				if (Chinitsu(hand)) r.Hits.Add(new YakuHit(YakuId.Chinitsu, 6));
				if (Tsuuiisou(hand)) { r.Hits.Add(new YakuHit(YakuId.Tsuuiisou, 13, true)); FinalizeYakuman(r); return r; }
				FinalizeNormal(r);
				return r;
			}

			// === 표준형 ===
			// 야쿠만 우선 검사
			bool yakuman = false;
			if (Suuankou(hand)) { r.Hits.Add(new YakuHit(YakuId.Suuankou, 13, true)); yakuman = true; }
			if (Daisangen(hand)) { r.Hits.Add(new YakuHit(YakuId.Daisangen, 13, true)); yakuman = true; }
			if (Tsuuiisou(hand)) { r.Hits.Add(new YakuHit(YakuId.Tsuuiisou, 13, true)); yakuman = true; }
			if (Ryuuiisou(hand)) { r.Hits.Add(new YakuHit(YakuId.Ryuuiisou, 13, true)); yakuman = true; }
			if (Chinroutou(hand)) { r.Hits.Add(new YakuHit(YakuId.Chinroutou, 13, true)); yakuman = true; }
			if (Daisuushii(hand)) { r.Hits.Add(new YakuHit(YakuId.Daisuushii, 26, true)); yakuman = true; }
			else if (Shousuushii(hand)) { r.Hits.Add(new YakuHit(YakuId.Shousuushii, 13, true)); yakuman = true; }
			if (ChuurenPoutou(hand)) { r.Hits.Add(new YakuHit(YakuId.ChuurenPoutou, 13, true)); yakuman = true; }
			if (Suukantsu(hand)) { r.Hits.Add(new YakuHit(YakuId.Suukantsu, 13, true)); yakuman = true; }
			if (yakuman) { FinalizeYakuman(r); return r; }

			if (riichi) r.Hits.Add(new YakuHit(YakuId.Riichi, 1));
			if (hand.TsumoWin) r.Hits.Add(new YakuHit(YakuId.MenzenTsumo, 1));
			if (Pinfu(hand)) r.Hits.Add(new YakuHit(YakuId.Pinfu, 1));
			if (AllTanyao(hand)) r.Hits.Add(new YakuHit(YakuId.Tanyao, 1));
			int peikou = PeikouCount(hand);
			if (peikou == 1) r.Hits.Add(new YakuHit(YakuId.Iipeikou, 1));
			else if (peikou == 2) r.Hits.Add(new YakuHit(YakuId.Ryanpeikou, 3));

			// 役牌 三元
			foreach (var m in hand.Melds)
			{
				if ((m.Kind == MeldKind.Koutsu || m.Kind == MeldKind.Kantsu) && m.First.Suit == Suit.Dragon)
				{
					switch (m.First.Value)
					{
						case 1: r.Hits.Add(new YakuHit(YakuId.YakuhaiHaku, 1)); break;
						case 2: r.Hits.Add(new YakuHit(YakuId.YakuhaiHatsu, 1)); break;
						case 3: r.Hits.Add(new YakuHit(YakuId.YakuhaiChun, 1)); break;
					}
				}
			}

			if (SanshokuDoujun(hand)) r.Hits.Add(new YakuHit(YakuId.SanshokuDoujun, 2));
			if (Ittsu(hand)) r.Hits.Add(new YakuHit(YakuId.Ittsu, 2));
			bool junchan = Junchan(hand);
			if (junchan) r.Hits.Add(new YakuHit(YakuId.Junchan, 3));
			else if (Chanta(hand)) r.Hits.Add(new YakuHit(YakuId.Chanta, 2));
			if (Toitoi(hand)) r.Hits.Add(new YakuHit(YakuId.Toitoi, 2));
			int ankouCount = ConcealedKoutsuCount(hand);
			if (ankouCount == 3) r.Hits.Add(new YakuHit(YakuId.SanAnkou, 2));
			if (SanshokuDoukou(hand)) r.Hits.Add(new YakuHit(YakuId.SanshokuDoukou, 2));
			int kantsuCount = hand.Melds.Count(m => m.Kind == MeldKind.Kantsu);
			if (kantsuCount == 3) r.Hits.Add(new YakuHit(YakuId.SanKantsu, 2));
			if (Shousangen(hand)) r.Hits.Add(new YakuHit(YakuId.Shousangen, 2));
			if (Honroutou(hand)) r.Hits.Add(new YakuHit(YakuId.Honroutou, 2));
			if (Chinitsu(hand)) r.Hits.Add(new YakuHit(YakuId.Chinitsu, 6));
			else if (Honitsu(hand, out _)) r.Hits.Add(new YakuHit(YakuId.Honitsu, 3));

			FinalizeNormal(r);
			return r;
		}

		static void FinalizeNormal(YakuResult r)
		{
			r.YakumanMultiplier = 0;
			r.TotalHan = r.Hits.Sum(h => h.Han) + r.DoraCount;
		}

		static void FinalizeYakuman(YakuResult r)
		{
			r.YakumanMultiplier = r.Hits.Where(h => h.IsYakuman).Sum(h => h.Han / 13);
			if (r.YakumanMultiplier <= 0) r.YakumanMultiplier = 1;
			r.TotalHan = 13 * r.YakumanMultiplier;
		}

		// === 야쿠 판정 헬퍼 ===

		static bool AllTanyao(WinningHand h)
		{
			if (h.Shape == WinShape.Chiitoitsu)
				return h.Pairs.All(p => !p.IsTerminalOrHonor);
			if (h.Pair.IsTerminalOrHonor) return false;
			foreach (var m in h.Melds)
			{
				foreach (var t in m.Tiles) if (t.IsTerminalOrHonor) return false;
			}
			return true;
		}

		static bool Pinfu(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			if (h.AnkanCount > 0) return false;
			foreach (var m in h.Melds) if (m.Kind != MeldKind.Shuntsu) return false;
			// 머리가 役패(三元)이면 X. 자/장풍은 미구현이라 풍은 모두 OK
			if (h.Pair.Suit == Suit.Dragon) return false;
			// 양면대기 판정 단순화: 통과
			return true;
		}

		static int PeikouCount(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return 0;
			var shun = h.Melds.Where(m => m.Kind == MeldKind.Shuntsu).ToList();
			int pairs = 0;
			var used = new bool[shun.Count];
			for (int i = 0; i < shun.Count; i++)
			{
				if (used[i]) continue;
				for (int j = i + 1; j < shun.Count; j++)
				{
					if (used[j]) continue;
					if (shun[i].First.SameKind(shun[j].First)) { pairs++; used[i] = used[j] = true; break; }
				}
			}
			return pairs;
		}

		static bool SanshokuDoujun(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			var shun = h.Melds.Where(m => m.Kind == MeldKind.Shuntsu).ToList();
			for (int v = 1; v <= 7; v++)
			{
				bool m = shun.Any(s => s.First.Suit == Suit.Man && s.First.Value == v);
				bool p = shun.Any(s => s.First.Suit == Suit.Pin && s.First.Value == v);
				bool s2 = shun.Any(s => s.First.Suit == Suit.Sou && s.First.Value == v);
				if (m && p && s2) return true;
			}
			return false;
		}

		static bool Ittsu(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			var shun = h.Melds.Where(m => m.Kind == MeldKind.Shuntsu).ToList();
			foreach (Suit s in new[] { Suit.Man, Suit.Pin, Suit.Sou })
			{
				bool a = shun.Any(m => m.First.Suit == s && m.First.Value == 1);
				bool b = shun.Any(m => m.First.Suit == s && m.First.Value == 4);
				bool c = shun.Any(m => m.First.Suit == s && m.First.Value == 7);
				if (a && b && c) return true;
			}
			return false;
		}

		static bool MeldHasTerminalOrHonor(Meld m)
		{
			foreach (var t in m.Tiles) if (t.IsTerminalOrHonor) return true;
			return false;
		}
		static bool MeldHasHonor(Meld m)
		{
			foreach (var t in m.Tiles) if (t.IsHonor) return true;
			return false;
		}
		static bool MeldHasTerminal(Meld m)
		{
			foreach (var t in m.Tiles) if (t.IsTerminal) return true;
			return false;
		}

		static bool Chanta(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			if (!h.Pair.IsTerminalOrHonor) return false;
			bool hasShuntsu = false;
			foreach (var m in h.Melds)
			{
				if (!MeldHasTerminalOrHonor(m)) return false;
				if (m.Kind == MeldKind.Shuntsu) hasShuntsu = true;
			}
			return hasShuntsu;
		}

		static bool Junchan(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			if (h.Pair.IsHonor || !h.Pair.IsTerminal) return false;
			bool hasShuntsu = false;
			foreach (var m in h.Melds)
			{
				bool hasTerm = false;
				foreach (var t in m.Tiles)
				{
					if (t.IsHonor) return false;
					if (t.IsTerminal) hasTerm = true;
				}
				if (!hasTerm) return false;
				if (m.Kind == MeldKind.Shuntsu) hasShuntsu = true;
			}
			return hasShuntsu;
		}

		static bool Toitoi(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			foreach (var m in h.Melds) if (m.Kind == MeldKind.Shuntsu) return false;
			return true;
		}

		/// <summary>안커 + 안깡 카운트. 본 게임은 호출 없음 → 모든 커쯔가 안커.</summary>
		static int ConcealedKoutsuCount(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return 0;
			return h.Melds.Count(m => m.Kind == MeldKind.Koutsu || m.Kind == MeldKind.Kantsu);
		}

		static bool Suuankou(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			return ConcealedKoutsuCount(h) == 4;
		}

		static bool SanshokuDoukou(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			var ko = h.Melds.Where(m => m.Kind == MeldKind.Koutsu || m.Kind == MeldKind.Kantsu).ToList();
			for (int v = 1; v <= 9; v++)
			{
				bool m = ko.Any(k => k.First.Suit == Suit.Man && k.First.Value == v);
				bool p = ko.Any(k => k.First.Suit == Suit.Pin && k.First.Value == v);
				bool s = ko.Any(k => k.First.Suit == Suit.Sou && k.First.Value == v);
				if (m && p && s) return true;
			}
			return false;
		}

		static bool Daisangen(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			int dragonKo = h.Melds.Count(m => (m.Kind == MeldKind.Koutsu || m.Kind == MeldKind.Kantsu) && m.First.Suit == Suit.Dragon);
			return dragonKo == 3;
		}

		static bool Shousangen(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			int dragonKo = h.Melds.Count(m => (m.Kind == MeldKind.Koutsu || m.Kind == MeldKind.Kantsu) && m.First.Suit == Suit.Dragon);
			bool dragonPair = h.Pair.Suit == Suit.Dragon;
			return dragonKo == 2 && dragonPair;
		}

		static bool Honroutou(WinningHand h)
		{
			if (h.Shape == WinShape.Chiitoitsu)
				return h.Pairs.All(p => p.IsTerminalOrHonor) && h.Pairs.Any(p => p.IsTerminal) && h.Pairs.Any(p => p.IsHonor || p.IsTerminal);
			if (h.Shape != WinShape.Standard) return false;
			if (!h.Pair.IsTerminalOrHonor) return false;
			foreach (var m in h.Melds)
			{
				if (m.Kind == MeldKind.Shuntsu) return false;
				foreach (var t in m.Tiles) if (!t.IsTerminalOrHonor) return false;
			}
			return true;
		}

		static bool Honitsu(WinningHand h, out int suitFound)
		{
			suitFound = -1;
			Suit? suit = null;
			bool hasHonor = false;
			System.Action<Tile> consider = t =>
			{
				if (t.IsHonor) hasHonor = true;
				else { if (suit == null) suit = t.Suit; }
			};
			if (h.Shape == WinShape.Chiitoitsu)
			{
				foreach (var p in h.Pairs) { consider(p); if (!p.IsHonor && suit != null && p.Suit != suit) return false; }
			}
			else
			{
				consider(h.Pair);
				if (!h.Pair.IsHonor && suit != null && h.Pair.Suit != suit) return false;
				foreach (var m in h.Melds)
				{
					foreach (var t in m.Tiles)
					{
						consider(t);
						if (!t.IsHonor && suit != null && t.Suit != suit) return false;
					}
				}
			}
			if (suit == null || !hasHonor) return false;
			suitFound = (int)suit.Value;
			return true;
		}

		static bool Chinitsu(WinningHand h)
		{
			Suit? suit = null;
			System.Func<Tile, bool> ok = t =>
			{
				if (t.IsHonor) return false;
				if (suit == null) { suit = t.Suit; return true; }
				return t.Suit == suit;
			};
			if (h.Shape == WinShape.Chiitoitsu)
			{
				foreach (var p in h.Pairs) if (!ok(p)) return false;
			}
			else
			{
				if (!ok(h.Pair)) return false;
				foreach (var m in h.Melds) foreach (var t in m.Tiles) if (!ok(t)) return false;
			}
			return suit != null;
		}

		static bool Tsuuiisou(WinningHand h)
		{
			if (h.Shape == WinShape.Chiitoitsu) return h.Pairs.All(p => p.IsHonor);
			if (h.Shape != WinShape.Standard) return false;
			if (!h.Pair.IsHonor) return false;
			foreach (var m in h.Melds) foreach (var t in m.Tiles) if (!t.IsHonor) return false;
			return true;
		}

		static bool Ryuuiisou(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			System.Func<Tile, bool> green = t =>
				(t.Suit == Suit.Sou && (t.Value == 2 || t.Value == 3 || t.Value == 4 || t.Value == 6 || t.Value == 8))
				|| (t.Suit == Suit.Dragon && t.Value == 2);
			if (!green(h.Pair)) return false;
			foreach (var m in h.Melds) foreach (var t in m.Tiles) if (!green(t)) return false;
			return true;
		}

		static bool Chinroutou(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			if (!h.Pair.IsTerminal) return false;
			foreach (var m in h.Melds)
			{
				if (m.Kind == MeldKind.Shuntsu) return false;
				foreach (var t in m.Tiles) if (!t.IsTerminal) return false;
			}
			return true;
		}

		static bool Daisuushii(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			int windKo = h.Melds.Count(m => (m.Kind == MeldKind.Koutsu || m.Kind == MeldKind.Kantsu) && m.First.Suit == Suit.Wind);
			return windKo == 4;
		}

		static bool Shousuushii(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			int windKo = h.Melds.Count(m => (m.Kind == MeldKind.Koutsu || m.Kind == MeldKind.Kantsu) && m.First.Suit == Suit.Wind);
			return windKo == 3 && h.Pair.Suit == Suit.Wind;
		}

		static bool ChuurenPoutou(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			if (h.AnkanCount > 0) return false;
			Suit? suit = null;
			var counts = new int[10];
			System.Action<Tile> add = t =>
			{
				if (t.IsHonor) return;
				if (suit == null) suit = t.Suit;
				if (t.Suit == suit) counts[t.Value]++;
			};
			foreach (var m in h.Melds) foreach (var t in m.Tiles) add(t);
			add(h.Pair); add(h.Pair);
			if (suit == null) return false;
			// 1112345678999 패턴 + 추가 1장
			int[] need = { 0, 3, 1, 1, 1, 1, 1, 1, 1, 3 };
			int extra = 0;
			for (int i = 1; i <= 9; i++)
			{
				if (counts[i] < need[i]) return false;
				extra += counts[i] - need[i];
			}
			return extra == 1;
		}

		static bool Suukantsu(WinningHand h)
		{
			if (h.Shape != WinShape.Standard) return false;
			return h.Melds.Count(m => m.Kind == MeldKind.Kantsu) == 4;
		}
	}
}
