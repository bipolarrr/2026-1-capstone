using NUnit.Framework;
using Mahjong;

namespace MahjongTests
{
	public class EnemyMahjongStateTests
	{
		[Test]
		public void SameSeed_ProducesSameGroups()
		{
			var a = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);
			var b = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);
			Assert.AreEqual(a.Group1.NeedTile, b.Group1.NeedTile);
			Assert.AreEqual(a.Group2.NeedTile, b.Group2.NeedTile);
			Assert.AreEqual(a.Group1.Type, b.Group1.Type);
		}

		[Test]
		public void OnDiscard_NonMatching_ReturnsNull()
		{
			var s = new EnemyMahjongState(rank: 1, seed: 50, doraTiles: null);
			// 거의 확률 0인 패: 무관한 다른 값
			var weird = new Tile(Suit.Dragon, 1);
			var trigger = s.OnPlayerDiscard(weird);
			// 우연히 백패가 needTile이면 null이 아닐 수 있음. 그 경우 통과 처리.
			if (trigger == null) Assert.Pass();
			else Assert.AreEqual(weird, s.Group1.NeedTile.SameKind(weird) ? s.Group1.NeedTile : s.Group2.NeedTile);
		}

		[Test]
		public void OnDiscard_Matching_ReturnsResult_WithRankScaledDamage()
		{
			var s = new EnemyMahjongState(rank: 3, seed: 9, doraTiles: null);
			var need = s.Group1.NeedTile;
			var r = s.OnPlayerDiscard(need);
			Assert.IsNotNull(r);
			Assert.AreEqual(3, r.RankUsed);
			Assert.AreEqual(TileIndex.Of(need), r.TriggeringTileKind);
			Assert.IsTrue(r.DamageHalfHearts >= 1, "랭크3은 또이츠(0.25×3=ceil 1)에서도 최소 1 절반하트");
		}

		[Test]
		public void TryTsumo_ZeroChance_ReturnsNull()
		{
			var s = new EnemyMahjongState(rank: 2, seed: 17, doraTiles: null);
			Assert.IsNull(s.TryTsumo(0f));
		}

		[Test]
		public void TryTsumo_Guaranteed_UsesCurrentWaitGroupDamage()
		{
			var s = new EnemyMahjongState(rank: 2, seed: 17, doraTiles: null);
			var r = s.TryTsumo(1f);
			Assert.IsNotNull(r);
			Assert.IsTrue(SameGroup(r.HitGroup, s.Group1) || SameGroup(r.HitGroup, s.Group2));
			Assert.AreEqual(TileIndex.Of(r.HitGroup.NeedTile), r.TriggeringTileKind);
			Assert.AreEqual(ExpectedDamage(2, r.HitGroup), r.DamageHalfHearts);
		}

		[Test]
		public void Reroll_ChangesGroupsForDifferentSeed()
		{
			var s1 = new EnemyMahjongState(rank: 2, seed: 1, doraTiles: null);
			var n1 = s1.Group1.NeedTile;
			var s2 = new EnemyMahjongState(rank: 2, seed: 2, doraTiles: null);
			var n2 = s2.Group1.NeedTile;
			// 다른 시드는 다른 결과(정확히 같을 확률 매우 낮음)
			Assert.IsTrue(!n1.Equals(n2) || !s1.Group1.Type.Equals(s2.Group1.Type));
		}

		[Test]
		public void EnemyWaitSnapshot_DisplayAndTrigger_UseSameCanonicalTiles()
		{
			var enemy = EnemyWithDistinctWaits();
			var snapshot = enemy.CreateWaitSnapshot(RevealedGroup1Decision(), enemyAlive: true);

			Assert.IsTrue(snapshot.IsExhaustiveDisplay);
			Assert.AreEqual(2, snapshot.AllWaitTiles.Count);
			Assert.AreEqual(1, snapshot.RevealedWaitTiles.Count);
			Assert.AreEqual(TileIndex.Of(enemy.Group1.NeedTile), TileIndex.Of(snapshot.RevealedWaitTiles[0]));
			Assert.IsTrue(snapshot.ContainsTriggerWait(enemy.Group1.NeedTile));
			Assert.IsFalse(snapshot.ContainsTriggerWait(enemy.Group2.NeedTile));
		}

		[Test]
		public void DiscardMatchingDisplayedWait_TriggersRonWithSameTile()
		{
			var enemy = EnemyWithDistinctWaits();
			var snapshot = enemy.CreateWaitSnapshot(RevealedGroup1Decision(), enemyAlive: true);
			var discard = enemy.Group1.NeedTile;

			var result = enemy.OnPlayerDiscard(discard, snapshot);

			Assert.IsNotNull(result);
			Assert.AreEqual(TileIndex.Of(discard), result.TriggeringTileKind);
			Assert.AreEqual(TileIndex.Of(discard), TileIndex.Of(result.HitGroup.NeedTile));
			Assert.IsTrue(snapshot.ContainsRevealedWait(discard));
		}

		[Test]
		public void DiscardNotInDisplayedWait_DoesNotTriggerRon_WhenDisplayIsExhaustive()
		{
			var enemy = EnemyWithDistinctWaits();
			var snapshot = enemy.CreateWaitSnapshot(RevealedGroup1Decision(), enemyAlive: true);
			var hiddenOnlyDiscard = enemy.Group2.NeedTile;

			var result = enemy.OnPlayerDiscard(hiddenOnlyDiscard, snapshot);

			Assert.IsTrue(snapshot.IsExhaustiveDisplay);
			Assert.IsFalse(snapshot.ContainsRevealedWait(hiddenOnlyDiscard));
			Assert.IsNull(result);
		}

		[Test]
		public void RedFiveCanonicalization_MatchesWaitPolicy()
		{
			var normalFiveWait = T(Suit.Man, 5);
			var redFiveDiscard = new Tile(Suit.Man, 5, isRedFive: true);
			var snapshot = EnemyWaitSnapshot.Create(
				new WaitGroup(T(Suit.Man, 4), T(Suit.Man, 6), normalFiveWait, EnemyComboType.Shuntsu, 0),
				new WaitGroup(T(Suit.Pin, 1), T(Suit.Pin, 1), T(Suit.Pin, 1), EnemyComboType.Koutsu, 0),
				RevealedGroup1Decision(),
				enemyAlive: true);

			bool triggered = snapshot.TryCreateTrigger(redFiveDiscard, rank: 2, out var result);

			Assert.IsTrue(triggered);
			Assert.AreEqual(TileIndex.Of(normalFiveWait), result.TriggeringTileKind);
			Assert.AreEqual(TileIndex.Of(redFiveDiscard), TileIndex.Of(result.HitGroup.NeedTile));
			Assert.IsTrue(result.TriggeringTile.IsRedFive);
			Assert.IsTrue(result.HitGroup.NeedTile.IsRedFive);
		}

		[Test]
		public void NoSources_ReturnsSafe()
		{
			var result = MahjongDangerEvaluator.Evaluate(T(Suit.Man, 1), null, playerHalfHearts: 10);

			Assert.AreEqual(MahjongDangerLevel.Safe, result.Level);
			Assert.AreEqual(0, result.VisibleHitCount);
			Assert.AreEqual(0, result.VisibleDamageHalfHearts);
			Assert.AreEqual(0, result.HiddenSourceCount);
			Assert.AreEqual(0f, result.HiddenExpectedDamage);
		}

		[Test]
		public void HiddenSourceOnly_ReturnsCautionWithoutTileLeak()
		{
			var wait = T(Suit.Pin, 3);
			var sources = new[]
			{
				DangerSource(wait, damageHalfHearts: 6, isRevealed: false)
			};

			var result = MahjongDangerEvaluator.Evaluate(wait, sources, playerHalfHearts: 10);

			Assert.AreEqual(MahjongDangerLevel.Caution, result.Level);
			Assert.AreEqual(0, result.VisibleHitCount);
			Assert.AreEqual(0, result.VisibleDamageHalfHearts);
			Assert.AreEqual(1, result.HiddenSourceCount);
			Assert.AreEqual(6f / 34f, result.HiddenExpectedDamage, 0.0001f);
		}

		[Test]
		public void RevealedMatchingSource_ReturnsDanger()
		{
			var sources = new[]
			{
				DangerSource(T(Suit.Sou, 7), damageHalfHearts: 3, isRevealed: true)
			};

			var result = MahjongDangerEvaluator.Evaluate(T(Suit.Sou, 7), sources, playerHalfHearts: 10);

			Assert.AreEqual(MahjongDangerLevel.Danger, result.Level);
			Assert.AreEqual(1, result.VisibleHitCount);
			Assert.AreEqual(3, result.VisibleDamageHalfHearts);
			Assert.AreEqual(0, result.HiddenSourceCount);
		}

		[Test]
		public void RevealedNonMatchingSource_WithNoHidden_ReturnsSafe()
		{
			var sources = new[]
			{
				DangerSource(T(Suit.Sou, 7), damageHalfHearts: 3, isRevealed: true)
			};

			var result = MahjongDangerEvaluator.Evaluate(T(Suit.Sou, 8), sources, playerHalfHearts: 10);

			Assert.AreEqual(MahjongDangerLevel.Safe, result.Level);
			Assert.AreEqual(0, result.VisibleHitCount);
			Assert.AreEqual(0, result.VisibleDamageHalfHearts);
			Assert.AreEqual(0, result.HiddenSourceCount);
		}

		[TestCase(4)]
		[TestCase(5)]
		public void RevealedDamageAtOrAbovePlayerHealth_ReturnsLethal(int damageHalfHearts)
		{
			var sources = new[]
			{
				DangerSource(T(Suit.Man, 9), damageHalfHearts, isRevealed: true)
			};

			var result = MahjongDangerEvaluator.Evaluate(T(Suit.Man, 9), sources, playerHalfHearts: 4);

			Assert.AreEqual(MahjongDangerLevel.Lethal, result.Level);
			Assert.AreEqual(1, result.VisibleHitCount);
			Assert.AreEqual(damageHalfHearts, result.VisibleDamageHalfHearts);
		}

		[Test]
		public void MultipleRevealedMatches_SumsDamage()
		{
			var candidate = T(Suit.Dragon, 3);
			var sources = new[]
			{
				DangerSource(candidate, damageHalfHearts: 2, isRevealed: true),
				DangerSource(candidate, damageHalfHearts: 3, isRevealed: true),
				DangerSource(T(Suit.Wind, 1), damageHalfHearts: 9, isRevealed: true)
			};

			var result = MahjongDangerEvaluator.Evaluate(candidate, sources, playerHalfHearts: 10);

			Assert.AreEqual(MahjongDangerLevel.Danger, result.Level);
			Assert.AreEqual(2, result.VisibleHitCount);
			Assert.AreEqual(5, result.VisibleDamageHalfHearts);
		}

		[Test]
		public void DeadEnemySources_AreIgnored()
		{
			var candidate = T(Suit.Man, 1);
			var sources = new[]
			{
				DangerSource(candidate, damageHalfHearts: 8, isRevealed: true, enemyAlive: false),
				DangerSource(T(Suit.Pin, 2), damageHalfHearts: 8, isRevealed: false, enemyAlive: false)
			};

			var result = MahjongDangerEvaluator.Evaluate(candidate, sources, playerHalfHearts: 4);

			Assert.AreEqual(MahjongDangerLevel.Safe, result.Level);
			Assert.AreEqual(0, result.VisibleHitCount);
			Assert.AreEqual(0, result.VisibleDamageHalfHearts);
			Assert.AreEqual(0, result.HiddenSourceCount);
			Assert.AreEqual(0f, result.HiddenExpectedDamage);
		}

		[Test]
		public void RedFiveAndNormalFive_AreSameDiscardKind()
		{
			var normalFiveWait = T(Suit.Man, 5);
			var redFiveCandidate = new Tile(Suit.Man, 5, isRedFive: true);
			var sources = new[]
			{
				DangerSource(normalFiveWait, damageHalfHearts: 2, isRevealed: true)
			};

			var result = MahjongDangerEvaluator.Evaluate(redFiveCandidate, sources, playerHalfHearts: 10);

			Assert.AreEqual(MahjongDangerLevel.Danger, result.Level);
			Assert.AreEqual(1, result.VisibleHitCount);
			Assert.AreEqual(2, result.VisibleDamageHalfHearts);
		}

		[Test]
		public void NegativeOrZeroDamage_DoesNotCreateVisibleDamage()
		{
			var candidate = T(Suit.Pin, 5);
			var sources = new[]
			{
				DangerSource(candidate, damageHalfHearts: 0, isRevealed: true),
				DangerSource(candidate, damageHalfHearts: -1, isRevealed: false)
			};

			var result = MahjongDangerEvaluator.Evaluate(candidate, sources, playerHalfHearts: 10);

			Assert.AreEqual(MahjongDangerLevel.Safe, result.Level);
			Assert.AreEqual(0, result.VisibleHitCount);
			Assert.AreEqual(0, result.VisibleDamageHalfHearts);
			Assert.AreEqual(0, result.HiddenSourceCount);
			Assert.AreEqual(0f, result.HiddenExpectedDamage);
		}

		[Test]
		public void Rank1_InitialDisplayPass_RemainsHidden()
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank: 1,
				enemyAlive: true,
				rollRank3Reveal: false,
				rank3RevealChancePerTurn: 0.05f,
				random01: 0.99f);

			Assert.IsFalse(decision.ShowGroup1Need);
			Assert.IsTrue(decision.ShowGroup1Shape);
			Assert.IsFalse(decision.ShowGroup2Need);
			Assert.IsFalse(decision.ShowGroup2Shape);
			Assert.IsFalse(decision.NewlyRevealedThisTurn);
		}

		[Test]
		public void Rank2_InitialDisplayPass_RemainsHidden()
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank: 2,
				enemyAlive: true,
				rollRank3Reveal: false,
				rank3RevealChancePerTurn: 0.05f,
				random01: 0.99f);

			Assert.IsFalse(decision.ShowGroup1Need);
			Assert.IsTrue(decision.ShowGroup1Shape);
			Assert.IsFalse(decision.ShowGroup2Need);
			Assert.IsFalse(decision.ShowGroup2Shape);
			Assert.IsFalse(decision.NewlyRevealedThisTurn);
		}

		[TestCase(1)]
		[TestCase(2)]
		public void Rank1AndRank2_TurnRevealPass_RevealsGroup1Need(int rank)
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank,
				enemyAlive: true,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 0.05f,
				random01: 0.99f);

			Assert.IsTrue(decision.ShowGroup1Need);
			Assert.IsFalse(decision.ShowGroup1Shape);
			Assert.IsFalse(decision.ShowGroup2Need);
			Assert.IsFalse(decision.ShowGroup2Shape);
			Assert.IsFalse(decision.NewlyRevealedThisTurn);
		}

		[Test]
		public void Rank3_WhenRollBelowChance_RevealsAccordingToCurrentPolicy()
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank: 3,
				enemyAlive: true,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 0.05f,
				random01: 0.049f);

			Assert.IsTrue(decision.ShowGroup1Need);
			Assert.IsFalse(decision.ShowGroup1Shape);
			Assert.IsFalse(decision.ShowGroup2Need);
			Assert.IsFalse(decision.ShowGroup2Shape);
			Assert.IsTrue(decision.NewlyRevealedThisTurn);
		}

		[Test]
		public void Rank3_WhenRollAtOrAboveChance_RemainsHidden()
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank: 3,
				enemyAlive: true,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 0.05f,
				random01: 0.05f);

			Assert.IsFalse(decision.ShowGroup1Need);
			Assert.IsTrue(decision.ShowGroup1Shape);
			Assert.IsFalse(decision.ShowGroup2Need);
			Assert.IsFalse(decision.ShowGroup2Shape);
			Assert.IsFalse(decision.NewlyRevealedThisTurn);
		}

		[Test]
		public void Rank3_PreviousRevealIsNotStickyInCurrentPolicy()
		{
			var revealed = MahjongWaitRevealPolicy.Evaluate(
				rank: 3,
				enemyAlive: true,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 0.05f,
				random01: 0.049f);
			var nextDecision = MahjongWaitRevealPolicy.Evaluate(
				rank: 3,
				enemyAlive: true,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 0.05f,
				random01: 0.05f);

			Assert.IsTrue(revealed.ShowGroup1Need);
			Assert.IsFalse(nextDecision.ShowGroup1Need);
			Assert.IsTrue(nextDecision.ShowGroup1Shape);
			Assert.IsFalse(nextDecision.NewlyRevealedThisTurn);
		}

		[Test]
		public void Rank3_WithZeroChance_DoesNotReveal()
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank: 3,
				enemyAlive: true,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 0f,
				random01: 0f);

			Assert.IsFalse(decision.ShowGroup1Need);
			Assert.IsTrue(decision.ShowGroup1Shape);
			Assert.IsFalse(decision.NewlyRevealedThisTurn);
		}

		[Test]
		public void Rank3_WithOneChance_Reveals()
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank: 3,
				enemyAlive: true,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 1f,
				random01: 0.999f);

			Assert.IsTrue(decision.ShowGroup1Need);
			Assert.IsFalse(decision.ShowGroup1Shape);
			Assert.IsTrue(decision.NewlyRevealedThisTurn);
		}

		[TestCase(4)]
		[TestCase(5)]
		public void Rank4AndRank5_PreserveCurrentHiddenPolicy(int rank)
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank,
				enemyAlive: true,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 1f,
				random01: 0f);

			Assert.IsFalse(decision.ShowGroup1Need);
			Assert.IsTrue(decision.ShowGroup1Shape);
			Assert.IsFalse(decision.ShowGroup2Need);
			Assert.IsFalse(decision.ShowGroup2Shape);
			Assert.IsFalse(decision.NewlyRevealedThisTurn);
		}

		[Test]
		public void DeadEnemy_PreservesCurrentDisplayPolicy()
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank: 1,
				enemyAlive: false,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 1f,
				random01: 0f);

			Assert.IsFalse(decision.ShowGroup1Need);
			Assert.IsFalse(decision.ShowGroup1Shape);
			Assert.IsFalse(decision.ShowGroup2Need);
			Assert.IsFalse(decision.ShowGroup2Shape);
			Assert.IsFalse(decision.NewlyRevealedThisTurn);
		}

		[Test]
		public void Policy_DoesNotExposeHiddenTileValues()
		{
			var fields = typeof(MahjongWaitRevealDecision).GetFields(
				System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.Public
				| System.Reflection.BindingFlags.NonPublic);

			foreach (var field in fields)
			{
				Assert.AreNotEqual(typeof(Tile), field.FieldType);
				Assert.AreNotEqual(typeof(WaitGroup), field.FieldType);
			}
		}

		[TestCase(1, true, true, false)]
		[TestCase(2, true, true, false)]
		[TestCase(3, true, false, false)]
		[TestCase(3, true, true, true)]
		[TestCase(3, false, true, false)]
		[TestCase(4, true, true, false)]
		public void NeedsRandomRoll_OnlyForAliveRank3WhenTurnRollRequested(
			int rank,
			bool enemyAlive,
			bool rollRank3Reveal,
			bool expected)
		{
			Assert.AreEqual(expected, MahjongWaitRevealPolicy.NeedsRandomRoll(rank, enemyAlive, rollRank3Reveal));
		}

		[TestCase(0)]
		[TestCase(-1)]
		public void LowOutOfRangeRanks_InitialDisplayPass_RemainsHidden(int rank)
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank,
				enemyAlive: true,
				rollRank3Reveal: false,
				rank3RevealChancePerTurn: 0.05f,
				random01: 0.99f);

			Assert.IsFalse(decision.ShowGroup1Need);
			Assert.IsTrue(decision.ShowGroup1Shape);
		}

		[Test]
		public void HighOutOfRangeRanks_PreserveCurrentHiddenPolicy()
		{
			var decision = MahjongWaitRevealPolicy.Evaluate(
				rank: 6,
				enemyAlive: true,
				rollRank3Reveal: true,
				rank3RevealChancePerTurn: 1f,
				random01: 0f);

			Assert.IsFalse(decision.ShowGroup1Need);
			Assert.IsTrue(decision.ShowGroup1Shape);
		}

		[Test]
		public void DangerSourceBuilder_DeadEnemy_ReturnsNoSources()
		{
			var enemy = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);

			var sources = MahjongDangerSourceBuilder.BuildSources(
				enemy,
				RevealedGroup1Decision(),
				enemyAlive: false);

			Assert.AreEqual(0, sources.Count);
		}

		[Test]
		public void DangerSourceBuilder_RevealedGroup_BecomesRevealedSource()
		{
			var enemy = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);

			var sources = MahjongDangerSourceBuilder.BuildSources(
				enemy,
				RevealedGroup1Decision(),
				enemyAlive: true);

			Assert.AreEqual(1, sources.Count);
			Assert.IsTrue(sources[0].IsRevealed);
			Assert.AreEqual(enemy.Group1.NeedTile, sources[0].WaitTile);
			Assert.AreEqual(ExpectedDamage(enemy.Rank, enemy.Group1), sources[0].DamageHalfHearts);
		}

		[Test]
		public void DangerSourceBuilder_BackedOrHiddenGroup_BecomesHiddenSource()
		{
			var enemy = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);

			var sources = MahjongDangerSourceBuilder.BuildSources(
				enemy,
				HiddenDecision(),
				enemyAlive: true);

			Assert.AreEqual(2, sources.Count);
			Assert.IsFalse(sources[0].IsRevealed);
			Assert.IsFalse(sources[1].IsRevealed);
		}

		[Test]
		public void DangerSourceBuilder_IncludesBothGroupsCheckedByOnPlayerDiscard()
		{
			var enemy = new EnemyMahjongState(rank: 3, seed: 9, doraTiles: null);

			var sources = MahjongDangerSourceBuilder.BuildSources(
				enemy,
				HiddenDecision(),
				enemyAlive: true);

			Assert.AreEqual(2, sources.Count);
			Assert.AreEqual(enemy.Group1.NeedTile, sources[0].WaitTile);
			Assert.AreEqual(enemy.Group2.NeedTile, sources[1].WaitTile);
		}

		[Test]
		public void DangerSourceBuilder_DoesNotExposeHiddenTilesInFinalDangerResult()
		{
			var fields = typeof(MahjongDiscardDanger).GetFields(
				System.Reflection.BindingFlags.Instance
				| System.Reflection.BindingFlags.Public
				| System.Reflection.BindingFlags.NonPublic);

			foreach (var field in fields)
			{
				Assert.AreNotEqual(typeof(Tile), field.FieldType);
				Assert.AreNotEqual(typeof(WaitGroup), field.FieldType);
				Assert.AreNotEqual(typeof(EnemyMahjongState), field.FieldType);
			}
		}

		[Test]
		public void EvaluateDiscardDanger_RevealedMatchingTile_ReturnsDanger()
		{
			var enemy = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);
			var sources = MahjongDangerSourceBuilder.BuildSources(
				enemy,
				RevealedGroup1Decision(),
				enemyAlive: true);

			var result = MahjongDangerEvaluator.Evaluate(enemy.Group1.NeedTile, sources, playerHalfHearts: 999);

			Assert.AreEqual(MahjongDangerLevel.Danger, result.Level);
			Assert.AreEqual(1, result.VisibleHitCount);
			Assert.AreEqual(ExpectedDamage(enemy.Rank, enemy.Group1), result.VisibleDamageHalfHearts);
		}

		[Test]
		public void EvaluateDiscardDanger_HiddenMatchingTile_DoesNotBecomeDanger()
		{
			var enemy = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);
			var sources = MahjongDangerSourceBuilder.BuildSources(
				enemy,
				HiddenDecision(),
				enemyAlive: true);

			var result = MahjongDangerEvaluator.Evaluate(enemy.Group1.NeedTile, sources, playerHalfHearts: 999);

			Assert.AreEqual(MahjongDangerLevel.Caution, result.Level);
			Assert.AreEqual(0, result.VisibleHitCount);
			Assert.AreEqual(0, result.VisibleDamageHalfHearts);
			Assert.AreEqual(2, result.HiddenSourceCount);
		}

		[Test]
		public void EvaluateDiscardDanger_RevealedDamageAtHealth_ReturnsLethal()
		{
			var enemy = new EnemyMahjongState(rank: 2, seed: 100, doraTiles: null);
			var sources = MahjongDangerSourceBuilder.BuildSources(
				enemy,
				RevealedGroup1Decision(),
				enemyAlive: true);
			int visibleDamage = sources[0].DamageHalfHearts;

			var result = MahjongDangerEvaluator.Evaluate(enemy.Group1.NeedTile, sources, visibleDamage);

			Assert.AreEqual(MahjongDangerLevel.Lethal, result.Level);
			Assert.AreEqual(visibleDamage, result.VisibleDamageHalfHearts);
		}

		[Test]
		public void EvaluateDiscardDanger_MultipleEnemies_SumsVisibleDamage()
		{
			var candidate = T(Suit.Man, 4);
			var sources = new[]
			{
				new MahjongDangerSource(candidate, damageHalfHearts: 2, isRevealed: true, enemyAlive: true),
				new MahjongDangerSource(candidate, damageHalfHearts: 5, isRevealed: true, enemyAlive: true)
			};

			var result = MahjongDangerEvaluator.Evaluate(candidate, sources, playerHalfHearts: 999);

			Assert.AreEqual(MahjongDangerLevel.Danger, result.Level);
			Assert.AreEqual(2, result.VisibleHitCount);
			Assert.AreEqual(7, result.VisibleDamageHalfHearts);
		}

		static Tile T(Suit suit, int value) => new Tile(suit, value);

		static EnemyMahjongState EnemyWithDistinctWaits()
		{
			for (int seed = 1; seed < 500; seed++)
			{
				var enemy = new EnemyMahjongState(rank: 2, seed, doraTiles: null);
				if (TileIndex.Of(enemy.Group1.NeedTile) != TileIndex.Of(enemy.Group2.NeedTile))
					return enemy;
			}

			Assert.Fail("No deterministic enemy with distinct wait tiles found.");
			return null;
		}

		static MahjongWaitRevealDecision RevealedGroup1Decision()
		{
			return new MahjongWaitRevealDecision(
				showGroup1Need: true,
				showGroup1Shape: false,
				showGroup2Need: false,
				showGroup2Shape: false,
				newlyRevealedThisTurn: false);
		}

		static MahjongWaitRevealDecision HiddenDecision()
		{
			return new MahjongWaitRevealDecision(
				showGroup1Need: false,
				showGroup1Shape: true,
				showGroup2Need: false,
				showGroup2Shape: false,
				newlyRevealedThisTurn: false);
		}

		static MahjongDangerSource DangerSource(
			Tile waitTile,
			int damageHalfHearts,
			bool isRevealed,
			bool enemyAlive = true)
		{
			return new MahjongDangerSource(waitTile, damageHalfHearts, isRevealed, enemyAlive);
		}

		static bool SameGroup(WaitGroup a, WaitGroup b)
		{
			return a.Type == b.Type
				&& a.Slot1.Equals(b.Slot1)
				&& a.Slot2.Equals(b.Slot2)
				&& a.NeedTile.Equals(b.NeedTile);
		}

		static int ExpectedDamage(int rank, WaitGroup g)
		{
			float baseDmg;
			switch (g.Type)
			{
				case EnemyComboType.Shuntsu: baseDmg = rank * 0.5f; break;
				case EnemyComboType.Koutsu:  baseDmg = rank * 1.0f; break;
				case EnemyComboType.Toitsu:  baseDmg = rank * 0.25f; break;
				default: baseDmg = 0f; break;
			}
			return (int)System.Math.Ceiling(baseDmg + g.DoraInGroup * rank * 1.0f);
		}
	}
}
