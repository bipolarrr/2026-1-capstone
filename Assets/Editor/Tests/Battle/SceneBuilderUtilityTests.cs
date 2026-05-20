using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BattleTests
{
	public class SceneBuilderUtilityTests
	{
		class DummyTarget
		{
			int value;
			public int Value => value;
		}

		[Test]
		public void SetField_SetsPrivateFieldWithoutFailure()
		{
			SceneBuilderUtility.BeginSceneBuildValidation("test");
			var target = new DummyTarget();

			SceneBuilderUtility.SetField(target, "value", 7);

			Assert.AreEqual(7, target.Value);
			Assert.AreEqual(0, SceneBuilderUtility.FieldBindingFailureCount);
		}

		[Test]
		public void SetField_RecordsMissingField()
		{
			SceneBuilderUtility.BeginSceneBuildValidation("test");

			SceneBuilderUtility.SetField(new DummyTarget(), "missing", 7);

			Assert.AreEqual(1, SceneBuilderUtility.FieldBindingFailureCount);
		}

		[Test]
		public void BuildSceneShell_CreatesCameraCanvasAndEventSystem()
		{
			var shell = SceneBuilderUtility.BuildSceneShell("TestCamera", Color.black,
				includeAudioListener: true);

			try
			{
				Assert.NotNull(shell.camera);
				Assert.NotNull(shell.canvas);
				Assert.NotNull(shell.canvasRoot);
				Assert.NotNull(shell.eventSystem);
				Assert.NotNull(shell.camera.GetComponent<AudioListener>());
				Assert.NotNull(shell.canvas.GetComponent<CanvasScaler>());
				Assert.NotNull(shell.canvas.GetComponent<GraphicRaycaster>());
				Assert.NotNull(shell.eventSystem.GetComponent<EventSystem>());
			}
			finally
			{
				if (shell.camera != null)
					Object.DestroyImmediate(shell.camera.gameObject);
				if (shell.canvas != null)
					Object.DestroyImmediate(shell.canvas.gameObject);
				if (shell.eventSystem != null)
					Object.DestroyImmediate(shell.eventSystem);
			}
		}

		[Test]
		public void BuildExploreEnemySlots_ReturnsFourBindableSlots()
		{
			var rootGo = new GameObject("ExploreEnemySlotsTest", typeof(RectTransform));

			try
			{
				var handles = SceneBuilderUtility.BuildExploreEnemySlots(
					rootGo.transform, 0.12f, Color.black, Color.red, null);

				Assert.AreEqual(4, handles.panels.Length);
				Assert.AreEqual(4, handles.bodies.Length);
				Assert.AreEqual(4, handles.idleProjectiles.Length);
				Assert.AreEqual(4, handles.names.Length);
				Assert.AreEqual(4, handles.hpFills.Length);
				Assert.AreEqual(4, handles.hpTexts.Length);
				Assert.NotNull(handles.root);
				Assert.NotNull(handles.panels[0]);
				Assert.NotNull(handles.bodies[0]);
				Assert.NotNull(handles.idleProjectiles[0]);
				Assert.NotNull(handles.names[0]);
				Assert.NotNull(handles.hpFills[0]);
				Assert.NotNull(handles.hpTexts[0]);
			}
			finally
			{
				Object.DestroyImmediate(rootGo);
			}
		}

		[Test]
		public void BuildBattleRootBase_BindsControllerWithoutFieldFailures()
		{
			SceneBuilderUtility.BeginSceneBuildValidation("test");
			var canvasGo = new GameObject("Canvas", typeof(RectTransform));
			var backgroundGo = new GameObject("Background", typeof(RectTransform), typeof(Image));
			var playerGo = new GameObject("Player", typeof(RectTransform), typeof(Image), typeof(PlayerBodyAnimator));
			var heartGo = new GameObject("Heart", typeof(RectTransform), typeof(HeartDisplay));
			var logGo = new GameObject("Log", typeof(RectTransform), typeof(BattleLog));
			var focusGo = new GameObject("Focus", typeof(RectTransform), typeof(BattleBottomFocusController));
			var damageGo = new GameObject("DamageSpawn", typeof(RectTransform));

			backgroundGo.transform.SetParent(canvasGo.transform, false);
			playerGo.transform.SetParent(canvasGo.transform, false);
			heartGo.transform.SetParent(canvasGo.transform, false);
			logGo.transform.SetParent(canvasGo.transform, false);
			focusGo.transform.SetParent(canvasGo.transform, false);
			damageGo.transform.SetParent(canvasGo.transform, false);

			GameObject[] enemyPanels = null;
			try
			{
				enemyPanels = new GameObject[4];
				var enemyBodies = new Image[4];
				var enemyNames = new TMPro.TMP_Text[4];
				var enemyHpFills = new Image[4];
				var enemyHpTexts = new TMPro.TMP_Text[4];
				var targetMarkers = new Image[4];
				var deadOverlays = new TMPro.TMP_Text[4];
				var enemyAnimators = new EnemySpriteAnimator[4];
				var idleProjectiles = new Image[4];
				var buttons = new Button[4];
				for (int i = 0; i < 4; i++)
				{
					var enemyGo = new GameObject($"Enemy{i}", typeof(RectTransform), typeof(Image), typeof(Button));
					enemyGo.transform.SetParent(canvasGo.transform, false);
					enemyPanels[i] = enemyGo;
					enemyBodies[i] = enemyGo.GetComponent<Image>();
					enemyHpFills[i] = enemyGo.GetComponent<Image>();
					targetMarkers[i] = enemyGo.GetComponent<Image>();
					idleProjectiles[i] = enemyGo.GetComponent<Image>();
					buttons[i] = enemyGo.GetComponent<Button>();
				}

				var handles = SceneBuilderUtility.BuildBattleRootBase<BattleSceneController>(
					new SceneBuilderUtility.StageBackgroundHandles
					{
						image = backgroundGo.GetComponent<Image>(),
					},
					new StageSpriteBundle[0],
					new SceneBuilderUtility.BattlePlayerRigHandles
					{
						bodyImage = playerGo.GetComponent<Image>(),
						bodyAnimator = playerGo.GetComponent<PlayerBodyAnimator>(),
					},
					new SceneBuilderUtility.EnemySlotStripHandles
					{
						panels = enemyPanels,
						bodies = enemyBodies,
						names = enemyNames,
						hpFills = enemyHpFills,
						hpTexts = enemyHpTexts,
						targetMarkers = targetMarkers,
						deadOverlays = deadOverlays,
						animators = enemyAnimators,
						idleProjectiles = idleProjectiles,
						buttons = buttons,
					},
					0.44f,
					heartGo.GetComponent<HeartDisplay>(),
					logGo.GetComponent<BattleLog>(),
					focusGo.GetComponent<BattleBottomFocusController>(),
					damageGo.GetComponent<RectTransform>());

				Assert.NotNull(handles.root);
				Assert.NotNull(handles.controller);
				Assert.NotNull(handles.vfx);
				Assert.NotNull(handles.animations);
				Assert.AreEqual(0, SceneBuilderUtility.FieldBindingFailureCount);
				Object.DestroyImmediate(handles.root);
			}
			finally
			{
				Object.DestroyImmediate(canvasGo);
			}
		}
	}
}
