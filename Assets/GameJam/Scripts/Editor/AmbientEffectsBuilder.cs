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
            particles.Play();
            return particles;
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
