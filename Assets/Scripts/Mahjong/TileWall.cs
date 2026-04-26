using System;
using System.Collections.Generic;

namespace Mahjong
{
	/// <summary>
	/// 1인 전투형 패산. 왕패 없음. 도라 N장은 라운드 시작 시 별도 보관(인디케이터).
	/// 깡 영상수 보충은 패산 끝에서 1장 가져온다.
	/// </summary>
	public class TileWall
	{
		readonly List<Tile> tiles;
		readonly List<Tile> doraIndicators = new List<Tile>();
		int drawIndex;
		int rinshanIndex;

		public IReadOnlyList<Tile> DoraIndicators => doraIndicators;
		public int Remaining => rinshanIndex - drawIndex + 1;
		public int TotalCount => tiles.Count;

		public TileWall(int seed = 0) : this(TileFactory.BuildStandardSet(), seed) { }

		public TileWall(List<Tile> sourceSet, int seed)
		{
			tiles = new List<Tile>(sourceSet);
			Shuffle(tiles, new Random(seed == 0 ? Environment.TickCount : seed));
			drawIndex = 0;
			rinshanIndex = tiles.Count - 1;
		}

		public void ReserveDoraIndicators(int count = 5)
		{
			if (doraIndicators.Count > 0) throw new InvalidOperationException("도라 이미 예약됨");
			if (count < 0 || count > tiles.Count - 14) throw new ArgumentOutOfRangeException(nameof(count));
			for (int i = 0; i < count; i++)
				doraIndicators.Add(tiles[rinshanIndex - i]);
			// 도라는 인디케이터로만 사용. 패산에서 빼지 않고 끝쪽을 가린다.
			rinshanIndex -= count;
		}

		public bool TryDraw(out Tile tile)
		{
			if (drawIndex > rinshanIndex)
			{
				tile = default;
				return false;
			}
			tile = tiles[drawIndex++];
			return true;
		}

		/// <summary>깡 후 영상수 보충. 본 게임은 왕패 없음 → 패산 끝에서 1장 가져옴.</summary>
		public bool TryDrawRinshan(out Tile tile)
		{
			if (rinshanIndex < drawIndex)
			{
				tile = default;
				return false;
			}
			tile = tiles[rinshanIndex--];
			return true;
		}

		static void Shuffle<T>(IList<T> list, Random rng)
		{
			for (int i = list.Count - 1; i > 0; i--)
			{
				int j = rng.Next(i + 1);
				(list[i], list[j]) = (list[j], list[i]);
			}
		}

		/// <summary>도라 인디케이터로부터 실제 도라패 환산.</summary>
		public IEnumerable<Tile> GetDoraTiles()
		{
			foreach (var ind in doraIndicators)
				yield return ind.NextForDora();
		}
	}
}
