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
			case "/stage":
				return HandleStage(parts);
			case "/nextround":
				return HandleNextRound();
			case "/help":
				return HandleHelp();
			default:
				return $"알 수 없는 명령: {cmd}\n/help 를 입력하세요.";
		}
	}

	static string HandleStage(string[] parts)
	{
		if (parts.Length < 2)
		{
			var sb = new System.Text.StringBuilder();
			sb.AppendLine("사용법: /stage <index | name | id>");
			sb.AppendLine("등록된 스테이지:");
			int i = 0;
			foreach (var id in StageRegistry.AllIds)
			{
				var s = StageRegistry.Get(id);
				int underscore = id.IndexOf('_');
				string namePart = underscore >= 0 ? id.Substring(0, underscore) : id;
				string mark = id == GameSessionManager.CurrentStageId ? " ← 현재" : "";
				sb.AppendLine($"  [{i}] {namePart}  (id={id}, {s?.displayName}){mark}");
				i++;
			}
			return sb.ToString().TrimEnd();
		}

		string arg = parts[1];
		string targetId = ResolveStageId(arg, out string resolveError);
		if (targetId == null)
			return $"[오류] {resolveError}\n/stage (인자 없음)으로 목록 확인";
		var stage = StageRegistry.Get(targetId);

		// 현재 전투(있다면)를 취소하고 해당 스테이지 1라운드로 이동
		GameSessionManager.CurrentStageId    = targetId;
		GameSessionManager.CurrentEventIndex = 0;
		GameSessionManager.BattleEnemies.Clear();
		GameSessionManager.IsBossBattle      = false;
		GameSessionManager.LastBattleResult  = BattleResult.None;

		Debug.Log($"[Debug] /stage → {targetId} ({stage.displayName}) 1라운드로 이동");

		// Explore 씬으로 전환 — 현재 전투 씬이어도 강제로 나감 (= 전투 종료)
		UnityEngine.SceneManagement.SceneManager.LoadScene("GameExploreScene");
		return $"스테이지 전환: {stage.displayName} ({targetId}) — 1라운드 시작";
	}

	/// <summary>
	/// /stage 인자를 스테이지 ID로 해석. 1) 정확한 ID, 2) 등록 순서 인덱스(0-base),
	/// 3) ID의 밑줄 앞 이름 대소문자 무시 매칭 순으로 시도. 모호하면 null + 에러 메시지.
	/// </summary>
	static string ResolveStageId(string arg, out string error)
	{
		error = null;

		// 1) 정확한 ID
		if (StageRegistry.Get(arg) != null)
			return arg;

		// 2) 숫자 인덱스 (등록 순서, 0-base)
		if (int.TryParse(arg, out int idx))
		{
			var ids = new List<string>(StageRegistry.AllIds);
			if (idx < 0 || idx >= ids.Count)
			{
				error = $"스테이지 인덱스 범위 초과: {idx} (0 ~ {ids.Count - 1})";
				return null;
			}
			return ids[idx];
		}

		// 3) 이름 매칭 — ID의 밑줄 앞부분을 대소문자 무시로 비교
		string lower = arg.ToLowerInvariant();
		var matches = new List<string>();
		foreach (var id in StageRegistry.AllIds)
		{
			int underscore = id.IndexOf('_');
			string namePart = underscore >= 0 ? id.Substring(0, underscore) : id;
			if (namePart.ToLowerInvariant() == lower)
				matches.Add(id);
		}
		if (matches.Count == 1) return matches[0];
		if (matches.Count > 1)
		{
			error = $"이름 '{arg}' 이(가) 여러 스테이지에 매칭: {string.Join(", ", matches)}";
			return null;
		}

		error = $"알 수 없는 스테이지: {arg}";
		return null;
	}

	static string HandleSetDice(string[] parts)
	{
		var dice = Object.FindFirstObjectByType<BattleSceneController>();
		if (dice == null)
			return "[오류] /setdice는 주사위 전투 씬에서만 사용 가능합니다.";

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

		return dice.DebugSetDice(values);
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

	static string HandleNextRound()
	{
		var stage = GameSessionManager.CurrentStage;
		if (stage == null || stage.rounds == null || stage.rounds.Count == 0)
			return "[오류] 활성 스테이지에 라운드가 없습니다.";

		int idx = GameSessionManager.CurrentEventIndex;
		if (idx < 0 || idx >= stage.rounds.Count)
			return "[오류] 이미 스테이지의 모든 라운드를 완료했습니다.";

		var round = stage.rounds[idx];
		if (round == StageRoundType.ItemBox)
			return "[거부] 파워업 라운드는 /nextround로 건너뛸 수 없습니다. 직접 선택하세요.";

		// 현재 라운드를 "정상 클리어"한 것으로 처리 — Explore 씬의 Start가 BattleResult.Won 분기에서
		// CurrentEventIndex를 증가시키고 StartWalking으로 자연스럽게 다음 이벤트를 트리거한다.
		GameSessionManager.BattleEnemies.Clear();
		GameSessionManager.IsBossBattle     = false;
		GameSessionManager.LastBattleResult = BattleResult.Won;

		Debug.Log($"[Debug] /nextround stage={stage.id} round={idx}({round}) → 다음 라운드로 강제 진행");

		UnityEngine.SceneManagement.SceneManager.LoadScene("GameExploreScene");
		return $"라운드 {idx + 1} ({round}) 강제 클리어 → 다음 라운드로";
	}

	static string HandleHelp()
	{
		return
			"=== 디버그 명령 목록 ===\n" +
			"/setdice 1 2 3 4 5  — 주사위 결과 강제 (1~6, 5개)\n" +
			"/kill player        — 플레이어 즉사\n" +
			"/kill mob @a        — 모든 적 즉사\n" +
			"/kill mob 0 1 2     — 지정 인덱스 적 즉사\n" +
			"/stage              — 등록된 스테이지 목록\n" +
			"/stage <index|name|id>  — 해당 스테이지 1라운드로 이동 (예: /stage 0, /stage forest, /stage forest_1)\n" +
			"/nextround          — 현재 라운드를 정상 클리어한 것으로 처리해 다음 라운드로\n" +
			"                      (파워업 라운드는 불가 — 직접 선택해야 함)\n" +
			"/help               — 이 도움말\n" +
			"※ 전투 명령은 전투 씬에서만 동작합니다.";
	}

	static IBattleDebugTarget FindBattle()
	{
		var dice = Object.FindFirstObjectByType<BattleSceneController>();
		if (dice != null) return dice;
		var mah = Object.FindFirstObjectByType<Mahjong.MahjongBattleController>();
		if (mah != null) return mah;
		return null;
	}
}
