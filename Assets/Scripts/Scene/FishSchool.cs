// ============================================================
// PediatricVR — FishSchool.cs
//
// A simple procedural fish school. Each school moves as a unit
// toward randomly-chosen waypoints, while individual fish bob
// on a sine wave offset by their index (natural-looking stagger).
//
// This is intentionally simple — no full boids. The goal is
// calm, predictable, non-startling movement for anxious children.
//
// GameObject setup:
//   Create an empty "FishSchool_01" GameObject in the scene.
//   Attach this script. Assign a fish prefab (simple rigged/animated
//   fish mesh). Set the school radius and waypoint parameters.
//   Add multiple FishSchool GameObjects for variety (e.g., 3 schools).
//   Drag all FishSchool references into UnderwaterController.
// ============================================================

using System.Collections.Generic;
using UnityEngine;

namespace PediatricVR
{
    public class FishSchool : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Fish Prefab & Count")]
        [Tooltip("Assign a fish mesh prefab. Can be a simple rigged model from the Unity Asset Store.")]
        [SerializeField] private GameObject fishPrefab;
        [SerializeField] private int        fishCount   = 10;

        [Header("School Formation")]
        [Tooltip("Radius within which individual fish spawn around the school centre.")]
        [SerializeField] private float schoolRadius = 1.8f;

        [Header("Movement")]
        [SerializeField] private float swimSpeed      = 0.25f;
        [SerializeField] private float rotationSpeed  = 1.5f;
        [Tooltip("How far from current position to pick the next waypoint.")]
        [SerializeField] private float waypointRadius = 6f;
        [Tooltip("Arrive-threshold: how close to get before picking a new waypoint.")]
        [SerializeField] private float arriveDistance = 1.5f;

        [Header("Bob Animation")]
        [SerializeField] private float bobAmplitude  = 0.12f;
        [SerializeField] private float bobFrequency  = 0.75f;

        [Header("Spawn Bounds")]
        [Tooltip("School will not wander outside a box this large around its start position.")]
        [SerializeField] private Vector3 wanderBounds = new Vector3(12f, 3f, 12f);

        // ── Private ────────────────────────────────────────────
        private readonly List<GameObject> _fishInstances   = new();
        private readonly List<Vector3>    _localOffsets    = new();

        private Vector3 _startPosition;
        private Vector3 _targetWaypoint;
        private bool    _isActive;

        // ══════════════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════════════

        private void Awake()
        {
            _startPosition = transform.position;
            SpawnFish();
            PickNewWaypoint();
        }

        private void Update()
        {
            if (!_isActive || fishPrefab == null) return;

            MoveSchoolTowardWaypoint();
            AnimateIndividualFish();
        }

        // ══════════════════════════════════════════════════════
        // Public API — called by UnderwaterController
        // ══════════════════════════════════════════════════════

        public void SetSpeed(float speed)
        {
            swimSpeed = Mathf.Max(0.05f, speed);
        }

        public void SetActive(bool active)
        {
            _isActive = active;
            foreach (var fish in _fishInstances)
                if (fish != null) fish.SetActive(active);
        }

        // ══════════════════════════════════════════════════════
        // Private
        // ══════════════════════════════════════════════════════

        private void SpawnFish()
        {
            if (fishPrefab == null)
            {
                Debug.LogWarning($"[FishSchool] No fishPrefab assigned on {gameObject.name}.");
                return;
            }

            Random.InitState(gameObject.GetInstanceID()); // deterministic per school

            for (int i = 0; i < fishCount; i++)
            {
                Vector3 offset = Random.insideUnitSphere * schoolRadius;
                Vector3 spawnPos = transform.position + offset;

                var fish = Instantiate(fishPrefab, spawnPos, Quaternion.identity, transform);
                fish.SetActive(false);
                _fishInstances.Add(fish);
                _localOffsets.Add(offset);
            }
        }

        private void MoveSchoolTowardWaypoint()
        {
            float distance = Vector3.Distance(transform.position, _targetWaypoint);
            if (distance < arriveDistance) PickNewWaypoint();

            transform.position = Vector3.MoveTowards(
                transform.position,
                _targetWaypoint,
                swimSpeed * Time.deltaTime
            );

            Vector3 dir = (_targetWaypoint - transform.position);
            if (dir.sqrMagnitude > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir.normalized);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            }
        }

        private void AnimateIndividualFish()
        {
            float time = Time.time;
            float phaseStep = (Mathf.PI * 2f) / Mathf.Max(1, fishCount);

            for (int i = 0; i < _fishInstances.Count; i++)
            {
                if (_fishInstances[i] == null) continue;

                float phase   = i * phaseStep;
                float bobY    = Mathf.Sin(time * bobFrequency + phase) * bobAmplitude;
                var localPos  = _localOffsets[i];
                localPos.y   += bobY;

                _fishInstances[i].transform.localPosition = localPos;
                _fishInstances[i].transform.rotation      = transform.rotation;
            }
        }

        private void PickNewWaypoint()
        {
            // Random direction biased toward horizontal (fish don't dive steeply)
            Vector3 randomDir = Random.insideUnitSphere;
            randomDir.y *= 0.25f;
            randomDir.Normalize();

            Vector3 candidate = transform.position + randomDir * waypointRadius;

            // Clamp within wander bounds around start position
            candidate.x = Mathf.Clamp(candidate.x, _startPosition.x - wanderBounds.x * 0.5f,
                                                     _startPosition.x + wanderBounds.x * 0.5f);
            candidate.y = Mathf.Clamp(candidate.y, _startPosition.y - wanderBounds.y * 0.5f,
                                                     _startPosition.y + wanderBounds.y * 0.5f);
            candidate.z = Mathf.Clamp(candidate.z, _startPosition.z - wanderBounds.z * 0.5f,
                                                     _startPosition.z + wanderBounds.z * 0.5f);

            _targetWaypoint = candidate;
        }
    }
}
