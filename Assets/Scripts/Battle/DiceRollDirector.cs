using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 주사위 굴림 라이프사이클 전담 컨트롤러. BattleSceneController는 이 컴포넌트에
/// 턴 단위로 위임하고 확정/데미지/적 반격만 담당한다.
///
/// 상태 머신:
///   Idle  ──StartRoll──▶ Rolling ──RequestStop──▶ Stopping
///                                                   │
///                                        ┌──────────┴──────────┐
///                                        │                     │
///                                   showComeOut?            !showComeOut
///                                        │                     │
///                                  ComeOutPending          (바로 Finalize)
///                                        │                     │
///                                  AcceptComeOut               │
///                                        ▼                     │
///                                   Finalizing ◀───────────────┘
///                                        │
///                                        ▼
///                                      Idle
///
/// 같은 "굴리기" 버튼이 상태에 따라 굴리기/멈추기/나와라! 3역할을 수행한다.
/// </summary>
public class DiceRollDirector : MonoBehaviour
{
	public enum RollPhase { Idle, Rolling, Stopping, ComeOutPending, Finalizing }

	/// <summary>
	/// 이번 턴이 공격용인지 방어용인지. 방어 턴은 족보 유도/"나와라!"/플리커 연출 없이
	/// 굴림 결과를 그대로 5개 동시에 정지시킨다(로그·BGM 없음).
	/// </summary>
	public enum TurnMode { Attack, Defense }

	[SerializeField] Dice[] dice;
	[SerializeField] DiceViewportInteraction viewportInteraction;
	[SerializeField] Button rollButton;

	// "나와라 송" 오디오는 AudioManager/DiceDrumRollAudio 파사드 경유로만 재생한다.
	// 별도 AudioSource 필드는 두지 않는다 — 단일 진실 소스 원칙.

	[SerializeField] Vector3 vaultCenter;

	[Header("주사위 슬롯 레이아웃")]
	// 비홀드 주사위들은 slotCenter를 기준으로 slotSpacing 간격, 항상 대칭 배치.
	// 짝수 개: 중앙에 주사위 없음 ( ±spacing/2, ±spacing*3/2 ... ).
	// 홀수 개: 중앙에 주사위 1개 ( 0, ±spacing, ... ).
	// 공식: x_i = (i - (N-1)/2) * slotSpacing (i는 0..N-1 슬롯 인덱스, N은 비홀드 개수).
	[SerializeField] Vector3 slotCenter;
	[SerializeField] float   slotSpacing = 1.4f;

	[Header("저장 슬롯 스프라이트 표시")]
	[SerializeField] Image[]  heldDiceImages;
	[SerializeField] Sprite[] diceFaceSprites;

	[Header("연출 타이밍")]
	[SerializeField] float sequentialStopGap = 1f;
	[SerializeField] float snapDuration      = 0.18f;
	[SerializeField] float flickerDuration   = 0.9f;
	[SerializeField] float sortSlideDuration = 0.3f;

	static readonly Color ComeOutNormalColor    = new Color(0.90f, 0.70f, 0.15f, 0.95f);
	static readonly Color ComeOutHighlightColor = new Color(1.00f, 0.85f, 0.25f, 1.00f);
	static readonly Color ComeOutPressedColor   = new Color(0.65f, 0.48f, 0.08f, 1.00f);

	public RollPhase Phase { get; private set; } = RollPhase.Idle;
	public TurnMode Mode { get; private set; } = TurnMode.Attack;
	public int  RollsRemaining { get; private set; }
	public int  MaxRolls       { get; private set; } = 3;
	public bool HasRolledOnce  { get; private set; }

	public event System.Action OnRollStarted;
	public event System.Action OnRollSettled;  // 매 굴림 정지 완료 (데미지 프리뷰 갱신)
	public event System.Action OnHoldChanged;
	public event System.Action<DiceStopProfile, int[], bool[]> OnComeOutStarted;  // 나와라! 단계 진입

	int[]           currentPlan;
	bool[]          currentHeldMask;
	DiceStopProfile currentProfile;

	Coroutine stopRoutine;
	int       settleCounter;

	List<int>    heldOrder = new List<int>();

	TMP_Text              rollButtonLabel;
	ColorBlock            rollButtonDefaultColors;
	bool                  rollButtonColorsCached;
	bool                  holdInteractionEnabled;

	// ── 타이밍 플래그 ────────────────────────────────────
	// 유저가 "n개의 눈만 남은 상태"를 시각적으로 보고 있는 구간을 나타낸다.
	// 사전 계산된 프로필(StartRoll 시점)과 분리 — stable 주사위가 실제로 화면에서 멈춘 뒤에만 true.
	// 이 플래그가 true인 동안에만 "나와라!" 노래가 재생되고 버튼이 come-out 상태로 전환된다.
	bool isComboImminent;

	public bool IsComboImminent
	{
		get => isComboImminent;
		private set
		{
			if (isComboImminent == value) return;
			isComboImminent = value;
			OnComboImminentChanged(value);
		}
	}

	// ─────────────────────────────────────────────────────
	// Lifecycle
	// ─────────────────────────────────────────────────────

	void Awake()
	{
		if (dice == null) return;

		for (int i = 0; i < dice.Length; i++)
		{
			if (dice[i] == null) continue;
			dice[i].OnSettled += HandleDieSettled;
		}

		if (viewportInteraction != null)
		{
			viewportInteraction.OnHoverEnter += HandleHoverEnter;
			viewportInteraction.OnHoverExit  += HandleHoverExit;
			viewportInteraction.OnClicked    += HandleDieClicked;
		}

		if (rollButton != null)
		{
			rollButtonLabel = rollButton.GetComponentInChildren<TMP_Text>();
			rollButtonDefaultColors = rollButton.colors;
			rollButtonColorsCached  = true;
		}
	}

	void OnDestroy()
	{
		if (dice != null)
		{
			foreach (var d in dice)
				if (d != null) d.OnSettled -= HandleDieSettled;
		}

		if (viewportInteraction != null)
		{
			viewportInteraction.OnHoverEnter -= HandleHoverEnter;
			viewportInteraction.OnHoverExit  -= HandleHoverExit;
			viewportInteraction.OnClicked    -= HandleDieClicked;
		}
	}

	// ─────────────────────────────────────────────────────
	// Public API (BattleSceneController가 호출)
	// ─────────────────────────────────────────────────────

	/// <summary>새 라운드(공격/방어) 시작. 홀드/상태 초기화 + 굴림 횟수 설정.</summary>
	public void BeginTurn(int rollsAllowed, TurnMode mode = TurnMode.Attack)
	{
		CancelStopRoutine();
		IsComboImminent = false;

		Mode              = mode;
		MaxRolls          = Mathf.Max(1, rollsAllowed);
		RollsRemaining    = MaxRolls;
		HasRolledOnce     = false;
		Phase             = RollPhase.Idle;
		currentPlan       = null;
		currentHeldMask   = null;
		currentProfile    = default;
		settleCounter     = 0;
		holdInteractionEnabled = false;

		ReleaseAllHolds();
		ClearEmphasis();
		ApplyButtonMode(RollPhase.Idle);
		if (rollButton != null) rollButton.interactable = true;
	}

	/// <summary>홀드 입력 가능 여부. BattleSceneController가 roundConfirmed 등의 컨텍스트로 제어.</summary>
	public void SetHoldInteractionEnabled(bool on)
	{
		holdInteractionEnabled = on;
	}

	public void SetRollButtonInteractable(bool on)
	{
		if (rollButton != null) rollButton.interactable = on;
	}

	public int[] ReadFinalValues()
	{
		if (dice == null) return System.Array.Empty<int>();
		int[] v = new int[dice.Length];
		for (int i = 0; i < dice.Length; i++)
			v[i] = dice[i] != null ? dice[i].Result : 0;
		return v;
	}

	public bool[] ReadHeldMask()
	{
		if (dice == null) return System.Array.Empty<bool>();
		bool[] m = new bool[dice.Length];
		for (int i = 0; i < dice.Length; i++)
			m[i] = dice[i] != null && dice[i].IsHeld;
		return m;
	}

	public bool AnyDiceSpinning()
	{
		if (dice == null) return false;
		foreach (var d in dice) if (d != null && d.IsSpinning) return true;
		return false;
	}

	/// <summary>디버그/강제 설정: 모든 주사위를 주어진 값으로 설정하고 Vault에 배치.</summary>
	public void ForceSetDiceToVault(int[] values)
	{
		if (dice == null || values == null || values.Length != dice.Length) return;

		CancelStopRoutine();
		ReleaseAllHolds();

		for (int i = 0; i < dice.Length; i++)
		{
			dice[i].ForceResult(values[i]);
			heldOrder.Add(i);
			dice[i].SetHeld(true, VaultSlotPosition(heldOrder.Count - 1));
			dice[i].SetVisible(false);
		}

		Phase         = RollPhase.Idle;
		HasRolledOnce = true;
		if (RollsRemaining > 0) RollsRemaining--;
		ApplyButtonMode(RollPhase.Idle);
		RefreshHeldDisplay();
	}

	public void ReleaseAllHolds()
	{
		if (dice == null) return;
		// 비홀드 주사위가 이미 있으면 그 y(=elevated 가능)에 맞춰 unhold된 주사위도 같은 높이로 등장.
		float visibleY = SampleNonHeldRestingY(exceptIdx: -1, fallback: slotCenter.y);
		for (int i = 0; i < dice.Length; i++)
		{
			if (dice[i] == null) continue;
			Vector3 slot = GetSlotPosition(i, dice.Length);
			Vector3 visible = new Vector3(slot.x, visibleY, slot.z);
			if (dice[i].IsHeld)
			{
				dice[i].SetHeld(false, visible);
				dice[i].SetVisible(true);
				dice[i].SetSpinAnchor(slot);       // anchor는 base y
			}
			else
			{
				dice[i].transform.position = visible;
				dice[i].SetSpinAnchor(slot);
			}
		}
		heldOrder.Clear();
		RefreshHeldDisplay();
	}

	// ─────────────────────────────────────────────────────
	// Slot layout — 비홀드 주사위는 slotCenter를 기준으로 대칭 배치.
	// ─────────────────────────────────────────────────────

	/// <summary>비홀드 row 내 indexInRow(0..rowCount-1) 슬롯의 월드 좌표.</summary>
	Vector3 GetSlotPosition(int indexInRow, int rowCount)
	{
		if (rowCount <= 0) return slotCenter;
		float offset = (indexInRow - (rowCount - 1) * 0.5f) * slotSpacing;
		return slotCenter + Vector3.right * offset;
	}

	/// <summary>주어진 die가 비홀드 집합(face 오름차순) 내 rank 번째에 위치할 슬롯.
	/// treatAsNonHeld=true면 die가 아직 held 상태라도 비홀드로 계산.</summary>
	Vector3 ComputeFaceSortedSlotFor(int dieIdx, bool treatAsNonHeld)
	{
		var nonHeld = new List<int>();
		for (int i = 0; i < dice.Length; i++)
		{
			if (dice[i] == null) continue;
			if (i == dieIdx)
			{
				if (treatAsNonHeld || !dice[i].IsHeld) nonHeld.Add(i);
			}
			else if (!dice[i].IsHeld)
			{
				nonHeld.Add(i);
			}
		}
		nonHeld.Sort((a, b) => dice[a].Result.CompareTo(dice[b].Result));
		int rank = nonHeld.IndexOf(dieIdx);
		if (rank < 0) return slotCenter;
		return GetSlotPosition(rank, nonHeld.Count);
	}

	// ─────────────────────────────────────────────────────
	// Roll button dispatch — 상태에 따라 3역할 분기
	// ─────────────────────────────────────────────────────

	public void OnRollButtonPressed()
	{
		switch (Phase)
		{
			case RollPhase.Idle:            StartRoll();       break;
			case RollPhase.Rolling:         RequestStop();     break;
			case RollPhase.ComeOutPending:  AcceptComeOut();   break;
			// Stopping/Finalizing: 무시
		}
	}

	void StartRoll()
	{
		if (dice == null || RollsRemaining <= 0) return;

		int toRoll = 0;
		for (int i = 0; i < dice.Length; i++)
			if (!dice[i].IsHeld) toRoll++;

		if (toRoll == 0)
		{
			Debug.LogWarning("[DiceRoll] 모든 주사위가 홀드 중 — 굴림 취소");
			return;
		}

		RollsRemaining--;
		HasRolledOnce = true;

		// 원시 결과 → ComboFortune.Apply (파워업 훅) → currentPlan 저장
		int[] raw = new int[dice.Length];
		bool[] heldMask = new bool[dice.Length];
		for (int i = 0; i < dice.Length; i++)
		{
			heldMask[i] = dice[i].IsHeld;
			raw[i] = heldMask[i] ? dice[i].Result : DiceRandomizer.Next();
		}

		var plan = ComboFortune.Apply(raw, heldMask, GameSessionManager.PowerUps);
		currentPlan     = plan.values;
		currentHeldMask = heldMask;
		if (plan.comboBoosted)
			Debug.Log($"[DiceRoll] ComboFortune boosted: reason=\"{plan.boostReason}\"");

		// 비홀드 주사위만 무한 회전 시작 (멈춤은 유저 입력).
		// 각 주사위의 spin 기준점(homePosition)을 현재 transform 위치(=이전 sort가 맞춰둔 centered slot)로
		// 동기화 → BeginSpin이 기준점 위에서 elevation을 더해 그 자리에서 spin 하게 한다.
		settleCounter = 0;
		for (int i = 0; i < dice.Length; i++)
		{
			if (heldMask[i]) continue;
			Vector3 cur = dice[i].transform.position;
			dice[i].SetSpinAnchor(new Vector3(cur.x, slotCenter.y, cur.z));
			dice[i].BeginSpin(currentPlan[i]);
		}

		// 멈추기 프로필 사전 계산 — 공격 턴에서만 유효.
		// 방어 턴은 족보 유도 없이 전체 동시 정지이므로 빈 프로필을 사용한다.
		if (Mode == TurnMode.Attack)
		{
			int preferredRank = ComboFortune.GetPreferredTargetRank(GameSessionManager.PowerUps);
			currentProfile = ComboProximity.ComputeStopProfile(currentPlan, currentHeldMask, preferredRank);
		}
		else
		{
			currentProfile = DiceStopProfile.CreateEmpty(dice.Length);
		}

		Debug.Log($"[DiceRoll] StartRoll mode={Mode} plan=[{string.Join(",", currentPlan)}] " +
		          $"case={currentProfile.scenario} rank={currentProfile.plannedRank} " +
		          $"stable={MaskToString(currentProfile.stableMask)} " +
		          $"decisive={MaskToString(currentProfile.decisiveMask)}");

		// 강조 연출은 stable 4개가 실제로 멈춘 뒤(StopByProfileRoutine 내부)에서 시작.
		// 여기서는 잔존 상태만 제거.
		ClearEmphasis();

		Phase = RollPhase.Rolling;
		ApplyButtonMode(RollPhase.Rolling);
		holdInteractionEnabled = false;
		// ※ 회전 중에는 무음. "나와라 송"은 IsComboImminent 플래그 setter에서만 재생.

		OnRollStarted?.Invoke();
	}

	void RequestStop()
	{
		if (Phase != RollPhase.Rolling) return;
		Phase = RollPhase.Stopping;
		ApplyButtonMode(RollPhase.Stopping);
		stopRoutine = StartCoroutine(Mode == TurnMode.Defense
			? StopAllAtOnceRoutine()
			: StopByProfileRoutine(currentProfile));
	}

	/// <summary>
	/// 방어 턴 전용 정지 루틴 — 회전 중인 주사위 5개(비홀드)를 동시에 snap 정지.
	/// 족보 유도 로그·"나와라!"·드럼롤 BGM 없음. 홀드는 방어에선 불가하지만 있어도 무시.
	/// </summary>
	IEnumerator StopAllAtOnceRoutine()
	{
		var pending = new List<int>();
		for (int i = 0; i < dice.Length; i++)
		{
			if (currentHeldMask != null && i < currentHeldMask.Length && currentHeldMask[i]) continue;
			if (!dice[i].IsSpinning) continue;
			if (currentPlan != null && i < currentPlan.Length)
				dice[i].RetargetFace(currentPlan[i]);
			dice[i].StopToFace(snapDuration);
			pending.Add(i);
		}

		yield return WaitUntilSettled(pending);
		yield return SortDiceVisuallyRoutine(pending);
		FinalizeRoll();
	}

	IEnumerator StopByProfileRoutine(DiceStopProfile profile)
	{
		var stable   = CollectPending(profile.stableMask);
		var decisive = CollectPending(profile.decisiveMask);

		// Fisher–Yates 셔플로 "어느 쪽이 먼저 멈출지" 긴장감 부여
		for (int i = stable.Count - 1; i > 0; i--)
		{
			int j = Random.Range(0, i + 1);
			(stable[i], stable[j]) = (stable[j], stable[i]);
		}

		// Stable 순차 정지
		for (int k = 0; k < stable.Count; k++)
		{
			if (k > 0) yield return new WaitForSeconds(sequentialStopGap);
			int idx = stable[k];
			if (currentPlan != null && idx < currentPlan.Length)
				dice[idx].RetargetFace(currentPlan[idx]);
			dice[idx].StopToFace(snapDuration);
		}

		// 모든 stable이 실제로 settle(IsSpinning=false)할 때까지 대기.
		// 시간 기반 대기가 아니라 주사위 상태 기반 — snap 애니메이션이 끝나야만 통과.
		// ▶ 이 yield 종료 시점 = 유저가 "4개가 멈춘 화면"을 시각적으로 처음 보는 순간.
		//    여기까지는 "사전 계산된 profile"과 "유저의 시각 타이밍"이 분리되어 있다.
		yield return WaitUntilSettled(stable);

		// 멈춘 주사위를 오름차순으로 시각적 재배치
		yield return SortDiceVisuallyRoutine(stable);

		// Case C 또는 decisive 없음 → 바로 마무리
		if (!profile.showComeOut || decisive.Count == 0)
		{
			FinalizeRoll();
			yield break;
		}

		// Case A (이미 5구성 족보 완성): "n개의 눈만 남은 상태"가 아니므로 나와라! 프롬프트 없음.
		// 드라마 flicker를 곧바로 실행해 마무리한다.
		if (profile.scenario == DiceStopCase.AlreadyCombo)
		{
			Phase = RollPhase.Finalizing;
			ApplyButtonMode(RollPhase.Finalizing);
			stopRoutine = StartCoroutine(FlickerDecisiveRoutine());
			yield break;
		}

		// Case B (OneAway): 여기가 바로 "유저가 n개의 눈만 남은 상태를 시각적으로 보기 시작한" 순간.
		// 타이밍 플래그를 올리면 OnComboImminentChanged가 노래와 "나와라!" 버튼 스타일을 켠다.
		if (ComboProximity.ShouldEmphasize(profile, currentPlan))
			ApplyEmphasis(profile.decisiveMask);

		Phase = RollPhase.ComeOutPending;
		IsComboImminent = true;

		OnComeOutStarted?.Invoke(profile, currentPlan, currentHeldMask);

		stopRoutine = null; // 이후는 AcceptComeOut이 새 코루틴 시작
	}

	void AcceptComeOut()
	{
		if (Phase != RollPhase.ComeOutPending) return;
		// 플래그 하강 → OnComboImminentChanged가 노래 정지.
		IsComboImminent = false;
		Phase = RollPhase.Finalizing;
		ApplyButtonMode(RollPhase.Finalizing);
		stopRoutine = StartCoroutine(FlickerDecisiveRoutine());
	}

	IEnumerator FlickerDecisiveRoutine()
	{
		var decisive = CollectPending(currentProfile.decisiveMask);

		// 파워업 보정 — decisive 외 전부를 잠가 1장 이내 교체 강제
		if (currentProfile.applyBoost && currentPlan != null)
		{
			bool[] boostLock = new bool[currentPlan.Length];
			for (int i = 0; i < boostLock.Length; i++)
			{
				bool isDecisive = currentProfile.decisiveMask != null
				               && i < currentProfile.decisiveMask.Length
				               && currentProfile.decisiveMask[i];
				boostLock[i] = !isDecisive;
			}

			var plan = ComboFortune.Apply(currentPlan, boostLock, GameSessionManager.PowerUps);
			plan = ComboFortune.TryBoost(plan, boostLock, GameSessionManager.PowerUps);
			currentPlan = plan.values;
			Debug.Log($"[DiceRoll] 나와라! boosted={plan.comboBoosted} reason=\"{plan.boostReason}\" " +
			          $"plan=[{string.Join(",", currentPlan)}]");
		}

		// Decisive 주사위 동시 플리커
		for (int k = 0; k < decisive.Count; k++)
		{
			int idx = decisive[k];
			if (currentPlan != null && idx < currentPlan.Length)
				dice[idx].RetargetFace(currentPlan[idx]);
			dice[idx].FlickerStop(flickerDuration);
		}

		yield return WaitUntilSettled(decisive);
		FinalizeRoll();
	}

	void FinalizeRoll()
	{
		CancelStopRoutine();
		IsComboImminent = false;
		DiceDrumRollAudio.Stop();
		ClearEmphasis();

		Phase = RollPhase.Idle;
		ApplyButtonMode(RollPhase.Idle);
		holdInteractionEnabled = true;

		Debug.Log($"[DiceRoll] Settled values=[{string.Join(",", ReadFinalValues())}] rollsRemaining={RollsRemaining}");
		OnRollSettled?.Invoke();

		// 비홀드 주사위를 slotCenter 중심으로 face 오름차순 centered 정렬 (fire-and-forget)
		StartCoroutine(SortNonHeldCenteredRoutine());
	}

	IEnumerator WaitUntilSettled(List<int> indices)
	{
		// 단순 폴링 — IsSpinning false가 될 때까지.
		while (true)
		{
			bool anyRolling = false;
			for (int k = 0; k < indices.Count; k++)
			{
				if (dice[indices[k]].IsSpinning) { anyRolling = true; break; }
			}
			if (!anyRolling) break;
			yield return null;
		}
	}

	// ─────────────────────────────────────────────────────
	// Hold / click
	// ─────────────────────────────────────────────────────

	void HandleDieClicked(Dice die)
	{
		if (!holdInteractionEnabled) return;
		if (Phase != RollPhase.Idle) return;
		if (AnyDiceSpinning()) return;

		int idx = System.Array.IndexOf(dice, die);
		if (idx < 0) return;

		if (!die.IsHeld)
		{
			heldOrder.Add(idx);
			die.SetHeld(true, VaultSlotPosition(heldOrder.Count - 1));
			die.SetVisible(false);
		}
		else
		{
			heldOrder.Remove(idx);
			// 되돌아올 슬롯은 "이 주사위를 포함한 face 정렬" 기준으로 계산.
			// y는 다른 비홀드 주사위의 현재 y(=elevated)에 맞춘다 — 없으면 slotCenter.y.
			Vector3 target = ComputeFaceSortedSlotFor(idx, treatAsNonHeld: true);
			target.y = SampleNonHeldRestingY(exceptIdx: idx, fallback: slotCenter.y);
			die.SetHeld(false, target);
			die.SetVisible(true);
			RearrangeVault();
		}

		// 홀드/해제 시마다 비홀드 주사위를 slotCenter 기준 centered 배치로 재정렬.
		// 홀드: 빈 자리 없이 N-1 개가 대칭 배치. 해제: 되돌아온 주사위를 끼워 넣으며 정렬.
		StartCoroutine(SortNonHeldCenteredRoutine());

		OnHoldChanged?.Invoke();
		RefreshHeldDisplay();
	}

	void HandleHoverEnter(Dice die) { die.SetHovered(true); }
	void HandleHoverExit (Dice die) { die.SetHovered(false); }

	void RearrangeVault()
	{
		for (int slot = 0; slot < heldOrder.Count; slot++)
		{
			int idx = heldOrder[slot];
			dice[idx].transform.position = VaultSlotPosition(slot);
		}
	}

	// ── 저장 슬롯 스프라이트 버튼 콜백 ──────────────────────────────
	// 각 슬롯 버튼이 PersistentListener로 연결. slot 인덱스 → 실제 주사위 해제.

	public void UnholdSlot(int slot)
	{
		if (!holdInteractionEnabled) return;
		if (Phase != RollPhase.Idle) return;
		if (slot < 0 || slot >= heldOrder.Count) return;

		int idx = heldOrder[slot];
		heldOrder.RemoveAt(slot);
		Vector3 target = ComputeFaceSortedSlotFor(idx, treatAsNonHeld: true);
		target.y = SampleNonHeldRestingY(exceptIdx: idx, fallback: slotCenter.y);
		dice[idx].SetHeld(false, target);
		dice[idx].SetVisible(true);
		RearrangeVault();

		// 되돌아온 주사위를 비홀드 집합에 끼워 넣으며 centered 배치로 재정렬.
		StartCoroutine(SortNonHeldCenteredRoutine());

		OnHoldChanged?.Invoke();
		RefreshHeldDisplay();
	}

	public void UnholdSlot0() => UnholdSlot(0);
	public void UnholdSlot1() => UnholdSlot(1);
	public void UnholdSlot2() => UnholdSlot(2);
	public void UnholdSlot3() => UnholdSlot(3);
	public void UnholdSlot4() => UnholdSlot(4);

	void RefreshHeldDisplay()
	{
		if (heldDiceImages == null) return;
		for (int slot = 0; slot < heldDiceImages.Length; slot++)
		{
			var img = heldDiceImages[slot];
			if (img == null) continue;
			if (slot < heldOrder.Count && diceFaceSprites != null)
			{
				int value = dice[heldOrder[slot]].Result;
				int sprIdx = Mathf.Clamp(value - 1, 0, diceFaceSprites.Length - 1);
				img.sprite  = diceFaceSprites[sprIdx];
				img.enabled = true;
			}
			else
			{
				img.sprite  = null;
				img.enabled = false;
			}
		}
	}

	Vector3 VaultSlotPosition(int slot)
	{
		return new Vector3(vaultCenter.x, vaultCenter.y, vaultCenter.z + (1.7f - slot * 0.85f));
	}

	// ─────────────────────────────────────────────────────
	// Visual sort (오름차순 재배치)
	// ─────────────────────────────────────────────────────

	/// <summary>
	/// 정지한 `indices` 주사위들을 현재 자기들의 x 슬롯 내에서 face 오름차순으로 재배치.
	/// 사용 시점: StopByProfileRoutine의 stable 중간 정렬 — decisive가 여전히 spin 중이므로
	/// 비홀드 full set centered 재정렬을 하면 decisive 슬롯과 충돌한다.
	/// 따라서 stable이 이미 차지한 x 위치들만 face 값으로 swap한다.
	/// </summary>
	IEnumerator SortDiceVisuallyRoutine(List<int> indices)
	{
		if (indices == null || indices.Count <= 1) yield break;

		// 현재 x 좌표 오름차순으로 슬롯 확정 (stable의 자체 위치만 사용).
		var slots = new List<int>(indices);
		slots.Sort((a, b) => dice[a].transform.position.x.CompareTo(dice[b].transform.position.x));
		float elevatedY = dice[slots[0]].transform.position.y;
		var slotPositions = new Vector3[slots.Count];
		for (int i = 0; i < slots.Count; i++)
		{
			Vector3 p = dice[slots[i]].transform.position;
			slotPositions[i] = new Vector3(p.x, elevatedY, p.z);
		}

		// face 값 오름차순 (stable sort — 동일 값은 기존 순서 유지).
		var sorted = new List<int>(slots);
		sorted.Sort((a, b) => dice[a].Result.CompareTo(dice[b].Result));

		// 이미 정렬 상태면 생략.
		bool alreadySorted = true;
		for (int i = 0; i < sorted.Count; i++)
		{
			if (sorted[i] != slots[i]) { alreadySorted = false; break; }
		}
		if (alreadySorted) yield break;

		for (int i = 0; i < sorted.Count; i++)
			dice[sorted[i]].SlideTo(slotPositions[i], sortSlideDuration);

		yield return new WaitForSeconds(sortSlideDuration);
	}

	/// <summary>
	/// 모든 비홀드·정지 상태 주사위를 slotCenter 기준 centered 배치로 face 오름차순 재정렬.
	/// N=dice.Length → [-2.8,-1.4,0,1.4,2.8]  N=4 → [-2.1,-0.7,0.7,2.1]  N=3 → [-1.4,0,1.4] ...
	/// 짝수/홀수 개수 모두 slotCenter 기준 대칭 배치. 홀드된 주사위의 원래 자리는 비워두지 않는다.
	/// FinalizeRoll 직후와 홀드/언홀드 이벤트에서 공통으로 사용.
	/// 동시에 각 주사위의 spin 기준점도 해당 슬롯(elevation 제외)으로 갱신한다.
	/// </summary>
	IEnumerator SortNonHeldCenteredRoutine()
	{
		if (dice == null) yield break;

		var indices = new List<int>();
		for (int i = 0; i < dice.Length; i++)
		{
			if (dice[i] == null) continue;
			if (dice[i].IsHeld) continue;
			if (dice[i].IsSpinning) continue;
			indices.Add(i);
		}
		if (indices.Count == 0) yield break;

		// face 값 오름차순 (stable).
		var sorted = new List<int>(indices);
		sorted.Sort((a, b) => dice[a].Result.CompareTo(dice[b].Result));

		float elevatedY = dice[sorted[0]].transform.position.y;

		var targets = new Vector3[sorted.Count];
		bool allAtTarget = true;
		for (int i = 0; i < sorted.Count; i++)
		{
			Vector3 slot = GetSlotPosition(i, sorted.Count);
			targets[i] = new Vector3(slot.x, elevatedY, slot.z);
			// spin 기준점 갱신 — 다음 굴림이 이 슬롯에서 시작하도록.
			dice[sorted[i]].SetSpinAnchor(slot);
			if ((dice[sorted[i]].transform.position - targets[i]).sqrMagnitude > 0.0001f)
				allAtTarget = false;
		}
		if (allAtTarget) yield break;

		for (int i = 0; i < sorted.Count; i++)
			dice[sorted[i]].SlideTo(targets[i], sortSlideDuration);

		yield return new WaitForSeconds(sortSlideDuration);
	}

	/// <summary>특정 die를 제외한 비홀드·정지 주사위의 현재 y를 샘플. 없으면 fallback.</summary>
	float SampleNonHeldRestingY(int exceptIdx, float fallback)
	{
		if (dice == null) return fallback;
		for (int i = 0; i < dice.Length; i++)
		{
			if (i == exceptIdx) continue;
			if (dice[i] == null) continue;
			if (dice[i].IsHeld) continue;
			if (dice[i].IsSpinning) continue;
			return dice[i].transform.position.y;
		}
		return fallback;
	}

	// ─────────────────────────────────────────────────────
	// Settle tracking
	// ─────────────────────────────────────────────────────

	void HandleDieSettled(Dice die)
	{
		settleCounter++;
	}

	List<int> CollectPending(bool[] mask)
	{
		var list = new List<int>();
		if (mask == null) return list;
		for (int i = 0; i < dice.Length && i < mask.Length; i++)
		{
			if (!mask[i]) continue;
			if (currentHeldMask != null && i < currentHeldMask.Length && currentHeldMask[i]) continue;
			if (!dice[i].IsSpinning) continue;
			list.Add(i);
		}
		return list;
	}

	// ─────────────────────────────────────────────────────
	// Button mode / visuals
	// ─────────────────────────────────────────────────────

	void ApplyButtonMode(RollPhase phase)
	{
		if (rollButton == null) return;

		// Phase만 담당 — 라벨/색상 기본값. "나와라!" 전환은 IsComboImminent 플래그가 별도로 오버레이.
		switch (phase)
		{
			case RollPhase.Idle:
				if (rollButtonColorsCached) rollButton.colors = rollButtonDefaultColors;
				SetLabel("굴리기");
				rollButton.interactable = RollsRemaining > 0;
				break;
			case RollPhase.Rolling:
				if (rollButtonColorsCached) rollButton.colors = rollButtonDefaultColors;
				SetLabel("멈추기");
				rollButton.interactable = true;
				break;
			case RollPhase.Stopping:
			case RollPhase.Finalizing:
				rollButton.interactable = false;
				break;
			case RollPhase.ComeOutPending:
				// 기본 phase 스타일은 비활성/기본색 — 실제 "나와라!" 표시는 IsComboImminent 플래그가 담당.
				rollButton.interactable = false;
				break;
		}
	}

	/// <summary>
	/// IsComboImminent 플래그 전이 시 호출 — 노래 시작/정지와 "나와라!" 버튼 스타일을 동기화.
	/// StopByProfileRoutine이 stable 주사위의 실제 settle을 확인한 뒤에만 true로 올리므로,
	/// 이 콜백이 발동되는 시점 = 유저가 "n개의 눈만 남은 상태"를 화면에서 실제로 보는 순간과 일치한다.
	/// </summary>
	void OnComboImminentChanged(bool on)
	{
		Debug.Log($"[DiceRoll] IsComboImminent → {on} (Phase={Phase})");

		if (on)
		{
			// 단일 경로: AudioManager의 drumRollSource + DiceRoll_WakuWaku.wav 를 Play.
			DiceDrumRollAudio.Play();
			if (rollButton != null)
			{
				var cb = rollButton.colors;
				cb.normalColor      = ComeOutNormalColor;
				cb.highlightedColor = ComeOutHighlightColor;
				cb.pressedColor     = ComeOutPressedColor;
				cb.selectedColor    = ComeOutHighlightColor;
				rollButton.colors = cb;
				SetLabel("나와라!");
				rollButton.interactable = true;
			}
		}
		else
		{
			DiceDrumRollAudio.Stop();
			// 색/라벨 복원은 뒤따르는 ApplyButtonMode(Finalizing/Idle) 호출이 수행.
		}
	}

	void SetLabel(string text)
	{
		if (rollButtonLabel != null) rollButtonLabel.text = text;
	}

	void ApplyEmphasis(bool[] mask)
	{
		if (mask == null || dice == null) return;
		for (int i = 0; i < dice.Length; i++)
		{
			if (currentHeldMask != null && i < currentHeldMask.Length && currentHeldMask[i])
				continue;
			bool on = i < mask.Length && mask[i];
			dice[i].SetEmphasis(on);
		}
	}

	void ClearEmphasis()
	{
		if (dice == null) return;
		for (int i = 0; i < dice.Length; i++)
			if (dice[i] != null) dice[i].SetEmphasis(false);
	}

	void CancelStopRoutine()
	{
		if (stopRoutine != null)
		{
			StopCoroutine(stopRoutine);
			stopRoutine = null;
		}
	}

	static string MaskToString(bool[] mask)
	{
		if (mask == null) return "null";
		var sb = new System.Text.StringBuilder(mask.Length);
		for (int i = 0; i < mask.Length; i++) sb.Append(mask[i] ? '1' : '0');
		return sb.ToString();
	}
}
