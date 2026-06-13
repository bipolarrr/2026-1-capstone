using System.IO;
using Holdem;
using Holdem.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

public static class HoldemBattleSceneBuilder
{
	static readonly Color BgColor = new Color(0.05f, 0.08f, 0.10f);
	static readonly Color PanelBg = new Color(0.08f, 0.12f, 0.15f, 0.94f);
	static readonly Color EnemyHpFill = new Color(0.85f, 0.25f, 0.25f);
	static readonly Color TargetMarkerColor = new Color(1f, 0.85f, 0.2f, 0.9f);
	static readonly Color Felt = new Color(0.05f, 0.22f, 0.14f, 0.95f);
	static readonly Color CardFace = new Color(0.92f, 0.86f, 0.68f, 1f);
	static readonly Color CardBack = new Color(0.30f, 0.16f, 0.07f, 1f);
	static readonly Color AccentGold = new Color(1f, 0.83f, 0.36f, 1f);
	static readonly Color ButtonGreen = new Color(0.12f, 0.34f, 0.24f, 0.95f);
	static readonly Color ButtonBlue = new Color(0.13f, 0.20f, 0.36f, 0.95f);
	static readonly Color ButtonRed = new Color(0.48f, 0.14f, 0.14f, 0.95f);
	const float GroundY = 0.44f;

	const string CardSpriteRoot = "Assets/Holdem/Sprites/Cards";
	const string CardBackPath = CardSpriteRoot + "/card_back_acorn.png";
	const string CardFacePath = CardSpriteRoot + "/card_front_template.png";
	const string CardFrontFolder = CardSpriteRoot + "/Fronts";
	const string CommunityMatPath = "Assets/Holdem/Sprites/holdem_community_mat.png";
	const string DefenseBackPath = CardBackPath;
	static readonly string[] RankCodes = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
	static readonly string[] SuitCodes = { "C", "D", "H", "S" };
	static readonly string[] PipPaths =
	{
		CardSpriteRoot + "/pip_club.png",
		CardSpriteRoot + "/pip_diamond.png",
		CardSpriteRoot + "/pip_heart.png",
		CardSpriteRoot + "/pip_spade.png",
	};

	[MenuItem("Tools/Build HoldemBattle Scene")]
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
		SceneBuilderUtility.BeginSceneBuildValidation(nameof(HoldemBattleSceneBuilder));
		var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

		var shell = SceneBuilderUtility.BuildSceneShell("Main Camera", BgColor);
		var canvasRoot = shell.canvasRoot;
		var stageBundles = SceneBuilderUtility.BuildAllStageBundles();
		var fightBackground = SceneBuilderUtility.BuildStageBackground(
			canvasRoot, "FightBackgroundMask", "FightBackground", BgColor);
		var playerRig = SceneBuilderUtility.BuildBattlePlayerRig(canvasRoot, GroundY, includeJumpBelow: false);
		var enemySlots = SceneBuilderUtility.BuildBattleEnemySlots(
			canvasRoot, GroundY, EnemyHpFill, TargetMarkerColor);
		var damageSpawn = SceneBuilderUtility.BuildBattleDamageSpawnArea(canvasRoot, GroundY);
		var heartDisplay = SceneBuilderUtility.BuildHeartDisplay(
			canvasRoot, "PlayerHeartDisplay",
			new Vector2(0.02f, 0.88f), new Vector2(0.56f, 0.995f));

		EnsureAllHoldemCardImports();
		var cardBackSprite = LoadSprite(CardBackPath);
		var cardFaceSprite = LoadSprite(CardFacePath);
		var cardFrontSprites = LoadCardFrontSprites();
		var communityMatSprite = LoadSprite(CommunityMatPath);
		var defenseBackSprite = LoadSprite(DefenseBackPath);

		var lowerArea = BuildLowerArea(canvasRoot);
		var cardUi = BuildHoldemCardUi(lowerArea, cardFaceSprite, cardBackSprite, communityMatSprite);
		var actionUi = BuildActionButtons(lowerArea);
		var defenseUi = BuildDefensePanel(lowerArea, cardFaceSprite, defenseBackSprite != null ? defenseBackSprite : cardBackSprite);
		var messageView = BuildBattleMessage(lowerArea);
		var enemyAttackCardViews = BuildEnemyAttackCards(enemySlots, cardFaceSprite);
		var bottomFocusHandles = SceneBuilderUtility.BuildBattleBottomFocus(canvasRoot, lowerArea, PanelBg);

		var battleRootHandles = SceneBuilderUtility.BuildBattleRootBase<HoldemBattleController>(
			fightBackground, stageBundles, playerRig, enemySlots, GroundY,
			heartDisplay, bottomFocusHandles.log, bottomFocusHandles.focus, damageSpawn);
		var root = battleRootHandles.root;
		var ctrl = battleRootHandles.controller;

		var weaponProjectile = SceneBuilderUtility.CreatePlayerWeaponProjectileImage(canvasRoot);
		var attackAnimator = SceneBuilderUtility.AddPlayerAttackAnimator(
			root, playerRig.bodyImage, playerRig.bodyAnimator, weaponProjectile);

		var enemyProjectile = SceneBuilderUtility.CreateEnemyProjectileImage(
			canvasRoot,
			"EnemyProjectile",
			"Assets/Mobs/Sprites/Skeleton/Projectile/Skeleton_Arrow_transparent.png");

		var deathAnimator = root.AddComponent<PlayerDeathAnimator>();
		SceneBuilderUtility.SetField(deathAnimator, "playerBody", playerRig.bodyImage);
		SceneBuilderUtility.SetField(deathAnimator, "bodyAnimator", playerRig.bodyAnimator);
		SceneBuilderUtility.SetField(deathAnimator, "frameRate", SceneBuilderUtility.BattlePlayerActionFrameRate);
		SceneBuilderUtility.SetField(deathAnimator, "deathSprites", SceneBuilderUtility.LoadNumberedPixelSprites(
			SceneBuilderUtility.PlayerDieSpriteFolder, "", "png", SceneBuilderUtility.BattlePlayerDeathFrameCount));
		var deathDimmer = SceneBuilderUtility.CreateDimmer(canvasRoot, "DeathDimmer");
		deathDimmer.transform.SetAsLastSibling();
		SceneBuilderUtility.SetField(deathAnimator, "screenDimmer", deathDimmer.GetComponent<Image>());

		SceneBuilderUtility.SetField(ctrl, "holeCardImages", cardUi.holeImages);
		SceneBuilderUtility.SetField(ctrl, "holeCardLabels", cardUi.holeLabels);
		SceneBuilderUtility.SetField(ctrl, "holeRedrawCountLabels", cardUi.holeRedrawLabels);
		SceneBuilderUtility.SetField(ctrl, "holeCardViews", cardUi.holeViews);
		SceneBuilderUtility.SetField(ctrl, "communityCardImages", cardUi.communityImages);
		SceneBuilderUtility.SetField(ctrl, "communityCardLabels", cardUi.communityLabels);
		SceneBuilderUtility.SetField(ctrl, "communityCardViews", cardUi.communityViews);
		SceneBuilderUtility.SetField(ctrl, "cardFaceSprite", cardFaceSprite);
		SceneBuilderUtility.SetField(ctrl, "cardBackSprite", cardBackSprite);
		SceneBuilderUtility.SetField(ctrl, "cardFrontSprites", cardFrontSprites);
		SceneBuilderUtility.SetField(ctrl, "stageLabel", cardUi.stageLabel);
		SceneBuilderUtility.SetField(ctrl, "handResultLabel", cardUi.handLabel);
		SceneBuilderUtility.SetField(ctrl, "damagePreviewLabel", cardUi.previewLabel);
		SceneBuilderUtility.SetField(ctrl, "battleMessageView", messageView);
		SceneBuilderUtility.SetField(ctrl, "enemyAttackCardViews", enemyAttackCardViews);

		SceneBuilderUtility.SetField(ctrl, "attackButton", actionUi.attack);
		SceneBuilderUtility.SetField(ctrl, "redrawHole0Button", actionUi.redrawHole0);
		SceneBuilderUtility.SetField(ctrl, "redrawHole1Button", actionUi.redrawHole1);
		SceneBuilderUtility.SetField(ctrl, "redrawCommunityButton", actionUi.redrawCommunity);
		SceneBuilderUtility.SetField(ctrl, "cancelButton", actionUi.cancel);

		SceneBuilderUtility.SetField(ctrl, "defensePanel", defenseUi.panel);
		SceneBuilderUtility.SetField(ctrl, "defensePanelGroup", defenseUi.group);
		SceneBuilderUtility.SetField(ctrl, "defensePanelRect", defenseUi.rect);
		SceneBuilderUtility.SetField(ctrl, "defenseEnemyCardImage", defenseUi.enemyImage);
		SceneBuilderUtility.SetField(ctrl, "defenseEnemyCardLabel", defenseUi.enemyLabel);
		SceneBuilderUtility.SetField(ctrl, "defenseEnemyCardView", defenseUi.enemyView);
		SceneBuilderUtility.SetField(ctrl, "defenseCardImages", defenseUi.cardImages);
		SceneBuilderUtility.SetField(ctrl, "defenseCardLabels", defenseUi.cardLabels);
		SceneBuilderUtility.SetField(ctrl, "defenseCardViews", defenseUi.cardViews);
		SceneBuilderUtility.SetField(ctrl, "defenseButtons", defenseUi.buttons);
		SceneBuilderUtility.SetField(ctrl, "defenseResultLabel", defenseUi.resultLabel);
		SceneBuilderUtility.SetField(ctrl, "defenseBackSprite", defenseBackSprite);
		SceneBuilderUtility.SetField(ctrl, "deathAnimator", deathAnimator);
		SceneBuilderUtility.SetField(ctrl, "attackAnimator", attackAnimator);
		SceneBuilderUtility.SetField(ctrl, "playerBodyAnimator", playerRig.bodyAnimator);
		SceneBuilderUtility.SetField(ctrl, "enemyProjectile", enemyProjectile);

		UnityEventTools.AddPersistentListener(actionUi.attack.onClick, ctrl.ConfirmAttack);
		UnityEventTools.AddPersistentListener(actionUi.redrawHole0.onClick, ctrl.RedrawHoleCard0);
		UnityEventTools.AddPersistentListener(actionUi.redrawHole1.onClick, ctrl.RedrawHoleCard1);
		UnityEventTools.AddPersistentListener(actionUi.redrawCommunity.onClick, ctrl.RedrawCommunity);
		UnityEventTools.AddPersistentListener(actionUi.cancel.onClick, ctrl.CancelBattle);
		UnityEventTools.AddPersistentListener(defenseUi.buttons[0].onClick, ctrl.DefensePick0);
		UnityEventTools.AddPersistentListener(defenseUi.buttons[1].onClick, ctrl.DefensePick1);
		UnityEventTools.AddPersistentListener(defenseUi.buttons[2].onClick, ctrl.DefensePick2);
		UnityEventTools.AddPersistentListener(defenseUi.buttons[3].onClick, ctrl.DefensePick3);
		UnityEventTools.AddPersistentListener(defenseUi.buttons[4].onClick, ctrl.DefensePick4);
		SceneBuilderUtility.BindBattleEnemyPanelButtons(ctrl, enemySlots.buttons);

		SceneBuilderUtility.BuildDebugConsole();
		SceneBuilderUtility.BuildAudioManager(new[]
		{
			"Player_Attack", "Player_Attack_Small", "Player_Attack_Medium", "Player_Attack_Big",
			"Enemy123_Attack", "Enemy_Die", "Player_Death",
			"Player_PerfectDefense", "UI_Failure",
			"UI_Back_NO", "Transition_2"
		}, includeDrumRoll: false);

		return SceneBuilderUtility.SaveSceneAndShowDialog(scene,
			"Assets/Scenes/HoldemBattleScene.unity",
			"HoldemBattleScene 생성 완료!",
			showDialog: showCompletionDialog);
	}

	static RectTransform BuildLowerArea(RectTransform canvasRoot)
	{
		var uiBg = SceneBuilderUtility.CreateImage(canvasRoot, "HoldemPlayAreaBackdrop", new Color(0.015f, 0.025f, 0.025f, 0.96f));
		SceneBuilderUtility.AnchorBox(uiBg, 0f, 0f, 1f, 0.435f);
		AddPanelDepth(uiBg, new Color(0.68f, 0.50f, 0.22f, 0.26f), new Vector2(0f, 8f));

		var lowerArea = SceneBuilderUtility.CreateImage(canvasRoot, "HoldemPlayArea", new Color(0f, 0f, 0f, 0f));
		SceneBuilderUtility.AnchorBox(lowerArea, 0.025f, 0.02f, 0.975f, 0.425f);
		lowerArea.GetComponent<Image>().raycastTarget = false;
		return lowerArea;
	}

	static HoldemCardUi BuildHoldemCardUi(RectTransform lowerArea, Sprite cardFace,
		Sprite cardBack, Sprite communityMat)
	{
		var cardArea = SceneBuilderUtility.CreateImage(lowerArea, "HoldemCardArea", new Color(0f, 0f, 0f, 0f));
		SceneBuilderUtility.Stretch(cardArea);
		cardArea.GetComponent<Image>().raycastTarget = false;

		var handBack = SceneBuilderUtility.CreateImage(cardArea, "PlayerHandBackdrop", new Color(0.02f, 0.03f, 0.05f, 0.78f));
		SceneBuilderUtility.AnchorBox(handBack, 0f, 0f, 1f, 0.39f);
		AddPanelDepth(handBack, new Color(0.9f, 0.68f, 0.28f, 0.15f), new Vector2(0f, -6f));

		var mat = SceneBuilderUtility.CreateImage(cardArea, "CommunityBoardMat", Felt);
		SceneBuilderUtility.AnchorBox(mat, 0.16f, 0.43f, 0.78f, 0.82f);
		AddPanelDepth(mat, new Color(0.88f, 0.68f, 0.28f, 0.42f), new Vector2(0f, -9f));
		var matImage = mat.GetComponent<Image>();
		if (communityMat != null)
		{
			matImage.sprite = communityMat;
			matImage.color = Color.white;
			matImage.preserveAspect = false;
		}

		var communityImages = new Image[5];
		var communityLabels = new TMP_Text[5];
		var communityViews = new HoldemCardView[5];
		for (int i = 0; i < 5; i++)
		{
			float xMin = 0.0925f + i * 0.17f;
			float xMax = xMin + 0.135f;
			var slot = BuildCardSlot(mat, $"CommunityCard{i}", xMin, 0.12f, xMax, 0.88f,
				cardBack, CardBack, false);
			communityImages[i] = slot.image;
			communityLabels[i] = slot.label;
			communityViews[i] = slot.view;
		}

		var handFan = SceneBuilderUtility.CreateEmpty(cardArea, "PlayerHeldCardsFan");
		SceneBuilderUtility.AnchorBox(handFan, 0.24f, 0.02f, 0.60f, 0.43f);

		var hole0 = BuildCardSlot(handFan, "HoleCard0", 0.08f, 0.06f, 0.48f, 0.96f, cardFace, CardFace, false);
		var hole1 = BuildCardSlot(handFan, "HoleCard1", 0.42f, 0.06f, 0.82f, 0.96f, cardFace, CardFace, false);
		hole0.root.localRotation = Quaternion.Euler(0f, 0f, -7f);
		hole1.root.localRotation = Quaternion.Euler(0f, 0f, 7f);

		var info = SceneBuilderUtility.CreateImage(cardArea, "HoldemHandSummary", new Color(0.02f, 0.025f, 0.04f, 0.82f));
		SceneBuilderUtility.AnchorBox(info, 0.05f, 0.84f, 0.74f, 0.985f);
		AddPanelDepth(info, new Color(0.78f, 0.64f, 0.34f, 0.24f), new Vector2(0f, -5f));
		var stageLabel = SceneBuilderUtility.CreateTMPText(info, "StageLabel", "Stage 1", 18,
			new Color(0.84f, 0.90f, 1f), TextAlignmentOptions.Left, FontStyles.Bold);
		stageLabel.enableAutoSizing = true;
		stageLabel.fontSizeMin = 12;
		stageLabel.fontSizeMax = 18;
		SceneBuilderUtility.AnchorBox(stageLabel.GetComponent<RectTransform>(), 0.035f, 0.20f, 0.23f, 0.82f);
		var handLabel = SceneBuilderUtility.CreateTMPText(info, "HandLabel", "High Card", 25,
			AccentGold, TextAlignmentOptions.Left, FontStyles.Bold);
		handLabel.enableAutoSizing = true;
		handLabel.fontSizeMin = 16;
		handLabel.fontSizeMax = 28;
		SceneBuilderUtility.AnchorBox(handLabel.GetComponent<RectTransform>(), 0.25f, 0.16f, 0.58f, 0.86f);
		var previewLabel = SceneBuilderUtility.CreateTMPText(info, "DamagePreview", "", 17,
			new Color(0.86f, 0.95f, 0.88f), TextAlignmentOptions.Left, FontStyles.Bold);
		previewLabel.enableAutoSizing = true;
		previewLabel.fontSizeMin = 11;
		previewLabel.fontSizeMax = 18;
		previewLabel.textWrappingMode = TextWrappingModes.Normal;
		SceneBuilderUtility.AnchorBox(previewLabel.GetComponent<RectTransform>(), 0.59f, 0.12f, 0.97f, 0.88f);

		return new HoldemCardUi
		{
			holeImages = new[] { hole0.image, hole1.image },
			holeLabels = new[] { hole0.label, hole1.label },
			holeRedrawLabels = new[] { hole0.subLabel, hole1.subLabel },
			holeViews = new[] { hole0.view, hole1.view },
			communityImages = communityImages,
			communityLabels = communityLabels,
			communityViews = communityViews,
			stageLabel = stageLabel,
			handLabel = handLabel,
			previewLabel = previewLabel,
		};
	}

	static HoldemActionUi BuildActionButtons(RectTransform lowerArea)
	{
		var actionPanel = SceneBuilderUtility.CreateImage(lowerArea, "HoldemActionCluster", new Color(0.025f, 0.035f, 0.055f, 0.88f));
		SceneBuilderUtility.AnchorBox(actionPanel, 0.79f, 0.05f, 0.99f, 0.43f);
		AddPanelDepth(actionPanel, new Color(0.8f, 0.63f, 0.26f, 0.25f), new Vector2(0f, -6f));

		var attack = BuildButton(actionPanel, "AttackNowButton", "공격 확정", 0.08f, 0.31f, 0.92f, 0.93f, ButtonGreen, 23);
		var redrawCommunity = BuildButton(actionPanel, "RedrawCommunityButton", "공유패 교체", 0.08f, 0.06f, 0.54f, 0.23f, ButtonBlue, 14);
		var cancel = BuildButton(actionPanel, "CancelBattleButton", "퇴각", 0.58f, 0.06f, 0.92f, 0.23f, ButtonRed, 16);
		var redrawHole0 = BuildButton(lowerArea, "RedrawHole1Button", "교체", 0.285f, 0.028f, 0.385f, 0.115f, ButtonBlue, 15);
		var redrawHole1 = BuildButton(lowerArea, "RedrawHole2Button", "교체", 0.475f, 0.028f, 0.575f, 0.115f, ButtonBlue, 15);

		return new HoldemActionUi
		{
			attack = attack,
			redrawHole0 = redrawHole0,
			redrawHole1 = redrawHole1,
			redrawCommunity = redrawCommunity,
			cancel = cancel,
		};
	}

	static HoldemDefenseUi BuildDefensePanel(RectTransform lowerArea, Sprite cardFace, Sprite defenseBack)
	{
		var panel = SceneBuilderUtility.CreateImage(lowerArea, "HoldemDefensePanel", new Color(0.02f, 0.025f, 0.045f, 0.99f), true);
		SceneBuilderUtility.AnchorBox(panel, 0.06f, 0.03f, 0.94f, 0.82f);
		AddPanelDepth(panel, new Color(0.95f, 0.72f, 0.25f, 0.36f), new Vector2(0f, -8f));
		var group = panel.gameObject.AddComponent<CanvasGroup>();
		group.alpha = 0f;
		group.blocksRaycasts = false;
		panel.gameObject.SetActive(false);

		var title = SceneBuilderUtility.CreateTMPText(panel, "DefenseTitle", "방어 선택", 22,
			AccentGold, TextAlignmentOptions.Left, FontStyles.Bold);
		SceneBuilderUtility.AnchorBox(title.GetComponent<RectTransform>(), 0.035f, 0.78f, 0.22f, 0.94f);

		var enemySlot = BuildCardSlot(panel, "EnemyAttackReferenceCard", 0.045f, 0.20f, 0.165f, 0.76f, cardFace, CardFace, false);
		var enemyCaption = SceneBuilderUtility.CreateTMPText(panel, "EnemyCaption", "상대 카드", 14,
			Color.white, TextAlignmentOptions.Center);
		SceneBuilderUtility.AnchorBox(enemyCaption.GetComponent<RectTransform>(), 0.035f, 0.06f, 0.175f, 0.18f);

		var resultLabel = SceneBuilderUtility.CreateTMPText(panel, "DefenseResultLabel", "", 24,
			Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
		resultLabel.enableAutoSizing = true;
		resultLabel.fontSizeMin = 14;
		resultLabel.fontSizeMax = 24;
		SceneBuilderUtility.AnchorBox(resultLabel.GetComponent<RectTransform>(), 0.20f, 0.77f, 0.95f, 0.94f);

		var defenseImages = new Image[5];
		var defenseLabels = new TMP_Text[5];
		var defenseViews = new HoldemCardView[5];
		var buttons = new Button[5];
		for (int i = 0; i < 5; i++)
		{
			float xMin = 0.235f + i * 0.142f;
			float xMax = xMin + 0.112f;
			var slot = BuildCardSlot(panel, $"DefenseCard{i}", xMin, 0.15f, xMax, 0.74f,
				defenseBack, CardBack, true);
			defenseImages[i] = slot.image;
			defenseLabels[i] = slot.label;
			defenseViews[i] = slot.view;
			buttons[i] = slot.button;
		}

		return new HoldemDefenseUi
		{
			panel = panel.gameObject,
			group = group,
			rect = panel,
			enemyImage = enemySlot.image,
			enemyLabel = enemySlot.label,
			enemyView = enemySlot.view,
			cardImages = defenseImages,
			cardLabels = defenseLabels,
			cardViews = defenseViews,
			buttons = buttons,
			resultLabel = resultLabel,
		};
	}

	static HoldemBattleMessageView BuildBattleMessage(RectTransform lowerArea)
	{
		var banner = SceneBuilderUtility.CreateImage(lowerArea, "HoldemBattleMessageBanner", new Color(0.055f, 0.035f, 0.075f, 0.95f));
		SceneBuilderUtility.AnchorBox(banner, 0.18f, 0.84f, 0.86f, 0.99f);
		AddPanelDepth(banner, new Color(1f, 0.76f, 0.25f, 0.55f), new Vector2(0f, -7f));
		var group = banner.gameObject.AddComponent<CanvasGroup>();
		group.alpha = 0f;
		group.blocksRaycasts = false;

		var label = SceneBuilderUtility.CreateTMPText(banner, "MessageLabel", "", 27,
			new Color(1f, 0.88f, 0.42f), TextAlignmentOptions.Center, FontStyles.Bold);
		label.enableAutoSizing = true;
		label.fontSizeMin = 16;
		label.fontSizeMax = 29;
		label.textWrappingMode = TextWrappingModes.Normal;
		SceneBuilderUtility.AnchorBox(label.GetComponent<RectTransform>(), 0.04f, 0.12f, 0.96f, 0.88f);

		var view = banner.gameObject.AddComponent<HoldemBattleMessageView>();
		view.Bind(label, group, banner.GetComponent<Image>());
		banner.SetAsLastSibling();
		return view;
	}

	static HoldemCardView[] BuildEnemyAttackCards(SceneBuilderUtility.EnemySlotStripHandles enemySlots, Sprite cardFace)
	{
		var views = new HoldemCardView[4];
		if (enemySlots.slotRoots == null)
			return views;

		for (int i = 0; i < enemySlots.slotRoots.Length && i < views.Length; i++)
		{
			var slotRoot = enemySlots.slotRoots[i];
			if (slotRoot == null)
				continue;

			var card = BuildCardSlot(slotRoot, "HoldemEnemyAttackCard", 0.33f, 0.52f, 0.67f, 1.02f,
				cardFace, CardFace, false);
			card.root.SetAsLastSibling();
			card.root.gameObject.SetActive(false);
			views[i] = card.view;
		}
		return views;
	}

	static CardSlotRefs BuildCardSlot(Transform parent, string name,
		float xMin, float yMin, float xMax, float yMax, Sprite sprite, Color fallbackColor, bool button)
	{
		var rt = SceneBuilderUtility.CreateImage(parent, name, sprite != null ? Color.white : fallbackColor, button);
		SceneBuilderUtility.AnchorBox(rt, xMin, yMin, xMax, yMax);
		rt.pivot = new Vector2(0.5f, 0.5f);
		var image = rt.GetComponent<Image>();
		image.sprite = sprite;
		image.preserveAspect = sprite != null;
		image.raycastTarget = button;
		var shadow = rt.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.45f);
		shadow.effectDistance = new Vector2(5f, -7f);
		var outline = rt.gameObject.AddComponent<Outline>();
		outline.effectColor = new Color(0.12f, 0.08f, 0.02f, 0.55f);
		outline.effectDistance = new Vector2(2f, -2f);

		var topSheen = SceneBuilderUtility.CreateImage(rt, "TopSheen", new Color(1f, 1f, 1f, 0.11f));
		SceneBuilderUtility.AnchorBox(topSheen, 0.08f, 0.78f, 0.92f, 0.89f);
		topSheen.GetComponent<Image>().raycastTarget = false;

		var label = SceneBuilderUtility.CreateTMPText(rt, "Label", "✦", 30,
			Color.black, TextAlignmentOptions.Center, FontStyles.Bold);
		label.enableAutoSizing = true;
		label.fontSizeMin = 16;
		label.fontSizeMax = 34;
		SceneBuilderUtility.AnchorBox(label.GetComponent<RectTransform>(), 0.06f, 0.18f, 0.94f, 0.86f);

		var subLabel = SceneBuilderUtility.CreateTMPText(rt, "SubLabel", "", 13,
			new Color(0.18f, 0.14f, 0.08f), TextAlignmentOptions.Center);
		subLabel.enableAutoSizing = true;
		subLabel.fontSizeMin = 9;
		subLabel.fontSizeMax = 13;
		SceneBuilderUtility.AnchorBox(subLabel.GetComponent<RectTransform>(), 0.03f, 0.02f, 0.97f, 0.19f);

		Button buttonComp = null;
		if (button)
		{
			buttonComp = rt.gameObject.AddComponent<Button>();
			buttonComp.targetGraphic = image;
			SceneBuilderUtility.SetButtonColorSet(buttonComp,
				sprite != null ? Color.white : fallbackColor,
				new Color(0.92f, 0.78f, 0.36f, 1f),
				new Color(0.75f, 0.62f, 0.25f, 1f));
			var colors = buttonComp.colors;
			colors.fadeDuration = 0.06f;
			buttonComp.colors = colors;
		}

		var view = rt.gameObject.AddComponent<HoldemCardView>();
		view.Bind(image, label, subLabel);

		return new CardSlotRefs
		{
			root = rt,
			image = image,
			label = label,
			subLabel = subLabel,
			button = buttonComp,
			view = view,
		};
	}

	static Button BuildButton(Transform parent, string name, string label,
		float xMin, float yMin, float xMax, float yMax, Color color, float labelSize = 22f)
	{
		var go = SceneBuilderUtility.CreateButton(parent, name, label, labelSize,
			color,
			new Color(Mathf.Min(color.r + 0.15f, 1f), Mathf.Min(color.g + 0.15f, 1f), Mathf.Min(color.b + 0.15f, 1f), 1f),
			new Color(color.r * 0.65f, color.g * 0.65f, color.b * 0.65f, 1f));
		SceneBuilderUtility.AnchorBox(go.GetComponent<RectTransform>(), xMin, yMin, xMax, yMax);
		AddPanelDepth(go.GetComponent<RectTransform>(), new Color(1f, 0.82f, 0.32f, 0.22f), new Vector2(0f, -4f));
		return go.GetComponent<Button>();
	}

	static void AddPanelDepth(RectTransform target, Color outlineColor, Vector2 shadowDistance)
	{
		if (target == null)
			return;
		var shadow = target.gameObject.AddComponent<Shadow>();
		shadow.effectColor = new Color(0f, 0f, 0f, 0.35f);
		shadow.effectDistance = shadowDistance;
		var outline = target.gameObject.AddComponent<Outline>();
		outline.effectColor = outlineColor;
		outline.effectDistance = new Vector2(2f, -2f);
	}

	static Sprite LoadSprite(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
			return null;

		EnsureHoldemPixelSprite(path);
		return AssetDatabase.LoadAssetAtPath<Sprite>(path);
	}

	static Sprite[] LoadCardFrontSprites()
	{
		var sprites = new Sprite[RankCodes.Length * SuitCodes.Length];
		int index = 0;
		for (int suit = 0; suit < SuitCodes.Length; suit++)
		{
			for (int rank = 0; rank < RankCodes.Length; rank++)
			{
				sprites[index++] = LoadSprite($"{CardFrontFolder}/{RankCodes[rank]}{SuitCodes[suit]}.png");
			}
		}
		return sprites;
	}

	static void EnsureAllHoldemCardImports()
	{
		EnsureHoldemPixelSprite(CardBackPath);
		EnsureHoldemPixelSprite(CardFacePath);
		for (int i = 0; i < PipPaths.Length; i++)
			EnsureHoldemPixelSprite(PipPaths[i]);
		for (int suit = 0; suit < SuitCodes.Length; suit++)
		{
			for (int rank = 0; rank < RankCodes.Length; rank++)
				EnsureHoldemPixelSprite($"{CardFrontFolder}/{RankCodes[rank]}{SuitCodes[suit]}.png");
		}
	}

	static void EnsureHoldemPixelSprite(string path)
	{
		if (string.IsNullOrEmpty(path) || !File.Exists(path))
			return;

		AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
		var importer = AssetImporter.GetAtPath(path) as TextureImporter;
		if (importer == null)
			return;

		bool reimport = false;
		if (importer.textureType != TextureImporterType.Sprite)
		{
			importer.textureType = TextureImporterType.Sprite;
			reimport = true;
		}
		if (importer.spriteImportMode != SpriteImportMode.Single)
		{
			importer.spriteImportMode = SpriteImportMode.Single;
			reimport = true;
		}
		if (importer.filterMode != FilterMode.Point)
		{
			importer.filterMode = FilterMode.Point;
			reimport = true;
		}
		if (importer.mipmapEnabled)
		{
			importer.mipmapEnabled = false;
			reimport = true;
		}
		if (importer.textureCompression != TextureImporterCompression.Uncompressed)
		{
			importer.textureCompression = TextureImporterCompression.Uncompressed;
			reimport = true;
		}
		if (!importer.alphaIsTransparency)
		{
			importer.alphaIsTransparency = true;
			reimport = true;
		}
		if (importer.npotScale != TextureImporterNPOTScale.None)
		{
			importer.npotScale = TextureImporterNPOTScale.None;
			reimport = true;
		}

		var settings = new TextureImporterSettings();
		importer.ReadTextureSettings(settings);
		if (settings.spriteMeshType != SpriteMeshType.FullRect)
		{
			settings.spriteMeshType = SpriteMeshType.FullRect;
			importer.SetTextureSettings(settings);
			reimport = true;
		}
		if (EnsurePlatformUncompressed(importer.GetDefaultPlatformTextureSettings(), importer))
			reimport = true;
		if (EnsurePlatformUncompressed(importer.GetPlatformTextureSettings("Standalone"), importer))
			reimport = true;

		if (reimport)
			importer.SaveAndReimport();
	}

	static bool EnsurePlatformUncompressed(TextureImporterPlatformSettings settings, TextureImporter importer)
	{
		if (settings.textureCompression == TextureImporterCompression.Uncompressed)
			return false;
		settings.textureCompression = TextureImporterCompression.Uncompressed;
		importer.SetPlatformTextureSettings(settings);
		return true;
	}

	struct CardSlotRefs
	{
		public RectTransform root;
		public Image image;
		public TMP_Text label;
		public TMP_Text subLabel;
		public Button button;
		public HoldemCardView view;
	}

	struct HoldemCardUi
	{
		public Image[] holeImages;
		public TMP_Text[] holeLabels;
		public TMP_Text[] holeRedrawLabels;
		public HoldemCardView[] holeViews;
		public Image[] communityImages;
		public TMP_Text[] communityLabels;
		public HoldemCardView[] communityViews;
		public TMP_Text stageLabel;
		public TMP_Text handLabel;
		public TMP_Text previewLabel;
	}

	struct HoldemActionUi
	{
		public Button attack;
		public Button redrawHole0;
		public Button redrawHole1;
		public Button redrawCommunity;
		public Button cancel;
	}

	struct HoldemDefenseUi
	{
		public GameObject panel;
		public CanvasGroup group;
		public RectTransform rect;
		public Image enemyImage;
		public TMP_Text enemyLabel;
		public HoldemCardView enemyView;
		public Image[] cardImages;
		public TMP_Text[] cardLabels;
		public HoldemCardView[] cardViews;
		public Button[] buttons;
		public TMP_Text resultLabel;
	}
}
