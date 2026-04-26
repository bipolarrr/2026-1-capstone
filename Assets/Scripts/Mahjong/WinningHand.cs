using System.Collections.Generic;

namespace Mahjong
{
	public enum WinShape { Standard, Chiitoitsu, Kokushi }

	/// <summary>화료 형태 1종에 대한 분해 결과. 도라/자풍 보너스는 야쿠 평가 단계에서 추가 합산.</summary>
	public class WinningHand
	{
		public WinShape Shape;
		public List<Meld> Melds = new List<Meld>();   // 표준형 4면자 (안깡 포함)
		public Tile Pair;                             // 표준형/七対子의 대표 머리(七対子는 첫 페어로 둠)
		public List<Tile> Pairs = new List<Tile>();   // 七対子 7페어
		public bool TsumoWin;                         // 쯔모(자가)
		public bool MenzenClosed;                     // 멘젠 여부 (안깡만 있을 때 멘젠 유지)
		public Tile WinningTile;                      // 화료 패
		public int AnkanCount;                        // 안깡 개수 (표준형 분해 시)

		public IEnumerable<Meld> AllMelds => Melds;
	}
}
