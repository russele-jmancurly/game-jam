// Compiles the player scripts for Android (Quest) without a build: catches #if UNITY_ANDROID / player-only errors.
var settings = new UnityEditor.Build.Player.ScriptCompilationSettings
{
    target = UnityEditor.BuildTarget.Android,
    group = UnityEditor.BuildTargetGroup.Android,
    options = UnityEditor.Build.Player.ScriptCompilationOptions.None,
};
if (!UnityEditor.BuildPipeline.IsBuildTargetSupported(UnityEditor.BuildTargetGroup.Android, UnityEditor.BuildTarget.Android)) { return "Android module not installed"; }
var result = UnityEditor.Build.Player.PlayerBuildInterface.CompilePlayerScripts(settings, "Temp/AndroidCompileCheck");
int ours = 0;
foreach (var a in result.assemblies) { if (a.StartsWith("GameJam")) ours++; }
return "assemblies=" + result.assemblies.Count + " gameJam=" + ours;
