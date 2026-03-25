// Assets/Scripts/Dice/YachtGameManager.cs
// 주사위 데미지 시스템.
// 5개 주사위, 3회 굴리기, 클릭 보관(Save Zone 이동).
// 콤보 판정: Yacht > Four of a Kind > Large Straight > Small Straight > Full House > 합산.

using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class YachtGameManager : MonoBehaviour
{
	[Header("주사위 (5개)")]
	[SerializeField] private YachtDie[] dice;

	[Header("인터랙션")]
	[SerializeField] private DiceViewportInteraction viewportInteraction;

	[Header("UI — 버튼")]
	[SerializeField] private Button rollButton;
	[SerializeField] private Button confirmButton;
	[SerializeField] private Button nextRollButton;

	[Header("UI — 텍스트")]
	[SerializeField] private TMP_Text rollsRemainingText;
	[SerializeField] private TMP_Text damagePreviewText;

	[Header("UI — 기타")]
	[SerializeField] private GameObject yachtBanner; // 미사용, 하위호환용

	[Header("적")]
	[SerializeField] private EnemyDisplay enemyDisplay;

	// ── 위치 ─────────────────────────────────────────────────────────
	private Vector3[] homePositions;
	private Vector3[] savePositions;
	private const float SaveZoneOffset = 2.3f;

	// ── 상태 ─────────────────────────────────────────────────────────
	private int rollsRemaining  = 3;
	private int totalScore      = 0;
	private int settledCount    = 0;
	private int activeDiceCount = 0;
	private bool roundConfirmed = false;

	// 콤보별 흔들림 강도
	private const float ShakeYacht = 25f;
	private const float ShakeFourOfAKind = 18f;
	private const float ShakeLargeStraight = 14f;
	private const float ShakeFullHouse = 10f;
	private const float ShakeSmallStraight = 7f;

	// ── 초기화 ───────────────────────────────────────────────────────

	private void Start()
	{
		homePositions = new Vector3[dice.Length];
		savePositions = new Vector3[dice.Length];
		for (int i = 0; i < dice.Length; i++)
		{
			homePositions[i] = dice[i].transform.position;
			savePositions[i] = dice[i].transform.position + new Vector3(0, 0, SaveZoneOffset);
		}

		foreach (var die in dice)
			die.OnSettled += OnDieSettled;

		if (viewportInteraction != null)
		{
			viewportInteraction.OnHoverEnter += die => die.SetHovered(true);
			viewportInteraction.OnHoverExit  += die => die.SetHovered(false);
			viewportInteraction.OnClicked    += OnDieClicked;
		}

		rollButton.onClick.AddListener(RollDice);
		confirmButton.onClick.AddListener(ConfirmScore);
		nextRollButton.onClick.AddListener(NextRound);

		confirmButton.gameObject.SetActive(false);
		nextRollButton.gameObject.SetActive(false);
		if (yachtBanner != null)
			yachtBanner.SetActive(false);

		RefreshUI();
	}

	private void OnDestroy()
	{
		foreach (var die in dice)
			die.OnSettled -= OnDieSettled;
	}

	// ── 주사위 클릭 (보관 토글) ─────────────────────────────────────

	private void OnDieClicked(YachtDie die)
	{
		if (roundConfirmed) return;
		if (rollsRemaining == 3) return;
		if (dice.Any(d => d.IsRolling)) return;

		int index = System.Array.IndexOf(dice, die);
		if (index < 0) return;

		if (die.IsHeld)
			die.SetHeld(false, homePositions[index]);
		else
			die.SetHeld(true, savePositions[index]);

		bool canRoll = rollsRemaining > 0 && dice.Any(d => !d.IsHeld);
		rollButton.interactable = canRoll;

		UpdateDamagePreview();
	}

	// ── 굴리기 ───────────────────────────────────────────────────────

	private void RollDice()
	{
		if (rollsRemaining <= 0) return;
		if (dice.Any(d => d.IsRolling)) return;

		hasRolledOnce = true;
		rollsRemaining--;
		settledCount    = 0;
		activeDiceCount = dice.Count(d => !d.IsHeld);

		rollButton.interactable = false;
		confirmButton.gameObject.SetActive(false);

		if (damagePreviewText != null)
			damagePreviewText.text = "";

		foreach (var die in dice)
			die.Roll();

		UpdateRollsText();

		if (activeDiceCount == 0)
		{
			rollsRemaining++;
			rollButton.interactable = false;
			confirmButton.gameObject.SetActive(true);
		}
	}

	private void OnDieSettled(YachtDie die, int result)
	{
		settledCount++;
		if (settledCount < activeDiceCount)
			return;

		// ── 모두 안정됨 ──────────────────────────────────────────────
		bool canRollAgain = rollsRemaining > 0 && dice.Any(d => !d.IsHeld);
		rollButton.interactable = canRollAgain;
		confirmButton.gameObject.SetActive(true);

		UpdateDamagePreview();
	}

	// ── 점수 확정 ────────────────────────────────────────────────────

	private void ConfirmScore()
	{
		if (dice.Any(d => d.IsRolling)) return;

		var (comboName, damage) = CalculateDamage();
		totalScore += damage;
		roundConfirmed = true;

		float shake = GetShakeIntensity(comboName);
		if (enemyDisplay != null)
			enemyDisplay.TakeDamage(damage, shake);

		// UI 전환: 확정/굴리기 숨기고, 결과 + 다음 롤 표시
		confirmButton.gameObject.SetActive(false);
		rollButton.gameObject.SetActive(false);
		nextRollButton.gameObject.SetActive(true);

		UpdateDamagePreview();
		UpdateTotalScoreText();
	}

	private void NextRound()
	{
		roundConfirmed = false;
		nextRollButton.gameObject.SetActive(false);
		rollButton.gameObject.SetActive(true);
		ResetRound();
	}

	private void ResetRound()
	{
		rollsRemaining  = 3;
		settledCount    = 0;
		activeDiceCount = 0;

		for (int i = 0; i < dice.Length; i++)
			if (dice[i].IsHeld)
				dice[i].SetHeld(false, homePositions[i]);

		rollButton.interactable = true;
		confirmButton.gameObject.SetActive(false);

		RefreshUI();
	}

	// ── 콤보 판정 & 데미지 계산 ──────────────────────────────────────

	private (string comboName, int damage) CalculateDamage()
	{
		int[] values = dice.Select(d => d.Result).OrderBy(v => v).ToArray();
		int sum = values.Sum();

		var groups = values.GroupBy(v => v)
		                   .OrderByDescending(g => g.Count())
		                   .ToArray();
		int maxCount   = groups[0].Count();
		int groupCount = groups.Length;

		if (maxCount == 5)
			return ("YACHT!", 50);

		if (maxCount == 4)
			return ("Four of a Kind!", 40);

		// Large Straight (5개 연속)
		if (groupCount == 5 && values.SequenceEqual(new[] { 2, 3, 4, 5, 6 }))
			return ("Large Straight!", 35);

		if (groupCount == 5 && values.SequenceEqual(new[] { 1, 2, 3, 4, 5 }))
			return ("Large Straight!", 30);

		// Full House (3+2)
		if (maxCount == 3 && groupCount == 2)
			return ("Full House!", 25);

		// Small Straight (4개 연속) — 중복 눈이 있어도 4연속 포함이면 인정
		if (HasRun(values, 4))
			return ("Small Straight!", 20);

		return (null, sum);
	}

	private float GetShakeIntensity(string comboName)
	{
		if (comboName == null)
			return 0f;
		if (comboName.Contains("YACHT"))
			return ShakeYacht;
		if (comboName.Contains("Four"))
			return ShakeFourOfAKind;
		if (comboName.Contains("Large"))
			return ShakeLargeStraight;
		if (comboName.Contains("Full"))
			return ShakeFullHouse;
		if (comboName.Contains("Small"))
			return ShakeSmallStraight;
		return 0f;
	}

	/// <summary>정렬된 주사위 눈에서 연속 length개 이상이 포함되어 있는지 확인.</summary>
	private bool HasRun(int[] sorted, int length)
	{
		var distinct = sorted.Distinct().OrderBy(v => v).ToArray();
		int run = 1;
		for (int i = 1; i < distinct.Length; i++)
		{
			run = (distinct[i] == distinct[i - 1] + 1) ? run + 1 : 1;
			if (run >= length) return true;
		}
		return false;
	}

	// ── UI ──────────────────────────────────────────────────────────

	private void RefreshUI()
	{
		UpdateRollsText();
		UpdateDamagePreview();
		UpdateTotalScoreText();
	}

	private void UpdateRollsText()
	{
		if (rollsRemainingText != null)
			rollsRemainingText.text = $"남은 굴리기  {rollsRemaining} / 3";
	}

	private bool hasRolledOnce = false;

	private void UpdateDamagePreview()
	{
		var (comboName, damage) = CalculateDamage();

		if (damagePreviewText != null)
		{
			damagePreviewText.text = hasRolledOnce
				? (comboName != null ? $"{comboName}  {damage}" : $"{damage}")
				: "";
		}
	}

	private void UpdateTotalScoreText() { }
}
