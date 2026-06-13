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
	public static int ExploreMapSeed;
	public static string CurrentExploreMapNodeId = "";
	public static string PendingExploreMapNodeId = "";

	// ── 스테이지 ──
	// 런타임에 이 값을 바꿔 활성 스테이지를 전환. StartNewGame에서 기본값으로 초기화.
	public static string CurrentStageId = Stage1Forest.Id;
	public static StageData CurrentStage => StageRegistry.Get(CurrentStageId) ?? StageRegistry.DefaultStage;

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
		ResetExploreMapRoute();
		ClearBattleEnemies();
		LastBattleResult = BattleResult.None;
		IsBossBattle = false;
		CurrentStageId = Stage1Forest.Id;
		Debug.Log($"[Session] StartNewGame character={character} hearts={PlayerHearts.TotalHalfHearts} stage={CurrentStageId}");
	}

	public static void ResetExploreMapRoute()
	{
		ExploreMapSeed = GenerateExploreMapSeed();
		CurrentExploreMapNodeId = "";
		PendingExploreMapNodeId = "";
	}

	static int GenerateExploreMapSeed()
	{
		int seed = System.Guid.NewGuid().GetHashCode();
		return seed != 0 ? seed : 1;
	}

	public static bool HasPowerUp(PowerUpType type)
	{
		return PowerUps.Contains(type);
	}

	public static void RemovePowerUp(PowerUpType type)
	{
		PowerUps.Remove(type);
	}

	public static bool AddPowerUp(PowerUpType type)
	{
		if (PowerUps.Contains(type))
			return false;
		PowerUps.Add(type);
		return true;
	}

	public static bool HasBattleEnemies => BattleEnemies.Count > 0;

	public static int BattleEnemyCount => BattleEnemies.Count;

	public static void ClearBattleEnemies()
	{
		BattleEnemies.Clear();
	}

	public static void PrepareBattleEnemies(IEnumerable<EnemyInfo> source, bool isBossBattle)
	{
		BattleEnemies.Clear();
		if (source != null)
		{
			foreach (var enemy in source)
			{
				if (enemy == null)
					continue;
				BattleEnemies.Add(enemy.Clone());
			}
		}
		IsBossBattle = isBossBattle;
	}

	public static void PrepareBattleEnemy(EnemyInfo enemy, bool isBossBattle)
	{
		BattleEnemies.Clear();
		if (enemy != null)
			BattleEnemies.Add(enemy.Clone());
		IsBossBattle = isBossBattle;
	}

	public static List<EnemyInfo> SnapshotBattleEnemies()
	{
		var snapshot = new List<EnemyInfo>(BattleEnemies.Count);
		foreach (var enemy in BattleEnemies)
		{
			if (enemy != null)
				snapshot.Add(enemy.Clone());
		}
		return snapshot;
	}

	public static void CompleteBattleWon()
	{
		LastBattleResult = BattleResult.Won;
		ClearBattleEnemies();
		IsBossBattle = false;
	}

	public static void CancelBattle()
	{
		LastBattleResult = BattleResult.Cancelled;
	}

	public static void ResetBattleResult()
	{
		LastBattleResult = BattleResult.None;
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
