using System;
using System.Collections.Generic;

namespace Mahjong
{
	public enum Suit
	{
		Man,    // 萬子 1-9
		Pin,    // 筒子 1-9
		Sou,    // 索子 1-9
		Wind,   // 風牌 1=東 2=南 3=西 4=北
		Dragon  // 三元 1=白 2=發 3=中
	}

	[Serializable]
	public readonly struct Tile : IEquatable<Tile>, IComparable<Tile>
	{
		public readonly Suit Suit;
		public readonly int Value;
		public readonly bool IsRedFive;

		public Tile(Suit suit, int value, bool isRedFive = false)
		{
			Suit = suit;
			Value = value;
			IsRedFive = isRedFive;
		}

		public bool IsHonor => Suit == Suit.Wind || Suit == Suit.Dragon;
		public bool IsNumber => Suit == Suit.Man || Suit == Suit.Pin || Suit == Suit.Sou;

		// 요구패(1·9·자패)
		public bool IsTerminalOrHonor => IsHonor || (IsNumber && (Value == 1 || Value == 9));
		// 노두패(1·9 수패만)
		public bool IsTerminal => IsNumber && (Value == 1 || Value == 9);

		// 도라 인디케이터의 다음 패. 수패 9→1, 자패는 동→남→…→북→동, 백→發→中→백
		public Tile NextForDora()
		{
			switch (Suit)
			{
				case Suit.Man:
				case Suit.Pin:
				case Suit.Sou:
					return new Tile(Suit, Value == 9 ? 1 : Value + 1);
				case Suit.Wind:
					return new Tile(Suit, Value == 4 ? 1 : Value + 1);
				case Suit.Dragon:
					return new Tile(Suit, Value == 3 ? 1 : Value + 1);
			}
			return this;
		}

		public bool SameKind(Tile other) => Suit == other.Suit && Value == other.Value;

		public bool Equals(Tile other) => Suit == other.Suit && Value == other.Value && IsRedFive == other.IsRedFive;
		public override bool Equals(object obj) => obj is Tile t && Equals(t);
		public override int GetHashCode() => ((int)Suit * 31 + Value) * 2 + (IsRedFive ? 1 : 0);

		public int CompareTo(Tile other)
		{
			return TileOrdering.Compare(this, other);
		}

		public static bool operator ==(Tile a, Tile b) => a.Equals(b);
		public static bool operator !=(Tile a, Tile b) => !a.Equals(b);

		public override string ToString()
		{
			string suit = Suit switch
			{
				Suit.Man => "m",
				Suit.Pin => "p",
				Suit.Sou => "s",
				Suit.Wind => "z",
				Suit.Dragon => "d",
				_ => "?"
			};
			return $"{Value}{suit}{(IsRedFive ? "r" : "")}";
		}
	}

	public static class TileOrdering
	{
		const int SuitStride = 100;
		const int ValueStride = 2;

		public static void Sort(List<Tile> tiles)
		{
			if (tiles == null)
				return;
			tiles.Sort(Compare);
		}

		public static int Compare(Tile left, Tile right)
		{
			return SortKey(left).CompareTo(SortKey(right));
		}

		public static int SortKey(Tile tile)
		{
			return SuitOrder(tile.Suit) * SuitStride
				+ tile.Value * ValueStride
				+ RedFiveOrder(tile);
		}

		static int SuitOrder(Suit suit)
		{
			switch (suit)
			{
				case Suit.Man: return 0;
				case Suit.Pin: return 1;
				case Suit.Sou: return 2;
				case Suit.Wind: return 3;
				case Suit.Dragon: return 4;
				default: return 99;
			}
		}

		static int RedFiveOrder(Tile tile)
		{
			return tile.IsNumber && tile.Value == 5 && tile.IsRedFive ? 1 : 0;
		}
	}
}
