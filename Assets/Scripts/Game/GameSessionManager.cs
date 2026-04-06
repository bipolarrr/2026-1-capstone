using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전반에서 유지되는 정적 매니저.
/// 캐릭터 선택, 플레이어 체력, 파워업, 전투 컨텍스트를 관리한다.
/// </summary>
public static class GameSessionManager
{
	public static CharacterType SelectedCharacter;
	public static int PlayerMaxHp = 25;
	public static int PlayerHp;
	public static List<PowerUpType> PowerUps = new List<PowerUpType>();

	public static int CurrentEventIndex;

	// ── 전투 컨텍스트 (전투 씬 진입 전 설정) ──
	public static List<EnemyInfo> BattleEnemies = new List<EnemyInfo>();
	public static bool IsBossBattle;

	// ── 전투 결과 (전투 씬이 설정, 탐험 씬이 읽음) ──
	public static BattleResult LastBattleResult = BattleResult.None;

	// 보스: ATK 6, HP 120 → 25÷6 ≈ 4라운드 생존, 120÷25(콤보 평균) ≈ 5라운드 필요
	// ReviveOnce로 +3~4라운드 → 생각하면 클리어 가능
	public static int BossHp = 120;

	public static void StartNewGame(CharacterType character)
	{
		SelectedCharacter = character;
		PlayerHp = PlayerMaxHp;
		PowerUps.Clear();
		CurrentEventIndex = 0;
		BattleEnemies.Clear();
		LastBattleResult = BattleResult.None;
		IsBossBattle = false;
		Debug.Log($"[Session] StartNewGame character={character} hp={PlayerHp}");
	}

	public static bool HasPowerUp(PowerUpType type)
	{
		return PowerUps.Contains(type);
	}

	public static void RemovePowerUp(PowerUpType type)
	{
		PowerUps.Remove(type);
	}

	/// <summary>
	/// 플레이어 피해 처리. ReviveOnce 패시브가 있으면 치명타 1회 무효화.
	/// </summary>
	public static bool TakePlayerDamage(int damage)
	{
		if (damage >= PlayerHp && HasPowerUp(PowerUpType.ReviveOnce))
		{
			Debug.Log($"[Session] ReviveOnce 발동: damage={damage} hp={PlayerHp} → ���효화");
			RemovePowerUp(PowerUpType.ReviveOnce);
			return true;
		}
		int before = PlayerHp;
		PlayerHp = Mathf.Max(0, PlayerHp - damage);
		Debug.Log($"[Session] TakePlayerDamage damage={damage} hp={before}→{PlayerHp}");
		return false;
	}
}

public enum BattleResult
{
	None,
	Won,
	Cancelled
}
