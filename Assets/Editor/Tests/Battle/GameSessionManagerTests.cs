using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BattleTests
{
	public class GameSessionManagerTests
	{
		[TearDown]
		public void TearDown()
		{
			GameSessionManager.StartNewGame(CharacterType.Dice);
		}

		[Test]
		public void PrepareBattleEnemies_ClonesInputEnemies()
		{
			var original = new EnemyInfo("Test Enemy", 20, 2, Color.white);
			GameSessionManager.PrepareBattleEnemies(new List<EnemyInfo> { original }, false);

			original.hp = 1;

			var snapshot = GameSessionManager.SnapshotBattleEnemies();
			Assert.AreEqual(1, snapshot.Count);
			Assert.AreEqual(20, snapshot[0].hp);
		}

		[Test]
		public void CompleteBattleWon_SetsResultAndClearsBattleEnemies()
		{
			GameSessionManager.PrepareBattleEnemy(new EnemyInfo("Test Enemy", 20, 2, Color.white), true);

			GameSessionManager.CompleteBattleWon();

			Assert.AreEqual(BattleResult.Won, GameSessionManager.LastBattleResult);
			Assert.AreEqual(0, GameSessionManager.BattleEnemyCount);
			Assert.IsFalse(GameSessionManager.IsBossBattle);
		}

		[Test]
		public void StartNewGame_ResetsMutableSessionFields()
		{
			GameSessionManager.PrepareBattleEnemy(new EnemyInfo("Test Enemy", 20, 2, Color.white), true);
			GameSessionManager.AddPowerUp(PowerUpType.ReviveOnce);
			GameSessionManager.CurrentEventIndex = 2;
			GameSessionManager.ExploreMapSeed = 42;
			GameSessionManager.CurrentExploreMapNodeId = "r2l0";
			GameSessionManager.PendingExploreMapNodeId = "r3l1";
			GameSessionManager.CancelBattle();

			GameSessionManager.StartNewGame(CharacterType.Mahjong);

			Assert.AreEqual(CharacterType.Mahjong, GameSessionManager.SelectedCharacter);
			Assert.AreEqual(0, GameSessionManager.CurrentEventIndex);
			Assert.AreNotEqual(0, GameSessionManager.ExploreMapSeed);
			Assert.AreEqual("", GameSessionManager.CurrentExploreMapNodeId);
			Assert.AreEqual("", GameSessionManager.PendingExploreMapNodeId);
			Assert.AreEqual(0, GameSessionManager.BattleEnemyCount);
			Assert.AreEqual(0, GameSessionManager.PowerUps.Count);
			Assert.AreEqual(BattleResult.None, GameSessionManager.LastBattleResult);
			Assert.IsFalse(GameSessionManager.IsBossBattle);
		}

		[Test]
		public void StartNewGame_StartsAtForestFirstRound()
		{
			GameSessionManager.CurrentStageId = Stage2Cave.Id;
			GameSessionManager.CurrentEventIndex = 8;

			GameSessionManager.StartNewGame(CharacterType.Dice);

			Assert.AreEqual(Stage1Forest.Id, GameSessionManager.CurrentStageId);
			Assert.AreEqual(0, GameSessionManager.CurrentEventIndex);
		}

		[Test]
		public void ResetExploreMapRoute_ClearsMapProgressAndCreatesSeed()
		{
			GameSessionManager.ExploreMapSeed = 0;
			GameSessionManager.CurrentExploreMapNodeId = "r8l1";
			GameSessionManager.PendingExploreMapNodeId = "r9l1";

			GameSessionManager.ResetExploreMapRoute();

			Assert.AreNotEqual(0, GameSessionManager.ExploreMapSeed);
			Assert.AreEqual("", GameSessionManager.CurrentExploreMapNodeId);
			Assert.AreEqual("", GameSessionManager.PendingExploreMapNodeId);
		}
	}

	public class PowerUpRewardCatalogTests
	{
		static readonly string[] StageIds = { Stage1Forest.Id, Stage2Cave.Id };

		[Test]
		public void DiceForestRewardSlotZero_ReturnsFixedOptionList()
		{
			var options = PowerUpRewardCatalog.GetOptions(CharacterType.Dice, Stage1Forest.Id, 0);

			Assert.AreEqual(3, options.Count);
			Assert.AreEqual(PowerUpType.OddEvenDouble, options[0].Type);
			Assert.AreEqual(PowerUpType.AllOrNothing, options[1].Type);
			Assert.AreEqual(PowerUpType.ReviveOnce, options[2].Type);
			Assert.AreEqual("홀짝 특화", options[0].Title);
		}

		[Test]
		public void SameInput_ReturnsDeterministicOptionsWithoutRandomStateDependency()
		{
			UnityEngine.Random.InitState(123);
			var first = BuildOptionSignature(
				PowerUpRewardCatalog.GetOptions(CharacterType.Mahjong, Stage1Forest.Id, 1));
			_ = UnityEngine.Random.value;

			UnityEngine.Random.InitState(987654);
			var second = BuildOptionSignature(
				PowerUpRewardCatalog.GetOptions(CharacterType.Mahjong, Stage1Forest.Id, 1));

			Assert.AreEqual(first, second);
		}

		[Test]
		public void DiceRewardTable_DoesNotContainMahjongOnlyPowerUps()
		{
			foreach (string stageId in StageIds)
			{
				for (int slot = 0; slot < PowerUpRewardCatalog.RewardSlotCount; slot++)
				{
					var options = PowerUpRewardCatalog.GetOptions(CharacterType.Dice, stageId, slot);
					Assert.Greater(options.Count, 0, $"{stageId}:{slot}");
					for (int i = 0; i < options.Count; i++)
						Assert.IsFalse(PowerUpRewardCatalog.IsMahjongOnly(options[i].Type), $"{stageId}:{slot}:{options[i].Type}");
				}
			}
		}

		[Test]
		public void MahjongRewardTable_DoesNotContainDiceOnlyPowerUps()
		{
			foreach (string stageId in StageIds)
			{
				for (int slot = 0; slot < PowerUpRewardCatalog.RewardSlotCount; slot++)
				{
					var options = PowerUpRewardCatalog.GetOptions(CharacterType.Mahjong, stageId, slot);
					Assert.Greater(options.Count, 0, $"{stageId}:{slot}");
					for (int i = 0; i < options.Count; i++)
					{
						Assert.AreNotEqual(PowerUpType.OddEvenDouble, options[i].Type, $"{stageId}:{slot}");
						Assert.AreNotEqual(PowerUpType.AllOrNothing, options[i].Type, $"{stageId}:{slot}");
						Assert.IsFalse(PowerUpRewardCatalog.IsDiceOnly(options[i].Type), $"{stageId}:{slot}:{options[i].Type}");
					}
				}
			}
		}

		[Test]
		public void StageOneAndTwo_AllRewardSlotsReturnImplementedOptions()
		{
			foreach (string stageId in StageIds)
			{
				for (int slot = 0; slot < PowerUpRewardCatalog.RewardSlotCount; slot++)
				{
					AssertImplementedOptions(PowerUpRewardCatalog.GetOptions(CharacterType.Dice, stageId, slot), $"{stageId}:Dice:{slot}");
					AssertImplementedOptions(PowerUpRewardCatalog.GetOptions(CharacterType.Mahjong, stageId, slot), $"{stageId}:Mahjong:{slot}");
				}
			}
		}

		[Test]
		public void EventIndex_MapsStageItemBoxOrderToRewardSlot()
		{
			Assert.AreEqual(0, PowerUpRewardCatalog.ResolveRewardSlotIndex(Stage1Forest.Id, 2));
			Assert.AreEqual(1, PowerUpRewardCatalog.ResolveRewardSlotIndex(Stage1Forest.Id, 4));
			Assert.AreEqual(2, PowerUpRewardCatalog.ResolveRewardSlotIndex(Stage1Forest.Id, 6));
			Assert.AreEqual(-1, PowerUpRewardCatalog.ResolveRewardSlotIndex(Stage1Forest.Id, 1));
		}

		[Test]
		public void HoldemRewardTable_IsExplicitlyOutOfScope()
		{
			var options = PowerUpRewardCatalog.GetOptions(CharacterType.Holdem, Stage1Forest.Id, 0);

			Assert.AreEqual(0, options.Count);
		}

		static void AssertImplementedOptions(IReadOnlyList<PowerUpRewardOption> options, string label)
		{
			Assert.Greater(options.Count, 0, label);
			for (int i = 0; i < options.Count; i++)
			{
				Assert.IsTrue(options[i].IsImplemented, $"{label}:{i}");
				Assert.IsTrue(options[i].IsSelectable, $"{label}:{i}");
			}
		}

		static string BuildOptionSignature(IReadOnlyList<PowerUpRewardOption> options)
		{
			var builder = new StringBuilder();
			for (int i = 0; i < options.Count; i++)
			{
				if (i > 0)
					builder.Append('|');
				builder.Append(options[i].Type)
					.Append(':')
					.Append(options[i].Title)
					.Append(':')
					.Append(options[i].Description);
			}
			return builder.ToString();
		}
	}

	public class StageRegistryTests
	{
		[Test]
		public void TryGetNextStage_UsesRegisteredStageOrder()
		{
			bool found = StageRegistry.TryGetNextStage(Stage1Forest.Id, out var nextStage);

			Assert.IsTrue(found);
			Assert.NotNull(nextStage);
			Assert.AreEqual(Stage2Cave.Id, nextStage.id);
		}

		[Test]
		public void TryGetNextStage_ReturnsFalseForLastOrUnknownStage()
		{
			Assert.IsFalse(StageRegistry.TryGetNextStage(Stage2Cave.Id, out var nextAfterCave));
			Assert.IsNull(nextAfterCave);
			Assert.IsFalse(StageRegistry.TryGetNextStage("missing", out var missingNext));
			Assert.IsNull(missingNext);
		}
	}

	public class GameExploreSceneBuilderTests
	{
		[Test]
		public void CreateMapNode_BuildsReadableStatePlateHierarchy()
		{
			var parent = new GameObject("MapNodeBuilderTestParent", typeof(RectTransform));

			try
			{
				var method = typeof(GameExploreSceneBuilder).GetMethod(
					"CreateMapNode",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.NotNull(method);

				method.Invoke(
					null,
					new object[]
					{
						parent,
						"MapNode_Test",
						new Vector2(0.5f, 0.5f),
						new Vector2(92f, 92f),
						"1-1",
						"전투",
						"전투"
					});

				var node = parent.transform.Find("MapNode_Test");
				Assert.NotNull(node);
				Assert.NotNull(node.Find("TopLabel"));
				Assert.NotNull(node.Find("IconImage"));
				Assert.NotNull(node.Find("BottomLabel"));
				Assert.NotNull(node.Find("StateShadow"));
				Assert.NotNull(node.Find("StateOutline"));
				Assert.NotNull(node.Find("StatePlate"));
				Assert.NotNull(node.Find("StateAccent"));
				Assert.NotNull(node.Find("SymbolLabel"));
				Assert.NotNull(node.Find("HoverBorderRoot"));
				Assert.NotNull(node.GetComponent<ExploreMapNodeDisplay>());
				var topLabel = node.Find("TopLabel").GetComponent<TMP_Text>();
				var topLabelRt = topLabel.GetComponent<RectTransform>();
				var stateShadowRt = node.Find("StateShadow").GetComponent<RectTransform>();
				var stateOutlineRt = node.Find("StateOutline").GetComponent<RectTransform>();
				var statePlateRt = node.Find("StatePlate").GetComponent<RectTransform>();
				var statePlateImage = node.Find("StatePlate").GetComponent<Image>();
				var stateOutlineImage = node.Find("StateOutline").GetComponent<Image>();
				var stateAccentRt = node.Find("StateAccent").GetComponent<RectTransform>();
				var stateAccentImage = node.Find("StateAccent").GetComponent<Image>();
				var iconRt = node.Find("IconImage").GetComponent<RectTransform>();
				var iconImage = node.Find("IconImage").GetComponent<Image>();
				var iconOutline = node.Find("IconImage").GetComponent<Outline>();
				var symbolLabel = node.Find("SymbolLabel").GetComponent<TMP_Text>();
				var symbolLabelRt = symbolLabel.GetComponent<RectTransform>();
				var title = node.Find("BottomLabel").GetComponent<TMP_Text>();
				var titleRt = title.GetComponent<RectTransform>();
				Assert.That(topLabel.fontSize, Is.EqualTo(18f).Within(0.0001f));
				Assert.That(title.fontSize, Is.EqualTo(42f).Within(0.0001f));
				Assert.AreEqual(new Vector2(0.5f, 0.5f), topLabelRt.anchorMin);
				Assert.AreEqual(new Vector2(0.5f, 0.5f), topLabelRt.anchorMax);
				Assert.AreEqual(new Vector2(76f, 76f), stateShadowRt.sizeDelta);
				Assert.AreEqual(new Vector2(3f, -3f), stateShadowRt.anchoredPosition);
				Assert.AreEqual(new Vector2(72f, 72f), stateOutlineRt.sizeDelta);
				Assert.AreEqual(new Vector2(64f, 64f), statePlateRt.sizeDelta);
				Assert.That(statePlateRt.localEulerAngles.z, Is.EqualTo(45f).Within(0.0001f));
				Assert.AreEqual(new Vector2(17f, 17f), stateAccentRt.sizeDelta);
				Assert.That(stateAccentRt.anchoredPosition.x, Is.EqualTo(21.76f).Within(0.0001f));
				Assert.That(stateAccentRt.anchoredPosition.y, Is.EqualTo(-21.76f).Within(0.0001f));
				Assert.Greater(statePlateImage.color.a, 0.9f);
				Assert.Greater(stateOutlineImage.color.a, 0.9f);
				Assert.AreEqual(statePlateImage.color, stateAccentImage.color);
				Assert.AreEqual(new Vector2(0.5f, 0.5f), iconRt.anchorMin);
				Assert.AreEqual(new Vector2(0.5f, 0.5f), iconRt.anchorMax);
				Assert.AreEqual(new Vector2(80f, 80f), iconRt.sizeDelta);
				Assert.AreEqual(new Vector2(64f, 64f), symbolLabelRt.sizeDelta);
				Assert.That(symbolLabel.fontSize, Is.EqualTo(34f).Within(0.0001f));
				Assert.AreEqual(new Vector2(0.5f, 0.5f), titleRt.anchorMin);
				Assert.AreEqual(new Vector2(0.5f, 0.5f), titleRt.anchorMax);
				Assert.AreEqual(new Vector2(176f, 44f), titleRt.sizeDelta);
				Assert.AreEqual(new Vector2(0f, 44f), titleRt.anchoredPosition);
				Assert.AreEqual(0f, node.GetComponent<Image>().color.a);
				Assert.IsFalse(iconImage.raycastTarget);
				Assert.IsFalse(symbolLabel.raycastTarget);
				Assert.IsFalse(topLabel.gameObject.activeSelf);
				Assert.IsFalse(title.gameObject.activeSelf);
				Assert.NotNull(iconOutline);
				Assert.IsFalse(iconOutline.enabled);
				Assert.AreEqual(new Vector2(4f, -4f), iconOutline.effectDistance);
				Assert.IsTrue(iconOutline.useGraphicAlpha);
			}
			finally
			{
				Object.DestroyImmediate(parent);
			}
		}

		[Test]
		public void CreateStoryFinalePanel_BuildsCutscenePlaceholderDialogAndReturnButton()
		{
			var parent = new GameObject("StoryFinalePanelTestParent", typeof(RectTransform));

			try
			{
				var method = typeof(GameExploreSceneBuilder).GetMethod(
					"CreateStoryFinalePanel",
					BindingFlags.Static | BindingFlags.NonPublic);
				Assert.NotNull(method);

				object[] args = { parent, null };
				var group = (CanvasGroup)method.Invoke(null, args);
				var returnButton = (GameObject)args[1];
				var panel = parent.transform.Find("VictoryPanel");
				Assert.NotNull(panel);
				var cutscenePlaceholder = panel.Find("FinaleCutscenePlaceholder/CutscenePlaceholderText").GetComponent<TMP_Text>();
				var dialogue = panel.Find("FinaleDialogue").GetComponent<TMP_Text>();
				var continued = panel.Find("ToBeContinuedText").GetComponent<TMP_Text>();

				Assert.NotNull(group);
				Assert.AreEqual(0f, group.alpha);
				Assert.IsFalse(group.blocksRaycasts);
				Assert.IsFalse(group.interactable);
				Assert.AreEqual("컷씬 준비 중", cutscenePlaceholder.text);
				StringAssert.Contains("수호자", dialogue.text);
				Assert.AreEqual("TO BE CONTINUED", continued.text);
				Assert.NotNull(returnButton);
				Assert.AreEqual("ReturnButton", returnButton.name);
				Assert.NotNull(returnButton.GetComponent<Button>());
			}
			finally
			{
				Object.DestroyImmediate(parent);
			}
		}
	}

	public class MapIconAssetTests
	{
		static readonly string[] MapIconPaths =
		{
			"Assets/UI/MapIcons/UI_MapIcon_Boss.png",
			"Assets/UI/MapIcons/UI_MapIcon_Combat.png",
			"Assets/UI/MapIcons/UI_MapIcon_Heal.png",
			"Assets/UI/MapIcons/UI_MapIcon_Shop.png"
		};

		[Test]
		public void MapIconPngs_HaveTransparentEdgesAndOpaqueCenters()
		{
			for (int i = 0; i < MapIconPaths.Length; i++)
			{
				var texture = LoadMapIconTexture(MapIconPaths[i]);
				try
				{
					AssertEdgesTransparent(texture, MapIconPaths[i]);
					Assert.GreaterOrEqual(
						texture.GetPixel(texture.width / 2, texture.height / 2).a,
						0.95f,
						MapIconPaths[i]);
				}
				finally
				{
					Object.DestroyImmediate(texture);
				}
			}
		}

		static Texture2D LoadMapIconTexture(string assetPath)
		{
			string fullPath = Path.Combine(
				Application.dataPath,
				assetPath.Substring("Assets/".Length).Replace('/', Path.DirectorySeparatorChar));
			Assert.IsTrue(File.Exists(fullPath), assetPath);

			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			texture.name = Path.GetFileName(assetPath);
			Assert.IsTrue(ImageConversion.LoadImage(texture, File.ReadAllBytes(fullPath)), assetPath);
			return texture;
		}

		static void AssertEdgesTransparent(Texture2D texture, string label)
		{
			int lastX = texture.width - 1;
			int lastY = texture.height - 1;
			AssertTransparent(texture, 0, 0, label);
			AssertTransparent(texture, lastX, 0, label);
			AssertTransparent(texture, 0, lastY, label);
			AssertTransparent(texture, lastX, lastY, label);
			AssertTransparent(texture, texture.width / 2, 0, label);
			AssertTransparent(texture, texture.width / 2, lastY, label);
			AssertTransparent(texture, 0, texture.height / 2, label);
			AssertTransparent(texture, lastX, texture.height / 2, label);
		}

		static void AssertTransparent(Texture2D texture, int x, int y, string label)
		{
			Assert.AreEqual(0f, texture.GetPixel(x, y).a, $"{label} ({x},{y})");
		}
	}

	public class DamageCalculatorPowerUpTests
	{
		[Test]
		public void OddEvenDouble_AllOddDice_DoublesExistingDamage()
		{
			var result = DamageCalculator.Calculate(
				new[] { 1, 1, 3, 3, 5 },
				new List<PowerUpType> { PowerUpType.OddEvenDouble });

			Assert.AreEqual(26, result.damage);
		}

		[Test]
		public void AllOrNothing_NoCombo_DoublesExistingDamage()
		{
			var result = DamageCalculator.Calculate(
				new[] { 1, 1, 2, 3, 5 },
				new List<PowerUpType> { PowerUpType.AllOrNothing });

			Assert.AreEqual(24, result.damage);
		}
	}

	public class ExploreMapPresentationPolicyTests
	{
		static readonly int[] RandomMapSeeds = { 0, 1, 17, 99, 12345, -987654321 };

		[Test]
		public void TryBuild_WithValidCurrentIndex_BuildsFixedTemplate()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("테스트 스테이지", 0, out var presentation);

			Assert.IsTrue(built);
			Assert.AreEqual(ExploreMapPresentationPolicy.RowCount, 10);
			Assert.AreEqual(ExploreMapPresentationPolicy.MaxNodeCount, presentation.NodeCount);
			Assert.AreEqual(ExploreMapPresentationPolicy.MaxConnectionCount, presentation.ConnectionCount);
			Assert.AreEqual("테스트 스테이지", presentation.StageTitle);
			Assert.AreEqual("1 / 9", presentation.ProgressText);
		}

		[Test]
		public void TryBuild_UsesMapTitleWithoutMapSuffix()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("숲", 0, out var presentation);

			Assert.IsTrue(built);
			Assert.AreEqual("숲", presentation.StageTitle);
			Assert.IsFalse(presentation.StageTitle.Contains("지도"));
			Assert.IsFalse(presentation.StageTitle.Contains("첨탑"));
		}

		[Test]
		public void TryBuild_WithEmptyMapTitle_LeavesStageTitleEmpty()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);

			Assert.IsTrue(built);
			Assert.AreEqual("", presentation.StageTitle);
		}

		[Test]
		public void StageTemplate_HasCombatUtilityAndFinalBossRows()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);

			Assert.IsTrue(built);

			var combatRows = new HashSet<int>();
			var utilityRows = new HashSet<int>();
			var bossRows = new HashSet<int>();
			for (int i = 0; i < presentation.NodeCount; i++)
			{
				var node = presentation.GetNode(i);
				if (node.Kind == ExploreMapNodeKind.Combat)
					combatRows.Add(node.Row);
				if (IsUtilityKind(node.Kind))
					utilityRows.Add(node.Row);
				if (node.Kind == ExploreMapNodeKind.Boss)
					bossRows.Add(node.Row);
			}

			CollectionAssert.AreEquivalent(new[] { 1, 2, 4, 6, 8 }, combatRows);
			CollectionAssert.AreEquivalent(new[] { 3, 5, 7 }, utilityRows);
			CollectionAssert.AreEquivalent(new[] { ExploreMapPresentationPolicy.BossRow }, bossRows);
		}

		[Test]
		public void CombatRows_DoNotIncludeUtilityNodes()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);

			Assert.IsTrue(built);
			for (int i = 0; i < presentation.NodeCount; i++)
			{
				var node = presentation.GetNode(i);
				if (!ExploreMapPresentationPolicy.IsCombatRow(node.Row))
					continue;

				Assert.AreEqual(ExploreMapNodeKind.Combat, node.Kind);
			}
		}

		[Test]
		public void UtilityRows_DoNotIncludeCombatNodes()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);

			Assert.IsTrue(built);
			for (int i = 0; i < presentation.NodeCount; i++)
			{
				var node = presentation.GetNode(i);
				if (!ExploreMapPresentationPolicy.IsUtilityRow(node.Row))
					continue;

				Assert.IsTrue(IsUtilityKind(node.Kind));
				Assert.AreNotEqual(ExploreMapNodeKind.Combat, node.Kind);
			}
		}

		[Test]
		public void TryBuild_DoesNotGenerateShopNodes()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);

			Assert.IsTrue(built);
			AssertNoShopNodes(presentation);
		}

		[Test]
		public void TryBuildRandom_DoesNotGenerateShopNodes()
		{
			foreach (int seed in RandomMapSeeds)
			{
				bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 0, seed, out var presentation);

				Assert.IsTrue(built);
				AssertNoShopNodes(presentation);
			}
		}

		[Test]
		public void TryBuild_FirstChoiceRowUsesOneBasedDisplayLabels()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);

			Assert.IsTrue(built);
			Assert.AreEqual("시작", presentation.GetNode(presentation.FindNodeIndex("r0l1")).IconLabel);
			Assert.AreEqual("1-1", presentation.GetNode(presentation.FindNodeIndex("r1l0")).IconLabel);
			Assert.AreEqual("1-2", presentation.GetNode(presentation.FindNodeIndex("r1l1")).IconLabel);
			Assert.AreEqual("1-3", presentation.GetNode(presentation.FindNodeIndex("r1l2")).IconLabel);
		}

		[Test]
		public void TryBuildRandom_GeneratedNodeTitlesDoNotExposeZeroBasedStageSuffix()
		{
			var config = new ExploreMapGenerationConfig(1, 3, 3);

			bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 0, config, out var presentation);

			Assert.IsTrue(built);
			var firstChoice = presentation.GetNode(presentation.FindNodeIndex("r1l0"));
			Assert.AreEqual("1-1", firstChoice.IconLabel);
			StringAssert.DoesNotContain("1-0", firstChoice.Title);
		}

		[Test]
		public void BossRow_IsSingleForcedBossNode()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 8, out var presentation);

			Assert.IsTrue(built);
			int bossCount = 0;
			for (int i = 0; i < presentation.NodeCount; i++)
			{
				var node = presentation.GetNode(i);
				if (node.Row != ExploreMapPresentationPolicy.BossRow)
					continue;

				bossCount++;
				Assert.AreEqual(ExploreMapNodeKind.Boss, node.Kind);
				Assert.IsTrue(node.IsReachable);
			}
			Assert.AreEqual(1, bossCount);
		}

		[Test]
		public void ReachableChoices_AreOnlyFromNextRow()
		{
			for (int currentEventIndex = 0; currentEventIndex < ExploreMapPresentationPolicy.EventCount; currentEventIndex++)
			{
				bool built = ExploreMapPresentationPolicy.TryBuild("", currentEventIndex, out var presentation);

				Assert.IsTrue(built);
				int reachableCount = 0;
				for (int i = 0; i < presentation.NodeCount; i++)
				{
					var node = presentation.GetNode(i);
					if (!node.IsReachable)
						continue;

					reachableCount++;
					Assert.AreEqual(currentEventIndex + 1, node.Row);
					Assert.IsTrue(node.IsSelectable);
				}
				Assert.Greater(reachableCount, 0);
			}
		}

		[Test]
		public void CurrentEventIndex_MapsDeterministicallyToCurrentRow()
		{
			for (int currentEventIndex = 0; currentEventIndex < ExploreMapPresentationPolicy.EventCount; currentEventIndex++)
			{
				bool built = ExploreMapPresentationPolicy.TryBuild("", currentEventIndex, out var presentation);

				Assert.IsTrue(built);
				Assert.AreEqual(currentEventIndex, presentation.CurrentRow);
				Assert.AreEqual(currentEventIndex + 1, presentation.CurrentEncounterRow);
				Assert.AreEqual($"{currentEventIndex + 1} / {ExploreMapPresentationPolicy.EventCount}", presentation.ProgressText);

				int currentNodeCount = 0;
				int reachableNodeCount = 0;
				for (int i = 0; i < presentation.NodeCount; i++)
				{
					var node = presentation.GetNode(i);
					if (node.IsCurrent)
					{
						currentNodeCount++;
						Assert.AreEqual(currentEventIndex, node.Row);
					}
					if (node.IsReachable)
					{
						reachableNodeCount++;
						Assert.AreEqual(currentEventIndex + 1, node.Row);
					}
					Assert.AreEqual(node.IsReachable, node.IsSelectable);
				}
				Assert.AreEqual(1, currentNodeCount);
				Assert.Greater(reachableNodeCount, 0);
			}
		}

		[Test]
		public void TryBuild_WithCurrentNodeId_RestrictsReachableChoicesToRouteEdges()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 1, "r1l0", out var presentation);

			Assert.IsTrue(built);
			Assert.AreEqual("r1l0", presentation.CurrentNodeId);

			Assert.IsTrue(presentation.GetNode(presentation.FindNodeIndex("r1l0")).IsCurrent);
			Assert.IsTrue(presentation.GetNode(presentation.FindNodeIndex("r2l0")).IsSelectable);
			Assert.IsTrue(presentation.GetNode(presentation.FindNodeIndex("r2l1")).IsSelectable);
			Assert.IsFalse(presentation.GetNode(presentation.FindNodeIndex("r2l2")).IsSelectable);
		}

		[Test]
		public void Connections_PointForwardOnly()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 4, out var presentation);
			Assert.IsTrue(built);

			for (int i = 0; i < presentation.ConnectionCount; i++)
			{
				var connection = presentation.GetConnection(i);
				var from = presentation.GetNode(connection.FromNodeIndex);
				var to = presentation.GetNode(connection.ToNodeIndex);

				Assert.AreEqual(connection.FromNodeId, from.NodeId);
				Assert.AreEqual(connection.ToNodeId, to.NodeId);
				Assert.AreEqual(from.Row + 1, to.Row);
			}
		}

		[Test]
		public void EveryNonFinalRouteNode_HasOutgoingConnection()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);
			Assert.IsTrue(built);

			for (int i = 0; i < presentation.NodeCount; i++)
			{
				var node = presentation.GetNode(i);
				if (node.Row == ExploreMapPresentationPolicy.BossRow)
				{
					Assert.AreEqual(0, node.ConnectedNextNodeCount);
					continue;
				}

				Assert.Greater(node.ConnectedNextNodeCount, 0, node.NodeId);
				for (int connectionIndex = 0; connectionIndex < node.ConnectedNextNodeCount; connectionIndex++)
				{
					string nextNodeId = node.GetConnectedNextNodeId(connectionIndex);
					int nextIndex = presentation.FindNodeIndex(nextNodeId);
					Assert.GreaterOrEqual(nextIndex, 0, nextNodeId);
					Assert.AreEqual(node.Row + 1, presentation.GetNode(nextIndex).Row);
				}
			}
		}

		[Test]
		public void LookupHelpers_ReturnExpectedNodesAndConnections()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);
			Assert.IsTrue(built);

			int startIndex = presentation.FindNodeIndex("r0l1");
			int firstCombatIndex = presentation.FindNodeIndex("r1l0");
			int missingIndex = presentation.FindNodeIndex("missing");

			Assert.GreaterOrEqual(startIndex, 0);
			Assert.GreaterOrEqual(firstCombatIndex, 0);
			Assert.AreEqual(-1, missingIndex);
			Assert.AreEqual(ExploreMapNodeKind.Start, presentation.GetNode(startIndex).Kind);
			Assert.AreEqual(ExploreMapNodeKind.Combat, presentation.GetNode(firstCombatIndex).Kind);

			int connectionIndex = presentation.FindConnectionIndex("r0l1", "r1l0");
			Assert.GreaterOrEqual(connectionIndex, 0);
			Assert.AreEqual(startIndex, presentation.GetConnection(connectionIndex).FromNodeIndex);
			Assert.AreEqual(firstCombatIndex, presentation.GetConnection(connectionIndex).ToNodeIndex);
			Assert.AreEqual(-1, presentation.FindConnectionIndex("r1l0", "r0l1"));
		}

		[Test]
		public void InvalidCurrentIndex_IsRejected()
		{
			Assert.IsFalse(ExploreMapPresentationPolicy.TryBuild("", -1, out var negativePresentation));
			Assert.AreEqual(0, negativePresentation.NodeCount);
			Assert.IsFalse(ExploreMapPresentationPolicy.TryBuild("", ExploreMapPresentationPolicy.EventCount, out var overflowPresentation));
			Assert.AreEqual(0, overflowPresentation.NodeCount);
		}

		[Test]
		public void TryBuildRandom_SameSeedAndConfig_BuildsIdenticalMap()
		{
			var config = new ExploreMapGenerationConfig(12345, 1, 3);

			bool firstBuilt = ExploreMapPresentationPolicy.TryBuildRandom("테스트 스테이지", 4, config, out var first);
			bool secondBuilt = ExploreMapPresentationPolicy.TryBuildRandom("테스트 스테이지", 4, config, out var second);

			Assert.IsTrue(firstBuilt);
			Assert.IsTrue(secondBuilt);
			Assert.AreEqual(BuildTopologySignature(first), BuildTopologySignature(second));
		}

		[Test]
		public void TryBuildRandom_DifferentSeedsUsuallyBuildDifferentShapeOrConnections()
		{
			var baselineConfig = ExploreMapGenerationConfig.CreateDefault(100);
			bool baselineBuilt = ExploreMapPresentationPolicy.TryBuildRandom("", 3, baselineConfig, out var baseline);

			Assert.IsTrue(baselineBuilt);
			string baselineSignature = BuildTopologySignature(baseline);
			bool foundDifferentMap = false;
			for (int seed = 101; seed <= 120; seed++)
			{
				var config = ExploreMapGenerationConfig.CreateDefault(seed);
				bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 3, config, out var candidate);
				Assert.IsTrue(built);
				if (BuildTopologySignature(candidate) == baselineSignature)
					continue;

				foundDifferentMap = true;
				break;
			}

			Assert.IsTrue(foundDifferentMap);
		}

		[Test]
		public void TryBuildRandom_DifferentSeedsBuildDifferentVisibleLayouts()
		{
			bool baselineBuilt = ExploreMapPresentationPolicy.TryBuildRandom("", 3, 100, out var baseline);

			Assert.IsTrue(baselineBuilt);
			string baselineSignature = BuildVisualLayoutSignature(baseline);
			bool foundDifferentLayout = false;
			for (int seed = 101; seed <= 120; seed++)
			{
				bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 3, seed, out var candidate);
				Assert.IsTrue(built);
				if (BuildVisualLayoutSignature(candidate) == baselineSignature)
					continue;

				foundDifferentLayout = true;
				break;
			}

			Assert.IsTrue(foundDifferentLayout);
		}

		[Test]
		public void TryBuildRandom_UsesJitteredVisibleNodePositions()
		{
			bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 3, 100, out var presentation);

			Assert.IsTrue(built);
			Assert.IsTrue(HasAnyJitteredMiddleNode(presentation));
		}

		[Test]
		public void ExploreMapLayout_ResolveNodeSizeUsesRestoredPlateBounds()
		{
			Assert.AreEqual(new Vector2(126f, 112f), ExploreMapLayout.ResolveNodeSize(ExploreMapNodeKind.Combat));
			Assert.AreEqual(new Vector2(134f, 118f), ExploreMapLayout.ResolveNodeSize(ExploreMapNodeKind.Start));
			Assert.AreEqual(new Vector2(146f, 128f), ExploreMapLayout.ResolveNodeSize(ExploreMapNodeKind.Boss));
		}

		[Test]
		public void ExploreMapLayout_WithReadableNodes_HasNoOverlappingBounds()
		{
			bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 3, 100, out var presentation);
			Assert.IsTrue(built);
			var config = ExploreMapLayout.CreateDefaultConfig(new Vector2(596f, 360f));

			var layout = ExploreMapLayout.Build(presentation, config);

			Assert.IsFalse(layout.HasOverlaps);
			AssertNoLayoutOverlaps(layout);
		}

		[Test]
		public void ExploreMapLayout_ContentHeightGrowsBeyondSmallViewport()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);
			Assert.IsTrue(built);
			float viewportHeight = 360f;
			var config = ExploreMapLayout.CreateDefaultConfig(new Vector2(596f, viewportHeight));

			var layout = ExploreMapLayout.Build(presentation, config);

			Assert.Greater(layout.ContentHeight, viewportHeight);
		}

		[Test]
		public void ExploreMapLayout_LaterRowsHaveGreaterYPositions()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 0, out var presentation);
			Assert.IsTrue(built);
			var layout = ExploreMapLayout.Build(
				presentation,
				ExploreMapLayout.CreateDefaultConfig(new Vector2(596f, 360f)));

			int startIndex = presentation.FindNodeIndex("r0l1");
			int bossIndex = presentation.FindNodeIndex("r9l1");

			Assert.Greater(layout.GetNode(bossIndex).Center.y, layout.GetNode(startIndex).Center.y);
		}

		[Test]
		public void ExploreMapLayout_DenseLayerSplitsIntoSubrows()
		{
			var nodes = new[]
			{
				BuildNode("r1l0_a", true, ExploreMapNodeKind.Combat, false, false, 1, 0),
				BuildNode("r1l0_b", true, ExploreMapNodeKind.Combat, false, false, 1, 0),
				BuildNode("r1l1_a", true, ExploreMapNodeKind.Combat, false, false, 1, 1),
				BuildNode("r1l1_b", true, ExploreMapNodeKind.Combat, false, false, 1, 1),
				BuildNode("r1l2_a", true, ExploreMapNodeKind.Combat, false, false, 1, 2),
				BuildNode("r1l2_b", true, ExploreMapNodeKind.Combat, false, false, 1, 2),
			};
			var presentation = BuildPresentation(nodes);
			var layout = ExploreMapLayout.Build(
				presentation,
				ExploreMapLayout.CreateDefaultConfig(new Vector2(360f, 360f)));

			Assert.Greater(CountUniqueLayoutRows(layout), 1);
			Assert.IsFalse(layout.HasOverlaps);
			AssertNoLayoutOverlaps(layout);
		}

		[Test]
		public void ExploreMapLayout_CurrentNodeScrollTargetIsClamped()
		{
			bool built = ExploreMapPresentationPolicy.TryBuild("", 8, out var presentation);
			Assert.IsTrue(built);
			var layout = ExploreMapLayout.Build(
				presentation,
				ExploreMapLayout.CreateDefaultConfig(new Vector2(596f, 360f)));
			int currentIndex = presentation.FindNodeIndex(presentation.CurrentNodeId);

			float normalizedPosition = layout.CalculateVerticalNormalizedPosition(currentIndex, 360f);

			Assert.That(normalizedPosition, Is.InRange(0f, 1f));
			Assert.Greater(normalizedPosition, 0f);
		}

		[Test]
		public void TryBuildRandom_BuildsFixedRowsAndSingleFinalBoss()
		{
			foreach (int seed in RandomMapSeeds)
			{
				bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 0, seed, out var presentation);

				Assert.IsTrue(built);
				Assert.AreEqual(ExploreMapPresentationPolicy.RowCount, CountRows(presentation), seed.ToString());

				int startCount = 0;
				int bossCount = 0;
				for (int i = 0; i < presentation.NodeCount; i++)
				{
					var node = presentation.GetNode(i);
					if (node.Row == ExploreMapPresentationPolicy.StartRow)
					{
						startCount++;
						Assert.AreEqual(ExploreMapNodeKind.Start, node.Kind);
						Assert.AreEqual(1, node.Lane);
						Assert.IsTrue(node.IsCurrent);
					}

					if (node.Row == ExploreMapPresentationPolicy.BossRow)
					{
						bossCount++;
						Assert.AreEqual(ExploreMapNodeKind.Boss, node.Kind);
						Assert.AreEqual(1, node.Lane);
					}
				}

				Assert.AreEqual(1, startCount, seed.ToString());
				Assert.AreEqual(1, bossCount, seed.ToString());
			}
		}

		[Test]
		public void TryBuildRandom_MiddleRowsIncludeCombatAndUtilityKinds()
		{
			foreach (int seed in RandomMapSeeds)
			{
				bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 0, seed, out var presentation);

				Assert.IsTrue(built);
				bool foundCombat = false;
				bool foundUtility = false;
				for (int i = 0; i < presentation.NodeCount; i++)
				{
					var node = presentation.GetNode(i);
					if (node.Row <= ExploreMapPresentationPolicy.StartRow || node.Row >= ExploreMapPresentationPolicy.BossRow)
						continue;

					if (ExploreMapPresentationPolicy.IsCombatRow(node.Row))
					{
						Assert.AreEqual(ExploreMapNodeKind.Combat, node.Kind);
						foundCombat = true;
					}

					if (ExploreMapPresentationPolicy.IsUtilityRow(node.Row))
					{
						Assert.IsTrue(IsUtilityKind(node.Kind));
						foundUtility = true;
					}
				}

				Assert.IsTrue(foundCombat, seed.ToString());
				Assert.IsTrue(foundUtility, seed.ToString());
			}
		}

		[Test]
		public void TryBuildRandom_ConnectionsPointForwardAndReferenceExistingNodes()
		{
			foreach (int seed in RandomMapSeeds)
			{
				bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 4, seed, out var presentation);

				Assert.IsTrue(built);
				for (int i = 0; i < presentation.ConnectionCount; i++)
				{
					var connection = presentation.GetConnection(i);
					int fromIndex = presentation.FindNodeIndex(connection.FromNodeId);
					int toIndex = presentation.FindNodeIndex(connection.ToNodeId);
					Assert.GreaterOrEqual(fromIndex, 0, connection.FromNodeId);
					Assert.GreaterOrEqual(toIndex, 0, connection.ToNodeId);
					Assert.AreEqual(fromIndex, connection.FromNodeIndex);
					Assert.AreEqual(toIndex, connection.ToNodeIndex);

					var from = presentation.GetNode(fromIndex);
					var to = presentation.GetNode(toIndex);
					Assert.AreEqual(from.Row + 1, to.Row, $"{connection.FromNodeId}->{connection.ToNodeId}");
				}
			}
		}

		[Test]
		public void TryBuildRandom_HasValidPathFromStartToBoss()
		{
			foreach (int seed in RandomMapSeeds)
			{
				bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 0, seed, out var presentation);

				Assert.IsTrue(built);
				string bossNodeId = FindBossNodeId(presentation);
				Assert.IsFalse(string.IsNullOrEmpty(bossNodeId));
				Assert.IsTrue(HasPath(presentation, "r0l1", bossNodeId), seed.ToString());
			}
		}

		[Test]
		public void TryBuildRandom_GraphReachableNodesHaveForwardRoute()
		{
			foreach (int seed in RandomMapSeeds)
			{
				bool built = ExploreMapPresentationPolicy.TryBuildRandom("", 0, seed, out var presentation);

				Assert.IsTrue(built);
				var graphReachableNodeIds = BuildGraphReachableNodeIds(presentation, "r0l1");
				for (int i = 0; i < presentation.NodeCount; i++)
				{
					var node = presentation.GetNode(i);
					Assert.IsTrue(graphReachableNodeIds.Contains(node.NodeId), node.NodeId);

					if (node.Row == ExploreMapPresentationPolicy.BossRow)
					{
						Assert.AreEqual(0, node.ConnectedNextNodeCount, node.NodeId);
					}
					else
					{
						Assert.Greater(node.ConnectedNextNodeCount, 0, node.NodeId);
					}

					if (node.Row != ExploreMapPresentationPolicy.StartRow)
						Assert.IsTrue(HasIncomingConnection(presentation, node.NodeId), node.NodeId);
				}
			}
		}

		[Test]
		public void TryBuildRandom_CurrentReachableAndSelectableStateMatchesCurrentIndex()
		{
			foreach (int seed in RandomMapSeeds)
			{
				for (int currentEventIndex = 0; currentEventIndex < ExploreMapPresentationPolicy.EventCount; currentEventIndex++)
				{
					bool built = ExploreMapPresentationPolicy.TryBuildRandom("", currentEventIndex, seed, out var presentation);

					Assert.IsTrue(built);
					Assert.AreEqual(currentEventIndex, presentation.CurrentRow);
					Assert.AreEqual(currentEventIndex + 1, presentation.CurrentEncounterRow);

					int currentNodeCount = 0;
					int reachableNodeCount = 0;
					string currentNodeId = "";
					for (int i = 0; i < presentation.NodeCount; i++)
					{
						var node = presentation.GetNode(i);
						if (node.IsCurrent)
						{
							currentNodeCount++;
							currentNodeId = node.NodeId;
							Assert.AreEqual(currentEventIndex, node.Row);
							Assert.AreEqual(1, node.Lane);
						}

						if (node.IsReachable)
						{
							reachableNodeCount++;
							Assert.AreEqual(currentEventIndex + 1, node.Row);
							Assert.IsTrue(node.IsSelectable);
						}
						else
						{
							Assert.IsFalse(node.IsSelectable);
						}

						Assert.AreEqual(node.Row < currentEventIndex, node.IsCompleted);
					}

					Assert.AreEqual(1, currentNodeCount);
					Assert.Greater(reachableNodeCount, 0);
					for (int i = 0; i < presentation.NodeCount; i++)
					{
						var node = presentation.GetNode(i);
						if (node.IsReachable)
							Assert.GreaterOrEqual(presentation.FindConnectionIndex(currentNodeId, node.NodeId), 0, node.NodeId);
					}
				}
			}
		}

		[Test]
		public void TryBuildRandom_InvalidConfigOrCurrentIndex_IsRejected()
		{
			AssertRejectedRandomConfig(new ExploreMapGenerationConfig(1, 0, 3));
			AssertRejectedRandomConfig(new ExploreMapGenerationConfig(1, 3, 2));
			AssertRejectedRandomConfig(new ExploreMapGenerationConfig(1, 1, 4));

			var validConfig = ExploreMapGenerationConfig.CreateDefault(1);
			Assert.IsFalse(ExploreMapPresentationPolicy.TryBuildRandom("", -1, validConfig, out var negativePresentation));
			Assert.AreEqual(0, negativePresentation.NodeCount);
			Assert.IsFalse(ExploreMapPresentationPolicy.TryBuildRandom("", ExploreMapPresentationPolicy.EventCount, validConfig, out var overflowPresentation));
			Assert.AreEqual(0, overflowPresentation.NodeCount);
		}

		static bool IsUtilityKind(ExploreMapNodeKind kind)
		{
			return kind == ExploreMapNodeKind.Heal
				|| kind == ExploreMapNodeKind.Loot;
		}

		static void AssertNoShopNodes(ExploreMapPresentation presentation)
		{
			for (int i = 0; i < presentation.NodeCount; i++)
				Assert.AreNotEqual(ExploreMapNodeKind.Shop, presentation.GetNode(i).Kind, presentation.GetNode(i).NodeId);
		}

		static void AssertRejectedRandomConfig(ExploreMapGenerationConfig config)
		{
			Assert.IsFalse(ExploreMapPresentationPolicy.TryBuildRandom("", 0, config, out var presentation));
			Assert.AreEqual(0, presentation.NodeCount);
		}

		static ExploreMapPresentation BuildPresentation(params ExploreMapNodeView[] nodes)
		{
			return new ExploreMapPresentation(
				0,
				1,
				"테스트 지도",
				"1 / 9",
				nodes,
				new ExploreMapConnection[0]);
		}

		static ExploreMapNodeView BuildNode(
			string nodeId,
			bool isReachable,
			ExploreMapNodeKind kind = ExploreMapNodeKind.Combat,
			bool isCurrent = false,
			bool isCompleted = false,
			int row = 1,
			int lane = 0)
		{
			return new ExploreMapNodeView(
				nodeId,
				row,
				lane,
				kind,
				"전투",
				"전투",
				"전투",
				isCurrent,
				isReachable,
				isReachable,
				isCompleted,
				new string[0]);
		}

		static void AssertNoLayoutOverlaps(ExploreMapLayoutResult layout)
		{
			for (int i = 0; i < layout.NodeCount; i++)
			{
				var a = layout.GetNode(i);
				if (!a.IsValid)
					continue;

				for (int j = i + 1; j < layout.NodeCount; j++)
				{
					var b = layout.GetNode(j);
					if (!b.IsValid)
						continue;
					Assert.IsFalse(a.CollisionRect.Overlaps(b.CollisionRect), $"{a.NodeId} overlaps {b.NodeId}");
				}
			}
		}

		static int CountUniqueLayoutRows(ExploreMapLayoutResult layout)
		{
			var rows = new List<float>();
			for (int i = 0; i < layout.NodeCount; i++)
			{
				var node = layout.GetNode(i);
				if (!node.IsValid)
					continue;

				bool found = false;
				for (int j = 0; j < rows.Count; j++)
				{
					if (Mathf.Abs(rows[j] - node.Center.y) > 0.01f)
						continue;

					found = true;
					break;
				}

				if (!found)
					rows.Add(node.Center.y);
			}
			return rows.Count;
		}

		static int CountRows(ExploreMapPresentation presentation)
		{
			var rows = new HashSet<int>();
			for (int i = 0; i < presentation.NodeCount; i++)
				rows.Add(presentation.GetNode(i).Row);
			return rows.Count;
		}

		static string FindBossNodeId(ExploreMapPresentation presentation)
		{
			for (int i = 0; i < presentation.NodeCount; i++)
			{
				var node = presentation.GetNode(i);
				if (node.Kind == ExploreMapNodeKind.Boss)
					return node.NodeId;
			}
			return "";
		}

		static bool HasIncomingConnection(ExploreMapPresentation presentation, string nodeId)
		{
			for (int i = 0; i < presentation.ConnectionCount; i++)
				if (presentation.GetConnection(i).ToNodeId == nodeId)
					return true;
			return false;
		}

		static bool HasPath(ExploreMapPresentation presentation, string startNodeId, string goalNodeId)
		{
			var reachableNodeIds = BuildGraphReachableNodeIds(presentation, startNodeId);
			return reachableNodeIds.Contains(goalNodeId);
		}

		static HashSet<string> BuildGraphReachableNodeIds(ExploreMapPresentation presentation, string startNodeId)
		{
			var reachableNodeIds = new HashSet<string>();
			var queue = new Queue<string>();
			reachableNodeIds.Add(startNodeId);
			queue.Enqueue(startNodeId);
			while (queue.Count > 0)
			{
				string currentNodeId = queue.Dequeue();
				for (int i = 0; i < presentation.ConnectionCount; i++)
				{
					var connection = presentation.GetConnection(i);
					if (connection.FromNodeId != currentNodeId)
						continue;
					if (reachableNodeIds.Contains(connection.ToNodeId))
						continue;

					reachableNodeIds.Add(connection.ToNodeId);
					queue.Enqueue(connection.ToNodeId);
				}
			}
			return reachableNodeIds;
		}

		static string BuildTopologySignature(ExploreMapPresentation presentation)
		{
			var builder = new StringBuilder();
			builder.Append("nodes=").Append(presentation.NodeCount)
				.Append("|connections=").Append(presentation.ConnectionCount);
			for (int i = 0; i < presentation.NodeCount; i++)
			{
				var node = presentation.GetNode(i);
				builder.Append("|n:")
					.Append(node.NodeId).Append(':')
					.Append(node.Row).Append(':')
					.Append(node.Lane).Append(':')
					.Append(node.Kind).Append(':');
				for (int connectionIndex = 0; connectionIndex < node.ConnectedNextNodeCount; connectionIndex++)
					builder.Append(node.GetConnectedNextNodeId(connectionIndex)).Append(',');
			}
			for (int i = 0; i < presentation.ConnectionCount; i++)
			{
				var connection = presentation.GetConnection(i);
				builder.Append("|c:")
					.Append(connection.FromNodeId).Append("->")
					.Append(connection.ToNodeId);
			}
			return builder.ToString();
		}

		static string BuildVisualLayoutSignature(ExploreMapPresentation presentation)
		{
			var builder = new StringBuilder();
			for (int i = 0; i < presentation.NodeCount; i++)
			{
				var node = presentation.GetNode(i);
				builder.Append("|")
					.Append(node.NodeId)
					.Append(":x").Append(Mathf.RoundToInt(node.NormalizedPosition.x * 1000f))
					.Append(":y").Append(Mathf.RoundToInt(node.NormalizedPosition.y * 1000f));
			}
			return builder.ToString();
		}

		static bool HasAnyJitteredMiddleNode(ExploreMapPresentation presentation)
		{
			for (int i = 0; i < presentation.NodeCount; i++)
			{
				var node = presentation.GetNode(i);
				if (node.Row <= ExploreMapPresentationPolicy.StartRow || node.Row >= ExploreMapPresentationPolicy.BossRow)
					continue;

				Vector2 defaultPosition = ResolveDefaultNodePosition(node.Row, node.Lane);
				if (Mathf.Abs(node.NormalizedPosition.x - defaultPosition.x) > 0.0001f)
					return true;
				if (Mathf.Abs(node.NormalizedPosition.y - defaultPosition.y) > 0.0001f)
					return true;
			}
			return false;
		}

		static Vector2 ResolveDefaultNodePosition(int row, int lane)
		{
			float y = Mathf.Lerp(0.045f, 0.955f, row / (float)ExploreMapPresentationPolicy.BossRow);
			float x;
			if (row == ExploreMapPresentationPolicy.StartRow || row == ExploreMapPresentationPolicy.BossRow)
				x = 0.5f;
			else
				x = lane == 0 ? 0.24f : lane == 1 ? 0.5f : 0.76f;
			return new Vector2(x, y);
		}
	}

	public class ExploreMapPresentationApplyTests
	{
		GameObject controllerObject;
		readonly List<Object> transientObjects = new List<Object>();

		[TearDown]
		public void TearDown()
		{
			if (controllerObject != null)
			{
				Object.DestroyImmediate(controllerObject);
				controllerObject = null;
			}
			for (int i = 0; i < transientObjects.Count; i++)
				if (transientObjects[i] != null)
					Object.DestroyImmediate(transientObjects[i]);
			transientObjects.Clear();

			GameSessionManager.StartNewGame(CharacterType.Dice);
		}

		[Test]
		public void ApplyMapNodes_DisablesRaycastsForNonReachableNodeGraphics()
		{
			var controller = CreateController();
			var slot = CreateNodeSlot("NonReachableNode");
			SetPrivateField(controller, "mapNodeRects", new[] { slot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { slot.Button });
			var presentation = BuildPresentation(BuildNode("r1l0", false));

			InvokeApplyMapNodes(controller, presentation);

			Assert.IsFalse(slot.RootGraphic.raycastTarget);
			Assert.IsFalse(slot.ChildGraphic.raycastTarget);
			Assert.IsFalse(slot.ButtonTargetGraphic.raycastTarget);
			Assert.IsFalse(slot.Button.interactable);
		}

		[Test]
		public void ApplyMapNodes_EnablesRaycastsForReachableNodeGraphics()
		{
			var controller = CreateController();
			var slot = CreateNodeSlot("ReachableNode");
			SetPrivateField(controller, "mapNodeRects", new[] { slot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { slot.Button });
			var presentation = BuildPresentation(BuildNode("r1l0", true));

			InvokeApplyMapNodes(controller, presentation);

			Assert.IsFalse(slot.RootGraphic.raycastTarget);
			Assert.IsFalse(slot.ChildGraphic.raycastTarget);
			Assert.IsTrue(slot.ButtonTargetGraphic.raycastTarget);
			Assert.IsTrue(slot.Button.interactable);
			AssertBorderStripsRaycastDisabled(slot);
		}

		[Test]
		public void ResolveMapNodeIconSprite_UsesKindIconsAndLootUsesShopIcon()
		{
			var controller = CreateController();
			var boss = CreateSprite("BossIcon");
			var shop = CreateSprite("ShopIcon");
			var heal = CreateSprite("HealIcon");
			var combat = CreateSprite("CombatIcon");
			SetPrivateField(controller, "mapBossIconSprite", boss);
			SetPrivateField(controller, "mapShopIconSprite", shop);
			SetPrivateField(controller, "mapHealIconSprite", heal);
			SetPrivateField(controller, "mapCombatIconSprite", combat);

			Assert.AreSame(boss, InvokeResolveMapNodeIconSprite(controller, ExploreMapNodeKind.Boss));
			Assert.AreSame(shop, InvokeResolveMapNodeIconSprite(controller, ExploreMapNodeKind.Shop));
			Assert.AreSame(heal, InvokeResolveMapNodeIconSprite(controller, ExploreMapNodeKind.Heal));
			Assert.AreSame(combat, InvokeResolveMapNodeIconSprite(controller, ExploreMapNodeKind.Combat));
			Assert.AreSame(combat, InvokeResolveMapNodeIconSprite(controller, ExploreMapNodeKind.Start));
			Assert.AreSame(shop, InvokeResolveMapNodeIconSprite(controller, ExploreMapNodeKind.Loot));
		}

		[Test]
		public void ApplyMapNodes_AssignsKindIconSpriteToIconImage()
		{
			var controller = CreateController();
			var slot = CreateNodeSlot("ShopNode");
			var shop = CreateSprite("ShopIcon");
			var combat = CreateSprite("CombatIcon");
			SetPrivateField(controller, "mapNodeRects", new[] { slot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { slot.Button });
			SetPrivateField(controller, "mapNodeIconImages", new[] { slot.IconImage });
			SetPrivateField(controller, "mapShopIconSprite", shop);
			SetPrivateField(controller, "mapCombatIconSprite", combat);
			var presentation = BuildPresentation(BuildNode("r1l0", true, ExploreMapNodeKind.Shop));

			InvokeApplyMapNodes(controller, presentation);

			Assert.AreSame(shop, slot.IconImage.sprite);
			Assert.AreEqual(Color.white, slot.IconImage.color);
		}

		[Test]
		public void ApplyMapNodes_AppliesReadableStatePlateColors()
		{
			var controller = CreateController();
			var currentSlot = CreateNodeSlot("CurrentNode");
			var reachableSlot = CreateNodeSlot("ReachableNode");
			var completedSlot = CreateNodeSlot("CompletedNode");
			currentSlot.ChildGraphic.color = Color.yellow;
			reachableSlot.ChildGraphic.color = Color.blue;
			completedSlot.ChildGraphic.color = Color.green;
			SetPrivateField(controller, "mapNodeRects", new[] { currentSlot.Root, reachableSlot.Root, completedSlot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { currentSlot.Button, reachableSlot.Button, completedSlot.Button });
			SetPrivateField(
				controller,
				"mapNodeGraphics",
				new Graphic[] { currentSlot.ChildGraphic, reachableSlot.ChildGraphic, completedSlot.ChildGraphic });
			var presentation = BuildPresentation(
				BuildNode("r1l0", false, ExploreMapNodeKind.Combat, true, false),
				BuildNode("r2l0", true),
				BuildNode("r0l1", false, ExploreMapNodeKind.Start, false, true));

			InvokeApplyMapNodes(controller, presentation);

			Assert.AreEqual(ExploreMapNodeDisplay.ResolveFillColor(presentation.GetNode(0)), currentSlot.ChildGraphic.color);
			Assert.AreEqual(ExploreMapNodeDisplay.ResolveFillColor(presentation.GetNode(1)), reachableSlot.ChildGraphic.color);
			Assert.AreEqual(ExploreMapNodeDisplay.ResolveFillColor(presentation.GetNode(2)), completedSlot.ChildGraphic.color);
			Assert.Greater(currentSlot.ChildGraphic.color.a, 0.8f);
			Assert.Greater(reachableSlot.ChildGraphic.color.a, 0.8f);
			Assert.Greater(completedSlot.ChildGraphic.color.a, 0.8f);
		}

		[Test]
		public void ExploreMapNodeDisplay_ShowKeepsPlateSizeAndEnlargesIconImage()
		{
			CreateController();
			var rootObject = new GameObject("RuntimeDisplayNode", typeof(RectTransform), typeof(Image));
			rootObject.transform.SetParent(controllerObject.transform);
			var root = rootObject.GetComponent<RectTransform>();
			var display = rootObject.AddComponent<ExploreMapNodeDisplay>();
			var shadow = CreateImageChild(rootObject.transform, "StateShadow");
			var outline = CreateImageChild(rootObject.transform, "StateOutline");
			var statePlate = CreateImageChild(rootObject.transform, "StatePlate");
			var stateAccent = CreateImageChild(rootObject.transform, "StateAccent");
			var iconImage = CreateImageChild(rootObject.transform, "IconImage");
			var symbolObject = new GameObject("SymbolLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
			symbolObject.transform.SetParent(rootObject.transform);
			var symbolLabel = symbolObject.GetComponent<TMP_Text>();
			SetPrivateField(display, "root", root);
			SetPrivateField(display, "shadowGraphic", shadow);
			SetPrivateField(display, "outlineGraphic", outline);
			SetPrivateField(display, "stateGraphic", statePlate);
			SetPrivateField(display, "accentGraphic", stateAccent);
			SetPrivateField(display, "iconImage", iconImage);
			SetPrivateField(display, "symbolLabel", symbolLabel);
			var iconSprite = CreateSprite("RuntimeMapIcon");
			var node = BuildNode("r1l0", true, ExploreMapNodeKind.Heal);

			display.Show(node, iconSprite, new Vector2(100f, 200f));

			Color expectedFill = ExploreMapNodeDisplay.ResolveFillColor(node);
			Assert.AreEqual(ExploreMapLayout.ResolveNodeSize(node.Kind), root.sizeDelta);
			Assert.AreEqual(new Vector2(64f, 64f), statePlate.rectTransform.sizeDelta);
			Assert.AreEqual(new Vector2(76f, 76f), shadow.rectTransform.sizeDelta);
			Assert.AreEqual(new Vector2(3f, -3f), shadow.rectTransform.anchoredPosition);
			Assert.AreEqual(new Vector2(72f, 72f), outline.rectTransform.sizeDelta);
			Assert.AreEqual(new Vector2(17f, 17f), stateAccent.rectTransform.sizeDelta);
			Assert.That(stateAccent.rectTransform.anchoredPosition.x, Is.EqualTo(21.76f).Within(0.0001f));
			Assert.That(stateAccent.rectTransform.anchoredPosition.y, Is.EqualTo(-21.76f).Within(0.0001f));
			Assert.AreEqual(new Vector2(80f, 80f), iconImage.rectTransform.sizeDelta);
			Assert.AreEqual(expectedFill, statePlate.color);
			Assert.AreEqual(expectedFill, stateAccent.color);
			Assert.IsTrue(iconImage.gameObject.activeSelf);
			Assert.IsFalse(symbolLabel.gameObject.activeSelf);
		}

		[Test]
		public void ApplyMapNodes_DisablesHoverForNonReachableNode()
		{
			var controller = CreateController();
			var slot = CreateNodeSlot("NonReachableHoverNode");
			SetPrivateField(controller, "mapNodeRects", new[] { slot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { slot.Button });
			SetPrivateField(controller, "mapNodeHoverEffects", new[] { slot.HoverEffect });
			slot.HoverEffect.SetHoverEnabled(true);
			slot.HoverEffect.OnPointerEnter(null);
			var presentation = BuildPresentation(BuildNode("r1l0", false));

			InvokeApplyMapNodes(controller, presentation);

			Assert.IsFalse(slot.HoverEffect.IsHoverEnabled);
			Assert.IsFalse(slot.HoverBorderRoot.activeSelf);
			Assert.IsFalse(slot.HoverOutline.gameObject.activeSelf);
			Assert.IsFalse(slot.IconOutline.enabled);
			Assert.AreEqual(Vector3.one, slot.Root.localScale);

			slot.HoverEffect.OnPointerEnter(null);

			Assert.IsFalse(slot.HoverBorderRoot.activeSelf);
			Assert.IsFalse(slot.HoverOutline.gameObject.activeSelf);
			Assert.IsFalse(slot.IconOutline.enabled);
		}

		[Test]
		public void HoverEffect_ShowsIconOutlineOnlyAndResetsOnExit()
		{
			CreateController();
			var slot = CreateNodeSlot("ReachableHoverBorderNode");

			slot.HoverEffect.SetHoverEnabled(true);
			slot.HoverEffect.OnPointerEnter(null);

			Assert.IsFalse(slot.HoverBorderRoot.activeSelf);
			Assert.IsFalse(slot.HoverOutline.gameObject.activeSelf);
			Assert.IsTrue(slot.IconOutline.enabled);
			AssertBorderStripsRaycastDisabled(slot);
			Assert.That(slot.Root.localScale.x, Is.EqualTo(1.06f).Within(0.0001f));
			Assert.That(slot.Root.localScale.y, Is.EqualTo(1.06f).Within(0.0001f));
			Assert.That(slot.Root.localScale.z, Is.EqualTo(1.06f).Within(0.0001f));

			slot.HoverEffect.OnPointerExit(null);

			Assert.IsFalse(slot.HoverBorderRoot.activeSelf);
			Assert.IsFalse(slot.HoverOutline.gameObject.activeSelf);
			Assert.IsFalse(slot.IconOutline.enabled);
			Assert.AreEqual(Vector3.one, slot.Root.localScale);
		}

		[Test]
		public void HoverEffect_OnDisableClearsBorderAndScale()
		{
			CreateController();
			var slot = CreateNodeSlot("DisableHoverBorderNode");
			slot.HoverEffect.SetHoverEnabled(true);
			slot.HoverEffect.OnPointerEnter(null);

			InvokeHoverEffectOnDisable(slot.HoverEffect);

			Assert.IsFalse(slot.HoverBorderRoot.activeSelf);
			Assert.IsFalse(slot.HoverOutline.gameObject.activeSelf);
			Assert.IsFalse(slot.IconOutline.enabled);
			Assert.AreEqual(Vector3.one, slot.Root.localScale);
		}

		[Test]
		public void ApplyMapNodes_ReachableNodeKeepsClickListenerAndHoverTarget()
		{
			var controller = CreateController();
			var slot = CreateNodeSlot("ReachableHoverNode");
			var presentation = BuildPresentation(BuildNode("r1l0", true, ExploreMapNodeKind.Loot));
			SetPrivateField(controller, "mapNodeRects", new[] { slot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { slot.Button });
			SetPrivateField(controller, "mapNodeHoverEffects", new[] { slot.HoverEffect });
			SetPrivateField(controller, "isMapMode", true);
			SetPrivateField(controller, "hasActiveMapPresentation", true);
			SetPrivateField(controller, "activeMapPresentation", presentation);
			SetPrivateField(controller, "itemGroup", CreateCanvasGroup("ItemGroup"));
			SetPrivateField(controller, "itemEncounterTitle", CreateText("ItemTitle"));
			SetPrivateField(controller, "itemButtons", new[] { CreateButton("Item0"), CreateButton("Item1"), CreateButton("Item2") });
			SetPrivateField(controller, "itemTitles", new[] { CreateText("Title0"), CreateText("Title1"), CreateText("Title2") });
			SetPrivateField(controller, "itemDescs", new[] { CreateText("Desc0"), CreateText("Desc1"), CreateText("Desc2") });

			InvokeApplyMapNodes(controller, presentation);
			slot.Button.onClick.Invoke();

			Assert.IsTrue(slot.Button.interactable);
			Assert.IsTrue(slot.HoverEffect.IsHoverEnabled);
			Assert.AreEqual("r1l0", GameSessionManager.PendingExploreMapNodeId);
		}

		[Test]
		public void ApplyMapNodes_DisablesUnusedSlotsAndSkipsMissingSlots()
		{
			var controller = CreateController();
			var usedSlot = CreateNodeSlot("UsedNode");
			var extraSlot = CreateNodeSlot("ExtraNode");
			SetPrivateField(controller, "mapNodeRects", new[] { usedSlot.Root, null, extraSlot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { usedSlot.Button, null, extraSlot.Button });
			var presentation = BuildPresentation(BuildNode("r1l0", true));

			Assert.DoesNotThrow(() => InvokeApplyMapNodes(controller, presentation));

			Assert.IsTrue(usedSlot.Root.gameObject.activeSelf);
			Assert.IsFalse(usedSlot.RootGraphic.raycastTarget);
			Assert.IsTrue(usedSlot.ButtonTargetGraphic.raycastTarget);
			Assert.IsFalse(extraSlot.Root.gameObject.activeSelf);
			Assert.IsFalse(extraSlot.RootGraphic.raycastTarget);
			Assert.IsFalse(extraSlot.ChildGraphic.raycastTarget);
			Assert.IsFalse(extraSlot.ButtonTargetGraphic.raycastTarget);
		}

		[Test]
		public void ApplyMapPresentation_AttachesPlayerLocationMarkerToCurrentNodeId()
		{
			var controller = CreateController();
			var currentSlot = CreateNodeSlot("CurrentNodeSlot");
			var pendingSlot = CreateNodeSlot("PendingNodeSlot");
			var marker = CreatePlayerMarker("PlayerLocationMarker");
			GameSessionManager.CurrentExploreMapNodeId = "r1l0";
			GameSessionManager.PendingExploreMapNodeId = "r2l0";
			SetPrivateField(controller, "mapNodeRects", new[] { currentSlot.Root, pendingSlot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { currentSlot.Button, pendingSlot.Button });
			SetPrivateField(controller, "playerMapMarker", marker.Root);
			var presentation = BuildPresentation(
				BuildNode("r1l0", false, ExploreMapNodeKind.Combat),
				BuildNode("r2l0", true, ExploreMapNodeKind.Loot));

			InvokeApplyMapPresentation(controller, presentation);

			Assert.AreSame(currentSlot.Root, marker.Root.parent);
			Assert.IsTrue(marker.Root.gameObject.activeSelf);
			Assert.That(marker.Root.anchoredPosition.y, Is.GreaterThan(0f));
		}

		[Test]
		public void ApplyMapPresentation_DisablesPlayerLocationMarkerRaycast()
		{
			var controller = CreateController();
			var slot = CreateNodeSlot("CurrentMarkerRaycastNode");
			var marker = CreatePlayerMarker("PlayerLocationMarker");
			marker.Image.raycastTarget = true;
			GameSessionManager.CurrentExploreMapNodeId = "r1l0";
			SetPrivateField(controller, "mapNodeRects", new[] { slot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { slot.Button });
			SetPrivateField(controller, "playerMapMarker", marker.Root);
			var presentation = BuildPresentation(BuildNode("r1l0", false));

			InvokeApplyMapPresentation(controller, presentation);

			Assert.IsFalse(marker.Image.raycastTarget);
		}

		[Test]
		public void ApplyMapPresentation_ReusesPlayerLocationMarkerAcrossRefreshes()
		{
			var controller = CreateController();
			var slot = CreateNodeSlot("CurrentMarkerReuseNode");
			var marker = CreatePlayerMarker("PlayerLocationMarker");
			GameSessionManager.CurrentExploreMapNodeId = "r1l0";
			SetPrivateField(controller, "mapNodeRects", new[] { slot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { slot.Button });
			SetPrivateField(controller, "playerMapMarker", marker.Root);
			var presentation = BuildPresentation(BuildNode("r1l0", false));

			InvokeApplyMapPresentation(controller, presentation);
			InvokeApplyMapPresentation(controller, presentation);

			Assert.AreEqual(1, CountTransformsNamed(controllerObject.transform, "PlayerLocationMarker"));
			Assert.AreSame(marker.Root, slot.Root.Find("PlayerLocationMarker"));
		}

		[Test]
		public void ApplyMapPresentation_HidesPlayerLocationMarkerWhenCurrentNodeMissing()
		{
			var controller = CreateController();
			var slot = CreateNodeSlot("MissingMarkerNode");
			var marker = CreatePlayerMarker("PlayerLocationMarker");
			marker.Root.gameObject.SetActive(true);
			GameSessionManager.CurrentExploreMapNodeId = "missing";
			SetPrivateField(controller, "mapNodeRects", new[] { slot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { slot.Button });
			SetPrivateField(controller, "playerMapMarker", marker.Root);
			var presentation = BuildPresentation(BuildNode("r1l0", false));
			LogAssert.Expect(
				LogType.Warning,
				"[Explore] Player location marker hidden: current node id 'missing' was not found in the current map presentation.");

			Assert.DoesNotThrow(() => InvokeApplyMapPresentation(controller, presentation));

			Assert.IsFalse(marker.Root.gameObject.activeSelf);
		}

		[Test]
		public void TrySelectMapNode_ReachableNodeSetsPendingRouteAndActiveKind()
		{
			var controller = CreateController();
			var presentation = BuildPresentation(BuildNode("r1l0", true, ExploreMapNodeKind.Loot));
			SetPrivateField(controller, "isMapMode", true);
			SetPrivateField(controller, "hasActiveMapPresentation", true);
			SetPrivateField(controller, "activeMapPresentation", presentation);

			bool selected = InvokeTrySelectMapNode(controller, 0);

			Assert.IsTrue(selected);
			Assert.AreEqual("r1l0", GameSessionManager.PendingExploreMapNodeId);
			Assert.AreEqual(ExploreMapNodeKind.Loot, GetPrivateField<ExploreMapNodeKind>(controller, "activeEncounterKind"));
		}

		[Test]
		public void TrySelectMapNode_NonReachableNodeIsIgnored()
		{
			var controller = CreateController();
			var presentation = BuildPresentation(BuildNode("r1l0", false, ExploreMapNodeKind.Combat));
			SetPrivateField(controller, "isMapMode", true);
			SetPrivateField(controller, "hasActiveMapPresentation", true);
			SetPrivateField(controller, "activeMapPresentation", presentation);

			bool selected = InvokeTrySelectMapNode(controller, 0);

			Assert.IsFalse(selected);
			Assert.AreEqual("", GameSessionManager.PendingExploreMapNodeId);
		}

		[Test]
		public void CompleteSelectedEncounterProgress_AdvancesIndexAndCommitsRoute()
		{
			var controller = CreateController();
			GameSessionManager.CurrentEventIndex = 0;
			GameSessionManager.CurrentExploreMapNodeId = "r0l1";
			GameSessionManager.PendingExploreMapNodeId = "r1l0";

			InvokePrivate(controller, "CompleteSelectedEncounterProgress");

			Assert.AreEqual(1, GameSessionManager.CurrentEventIndex);
			Assert.AreEqual("r1l0", GameSessionManager.CurrentExploreMapNodeId);
			Assert.AreEqual("", GameSessionManager.PendingExploreMapNodeId);
		}

		[Test]
		public void CompleteSelectedEncounterProgress_ForestBossStartsCaveAtFirstRound()
		{
			var controller = CreateController();
			GameSessionManager.StartNewGame(CharacterType.Dice);
			GameSessionManager.CurrentStageId = Stage1Forest.Id;
			GameSessionManager.CurrentEventIndex = ExploreMapPresentationPolicy.EventCount - 1;
			GameSessionManager.ExploreMapSeed = 100;
			GameSessionManager.CurrentExploreMapNodeId = "r8l1";
			GameSessionManager.PendingExploreMapNodeId = "r9l1";
			GameSessionManager.PrepareBattleEnemy(new EnemyInfo("Boss", 1, 5, Color.white), true);

			InvokePrivate(controller, "CompleteSelectedEncounterProgress");

			Assert.AreEqual(Stage2Cave.Id, GameSessionManager.CurrentStageId);
			Assert.AreEqual(0, GameSessionManager.CurrentEventIndex);
			Assert.AreNotEqual(0, GameSessionManager.ExploreMapSeed);
			Assert.AreEqual("", GameSessionManager.CurrentExploreMapNodeId);
			Assert.AreEqual("", GameSessionManager.PendingExploreMapNodeId);
			Assert.AreEqual(0, GameSessionManager.BattleEnemyCount);
			Assert.IsFalse(GameSessionManager.IsBossBattle);
		}

		[Test]
		public void CompleteSelectedEncounterProgress_CaveBossShowsFinalVictory()
		{
			var controller = CreateController();
			var victoryGroup = CreateCanvasGroup("VictoryGroup");
			SetPrivateField(controller, "victoryPanel", victoryGroup);
			GameSessionManager.StartNewGame(CharacterType.Dice);
			GameSessionManager.CurrentStageId = Stage2Cave.Id;
			GameSessionManager.CurrentEventIndex = ExploreMapPresentationPolicy.EventCount - 1;
			GameSessionManager.ExploreMapSeed = 100;
			GameSessionManager.CurrentExploreMapNodeId = "r8l1";
			GameSessionManager.PendingExploreMapNodeId = "r9l1";

			InvokePrivate(controller, "CompleteSelectedEncounterProgress");
			InvokePrivate(controller, "ShowMapMode");

			Assert.AreEqual(Stage2Cave.Id, GameSessionManager.CurrentStageId);
			Assert.AreEqual(ExploreMapPresentationPolicy.EventCount, GameSessionManager.CurrentEventIndex);
			Assert.AreEqual(1f, victoryGroup.alpha);
			Assert.IsTrue(victoryGroup.blocksRaycasts);
			Assert.IsTrue(victoryGroup.interactable);
		}

		[Test]
		public void ShowEncounterMode_CaveFinalBossShowsFinaleWithoutPreparingBattle()
		{
			var controller = CreateController();
			var victoryGroup = CreateCanvasGroup("VictoryGroup");
			var mapGroup = CreateCanvasGroup("MapGraph");
			var encounterGroup = CreateCanvasGroup("EncounterPanel");
			var combatGroup = CreateCanvasGroup("CombatGroup");
			var itemGroup = CreateCanvasGroup("ItemGroup");
			SetPrivateField(controller, "victoryPanel", victoryGroup);
			SetPrivateField(controller, "mapGraphGroup", mapGroup);
			SetPrivateField(controller, "encounterPanel", encounterGroup);
			SetPrivateField(controller, "combatGroup", combatGroup);
			SetPrivateField(controller, "itemGroup", itemGroup);
			SetPrivateField(controller, "powerUpText", CreateText("PowerUps"));

			GameSessionManager.StartNewGame(CharacterType.Dice);
			GameSessionManager.CurrentStageId = Stage2Cave.Id;
			GameSessionManager.CurrentEventIndex = ExploreMapPresentationPolicy.EventCount - 1;
			GameSessionManager.ExploreMapSeed = 100;
			GameSessionManager.CurrentExploreMapNodeId = "r8l1";
			GameSessionManager.PendingExploreMapNodeId = "r9l1";
			GameSessionManager.PrepareBattleEnemy(new EnemyInfo("Existing Enemy", 10, 1, Color.white), true);

			InvokeShowEncounterMode(controller, ExploreMapNodeKind.Boss, "r9l1");

			Assert.AreEqual(Stage2Cave.Id, GameSessionManager.CurrentStageId);
			Assert.AreEqual(ExploreMapPresentationPolicy.EventCount, GameSessionManager.CurrentEventIndex);
			Assert.AreEqual("r9l1", GameSessionManager.CurrentExploreMapNodeId);
			Assert.AreEqual("", GameSessionManager.PendingExploreMapNodeId);
			Assert.AreEqual(0, GameSessionManager.BattleEnemyCount);
			Assert.IsFalse(GameSessionManager.IsBossBattle);
			Assert.AreEqual(1f, victoryGroup.alpha);
			Assert.IsTrue(victoryGroup.blocksRaycasts);
			Assert.IsTrue(victoryGroup.interactable);
			Assert.AreEqual(0f, combatGroup.alpha);
			Assert.AreEqual(0f, encounterGroup.alpha);
			Assert.AreEqual(0f, itemGroup.alpha);
		}

		[Test]
		public void CompleteSelectedEncounterProgress_ForestCombatDoesNotStartCave()
		{
			var controller = CreateController();
			GameSessionManager.StartNewGame(CharacterType.Dice);
			GameSessionManager.CurrentStageId = Stage1Forest.Id;
			GameSessionManager.CurrentEventIndex = 0;
			GameSessionManager.ExploreMapSeed = 100;
			GameSessionManager.CurrentExploreMapNodeId = "r0l1";
			GameSessionManager.PendingExploreMapNodeId = "r1l0";

			InvokePrivate(controller, "CompleteSelectedEncounterProgress");

			Assert.AreEqual(Stage1Forest.Id, GameSessionManager.CurrentStageId);
			Assert.AreEqual(1, GameSessionManager.CurrentEventIndex);
			Assert.AreEqual("r1l0", GameSessionManager.CurrentExploreMapNodeId);
		}

		[Test]
		public void CompleteSelectedEncounterProgress_UtilityNodeAdvancesWithoutStageTransition()
		{
			var controller = CreateController();
			GameSessionManager.StartNewGame(CharacterType.Dice);
			GameSessionManager.CurrentStageId = Stage1Forest.Id;
			GameSessionManager.CurrentEventIndex = 2;
			GameSessionManager.ExploreMapSeed = 100;
			GameSessionManager.CurrentExploreMapNodeId = "r2l1";
			GameSessionManager.PendingExploreMapNodeId = "r3l2";

			InvokePrivate(controller, "CompleteSelectedEncounterProgress");

			Assert.AreEqual(Stage1Forest.Id, GameSessionManager.CurrentStageId);
			Assert.AreEqual(3, GameSessionManager.CurrentEventIndex);
			Assert.AreEqual("r3l2", GameSessionManager.CurrentExploreMapNodeId);
			Assert.AreEqual("", GameSessionManager.PendingExploreMapNodeId);
		}

		[Test]
		public void ShowMapMode_HidesEncounterGroupsAndShowsMapGraph()
		{
			var controller = CreateController();
			var mapGroup = CreateCanvasGroup("MapGraph");
			var encounterGroup = CreateCanvasGroup("EncounterPanel");
			var combatGroup = CreateCanvasGroup("CombatGroup");
			var itemGroup = CreateCanvasGroup("ItemGroup");
			var victoryGroup = CreateCanvasGroup("VictoryGroup");
			var nodeRoot = CreateRect("MapNodeRoot");
			var connectionRoot = CreateRect("MapConnectionRoot");
			var nodeSlot = CreateNodeSlot("MapNodeSlot");
			encounterGroup.alpha = 1f;
			combatGroup.alpha = 1f;
			itemGroup.alpha = 1f;
			GameSessionManager.ExploreMapSeed = 100;
			GameSessionManager.CurrentEventIndex = 0;
			GameSessionManager.CurrentExploreMapNodeId = "r0l1";

			SetPrivateField(controller, "mapGraphGroup", mapGroup);
			SetPrivateField(controller, "encounterPanel", encounterGroup);
			SetPrivateField(controller, "combatGroup", combatGroup);
			SetPrivateField(controller, "itemGroup", itemGroup);
			SetPrivateField(controller, "victoryPanel", victoryGroup);
			SetPrivateField(controller, "mapNodeRoot", nodeRoot);
			SetPrivateField(controller, "mapConnectionRoot", connectionRoot);
			SetPrivateField(controller, "mapNodeRects", new[] { nodeSlot.Root });
			SetPrivateField(controller, "mapNodeButtons", new[] { nodeSlot.Button });

			InvokePrivate(controller, "ShowMapMode");

			Assert.AreEqual(1f, mapGroup.alpha);
			Assert.AreEqual(0f, encounterGroup.alpha);
			Assert.AreEqual(0f, combatGroup.alpha);
			Assert.AreEqual(0f, itemGroup.alpha);
		}

		[Test]
		public void ShowEncounterMode_ForItemNodeHidesMapAndShowsItemEncounter()
		{
			var controller = CreateController();
			var mapGroup = CreateCanvasGroup("MapGraph");
			var encounterGroup = CreateCanvasGroup("EncounterPanel");
			var combatGroup = CreateCanvasGroup("CombatGroup");
			var itemGroup = CreateCanvasGroup("ItemGroup");
			var victoryGroup = CreateCanvasGroup("VictoryGroup");
			mapGroup.alpha = 1f;

			SetPrivateField(controller, "mapGraphGroup", mapGroup);
			SetPrivateField(controller, "encounterPanel", encounterGroup);
			SetPrivateField(controller, "combatGroup", combatGroup);
			SetPrivateField(controller, "itemGroup", itemGroup);
			SetPrivateField(controller, "victoryPanel", victoryGroup);
			SetPrivateField(controller, "itemEncounterTitle", CreateText("ItemTitle"));
			SetPrivateField(controller, "itemButtons", new[] { CreateButton("Item0"), CreateButton("Item1"), CreateButton("Item2") });
			SetPrivateField(controller, "itemTitles", new[] { CreateText("Title0"), CreateText("Title1"), CreateText("Title2") });
			SetPrivateField(controller, "itemDescs", new[] { CreateText("Desc0"), CreateText("Desc1"), CreateText("Desc2") });

			InvokeShowEncounterMode(controller, ExploreMapNodeKind.Loot, "r1l0");

			Assert.AreEqual(0f, mapGroup.alpha);
			Assert.AreEqual(1f, encounterGroup.alpha);
			Assert.AreEqual(1f, itemGroup.alpha);
			Assert.AreEqual(0f, combatGroup.alpha);
		}

		[Test]
		public void ShowEncounterMode_ForLootNodeDisplaysCatalogOptions()
		{
			GameSessionManager.StartNewGame(CharacterType.Mahjong);
			GameSessionManager.CurrentStageId = Stage1Forest.Id;
			GameSessionManager.CurrentEventIndex = 2;
			var controller = CreateController();
			var encounterGroup = CreateCanvasGroup("EncounterPanel");
			var itemGroup = CreateCanvasGroup("ItemGroup");
			var itemButtons = new[] { CreateButton("Item0"), CreateButton("Item1"), CreateButton("Item2") };
			var itemTitles = new[] { CreateText("Title0"), CreateText("Title1"), CreateText("Title2") };
			var itemDescs = new[] { CreateText("Desc0"), CreateText("Desc1"), CreateText("Desc2") };

			SetPrivateField(controller, "encounterPanel", encounterGroup);
			SetPrivateField(controller, "itemGroup", itemGroup);
			SetPrivateField(controller, "itemEncounterTitle", CreateText("ItemTitle"));
			SetPrivateField(controller, "itemButtons", itemButtons);
			SetPrivateField(controller, "itemTitles", itemTitles);
			SetPrivateField(controller, "itemDescs", itemDescs);

			InvokeShowEncounterMode(controller, ExploreMapNodeKind.Loot, "r3l1");

			var options = PowerUpRewardCatalog.GetOptions(CharacterType.Mahjong, Stage1Forest.Id, 0);
			for (int i = 0; i < options.Count; i++)
			{
				Assert.IsTrue(itemButtons[i].gameObject.activeSelf);
				Assert.IsTrue(itemButtons[i].interactable);
				Assert.AreEqual(options[i].Title, itemTitles[i].text);
				Assert.AreEqual(options[i].Description, itemDescs[i].text);
				Assert.IsFalse(PowerUpRewardCatalog.IsDiceOnly(options[i].Type));
			}
		}

		[Test]
		public void ItemSelection_AddsCatalogPowerUpAndContinuesProgression()
		{
			GameSessionManager.StartNewGame(CharacterType.Dice);
			GameSessionManager.CurrentStageId = Stage1Forest.Id;
			GameSessionManager.CurrentEventIndex = 2;
			GameSessionManager.CurrentExploreMapNodeId = "r2l1";
			GameSessionManager.PendingExploreMapNodeId = "r3l1";
			var controller = CreateController();
			var encounterGroup = CreateCanvasGroup("EncounterPanel");
			var itemGroup = CreateCanvasGroup("ItemGroup");
			var itemButtons = new[] { CreateButton("Item0"), CreateButton("Item1"), CreateButton("Item2") };

			SetPrivateField(controller, "encounterPanel", encounterGroup);
			SetPrivateField(controller, "itemGroup", itemGroup);
			SetPrivateField(controller, "itemEncounterTitle", CreateText("ItemTitle"));
			SetPrivateField(controller, "itemButtons", itemButtons);
			SetPrivateField(controller, "itemTitles", new[] { CreateText("Title0"), CreateText("Title1"), CreateText("Title2") });
			SetPrivateField(controller, "itemDescs", new[] { CreateText("Desc0"), CreateText("Desc1"), CreateText("Desc2") });
			SetPrivateField(controller, "powerUpText", CreateText("PowerUps"));

			var options = PowerUpRewardCatalog.GetOptions(CharacterType.Dice, Stage1Forest.Id, 0);
			InvokeShowEncounterMode(controller, ExploreMapNodeKind.Loot, "r3l1");

			itemButtons[0].onClick.Invoke();

			Assert.Contains(options[0].Type, GameSessionManager.PowerUps);
			Assert.AreEqual(3, GameSessionManager.CurrentEventIndex);
			Assert.AreEqual("r3l1", GameSessionManager.CurrentExploreMapNodeId);
			Assert.AreEqual("", GameSessionManager.PendingExploreMapNodeId);
		}

		GameExploreController CreateController()
		{
			controllerObject = new GameObject("ExploreMapPresentationApplyTests");
			return controllerObject.AddComponent<GameExploreController>();
		}

		CanvasGroup CreateCanvasGroup(string name)
		{
			var groupObject = new GameObject(name, typeof(RectTransform), typeof(CanvasGroup));
			groupObject.transform.SetParent(controllerObject.transform);
			return groupObject.GetComponent<CanvasGroup>();
		}

		RectTransform CreateRect(string name)
		{
			var rectObject = new GameObject(name, typeof(RectTransform));
			rectObject.transform.SetParent(controllerObject.transform);
			return rectObject.GetComponent<RectTransform>();
		}

		Button CreateButton(string name)
		{
			var buttonObject = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
			buttonObject.transform.SetParent(controllerObject.transform);
			return buttonObject.GetComponent<Button>();
		}

		TMP_Text CreateText(string name)
		{
			var textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
			textObject.transform.SetParent(controllerObject.transform);
			return textObject.GetComponent<TMP_Text>();
		}

		PlayerMarker CreatePlayerMarker(string name)
		{
			var markerObject = new GameObject(name, typeof(RectTransform), typeof(Image));
			markerObject.transform.SetParent(controllerObject.transform);
			markerObject.SetActive(false);
			var image = markerObject.GetComponent<Image>();
			image.raycastTarget = true;
			return new PlayerMarker(markerObject.GetComponent<RectTransform>(), image);
		}

		NodeSlot CreateNodeSlot(string name)
		{
			var rootObject = new GameObject(name, typeof(RectTransform), typeof(Image));
			rootObject.transform.SetParent(controllerObject.transform);
			var root = rootObject.GetComponent<RectTransform>();
			var rootGraphic = rootObject.GetComponent<Image>();
			rootGraphic.raycastTarget = true;

			var childObject = new GameObject("ChildGraphic", typeof(RectTransform), typeof(Image));
			childObject.transform.SetParent(rootObject.transform);
			var childGraphic = childObject.GetComponent<Image>();
			childGraphic.raycastTarget = true;

			var iconObject = new GameObject("IconImage", typeof(RectTransform), typeof(Image));
			iconObject.transform.SetParent(rootObject.transform);
			var iconImage = iconObject.GetComponent<Image>();
			iconImage.raycastTarget = true;
			var iconOutline = iconObject.AddComponent<Outline>();
			iconOutline.effectDistance = new Vector2(4f, -4f);
			iconOutline.useGraphicAlpha = true;
			iconOutline.enabled = false;

			var outlineObject = new GameObject("HoverOutline", typeof(RectTransform), typeof(Image));
			outlineObject.transform.SetParent(rootObject.transform);
			var hoverOutline = outlineObject.GetComponent<Image>();
			hoverOutline.raycastTarget = false;
			outlineObject.SetActive(false);

			var hoverBorderRootObject = new GameObject("HoverBorderRoot", typeof(RectTransform));
			hoverBorderRootObject.transform.SetParent(rootObject.transform);
			var hoverBorderStrips = new[]
			{
				CreateBorderStrip(hoverBorderRootObject.transform, "BorderTop"),
				CreateBorderStrip(hoverBorderRootObject.transform, "BorderBottom"),
				CreateBorderStrip(hoverBorderRootObject.transform, "BorderLeft"),
				CreateBorderStrip(hoverBorderRootObject.transform, "BorderRight")
			};
			hoverBorderRootObject.SetActive(false);

			var targetObject = new GameObject("ButtonTargetGraphic", typeof(RectTransform), typeof(Image));
			targetObject.transform.SetParent(controllerObject.transform);
			var buttonTargetGraphic = targetObject.GetComponent<Image>();
			buttonTargetGraphic.raycastTarget = true;

			var button = rootObject.AddComponent<Button>();
			button.targetGraphic = buttonTargetGraphic;
			button.interactable = true;

			var hoverEffect = rootObject.AddComponent<ExploreMapNodeHoverEffect>();
			SetPrivateField(hoverEffect, "animatedTarget", root);
			SetPrivateField(hoverEffect, "outlineImage", hoverOutline);
			SetPrivateField(hoverEffect, "borderRoot", hoverBorderRootObject);
			SetPrivateField(hoverEffect, "iconOutline", iconOutline);

			return new NodeSlot(
				root,
				rootGraphic,
				childGraphic,
				iconImage,
				iconOutline,
				hoverOutline,
				hoverBorderRootObject,
				hoverBorderStrips,
				hoverEffect,
				buttonTargetGraphic,
				button);
		}

		Image CreateBorderStrip(Transform parent, string name)
		{
			var stripObject = new GameObject(name, typeof(RectTransform), typeof(Image));
			stripObject.transform.SetParent(parent);
			var strip = stripObject.GetComponent<Image>();
			strip.raycastTarget = false;
			return strip;
		}

		Image CreateImageChild(Transform parent, string name)
		{
			var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
			imageObject.transform.SetParent(parent);
			return imageObject.GetComponent<Image>();
		}

		Sprite CreateSprite(string name)
		{
			var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
			texture.name = $"{name}Texture";
			var sprite = Sprite.Create(texture, new Rect(0f, 0f, 2f, 2f), new Vector2(0.5f, 0.5f), 100f);
			sprite.name = name;
			transientObjects.Add(sprite);
			transientObjects.Add(texture);
			return sprite;
		}

		static ExploreMapPresentation BuildPresentation(params ExploreMapNodeView[] nodes)
		{
			return new ExploreMapPresentation(
				0,
				1,
				"테스트 지도",
				"1 / 9",
				nodes,
				new ExploreMapConnection[0]);
		}

		static ExploreMapNodeView BuildNode(
			string nodeId,
			bool isReachable,
			ExploreMapNodeKind kind = ExploreMapNodeKind.Combat,
			bool isCurrent = false,
			bool isCompleted = false,
			int row = 1,
			int lane = 0)
		{
			return new ExploreMapNodeView(
				nodeId,
				row,
				lane,
				kind,
				"전투",
				"전투",
				"전투",
				isCurrent,
				isReachable,
				isReachable,
				isCompleted,
				new string[0]);
		}

		static bool InvokeTrySelectMapNode(GameExploreController controller, int nodeIndex)
		{
			var method = typeof(GameExploreController).GetMethod(
				"TrySelectMapNodeForCurrentPresentation",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(method);
			object[] args = { nodeIndex, default(ExploreMapNodeView) };
			return (bool)method.Invoke(controller, args);
		}

		static void InvokeShowEncounterMode(
			GameExploreController controller,
			ExploreMapNodeKind nodeKind,
			string nodeId)
		{
			var method = typeof(GameExploreController).GetMethod(
				"ShowEncounterMode",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(method);
			method.Invoke(controller, new object[] { nodeKind, nodeId });
		}

		static void InvokePrivate(GameExploreController controller, string methodName)
		{
			var method = typeof(GameExploreController).GetMethod(
				methodName,
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(method);
			method.Invoke(controller, null);
		}

		static void InvokeApplyMapNodes(GameExploreController controller, ExploreMapPresentation presentation)
		{
			var method = typeof(GameExploreController).GetMethod(
				"ApplyMapNodes",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(method);
			method.Invoke(controller, new object[] { presentation });
		}

		static void InvokeApplyMapPresentation(GameExploreController controller, ExploreMapPresentation presentation)
		{
			var method = typeof(GameExploreController).GetMethod(
				"ApplyMapPresentation",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(method);
			method.Invoke(controller, new object[] { presentation });
		}

		static Sprite InvokeResolveMapNodeIconSprite(GameExploreController controller, ExploreMapNodeKind kind)
		{
			var method = typeof(GameExploreController).GetMethod(
				"ResolveMapNodeIconSprite",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(method);
			return (Sprite)method.Invoke(controller, new object[] { kind });
		}

		static void InvokeHoverEffectOnDisable(ExploreMapNodeHoverEffect hoverEffect)
		{
			var method = typeof(ExploreMapNodeHoverEffect).GetMethod(
				"OnDisable",
				BindingFlags.Instance | BindingFlags.NonPublic);
			Assert.NotNull(method);
			method.Invoke(hoverEffect, null);
		}

		static void SetPrivateField(object target, string fieldName, object value)
		{
			target.GetType()
				.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
				.SetValue(target, value);
		}

		static T GetPrivateField<T>(object target, string fieldName)
		{
			return (T)target.GetType()
				.GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic)
				.GetValue(target);
		}

		static void AssertBorderStripsRaycastDisabled(NodeSlot slot)
		{
			for (int i = 0; i < slot.HoverBorderStrips.Length; i++)
				Assert.IsFalse(slot.HoverBorderStrips[i].raycastTarget, slot.HoverBorderStrips[i].name);
		}

		static int CountTransformsNamed(Transform root, string name)
		{
			if (root == null)
				return 0;

			int count = root.name == name ? 1 : 0;
			for (int i = 0; i < root.childCount; i++)
				count += CountTransformsNamed(root.GetChild(i), name);
			return count;
		}

		readonly struct PlayerMarker
		{
			public readonly RectTransform Root;
			public readonly Image Image;

			public PlayerMarker(RectTransform root, Image image)
			{
				Root = root;
				Image = image;
			}
		}

		readonly struct NodeSlot
		{
			public readonly RectTransform Root;
			public readonly Image RootGraphic;
			public readonly Image ChildGraphic;
			public readonly Image IconImage;
			public readonly Outline IconOutline;
			public readonly Image HoverOutline;
			public readonly GameObject HoverBorderRoot;
			public readonly Image[] HoverBorderStrips;
			public readonly ExploreMapNodeHoverEffect HoverEffect;
			public readonly Image ButtonTargetGraphic;
			public readonly Button Button;

			public NodeSlot(
				RectTransform root,
				Image rootGraphic,
				Image childGraphic,
				Image iconImage,
				Outline iconOutline,
				Image hoverOutline,
				GameObject hoverBorderRoot,
				Image[] hoverBorderStrips,
				ExploreMapNodeHoverEffect hoverEffect,
				Image buttonTargetGraphic,
				Button button)
			{
				Root = root;
				RootGraphic = rootGraphic;
				ChildGraphic = childGraphic;
				IconImage = iconImage;
				IconOutline = iconOutline;
				HoverOutline = hoverOutline;
				HoverBorderRoot = hoverBorderRoot;
				HoverBorderStrips = hoverBorderStrips;
				HoverEffect = hoverEffect;
				ButtonTargetGraphic = buttonTargetGraphic;
				Button = button;
			}
		}
	}
}
