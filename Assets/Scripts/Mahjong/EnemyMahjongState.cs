using System;
using System.Collections.Generic;

namespace Mahjong
{
	public enum EnemyComboType { None, Shuntsu, Koutsu, Toitsu }

	public readonly struct WaitGroup
	{
		public readonly Tile Slot1;
		public readonly Tile Slot2;
		public readonly Tile NeedTile;
		public readonly EnemyComboType Type;
		public readonly int DoraInGroup;
		public WaitGroup(Tile s1, Tile s2, Tile need, EnemyComboType type, int dora)
		{ Slot1 = s1; Slot2 = s2; NeedTile = need; Type = type; DoraInGroup = dora; }
	}

	public class EnemyTriggerResult
	{
		public EnemyComboType Combo;
		public int DoraCount;
		public int RankUsed;
		public int DamageHalfHearts;
		public WaitGroup HitGroup;
	}

	/// <summary>
	/// 1~3랭크 적의 가상 대기 조합. 플레이어 패산과 무관한 별도 랜덤 풀에서 생성.
	/// 발동 후·플레이어 공격 후·중간 포기 후 모두 Reroll.
	/// </summary>
	public class EnemyMahjongState
	{
		readonly Random rng;
		readonly int rank;
		WaitGroup g1;
		WaitGroup g2;
		IReadOnlyList<Tile> currentDoras;

		public int Rank => rank;
		public WaitGroup Group1 => g1;
		public WaitGroup Group2 => g2;

		public EnemyMahjongState(int rank, int seed, IReadOnlyList<Tile> doraTiles)
		{
			this.rank = rank;
			rng = new Random(seed == 0 ? Environment.TickCount : seed);
			currentDoras = doraTiles;
			Reroll(doraTiles);
		}

		public void Reroll(IReadOnlyList<Tile> doraTiles)
		{
			currentDoras = doraTiles;
			g1 = GenerateGroup();
			g2 = GenerateGroup();
		}

		WaitGroup GenerateGroup()
		{
			// 분포: 슌쯔 40% / 커쯔 40% / 또이츠(머리 대기) 20%.
			int roll = rng.Next(5);
			if (roll < 2)
			{
				// 슌쯔(중간 대기) — 수패만 (자패는 슌쯔 불가)
				int suit = rng.Next(3); // 0=m 1=p 2=s
				int start = rng.Next(1, 8); // 1..7
				var t1 = new Tile((Suit)suit, start);
				var t2 = new Tile((Suit)suit, start + 2);
				var need = new Tile((Suit)suit, start + 1);
				return new WaitGroup(t1, t2, need, EnemyComboType.Shuntsu, CountDoraIn(t1, t2, need));
			}
			else if (roll < 4)
			{
				// 커쯔 대기(샨폰형) — 2장 동일, 한 장 더로 삼면자 완성. 모든 종류 가능.
				int suitRoll = rng.Next(34);
				Tile sample = TileIndex.FromIndex(suitRoll);
				return new WaitGroup(sample, sample, sample, EnemyComboType.Koutsu, CountDoraIn(sample, sample, sample));
			}
			else
			{
				// 또이츠(머리) 대기 — 1장 보유, 한 장 더로 머리 완성. 모든 종류 가능.
				// Slot2는 미사용(UI에서 숨김). 피해 계산은 Combo 타입으로 구분.
				// 도라는 보유 1장 + 필요 1장 = 2회 카운트.
				int suitRoll = rng.Next(34);
				Tile sample = TileIndex.FromIndex(suitRoll);
				int dora = 0;
				if (currentDoras != null)
					foreach (var d in currentDoras) if (sample.SameKind(d)) dora += 2;
				return new WaitGroup(sample, sample, sample, EnemyComboType.Toitsu, dora);
			}
		}

		int CountDoraIn(Tile a, Tile b, Tile c)
		{
			if (currentDoras == null) return 0;
			int n = 0;
			foreach (var d in currentDoras)
			{
				if (a.SameKind(d)) n++;
				if (b.SameKind(d)) n++;
				if (c.SameKind(d)) n++;
			}
			return n;
		}

		/// <summary>플레이어 버림패와 비교. 발동 시 결과 반환, 미발동 시 null.</summary>
		public EnemyTriggerResult OnPlayerDiscard(Tile discard)
		{
			if (discard.SameKind(g1.NeedTile)) return ComputeDamage(g1);
			if (discard.SameKind(g2.NeedTile)) return ComputeDamage(g2);
			return null;
		}

		EnemyTriggerResult ComputeDamage(WaitGroup g)
		{
			// 슌쯔: 랭크 × 0.5 절반하트
			// 커쯔: 랭크 × 1.0 절반하트
			// 또이츠(머리): 랭크 × 0.25 절반하트 (중간 포기 기준 멘츠:머리 = 2:1 비율 유지)
			// 도라 1장당 추가 랭크 × 1.0 절반하트
			float baseDmg;
			switch (g.Type)
			{
				case EnemyComboType.Shuntsu: baseDmg = rank * 0.5f; break;
				case EnemyComboType.Koutsu:  baseDmg = rank * 1.0f; break;
				case EnemyComboType.Toitsu:  baseDmg = rank * 0.25f; break;
				default: baseDmg = 0f; break;
			}
			float dmg = baseDmg + g.DoraInGroup * rank * 1.0f;
			int half = (int)Math.Ceiling(dmg);
			return new EnemyTriggerResult
			{
				Combo = g.Type,
				DoraCount = g.DoraInGroup,
				RankUsed = rank,
				DamageHalfHearts = half,
				HitGroup = g
			};
		}
	}
}
