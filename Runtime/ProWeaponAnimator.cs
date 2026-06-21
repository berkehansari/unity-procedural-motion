// -----------------------------------------------------------------------------
// Copyright (c) 2026 Berkehan Sarı. All rights reserved.
// https://github.com/berkehansari/unity-procedural-motion.git
// This software is provided "as is", without warranty of any kind.
// For licensing details and usage, refer to the LICENSE file in the repository.
// -----------------------------------------------------------------------------

using UnityEngine;
using UnityEngine.Audio;

namespace Org.BerkehanSari.ProceduralMotion
{
    public class ProWeaponAnimator : MonoBehaviour
    {
        [Header("Audio Settings")]
        public AudioSource movementAudio;
        public AudioMixerGroup sfxGroup;

        [Header("References")]
        public Camera mainCamera;
        private float defaultFOV;
        private IMotionStateProvider motionProvider;

        [Header("AAA Second Order Dynamics (Spring Physics)")]
        [Tooltip("Weapon mass and stiffness. Higher stiffness means faster snap, higher damping means less oscillation.")]
        public float positionalStiffness = 150f;
        public float positionalDamping = 14f;
        public float rotationalStiffness = 150f;
        public float rotationalDamping = 14f;

        [Header("AAA Look Sway (Mouse Input)")]
        public float swayPosAmount = 0.02f;
        public float swayRotAmount = 3.5f;
        public float swayRollAmount = 4.5f;
        public float maxSwayAmount = 0.06f;
        public float maxSwayRot = 12f;

        [Header("AAA Inertia (Movement & Jumping)")]
        public float strafeTilt = 0.15f;
        public float forwardPush = 0.015f;
        public float lateralPush = 0.01f;
        public float verticalPush = 0.02f;
        public float jumpTilt = 1.5f;

        [Header("AAA Organic Idle & Breathing")]
        public float idleBreathSpeed = 0.8f;
        public Vector3 idleSwayPos = new Vector3(0.005f, 0.005f, 0f);
        public Vector3 idleSwayRot = new Vector3(0.5f, 0.8f, 0.3f);

        [Header("AAA Step Bobbing")]
        public float walkBobSpeed = 14f;
        public Vector3 bobPositionLimits = new Vector3(0.01f, 0.015f, 0f);
        public Vector3 bobRotationLimits = new Vector3(1.2f, 0.5f, 0.8f);
        public float stepJoltMultiplier = 2f;

        [Header("AAA Firing Recoil (MW19 Style)")]
        [Tooltip("How fast the weapon snaps back when fired.")]
        public float recoilSnappiness = 40f;
        [Tooltip("How fast the weapon returns to its resting position.")]
        public float recoilReturnSpeed = 10f;
        [Tooltip("Injects raw kinetic energy into the spring system for realistic barrel wobble.")]
        public float recoilSpringImpulse = 15f;
        public float impactReturnSpeed = 5f;

        [Header("Dynamic FOV & Damage Feedback")]
        public float fireFovKick = 1.5f;
        public float damageRotShake = 15f;
        public float damagePosShake = 0.2f;
        public float damageFovKick = -3f;

        [Header("Weapon Switch Animation")]
        public float equipSmooth = 8f;
        public Vector3 equipPosOffset = new Vector3(0, -0.5f, 0);
        public Vector3 equipRotOffset = new Vector3(30f, 0, 0);

        [Header("Debug & Testing")]
        [Tooltip("Enable to test recoil and damage impacts using mouse and keyboard.")]
        public bool enableDebugInputs = true;
        [Tooltip("Positional punch (usually pushes backward on Z and slightly up on Y).")]
        public Vector3 debugRecoilPos = new Vector3(0f, 0.01f, -0.08f);
        [Tooltip("Rotational kick (Negative X is muzzle rise, Y/Z are chaotic horizontal bounces).")]
        public Vector3 debugRecoilRot = new Vector3(-8f, 2.5f, 3f);

        [HideInInspector] public bool isMoving = false;

        private Vector3 initialPosition;
        private Quaternion initialRotation;

        // --- SPRING PHYSICS VARIABLES ---
        private Vector3 currentPosVelocity;
        private Vector3 currentRotVelocity;
        private Vector3 smoothedPos;
        private Vector3 smoothedRot;

        private Vector3 currentTargetPos;
        private Vector3 currentTargetRot;

        private float bobTime = 0f;
        private float breathTime = 0f;

        private Vector3 currentRecoilPos, targetRecoilPos;
        private Vector3 currentRecoilRot, targetRecoilRot;
        private Vector3 impactPosition, impactRotation;

        private float targetFOV;
        private float equipProgress = 0f;
        private bool isEquipping = true;

        private void Start()
        {
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;

            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera != null) defaultFOV = mainCamera.fieldOfView;
            targetFOV = defaultFOV;

            motionProvider = GetComponentInParent<IMotionStateProvider>();

            if (movementAudio != null)
            {
                movementAudio.loop = true;
                if (sfxGroup != null) movementAudio.outputAudioMixerGroup = sfxGroup;
            }

            isEquipping = true;
        }

        private void Update()
        {
            HandleAudio();
            HandleDebugInputs();

            impactPosition = Vector3.Lerp(impactPosition, Vector3.zero, 1f - Mathf.Exp(-impactReturnSpeed * Time.deltaTime));
            impactRotation = Vector3.Lerp(impactRotation, Vector3.zero, 1f - Mathf.Exp(-impactReturnSpeed * Time.deltaTime));

            if (mainCamera != null)
            {
                mainCamera.fieldOfView = Mathf.Lerp(mainCamera.fieldOfView, targetFOV, 1f - Mathf.Exp(-10f * Time.deltaTime));
                targetFOV = Mathf.Lerp(targetFOV, defaultFOV, 1f - Mathf.Exp(-2f * Time.deltaTime));
            }
        }

        private void LateUpdate()
        {
            CalculateProceduralTargets();
            CalculateRecoil();

            // Apply Second Order Dynamics (Hooke's Law)
            ApplySpringDynamics(ref smoothedPos, ref currentPosVelocity, currentTargetPos, positionalStiffness, positionalDamping);
            ApplySpringDynamics(ref smoothedRot, ref currentRotVelocity, currentTargetRot, rotationalStiffness, rotationalDamping);

            equipProgress = Mathf.Lerp(equipProgress, isEquipping ? 1f : 0f, 1f - Mathf.Exp(-equipSmooth * Time.deltaTime));

            // Final Matrix Composition
            transform.localPosition = initialPosition + smoothedPos + currentRecoilPos + impactPosition + Vector3.Lerp(equipPosOffset, Vector3.zero, equipProgress);

            transform.localRotation = initialRotation
                                    * Quaternion.Euler(smoothedRot)
                                    * Quaternion.Euler(currentRecoilRot)
                                    * Quaternion.Euler(impactRotation)
                                    * Quaternion.Euler(Vector3.Lerp(equipRotOffset, Vector3.zero, equipProgress));
        }

        void HandleDebugInputs()
        {
            if (!enableDebugInputs) return;

            // Test Fire Recoil
            if (Input.GetMouseButtonDown(0))
            {
                RecoilFire(debugRecoilPos, debugRecoilRot);
            }

            // Test Damage Impact
            if (Input.GetKeyDown(KeyCode.H))
            {
                TriggerDamageEffect(1.5f);
            }
        }

        void CalculateProceduralTargets()
        {
            float mouseX = Input.GetAxis("Mouse X");
            float mouseY = Input.GetAxis("Mouse Y");

            Vector3 swayPos = new Vector3(Mathf.Clamp(mouseX * swayPosAmount, -maxSwayAmount, maxSwayAmount), Mathf.Clamp(mouseY * swayPosAmount, -maxSwayAmount, maxSwayAmount), 0);
            Vector3 swayRot = new Vector3(Mathf.Clamp(-mouseY * swayRotAmount, -maxSwayRot, maxSwayRot), Mathf.Clamp(mouseX * swayRotAmount, -maxSwayRot, maxSwayRot), Mathf.Clamp(mouseX * swayRollAmount, -maxSwayRot, maxSwayRot));

            Vector3 velPos = Vector3.zero;
            Vector3 velRot = Vector3.zero;
            if (motionProvider != null)
            {
                Vector3 localVel = transform.root.InverseTransformDirection(motionProvider.Velocity);

                float localX = Mathf.Clamp(localVel.x, -15f, 15f);
                float localY = Mathf.Clamp(localVel.y, -20f, 20f);
                float localZ = Mathf.Clamp(localVel.z, -15f, 15f);

                velPos = new Vector3(-localX * lateralPush, -localY * verticalPush, -Mathf.Abs(localZ) * forwardPush);
                velRot = new Vector3(localY * jumpTilt, 0, -localX * strafeTilt);
            }

            Vector3 bobPos = Vector3.zero;
            Vector3 bobRot = Vector3.zero;

            breathTime += Time.deltaTime * idleBreathSpeed;
            float pX = (Mathf.PerlinNoise(breathTime, 10f) - 0.5f) * 2f;
            float pY = (Mathf.PerlinNoise(20f, breathTime) - 0.5f) * 2f;
            float pZ = (Mathf.PerlinNoise(breathTime, 30f) - 0.5f) * 2f;

            bobPos += new Vector3(pX * idleSwayPos.x, pY * idleSwayPos.y, 0);
            bobRot += new Vector3(pX * idleSwayRot.x, pY * idleSwayRot.y, pZ * idleSwayRot.z);

            if (motionProvider != null && motionProvider.IsGrounded && motionProvider.Velocity.magnitude > 0.5f)
            {
                isMoving = true;
                bobTime += Time.deltaTime * walkBobSpeed;

                bobPos.x += Mathf.Cos(bobTime * 0.5f) * bobPositionLimits.x;
                bobPos.y += Mathf.Sin(bobTime) * bobPositionLimits.y;

                float stepJolt = Mathf.Abs(Mathf.Sin(bobTime));
                bobRot.x += stepJolt * bobRotationLimits.x * stepJoltMultiplier;
                bobRot.y += Mathf.Cos(bobTime * 0.5f) * bobRotationLimits.y;
                bobRot.z += Mathf.Cos(bobTime * 0.5f) * bobRotationLimits.z;
            }
            else
            {
                isMoving = false;
            }

            currentTargetPos = swayPos + velPos + bobPos;
            currentTargetRot = swayRot + velRot + bobRot;
        }

        void ApplySpringDynamics(ref Vector3 current, ref Vector3 velocity, Vector3 target, float stiffness, float damping)
        {
            Vector3 displacement = target - current;
            Vector3 springForce = displacement * stiffness;
            velocity += springForce * Time.deltaTime;
            velocity *= (1f - damping * Time.deltaTime);
            current += velocity * Time.deltaTime;
        }

        void CalculateRecoil()
        {
            targetRecoilPos = Vector3.Lerp(targetRecoilPos, Vector3.zero, 1f - Mathf.Exp(-recoilReturnSpeed * Time.deltaTime));
            targetRecoilRot = Vector3.Lerp(targetRecoilRot, Vector3.zero, 1f - Mathf.Exp(-recoilReturnSpeed * Time.deltaTime));
            currentRecoilPos = Vector3.Lerp(currentRecoilPos, targetRecoilPos, 1f - Mathf.Exp(-recoilSnappiness * Time.deltaTime));
            currentRecoilRot = Vector3.Lerp(currentRecoilRot, targetRecoilRot, 1f - Mathf.Exp(-recoilSnappiness * Time.deltaTime));
        }

        void HandleAudio()
        {
            if (movementAudio == null) return;

            bool shouldPlay = isMoving && (motionProvider != null && motionProvider.IsGrounded);
            if (shouldPlay && !movementAudio.isPlaying) movementAudio.Play();
            else if (!shouldPlay && movementAudio.isPlaying) movementAudio.Pause();
        }

        public void RecoilFire(Vector3 posPower, Vector3 rotPower)
        {
            targetRecoilPos += new Vector3(Random.Range(-posPower.x, posPower.x), Random.Range(0, posPower.y), posPower.z);

            float randRotY = Random.Range(-rotPower.y, rotPower.y);
            float randRotZ = Random.Range(-rotPower.z, rotPower.z);
            targetRecoilRot += new Vector3(rotPower.x, randRotY, randRotZ);

            currentRotVelocity += new Vector3(rotPower.x * recoilSpringImpulse, randRotY * recoilSpringImpulse, randRotZ * recoilSpringImpulse);
            currentPosVelocity += new Vector3(0, 0, posPower.z * recoilSpringImpulse);

            targetFOV += fireFovKick;
        }

        public void TriggerDamageEffect(float intensityMultiplier = 1f)
        {
            float randX = Random.Range(-damageRotShake, damageRotShake);
            float randY = Random.Range(-damageRotShake, damageRotShake) * 0.5f;
            float randZ = Random.Range(-damageRotShake, damageRotShake) * 1.5f;

            impactRotation += new Vector3(-Mathf.Abs(randX), randY, randZ) * intensityMultiplier;
            impactPosition += new Vector3(Random.Range(-0.05f, 0.05f), -damagePosShake, -damagePosShake * 2f) * intensityMultiplier;
            targetFOV += damageFovKick * intensityMultiplier;
        }

        public void ApplyImpact(Vector3 posAmount, Vector3 rotAmount)
        {
            impactPosition += posAmount;
            impactRotation += rotAmount;
        }

        public void SetWeaponVisibility(bool showWeapon)
        {
            isEquipping = showWeapon;
        }
    }
}
