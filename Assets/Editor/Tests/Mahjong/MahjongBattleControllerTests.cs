using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Mahjong;

namespace MahjongTests
{
	public class MahjongBattleControllerTests
	{
		GameObject go;

		[TearDown]
		public void TearDown()
		{
			if (go != null)
				Object.DestroyImmediate(go);
			GameSessionManager.ClearBattleEnemies();
			GameSessionManager.ResetBattleResult();
		}

		[Test]
		public void Victory_ClearsSessionBattleEnemies()
		{
			GameSessionManager.PrepareBattleEnemy(new EnemyInfo("Old Enemy", 10, 1, Color.white), false);
			GameSessionManager.ResetBattleResult();

			go = new GameObject("MahjongBattleControllerTest");
			var controller = go.AddComponent<MahjongBattleController>();
			var localEnemies = new List<EnemyInfo>
			{
				new EnemyInfo("Defeated Enemy", 10, 1, Color.white) { hp = 0 }
			};

			typeof(BattleControllerBase)
				.GetField("enemies", BindingFlags.Instance | BindingFlags.NonPublic)
				.SetValue(controller, localEnemies);

			typeof(MahjongBattleController)
				.GetMethod("CheckVictory", BindingFlags.Instance | BindingFlags.NonPublic)
				.Invoke(controller, null);

			Assert.AreEqual(BattleResult.Won, GameSessionManager.LastBattleResult);
			Assert.AreEqual(0, GameSessionManager.BattleEnemyCount);
		}
	}
}
