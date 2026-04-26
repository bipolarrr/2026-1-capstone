using System.Collections.Generic;

namespace Mahjong
{
	/// <summary>한 라운드(=한 전투) 단위 상태 컨테이너.</summary>
	public class MahjongMatchState
	{
		public TileWall Wall;
		public Hand PlayerHand;
		public List<Tile> Discards = new List<Tile>(64);
		public bool RiichiDeclared;
		public int InitialHandSize = 13;
		public int DoraReserveCount = 5;

		public MahjongMatchState(int seed)
		{
			Wall = new TileWall(seed);
			Wall.ReserveDoraIndicators(DoraReserveCount);
			PlayerHand = new Hand();

			var initial = new List<Tile>(InitialHandSize);
			for (int i = 0; i < InitialHandSize; i++)
			{
				if (Wall.TryDraw(out var t)) initial.Add(t);
			}
			PlayerHand.DealInitial(initial);
		}

		public bool DrawNext()
		{
			if (Wall.TryDraw(out var t))
			{
				PlayerHand.SetDraw(t);
				return true;
			}
			return false;
		}

		public bool DiscardFromHand(Tile tile)
		{
			if (!PlayerHand.Discard(tile)) return false;
			Discards.Add(tile);
			return true;
		}
	}
}
