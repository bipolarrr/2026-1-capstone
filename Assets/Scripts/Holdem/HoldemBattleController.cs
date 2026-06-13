using System.Collections;
using System.Collections.Generic;
using Holdem.UI;
using TMPro;
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Holdem
{
	public class HoldemBattleController : BattleControllerBase, IBattleDebugTarget
	{
		const int HoleSlotCount = 2;
		const int CommunitySlotCount = 5;
		const int DefenseSlotCount = 5;
#if UNITY_EDITOR
		const string EditorCardSpriteRoot = "Assets/Holdem/Sprites/Cards";
		const string EditorCardBackPath = EditorCardSpriteRoot + "/card_back_acorn.png";
		const string EditorCardFacePath = EditorCardSpriteRoot + "/card_front_template.png";
		const string EditorCardFrontFolder = EditorCardSpriteRoot + "/Fronts";
		static readonly string[] EditorRankCodes = { "2", "3", "4", "5", "6", "7", "8", "9", "10", "J", "Q", "K", "A" };
		static readonly string[] EditorSuitCodes = { "C", "D", "H", "S" };
#endif

		[Header("Hold'em Cards")]
		[SerializeField] Image[] holeCardImages = new Image[HoleSlotCount];
		[SerializeField] TMP_Text[] holeCardLabels = new TMP_Text[HoleSlotCount];
		[SerializeField] TMP_Text[] holeRedrawCountLabels = new TMP_Text[HoleSlotCount];
		[SerializeField] HoldemCardView[] holeCardViews = new HoldemCardView[HoleSlotCount];
		[SerializeField] Image[] communityCardImages = new Image[CommunitySlotCount];
		[SerializeField] TMP_Text[] communityCardLabels = new TMP_Text[CommunitySlotCount];
		[SerializeField] HoldemCardView[] communityCardViews = new HoldemCardView[CommunitySlotCount];
		[SerializeField] Sprite cardFaceSprite;
		[SerializeField] Sprite cardBackSprite;
		[SerializeField] Sprite[] cardFrontSprites = new Sprite[52];

		[Header("Hold'em HUD")]
		[SerializeField] TMP_Text stageLabel;
		[SerializeField] TMP_Text handResultLabel;
		[SerializeField] TMP_Text damagePreviewLabel;
		[SerializeField] HoldemBattleMessageView battleMessageView;
		[SerializeField] HoldemCardView[] enemyAttackCardViews = new HoldemCardView[4];
		[SerializeField] Button attackButton;
		[SerializeField] Button redrawHole0Button;
		[SerializeField] Button redrawHole1Button;
		[SerializeField] Button redrawCommunityButton;
		[SerializeField] Button cancelButton;

		[Header("Defense UI")]
		[SerializeField] GameObject defensePanel;
		[SerializeField] CanvasGroup defensePanelGroup;
		[SerializeField] RectTransform defensePanelRect;
		[SerializeField] Image defenseEnemyCardImage;
		[SerializeField] TMP_Text defenseEnemyCardLabel;
		[SerializeField] HoldemCardView defenseEnemyCardView;
		[SerializeField] Image[] defenseCardImages = new Image[DefenseSlotCount];
		[SerializeField] TMP_Text[] defenseCardLabels = new TMP_Text[DefenseSlotCount];
		[SerializeField] HoldemCardView[] defenseCardViews = new HoldemCardView[DefenseSlotCount];
		[SerializeField] Button[] defenseButtons = new Button[DefenseSlotCount];
		[SerializeField] TMP_Text defenseResultLabel;
		[SerializeField] Sprite defenseBackSprite;

		[Header("Shared Animation")]
		[SerializeField] PlayerDeathAnimator deathAnimator;
		[SerializeField] PlayerAttackAnimator attackAnimator;
		[SerializeField] PlayerBodyAnimator playerBodyAnimator;
		[SerializeField] Image enemyProjectile;
		[SerializeField] BattleBottomFocusController bottomFocus;

		readonly System.Random defenseRandom = new System.Random();
		HoldemRoundState roundState;
		bool battleEnded;
		bool roundConfirmed;
		bool resolvingCounterattacks;
		bool waitingForDefensePick;
		bool defensePicked;
		bool pendingDefenseBlocked;
		int pendingEnemyIndex = -1;
		int pendingEnemyDamage;
		HoldemDefenseChallenge pendingDefenseChallenge;
		HoldemCard pendingChosenDefenseCard;
		Coroutine defensePanelRoutine;
		Coroutine defensePickRoutine;
		Coroutine dealRoutine;

		void Start()
		{
			vfx?.Init(Camera.main?.transform);

			if (GameSessionManager.PlayerHearts.TotalHalfHearts == 0)
			{
				Debug.LogWarning("[HoldemBattle] PlayerHearts가 비어 있음 - 리셋");
				GameSessionManager.PlayerHearts.Reset();
			}

			EnsureSessionBattleEnemies("HoldemBattle");
			LoadSessionEnemiesSnapshot();
			ApplyStageBackground();
			SetupEnemyDisplay();
			heartDisplay?.Refresh(GameSessionManager.PlayerHearts);
			if (bottomFocus != null)
			{
				bottomFocus.Bind(battleLog);
				bottomFocus.ShowInput();
			}

			InitializeCardPresentation();
			HideDefensePanel();
			LogBattleIntro();
			BeginNewPlayerAttackRound(false);
		}

		public override void OnEnemyPanelClicked(int index)
		{
			if (battleEnded || roundConfirmed || resolvingCounterattacks)
				return;

			base.OnEnemyPanelClicked(index);
			RefreshHoldemUI();
		}

		public void ConfirmAttack()
		{
			if (battleEnded || roundConfirmed || resolvingCounterattacks || roundState == null)
				return;
			if (!AnyLivingEnemy())
				return;

			var hand = roundState.EvaluateVisibleHand();
			if (hand.Rank == HoldemHandRank.HighCard && !HasValidTarget())
			{
				battleLog?.AddEntry("홀덤: 하이카드 공격은 대상을 선택해야 합니다.", BattleEventPresentation.LogAndPopup);
				return;
			}

			StartCoroutine(ConfirmAttackRoutine(hand));
		}

		IEnumerator ConfirmAttackRoutine(HoldemHandResult hand)
		{
			roundConfirmed = true;
			roundState.ConfirmAttack();
			RefreshActionButtons();

			var preview = HoldemDamageTable.Calculate(hand, roundState.StageNumber);
			int attackTarget = ResolveAttackTargetIndex();
			int visualTarget = preview.IsAoe ? FindFirstAliveEnemyIndex(enemies) : attackTarget;

			LogPlayerAttack(hand, preview);
			yield return PlayAttackResultEmphasis(preview.IsAoe);

			bool impactApplied = false;
			System.Action applyImpact = () =>
			{
				if (impactApplied)
					return;
				impactApplied = true;
				ApplyPlayerAttackImpact(hand, preview, attackTarget);
			};

			AudioManager.Play(ResolvePlayerAttackClipName(hand.Rank));
			if (attackAnimator != null)
			{
				var targetBody = EnemyBodyRect(visualTarget);
				Coroutine attackRoutine = attackAnimator.Play(targetBody, applyImpact);
				if (attackRoutine != null)
					yield return attackRoutine;
			}
			if (!impactApplied)
				applyImpact();

			if (AllEnemiesDead())
			{
				battleLog?.AddEntry("<color=#55FF55>모든 적을 처치했다!</color>",
					BattleEventPresentation.LogAndAnimation);
				yield return BattleWonRoutine();
				yield break;
			}

			if (!HasValidTarget())
				targetIndex = FindFirstAliveEnemyIndex(enemies);
			RefreshTargetMarkers();
			yield return EnemyCounterattackRoutine();
		}

		public void RedrawHoleCard0()
		{
			RedrawHoleCard(0);
		}

		public void RedrawHoleCard1()
		{
			RedrawHoleCard(1);
		}

		void RedrawHoleCard(int index)
		{
			if (roundState == null || !roundState.RedrawHoleCard(index))
				return;

			string label = index == 0 ? "첫 번째" : "두 번째";
			battleLog?.AddEntry($"홀덤: {label} 손패 재드로우: 남은 횟수 {roundState.HoleRedrawsRemaining[index]}.");
			RefreshHoldemUI();
			PlayCardRedrawAnimation(holeCardViews, index);
		}

		public void RedrawCommunity()
		{
			if (roundState == null || !roundState.RedrawCommunity())
				return;

			battleLog?.AddEntry("홀덤: 공유패 전체 재드로우 사용.");
			RefreshHoldemUI();
			PlayCommunityRedrawAnimation();
		}

		public void CancelBattle()
		{
			if (battleEnded || resolvingCounterattacks || roundConfirmed)
				return;

			battleEnded = true;
			GameSessionManager.CancelBattle();
			battleLog?.AddEntry("홀덤 전투 취소", BattleEventPresentation.LogAndAnimation);
			AudioManager.Play("UI_Back_NO");
			AudioManager.Play("Transition_2");
			SceneManager.LoadScene("GameExploreScene");
		}

		public void DefensePick0() => ResolveDefensePick(0);
		public void DefensePick1() => ResolveDefensePick(1);
		public void DefensePick2() => ResolveDefensePick(2);
		public void DefensePick3() => ResolveDefensePick(3);
		public void DefensePick4() => ResolveDefensePick(4);

		void ResolveDefensePick(int index)
		{
			if (!waitingForDefensePick || defensePicked)
				return;
			if (index < 0 || index >= pendingDefenseChallenge.DefenseCards.Count)
				return;
			if (defensePickRoutine != null)
				return;

			defensePickRoutine = StartCoroutine(ResolveDefensePickRoutine(index));
		}

		IEnumerator ResolveDefensePickRoutine(int index)
		{
			pendingChosenDefenseCard = pendingDefenseChallenge.DefenseCards[index];
			var result = HoldemDefenseResolver.Resolve(
				pendingDefenseChallenge.EnemyAttackCard,
				pendingChosenDefenseCard);
			pendingDefenseBlocked = result.Blocked;
			waitingForDefensePick = false;

			for (int i = 0; defenseButtons != null && i < defenseButtons.Length; i++)
			{
				if (defenseButtons[i] != null)
					defenseButtons[i].interactable = false;
				if (defenseCardViews != null && i < defenseCardViews.Length && defenseCardViews[i] != null)
					defenseCardViews[i].SetInteractableVisual(false);
			}

			if (defenseCardViews != null && index < defenseCardViews.Length && defenseCardViews[index] != null)
			{
				yield return defenseCardViews[index].PlayFlip(() =>
					SetDefenseCardFace(index, pendingChosenDefenseCard));
			}
			else
			{
				SetDefenseCardFace(index, pendingChosenDefenseCard);
			}

			string comparison = pendingDefenseBlocked ? "≥" : "<";
			string message = pendingDefenseBlocked
				? $"방어 성공: {HoldemCard.RankLabel(pendingChosenDefenseCard.Rank)} {comparison} {HoldemCard.RankLabel(pendingDefenseChallenge.EnemyAttackCard.Rank)}."
				: $"방어 실패: {HoldemCard.RankLabel(pendingChosenDefenseCard.Rank)} {comparison} {HoldemCard.RankLabel(pendingDefenseChallenge.EnemyAttackCard.Rank)}.";
			if (defenseResultLabel != null)
				defenseResultLabel.text = message;
			battleMessageView?.Show(
				pendingDefenseBlocked ? "방어 성공! 적의 카드가 힘을 잃었다." : "방어 실패! 공격이 파고든다.",
				pendingDefenseBlocked ? new Color(0.62f, 0.94f, 1f) : new Color(1f, 0.56f, 0.42f));
			battleLog?.AddEntry($"홀덤: {message}", BattleEventPresentation.LogAndAnimation);
			AudioManager.Play(pendingDefenseBlocked ? "Player_PerfectDefense" : "UI_Failure");

			var chosenView = defenseCardViews != null && index < defenseCardViews.Length ? defenseCardViews[index] : null;
			if (chosenView != null)
				yield return chosenView.PlayResultFeedback(pendingDefenseBlocked);

			var enemyCardView = CurrentEnemyAttackCardView();
			if (enemyCardView != null)
			{
				if (pendingDefenseBlocked)
					yield return enemyCardView.PlayDefeatedDrop();
				else
					yield return enemyCardView.PlayShake(18f, 0.24f);
			}

			yield return new WaitForSeconds(0.18f);
			defensePicked = true;
			defensePickRoutine = null;
		}

		void BeginNewPlayerAttackRound(bool addSeparator)
		{
			if (battleEnded)
				return;

			bool initialDeal = roundState == null;
			HoldemTurnAdvanceResult turnAdvance = default;
			if (roundState == null)
				roundState = new HoldemRoundState((int)(Time.realtimeSinceStartup * 1000f));
			else
				turnAdvance = roundState.BeginNextPlayerAttackTurn();

			roundConfirmed = false;
			resolvingCounterattacks = false;
			waitingForDefensePick = false;
			defensePicked = false;
			HideDefensePanel();
			if (addSeparator)
				battleLog?.AddEntry("<color=#AAAAAA>── 홀덤 다음 라운드 ──</color>");
			RefreshHoldemUI();
			if (initialDeal)
				PlayInitialDealAnimation();
			else
				PlayTurnStartCommunityAnimation(turnAdvance);
		}

		IEnumerator EnemyCounterattackRoutine()
		{
			resolvingCounterattacks = true;
			RefreshActionButtons();

			for (int i = 0; i < enemies.Count; i++)
			{
				if (battleEnded)
					yield break;
				if (!enemies[i].IsAlive)
					continue;

				pendingEnemyIndex = i;
				pendingEnemyDamage = CalculateEnemyDamageHalfHearts(enemies[i].rank);
				pendingDefenseChallenge = HoldemDefenseResolver.GenerateChallenge(enemies[i].rank, defenseRandom);
				pendingDefenseBlocked = false;
				defensePicked = false;
				waitingForDefensePick = true;
				ShowDefenseChallenge(i);

				yield return new WaitUntil(() => defensePicked || battleEnded);
				if (battleEnded)
					yield break;

				yield return PlayEnemyCounterattack(i, pendingDefenseBlocked, pendingEnemyDamage);
				if (battleEnded)
					yield break;

				yield return new WaitForSeconds(0.25f);
				HideDefensePanel();
			}

			if (!battleEnded)
				BeginNewPlayerAttackRound(true);
		}

		void ShowDefenseChallenge(int enemyIndex)
		{
			ShowDefensePanelAnimated();
			ShowEnemyAttackCard(enemyIndex);

			SetDefenseEnemyCardFace(pendingDefenseChallenge.EnemyAttackCard);
			if (defenseResultLabel != null)
				defenseResultLabel.text = $"{enemies[enemyIndex].name}의 공격 카드";
			battleMessageView?.Show("이 카드와 같거나 더 높은 카드를 골라야 방어 성공!", AccentTextColor());

			for (int i = 0; i < DefenseSlotCount; i++)
			{
				SetDefenseCardBack(i);
				if (defenseButtons != null && i < defenseButtons.Length && defenseButtons[i] != null)
					defenseButtons[i].interactable = true;
				if (defenseCardViews != null && i < defenseCardViews.Length && defenseCardViews[i] != null)
				{
					defenseCardViews[i].SetInteractableVisual(true);
					StartCoroutine(defenseCardViews[i].PlayDealIn(i * 0.035f, new Vector2(0f, -40f)));
				}
			}
		}

		void HideDefensePanel()
		{
			if (defensePanelRoutine != null)
			{
				StopCoroutine(defensePanelRoutine);
				defensePanelRoutine = null;
			}
			if (defensePickRoutine != null)
			{
				StopCoroutine(defensePickRoutine);
				defensePickRoutine = null;
			}
			if (defenseCardViews != null)
			{
				for (int i = 0; i < defenseCardViews.Length; i++)
				{
					if (defenseCardViews[i] != null)
						defenseCardViews[i].SetInteractableVisual(false);
				}
			}
			if (defensePanelGroup != null)
			{
				defensePanelGroup.alpha = 0f;
				defensePanelGroup.blocksRaycasts = false;
			}
			if (defensePanelRect != null)
				defensePanelRect.anchoredPosition = new Vector2(0f, -28f);
			if (defensePanel != null)
				defensePanel.SetActive(false);
			HideEnemyAttackCards();
			battleMessageView?.Hide();
		}

		IEnumerator PlayEnemyCounterattack(int enemyIndex, bool blocked, int damageHalfHearts)
		{
			if (!ShouldPlayEnemyAttackPresentation(blocked))
			{
				Coroutine defenseRoutine = playerBodyAnimator != null ? playerBodyAnimator.PlayDefense() : null;
				if (battleAnims != null && playerBody != null)
					battleAnims.FlashHit(playerBody, new Color(0.35f, 0.85f, 1f), 0.14f, 0.24f);
				if (defenseRoutine != null)
					yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);
				else
					yield return new WaitForSeconds(0.12f);
				yield break;
			}

			var body = EnemyBodyRect(enemyIndex);
			bool impactApplied = false;
			System.Action applyImpact = () =>
			{
				if (impactApplied)
					return;
				impactApplied = true;
				ApplyHoldemEnemyAttackImpact(enemyIndex, damageHalfHearts);
			};

			Coroutine attackRoutine = PlayEnemyAttackAnimation(enemyIndex);
			if (battleAnims != null && body != null)
				yield return battleAnims.JumpInPlace(body, 26f, 0.28f);

			if (attackRoutine != null)
				yield return new WaitWhile(() =>
					enemyAnimators != null
					&& enemyIndex >= 0
					&& enemyIndex < enemyAnimators.Length
					&& enemyAnimators[enemyIndex] != null
					&& enemyAnimators[enemyIndex].IsActionPlaying);

			applyImpact();

			Coroutine hitRoutine = playerBodyAnimator != null
				? playerBodyAnimator.PlayHitByEnemyRank(enemies[enemyIndex].rank, damageHalfHearts)
				: null;
			if (battleAnims != null && playerBody != null)
				battleAnims.FlashDamage(playerBody);
			if (hitRoutine != null)
				yield return new WaitWhile(() => playerBodyAnimator != null && playerBodyAnimator.IsActionPlaying);

			if (!GameSessionManager.IsPlayerAlive)
				yield return PlayerDefeatedRoutine();
		}

		void ApplyHoldemEnemyAttackImpact(int enemyIndex, int damageHalfHearts)
		{
			bool revived = GameSessionManager.TakePlayerDamage(damageHalfHearts);
			heartDisplay?.Refresh(GameSessionManager.PlayerHearts);
			if (revived)
				battleLog?.AddEntry("부활의 부적이 치명타를 막았다.", BattleEventPresentation.LogAndAnimation);
			else
				battleLog?.AddEntry($"{enemies[enemyIndex].name} 공격 적중: {damageHalfHearts} 피해.",
					BattleEventPresentation.LogAndAnimation);
		}

		void RefreshHoldemUI()
		{
			if (roundState == null)
				return;

			for (int i = 0; i < HoleSlotCount; i++)
			{
				SetHoleCardFace(i, roundState.HoleCards[i]);
				if (i < holeRedrawCountLabels.Length && holeRedrawCountLabels[i] != null)
					holeRedrawCountLabels[i].text = $"재드로우 {roundState.HoleRedrawsRemaining[i]}";
			}

			int revealed = roundState.RevealedCommunityCount;
			for (int i = 0; i < CommunitySlotCount; i++)
			{
				bool visible = i < revealed;
				if (visible)
					SetCommunityCardFace(i, roundState.CommunityCards[i]);
				else
					SetCommunityCardBack(i);
			}

			var hand = roundState.EvaluateVisibleHand();
			var preview = HoldemDamageTable.Calculate(hand, roundState.StageNumber);
			if (stageLabel != null)
				stageLabel.text = $"Stage {roundState.StageNumber} | 공유패 {revealed}/5";
			if (handResultLabel != null)
				handResultLabel.text = FormatHandName(hand);
			if (damagePreviewLabel != null)
				damagePreviewLabel.text = BuildPreviewText(hand, preview);

			RefreshActionButtons();
		}

		void RefreshActionButtons()
		{
			bool canAct = !battleEnded && !roundConfirmed && !resolvingCounterattacks && roundState != null;
			if (attackButton != null)
				attackButton.interactable = canAct && AnyLivingEnemy();
			if (redrawHole0Button != null)
				redrawHole0Button.interactable = canAct && roundState.HoleRedrawsRemaining[0] > 0;
			if (redrawHole1Button != null)
				redrawHole1Button.interactable = canAct && roundState.HoleRedrawsRemaining[1] > 0;
			if (redrawCommunityButton != null)
				redrawCommunityButton.interactable = canAct && roundState.CommunityRedrawsRemaining > 0;
			if (cancelButton != null)
				cancelButton.interactable = !battleEnded && !roundConfirmed && !resolvingCounterattacks;
		}

		void InitializeCardPresentation()
		{
#if UNITY_EDITOR
			EnsureEditorCardSprites();
#endif
			ApplyRuntimeCardPresentationLayout();
			CaptureCardBasePoses(holeCardViews);
			CaptureCardBasePoses(communityCardViews);
			CaptureCardBasePoses(defenseCardViews);
			CaptureCardBasePoses(enemyAttackCardViews);
			if (defenseEnemyCardView != null)
				defenseEnemyCardView.CaptureBasePose();
			HideEnemyAttackCards();
			battleMessageView?.HideImmediate();
		}

		void ApplyRuntimeCardPresentationLayout()
		{
			var communityMat = ParentRect(communityCardViews);
			SetAnchorBox(communityMat, 0.08f, 0.47f, 0.77f, 0.86f);
			for (int i = 0; communityCardViews != null && i < communityCardViews.Length; i++)
			{
				float xMin = 0.155f + i * 0.132f;
				ApplyCardLayout(communityCardViews[i], xMin, 0.06f, xMin + 0.095f, 0.94f, 1.10f);
			}

			var handFan = ParentRect(holeCardViews);
			SetAnchorBox(handFan, 0.22f, 0.00f, 0.70f, 0.52f);
			ApplyCardLayout(SafeView(holeCardViews, 0), 0.18f, 0.07f, 0.36f, 0.95f, 1.08f);
			ApplyCardLayout(SafeView(holeCardViews, 1), 0.425f, 0.07f, 0.605f, 0.95f, 1.08f);
			ApplyButtonLayout(redrawHole0Button, 0.31f, 0.028f, 0.39f, 0.108f);
			ApplyButtonLayout(redrawHole1Button, 0.43f, 0.028f, 0.51f, 0.108f);

			for (int i = 0; enemyAttackCardViews != null && i < enemyAttackCardViews.Length; i++)
				ApplyCardLayout(enemyAttackCardViews[i], 0.26f, 0.44f, 0.74f, 1.02f, 1.06f);
		}

		static RectTransform ParentRect(HoldemCardView[] views)
		{
			var view = SafeView(views, 0);
			return view != null && view.RectTransform != null
				? view.RectTransform.parent as RectTransform
				: null;
		}

		static HoldemCardView SafeView(HoldemCardView[] views, int index)
		{
			if (views == null || index < 0 || index >= views.Length)
				return null;
			return views[index];
		}

		static void ApplyCardLayout(HoldemCardView view,
			float xMin, float yMin, float xMax, float yMax, float scale)
		{
			if (view == null)
				return;

			var rect = view.RectTransform;
			SetAnchorBox(rect, xMin, yMin, xMax, yMax);
			rect.localScale = new Vector3(scale, scale, 1f);
		}

		static void ApplyButtonLayout(Button button, float xMin, float yMin, float xMax, float yMax)
		{
			if (button == null)
				return;

			SetAnchorBox(button.GetComponent<RectTransform>(), xMin, yMin, xMax, yMax);
		}

		static void SetAnchorBox(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
		{
			if (rect == null)
				return;

			rect.anchorMin = new Vector2(xMin, yMin);
			rect.anchorMax = new Vector2(xMax, yMax);
			rect.offsetMin = Vector2.zero;
			rect.offsetMax = Vector2.zero;
			rect.anchoredPosition = Vector2.zero;
		}

		static void CaptureCardBasePoses(HoldemCardView[] views)
		{
			if (views == null)
				return;
			for (int i = 0; i < views.Length; i++)
			{
				if (views[i] != null)
					views[i].CaptureBasePose();
			}
		}

		void SetHoleCardFace(int index, HoldemCard card)
		{
			string redrawText = index >= 0 && index < roundState.HoleRedrawsRemaining.Length
				? $"재드로우 {roundState.HoleRedrawsRemaining[index]}"
				: "";
			SetCardFace(holeCardViews, holeCardImages, holeCardLabels, index, card, redrawText);
		}

		void SetCommunityCardFace(int index, HoldemCard card)
		{
			SetCardFace(communityCardViews, communityCardImages, communityCardLabels, index, card, "");
		}

		void SetCommunityCardBack(int index)
		{
			SetCardBack(communityCardViews, communityCardImages, communityCardLabels, index, cardBackSprite);
		}

		void SetDefenseEnemyCardFace(HoldemCard card)
		{
			Sprite frontSprite = CardFrontSpriteFor(card);
			string display = frontSprite != null ? "" : card.ToDisplayString();
			if (defenseEnemyCardView != null)
				defenseEnemyCardView.SetFace(frontSprite != null ? frontSprite : cardFaceSprite,
					CardFaceFallback(), display, "공격", CardTextColor(card));
			if (defenseEnemyCardLabel != null)
			{
				defenseEnemyCardLabel.text = display;
				defenseEnemyCardLabel.color = CardTextColor(card);
			}
			if (defenseEnemyCardImage != null)
				ApplyFaceVisual(defenseEnemyCardImage, frontSprite);
		}

		void SetDefenseCardFace(int index, HoldemCard card)
		{
			SetCardFace(defenseCardViews, defenseCardImages, defenseCardLabels, index, card, "");
		}

		void SetDefenseCardBack(int index)
		{
			SetCardBack(defenseCardViews, defenseCardImages, defenseCardLabels, index, defenseBackSprite);
		}

		void SetCardFace(HoldemCardView[] views, Image[] images, TMP_Text[] labels,
			int index, HoldemCard card, string detailText)
		{
			string display = card.ToDisplayString();
			var textColor = CardTextColor(card);
			Sprite frontSprite = CardFrontSpriteFor(card);
			string labelText = frontSprite != null ? "" : display;
			if (views != null && index >= 0 && index < views.Length && views[index] != null)
				views[index].SetFace(frontSprite != null ? frontSprite : cardFaceSprite,
					CardFaceFallback(), labelText, detailText, textColor);
			if (labels != null && index >= 0 && index < labels.Length && labels[index] != null)
			{
				labels[index].text = labelText;
				labels[index].color = textColor;
			}
			if (images != null && index >= 0 && index < images.Length && images[index] != null)
				ApplyFaceVisual(images[index], frontSprite);
		}

		void SetCardBack(HoldemCardView[] views, Image[] images, TMP_Text[] labels, int index, Sprite backSprite)
		{
			if (views != null && index >= 0 && index < views.Length && views[index] != null)
				views[index].SetBack(backSprite != null ? backSprite : cardBackSprite, CardBackFallback(), "✦", "");
			if (labels != null && index >= 0 && index < labels.Length && labels[index] != null)
			{
				labels[index].text = "✦";
				labels[index].color = AccentTextColor();
			}
			if (images != null && index >= 0 && index < images.Length && images[index] != null)
				ApplyBackVisual(images[index], backSprite);
		}

		void PlayInitialDealAnimation()
		{
			if (!isActiveAndEnabled)
				return;
			if (dealRoutine != null)
				StopCoroutine(dealRoutine);
			dealRoutine = StartCoroutine(InitialDealRoutine());
		}

		IEnumerator InitialDealRoutine()
		{
			if (holeCardViews != null)
			{
				for (int i = 0; i < holeCardViews.Length; i++)
				{
					if (holeCardViews[i] != null)
						StartCoroutine(holeCardViews[i].PlayDealIn(i * 0.08f, new Vector2(0f, -90f)));
				}
			}
			if (communityCardViews != null)
			{
				for (int i = 0; i < communityCardViews.Length; i++)
				{
					if (communityCardViews[i] != null)
						StartCoroutine(communityCardViews[i].PlayDealIn(0.12f + i * 0.045f, new Vector2(-52f, 70f)));
				}
			}
			yield return new WaitForSeconds(0.48f);
			dealRoutine = null;
		}

		void PlayTurnStartCommunityAnimation(HoldemTurnAdvanceResult turnAdvance)
		{
			if (!turnAdvance.Advanced)
				return;
			if (turnAdvance.ReplacedCommunity)
			{
				PlayCommunityRedrawAnimation();
				return;
			}
			if (turnAdvance.RevealedAdditionalCards)
			{
				PlayCommunityRevealAnimation(
					turnAdvance.PreviousRevealedCommunityCount,
					turnAdvance.RevealedCommunityCount);
			}
		}

		void PlayCommunityRevealAnimation(int previousRevealed, int currentRevealed)
		{
			if (communityCardViews == null)
				return;
			for (int i = Mathf.Max(0, previousRevealed); i < currentRevealed && i < communityCardViews.Length; i++)
			{
				int cardIndex = i;
				if (communityCardViews[cardIndex] != null)
				{
					SetCommunityCardBack(cardIndex);
					StartCoroutine(communityCardViews[cardIndex].PlayFlip(() =>
						SetCommunityCardFace(cardIndex, roundState.CommunityCards[cardIndex])));
				}
			}
		}

		void PlayCardRedrawAnimation(HoldemCardView[] views, int index)
		{
			if (views == null || index < 0 || index >= views.Length || views[index] == null)
				return;
			StartCoroutine(views[index].PlayRedraw());
		}

		void PlayCommunityRedrawAnimation()
		{
			if (communityCardViews == null)
				return;
			for (int i = 0; i < communityCardViews.Length; i++)
			{
				if (communityCardViews[i] == null)
					continue;
				StartCoroutine(PlayDelayedCardRedraw(communityCardViews[i], i * 0.04f));
			}
		}

		IEnumerator PlayDelayedCardRedraw(HoldemCardView view, float delay)
		{
			if (delay > 0f)
				yield return new WaitForSeconds(delay);
			if (view != null)
				yield return view.PlayRedraw();
		}

		IEnumerator PlayAttackResultEmphasis(bool isAoe)
		{
			battleMessageView?.Show(isAoe ? "완성된 패가 전장을 휩쓴다!" : "선택한 적에게 승부를 건다!", AccentTextColor());
			float duration = 0.28f;
			float elapsed = 0f;
			var handRect = handResultLabel != null ? handResultLabel.rectTransform : null;
			var previewRect = damagePreviewLabel != null ? damagePreviewLabel.rectTransform : null;
			Vector3 handBase = handRect != null ? handRect.localScale : Vector3.one;
			Vector3 previewBase = previewRect != null ? previewRect.localScale : Vector3.one;
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				float pulse = Mathf.Sin(t * Mathf.PI) * 0.16f;
				if (handRect != null)
					handRect.localScale = handBase * (1f + pulse);
				if (previewRect != null)
					previewRect.localScale = previewBase * (1f + pulse * 0.75f);
				yield return null;
			}
			if (handRect != null)
				handRect.localScale = handBase;
			if (previewRect != null)
				previewRect.localScale = previewBase;
		}

		void ShowDefensePanelAnimated()
		{
			if (defensePanel == null)
				return;
			if (defensePanelRoutine != null)
				StopCoroutine(defensePanelRoutine);
			defensePanel.SetActive(true);
			defensePanelRoutine = StartCoroutine(DefensePanelRoutine(true));
		}

		IEnumerator DefensePanelRoutine(bool show)
		{
			if (defensePanelGroup == null || defensePanelRect == null)
				yield break;

			float startAlpha = defensePanelGroup.alpha;
			float endAlpha = show ? 1f : 0f;
			Vector2 hidden = new Vector2(0f, -28f);
			Vector2 startPosition = defensePanelRect.anchoredPosition;
			Vector2 endPosition = show ? Vector2.zero : hidden;
			defensePanelGroup.blocksRaycasts = show;
			float elapsed = 0f;
			const float duration = 0.18f;
			while (elapsed < duration)
			{
				elapsed += Time.deltaTime;
				float t = Mathf.Clamp01(elapsed / duration);
				float eased = 1f - Mathf.Pow(1f - t, 3f);
				defensePanelGroup.alpha = Mathf.Lerp(startAlpha, endAlpha, eased);
				defensePanelRect.anchoredPosition = Vector2.Lerp(startPosition, endPosition, eased);
				yield return null;
			}
			defensePanelGroup.alpha = endAlpha;
			defensePanelRect.anchoredPosition = endPosition;
			defensePanelRoutine = null;
		}

		void ShowEnemyAttackCard(int enemyIndex)
		{
			HideEnemyAttackCards();
			if (enemyAttackCardViews == null || enemyIndex < 0 || enemyIndex >= enemyAttackCardViews.Length)
				return;
			var view = enemyAttackCardViews[enemyIndex];
			if (view == null)
				return;
			view.ShowImmediate(true);
			var attackCard = pendingDefenseChallenge.EnemyAttackCard;
			Sprite frontSprite = CardFrontSpriteFor(attackCard);
			view.SetFace(frontSprite != null ? frontSprite : cardFaceSprite, CardFaceFallback(),
				frontSprite != null ? "" : attackCard.ToDisplayString(), "ATTACK",
				CardTextColor(attackCard));
			StartCoroutine(view.PlayDealIn(0f, new Vector2(0f, 42f)));
		}

		void HideEnemyAttackCards()
		{
			if (enemyAttackCardViews == null)
				return;
			for (int i = 0; i < enemyAttackCardViews.Length; i++)
			{
				if (enemyAttackCardViews[i] != null)
					enemyAttackCardViews[i].ShowImmediate(false);
			}
		}

		HoldemCardView CurrentEnemyAttackCardView()
		{
			if (enemyAttackCardViews == null || pendingEnemyIndex < 0 || pendingEnemyIndex >= enemyAttackCardViews.Length)
				return null;
			return enemyAttackCardViews[pendingEnemyIndex];
		}

		static Color CardFaceFallback()
		{
			return new Color(0.92f, 0.86f, 0.68f, 1f);
		}

		static Color CardBackFallback()
		{
			return new Color(0.30f, 0.16f, 0.07f, 1f);
		}

		static Color AccentTextColor()
		{
			return new Color(1f, 0.83f, 0.36f, 1f);
		}

		static Color CardTextColor(HoldemCard card)
		{
			return card.Suit == HoldemSuit.Hearts || card.Suit == HoldemSuit.Diamonds
				? new Color(0.68f, 0.08f, 0.08f, 1f)
				: new Color(0.06f, 0.06f, 0.08f, 1f);
		}

		void ApplyPlayerAttackImpact(HoldemHandResult hand, HoldemDamagePreview preview, int attackTarget)
		{
			if (preview.IsAoe)
			{
				for (int i = 0; i < enemies.Count; i++)
				{
					if (enemies[i].IsAlive)
						DamageEnemy(i, preview.Damage);
				}
			}
			else if (attackTarget >= 0 && attackTarget < enemies.Count && enemies[attackTarget].IsAlive)
			{
				DamageEnemy(attackTarget, preview.Damage);
			}

			RefreshAllEnemyHp();
			RefreshTargetMarkers();
			if (vfx != null && hand.Rank >= HoldemHandRank.ThreeOfAKind)
				vfx.Shake(8f + (int)hand.Rank * 1.5f);
		}

		void DamageEnemy(int enemyIndex, int damage)
		{
			bool wasAlive = enemies[enemyIndex].IsAlive;
			enemies[enemyIndex].TakeDamage(damage);
			vfx?.SpawnDamageText(enemyIndex, damage);
			PlayEnemyDamagedFeedback(enemyIndex);
			if (wasAlive && !enemies[enemyIndex].IsAlive)
			{
				battleLog?.AddEntry($"  <color=#FF8888>{enemies[enemyIndex].name} 처치!</color>");
				AudioManager.Play("Enemy_Die");
			}
		}

		void LogPlayerAttack(HoldemHandResult hand, HoldemDamagePreview preview)
		{
			if (battleLog == null)
				return;

			if (preview.IsAoe)
			{
				battleLog.AddEntry(
					$"홀덤: Stage {roundState.StageNumber}, {hand.KoreanName} x{preview.StageMultiplier:0.0} - 모든 적에게 {preview.Damage} 데미지.",
					BattleEventPresentation.LogAndAnimation);
				return;
			}

			battleLog.AddEntry(
				$"홀덤: 하이카드 {HoldemCard.RankLabel(hand.PrimaryRank)} - 대상에게 {preview.Damage} 데미지.",
				BattleEventPresentation.LogAndAnimation);
		}

		string BuildPreviewText(HoldemHandResult hand, HoldemDamagePreview preview)
		{
			string target = preview.IsAoe ? "전체 공격" : "단일 공격";
			return $"x{preview.StageMultiplier:0.0} | {target} {preview.Damage}";
		}

		static string FormatHandName(HoldemHandResult hand)
		{
			if (hand == null)
				return "";
			if (hand.Rank == HoldemHandRank.HighCard)
				return $"High Card {HoldemCard.RankLabel(hand.PrimaryRank)}";
			return hand.DisplayName;
		}

#if UNITY_EDITOR
		void EnsureEditorCardSprites()
		{
			if (cardBackSprite == null)
				cardBackSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorCardBackPath);
			if (cardFaceSprite == null)
				cardFaceSprite = AssetDatabase.LoadAssetAtPath<Sprite>(EditorCardFacePath);
			if (defenseBackSprite == null)
				defenseBackSprite = cardBackSprite;

			if (cardFrontSprites == null || cardFrontSprites.Length != 52)
				cardFrontSprites = new Sprite[52];

			int missingCount = 0;
			int index = 0;
			for (int suit = 0; suit < EditorSuitCodes.Length; suit++)
			{
				for (int rank = 0; rank < EditorRankCodes.Length; rank++)
				{
					if (cardFrontSprites[index] == null)
					{
						string path = $"{EditorCardFrontFolder}/{EditorRankCodes[rank]}{EditorSuitCodes[suit]}.png";
						cardFrontSprites[index] = AssetDatabase.LoadAssetAtPath<Sprite>(path);
					}
					if (cardFrontSprites[index] == null)
						missingCount++;
					index++;
				}
			}

			if (cardBackSprite == null || cardFaceSprite == null || missingCount > 0)
			{
				Debug.LogWarning(
					$"[HoldemBattle] 카드 스프라이트 일부가 비어 있습니다. back={cardBackSprite != null}, face={cardFaceSprite != null}, frontsMissing={missingCount}");
			}
		}
#endif

		Sprite CardFrontSpriteFor(HoldemCard card)
		{
			int index = CardFrontSpriteIndex(card);
			if (cardFrontSprites == null || index < 0 || index >= cardFrontSprites.Length)
				return null;
			return cardFrontSprites[index];
		}

		static int CardFrontSpriteIndex(HoldemCard card)
		{
			int rankIndex = RankSpriteIndex(card.Rank);
			int suitIndex = SuitSpriteIndex(card.Suit);
			if (rankIndex < 0 || suitIndex < 0)
				return -1;
			return suitIndex * 13 + rankIndex;
		}

		static int RankSpriteIndex(HoldemRank rank)
		{
			int value = (int)rank;
			return value >= 2 && value <= 14 ? value - 2 : -1;
		}

		static int SuitSpriteIndex(HoldemSuit suit)
		{
			switch (suit)
			{
				case HoldemSuit.Clubs: return 0;
				case HoldemSuit.Diamonds: return 1;
				case HoldemSuit.Hearts: return 2;
				case HoldemSuit.Spades: return 3;
				default: return -1;
			}
		}

		void ApplyFaceVisual(Image image, Sprite frontSprite = null)
		{
			if (image == null)
				return;
			image.sprite = HoldemCardView.GetDisplaySprite(frontSprite != null ? frontSprite : cardFaceSprite);
			image.color = image.sprite != null ? Color.white : new Color(0.92f, 0.86f, 0.68f, 1f);
			image.preserveAspect = image.sprite != null;
		}

		void ApplyBackVisual(Image image, Sprite backSprite)
		{
			if (image == null)
				return;
			image.sprite = HoldemCardView.GetDisplaySprite(backSprite != null ? backSprite : cardBackSprite);
			image.color = image.sprite != null ? Color.white : new Color(0.30f, 0.16f, 0.07f, 1f);
			image.preserveAspect = image.sprite != null;
		}

		RectTransform EnemyBodyRect(int index)
		{
			if (enemyBodies == null || index < 0 || index >= enemyBodies.Length || enemyBodies[index] == null)
				return null;
			return enemyBodies[index].rectTransform;
		}

		bool HasValidTarget()
		{
			return targetIndex >= 0 && targetIndex < enemies.Count && enemies[targetIndex].IsAlive;
		}

		int ResolveAttackTargetIndex()
		{
			return HasValidTarget() ? targetIndex : FindFirstAliveEnemyIndex(enemies);
		}

		bool AnyLivingEnemy()
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				if (enemies[i].IsAlive)
					return true;
			}
			return false;
		}

		bool AllEnemiesDead()
		{
			return !AnyLivingEnemy();
		}

		static int CalculateEnemyDamageHalfHearts(int enemyRank)
		{
			return Mathf.CeilToInt(Mathf.Clamp(enemyRank, 1, 5) * 0.5f);
		}

		public static bool ShouldPlayEnemyAttackPresentation(bool defenseBlocked)
		{
			return !defenseBlocked;
		}

		static string ResolvePlayerAttackClipName(HoldemHandRank rank)
		{
			if (rank >= HoldemHandRank.FullHouse)
				return "Player_Attack_Big";
			if (rank >= HoldemHandRank.Straight)
				return "Player_Attack_Medium";
			if (rank >= HoldemHandRank.OnePair)
				return "Player_Attack_Small";
			return "Player_Attack";
		}

		IEnumerator BattleWonRoutine()
		{
			battleEnded = true;
			RefreshActionButtons();
			float waitStartedAt = Time.time;
			yield return WaitForEnemyDeathAnimations(5.2f);
			float remaining = 1.0f - (Time.time - waitStartedAt);
			if (remaining > 0f)
				yield return new WaitForSeconds(remaining);
			GameSessionManager.CompleteBattleWon();
			AudioManager.Play("Transition_2");
			SceneManager.LoadScene("GameExploreScene");
		}

		IEnumerator PlayerDefeatedRoutine()
		{
			if (battleEnded)
				yield break;

			battleEnded = true;
			RefreshActionButtons();
			HideDefensePanel();
			battleLog?.AddEntry("패배", BattleEventPresentation.LogAndAnimation);
			AudioManager.Play("Player_Death");
			if (deathAnimator != null)
				yield return deathAnimator.PlayDeathSequence();
			else
			{
				yield return new WaitForSeconds(1.0f);
				SceneManager.LoadScene("MainMenu");
			}
		}

		public string DebugKillPlayer()
		{
			if (!GameSessionManager.IsPlayerAlive)
				return "[무시] 플레이어가 이미 사망 상태입니다.";

			GameSessionManager.PlayerHearts.TakeDamage(GameSessionManager.PlayerHearts.TotalHalfHearts);
			heartDisplay?.Refresh(GameSessionManager.PlayerHearts);
			StartCoroutine(PlayerDefeatedRoutine());
			return "[Debug] 홀덤 플레이어 즉사";
		}

		public string DebugKillAllEnemies()
		{
			for (int i = 0; i < enemies.Count; i++)
			{
				enemies[i].hp = 0;
				UpdateEnemyHp(i);
			}
			RefreshTargetMarkers();
			StartCoroutine(BattleWonRoutine());
			return "[Debug] 홀덤 모든 적 즉사";
		}

		public string DebugKillEnemies(int[] indices)
		{
			if (indices == null || indices.Length == 0)
				return "[Debug] 인덱스 없음";

			var killed = new List<int>();
			foreach (int idx in indices)
			{
				if (idx < 0 || idx >= enemies.Count)
					continue;
				enemies[idx].hp = 0;
				UpdateEnemyHp(idx);
				killed.Add(idx);
			}
			RefreshTargetMarkers();
			if (AllEnemiesDead())
				StartCoroutine(BattleWonRoutine());
			return $"[Debug] 홀덤 적 즉사: [{string.Join(",", killed)}]";
		}

		public string DebugPlaySprite(string target, int objectIndex, string spriteKind, float loopSeconds)
		{
			return DebugPlayBattleSprite(target, objectIndex, spriteKind, playerBodyAnimator, loopSeconds);
		}
	}
}
