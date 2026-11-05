var p = UnityEngine.Object.FindFirstObjectByType<GorillaLocomotion.Player>();
if (p == null) return "no gorilla Player in scene";
var s = p.sounds;
return $"turnParent={p.turnParent?.name} head={p.headFollower?.name} L={p.leftFollower?.name}/{p.leftController?.name} R={p.rightFollower?.name}/{p.rightController?.name} " +
       $"taps={p.leftTapSource?.name},{p.rightTapSource?.name} slide={p.leftSlipSource?.clip?.name} loop={p.leftSlipSource?.loop} sounds={(s != null ? s.DefaultSounds.Length : -1)} " +
       $"history={p.velocityHistorySize} layers={p.locomotionEnabledLayers.value} scene={UnityEngine.SceneManagement.SceneManager.GetActiveScene().path} dirty={UnityEngine.SceneManagement.SceneManager.GetActiveScene().isDirty}";
