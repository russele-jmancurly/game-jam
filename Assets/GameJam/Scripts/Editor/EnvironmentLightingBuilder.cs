using GameJam.Core;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace GameJam.Editor
{
    /// <summary>
    /// Light story from the art bible: late-morning sun from front-left, cool sky bounce in the shadows,
    /// palette sky and fog, and a light global Volume. Bloom is skipped on purpose: the Quest pipeline asset
    /// runs without HDR, where bloom thresholds never fire. Post-processing renders in PC mode only by
    /// default (PlayerRigConfig.PostProcessingInHeadset) to protect Quest frame time.
    /// </summary>
    public static class EnvironmentLightingBuilder
    {
        public const string ProfilePath = "Assets/GameJam/Art/Settings/BackyardVolume.asset";
        private const string SkyboxName = "BackyardSky";
        private const float MinShadowDistance = 14f;

        public static void Build()
        {
            BuildSun();
            BuildAmbientAndFog();
            BuildVolume();
            RaiseShadowDistance();
        }

        private static void BuildSun()
        {
            Light sun = null;
            foreach (Light light in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    sun = light;
                    break;
                }
            }

            if (sun == null)
            {
                sun = new GameObject("Sun").AddComponent<Light>();
                sun.type = LightType.Directional;
            }

            sun.name = "Sun";
            sun.color = ArtPalette.SunLight;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;
            // Elevation ~42 degrees, coming across the course from front-left so faces get light and shadow sides.
            sun.transform.rotation = Quaternion.Euler(42f, 135f, 0f);
            RenderSettings.sun = sun;
        }

        private static void BuildAmbientAndFog()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = ArtPalette.AmbientSky;
            RenderSettings.ambientEquatorColor = ArtPalette.AmbientEquator;
            RenderSettings.ambientGroundColor = ArtPalette.AmbientGround;

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = ArtPalette.Fog;
            RenderSettings.fogStartDistance = 70f;
            RenderSettings.fogEndDistance = 240f;

            Material sky = AssetDatabase.LoadAssetAtPath<Material>($"{PlaceholderAssets.MaterialFolder}/{SkyboxName}.mat");
            if (sky == null)
            {
                sky = new Material(Shader.Find("Skybox/Procedural"));
                AssetDatabase.CreateAsset(sky, $"{PlaceholderAssets.MaterialFolder}/{SkyboxName}.mat");
            }

            sky.SetColor("_SkyTint", ArtPalette.SkyTop);
            sky.SetColor("_GroundColor", ArtPalette.AmbientGround);
            sky.SetFloat("_AtmosphereThickness", 0.7f);
            sky.SetFloat("_Exposure", 1.25f);
            sky.SetFloat("_SunSize", 0.035f);
            sky.SetFloat("_SunSizeConvergence", 6f);
            EditorUtility.SetDirty(sky);
            RenderSettings.skybox = sky;
        }

        private static void BuildVolume()
        {
            System.IO.Directory.CreateDirectory("Assets/GameJam/Art/Settings");
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, ProfilePath);
            }

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral); // flat toy colours keep their hue

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.saturation.Override(10f);
            color.contrast.Override(6f);
            color.postExposure.Override(0.1f);

            WhiteBalance whiteBalance = GetOrAdd<WhiteBalance>(profile);
            whiteBalance.temperature.Override(6f); // a touch of morning warmth

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(0.2f);
            vignette.smoothness.Override(0.45f);
            vignette.color.Override(ArtPalette.Shadow);

            EditorUtility.SetDirty(profile);

            var volumeObject = new GameObject("GlobalVolume");
            var volume = volumeObject.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (!profile.TryGet(out T component))
            {
                component = profile.Add<T>(true);
                AssetDatabase.AddObjectToAsset(component, profile);
            }

            component.active = true;
            return component;
        }

        /// <summary>
        /// The Quest pipeline asset shipped with a 2.5 m shadow distance, so shadows vanished a few steps away.
        /// Raise it to cover the playable section around the player; one cascade keeps the Quest cost low.
        /// </summary>
        private static void RaiseShadowDistance()
        {
            foreach (string guid in AssetDatabase.FindAssets("t:UniversalRenderPipelineAsset"))
            {
                var asset = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset != null && asset.shadowDistance < MinShadowDistance)
                {
                    asset.shadowDistance = MinShadowDistance;
                    EditorUtility.SetDirty(asset);
                }
            }
        }
    }
}
