using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

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
			GameSessionManager.CancelBattle();

			GameSessionManager.StartNewGame(CharacterType.Mahjong);

			Assert.AreEqual(CharacterType.Mahjong, GameSessionManager.SelectedCharacter);
			Assert.AreEqual(0, GameSessionManager.CurrentEventIndex);
			Assert.AreEqual(0, GameSessionManager.BattleEnemyCount);
			Assert.AreEqual(0, GameSessionManager.PowerUps.Count);
			Assert.AreEqual(BattleResult.None, GameSessionManager.LastBattleResult);
			Assert.IsFalse(GameSessionManager.IsBossBattle);
		}
	}
}
