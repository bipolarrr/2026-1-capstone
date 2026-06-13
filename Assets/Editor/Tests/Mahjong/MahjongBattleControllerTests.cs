using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Mahjong;

namespace MahjongTests
{
	public class MahjongBattleControllerTests
	{
		GameObject go;

		[TearDown]
		public void TearDown()
		{
			DestroyCurrentGameObject();
			GameSessionManager.PowerUps.Clear();
			GameSessionManager.ClearBattleEnemies();
			GameSessionManager.ResetBattleResult();
		}

		[Test]
		public void Victory_ClearsSessionBattleEnemies()
		{
			GameSessionManager.PrepareBattleEnemy(new EnemyInfo("Old Enemy", 10, 1, Color.white), false);
			GameSessionManager.ResetBattleResult();

			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();
			var localEnemies = new List<EnemyInfo>
			{
				new EnemyInfo("Defeated Enemy", 10, 1, Color.white) { hp = 0 }
			};

			typeof(BattleControllerBase)
				.GetField("enemies", BindingFlags.Instance | BindingFlags.NonPublic)
				.SetValue(controller, localEnemies);

			typeof(MahjongBattleController)
				.GetMethod("CheckVictory", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(controller, null);

			Assert.AreEqual(BattleResult.Won, GameSessionManager.LastBattleResult);
			Assert.AreEqual(0, GameSessionManager.BattleEnemyCount);
		}

		[Test]
		public void RiichiSnapshot_TenpaiDiscardOptions_CachesAvailable()
		{
			var controller = CreateControllerWithState(riichiDeclared: false);

			RefreshRiichiSnapshot(controller);
			var availability = ReadRiichiAvailability(controller);

			Assert.IsTrue(availability.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.Available, availability.Reason);
			Assert.Contains(TileIndex.Of(T(Suit.Dragon, 1)), new List<int>(availability.RiichiDiscardTileKinds));
		}

		[Test]
		public void RiichiSnapshot_AlreadyDeclared_CachesUnavailable()
		{
			var controller = CreateControllerWithState(riichiDeclared: true);

			RefreshRiichiSnapshot(controller);
			var availability = ReadRiichiAvailability(controller);

			Assert.IsFalse(availability.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.AlreadyDeclared, availability.Reason);
			Assert.AreEqual(0, availability.RiichiDiscardTileKinds.Count);
		}

		[Test]
		public void RiichiSnapshot_UninitializedState_CachesCannotActWithoutThrowing()
		{
			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();

			RefreshRiichiSnapshot(controller);
			var availability = ReadRiichiAvailability(controller);

			Assert.IsFalse(availability.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.CannotAct, availability.Reason);
		}

		[Test]
		public void RiichiButton_NoTenpaiSnapshot_DisablesButton()
		{
			var controller = CreateControllerWithNoTenpaiState();
			var button = AttachRiichiButton(controller, interactable: true);

			RefreshButtons(controller);

			var availability = ReadRiichiAvailability(controller);
			Assert.IsFalse(button.interactable);
			Assert.IsFalse(availability.CanDeclareRiichi);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.NoTenpaiDiscard, availability.Reason);
		}

		[Test]
		public void RiichiButton_TenpaiSnapshot_EnablesButton()
		{
			var controller = CreateControllerWithState(riichiDeclared: false);
			var button = AttachRiichiButton(controller, interactable: false);

			RefreshButtons(controller);

			Assert.IsTrue(button.interactable);
		}

		[Test]
		public void RiichiButton_AlreadyDeclared_DisablesButton()
		{
			var controller = CreateControllerWithState(riichiDeclared: true);
			var button = AttachRiichiButton(controller, interactable: true);

			RefreshButtons(controller);

			var availability = ReadRiichiAvailability(controller);
			Assert.IsFalse(button.interactable);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.AlreadyDeclared, availability.Reason);
		}

		[Test]
		public void RiichiButton_UninitializedOrInvalidHand_DisablesWithoutThrowing()
		{
			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();
			var button = AttachRiichiButton(controller, interactable: true);

			Assert.DoesNotThrow(() => RefreshButtons(controller));

			var availability = ReadRiichiAvailability(controller);
			Assert.IsFalse(button.interactable);
			Assert.AreEqual(MahjongRiichiAvailabilityReason.CannotAct, availability.Reason);
		}

		[Test]
		public void RiichiButton_RefreshDoesNotDeclareRiichi()
		{
			var controller = CreateControllerWithState(riichiDeclared: false);
			AttachRiichiButton(controller, interactable: false);

			RefreshButtons(controller);

			Assert.IsFalse(ReadState(controller).RiichiDeclared);
		}

		[Test]
		public void OnDeclareRiichi_NoTenpai_DoesNotDeclare()
		{
			var controller = CreateControllerWithNoTenpaiState();
			var state = ReadState(controller);
			int closedCount = state.PlayerHand.Closed.Count;
			bool hasDraw = state.PlayerHand.Draw.HasValue;
			int concealedCount = state.PlayerHand.ConcealedFourteen().Count;
			int discardCount = state.Discards.Count;

			Assert.DoesNotThrow(() => controller.OnDeclareRiichi());

			Assert.IsFalse(state.RiichiDeclared);
			Assert.AreEqual(closedCount, state.PlayerHand.Closed.Count);
			Assert.AreEqual(hasDraw, state.PlayerHand.Draw.HasValue);
			Assert.AreEqual(concealedCount, state.PlayerHand.ConcealedFourteen().Count);
			Assert.AreEqual(discardCount, state.Discards.Count);
		}

		[Test]
		public void OnDeclareRiichi_Tenpai_Declares()
		{
			var controller = CreateControllerWithState(riichiDeclared: false);
			var state = ReadState(controller);

			Assert.DoesNotThrow(() => controller.OnDeclareRiichi());

			Assert.IsTrue(state.RiichiDeclared);
		}

		[Test]
		public void OnDeclareRiichi_AlreadyDeclared_DoesNotBreak()
		{
			var controller = CreateControllerWithState(riichiDeclared: true);
			var state = ReadState(controller);
			int closedCount = state.PlayerHand.Closed.Count;
			bool hasDraw = state.PlayerHand.Draw.HasValue;
			int concealedCount = state.PlayerHand.ConcealedFourteen().Count;
			int discardCount = state.Discards.Count;

			Assert.DoesNotThrow(() => controller.OnDeclareRiichi());

			Assert.IsTrue(state.RiichiDeclared);
			Assert.AreEqual(closedCount, state.PlayerHand.Closed.Count);
			Assert.AreEqual(hasDraw, state.PlayerHand.Draw.HasValue);
			Assert.AreEqual(concealedCount, state.PlayerHand.ConcealedFourteen().Count);
			Assert.AreEqual(discardCount, state.Discards.Count);
		}

		[Test]
		public void OnDeclareRiichi_UninitializedOrInvalidHand_DoesNotThrowAndDoesNotDeclare()
		{
			var controller = CreateControllerWithUninitializedHandState();
			var state = ReadState(controller);

			Assert.DoesNotThrow(() => controller.OnDeclareRiichi());

			Assert.IsFalse(state.RiichiDeclared);
		}

		[Test]
		public void OnDeclareRiichi_DoesNotDiscardTile()
		{
			var controller = CreateControllerWithState(riichiDeclared: false);
			var state = ReadState(controller);
			int closedCount = state.PlayerHand.Closed.Count;
			bool hasDraw = state.PlayerHand.Draw.HasValue;
			int concealedCount = state.PlayerHand.ConcealedFourteen().Count;
			int discardCount = state.Discards.Count;

			controller.OnDeclareRiichi();

			Assert.IsTrue(state.RiichiDeclared);
			Assert.AreEqual(closedCount, state.PlayerHand.Closed.Count);
			Assert.AreEqual(hasDraw, state.PlayerHand.Draw.HasValue);
			Assert.AreEqual(concealedCount, state.PlayerHand.ConcealedFourteen().Count);
			Assert.AreEqual(discardCount, state.Discards.Count);
		}

		[Test]
		public void RiichiButton_StillUsesSnapshot()
		{
			var noTenpai = CreateControllerWithNoTenpaiState();
			var noTenpaiButton = AttachRiichiButton(noTenpai, interactable: true);
			RefreshButtons(noTenpai);
			Assert.IsFalse(noTenpaiButton.interactable);
			DestroyCurrentGameObject();

			var tenpai = CreateControllerWithState(riichiDeclared: false);
			var tenpaiButton = AttachRiichiButton(tenpai, interactable: false);
			RefreshButtons(tenpai);
			Assert.IsTrue(tenpaiButton.interactable);
			DestroyCurrentGameObject();

			var alreadyDeclared = CreateControllerWithState(riichiDeclared: true);
			var alreadyDeclaredButton = AttachRiichiButton(alreadyDeclared, interactable: true);
			RefreshButtons(alreadyDeclared);
			Assert.IsFalse(alreadyDeclaredButton.interactable);
			DestroyCurrentGameObject();

			var invalidHand = CreateControllerWithUninitializedHandState();
			var invalidHandButton = AttachRiichiButton(invalidHand, interactable: true);
			RefreshButtons(invalidHand);
			Assert.IsFalse(invalidHandButton.interactable);
		}

		[Test]
		public void PartialAttackButton_RefreshShowsExpectedDamage()
		{
			var controller = CreateControllerWithState(riichiDeclared: false);
			var button = AttachPartialButton(controller, out var label, interactable: false);

			RefreshButtons(controller);

			Assert.IsTrue(button.interactable);
			StringAssert.Contains("중간공격", label.text);
			StringAssert.Contains("예상 피해 1", label.text);
		}

		[Test]
		public void PartialAttackButton_WithMahjongPartialFocus_ShowsBoostedDamage()
		{
			GameSessionManager.PowerUps.Clear();
			GameSessionManager.AddPowerUp(PowerUpType.MahjongPartialFocus);
			var controller = CreateControllerWithState(riichiDeclared: false);
			var button = AttachPartialButton(controller, out var label, interactable: false);

			RefreshButtons(controller);

			Assert.IsTrue(button.interactable);
			StringAssert.Contains("예상 피해 2", label.text);
		}

		[Test]
		public void PartialAttackButton_UninitializedState_DisablesAndShowsUnavailableDamage()
		{
			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();
			var button = AttachPartialButton(controller, out var label, interactable: true);

			Assert.DoesNotThrow(() => RefreshButtons(controller));

			Assert.IsFalse(button.interactable);
			StringAssert.Contains("예상 피해 -", label.text);
		}

		[Test]
		public void EnemyWaitTilesDisplay_ShowBacked_UsesBackSpriteForHiddenSlots()
		{
			var harness = CreateWaitDisplayHarness();
			var wait = new WaitGroup(
				T(Suit.Man, 1),
				T(Suit.Man, 3),
				T(Suit.Man, 2),
				EnemyComboType.Shuntsu,
				0);

			harness.Display.Init(null);
			harness.Display.ShowBacked(wait);

			Assert.AreSame(EnemyWaitTilesDisplay.SharedBackSprite, harness.ImageA.sprite);
			Assert.AreSame(EnemyWaitTilesDisplay.SharedBackSprite, harness.ImageB.sprite);
			Assert.IsFalse(harness.MarkA.gameObject.activeSelf);
			Assert.IsFalse(harness.MarkB.gameObject.activeSelf);
			Assert.IsFalse(harness.SlotNeed.gameObject.activeSelf);
		}

		[Test]
		public void EnemyWaitTilesDisplay_RevealWaitTile_KeepsHiddenSlotsBackedAndUsesFaceSpriteForRevealedSlot()
		{
			var harness = CreateWaitDisplayHarness();
			var db = ScriptableObject.CreateInstance<MahjongTileSpriteDatabase>();
			var faceSprite = CreateSolidSprite(new Color32(235, 225, 190, 255));
			var manSprites = new Sprite[9];
			manSprites[1] = faceSprite;
			SetPrivateField(db, "manSprites", manSprites);
			var wait = new WaitGroup(
				T(Suit.Man, 1),
				T(Suit.Man, 3),
				T(Suit.Man, 2),
				EnemyComboType.Shuntsu,
				0);

			harness.Display.Init(db);
			harness.Display.RevealWaitTile(wait);
			var expectedBackSprite = db.GetBackSprite();

			Assert.IsTrue(harness.SlotA.gameObject.activeSelf);
			Assert.IsTrue(harness.SlotB.gameObject.activeSelf);
			Assert.IsTrue(harness.SlotNeed.gameObject.activeSelf);
			Assert.AreSame(expectedBackSprite, harness.ImageA.sprite);
			Assert.AreSame(expectedBackSprite, harness.ImageB.sprite);
			Assert.AreSame(faceSprite, harness.ImageNeed.sprite);
			Assert.IsFalse(harness.MarkA.gameObject.activeSelf);
			Assert.IsFalse(harness.MarkB.gameObject.activeSelf);
			Assert.IsFalse(harness.MarkNeed.gameObject.activeSelf);

			Object.DestroyImmediate(db);
		}

		[Test]
		public void TileSpriteDatabase_GetSprite_RedFiveUsesDedicatedSprite()
		{
			var db = ScriptableObject.CreateInstance<MahjongTileSpriteDatabase>();
			var normalSprite = CreateSolidSprite(new Color32(235, 225, 190, 255));
			var redTexture = CreateSolidTexture(new Color32(190, 30, 30, 255));
			var manSprites = new Sprite[9];
			manSprites[4] = normalSprite;
			SetPrivateField(db, "manSprites", manSprites);
			SetPrivateField(db, "redManFiveTexture", redTexture);

			Assert.AreSame(normalSprite, db.GetSprite(new Tile(Suit.Man, 5)));
			Assert.AreSame(redTexture, db.GetSprite(new Tile(Suit.Man, 5, isRedFive: true)).texture);
			Assert.IsTrue(db.HasDedicatedRedFiveSprite(new Tile(Suit.Man, 5, isRedFive: true)));

			Object.DestroyImmediate(db);
		}

		[Test]
		public void TileSpriteDatabase_GetSprite_HonorsAndBackUseDedicatedTextures()
		{
			var db = ScriptableObject.CreateInstance<MahjongTileSpriteDatabase>();
			var eastTexture = CreateSolidTexture(new Color32(30, 30, 30, 255));
			var greenTexture = CreateSolidTexture(new Color32(30, 150, 60, 255));
			var backTexture = CreateSolidTexture(new Color32(60, 100, 75, 255));
			var windTextures = new Texture2D[4];
			var dragonTextures = new Texture2D[3];
			windTextures[0] = eastTexture;
			dragonTextures[1] = greenTexture;
			SetPrivateField(db, "windTextures", windTextures);
			SetPrivateField(db, "dragonTextures", dragonTextures);
			SetPrivateField(db, "backTexture", backTexture);

			Assert.AreSame(eastTexture, db.GetSprite(new Tile(Suit.Wind, 1)).texture);
			Assert.AreSame(greenTexture, db.GetSprite(new Tile(Suit.Dragon, 2)).texture);
			Assert.AreSame(backTexture, db.GetBackSprite().texture);

			Object.DestroyImmediate(db);
		}

		[Test]
		public void EnemyWaitTilesDisplay_ShowBacked_WithDatabaseBackSprite_UsesDatabaseBackSprite()
		{
			var harness = CreateWaitDisplayHarness();
			var db = ScriptableObject.CreateInstance<MahjongTileSpriteDatabase>();
			var backTexture = CreateSolidTexture(new Color32(60, 100, 75, 255));
			SetPrivateField(db, "backTexture", backTexture);
			var wait = new WaitGroup(
				T(Suit.Man, 1),
				T(Suit.Man, 3),
				T(Suit.Man, 2),
				EnemyComboType.Shuntsu,
				0);

			harness.Display.Init(db);
			harness.Display.ShowBacked(wait);

			Assert.AreSame(backTexture, harness.ImageA.sprite.texture);
			Assert.AreSame(backTexture, harness.ImageB.sprite.texture);

			Object.DestroyImmediate(db);
		}

		[Test]
		public void MahjongTileVisual_Bind_RedFiveWithDedicatedSprite_DisablesRedMarker()
		{
			go = new GameObject("MahjongTileVisualTest", typeof(RectTransform));
			var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
			backgroundObject.transform.SetParent(go.transform, false);
			var redMarkerObject = new GameObject("RedMarker", typeof(RectTransform), typeof(Image));
			redMarkerObject.transform.SetParent(go.transform, false);

			var background = backgroundObject.GetComponent<Image>();
			var redMarker = redMarkerObject.GetComponent<Image>();
			var visual = go.AddComponent<MahjongTileVisual>();
			var db = ScriptableObject.CreateInstance<MahjongTileSpriteDatabase>();
			var redTexture = CreateSolidTexture(new Color32(190, 30, 30, 255));
			SetPrivateField(db, "redManFiveTexture", redTexture);
			SetPrivateField(visual, "background", background);
			SetPrivateField(visual, "redMarker", redMarker);

			visual.SetSpriteDatabase(db);
			visual.Bind(new Tile(Suit.Man, 5, isRedFive: true), null);

			Assert.AreSame(redTexture, background.sprite.texture);
			Assert.IsFalse(redMarkerObject.activeSelf);

			Object.DestroyImmediate(db);
		}

		[Test]
		public void EnemyWaitTilesDisplay_RevealWaitTile_RedFiveUsesDedicatedSprite()
		{
			var harness = CreateWaitDisplayHarness();
			var db = ScriptableObject.CreateInstance<MahjongTileSpriteDatabase>();
			var redTexture = CreateSolidTexture(new Color32(190, 30, 30, 255));
			SetPrivateField(db, "redManFiveTexture", redTexture);
			var wait = new WaitGroup(
				T(Suit.Man, 4),
				T(Suit.Man, 6),
				new Tile(Suit.Man, 5, isRedFive: true),
				EnemyComboType.Shuntsu,
				0);

			harness.Display.Init(db);
			harness.Display.RevealWaitTile(wait);

			Assert.AreSame(redTexture, harness.ImageNeed.sprite.texture);
			Assert.IsFalse(harness.MarkNeed.gameObject.activeSelf);

			Object.DestroyImmediate(db);
		}

		[Test]
		public void RefreshWaitDisplaysForNextTurn_PreviouslyRevealedWaitStaysRevealed()
		{
			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();
			var harness = CreateWaitDisplayHarness();
			harness.Display.Init(null);
			SetPrivateField(controller, "waitDisplays", new[] { harness.Display });
			SetPrivateField(controller, "enemies", new List<EnemyInfo>
			{
				new EnemyInfo("Rank3 Enemy", 10, 3, Color.white)
			});
			var enemyStates = ReadEnemyStates(controller);
			enemyStates.Clear();
			enemyStates.Add(new EnemyMahjongState(rank: 3, seed: 10, doraTiles: null));
			var revealDecisions = GetPrivateField<List<MahjongWaitRevealDecision>>(controller, "lastWaitRevealDecisions");
			revealDecisions.Clear();
			revealDecisions.Add(new MahjongWaitRevealDecision(
				showGroup1Need: true,
				showGroup1Shape: false,
				showGroup2Need: false,
				showGroup2Shape: false,
				newlyRevealedThisTurn: true));

			RefreshWaitDisplaysForNextTurn(controller, rollRank3Reveal: false);

			Assert.IsTrue(revealDecisions[0].ShowGroup1Need);
			Assert.IsFalse(revealDecisions[0].NewlyRevealedThisTurn);
			Assert.IsTrue(harness.SlotNeed.gameObject.activeSelf);
		}

		[Test]
		public void DiscardRoutine_NonTriggeringDiscard_KeepsEnemyWaitsUntilAttack()
		{
			int seed = FindSeedWhereNextRerollChangesWaits();
			var enemyState = new EnemyMahjongState(rank: 1, seed, doraTiles: null);
			var before = SnapshotWaits(enemyState);
			var postDiscardHand = NoTenpaiPostDiscardHand();
			var safeDiscard = FirstNonWaitTile(enemyState, postDiscardHand);
			var drawTile = postDiscardHand[0];
			var controller = CreateControllerForDiscardLifecycle(enemyState, safeDiscard, drawTile, postDiscardHand);
			var state = ReadState(controller);

			Assert.IsTrue(state.DiscardFromHand(safeDiscard));
			SetPrivateField(controller, "enemyTsumoChancePerTurn", 0f);
			var routine = (IEnumerator)GetPrivateMethod(controller, "DiscardRoutine")
				.Invoke(controller, new object[] { safeDiscard });

			RunCoroutineToEnd(routine);

			AssertWaitsEqual(before, ReadEnemyStates(controller)[0]);
		}

		[Test]
		public void DiscardRoutine_DiscardNotInDisplayedWait_DoesNotTriggerRon_WhenDisplayIsExhaustive()
		{
			var enemyState = EnemyWithDistinctWaits();
			var hiddenOnlyDiscard = enemyState.Group2.NeedTile;
			var before = SnapshotWaits(enemyState);
			var postDiscardHand = NoTenpaiPostDiscardHand();
			var drawTile = postDiscardHand[0];
			var controller = CreateControllerForDiscardLifecycle(enemyState, hiddenOnlyDiscard, drawTile, postDiscardHand);
			var state = ReadState(controller);
			int beforeHearts = GameSessionManager.PlayerHearts.TotalHalfHearts;
			var revealDecisions = GetPrivateField<List<MahjongWaitRevealDecision>>(controller, "lastWaitRevealDecisions");
			revealDecisions.Clear();
			revealDecisions.Add(RevealedGroup1Decision());

			Assert.IsTrue(state.DiscardFromHand(hiddenOnlyDiscard));
			SetPrivateField(controller, "enemyTsumoChancePerTurn", 0f);
			var routine = (IEnumerator)GetPrivateMethod(controller, "DiscardRoutine")
				.Invoke(controller, new object[] { hiddenOnlyDiscard });

			RunCoroutineToEnd(routine);

			Assert.AreEqual(beforeHearts, GameSessionManager.PlayerHearts.TotalHalfHearts);
			AssertWaitsEqual(before, ReadEnemyStates(controller)[0]);
		}

		[Test]
		public void PlayEnemyAttackSequence_AttackingEnemyRerollsWaits()
		{
			var discarded = T(Suit.Dragon, 3);
			var controller = CreateControllerForCounter(NoTenpaiPostDiscardHand(), discarded);
			int seed = FindSeedWhereNextRerollChangesWaits();
			var enemyState = new EnemyMahjongState(rank: 1, seed, doraTiles: null);
			ReadEnemyStates(controller)[0] = enemyState;
			var before = SnapshotWaits(enemyState);

			RunRonAttackSequence(controller, enemyIndex: 0, discarded, incomingDamage: 1);

			AssertWaitsNotEqual(before, ReadEnemyStates(controller)[0]);
		}

		[Test]
		public void NeedTileCounter_TenpaiRon_DamagesEnemyWithoutPlayerDamage()
		{
			var discarded = T(Suit.Man, 1);
			var controller = CreateControllerForCounter(TenpaiPostDiscardHand(), discarded);
			var state = ReadState(controller);
			int beforeHearts = GameSessionManager.PlayerHearts.TotalHalfHearts;
			int beforeClosedCount = state.PlayerHand.Closed.Count;
			int beforeDiscardCount = state.Discards.Count;

			RunRonAttackSequence(controller, enemyIndex: 0, discarded, incomingDamage: 6);

			var enemy = ReadEnemies(controller)[0];
			Assert.AreEqual(beforeHearts, GameSessionManager.PlayerHearts.TotalHalfHearts);
			Assert.AreEqual(10 - 4, enemy.hp);
			Assert.AreEqual(beforeClosedCount, state.PlayerHand.Closed.Count);
			Assert.AreEqual(beforeDiscardCount, state.Discards.Count);
			Assert.IsTrue(state.Discards[0].SameKind(discarded));
			AssertLogContains(ReadBattleLog(controller), "역공격! 필요한 패를 노린 적을 받아쳤다.");
			AssertLogContains(ReadBattleLog(controller), "COUNTER!");
		}

		[Test]
		public void NeedTileCounter_IishantenRon_UsesLowerCounterDamage()
		{
			var discarded = T(Suit.Man, 2);
			var postDiscard = FourteenWithFloatingDragon();
			RemoveFirstKind(postDiscard, discarded);
			var controller = CreateControllerForCounter(postDiscard, discarded);
			int beforeHearts = GameSessionManager.PlayerHearts.TotalHalfHearts;

			RunRonAttackSequence(controller, enemyIndex: 0, discarded, incomingDamage: 6);

			var enemy = ReadEnemies(controller)[0];
			Assert.AreEqual(beforeHearts, GameSessionManager.PlayerHearts.TotalHalfHearts);
			Assert.AreEqual(10 - 2, enemy.hp);
		}

		[Test]
		public void NeedTileCounter_NonNeedRon_KeepsExistingEnemyAttackDamage()
		{
			var discarded = T(Suit.Dragon, 3);
			var controller = CreateControllerForCounter(NoTenpaiPostDiscardHand(), discarded);
			int beforeHearts = GameSessionManager.PlayerHearts.TotalHalfHearts;

			Assert.IsFalse(TryResolveNeedTileCounter(controller, discarded, out var counterResult));
			Assert.AreEqual(MahjongNeedTileCounterResult.None, counterResult);

			ApplyEnemyAttackImpact(controller, enemyIndex: 0, discarded, incomingDamage: 3);

			var enemy = ReadEnemies(controller)[0];
			Assert.AreEqual(beforeHearts - 3, GameSessionManager.PlayerHearts.TotalHalfHearts);
			Assert.AreEqual(10, enemy.hp);
		}

		[Test]
		public void RonTrigger_UsesDiscardTileForShotMarkerAndLog()
		{
			var discarded = new Tile(Suit.Man, 5, isRedFive: true);
			var controller = CreateControllerForCounter(NoTenpaiPostDiscardHand(), discarded);
			var discardVisual = CreateDiscardVisual(discarded);
			var trigger = TriggerFor(discarded, incomingDamage: 3);
			int beforeHearts = GameSessionManager.PlayerHearts.TotalHalfHearts;
			object[] args = { 0, discardVisual, trigger, "론!" };

			GetPrivateMethod(controller, "ApplyEnemyAttackImpact").Invoke(controller, args);

			Assert.AreEqual(TileIndex.Of(discarded), trigger.TriggeringTileKind);
			Assert.AreEqual(TileIndex.Of(discarded), TileIndex.Of(discardVisual.Data));
			Assert.IsTrue(discardVisual.SkullOverlay.activeSelf);
			Assert.AreEqual(beforeHearts - 3, GameSessionManager.PlayerHearts.TotalHalfHearts);
			AssertLogContains(ReadBattleLog(controller), "론!");
			AssertLogContains(ReadBattleLog(controller), MahjongTileVisual.LabelFor(discarded));
		}

		[Test]
		public void MahjongSafetyCharm_EnemyAttackImpactReducesPlayerDamage()
		{
			var discarded = T(Suit.Dragon, 3);
			var controller = CreateControllerForCounter(NoTenpaiPostDiscardHand(), discarded);
			GameSessionManager.AddPowerUp(PowerUpType.MahjongSafetyCharm);
			int beforeHearts = GameSessionManager.PlayerHearts.TotalHalfHearts;

			ApplyEnemyAttackImpact(controller, enemyIndex: 0, discarded, incomingDamage: 3);

			Assert.AreEqual(beforeHearts - 2, GameSessionManager.PlayerHearts.TotalHalfHearts);
			Assert.AreEqual(10, ReadEnemies(controller)[0].hp);
		}

		[Test]
		public void NeedTileCounter_CandidateWithoutEnemyAttack_DoesNotApplyDamage()
		{
			var discarded = T(Suit.Man, 1);
			var controller = CreateControllerForCounter(TenpaiPostDiscardHand(), discarded);
			int beforeHearts = GameSessionManager.PlayerHearts.TotalHalfHearts;
			int beforeEnemyHp = ReadEnemies(controller)[0].hp;

			Assert.IsTrue(TryResolveNeedTileCounter(controller, discarded, out var counterResult));
			Assert.AreEqual(MahjongNeedTileCounterResult.Tenpai, counterResult);

			Assert.AreEqual(beforeHearts, GameSessionManager.PlayerHearts.TotalHalfHearts);
			Assert.AreEqual(beforeEnemyHp, ReadEnemies(controller)[0].hp);
			Assert.AreEqual(0, ReadBattleLog(controller).Entries.Count);
		}

		[Test]
		public void NeedTileCounter_MultipleRonEnemies_DoNotStackPlayerDamage()
		{
			var discarded = T(Suit.Man, 1);
			var controller = CreateControllerForCounter(TenpaiPostDiscardHand(), discarded, enemyCount: 2);
			int beforeHearts = GameSessionManager.PlayerHearts.TotalHalfHearts;

			RunRonAttackSequence(controller, enemyIndex: 0, discarded, incomingDamage: 6);
			RunRonAttackSequence(controller, enemyIndex: 1, discarded, incomingDamage: 6);

			var enemies = ReadEnemies(controller);
			Assert.AreEqual(beforeHearts, GameSessionManager.PlayerHearts.TotalHalfHearts);
			Assert.AreEqual(10 - 4, enemies[0].hp);
			Assert.AreEqual(10 - 4, enemies[1].hp);
		}

		static Tile T(Suit suit, int value) => new Tile(suit, value);

		static MahjongWaitRevealDecision RevealedGroup1Decision()
		{
			return new MahjongWaitRevealDecision(
				showGroup1Need: true,
				showGroup1Shape: false,
				showGroup2Need: false,
				showGroup2Shape: false,
				newlyRevealedThisTurn: false);
		}

		readonly struct EnemyWaitSnapshot
		{
			public readonly WaitGroup Group1;
			public readonly WaitGroup Group2;

			public EnemyWaitSnapshot(WaitGroup group1, WaitGroup group2)
			{
				Group1 = group1;
				Group2 = group2;
			}
		}

		readonly struct WaitDisplayHarness
		{
			public readonly EnemyWaitTilesDisplay Display;
			public readonly RectTransform SlotA;
			public readonly RectTransform SlotB;
			public readonly RectTransform SlotNeed;
			public readonly Image ImageA;
			public readonly Image ImageB;
			public readonly Image ImageNeed;
			public readonly TMP_Text MarkA;
			public readonly TMP_Text MarkB;
			public readonly TMP_Text MarkNeed;

			public WaitDisplayHarness(
				EnemyWaitTilesDisplay display,
				RectTransform slotA,
				RectTransform slotB,
				RectTransform slotNeed,
				Image imageA,
				Image imageB,
				Image imageNeed,
				TMP_Text markA,
				TMP_Text markB,
				TMP_Text markNeed)
			{
				Display = display;
				SlotA = slotA;
				SlotB = slotB;
				SlotNeed = slotNeed;
				ImageA = imageA;
				ImageB = imageB;
				ImageNeed = imageNeed;
				MarkA = markA;
				MarkB = markB;
				MarkNeed = markNeed;
			}
		}

		static List<Tile> TenpaiPostDiscardHand()
		{
			return new List<Tile>
			{
				T(Suit.Pin,1),T(Suit.Pin,2),T(Suit.Pin,3),
				T(Suit.Pin,4),T(Suit.Pin,5),T(Suit.Pin,6),
				T(Suit.Sou,7),T(Suit.Sou,8),T(Suit.Sou,9),
				T(Suit.Wind,1),T(Suit.Wind,1),
				T(Suit.Man,2),T(Suit.Man,3)
			};
		}

		static List<Tile> FourteenWithFloatingDragon()
		{
			var hand = TenpaiPostDiscardHand();
			hand.Add(T(Suit.Dragon,1));
			return hand;
		}

		static List<Tile> NoTenpaiPostDiscardHand()
		{
			return new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,4),T(Suit.Man,7),
				T(Suit.Pin,3),T(Suit.Pin,6),T(Suit.Pin,9),
				T(Suit.Sou,2),T(Suit.Sou,5),T(Suit.Sou,8),
				T(Suit.Wind,1),T(Suit.Wind,3),
				T(Suit.Dragon,1)
			};
		}

		MahjongBattleController CreateControllerForDiscardLifecycle(
			EnemyMahjongState enemyState,
			Tile discardTile,
			Tile drawTile,
			List<Tile> postDiscardHand)
		{
			GameSessionManager.PlayerHearts.Reset();
			GameSessionManager.PowerUps.Clear();
			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();

			var state = new MahjongMatchState(seed: 1);
			var initial = new List<Tile>(postDiscardHand.Count);
			bool removedDrawFromClosed = false;
			for (int i = 0; i < postDiscardHand.Count; i++)
			{
				if (!removedDrawFromClosed && postDiscardHand[i].SameKind(drawTile))
				{
					removedDrawFromClosed = true;
					continue;
				}
				initial.Add(postDiscardHand[i]);
			}
			initial.Add(discardTile);
			Assert.AreEqual(13, initial.Count);
			state.PlayerHand.DealInitial(initial);
			state.PlayerHand.SetDraw(drawTile);
			SetPrivateField(controller, "state", state);

			SetPrivateField(controller, "enemies", new List<EnemyInfo>
			{
				new EnemyInfo("Wait Holder", 100, 1, Color.white)
			});
			AttachMinimalEnemyArrays(controller, 1);
			var enemyStates = ReadEnemyStates(controller);
			enemyStates.Clear();
			enemyStates.Add(enemyState);
			return controller;
		}

		MahjongBattleController CreateControllerForCounter(List<Tile> postDiscardHand, Tile discardedTile, int enemyCount = 1)
		{
			GameSessionManager.PlayerHearts.Reset();
			GameSessionManager.PowerUps.Clear();
			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();

			var state = new MahjongMatchState(seed: 1);
			state.PlayerHand.DealInitial(postDiscardHand);
			state.Discards.Add(discardedTile);
			SetPrivateField(controller, "state", state);

			var localEnemies = new List<EnemyInfo>();
			for (int i = 0; i < enemyCount; i++)
				localEnemies.Add(new EnemyInfo($"Counter Target {i}", 10, 1, Color.white));
			SetPrivateField(controller, "enemies", localEnemies);
			AttachMinimalEnemyArrays(controller, enemyCount);

			var enemyStates = ReadEnemyStates(controller);
			enemyStates.Clear();
			for (int i = 0; i < enemyCount; i++)
				enemyStates.Add(new EnemyMahjongState(rank: 1, seed: 200 + i, doraTiles: null));

			var logObject = new GameObject("BattleLog");
			logObject.transform.SetParent(go.transform);
			var log = logObject.AddComponent<BattleLog>();
			SetPrivateField(controller, "battleLog", log);
			return controller;
		}

		void AttachMinimalEnemyArrays(MahjongBattleController controller, int enemyCount)
		{
			var panels = new GameObject[enemyCount];
			var bodies = new Image[enemyCount];
			for (int i = 0; i < enemyCount; i++)
			{
				var panel = new GameObject($"EnemyPanel{i}", typeof(RectTransform));
				panel.transform.SetParent(go.transform);
				panels[i] = panel;

				var body = new GameObject($"EnemyBody{i}", typeof(RectTransform), typeof(Image));
				body.transform.SetParent(panel.transform);
				bodies[i] = body.GetComponent<Image>();
			}
			SetPrivateField(controller, "enemyPanels", panels);
			SetPrivateField(controller, "enemyBodies", bodies);
		}

		MahjongBattleController CreateControllerWithState(bool riichiDeclared)
		{
			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();
			var state = new MahjongMatchState(seed: 1);
			state.PlayerHand.DealInitial(new List<Tile>
			{
				T(Suit.Pin,1),T(Suit.Pin,2),T(Suit.Pin,3),
				T(Suit.Pin,4),T(Suit.Pin,5),T(Suit.Pin,6),
				T(Suit.Sou,7),T(Suit.Sou,8),T(Suit.Sou,9),
				T(Suit.Wind,1),T(Suit.Wind,1),
				T(Suit.Man,2),T(Suit.Man,3)
			});
			state.PlayerHand.SetDraw(T(Suit.Dragon,1));
			state.RiichiDeclared = riichiDeclared;
			SetPrivateField(controller, "state", state);
			return controller;
		}

		MahjongBattleController CreateControllerWithUninitializedHandState()
		{
			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();
			var state = new MahjongMatchState(seed: 1)
			{
				PlayerHand = null
			};
			SetPrivateField(controller, "state", state);
			return controller;
		}

		MahjongBattleController CreateControllerWithNoTenpaiState()
		{
			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();
			var state = new MahjongMatchState(seed: 1);
			state.PlayerHand.DealInitial(new List<Tile>
			{
				T(Suit.Man,1),T(Suit.Man,2),T(Suit.Man,4),T(Suit.Man,7),
				T(Suit.Pin,3),T(Suit.Pin,6),T(Suit.Pin,9),
				T(Suit.Sou,2),T(Suit.Sou,5),T(Suit.Sou,8),
				T(Suit.Wind,1),T(Suit.Wind,3),
				T(Suit.Dragon,1)
			});
			state.PlayerHand.SetDraw(T(Suit.Dragon,3));
			SetPrivateField(controller, "state", state);
			return controller;
		}

		Button AttachRiichiButton(MahjongBattleController controller, bool interactable)
		{
			var buttonObject = new GameObject("RiichiButton");
			buttonObject.transform.SetParent(go.transform);
			var button = buttonObject.AddComponent<Button>();
			button.interactable = interactable;
			SetPrivateField(controller, "riichiButton", button);
			return button;
		}

		Button AttachPartialButton(MahjongBattleController controller, out TMP_Text label, bool interactable)
		{
			var buttonObject = new GameObject("PartialButton");
			buttonObject.transform.SetParent(go.transform);
			var button = buttonObject.AddComponent<Button>();
			button.interactable = interactable;

			var labelObject = new GameObject("Label");
			labelObject.transform.SetParent(buttonObject.transform);
			label = labelObject.AddComponent<TextMeshProUGUI>();

			SetPrivateField(controller, "tempButton1", button);
			return button;
		}

		WaitDisplayHarness CreateWaitDisplayHarness()
		{
			if (go == null)
				go = new GameObject("MahjongBattleControllerTest");

			var root = new GameObject("EnemyWaitTilesDisplay", typeof(RectTransform));
			root.transform.SetParent(go.transform, false);
			var group = root.AddComponent<CanvasGroup>();
			var display = root.AddComponent<EnemyWaitTilesDisplay>();
			var slotA = CreateWaitSlot(root.transform, "SlotA", out var imageA, out var markA);
			var slotB = CreateWaitSlot(root.transform, "SlotB", out var imageB, out var markB);
			var slotNeed = CreateWaitSlot(root.transform, "SlotNeed", out var imageNeed, out var markNeed);

			SetPrivateField(display, "slotA", slotA);
			SetPrivateField(display, "slotB", slotB);
			SetPrivateField(display, "slotNeed", slotNeed);
			SetPrivateField(display, "imgA", imageA);
			SetPrivateField(display, "imgB", imageB);
			SetPrivateField(display, "imgNeed", imageNeed);
			SetPrivateField(display, "markA", markA);
			SetPrivateField(display, "markB", markB);
			SetPrivateField(display, "markNeed", markNeed);
			SetPrivateField(display, "group", group);

			return new WaitDisplayHarness(
				display,
				slotA,
				slotB,
				slotNeed,
				imageA,
				imageB,
				imageNeed,
				markA,
				markB,
				markNeed);
		}

		static RectTransform CreateWaitSlot(Transform parent, string name, out Image image, out TMP_Text mark)
		{
			var slotObject = new GameObject(name, typeof(RectTransform), typeof(Image));
			slotObject.transform.SetParent(parent, false);
			var slot = slotObject.GetComponent<RectTransform>();
			slot.sizeDelta = new Vector2(32f, 44f);
			image = slotObject.GetComponent<Image>();

			var markObject = new GameObject("Mark", typeof(RectTransform));
			markObject.transform.SetParent(slotObject.transform, false);
			mark = markObject.AddComponent<TextMeshProUGUI>();
			mark.text = "?";
			return slot;
		}

		static Texture2D CreateSolidTexture(Color32 color)
		{
			const int size = 8;
			var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
			{
				hideFlags = HideFlags.HideAndDontSave
			};
			var pixels = new Color32[size * size];
			for (int i = 0; i < pixels.Length; i++)
				pixels[i] = color;
			texture.SetPixels32(pixels);
			texture.Apply(false, true);
			return texture;
		}

		static Sprite CreateSolidSprite(Color32 color)
		{
			const int size = 8;
			var texture = CreateSolidTexture(color);
			var sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
			sprite.hideFlags = HideFlags.HideAndDontSave;
			return sprite;
		}

		MahjongTileVisual CreateDiscardVisual(Tile tile)
		{
			if (go == null)
				go = new GameObject("MahjongBattleControllerTest");

			var visualObject = new GameObject("DiscardVisual", typeof(RectTransform));
			visualObject.transform.SetParent(go.transform, false);
			var backgroundObject = new GameObject("Background", typeof(RectTransform), typeof(Image));
			backgroundObject.transform.SetParent(visualObject.transform, false);
			var skullObject = new GameObject("SkullOverlay");
			skullObject.transform.SetParent(visualObject.transform, false);
			var visual = visualObject.AddComponent<MahjongTileVisual>();
			SetPrivateField(visual, "background", backgroundObject.GetComponent<Image>());
			SetPrivateField(visual, "skullOverlay", skullObject);

			visual.Bind(tile, null);
			return visual;
		}

		static void RefreshButtons(MahjongBattleController controller)
		{
			typeof(MahjongBattleController)
				.GetMethod("RefreshButtons", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(controller, null);
		}

		static void RefreshWaitDisplaysForNextTurn(MahjongBattleController controller, bool rollRank3Reveal)
		{
			typeof(MahjongBattleController)
				.GetMethod("RefreshWaitDisplaysForNextTurn", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(controller, new object[] { rollRank3Reveal });
		}

		static void RefreshRiichiSnapshot(MahjongBattleController controller)
		{
			typeof(MahjongBattleController)
				.GetMethod("RefreshRiichiAvailabilitySnapshot", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(controller, null);
		}

		static MahjongRiichiAvailability ReadRiichiAvailability(MahjongBattleController controller)
		{
			return (MahjongRiichiAvailability)typeof(MahjongBattleController)
				.GetField("currentRiichiAvailability", BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(controller);
		}

		static MahjongMatchState ReadState(MahjongBattleController controller)
		{
			return GetPrivateField<MahjongMatchState>(controller, "state");
		}

		static List<EnemyInfo> ReadEnemies(MahjongBattleController controller)
		{
			return GetPrivateField<List<EnemyInfo>>(controller, "enemies");
		}

		static List<EnemyMahjongState> ReadEnemyStates(MahjongBattleController controller)
		{
			return GetPrivateField<List<EnemyMahjongState>>(controller, "enemyStates");
		}

		static BattleLog ReadBattleLog(MahjongBattleController controller)
		{
			return GetPrivateField<BattleLog>(controller, "battleLog");
		}

		static bool TryResolveNeedTileCounter(
			MahjongBattleController controller,
			Tile discardedTile,
			out MahjongNeedTileCounterResult counterResult)
		{
			object[] args = { (Tile?)discardedTile, MahjongNeedTileCounterResult.None };
			bool triggered = (bool)GetPrivateMethod(controller, "TryResolveNeedTileCounter").Invoke(controller, args);
			counterResult = (MahjongNeedTileCounterResult)args[1];
			return triggered;
		}

		static void RunRonAttackSequence(
			MahjongBattleController controller,
			int enemyIndex,
			Tile discardedTile,
			int incomingDamage)
		{
			var trigger = TriggerFor(discardedTile, incomingDamage);
			object[] args = { enemyIndex, null, trigger, "론!", (Tile?)discardedTile };
			var routine = (IEnumerator)GetPrivateMethod(controller, "PlayEnemyAttackSequence").Invoke(controller, args);
			RunCoroutineToEnd(routine);
		}

		static void ApplyEnemyAttackImpact(
			MahjongBattleController controller,
			int enemyIndex,
			Tile discardedTile,
			int incomingDamage)
		{
			var trigger = TriggerFor(discardedTile, incomingDamage);
			object[] args = { enemyIndex, null, trigger, "론!" };
			GetPrivateMethod(controller, "ApplyEnemyAttackImpact").Invoke(controller, args);
		}

		static EnemyTriggerResult TriggerFor(Tile discardedTile, int incomingDamage)
		{
			return new EnemyTriggerResult
			{
				Combo = EnemyComboType.Koutsu,
				DoraCount = 0,
				RankUsed = 1,
				DamageHalfHearts = incomingDamage,
				HitGroup = new WaitGroup(discardedTile, discardedTile, discardedTile, EnemyComboType.Koutsu, 0),
				TriggeringTile = discardedTile
			};
		}

		static void RunCoroutineToEnd(IEnumerator routine, int depth = 0)
		{
			Assert.Less(depth, 8, "Coroutine nesting exceeded test guard.");
			int guard = 0;
			while (routine.MoveNext())
			{
				Assert.Less(++guard, 64, "Coroutine did not finish within test guard.");
				if (routine.Current is IEnumerator nested)
					RunCoroutineToEnd(nested, depth + 1);
			}
		}

		static void AssertLogContains(BattleLog log, string expected)
		{
			foreach (var entry in log.Entries)
			{
				if (entry.Message.Contains(expected))
					return;
			}
			Assert.Fail($"Expected battle log to contain: {expected}");
		}

		static int FindSeedWhereNextRerollChangesWaits()
		{
			for (int seed = 1; seed < 200; seed++)
			{
				var enemyState = new EnemyMahjongState(rank: 1, seed, doraTiles: null);
				var before = SnapshotWaits(enemyState);
				enemyState.Reroll(null);
				if (!WaitsEqual(before, enemyState))
					return seed;
			}
			Assert.Fail("No deterministic changing enemy wait seed found.");
			return 1;
		}

		static EnemyMahjongState EnemyWithDistinctWaits()
		{
			for (int seed = 1; seed < 500; seed++)
			{
				var enemyState = new EnemyMahjongState(rank: 1, seed, doraTiles: null);
				if (TileIndex.Of(enemyState.Group1.NeedTile) != TileIndex.Of(enemyState.Group2.NeedTile))
					return enemyState;
			}

			Assert.Fail("No deterministic enemy with distinct wait tiles found.");
			return null;
		}

		static EnemyWaitSnapshot SnapshotWaits(EnemyMahjongState enemyState)
		{
			return new EnemyWaitSnapshot(enemyState.Group1, enemyState.Group2);
		}

		static void AssertWaitsEqual(EnemyWaitSnapshot expected, EnemyMahjongState actual)
		{
			Assert.IsTrue(WaitsEqual(expected, actual), "Enemy wait groups should stay unchanged.");
		}

		static void AssertWaitsNotEqual(EnemyWaitSnapshot expected, EnemyMahjongState actual)
		{
			Assert.IsFalse(WaitsEqual(expected, actual), "Enemy wait groups should reroll after the enemy attacks.");
		}

		static bool WaitsEqual(EnemyWaitSnapshot expected, EnemyMahjongState actual)
		{
			return SameWaitGroup(expected.Group1, actual.Group1)
				&& SameWaitGroup(expected.Group2, actual.Group2);
		}

		static bool SameWaitGroup(WaitGroup a, WaitGroup b)
		{
			return a.Type == b.Type
				&& a.Slot1 == b.Slot1
				&& a.Slot2 == b.Slot2
				&& a.NeedTile == b.NeedTile
				&& a.DoraInGroup == b.DoraInGroup;
		}

		static Tile FirstNonWaitTile(EnemyMahjongState enemyState, IReadOnlyList<Tile> excludedTiles)
		{
			for (int i = 0; i < TileIndex.Count; i++)
			{
				var candidate = TileIndex.FromIndex(i);
				if (candidate.SameKind(enemyState.Group1.NeedTile)
					|| candidate.SameKind(enemyState.Group2.NeedTile)
					|| ContainsKind(excludedTiles, candidate))
				{
					continue;
				}
				return candidate;
			}
			Assert.Fail("No non-wait discard tile found.");
			return default;
		}

		static bool ContainsKind(IReadOnlyList<Tile> tiles, Tile candidate)
		{
			if (tiles == null)
				return false;
			for (int i = 0; i < tiles.Count; i++)
				if (tiles[i].SameKind(candidate))
					return true;
			return false;
		}

		static void RemoveFirstKind(List<Tile> tiles, Tile kind)
		{
			for (int i = 0; i < tiles.Count; i++)
			{
				if (!tiles[i].SameKind(kind))
					continue;
				tiles.RemoveAt(i);
				return;
			}
			Assert.Fail($"Tile kind not found: {kind}");
		}

		void DestroyCurrentGameObject()
		{
			if (go == null)
				return;
			Object.DestroyImmediate(go);
			go = null;
		}

		static void SetPrivateField(object target, string fieldName, object value)
		{
			var field = FindPrivateField(target.GetType(), fieldName);
			Assert.IsNotNull(field, $"Private field not found: {fieldName}");
			field.SetValue(target, value);
		}

		static T GetPrivateField<T>(object target, string fieldName)
		{
			var field = FindPrivateField(target.GetType(), fieldName);
			Assert.IsNotNull(field, $"Private field not found: {fieldName}");
			return (T)field.GetValue(target);
		}

		static MethodInfo GetPrivateMethod(object target, string methodName)
		{
			var type = target.GetType();
			while (type != null)
			{
				var method = type.GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
				if (method != null)
					return method;
				type = type.BaseType;
			}
			Assert.Fail($"Private method not found: {methodName}");
			return null;
		}

		static FieldInfo FindPrivateField(System.Type type, string fieldName)
		{
			while (type != null)
			{
				var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
				if (field != null)
					return field;
				type = type.BaseType;
			}
			return null;
		}
	}
}
