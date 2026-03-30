# Umbrella

Unity team project repository.

## Project Info
- Unity version: `6000.4.0f1`
- Render pipeline: `Universal 3D (URP)`
- Main branch: `main`

## Initial Setup
1. Install Unity Hub and Unity `6000.4.0f1`.
2. Clone this repository.
3. Open the cloned folder in Unity Hub.
4. Let Unity finish the first import.
5. If Git LFS is not installed yet, install it and run `git lfs pull`.

## Folder Rules
- Put scenes in `Assets/Scenes`.
- Put scripts in `Assets/Scripts`.
- Put prefabs in `Assets/Prefabs`.
- Put materials in `Assets/Materials`.
- Put art assets in `Assets/Art`.

## Git Branch Rules
- Do not work directly on `main`.
- Create a branch for every task.
- Branch naming rule: `feature/<name>-<feature>`
- Example branch names:
  - `feature/minsu-player-move`
  - `feature/jiwon-ui-inventory`
  - `feature/taeho-enemy-spawn`

## Recommended Workflow
1. Pull the latest `main`.
2. Create a new `feature/...` branch.
3. Work and commit on that branch.
4. Push the branch to GitHub.
5. Merge into `main` after review or team confirmation.

## SourceTree Workflow
1. Fetch or Pull `main`.
2. Use `Branch` to create `feature/<name>-<feature>` from `main`.
3. Commit changes with a short, clear message.
4. Push the branch.
5. Create a Pull Request on GitHub, or merge after the team agrees.

## Collaboration Rules
- Do not edit the same scene at the same time.
- Avoid making large unrelated changes in one commit.
- Keep `main` in a runnable state.
- Before merging, sync with the latest `main` and check for conflicts.
- When changing shared prefabs or scenes, tell the team first.

## Unity Version Control Settings
These settings must stay enabled for Git collaboration.
- `Version Control = Visible Meta Files`
- `Asset Serialization = Force Text`

## Files Included In Git
Keep these in Git:
- `Assets/`
- `Packages/`
- `ProjectSettings/`
- `.gitignore`
- `.gitattributes`

Do not commit these folders:
- `Library/`
- `Temp/`
- `Logs/`
- `UserSettings/`
- `.vs/`

## Notes
- Large binary assets should be tracked with Git LFS.
- Team members should clone the same repository instead of creating separate Unity projects.
