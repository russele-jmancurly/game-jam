// MPPM recovery, run INSIDE a virtual player: apply the main Editor's current Fusion custom-dependency hashes
// (read from its Temp/FusionMppm/Dependency_Fusion.*.json, never hardcoded), then refresh the AssetDatabase.
// A clone lives in <main>/Library/VP/<id>; its Assets folder is <clone>/Assets, so the main project is three levels up.
var cloneRoot = System.IO.Directory.GetParent(UnityEngine.Application.dataPath).FullName;
var folder = System.IO.Path.GetFullPath(System.IO.Path.Combine(cloneRoot, "..", "..", "..", "Temp", "FusionMppm"));
var sb = new System.Text.StringBuilder("status=" + Fusion.FusionMppm.Status + " ");
foreach (var file in System.IO.Directory.GetFiles(folder, "Dependency_Fusion.*.json"))
{
    var json = System.IO.File.ReadAllText(file);
    var name = System.Text.RegularExpressions.Regex.Match(json, "\"DependencyName\":\"([^\"]+)\"").Groups[1].Value;
    var hash = System.Text.RegularExpressions.Regex.Match(json, "\"Hash\":\"([0-9a-f]+)\"").Groups[1].Value;
    if (name.Length == 0 || hash.Length == 0) { sb.Append("skip ").Append(System.IO.Path.GetFileName(file)).Append(' '); continue; }
    new Fusion.FusionMppmRegisterCustomDependencyCommand { DependencyName = name, Hash = hash }.Execute();
    sb.Append(name).Append('=').Append(hash).Append(' ');
}
UnityEditor.AssetDatabase.Refresh();
return sb.ToString();
