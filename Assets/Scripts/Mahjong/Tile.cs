using System;

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
			int s = Suit.CompareTo(other.Suit);
			if (s != 0) return s;
			return Value.CompareTo(other.Value);
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
}
