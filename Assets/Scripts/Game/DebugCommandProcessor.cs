using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 디버그 콘솔 명령 파싱 및 실행. 전투 명령은 BattleSceneController에 위임한다.
/// </summary>
public static class DebugCommandProcessor
{
	public static string Execute(string input)
	{
		if (string.IsNullOrWhiteSpace(input))
			return "";

		string trimmed = input.Trim();
		if (!trimmed.StartsWith("/"))
			return "명령은 /로 시작해야 합니다. /help 를 입력하세요.";

		string[] parts = trimmed.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
		string cmd = parts[0].ToLower();

		switch (cmd)
		{
			case "/setdice":
				return HandleSetDice(parts);
			case "/kill":
				return HandleKill(parts);
			case "/help":
				return HandleHelp();
			default:
				return $"알 수 없는 명령: {cmd}\n/help 를 입력하세요.";
		}
	}

	static string HandleSetDice(string[] parts)
	{
		var battle = FindBattle();
		if (battle == null)
			return "[오류] 전투 씬에서만 사용 가능합니다.";

		if (parts.Length != 6)
			return "[오류] /setdice 는 5개의 숫자가 필요합니다.\n사용법: /setdice 1 2 3 4 5";

		int[] values = new int[5];
		for (int i = 0; i < 5; i++)
		{
			if (!int.TryParse(parts[i + 1], out int v))
				return $"[오류] '{parts[i + 1]}'은(는) 유효한 숫자가 아닙니다.";
			if (v < 1 || v > 6)
				return $"[오류] 주사위 값은 1~6 범위여야 합니다. (입력: {v})";
			values[i] = v;
		}

		return battle.DebugSetDice(values);
	}

	static string HandleKill(string[] parts)
	{
		if (parts.Length < 2)
			return "[오류] /kill 뒤에 대상을 지정하세요.\n사용법: /kill player | /kill mob @a | /kill mob 0 1 2";

		string target = parts[1].ToLower();

		switch (target)
		{
			case "player":
				return HandleKillPlayer();
			case "mob":
				return HandleKillMob(parts);
			default:
				return $"[오류] 알 수 없는 대상: {target}\n사용법: /kill player | /kill mob @a | /kill mob 0 1 2";
		}
	}

	static string HandleKillPlayer()
	{
		var battle = FindBattle();
		if (battle == null)
			return "[오류] 전투 씬에서만 사용 가능합니다.";
		return battle.DebugKillPlayer();
	}

	static string HandleKillMob(string[] parts)
	{
		var battle = FindBattle();
		if (battle == null)
			return "[오류] 전투 씬에서만 사용 가능합니다.";

		if (parts.Length < 3)
			return "[오류] /kill mob 뒤에 인덱스 또는 @a를 지정하세요.\n사용법: /kill mob @a | /kill mob 0 1 2";

		if (parts[2].ToLower() == "@a")
			return battle.DebugKillAllEnemies();

		var indices = new List<int>();
		for (int i = 2; i < parts.Length; i++)
		{
			if (!int.TryParse(parts[i], out int idx))
				return $"[오류] '{parts[i]}'은(는) 유효한 인덱스가 아닙니다.";
			indices.Add(idx);
		}

		return battle.DebugKillEnemies(indices.ToArray());
	}

	static string HandleHelp()
	{
		return
			"=== 디버그 명령 목록 ===\n" +
			"/setdice 1 2 3 4 5  — 주사위 결과 강제 (1~6, 5개)\n" +
			"/kill player        — 플레이어 즉사\n" +
			"/kill mob @a        — 모든 적 즉사\n" +
			"/kill mob 0 1 2     — 지정 인덱스 적 즉사\n" +
			"/help               — 이 도움말\n" +
			"※ 전투 명령은 전투 씬에서만 동작합니다.";
	}

	static BattleSceneController FindBattle()
	{
		return Object.FindFirstObjectByType<BattleSceneController>();
	}
}
