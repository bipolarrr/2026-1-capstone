# Task 000C Approved File List

No prior Task 000B approved file list was found in the repository.

This file records the exact file list to paste into Task 000C under:

```text
Approved files to add:
```

Current note: as of commit `7f4bbaf6`, these files are already tracked in `HEAD`.

## Approved Files To Add

```text
Packages/manifest.json
Packages/packages-lock.json
ProjectSettings/AudioManager.asset
ProjectSettings/ClusterInputManager.asset
ProjectSettings/DynamicsManager.asset
ProjectSettings/EditorBuildSettings.asset
ProjectSettings/EditorSettings.asset
ProjectSettings/GraphicsSettings.asset
ProjectSettings/InputManager.asset
ProjectSettings/MemorySettings.asset
ProjectSettings/MultiplayerManager.asset
ProjectSettings/NavMeshAreas.asset
ProjectSettings/NetworkManager.asset
ProjectSettings/PackageManagerSettings.asset
ProjectSettings/Physics2DSettings.asset
ProjectSettings/PresetManager.asset
ProjectSettings/ProjectSettings.asset
ProjectSettings/ProjectVersion.txt
ProjectSettings/QualitySettings.asset
ProjectSettings/SceneTemplateSettings.json
ProjectSettings/ShaderGraphSettings.asset
ProjectSettings/TagManager.asset
ProjectSettings/TimeManager.asset
ProjectSettings/UnityConnectSettings.asset
ProjectSettings/URPProjectSettings.asset
ProjectSettings/VersionControlSettings.asset
ProjectSettings/VFXManager.asset
ProjectSettings/XRSettings.asset
```

## Minimal Critical Subset

These are the files that unblock Unity project identification and package restore:

```text
ProjectSettings/ProjectVersion.txt
Packages/manifest.json
Packages/packages-lock.json
```

## Do Not Add For Task 000C

```text
*.slnx
*.sln
*.csproj
Library/
Temp/
Logs/
Obj/
Build/
Builds/
UserSettings/
Assets/Scenes/
```

## Gitignore Check

`.gitignore` and `.git/info/exclude` do not currently ignore `ProjectSettings/` or `Packages/`.

The existing ignore rules already cover generated Unity and IDE output such as `Library/`,
`Temp/`, `Logs/`, `Obj/`, `Build/`, `Builds/`, `UserSettings/`, `*.csproj`, `*.sln`,
and `Capstone.slnx`.
