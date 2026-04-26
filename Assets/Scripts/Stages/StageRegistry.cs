using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 모든 스테이지 정의의 중앙 레지스트리.
/// 새 스테이지를 만들려면 Assets/Scripts/Stages/ 아래에 static Build() 메서드를 가진
/// Stage* 클래스를 추가하고 EnsureInitialized 안에서 Register를 호출한다.
/// </summary>
public static class StageRegistry
{
	static readonly Dictionary<string, StageData> stages    = new Dictionary<string, StageData>();
	static readonly List<string>                   orderedIds = new List<string>();
	static bool initialized;

	static void EnsureInitialized()
	{
		if (initialized) return;
		initialized = true;

		// 등록 순서 = 게임 진행 순서의 기본값
		Register(Stage1Forest.Build());
		Register(Stage2Cave.Build());
	}

	public static void Register(StageData stage)
	{
		if (stage == null || string.IsNullOrEmpty(stage.id))
		{
			Debug.LogWarning("[StageRegistry] id가 없는 스테이지는 등록할 수 없습니다.");
			return;
		}
		if (!stages.ContainsKey(stage.id))
			orderedIds.Add(stage.id);
		stages[stage.id] = stage;
	}

	public static StageData Get(string id)
	{
		EnsureInitialized();
		if (string.IsNullOrEmpty(id)) return null;
		return stages.TryGetValue(id, out var s) ? s : null;
	}

	public static IReadOnlyList<string> AllIds
	{
		get { EnsureInitialized(); return orderedIds; }
	}

	public static IEnumerable<StageData> AllStages
	{
		get
		{
			EnsureInitialized();
			for (int i = 0; i < orderedIds.Count; i++)
				yield return stages[orderedIds[i]];
		}
	}

	public static StageData DefaultStage
	{
		get
		{
			EnsureInitialized();
			return orderedIds.Count > 0 ? stages[orderedIds[0]] : null;
		}
	}
}
