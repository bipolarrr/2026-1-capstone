using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class SceneBuilderIncrementalBuild
{
	public const int ManifestVersion = 1;
	const string ManifestPath = "Library/SceneBuilderIncremental/manifest.json";
	const string SharedUtilityPath = "Assets/Editor/SceneBuilderUtility.cs";

	public readonly struct SceneBuildTarget
	{
		public readonly string SceneName;
		public readonly string BuilderPath;
		public readonly string OutputScenePath;
		public readonly Func<bool> BuildAction;

		public SceneBuildTarget(string sceneName, string builderPath, string outputScenePath, Func<bool> buildAction = null)
		{
			SceneName = sceneName;
			BuilderPath = builderPath;
			OutputScenePath = outputScenePath;
			BuildAction = buildAction;
		}
	}

	public sealed class SceneBuildChange
	{
		public SceneBuildTarget Target;
		public string Reason;
		public string InputHash;
		public string OutputHash;
	}

	[Serializable]
	public sealed class Manifest
	{
		public int version;
		public string unityVersion;
		public List<ManifestSceneEntry> scenes = new List<ManifestSceneEntry>();
	}

	[Serializable]
	public sealed class ManifestSceneEntry
	{
		public string sceneName;
		public string inputHash;
		public string outputHash;
		public string outputScenePath;
		public string builtAtUtc;
	}

	public static SceneBuildTarget[] DefaultTargets => new[]
	{
		new SceneBuildTarget("MainMenu", "Assets/Editor/MainMenuSceneBuilder.cs", "Assets/Scenes/MainMenu.unity", MainMenuSceneBuilder.BuildForIncremental),
		new SceneBuildTarget("CharacterSelect", "Assets/Editor/CharacterSelectSceneBuilder.cs", "Assets/Scenes/CharacterSelect.unity", CharacterSelectSceneBuilder.BuildForIncremental),
		new SceneBuildTarget("GameExploreScene", "Assets/Editor/GameExploreSceneBuilder.cs", "Assets/Scenes/GameExploreScene.unity", GameExploreSceneBuilder.BuildForIncremental),
		new SceneBuildTarget("DiceBattleScene", "Assets/Editor/DiceBattleSceneBuilder.cs", "Assets/Scenes/DiceBattleScene.unity", DiceBattleSceneBuilder.BuildForIncremental),
		new SceneBuildTarget("MahjongBattleScene", "Assets/Editor/MahjongBattleSceneBuilder.cs", "Assets/Scenes/MahjongBattleScene.unity", MahjongBattleSceneBuilder.BuildForIncremental),
		new SceneBuildTarget("HoldemBattleScene", "Assets/Editor/HoldemBattleSceneBuilder.cs", "Assets/Scenes/HoldemBattleScene.unity", HoldemBattleSceneBuilder.BuildForIncremental),
		new SceneBuildTarget("AnimationDebugScene", "Assets/Editor/AnimationDebugSceneBuilder.cs", AnimationDebugSceneBuilder.ScenePath, AnimationDebugSceneBuilder.BuildForIncremental),
	};

	[MenuItem("Tools/Scene Builders/Report Changed Scenes")]
	public static void ReportChangedScenes()
	{
		var changes = FindChangedScenes(DefaultTargets, ManifestPath, Application.unityVersion, SharedUtilityPath);
		LogChangedScenes(changes);
		if (!Application.isBatchMode)
			EditorUtility.DisplayDialog("Scene Builders", FormatChangedScenes(changes), "확인");
	}

	[MenuItem("Tools/Scene Builders/Build Changed Scenes")]
	public static void BuildChangedScenes()
	{
		BuildChangedScenes(DefaultTargets, ManifestPath, Application.unityVersion, SharedUtilityPath);
	}

	[MenuItem("Tools/Scene Builders/Clear Incremental Cache")]
	public static void ClearIncrementalCache()
	{
		ClearCache(ManifestPath);
		Debug.Log($"[SceneBuilderIncrementalBuild] Cleared cache: {ManifestPath}");
		if (!Application.isBatchMode)
			EditorUtility.DisplayDialog("Scene Builders", "증분 빌드 캐시를 삭제했습니다.", "확인");
	}

	public static List<SceneBuildChange> FindChangedScenes(
		IReadOnlyList<SceneBuildTarget> targets,
		string manifestPath,
		string unityVersion,
		string sharedUtilityPath)
	{
		var manifest = LoadManifest(manifestPath);
		var changes = new List<SceneBuildChange>();
		bool manifestMissing = manifest == null;
		bool manifestMismatch = !manifestMissing &&
			(manifest.version != ManifestVersion || !string.Equals(manifest.unityVersion, unityVersion, StringComparison.Ordinal));

		for (int i = 0; i < targets.Count; i++)
		{
			var target = targets[i];
			string inputHash = ComputeInputHash(target, unityVersion, sharedUtilityPath);
			string outputHash = File.Exists(target.OutputScenePath) ? ComputeFileHash(target.OutputScenePath) : "";
			string reason = null;

			var entry = manifest != null ? FindEntry(manifest, target.SceneName) : null;
			if (manifestMissing)
				reason = "manifest missing";
			else if (manifestMismatch)
				reason = "manifest version or Unity version changed";
			else if (entry == null)
				reason = "scene missing from manifest";
			else if (!File.Exists(target.OutputScenePath))
				reason = "output scene missing";
			else if (!string.Equals(entry.inputHash, inputHash, StringComparison.Ordinal))
				reason = "builder input changed";
			else if (!string.Equals(entry.outputHash, outputHash, StringComparison.Ordinal))
				reason = "output scene drifted";

			if (reason != null)
			{
				changes.Add(new SceneBuildChange
				{
					Target = target,
					Reason = reason,
					InputHash = inputHash,
					OutputHash = outputHash,
				});
			}
		}

		return changes;
	}

	public static bool BuildChangedScenes(
		IReadOnlyList<SceneBuildTarget> targets,
		string manifestPath,
		string unityVersion,
		string sharedUtilityPath,
		bool promptBeforeBuild = true,
		bool showResultDialog = true)
	{
		var changes = FindChangedScenes(targets, manifestPath, unityVersion, sharedUtilityPath);
		if (changes.Count == 0)
		{
			Debug.Log("[SceneBuilderIncrementalBuild] 0 changed scenes.");
			if (showResultDialog && !Application.isBatchMode)
				EditorUtility.DisplayDialog("Scene Builders", "변경된 씬이 없습니다.", "확인");
			return true;
		}

		string message = FormatChangedScenes(changes);
		if (promptBeforeBuild && !Application.isBatchMode)
		{
			bool proceed = EditorUtility.DisplayDialog(
				"Build Changed Scenes",
				message + "\n\n변경된 씬만 다시 생성하시겠습니까?",
				"빌드", "취소");
			if (!proceed)
				return false;
		}

		var manifest = LoadManifest(manifestPath) ?? new Manifest();
		manifest.version = ManifestVersion;
		manifest.unityVersion = unityVersion;
		bool allSucceeded = true;
		int successCount = 0;

		for (int i = 0; i < changes.Count; i++)
		{
			var change = changes[i];
			if (change.Target.BuildAction == null)
			{
				Debug.LogWarning($"[SceneBuilderIncrementalBuild] No build action for {change.Target.SceneName}");
				allSucceeded = false;
				continue;
			}

			Debug.Log($"[SceneBuilderIncrementalBuild] Building {change.Target.SceneName}: {change.Reason}");
			bool saved = false;
			try
			{
				saved = change.Target.BuildAction();
			}
			catch (Exception ex)
			{
				Debug.LogException(ex);
			}

			if (!saved || SceneBuilderUtility.FieldBindingFailureCount > 0 || !File.Exists(change.Target.OutputScenePath))
			{
				Debug.LogWarning($"[SceneBuilderIncrementalBuild] {change.Target.SceneName} build did not update the incremental manifest.");
				allSucceeded = false;
				continue;
			}

			UpsertEntry(manifest, change.Target, unityVersion, sharedUtilityPath);
			successCount++;
		}

		if (successCount > 0)
			SaveManifest(manifestPath, manifest);

		Debug.Log($"[SceneBuilderIncrementalBuild] Built {successCount}/{changes.Count} changed scenes.");
		if (showResultDialog && !Application.isBatchMode)
			EditorUtility.DisplayDialog("Scene Builders", $"빌드 완료: {successCount}/{changes.Count}", "확인");
		return allSucceeded;
	}

	public static void ClearCache(string manifestPath)
	{
		if (File.Exists(manifestPath))
			File.Delete(manifestPath);
	}

	public static void SaveManifest(string manifestPath, Manifest manifest)
	{
		string directory = Path.GetDirectoryName(manifestPath);
		if (!string.IsNullOrEmpty(directory))
			Directory.CreateDirectory(directory);
		File.WriteAllText(manifestPath, JsonUtility.ToJson(manifest, true), Encoding.UTF8);
	}

	public static Manifest LoadManifest(string manifestPath)
	{
		if (!File.Exists(manifestPath))
			return null;

		try
		{
			return JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath, Encoding.UTF8));
		}
		catch (Exception ex)
		{
			Debug.LogWarning($"[SceneBuilderIncrementalBuild] Manifest read failed: {ex.Message}");
			return null;
		}
	}

	public static string ComputeInputHash(SceneBuildTarget target, string unityVersion, string sharedUtilityPath)
	{
		using (var sha = SHA256.Create())
		{
			AppendText(sha, $"manifest:{ManifestVersion}\n");
			AppendText(sha, $"unity:{unityVersion}\n");
			AppendFile(sha, target.BuilderPath);
			AppendFile(sha, sharedUtilityPath);
			sha.TransformFinalBlock(Array.Empty<byte>(), 0, 0);
			return ToHex(sha.Hash);
		}
	}

	public static string ComputeFileHash(string path)
	{
		using (var sha = SHA256.Create())
		using (var stream = File.OpenRead(path))
		{
			return ToHex(sha.ComputeHash(stream));
		}
	}

	static void UpsertEntry(Manifest manifest, SceneBuildTarget target, string unityVersion, string sharedUtilityPath)
	{
		var entry = FindEntry(manifest, target.SceneName);
		if (entry == null)
		{
			entry = new ManifestSceneEntry { sceneName = target.SceneName };
			manifest.scenes.Add(entry);
		}

		entry.inputHash = ComputeInputHash(target, unityVersion, sharedUtilityPath);
		entry.outputHash = ComputeFileHash(target.OutputScenePath);
		entry.outputScenePath = target.OutputScenePath;
		entry.builtAtUtc = DateTime.UtcNow.ToString("O");
	}

	static ManifestSceneEntry FindEntry(Manifest manifest, string sceneName)
	{
		if (manifest == null || manifest.scenes == null)
			return null;

		for (int i = 0; i < manifest.scenes.Count; i++)
		{
			var entry = manifest.scenes[i];
			if (entry != null && string.Equals(entry.sceneName, sceneName, StringComparison.Ordinal))
				return entry;
		}
		return null;
	}

	static void LogChangedScenes(IReadOnlyList<SceneBuildChange> changes)
	{
		Debug.Log("[SceneBuilderIncrementalBuild] " + FormatChangedScenes(changes));
	}

	static string FormatChangedScenes(IReadOnlyList<SceneBuildChange> changes)
	{
		if (changes.Count == 0)
			return "0 changed scenes.";

		var sb = new StringBuilder();
		sb.AppendLine($"{changes.Count} changed scene(s):");
		for (int i = 0; i < changes.Count; i++)
			sb.AppendLine($"- {changes[i].Target.SceneName}: {changes[i].Reason}");
		return sb.ToString().TrimEnd();
	}

	static void AppendFile(HashAlgorithm sha, string path)
	{
		AppendText(sha, $"file:{path}\n");
		if (!File.Exists(path))
		{
			AppendText(sha, "<missing>\n");
			return;
		}

		byte[] bytes = File.ReadAllBytes(path);
		if (bytes.Length > 0)
			sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
		AppendText(sha, "\n");
	}

	static void AppendText(HashAlgorithm sha, string value)
	{
		byte[] bytes = Encoding.UTF8.GetBytes(value ?? "");
		if (bytes.Length > 0)
			sha.TransformBlock(bytes, 0, bytes.Length, null, 0);
	}

	static string ToHex(byte[] bytes)
	{
		if (bytes == null)
			return "";

		var sb = new StringBuilder(bytes.Length * 2);
		for (int i = 0; i < bytes.Length; i++)
			sb.Append(bytes[i].ToString("x2"));
		return sb.ToString();
	}
}
