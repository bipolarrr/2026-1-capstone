using System.Collections.Generic;

/// <summary>
/// 전투 씬(주사위·마작 등) 공통 디버그 훅. DebugCommandProcessor가 이 인터페이스를 통해 전투 컨트롤러를 조작한다.
/// </summary>
public interface IBattleDebugTarget
{
	string DebugKillPlayer();
	string DebugKillAllEnemies();
	string DebugKillEnemies(int[] indices);
}
