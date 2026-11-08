using GameJam.Core;
using UnityEngine;

namespace GameJam.Editor
{
    /// <summary>
    /// Ambient particles that make the yard feel alive without touching gameplay (no colliders, no collisions): pollen
    /// drifting over the whole yard and leaves falling from the tree canopy. Both share the soft effect-particle material.
    /// </summary>
    internal static class AmbientEffectsBuilder
    {
        /// <summary>Slow, glowing specks over the whole yard; centred on <paramref name="center"/>.</summary>
        public static ParticleSystem Pollen(Transform parent, Vector3 center, Vector3 size)
        {
            ParticleSystem particles = Create("AmbientPollen", parent, center, 320);
            ParticleSystem.MainModule main = particles.main;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(8f, 14f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.05f, 0.25f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.12f);
            Color warm = Color.Lerp(ArtPalette.Accent, Color.white, 0.5f);
            Color pale = Color.white;
            warm.a = 0.75f;
            pale.a = 0.6f;
            main.startColor = new ParticleSystem.MinMaxGradient(warm, pale);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 28f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = size;

            ParticleSystem.VelocityOverLifetimeModule drift = particles.velocityOverLifetime;
            drift.enabled = true;
            drift.space = ParticleSystemSimulationSpace.World;
            drift.x = new ParticleSystem.MinMaxCurve(0.1f, 0.3f); // a light breeze across the yard
            drift.y = new ParticleSystem.MinMaxCurve(-0.03f, 0.06f);
            drift.z = new ParticleSystem.MinMaxCurve(-0.1f, 0.1f);

            Wobble(particles, 0.25f, 0.3f);
            FadeInOut(particles);
            particles.Play();
            return particles;
        }

        /// <summary>Leaves tumbling down from a tree canopy around <paramref name="canopyCenter"/>.</summary>
        public static ParticleSystem FallingLeaves(Transform parent, Vector3 canopyCenter, float radius, Color leaf, Color leafLight)
        {
            ParticleSystem particles = Create("FallingLeaves", parent, canopyCenter, 60);
            ParticleSystem.MainModule main = particles.main;
            main.prewarm = true;
            main.startLifetime = new ParticleSystem.MinMaxCurve(7f, 10f);
            main.startSpeed = 0f;
            main.gravityModifier = 0.02f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.startColor = new ParticleSystem.MinMaxGradient(leaf, leafLight);

            ParticleSystem.EmissionModule emission = particles.emission;
            emission.rateOverTime = 5f;

            ParticleSystem.ShapeModule shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(radius * 2f, 1.5f, radius * 2f);

            ParticleSystem.RotationOverLifetimeModule spin = particles.rotationOverLifetime;
            spin.enabled = true;
            spin.z = new ParticleSystem.MinMaxCurve(-2f, 2f);

            ParticleSystem.VelocityOverLifetimeModule fall = particles.velocityOverLifetime;
            fall.enabled = true;
            fall.space = ParticleSystemSimulationSpace.World;
            fall.x = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
            fall.y = new ParticleSystem.MinMaxCurve(-1.1f, -0.8f); // ~12 m down over a lifetime
            fall.z = new ParticleSystem.MinMaxCurve(-0.2f, 0.2f);

            Wobble(particles, 0.8f, 0.5f);
            FadeInOut(particles);
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.sharedMaterial = PlaceholderPrefabBuilder.LeafParticleMaterial();
            particles.Play();
            return particles;
        }

        /// <summary>
        /// The scene's pooled one-shot bursts (<see cref="GameJam.Gameplay.BurstEffects"/>): confetti strips in the candy
        /// colours for a cleared hoop, rubber shreds (tinted per pop) and a quick air puff for a balloon pop. All fired with
        /// Emit, so nothing is instantiated during play.
        /// </summary>
        public static GameJam.Gameplay.BurstEffects Bursts(Transform parent)
        {
            var root = new GameObject("BurstEffects");
            root.transform.SetParent(parent, false);

            ParticleSystem confetti = OneShot("Confetti", root.transform, 400, PlaceholderPrefabBuilder.ParticleMaterial("EffectConfetti",
                GeneratedTextures.ConfettiStrip("T_Confetti")));
            ParticleSystem.MainModule main = confetti.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.4f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 6.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.11f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 0.45f;
            var candy = new Gradient();
            var keys = new GradientColorKey[ArtPalette.Candy.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                keys[i] = new GradientColorKey(ArtPalette.Candy[i], (i + 1f) / keys.Length);
            }

            candy.SetKeys(keys, new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            candy.mode = GradientMode.Fixed;
            main.startColor = new ParticleSystem.MinMaxGradient(candy) { mode = ParticleSystemGradientMode.RandomColor };
            ParticleSystem.ShapeModule shape = confetti.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.4f;
            Flutter(confetti, 1.2f, 0.25f, 9f);
            FadeOutAtEnd(confetti, 0.8f);

            ParticleSystem shreds = OneShot("Shreds", root.transform, 200, PlaceholderPrefabBuilder.ParticleMaterial("EffectShred",
                GeneratedTextures.Shred("T_Shred")));
            ParticleSystem.MainModule shredMain = shreds.main;
            shredMain.startLifetime = new ParticleSystem.MinMaxCurve(0.6f, 1f);
            shredMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 5.5f);
            shredMain.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.15f);
            shredMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            shredMain.gravityModifier = 0.9f;
            ParticleSystem.ShapeModule shredShape = shreds.shape;
            shredShape.shapeType = ParticleSystemShapeType.Sphere;
            shredShape.radius = 0.25f; // about the balloon's size
            Flutter(shreds, 1.5f, 0.15f, 14f);
            FadeOutAtEnd(shreds, 0.7f);

            ParticleSystem puff = OneShot("Puff", root.transform, 20, PlaceholderPrefabBuilder.EffectParticleMaterial());
            ParticleSystem.MainModule puffMain = puff.main;
            puffMain.startLifetime = 0.28f;
            puffMain.startSpeed = 0f;
            puffMain.startSize = 0.55f;
            puffMain.startColor = new Color(1f, 1f, 1f, 0.6f);
            ParticleSystem.SizeOverLifetimeModule grow = puff.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.4f));
            FadeOutAtEnd(puff, 0f);

            var bursts = root.AddComponent<GameJam.Gameplay.BurstEffects>();
            bursts.Configure(confetti, shreds, puff);
            return bursts;
        }

        /// <summary>
        /// The particles for <see cref="GameJam.Player.HandTapEffects"/> under <paramref name="parent"/>: clods of soil and
        /// grass bits that fly up and fall back (gravity, shrinking), and a faint dust puff.
        /// </summary>
        public static (ParticleSystem clods, ParticleSystem dust) HandDirt(Transform parent)
        {
            var root = new GameObject("HandDirt");
            root.transform.SetParent(parent, false);

            ParticleSystem clods = OneShot("Clods", root.transform, 120, PlaceholderPrefabBuilder.ParticleMaterial("EffectClod",
                GeneratedTextures.Clod("T_Clod")));
            ParticleSystem.MainModule main = clods.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.65f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.055f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.gravityModifier = 1f;
            ParticleSystem.RotationOverLifetimeModule tumble = clods.rotationOverLifetime;
            tumble.enabled = true;
            tumble.z = new ParticleSystem.MinMaxCurve(-10f, 10f);
            var soil = new Gradient();
            Color soilDark = Color.Lerp(ArtPalette.TimberDark, Color.black, 0.5f);
            Color grassLight = Color.Lerp(ArtPalette.LawnDark, ArtPalette.Accent, 0.25f);
            soil.SetKeys(new[]
                {
                    new GradientColorKey(soilDark, 0.35f), new GradientColorKey(Color.Lerp(ArtPalette.TimberDark, Color.black, 0.3f), 0.55f),
                    new GradientColorKey(ArtPalette.LawnDark, 0.85f), new GradientColorKey(grassLight, 1f),
                },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) });
            soil.mode = GradientMode.Fixed;
            main.startColor = new ParticleSystem.MinMaxGradient(soil) { mode = ParticleSystemGradientMode.RandomColor };
            ParticleSystem.SizeOverLifetimeModule shrink = clods.sizeOverLifetime;
            shrink.enabled = true;
            shrink.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0.3f));

            ParticleSystem dust = OneShot("Dust", root.transform, 20, PlaceholderPrefabBuilder.EffectParticleMaterial());
            ParticleSystem.MainModule dustMain = dust.main;
            dustMain.startLifetime = new ParticleSystem.MinMaxCurve(0.5f, 0.8f);
            dustMain.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.16f);
            Color dustColor = Color.Lerp(ArtPalette.TimberDark, Color.white, 0.45f);
            dustColor.a = 0.35f;
            dustMain.startColor = dustColor;
            ParticleSystem.SizeOverLifetimeModule spread = dust.sizeOverLifetime;
            spread.enabled = true;
            spread.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.8f));
            FadeOutAtEnd(dust, 0f);

            PlaceholderShapes.SetLayerRecursively(root, GameLayers.IgnoreRaycast);
            return (clods, dust);
        }

        /// <summary>A world-space particle system that only emits when told to (Emit): no looping, no rate.</summary>
        private static ParticleSystem OneShot(string name, Transform parent, int maxParticles, Material material)
        {
            var emitter = new GameObject(name);
            emitter.transform.SetParent(parent, false);
            var particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            ParticleSystem.MainModule main = particles.main;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            ParticleSystem.EmissionModule emission = particles.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = particles.shape;
            shape.enabled = true;
            var particleRenderer = emitter.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = material;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            PlaceholderShapes.SetLayerRecursively(emitter, GameLayers.IgnoreRaycast);
            return particles;
        }

        /// <summary>Paper-like flight: air drag slows the burst, a little noise flutters it, and it spins.</summary>
        private static void Flutter(ParticleSystem particles, float noiseStrength, float drag, float spin)
        {
            ParticleSystem.LimitVelocityOverLifetimeModule air = particles.limitVelocityOverLifetime;
            air.enabled = true;
            air.drag = drag;
            air.multiplyDragByParticleSize = false;
            air.multiplyDragByParticleVelocity = true;
            Wobble(particles, noiseStrength, 0.8f);
            ParticleSystem.RotationOverLifetimeModule rotation = particles.rotationOverLifetime;
            rotation.enabled = true;
            rotation.z = new ParticleSystem.MinMaxCurve(-spin, spin);
        }

        /// <summary>Full alpha until <paramref name="fadeFrom"/> of the lifetime, then fades out.</summary>
        private static void FadeOutAtEnd(ParticleSystem particles, float fadeFrom)
        {
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeFrom), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
        }

        private static ParticleSystem Create(string name, Transform parent, Vector3 position, int maxParticles)
        {
            var emitter = new GameObject(name);
            emitter.transform.SetParent(parent, false);
            emitter.transform.position = position;
            var particles = emitter.AddComponent<ParticleSystem>();
            particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = particles.main;
            main.loop = true;
            main.playOnAwake = true;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = maxParticles;
            main.scalingMode = ParticleSystemScalingMode.Shape;

            var particleRenderer = emitter.GetComponent<ParticleSystemRenderer>();
            particleRenderer.sharedMaterial = PlaceholderPrefabBuilder.EffectParticleMaterial();
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            particleRenderer.maxParticleSize = 0.2f;
            PlaceholderShapes.SetLayerRecursively(emitter, GameLayers.IgnoreRaycast);
            return particles;
        }

        private static void Wobble(ParticleSystem particles, float strength, float frequency)
        {
            ParticleSystem.NoiseModule noise = particles.noise;
            noise.enabled = true;
            noise.strength = strength;
            noise.frequency = frequency;
            noise.quality = ParticleSystemNoiseQuality.Low;
        }

        private static void FadeInOut(ParticleSystem particles)
        {
            ParticleSystem.ColorOverLifetimeModule fade = particles.colorOverLifetime;
            fade.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(1f, 0.8f), new GradientAlphaKey(0f, 1f) });
            fade.color = gradient;
        }
    }
}
