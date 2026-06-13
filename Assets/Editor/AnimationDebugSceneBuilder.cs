using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class AnimationDebugSceneBuilder
{
	public const string ScenePath = "Assets/Scenes/AnimationDebugScene.unity";

	static readonly Color BgColor = new Color(0.055f, 0.060f, 0.070f);
	static readonly Color PanelBg = new Color(0.070f, 0.080f, 0.095f, 0.94f);
	static readonly Color ButtonBg = new Color(0.14f, 0.17f, 0.23f, 0.96f);
	static readonly Color ButtonHi = new Color(0.24f, 0.30f, 0.40f, 1f);
	static readonly Color ButtonPressed = new Color(0.08f, 0.10f, 0.14f, 1f);
	static readonly Color EnemyHpFill = new Color(0.85f, 0.25f, 0.25f);
	static readonly Color TargetMarkerColor = new Color(1f, 0.85f, 0.2f, 0.9f);
	static readonly Color Accent = new Color(1f, 0.82f, 0.30f, 1f);
	const float GroundY = 0.44f;

	[MenuItem("Tools/Build Animation Debug Scene")]
	public static void BuildScene()
	{
		BuildSceneInternal(showCompletionDialog: true);
	}

	public static bool BuildForIncremental()
	{
		return BuildSceneInternal(showCompletionDialog: false);
	}

	static bool BuildSceneInternal(bool showCompletionDialog)
	{
		SceneBuilderUtility.BeginSceneBuildValidation(nameof(AnimationDebugSceneBuilder));
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var shell = SceneBuilderUtility.BuildSceneShell("Main Camera", BgColor);
		var canvasRoot = shell.canvasRoot;
		var stageBundles = SceneBuilderUtility.BuildAllStageBundles();
		var background = SceneBuilderUtility.BuildStageBackground(
			canvasRoot, "FightBackgroundMask", "FightBackground", BgColor);
		var playerRig = SceneBuilderUtility.BuildBattlePlayerRig(
			canvasRoot, GroundY, includeJumpBelow: false);
		var enemySlots = SceneBuilderUtility.BuildBattleEnemySlots(
			canvasRoot, GroundY, EnemyHpFill, TargetMarkerColor);
		var damageSpawn = SceneBuilderUtility.BuildBattleDamageSpawnArea(canvasRoot, GroundY);
		var heartDisplay = SceneBuilderUtility.BuildHeartDisplay(
			canvasRoot, "PlayerHeartDisplay",
			new Vector2(0.02f, 0.88f), new Vector2(0.56f, 0.995f));

		var lowerArea = BuildLowerArea(canvasRoot);
		var labels = BuildStatusLabels(lowerArea);
		var buttons = BuildButtons(lowerArea);

		var battleRoot = SceneBuilderUtility.BuildBattleRootBase<AnimationDebugSceneController>(
			background, stageBundles, playerRig, enemySlots, GroundY,
			heartDisplay, null, null, damageSpawn);
		battleRoot.root.name = "AnimationDebugRoot";
		var ctrl = battleRoot.controller;

		var weaponProjectile = SceneBuilderUtility.CreatePlayerWeaponProjectileImage(
			canvasRoot, "AnimationDebugPlayerWeaponProjectile");
		var attackAnimator = SceneBuilderUtility.AddPlayerAttackAnimator(
			battleRoot.root, playerRig.bodyImage, playerRig.bodyAnimator, weaponProjectile);
		var enemyProjectile = SceneBuilderUtility.CreateEnemyProjectileImage(
			canvasRoot,
			"AnimationDebugEnemyProjectile",
			"Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png");
		var attackProjectileVfx = battleRoot.root.AddComponent<EnemyAttackProjectileVfx>();
		SceneBuilderUtility.SetField(attackProjectileVfx, "vfxParent", canvasRoot);

		SceneBuilderUtility.SetField(ctrl, "attackAnimator", attackAnimator);
		SceneBuilderUtility.SetField(ctrl, "enemyProjectile", enemyProjectile);
		SceneBuilderUtility.SetField(ctrl, "attackProjectileVfx", attackProjectileVfx);
		SceneBuilderUtility.SetField(ctrl, "selectedLabel", labels.selected);
		SceneBuilderUtility.SetField(ctrl, "detailsLabel", labels.details);
		SceneBuilderUtility.SetField(ctrl, "statusLabel", labels.status);
		SceneBuilderUtility.SetField(ctrl, "previousButton", buttons.previous);
		SceneBuilderUtility.SetField(ctrl, "nextButton", buttons.next);

		BindButtons(ctrl, buttons);

		return SceneBuilderUtility.SaveSceneAssetOnlyAndShowDialog(scene,
			ScenePath,
			"AnimationDebugScene generated. It was not added to Build Settings.",
			showCompletionDialog);
	}

	static RectTransform BuildLowerArea(RectTransform canvasRoot)
	{
		var lower = SceneBuilderUtility.CreateImage(canvasRoot, "AnimationDebugPanel", PanelBg, true);
		SceneBuilderUtility.AnchorBox(lower, 0f, 0f, 1f, 0.405f);
		return lower;
	}

	static DebugLabels BuildStatusLabels(RectTransform lowerArea)
	{
		var selected = SceneBuilderUtility.CreateTMPText(
			lowerArea, "SelectedLabel", "", 24f, Accent,
			TextAlignmentOptions.Left, FontStyles.Bold);
		selected.textWrappingMode = TextWrappingModes.NoWrap;
		SceneBuilderUtility.AnchorBox(selected.GetComponent<RectTransform>(),
			0.02f, 0.86f, 0.98f, 0.98f);

		var details = SceneBuilderUtility.CreateTMPText(
			lowerArea, "DetailsLabel", "", 15f, new Color(0.82f, 0.86f, 0.92f),
			TextAlignmentOptions.TopLeft);
		details.textWrappingMode = TextWrappingModes.Normal;
		SceneBuilderUtility.AnchorBox(details.GetComponent<RectTransform>(),
			0.02f, 0.52f, 0.98f, 0.84f);

		var status = SceneBuilderUtility.CreateTMPText(
			lowerArea, "StatusLabel", "", 18f, new Color(0.78f, 0.95f, 1f),
			TextAlignmentOptions.Left);
		status.textWrappingMode = TextWrappingModes.NoWrap;
		SceneBuilderUtility.AnchorBox(status.GetComponent<RectTransform>(),
			0.02f, 0.02f, 0.98f, 0.12f);

		return new DebugLabels
		{
			selected = selected,
			details = details,
			status = status,
		};
	}

	static DebugButtons BuildButtons(RectTransform lowerArea)
	{
		var buttons = new DebugButtons
		{
			previous = CreateButton(lowerArea, "PreviousButton", "Prev", 0.02f, 0.41f, 0.09f, 0.50f),
			next = CreateButton(lowerArea, "NextButton", "Next", 0.10f, 0.41f, 0.17f, 0.50f),
			reset = CreateButton(lowerArea, "ResetButton", "Reset", 0.18f, 0.41f, 0.25f, 0.50f),
			playAll = CreateButton(lowerArea, "PlayAllButton", "Play All", 0.87f, 0.41f, 0.98f, 0.50f),
		};

		string[] playerLabels =
		{
			"P Idle",
			"P Low",
			"P Jump",
			"P Def",
			"P S Hit",
			"P B Hit",
			"P Debuff",
			"P Death",
			"P Attack",
		};
		buttons.player = CreateButtonRow(lowerArea, "Player", playerLabels, 0.27f, 0.39f);

		string[] enemyLabels =
		{
			"E Idle",
			"E Attack",
			"E Hit",
			"E Death",
		};
		buttons.enemy = CreateButtonRow(lowerArea, "Enemy", enemyLabels, 0.14f, 0.26f);
		return buttons;
	}

	static Button[] CreateButtonRow(RectTransform parent, string prefix, string[] labels,
		float yMin, float yMax)
	{
		var buttons = new Button[labels.Length];
		float left = 0.02f;
		float right = 0.98f;
		float gap = 0.006f;
		float width = (right - left - gap * (labels.Length - 1)) / labels.Length;
		for (int i = 0; i < labels.Length; i++)
		{
			float xMin = left + i * (width + gap);
			float xMax = xMin + width;
			buttons[i] = CreateButton(parent, $"{prefix}{i}Button", labels[i], xMin, yMin, xMax, yMax);
		}
		return buttons;
	}

	static Button CreateButton(RectTransform parent, string name, string label,
		float xMin, float yMin, float xMax, float yMax)
	{
		var go = SceneBuilderUtility.CreateButton(
			parent,
			name,
			label,
			16f,
			ButtonBg,
			ButtonHi,
			ButtonPressed);
		SceneBuilderUtility.AnchorBox(go.GetComponent<RectTransform>(), xMin, yMin, xMax, yMax);
		return go.GetComponent<Button>();
	}

	static void BindButtons(AnimationDebugSceneController ctrl, DebugButtons buttons)
	{
		UnityEventTools.AddPersistentListener(buttons.previous.onClick, ctrl.SelectPrevious);
		UnityEventTools.AddPersistentListener(buttons.next.onClick, ctrl.SelectNext);
		UnityEventTools.AddPersistentListener(buttons.reset.onClick, ctrl.ResetSelected);
		UnityEventTools.AddPersistentListener(buttons.playAll.onClick, ctrl.PlayAllSelectedMotions);

		UnityEventTools.AddPersistentListener(buttons.player[0].onClick, ctrl.PlayPlayerIdle);
		UnityEventTools.AddPersistentListener(buttons.player[1].onClick, ctrl.PlayPlayerLowHp);
		UnityEventTools.AddPersistentListener(buttons.player[2].onClick, ctrl.PlayPlayerJump);
		UnityEventTools.AddPersistentListener(buttons.player[3].onClick, ctrl.PlayPlayerDefense);
		UnityEventTools.AddPersistentListener(buttons.player[4].onClick, ctrl.PlayPlayerSmallHit);
		UnityEventTools.AddPersistentListener(buttons.player[5].onClick, ctrl.PlayPlayerStrongHit);
		UnityEventTools.AddPersistentListener(buttons.player[6].onClick, ctrl.PlayPlayerDebuff);
		UnityEventTools.AddPersistentListener(buttons.player[7].onClick, ctrl.PlayPlayerDeath);
		UnityEventTools.AddPersistentListener(buttons.player[8].onClick, ctrl.PlayPlayerFullWeaponAttack);

		UnityEventTools.AddPersistentListener(buttons.enemy[0].onClick, ctrl.PlayEnemyIdle);
		UnityEventTools.AddPersistentListener(buttons.enemy[1].onClick, ctrl.PlayEnemyAttack);
		UnityEventTools.AddPersistentListener(buttons.enemy[2].onClick, ctrl.PlayEnemyHit);
		UnityEventTools.AddPersistentListener(buttons.enemy[3].onClick, ctrl.PlayEnemyDeath);
	}

	struct DebugLabels
	{
		public TMP_Text selected;
		public TMP_Text details;
		public TMP_Text status;
	}

	struct DebugButtons
	{
		public Button previous;
		public Button next;
		public Button reset;
		public Button playAll;
		public Button[] player;
		public Button[] enemy;
	}
}
