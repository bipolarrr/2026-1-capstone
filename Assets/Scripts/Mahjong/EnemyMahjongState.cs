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
		public Tile TriggeringTile;
		public int TriggeringTileKind => TileIndex.Of(TriggeringTile);
	}

	public readonly struct EnemyWaitSnapshot
	{
		static readonly WaitGroup[] EmptyGroups = new WaitGroup[0];
		static readonly Tile[] EmptyTiles = new Tile[0];

		readonly WaitGroup[] allWaitGroups;
		readonly WaitGroup[] revealedWaitGroups;
		readonly Tile[] allWaitTiles;
		readonly Tile[] revealedWaitTiles;

		public static EnemyWaitSnapshot Empty => new EnemyWaitSnapshot(EmptyGroups, EmptyGroups);

		EnemyWaitSnapshot(WaitGroup[] allWaitGroups, WaitGroup[] revealedWaitGroups)
		{
			this.allWaitGroups = allWaitGroups ?? EmptyGroups;
			this.revealedWaitGroups = revealedWaitGroups ?? EmptyGroups;
			allWaitTiles = ExtractWaitTiles(this.allWaitGroups);
			revealedWaitTiles = ExtractWaitTiles(this.revealedWaitGroups);
		}

		public IReadOnlyList<WaitGroup> AllWaitGroups => allWaitGroups ?? EmptyGroups;
		public IReadOnlyList<WaitGroup> RevealedWaitGroups => revealedWaitGroups ?? EmptyGroups;
		public IReadOnlyList<Tile> AllWaitTiles => allWaitTiles ?? EmptyTiles;
		public IReadOnlyList<Tile> RevealedWaitTiles => revealedWaitTiles ?? EmptyTiles;
		public IReadOnlyList<WaitGroup> TriggerWaitGroups => IsExhaustiveDisplay ? RevealedWaitGroups : AllWaitGroups;
		public bool HasRevealedWaits => RevealedWaitTiles.Count > 0;
		public bool IsExhaustiveDisplay => HasRevealedWaits;

		public static EnemyWaitSnapshot Create(
			WaitGroup group1,
			WaitGroup group2,
			MahjongWaitRevealDecision revealDecision,
			bool enemyAlive)
		{
			if (!enemyAlive)
				return Empty;

			var allGroups = new[] { group1, group2 };
			var revealedGroups = new List<WaitGroup>(2);
			if (revealDecision.ShowGroup1Need)
				revealedGroups.Add(group1);
			if (revealDecision.ShowGroup2Need)
				revealedGroups.Add(group2);

			return new EnemyWaitSnapshot(allGroups, revealedGroups.ToArray());
		}

		public bool TryGetPrimaryDisplayGroup(out WaitGroup group)
		{
			if (RevealedWaitGroups.Count > 0)
			{
				group = RevealedWaitGroups[0];
				return true;
			}
			if (AllWaitGroups.Count > 0)
			{
				group = AllWaitGroups[0];
				return false;
			}
			group = default;
			return false;
		}

		public bool ContainsRevealedWait(Tile tile)
		{
			return ContainsTileKind(RevealedWaitTiles, TileIndex.Of(tile));
		}

		public bool ContainsTriggerWait(Tile tile)
		{
			int tileKind = TileIndex.Of(tile);
			foreach (var group in TriggerWaitGroups)
			{
				if (TileIndex.Of(group.NeedTile) == tileKind)
					return true;
			}
			return false;
		}

		public bool TryCreateTrigger(Tile discard, int rank, out EnemyTriggerResult result)
		{
			int discardKind = TileIndex.Of(discard);
			foreach (var group in TriggerWaitGroups)
			{
				if (TileIndex.Of(group.NeedTile) != discardKind)
					continue;

				result = CreateTriggerResult(group, rank, discard);
				return true;
			}

			result = null;
			return false;
		}

		static EnemyTriggerResult CreateTriggerResult(WaitGroup group, int rank, Tile triggeringTile)
		{
			int half = MahjongEnemyWaitDamage.GetDamageHalfHearts(rank, group);
			return new EnemyTriggerResult
			{
				Combo = group.Type,
				DoraCount = group.DoraInGroup,
				RankUsed = rank,
				DamageHalfHearts = half,
				HitGroup = WithTriggeringNeedTile(group, triggeringTile),
				TriggeringTile = triggeringTile
			};
		}

		static WaitGroup WithTriggeringNeedTile(WaitGroup group, Tile triggeringTile)
		{
			if (TileIndex.Of(group.NeedTile) != TileIndex.Of(triggeringTile))
				return group;
			return new WaitGroup(group.Slot1, group.Slot2, triggeringTile, group.Type, group.DoraInGroup);
		}

		static Tile[] ExtractWaitTiles(IReadOnlyList<WaitGroup> groups)
		{
			if (groups == null || groups.Count == 0)
				return EmptyTiles;

			var tiles = new Tile[groups.Count];
			for (int i = 0; i < groups.Count; i++)
				tiles[i] = groups[i].NeedTile;
			return tiles;
		}

		static bool ContainsTileKind(IReadOnlyList<Tile> tiles, int tileKind)
		{
			if (tileKind < 0 || tiles == null)
				return false;

			for (int i = 0; i < tiles.Count; i++)
				if (TileIndex.Of(tiles[i]) == tileKind)
					return true;
			return false;
		}
	}

	/// <summary>
	/// 1~3랭크 적의 가상 대기 조합. 플레이어 패산과 무관한 별도 랜덤 풀에서 생성.
	/// 생성자와 명시적 Reroll 호출에서만 새 조합을 만든다.
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
			return OnPlayerDiscard(discard, CreateWaitSnapshot(default, enemyAlive: true));
		}

		public EnemyTriggerResult OnPlayerDiscard(Tile discard, EnemyWaitSnapshot waitSnapshot)
		{
			return waitSnapshot.TryCreateTrigger(discard, rank, out var result) ? result : null;
		}

		/// <summary>플레이어 버림패와 무관한 적 쯔모 판정. 성공 시 현재 대기 그룹 중 하나로 피해를 계산한다.</summary>
		public EnemyTriggerResult TryTsumo(float chance)
		{
			if (chance <= 0f) return null;
			if (chance < 1f && rng.NextDouble() >= chance) return null;
			return ComputeDamage(rng.Next(2) == 0 ? g1 : g2);
		}

		EnemyTriggerResult ComputeDamage(WaitGroup g)
		{
			return CreateTsumoTrigger(g);
		}

		EnemyTriggerResult CreateTsumoTrigger(WaitGroup g)
		{
			int half = MahjongEnemyWaitDamage.GetDamageHalfHearts(rank, g);
			return new EnemyTriggerResult
			{
				Combo = g.Type,
				DoraCount = g.DoraInGroup,
				RankUsed = rank,
				DamageHalfHearts = half,
				HitGroup = g,
				TriggeringTile = g.NeedTile
			};
		}

		public EnemyWaitSnapshot CreateWaitSnapshot(MahjongWaitRevealDecision revealDecision, bool enemyAlive)
		{
			return EnemyWaitSnapshot.Create(g1, g2, revealDecision, enemyAlive);
		}
	}

	public static class MahjongEnemyWaitDamage
	{
		public static int GetDamageHalfHearts(int rank, WaitGroup group)
		{
			// 슌쯔: 랭크 × 0.5, 커쯔: 랭크 × 1.0, 또이츠: 랭크 × 0.25.
			// 도라는 1장당 랭크 × 1.0 절반하트를 더한다.
			float baseDmg;
			switch (group.Type)
			{
				case EnemyComboType.Shuntsu: baseDmg = rank * 0.5f; break;
				case EnemyComboType.Koutsu:  baseDmg = rank * 1.0f; break;
				case EnemyComboType.Toitsu:  baseDmg = rank * 0.25f; break;
				default: baseDmg = 0f; break;
			}
			float dmg = baseDmg + group.DoraInGroup * rank * 1.0f;
			return (int)Math.Ceiling(dmg);
		}
	}

	public enum MahjongDangerLevel
	{
		Safe,
		Caution,
		Danger,
		Lethal
	}

	public readonly struct MahjongDangerSource
	{
		public readonly Tile WaitTile;
		public readonly int DamageHalfHearts;
		public readonly bool IsRevealed;
		public readonly bool EnemyAlive;

		public MahjongDangerSource(Tile waitTile, int damageHalfHearts, bool isRevealed, bool enemyAlive)
		{
			WaitTile = waitTile;
			DamageHalfHearts = damageHalfHearts;
			IsRevealed = isRevealed;
			EnemyAlive = enemyAlive;
		}
	}

	public readonly struct MahjongDiscardDanger
	{
		public readonly MahjongDangerLevel Level;
		public readonly int VisibleHitCount;
		public readonly int VisibleDamageHalfHearts;
		public readonly int HiddenSourceCount;
		public readonly float HiddenExpectedDamage;

		public MahjongDiscardDanger(
			MahjongDangerLevel level,
			int visibleHitCount,
			int visibleDamageHalfHearts,
			int hiddenSourceCount,
			float hiddenExpectedDamage)
		{
			Level = level;
			VisibleHitCount = visibleHitCount;
			VisibleDamageHalfHearts = visibleDamageHalfHearts;
			HiddenSourceCount = hiddenSourceCount;
			HiddenExpectedDamage = hiddenExpectedDamage;
		}
	}

	public static class MahjongDangerEvaluator
	{
		public static MahjongDiscardDanger Evaluate(
			Tile candidateTile,
			IEnumerable<MahjongDangerSource> sources,
			int playerHalfHearts)
		{
			if (sources == null)
				return new MahjongDiscardDanger(MahjongDangerLevel.Safe, 0, 0, 0, 0f);

			int candidateKind = TileIndex.Of(candidateTile);
			int visibleHitCount = 0;
			int visibleDamageHalfHearts = 0;
			int hiddenSourceCount = 0;
			int hiddenDamageHalfHearts = 0;

			foreach (var source in sources)
			{
				if (!source.EnemyAlive || source.DamageHalfHearts <= 0)
					continue;

				if (!source.IsRevealed)
				{
					hiddenSourceCount++;
					hiddenDamageHalfHearts += source.DamageHalfHearts;
					continue;
				}

				if (candidateKind >= 0 && candidateKind == TileIndex.Of(source.WaitTile))
				{
					visibleHitCount++;
					visibleDamageHalfHearts += source.DamageHalfHearts;
				}
			}

			float hiddenExpectedDamage = hiddenDamageHalfHearts / (float)TileIndex.Count;
			MahjongDangerLevel level = ResolveLevel(
				visibleHitCount,
				visibleDamageHalfHearts,
				hiddenSourceCount,
				playerHalfHearts);

			return new MahjongDiscardDanger(
				level,
				visibleHitCount,
				visibleDamageHalfHearts,
				hiddenSourceCount,
				hiddenExpectedDamage);
		}

		static MahjongDangerLevel ResolveLevel(
			int visibleHitCount,
			int visibleDamageHalfHearts,
			int hiddenSourceCount,
			int playerHalfHearts)
		{
			if (visibleDamageHalfHearts > 0 && visibleDamageHalfHearts >= playerHalfHearts)
				return MahjongDangerLevel.Lethal;
			if (visibleHitCount > 0)
				return MahjongDangerLevel.Danger;
			if (hiddenSourceCount > 0)
				return MahjongDangerLevel.Caution;
			return MahjongDangerLevel.Safe;
		}
	}

	public readonly struct MahjongWaitRevealDecision
	{
		public readonly bool ShowGroup1Need;
		public readonly bool ShowGroup1Shape;
		public readonly bool ShowGroup2Need;
		public readonly bool ShowGroup2Shape;
		public readonly bool NewlyRevealedThisTurn;

		public MahjongWaitRevealDecision(
			bool showGroup1Need,
			bool showGroup1Shape,
			bool showGroup2Need,
			bool showGroup2Shape,
			bool newlyRevealedThisTurn)
		{
			ShowGroup1Need = showGroup1Need;
			ShowGroup1Shape = showGroup1Shape;
			ShowGroup2Need = showGroup2Need;
			ShowGroup2Shape = showGroup2Shape;
			NewlyRevealedThisTurn = newlyRevealedThisTurn;
		}
	}

	public static class MahjongWaitRevealPolicy
	{
		public static bool NeedsRandomRoll(int rank, bool enemyAlive, bool rollRank3Reveal)
		{
			return enemyAlive && rank == 3 && rollRank3Reveal;
		}

		public static MahjongWaitRevealDecision Evaluate(
			int rank,
			bool enemyAlive,
			bool rollRank3Reveal,
			float rank3RevealChancePerTurn,
			float random01)
		{
			if (!enemyAlive)
				return new MahjongWaitRevealDecision(false, false, false, false, false);

			if (rank <= 2 && rollRank3Reveal)
				return new MahjongWaitRevealDecision(true, false, false, false, false);

			if (rank == 3 && rollRank3Reveal && random01 < rank3RevealChancePerTurn)
				return new MahjongWaitRevealDecision(true, false, false, false, true);

			return new MahjongWaitRevealDecision(false, true, false, false, false);
		}
	}

	public static class MahjongDangerSourceBuilder
	{
		public static IReadOnlyList<MahjongDangerSource> BuildSources(
			EnemyMahjongState enemyState,
			MahjongWaitRevealDecision revealDecision,
			bool enemyAlive)
		{
			var sources = new List<MahjongDangerSource>(2);
			AppendSources(sources, enemyState, revealDecision, enemyAlive);
			return sources;
		}

		public static IReadOnlyList<MahjongDangerSource> BuildSources(
			EnemyWaitSnapshot waitSnapshot,
			int rank)
		{
			var sources = new List<MahjongDangerSource>(2);
			AppendSources(sources, waitSnapshot, rank);
			return sources;
		}

		public static void AppendSources(
			List<MahjongDangerSource> destination,
			EnemyMahjongState enemyState,
			MahjongWaitRevealDecision revealDecision,
			bool enemyAlive)
		{
			if (destination == null || enemyState == null || !enemyAlive)
				return;

			AppendSources(destination, enemyState.CreateWaitSnapshot(revealDecision, enemyAlive), enemyState.Rank);
		}

		public static void AppendSources(
			List<MahjongDangerSource> destination,
			EnemyWaitSnapshot waitSnapshot,
			int rank)
		{
			if (destination == null)
				return;

			bool isRevealedSource = waitSnapshot.IsExhaustiveDisplay;
			foreach (var group in waitSnapshot.TriggerWaitGroups)
				AppendGroupSource(destination, rank, group, isRevealedSource);
		}

		static void AppendGroupSource(
			List<MahjongDangerSource> destination,
			int rank,
			WaitGroup group,
			bool isRevealed)
		{
			int damage = MahjongEnemyWaitDamage.GetDamageHalfHearts(rank, group);
			destination.Add(new MahjongDangerSource(group.NeedTile, damage, isRevealed, enemyAlive: true));
		}
	}
}
