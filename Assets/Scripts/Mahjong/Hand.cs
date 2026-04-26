using System.Collections.Generic;

namespace Mahjong
{
	public enum MeldKind { Shuntsu, Koutsu, Kantsu }

	public readonly struct Meld
	{
		public readonly MeldKind Kind;
		public readonly Tile First; // 슌쯔 시작패 / 커쯔·캉쯔 종류패

		public Meld(MeldKind kind, Tile first) { Kind = kind; First = first; }

		public IEnumerable<Tile> Tiles
		{
			get
			{
				switch (Kind)
				{
					case MeldKind.Shuntsu:
						yield return First;
						yield return new Tile(First.Suit, First.Value + 1);
						yield return new Tile(First.Suit, First.Value + 2);
						break;
					case MeldKind.Koutsu:
						yield return First; yield return First; yield return First;
						break;
					case MeldKind.Kantsu:
						yield return First; yield return First; yield return First; yield return First;
						break;
				}
			}
		}
	}

	/// <summary>
	/// 닫힌 손패(13장) + 임시 쯔모 1장(있다면) + 안깡 그룹 리스트.
	/// 본 게임은 호출 없음 → 명깡·치·퐁 없음. 안깡만 가능.
	/// </summary>
	public class Hand
	{
		readonly List<Tile> closed = new List<Tile>(14);
		readonly List<Meld> ankans = new List<Meld>();
		Tile? draw;

		public IReadOnlyList<Tile> Closed => closed;
		public IReadOnlyList<Meld> Ankans => ankans;
		public Tile? Draw => draw;

		/// <summary>닫힌 손패 + 쯔모를 합친 14장(또는 깡 보정 후 13장).</summary>
		public int TotalTileCount => closed.Count + (draw.HasValue ? 1 : 0) + ankans.Count * 4;

		public void DealInitial(IList<Tile> tiles)
		{
			closed.Clear();
			ankans.Clear();
			draw = null;
			foreach (var t in tiles) closed.Add(t);
			closed.Sort();
		}

		public void SetDraw(Tile tile)
		{
			draw = tile;
		}

		public bool Discard(Tile tile)
		{
			if (draw.HasValue && draw.Value.SameKind(tile))
			{
				draw = null;
				return true;
			}
			int idx = closed.FindIndex(t => t.SameKind(tile));
			if (idx < 0) return false;
			closed.RemoveAt(idx);
			if (draw.HasValue)
			{
				closed.Add(draw.Value);
				closed.Sort();
				draw = null;
			}
			return true;
		}

		/// <summary>안깡 가능한 4장 동일 패 종류 후보.</summary>
		public IEnumerable<Tile> AnkanCandidates()
		{
			var counts = new Dictionary<(Suit, int), int>();
			foreach (var t in closed)
			{
				var key = (t.Suit, t.Value);
				counts[key] = counts.GetValueOrDefault(key) + 1;
			}
			if (draw.HasValue)
			{
				var k = (draw.Value.Suit, draw.Value.Value);
				counts[k] = counts.GetValueOrDefault(k) + 1;
			}
			foreach (var kv in counts)
				if (kv.Value == 4)
					yield return new Tile(kv.Key.Item1, kv.Key.Item2);
		}

		public bool DeclareAnkan(Tile kind)
		{
			int removed = 0;
			for (int i = closed.Count - 1; i >= 0 && removed < 4; i--)
			{
				if (closed[i].SameKind(kind)) { closed.RemoveAt(i); removed++; }
			}
			if (draw.HasValue && draw.Value.SameKind(kind) && removed < 4)
			{
				draw = null;
				removed++;
			}
			if (removed != 4) return false;
			ankans.Add(new Meld(MeldKind.Kantsu, kind));
			return true;
		}

		/// <summary>화료 평가용 14장(닫힌 + 쯔모 + 안깡 풀어서 면자 처리는 평가기에서).</summary>
		public List<Tile> ConcealedFourteen()
		{
			var list = new List<Tile>(closed);
			if (draw.HasValue) list.Add(draw.Value);
			list.Sort();
			return list;
		}
	}
}
