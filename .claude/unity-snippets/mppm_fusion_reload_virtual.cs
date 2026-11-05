// MPPM recovery, run INSIDE a virtual player after the dependency hashes are applied: drop the cached failure for
// NetworkProjectConfigAsset only, then load the global config again.
UnityEditor.AssetDatabase.Refresh();
Fusion.NetworkProjectConfig.UnloadGlobal();
var field = typeof(Fusion.FusionGlobalScriptableObject).GetField("_typesFailedToLoad",
    System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
var failed = (System.Collections.Generic.HashSet<System.Type>)field.GetValue(null);
int before = failed.Count;
bool removed = failed.Remove(typeof(Fusion.NetworkProjectConfigAsset));
var adb = UnityEditor.AssetDatabase.LoadMainAssetAtPath("Assets/Photon/Fusion/Resources/NetworkProjectConfig.fusion");
string global;
try { var g = Fusion.NetworkProjectConfigAsset.Global; global = g != null ? "loaded " + g.name : "null"; }
catch (System.Exception e) { global = "throws " + e.GetType().Name + ": " + e.Message; }
return "failedBefore=" + before + " removed=" + removed + " failedAfter=" + failed.Count + " adb=" + (adb != null ? adb.GetType().Name : "null") + " Global=" + global;
