// -----------------------------------------------------------------------------
// Copyright (c) 2026 Berkehan Sarı. All rights reserved.
// https://github.com/berkehansari/unity-procedural-motion.git
// This software is provided "as is", without warranty of any kind.
// For licensing details and usage, refer to the LICENSE file in the repository.
// -----------------------------------------------------------------------------

using UnityEngine;
using System.Collections;

namespace Org.BerkehanSari.ProceduralMotion
{
    [RequireComponent(typeof(Rigidbody))]
    public class PlayerController : MonoBehaviour, IMotionStateProvider
    {
        [Header("References")]
        public ProWeaponAnimator weaponAnimator;
        public AudioSource audioSource;
        public AudioClip jumpSound;
        public AudioClip landSound;
        public AudioClip dashSound;

        [Header("Movement")]
        public float moveSpeed = 12f;
        public float airControl = 0.5f;
        public float jumpForce = 12f;

        [Header("Mouse Look (Bakis Ayarlari)")]
        public float mouseSensitivity = 2f;
        public float upperLookLimit = 80f;
        public float lowerLookLimit = 80f;
        private float verticalRotation = 0f;

        [Header("Ladder Settings")]
        public float climbSpeed = 6f;
        private bool isClimbing = false;

        [Header("Slope Settings")]
        public float maxSlopeAngle = 45f;
        private RaycastHit slopeHit;

        [Header("Dash Settings")]
        public float dashSpeed = 25f;
        public float dashDuration = 0.2f;
        public float dashCooldown = 1.0f;
        private bool isDashing;
        private float lastDashTime;

        [Header("Game Feel")]
        public Camera playerCamera;
        public float normalFOV = 60f;
        public float dashFOV = 80f;

        [Header("Gravity & Physics")]
        public float upwardGravity = 1.0f;
        public float downwardGravity = 4.0f;

        private int airJumpCount = 0;

        [Header("Acceleration")]
        public float groundAcceleration = 60f;
        public float groundDeceleration = 25f;
        public float airAcceleration = 40f;
        public float maxGroundSpeed = 12f;
        public float maxAirSpeed = 14f;

        [Header("Pro Feel")]
        public float coyoteTime = 0.2f;
        private float coyoteTimeCounter;
        public float jumpBufferTime = 0.2f;
        private float jumpBufferCounter;

        [Header("Ground Detection")]
        public LayerMask groundLayer;
        public Transform groundCheckPivot;
        public float groundCheckRadius = 0.25f;

        [Header("Sandbox / Demo Mechanics Modifier")]
        [Tooltip("Self-contained unlock flags for the package evaluation sandbox.")]
        public bool isDashUnlocked = true;
        public int allowedAirJumps = 1;
        public float speedMultiplier = 1f;

        private Rigidbody rb;
        private Vector3 inputDir;
        private bool wasGrounded;

        // --- IMotionStateProvider Implementation ---
        public bool IsGrounded { get; private set; }
        public Vector3 Velocity => rb != null ? rb.linearVelocity : Vector3.zero;

        private Vector3 jumpRecoilPos = new Vector3(0, -0.05f, 0);
        private Vector3 jumpRecoilRot = new Vector3(5f, 0, 0);
        private Vector3 landRecoilPos = new Vector3(0, -0.15f, 0);
        private Vector3 landRecoilRot = new Vector3(10f, 0, 0);

        void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
            rb.constraints = RigidbodyConstraints.FreezeRotation;

            if (playerCamera == null) playerCamera = Camera.main;

            // FPS oyunlarında mouse imlecini ekrana kilitlemek ve gizlemek şarttır
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            if (playerCamera != null && !isDashing)
                playerCamera.fieldOfView = normalFOV;

            GetInput();
            HandleMouseLook(); // Ekleme: Kamerayı ve gövdeyi döndüren fonksiyon

            if (Input.GetButtonDown("Jump")) jumpBufferCounter = jumpBufferTime;
            else jumpBufferCounter -= Time.deltaTime;

            if (isClimbing && Input.GetButtonDown("Jump"))
            {
                isClimbing = false;
                rb.useGravity = false;
                PerformJump();
            }
            else if (!isClimbing)
            {
                HandleJump();
            }

            HandleDash();
            HandleCameraFOV();
        }

        void FixedUpdate()
        {
            GroundCheck();

            if (isDashing)
            {
                // Bypassed during physics translation interpolation coroutine
            }
            else if (isClimbing)
            {
                HandleClimbingPhysics();
            }
            else
            {
                MoveWithAcceleration();
                ApplyAdvancedGravity();
            }

            if (IsGrounded && !wasGrounded) OnLand();
            wasGrounded = IsGrounded;
        }

        void GetInput()
        {
            inputDir = new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical")).normalized;
        }

        void HandleMouseLook()
        {
            // Fare girdilerini alıp hassasiyetle çarpıyoruz
            float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

            // 1. Yatay Eksen (Sağa-Sola): Karakterin tüm Rigidbody gövdesini döndürür
            transform.Rotate(Vector3.up * mouseX);

            // 2. Dikey Eksen (Yukarı-Aşağı): Sadece kafayı (Kamerayı) döndürür
            verticalRotation -= mouseY;
            // Kafanın takla atıp ters dönmesini engellemek için sınırlandırıyoruz (Clamp)
            verticalRotation = Mathf.Clamp(verticalRotation, -lowerLookLimit, upperLookLimit);

            if (playerCamera != null)
            {
                playerCamera.transform.localRotation = Quaternion.Euler(verticalRotation, 0f, 0f);
            }
        }

        bool OnSlope()
        {
            if (Physics.Raycast(transform.position, Vector3.down, out slopeHit, 1.5f, groundLayer))
            {
                float angle = Vector3.Angle(Vector3.up, slopeHit.normal);
                return angle < maxSlopeAngle && angle != 0;
            }
            return false;
        }

        Vector3 GetSlopeMoveDirection(Vector3 direction)
        {
            return Vector3.ProjectOnPlane(direction, slopeHit.normal).normalized;
        }

        void HandleClimbingPhysics()
        {
            float verticalInput = Input.GetAxisRaw("Vertical");
            rb.linearVelocity = new Vector3(0, verticalInput * climbSpeed, 0);
        }

        void OnTriggerEnter(Collider other)
        {
            if (other.CompareTag("Ladder"))
            {
                isClimbing = true;
                rb.linearVelocity = Vector3.zero;
                airJumpCount = 0;
            }
        }

        void OnTriggerExit(Collider other)
        {
            if (other.CompareTag("Ladder")) isClimbing = false;
        }

        void HandleDash()
        {
            if (isDashUnlocked)
            {
                if (Input.GetKeyDown(KeyCode.LeftShift) && Time.time >= lastDashTime + dashCooldown)
                {
                    StartCoroutine(DashRoutine());
                }
            }
        }

        IEnumerator DashRoutine()
        {
            isDashing = true;
            isClimbing = false;
            lastDashTime = Time.time;

            Vector3 dashDir = inputDir.magnitude > 0 ? inputDir : Vector3.forward;
            Vector3 worldDashDir = Camera.main.transform.TransformDirection(dashDir);
            worldDashDir.y = 0;

            rb.linearVelocity = worldDashDir.normalized * dashSpeed;

            if (audioSource && dashSound) audioSource.PlayOneShot(dashSound);

            float startTime = Time.time;
            while (Time.time < startTime + dashDuration)
            {
                if (Input.GetButtonDown("Jump"))
                {
                    PerformJump();
                    isDashing = false;
                    yield break;
                }
                yield return null;
            }

            rb.linearVelocity = rb.linearVelocity.normalized * moveSpeed;
            isDashing = false;
        }

        void HandleCameraFOV()
        {
            if (playerCamera == null) return;
            float targetFOV = isDashing ? dashFOV : normalFOV;
            playerCamera.fieldOfView = Mathf.Lerp(playerCamera.fieldOfView, targetFOV, 1f - Mathf.Exp(-15f * Time.deltaTime));
        }

        void HandleJump()
        {
            if (coyoteTimeCounter > 0f && jumpBufferCounter > 0f)
            {
                PerformJump();
                jumpBufferCounter = 0f;
                airJumpCount = 0;
            }
            else if (Input.GetButtonDown("Jump") && !IsGrounded)
            {
                if (airJumpCount < allowedAirJumps)
                {
                    PerformJump();
                    jumpBufferCounter = 0f;
                    airJumpCount++;
                }
            }
        }

        void PerformJump()
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
            rb.AddForce(Vector3.up * jumpForce, ForceMode.VelocityChange);

            if (weaponAnimator != null) weaponAnimator.ApplyImpact(jumpRecoilPos, jumpRecoilRot);
            if (audioSource && jumpSound) audioSource.PlayOneShot(jumpSound);
        }

        void GroundCheck()
        {
            Vector3 checkPos = groundCheckPivot ? groundCheckPivot.position : transform.position + Vector3.up * 0.1f;
            bool hitGround = Physics.CheckSphere(checkPos, groundCheckRadius, groundLayer);

            if (hitGround)
            {
                IsGrounded = true;
                coyoteTimeCounter = coyoteTime;
                airJumpCount = 0;
            }
            else
            {
                IsGrounded = false;
                coyoteTimeCounter -= Time.fixedDeltaTime;
            }
        }

        void OnLand()
        {
            if (weaponAnimator != null) weaponAnimator.ApplyImpact(landRecoilPos, landRecoilRot);
            if (audioSource && landSound) audioSource.PlayOneShot(landSound);
        }

        void MoveWithAcceleration()
        {
            Vector3 worldDir = transform.TransformDirection(inputDir); // Düzenleme: Bakış yönüne göre gitmesi için Camera.main yerine transform kullandık
            worldDir.y = 0;
            worldDir.Normalize();

            if (OnSlope() && !isDashing)
            {
                worldDir = GetSlopeMoveDirection(worldDir);
            }

            Vector3 targetVel = worldDir * (moveSpeed * speedMultiplier);
            Vector3 currentVel = rb.linearVelocity;

            float accel = IsGrounded ? ((inputDir.magnitude > 0.1f) ? groundAcceleration : groundDeceleration) : airAcceleration;

            if (currentVel.magnitude > (maxGroundSpeed * speedMultiplier) && !IsGrounded)
            {
                if (Vector3.Dot(currentVel.normalized, worldDir) < 0.5f) { }
                else { accel = 0; }
            }

            Vector3 deltaVel = targetVel - currentVel;

            if (!IsGrounded)
            {
                deltaVel.y = 0;
            }

            Vector3 accelForce = deltaVel.normalized * accel * Time.fixedDeltaTime;
            if (accelForce.magnitude > deltaVel.magnitude) accelForce = deltaVel;

            rb.AddForce(accelForce, ForceMode.VelocityChange);

            if (OnSlope() && !isDashing && rb.linearVelocity.y > -1f)
            {
                rb.AddForce(Vector3.down * 80f, ForceMode.Force);
            }
        }

        void ApplyAdvancedGravity()
        {
            if (isClimbing) return;

            if (rb.linearVelocity.y > 0)
            {
                rb.AddForce(Vector3.down * upwardGravity * 10f, ForceMode.Acceleration);
            }
            else
            {
                rb.AddForce(Vector3.down * downwardGravity * 10f, ForceMode.Acceleration);
            }
        }
    }
}