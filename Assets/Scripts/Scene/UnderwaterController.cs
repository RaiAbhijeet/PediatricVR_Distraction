// ============================================================
// PediatricVR — UnderwaterController.cs
//
// Manages the underwater VR environment:
//   - Fade in/out via a full-screen black overlay CanvasGroup
//   - Fish school activation and speed control
//   - Caustic light animation
//   - Particle systems (bubbles, plankton)
//   - Ambient light intensity
//
// GameObject setup:
//   Attach to an empty "UnderwaterScene" GameObject.
//   This GameObject should be the root of all underwater scene objects.
//   Assign all serialized references in Inspector.
//
// The CanvasGroup for fading should be on a World Space Canvas
// that covers the entire FOV. Set its alpha to 1 (black) by default.
// ============================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PediatricVR
{
    public class UnderwaterController : MonoBehaviour
    {
        // ── Fish Schools ──────────────────────────────────────
        [Header("Fish")]
        [SerializeField] private List<FishSchool> fishSchools;

        // ── Scene VFX ─────────────────────────────────────────
        [Header("Scene Visual Elements")]
        [SerializeField] private GameObject  underwaterRoot;         // parent of all underwater visuals
        [SerializeField] private Light       sunRayLight;            // directional light for sun shafts
        [SerializeField] private ParticleSystem bubbleParticles;
        [SerializeField] private ParticleSystem planktonParticles;
        [SerializeField] private ParticleSystem causticProjector;    // optional caustic VFX particle system

        // ── Fade ──────────────────────────────────────────────
        [Header("Fade Overlay")]
        [Tooltip("CanvasGroup on a full-FOV World Space Canvas. Alpha=1 is black, Alpha=0 is clear.")]
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private float fadeInDuration  = 3f;
        [SerializeField] private float fadeOutDuration = 2f;

        // ── Caustic Animation ─────────────────────────────────
        [Header("Caustic Tiling Animation")]
        [SerializeField] private Renderer causticsRenderer;
        [SerializeField] private float causticsScrollSpeed = 0.03f;

        // ── Private ────────────────────────────────────────────
        private Coroutine _fadeCo;
        private const float IdleFadeDuration = 1.5f;

        // ══════════════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            // Start hidden — idle screen shows first
            SetSceneObjectsActive(false);
            if (fadeOverlay != null) fadeOverlay.alpha = 1f;

            ApplyUnderwaterEnvironment();
        }

        private void Start()
        {
            SetupBubbleParticle(bubbleParticles,   isBubble: true);
            SetupBubbleParticle(planktonParticles, isBubble: false);

            // Caustic projector has no runtime material — disable it to remove pink squares.
            // Underwater light caustics come from fog + ambient colour instead.
            if (causticProjector != null)
                causticProjector.gameObject.SetActive(false);
        }

        private void Update()
        {
            // Animate caustics UV scrolling if renderer is assigned
            if (causticsRenderer != null && causticsRenderer.isVisible)
            {
                float offset = Time.time * causticsScrollSpeed;
                causticsRenderer.material.SetTextureOffset("_MainTex", new Vector2(offset, offset * 0.7f));
            }
        }

        // ══════════════════════════════════════════════════════
        // Public API — called by GameManager / AgeProfileManager
        // ══════════════════════════════════════════════════════

        public void StartExperience(AgeProfile profile)
        {
            SetSceneObjectsActive(true);

            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeInRoutine());
        }

        public void StopExperience()
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeOutRoutine());
        }

        /// <summary>
        /// Immediate stop — no fade. Used by Reset command between patients.
        /// </summary>
        public void ForceStop()
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            SetSceneObjectsActive(false);
            if (fadeOverlay != null) fadeOverlay.alpha = 1f;
        }

        /// <summary>
        /// Fades the overlay to transparent so the idle screen is visible.
        /// Call this whenever transitioning back to idle (startup or after session).
        /// </summary>
        public void RevealIdleScreen()
        {
            if (_fadeCo != null) StopCoroutine(_fadeCo);
            _fadeCo = StartCoroutine(FadeOverlayTo(0f, IdleFadeDuration));
        }

        // ── Parameter setters (called by AgeProfileManager) ───

        public void SetFishSpeed(float speed)
        {
            foreach (var school in fishSchools)
                if (school != null) school.SetSpeed(speed);
        }

        public void SetAmbientIntensity(float intensity)
        {
            if (sunRayLight != null)
                sunRayLight.intensity = Mathf.Clamp01(intensity) * 1.5f;
        }

        public void SetFadeInDuration(float duration)
        {
            fadeInDuration = Mathf.Max(0.5f, duration);
        }

        // ══════════════════════════════════════════════════════
        // Private — Coroutines
        // ══════════════════════════════════════════════════════

        private IEnumerator FadeInRoutine()
        {
            yield return StartCoroutine(FadeOverlayTo(0f, fadeInDuration));
        }

        private IEnumerator FadeOutRoutine()
        {
            yield return StartCoroutine(FadeOverlayTo(1f, fadeOutDuration));
            SetSceneObjectsActive(false);
        }

        private IEnumerator FadeOverlayTo(float target, float duration)
        {
            if (fadeOverlay == null) yield break;
            float start = fadeOverlay.alpha;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                fadeOverlay.alpha = Mathf.Lerp(start, target, Mathf.Clamp01(elapsed / duration));
                yield return null;
            }
            fadeOverlay.alpha = target;
        }

        // ══════════════════════════════════════════════════════
        // Private — Helpers
        // ══════════════════════════════════════════════════════

        private void SetSceneObjectsActive(bool active)
        {
            if (underwaterRoot != null) underwaterRoot.SetActive(active);

            ToggleParticles(bubbleParticles,   active);
            ToggleParticles(planktonParticles, active);
            // causticProjector intentionally excluded — disabled at Start to remove pink squares

            foreach (var school in fishSchools)
                if (school != null) school.SetActive(active);
        }

        private static void ToggleParticles(ParticleSystem ps, bool active)
        {
            if (ps == null) return;
            if (active) ps.Play();
            else        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        // ══════════════════════════════════════════════════════
        // Underwater Environment — camera, fog, lighting
        // ══════════════════════════════════════════════════════

        private void ApplyUnderwaterEnvironment()
        {
            // Fog is disabled — per-pixel cost in VR stereo is too high for Quest 3S.
            // Underwater feel comes from dark camera background + teal ambient light instead.
            RenderSettings.fog = false;

            // Ambient light — dim deep-ocean blue
            RenderSettings.ambientMode  = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.04f, 0.14f, 0.28f);

            // Directional light recoloured as filtered sunlight through water
            if (sunRayLight != null)
            {
                sunRayLight.color     = new Color(0.25f, 0.65f, 0.85f);
                sunRayLight.intensity = 0.55f;
            }

            // Deep water camera background (runs after OVR cameras are ready)
            StartCoroutine(SetCameraBackgroundNextFrame());
        }

        private IEnumerator SetCameraBackgroundNextFrame()
        {
            yield return null; // wait one frame for OVR cameras to initialise

            var deepBlue = new Color(0.01f, 0.04f, 0.12f);
            foreach (var cam in Camera.allCameras)
            {
                cam.clearFlags       = CameraClearFlags.SolidColor;
                cam.backgroundColor  = deepBlue;
            }
        }

        // ══════════════════════════════════════════════════════
        // Particle Setup — procedural bubble / plankton visuals
        // ══════════════════════════════════════════════════════

        private void SetupBubbleParticle(ParticleSystem ps, bool isBubble)
        {
            if (ps == null) return;

            // Material
            var psRenderer = ps.GetComponent<ParticleSystemRenderer>();
            Texture2D tex  = isBubble ? CreateBubbleRingTexture() : CreateSoftDotTexture();
            Color     col  = isBubble
                ? new Color(0.80f, 0.95f, 1.00f, 0.55f)
                : new Color(0.45f, 0.80f, 1.00f, 0.35f);
            psRenderer.material = CreateParticleMaterial(tex, col);

            // Main module — counts kept very low for Quest 3S mobile GPU
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles    = isBubble ? 20 : 50;

            if (isBubble)
            {
                main.startSize       = new ParticleSystem.MinMaxCurve(0.04f, 0.18f);
                main.startLifetime   = new ParticleSystem.MinMaxCurve(5f, 10f);
                main.startSpeed      = new ParticleSystem.MinMaxCurve(0.0f, 0.0f);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(-0.06f);
                main.startColor      = new ParticleSystem.MinMaxGradient(
                    new Color(0.85f, 0.97f, 1f, 0.65f),
                    new Color(0.90f, 1.00f, 1f, 0.20f));
            }
            else // plankton
            {
                main.startSize       = new ParticleSystem.MinMaxCurve(0.008f, 0.030f);
                main.startLifetime   = new ParticleSystem.MinMaxCurve(8f, 16f);
                main.startSpeed      = new ParticleSystem.MinMaxCurve(0.01f, 0.04f);
                main.gravityModifier = new ParticleSystem.MinMaxCurve(0f);
                main.startColor      = new ParticleSystem.MinMaxGradient(
                    new Color(0.50f, 0.90f, 1f, 0.55f),
                    new Color(0.30f, 0.70f, 0.90f, 0.20f));
            }

            // Velocity over lifetime (bubbles drift up + slight random wander)
            if (isBubble)
            {
                var vel = ps.velocityOverLifetime;
                vel.enabled = true;
                vel.space   = ParticleSystemSimulationSpace.World;
                vel.x = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
                vel.y = new ParticleSystem.MinMaxCurve(0.20f,  0.55f);
                vel.z = new ParticleSystem.MinMaxCurve(-0.05f, 0.05f);
            }

            // Emission — reduced for mobile VR overdraw budget
            var emission = ps.emission;
            emission.rateOverTime = isBubble ? 2f : 6f;

            // Shape — wide flat box below the camera so bubbles rise past the player
            var shape = ps.shape;
            shape.enabled   = true;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale     = isBubble
                ? new Vector3(10f, 0.2f, 10f)   // wide flat area, bubbles rise upward
                : new Vector3(14f, 6f,  14f);    // large volume for plankton
        }

        private static Material CreateParticleMaterial(Texture2D texture, Color color)
        {
            // Try URP particle shader first; fall back to the legacy Alpha Blended shader
            Shader shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null)
                shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");

            var mat = new Material(shader) { name = "BubbleMat_Runtime" };

            // URP path
            if (shader.name.StartsWith("Universal"))
            {
                mat.SetTexture("_BaseMap", texture);
                mat.SetColor("_BaseColor", color);
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend",   0f);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = 3000;
            }
            else
            {
                // Legacy / Sprites fallback
                mat.SetTexture("_MainTex", texture);
                mat.SetColor("_Color", color);
                mat.renderQueue = 3000;
            }

            return mat;
        }

        private static Texture2D CreateBubbleRingTexture(int size = 64)
        {
            var tex     = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c     = (size - 1) * 0.5f;
            float outer = c * 0.92f;
            float inner = c * 0.62f;
            float edge  = outer - inner;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = 0f;

                if (d <= outer)
                {
                    if (d >= inner)
                    {
                        // Smooth ring
                        float t = (d - inner) / edge;
                        a = Mathf.SmoothStep(0f, 1f, t < 0.5f ? t * 2f : (1f - t) * 2f);
                        a *= 0.9f;
                    }
                    else
                    {
                        // Very faint glassy fill inside
                        a = 0.07f * Mathf.Clamp01(1f - d / inner);
                    }
                }
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            return tex;
        }

        private static Texture2D CreateSoftDotTexture(int size = 16)
        {
            var tex   = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float c   = (size - 1) * 0.5f;
            float r   = c * 0.88f;

            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x, y), new Vector2(c, c));
                float a = Mathf.Clamp01(1f - d / r);
                a = a * a;
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }

            tex.Apply();
            return tex;
        }
    }
}
