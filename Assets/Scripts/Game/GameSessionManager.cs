using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 게임 전반에서 유지되는 정적 매니저.
/// 캐릭터 선택, 플레이어 체력(하트), 파워업, 전투 컨텍스트를 관리한다.
/// </summary>
public static class GameSessionManager
{
	public static CharacterType SelectedCharacter;
	public static HeartContainer PlayerHearts = new HeartContainer();
	public static List<PowerUpType> PowerUps = new List<PowerUpType>();

	public static int CurrentEventIndex;

	// ── 전투 컨텍스트 (전투 씬 진입 전 설정) ──
	public static List<EnemyInfo> BattleEnemies = new List<EnemyInfo>();
	public static bool IsBossBattle;

	// ── 전투 결과 (전투 씬이 설정, 탐험 씬이 읽음) ──
	public static BattleResult LastBattleResult = BattleResult.None;

	// 보스 HP (적 체력이므로 하트와 무관)
	public static int BossHp = 120;

	public static bool IsPlayerAlive => PlayerHearts.IsAlive;

	public static void StartNewGame(CharacterType character)
	{
		SelectedCharacter = character;
		PlayerHearts.Reset();
		PowerUps.Clear();
		CurrentEventIndex = 0;
		BattleEnemies.Clear();
		LastBattleResult = BattleResult.None;
		IsBossBattle = false;
		Debug.Log($"[Session] StartNewGame character={character} hearts={PlayerHearts.TotalHalfHearts}");
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
	/// 플레이어 피해 처리 (반칸 단위). ReviveOnce 패시브가 있으면 치명타 1회 무효화.
	/// </summary>
	public static bool TakePlayerDamage(int halfHearts)
	{
		if (halfHearts >= PlayerHearts.TotalHalfHearts && HasPowerUp(PowerUpType.ReviveOnce))
		{
			Debug.Log($"[Session] ReviveOnce 발동: damage={halfHearts} hearts={PlayerHearts.TotalHalfHearts} → 무효화");
			RemovePowerUp(PowerUpType.ReviveOnce);
			return true;
		}
		int before = PlayerHearts.TotalHalfHearts;
		PlayerHearts.TakeDamage(halfHearts);
		Debug.Log($"[Session] TakePlayerDamage halfHearts={halfHearts} hearts={before}→{PlayerHearts.TotalHalfHearts}");
		return false;
	}
}

public enum BattleResult
{
	None,
	Won,
	Cancelled
}
