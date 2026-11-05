// ============================================================
// PediatricVR — HeadOrientationInput.cs
//
// Profile B only (Ages 8–10).
// Child looks left/right to guide a sea creature that swims ahead.
// No controllers, no buttons — head yaw is the only input.
//
// Design rationale:
//   - Dead zone prevents drift from passive head position
//   - Movement is always FORWARD + slight lateral drift
//   - Child feels agency without needing to "do" anything precise
//   - The guided object floats slightly ahead — child "follows" it
//
// This script is DISABLED at startup.
// AgeProfileManager.ApplyProfile() enables/disables it.
//
// GameObject setup:
//   Create an empty "GuidedCreature" in the scene.
//   Assign a sea turtle or dolphin mesh to it.
//   Attach HeadOrientationInput to a child of the XR Camera Rig.
//   Assign the XR Main Camera and GuidedCreature transform.
// ============================================================

using UnityEngine;

namespace PediatricVR
{
    public class HeadOrientationInput : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────
        [Header("Input Sensitivity")]
        [Tooltip("Head yaw within this angle (degrees) is treated as 'looking straight' — no steering.")]
        [SerializeField] private float deadZoneDegrees  = 8f;
        [Tooltip("Head yaw at this angle = maximum lateral steering input.")]
        [SerializeField] private float maxSteerDegrees  = 40f;

        [Header("Movement")]
        [SerializeField] private float forwardSpeed     = 0.8f;     // constant forward drift
        [SerializeField] private float lateralSpeed     = 0.6f;     // max lateral speed at full steer
        [SerializeField] private float smoothing        = 4f;       // velocity interpolation factor

        [Header("References")]
        [Tooltip("Assign the Main Camera (center eye) from your OVRCameraRig.")]
        [SerializeField] private Transform xrCamera;

        [Tooltip("The sea creature the child guides. Floats ahead of camera.")]
        [SerializeField] private Transform guidedCreature;

        [Header("Creature Lead Distance")]
        [Tooltip("How far ahead of the camera the creature stays.")]
        [SerializeField] private float creatureLeadDistance = 3f;
        [SerializeField] private float creatureHeight       = 0f;     // relative to camera height

        // ── Private ────────────────────────────────────────────
        private Vector3 _currentVelocity;
        private Vector3 _creatureBasePosition;

        // ══════════════════════════════════════════════════════
        // Unity Lifecycle
        // ══════════════════════════════════════════════════════

        private void OnEnable()
        {
            if (guidedCreature == null) return;

            // Initialise creature ahead of camera on enable
            if (xrCamera != null)
            {
                Vector3 forward = xrCamera.forward;
                forward.y = 0f;
                forward.Normalize();
                guidedCreature.position = xrCamera.position + forward * creatureLeadDistance;
            }

            guidedCreature.gameObject.SetActive(true);
        }

        private void OnDisable()
        {
            if (guidedCreature != null)
                guidedCreature.gameObject.SetActive(false);

            _currentVelocity = Vector3.zero;
        }

        private void Update()
        {
            if (xrCamera == null || guidedCreature == null) return;

            float steeringInput = ComputeSteeringInput();
            UpdateCreaturePosition(steeringInput);
            RotateCreatureToFaceVelocity();
        }

        // ══════════════════════════════════════════════════════
        // Private
        // ══════════════════════════════════════════════════════

        private float ComputeSteeringInput()
        {
            // Get head yaw in world space, normalized to -180..180
            float yaw = xrCamera.eulerAngles.y;
            if (yaw > 180f) yaw -= 360f;

            // Apply dead zone
            float absYaw = Mathf.Abs(yaw);
            if (absYaw < deadZoneDegrees) return 0f;

            // Normalize to -1..1 over the steer range
            float normalised = Mathf.Sign(yaw) * (absYaw - deadZoneDegrees) / (maxSteerDegrees - deadZoneDegrees);
            return Mathf.Clamp(normalised, -1f, 1f);
        }

        private void UpdateCreaturePosition(float steering)
        {
            // Always drift forward, steer laterally
            Vector3 camForward = xrCamera.forward;
            camForward.y = 0f;
            camForward.Normalize();

            Vector3 camRight = Vector3.Cross(Vector3.up, camForward) * -1f; // right relative to camera

            Vector3 targetVelocity = camForward * forwardSpeed + camRight * (steering * lateralSpeed);

            _currentVelocity = Vector3.Lerp(_currentVelocity, targetVelocity, Time.deltaTime * smoothing);

            // Move creature
            guidedCreature.position += _currentVelocity * Time.deltaTime;

            // Clamp creature to stay within lead distance range of camera
            Vector3 toCreature = guidedCreature.position - xrCamera.position;
            toCreature.y = 0f;
            if (toCreature.magnitude < creatureLeadDistance * 0.5f || toCreature.magnitude > creatureLeadDistance * 2f)
            {
                toCreature = toCreature.normalized * creatureLeadDistance;
                Vector3 clamped = xrCamera.position + toCreature;
                clamped.y = xrCamera.position.y + creatureHeight;
                guidedCreature.position = Vector3.Lerp(guidedCreature.position, clamped, Time.deltaTime * 2f);
            }
        }

        private void RotateCreatureToFaceVelocity()
        {
            if (_currentVelocity.sqrMagnitude < 0.01f) return;

            Quaternion targetRot = Quaternion.LookRotation(_currentVelocity.normalized);
            guidedCreature.rotation = Quaternion.Slerp(guidedCreature.rotation, targetRot, Time.deltaTime * 3f);
        }
    }
}
