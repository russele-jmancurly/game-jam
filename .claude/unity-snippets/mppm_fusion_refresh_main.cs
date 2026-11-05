// MPPM recovery, main Editor: force Fusion's NetworkProjectConfigImporter custom dependencies to recompute now.
var flags = System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static;
var importer = System.Type.GetType("Fusion.Editor.NetworkProjectConfigImporter, Fusion.Unity.Editor");
var sb = new System.Text.StringBuilder();
foreach (var name in new[] { "ScriptOrderDependency", "AddressablesDependency", "NetworkObjectPrefabDependency" })
{
    var dep = importer.GetField(name, flags).GetValue(null);
    dep.GetType().GetMethod("Refresh").Invoke(dep, new object[] { true });
    sb.Append(name).Append('=').Append(dep.GetType().GetField("Name").GetValue(dep)).Append(' ');
}
return sb.ToString();
