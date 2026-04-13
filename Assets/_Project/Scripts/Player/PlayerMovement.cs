// PlayerMovement.cs
using UnityEngine;

/// <summary>
/// Handles character movement, jumping, crouching, climbing, and state machine logic.
/// Utilizes a Rigidbody with hybrid physics: deterministic SphereCast grounding/drag mixed 
/// with advanced velocity-error kinematics and turn multipliers for optimal game feel.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(CapsuleCollider))]
[RequireComponent(typeof(PlayerInputHandler))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Spring Grounding (Floating Capsule)")]
    [SerializeField] private float _rideHoverOffset = 0.2f;
    [SerializeField] private float _raycastLength = 1.3f; 
    [SerializeField] private float _springStrength = 250f;
    [SerializeField] private float _springDamper = 20f;
    [SerializeField] private LayerMask _groundLayerMask;

    [Header("Movement (Kinematics)")]
    [SerializeField] private float _maxSpeed = 7f;
    [SerializeField] private float _acceleration = 12f;     
    [SerializeField] private float _maxAccelForce = 150f;
    [SerializeField] private float _maxTurnMultiplier = 2f; 
    [SerializeField] private float _sprintMultiplier = 2.5f;
    [SerializeField] private float _crouchMultiplier = 0.5f;
    [SerializeField] private float _slowWalkMultiplier = 0.4f;
    [SerializeField] private float _airControl = 0.4f;

    [Header("Sprint Ramp")]
    [SerializeField] private float _sprintRampSpeed = 3f;

    [Header("Jump Feel")]
    [SerializeField] private float _jumpBufferTime = 0.2f;
    [SerializeField] private float _coyoteTime = 0.15f;
    [SerializeField] private float _jumpCutMultiplier = 0.5f;

    [Header("Jump & Gravity")]
    [SerializeField] private float _jumpForce = 12f;
    [SerializeField] private float _gravityMultiplier = 3.5f;

    [Header("Landing")]
    [SerializeField] private float _hardLandingThreshold = -8f;
    [SerializeField] private float _landingSpeedPenalty = 0.4f;
    [SerializeField] private float _landingPenaltyDuration = 0.2f;

    [Header("Corner Correction")]
    [SerializeField] private float _cornerCorrectionDistance = 0.5f;
    [SerializeField] private float _cornerCorrectionOffset = 0.1f;

    [Header("Corner & Landing Assist")]
    [SerializeField] private float _cornerJumpRadius = 0.15f;
    [SerializeField] private float _cornerJumpPushForce = 3f;
    [SerializeField] private float _landingAssistRadius = 0.4f;
    [SerializeField] private float _landingAssistDistance = 0.5f;
    [SerializeField] private float _landingAssistForce = 5f;

    [Header("Edge Slip Forgiveness")]
    [SerializeField] private float _edgeSlipDelay = 0.25f;

    [Header("Controller Dimensions")]
    [SerializeField] private float _standingHeight = 2f;
    [SerializeField] private float _crouchHeight = 1f;
    [SerializeField] private LayerMask _obstacleMask;

    [Header("Slope Handling")]
    [SerializeField] private float _maxSlopeAngle = 45f;
    [SerializeField] private float _slideSpeed = 8f;
    [SerializeField] private float _slideControl = 0.4f;

    [Header("Vault Settings")]
    [SerializeField] private float _vaultMinHeight = 0.5f;
    [SerializeField] private float _vaultMaxHeight = 1.2f;
    [SerializeField] private float _vaultDuration = 0.25f;
    [SerializeField] private float _vaultDetectionDistance = 0.4f;
    [SerializeField] private LayerMask _vaultMask; 

    // Component References
    private Rigidbody _rb;
    private PlayerInputHandler _inputHandler;
    private CapsuleCollider _collider;
    private PlayerClimb _playerClimb;

    // Internal State
    private bool _isGrounded;
    private Vector3 _groundNormal = Vector3.up;
    private bool _isOnSteepSlope;
    private bool _hasJumpedThisState;
    private bool _jumpBuffered;
    private bool _isVaulting = false;
    private bool _isClimbing => _playerClimb != null && _playerClimb.IsClimbing;
    
    private float _jumpBufferTimer;
    private float _coyoteTimer;
    private float _edgeSlipTimer = 0f;
    private float _currentSpeedMultiplier = 1f;
    private float _landingPenaltyTimer = 0f;
    private bool _touchingSteepSurface = false;

    public PlayerState CurrentState { get; private set; }
    public bool IsOnSteepSlope => _isOnSteepSlope;
    public bool IsGrounded => _isGrounded;
    public bool IsVaulting => _isVaulting;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _playerClimb = GetComponent<PlayerClimb>();

        if (_rb == null || _collider == null || _inputHandler == null)
        {
            Debug.LogError("[PlayerMovement] Required components missing on Awake.");
            return;
        }

        if (_playerClimb == null)
        {
            Debug.LogError("[PlayerMovement] PlayerClimb não encontrado no Awake.");
        }

        _rb.useGravity = false; 
        _rb.interpolation = RigidbodyInterpolation.Interpolate;
        _rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
        _rb.constraints = RigidbodyConstraints.FreezeRotation;
        
        _rb.linearDamping = 0f;
    }

    private void Update()
    {
        if (_inputHandler == null) return;

        if (_inputHandler.JumpPressed)
        {
            _jumpBuffered = true;
            _jumpBufferTimer = _jumpBufferTime;
            _inputHandler.ConsumeJump();
        }

        if (_jumpBuffered)
        {
            _jumpBufferTimer -= Time.deltaTime;
            if (_jumpBufferTimer <= 0f)
                _jumpBuffered = false;
        }

        if (_inputHandler.JumpReleased && _rb.linearVelocity.y > 0f)
        {
            _rb.linearVelocity = new Vector3(
                _rb.linearVelocity.x,
                _rb.linearVelocity.y * _jumpCutMultiplier,
                _rb.linearVelocity.z
            );
        }
    }

    private void FixedUpdate()
    {
        if (_rb == null || _inputHandler == null) return;

        CheckCornerCorrection();
        CheckVaultCondition();
        CheckGround();
        HandleSlopeSlide(); 
        UpdateState();
        ApplyGravity();
        CalculateMovement();
        ApplyJump();
        HandleCrouchHeight();
    }

    private void CheckCornerCorrection()
    {
        if (CurrentState != PlayerState.Jumping || _rb.linearVelocity.y <= 0f) return;

        Vector3 topCenter = transform.position + _collider.center + (Vector3.up * (_collider.height / 2f - _collider.radius));

        bool hitRight = Physics.Raycast(topCenter, transform.right, _cornerCorrectionDistance, _obstacleMask);
        bool hitLeft = Physics.Raycast(topCenter, -transform.right, _cornerCorrectionDistance, _obstacleMask);

        if (hitRight && !hitLeft)
        {
            transform.position += -transform.right * _cornerCorrectionOffset;
        }
        else if (hitLeft && !hitRight)
        {
            transform.position += transform.right * _cornerCorrectionOffset;
        }

        bool hitForward = Physics.Raycast(topCenter, transform.forward, _cornerCorrectionDistance, _obstacleMask);
        bool hitBack = Physics.Raycast(topCenter, -transform.forward, _cornerCorrectionDistance, _obstacleMask);

        if (hitForward && !hitBack)
        {
            transform.position += -transform.forward * _cornerCorrectionOffset;
        }
        else if (hitBack && !hitForward)
        {
            transform.position += transform.forward * _cornerCorrectionOffset;
        }
    }

    private void CheckVaultCondition()
    {
        if (_isVaulting || _isClimbing) return;
        if (!_isGrounded || _inputHandler.MoveInput == Vector2.zero) return;

        Vector3 moveDir = (transform.right * _inputHandler.MoveInput.x + transform.forward * _inputHandler.MoveInput.y).normalized;

        Vector3 lowRayOrigin = transform.position + Vector3.up * _vaultMinHeight;
        Vector3 highRayOrigin = transform.position + Vector3.up * (_standingHeight - 0.1f);

        bool hitLow = Physics.Raycast(lowRayOrigin, moveDir, out RaycastHit lowHit, _vaultDetectionDistance, _vaultMask);
        bool hitHigh = Physics.Raycast(highRayOrigin, moveDir, _vaultDetectionDistance, _vaultMask);

        if (hitLow && !hitHigh)
        {
            StartCoroutine(VaultCoroutine(lowHit.point, moveDir));
        }
    }

    private System.Collections.IEnumerator VaultCoroutine(Vector3 vaultPoint, Vector3 moveDir)
    {
        _isVaulting = true;
        _collider.enabled = false;
        
        Vector3 targetPos = vaultPoint + Vector3.up * _vaultMaxHeight + moveDir * (_collider.radius * 2f);
        Vector3 currentVelocity = Vector3.zero;
        float elapsedTime = 0f;

        _rb.linearVelocity = Vector3.zero;

        float smoothTime = _vaultDuration * 0.5f;

        while (elapsedTime < _vaultDuration)
        {
            transform.position = Vector3.SmoothDamp(transform.position, targetPos, ref currentVelocity, smoothTime);
            elapsedTime += Time.fixedDeltaTime;
            yield return new WaitForFixedUpdate();
        }

        transform.position = targetPos;
        _rb.linearVelocity = Vector3.zero; 

        _collider.enabled = true;
        _isVaulting = false;
    }

    private void CheckGround()
    {
        if (_isVaulting || _isClimbing) return;

        Vector3 rayOrigin = transform.position + _collider.center;
        
        bool rayHit = Physics.Raycast(
            rayOrigin,
            Vector3.down,
            out RaycastHit hit,
            _raycastLength,
            _groundLayerMask,
            QueryTriggerInteraction.Ignore
        );

        if (rayHit)
        {
            _isGrounded = true;
            _coyoteTimer = _coyoteTime;
            _groundNormal = hit.normal;

            if ((CurrentState != PlayerState.Jumping || _rb.linearVelocity.y <= 0f) && !_jumpBuffered)
            {
                float targetRideHeight = _collider.center.y + _rideHoverOffset;
                float heightError = targetRideHeight - hit.distance;

                float verticalVelocity = _rb.linearVelocity.y;

                float springAccel = (heightError * _springStrength) - (verticalVelocity * _springDamper);

                if (springAccel < 0f || _rb.linearVelocity.y > -3f)
                {
                    _rb.AddForce(Vector3.up * springAccel, ForceMode.Acceleration);

                    if (hit.rigidbody != null)
                    {
                        hit.rigidbody.AddForceAtPosition(Vector3.down * (springAccel * _rb.mass), hit.point, ForceMode.Force);
                    }
                }
            }
        }
        else
        {
            if (_coyoteTimer > 0f)
            {
                _coyoteTimer -= Time.fixedDeltaTime;
                _isGrounded = _coyoteTimer > 0f;
            }
            else
            {
                _isGrounded = false;
            }
            _groundNormal = Vector3.up;

            if (_rb.linearVelocity.y < 0f)
            {
                Vector3 sphereCenter = transform.position + _collider.center;
                bool assistHit = Physics.SphereCast(
                    sphereCenter, 
                    _landingAssistRadius, 
                    Vector3.down, 
                    out RaycastHit assistHitInfo, 
                    _landingAssistDistance, 
                    _groundLayerMask, 
                    QueryTriggerInteraction.Ignore
                );

                if (assistHit)
                {
                    Vector3 pushDir = assistHitInfo.point - transform.position; 
                    pushDir.y = 0f;

                    if (pushDir.sqrMagnitude > 0.001f)
                    {
                        _rb.AddForce(pushDir.normalized * _landingAssistForce, ForceMode.Acceleration);
                    }
                }
            }
        }
    }

    private void UpdateState()
    {
        if (_isVaulting || _isClimbing) return;

        if (_isGrounded)
        {
            if ((CurrentState == PlayerState.Falling || CurrentState == PlayerState.Jumping) && _rb.linearVelocity.y <= 0.1f && !_jumpBuffered)
            {
                if (CurrentState == PlayerState.Falling && _rb.linearVelocity.y < _hardLandingThreshold)
                {
                    _landingPenaltyTimer = _landingPenaltyDuration;
                    Vector3 vel = _rb.linearVelocity;
                    vel.x *= _landingSpeedPenalty;
                    vel.z *= _landingSpeedPenalty;
                    _rb.linearVelocity = vel;
                }
                CurrentState = PlayerState.Idle;
                _hasJumpedThisState = false;
            }

            bool wantsToCrouch = _inputHandler.IsCrouching;
            
            if (!wantsToCrouch && CurrentState == PlayerState.Crouching)
            {
                if (CheckCeiling())
                {
                    wantsToCrouch = true;
                }
            }

            if (CurrentState != PlayerState.Jumping || _rb.linearVelocity.y <= 0.1f)
            {
                if (wantsToCrouch)
                {
                    CurrentState = PlayerState.Crouching;
                }
                else if (_inputHandler.IsSprinting && !_isOnSteepSlope && _inputHandler.MoveInput.y > 0.1f)
                {
                    CurrentState = PlayerState.Sprinting;
                }
                else if (_inputHandler.MoveInput != Vector2.zero)
                {
                    CurrentState = PlayerState.Walking;
                }
                else if (CurrentState != PlayerState.Jumping && CurrentState != PlayerState.Falling)
                {
                    CurrentState = PlayerState.Idle;
                }
            }

            if (_jumpBuffered && CurrentState != PlayerState.Crouching && !_isOnSteepSlope)
            {
                if (CurrentState != PlayerState.Jumping)
                {
                    CurrentState = PlayerState.Jumping;
                    _hasJumpedThisState = false; 
                }
            }
        }
        else
        {
            if (_rb.linearVelocity.y < 0f)
            {
                CurrentState = PlayerState.Falling;
            }
        }
    }

    private void ApplyGravity()
    {
        if (_isVaulting || _isClimbing) return;

        if (_rb.linearVelocity.y > 0f)
        {
            _rb.AddForce(Physics.gravity * 2f, ForceMode.Acceleration);
        }
        else
        {
            _rb.AddForce(Physics.gravity * _gravityMultiplier, ForceMode.Acceleration);
        }
    }

    private void CalculateMovement()
    {
        if (_isVaulting || _isClimbing) return;

        float targetSpeed = _maxSpeed;
        float desiredMultiplier = 1f;

        switch (CurrentState)
        {
            case PlayerState.Sprinting:
                desiredMultiplier = _sprintMultiplier;
                break;
            case PlayerState.Crouching:
                desiredMultiplier = _crouchMultiplier;
                break;
        }

        _currentSpeedMultiplier = Mathf.Lerp(_currentSpeedMultiplier, desiredMultiplier, Time.fixedDeltaTime * _sprintRampSpeed);
        targetSpeed *= _currentSpeedMultiplier;

        if (_inputHandler.IsWalking && !_inputHandler.IsSprinting && CurrentState != PlayerState.Crouching)
        {
            targetSpeed *= _slowWalkMultiplier;
        }

        Vector3 moveDirection = (transform.right * _inputHandler.MoveInput.x) + (transform.forward * _inputHandler.MoveInput.y);

        if (moveDirection.magnitude > 1f)
        {
            moveDirection.Normalize();
        }

        Vector3 desiredVelocity = moveDirection * targetSpeed;
        Vector3 currentHorizontalVel = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);

        Vector3 velocityError = desiredVelocity - currentHorizontalVel;

        float turnDot = 1f;
        if (currentHorizontalVel.sqrMagnitude > 0.1f && desiredVelocity.sqrMagnitude > 0.1f)
        {
            turnDot = Vector3.Dot(currentHorizontalVel.normalized, desiredVelocity.normalized);
        }
        
        float turnMultiplier = Mathf.Lerp(_maxTurnMultiplier, 1f, (turnDot + 1f) / 2f);

        float accelRate = _acceleration * turnMultiplier;
        if (!_isGrounded) accelRate *= _airControl; 

        if (_landingPenaltyTimer > 0f)
        {
            _landingPenaltyTimer -= Time.fixedDeltaTime;
            accelRate *= _landingSpeedPenalty;
        }

        Vector3 desiredAccel = velocityError * accelRate;

        float currentMaxAccel = _maxAccelForce * turnMultiplier;
        desiredAccel = Vector3.ClampMagnitude(desiredAccel, currentMaxAccel);

        if (_isOnSteepSlope)
        {
            Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
            float uphill = Vector3.Dot(desiredAccel.normalized, slideDir);
            
            if (uphill < 0f)
            {
                desiredAccel = Vector3.zero;
                
                Vector3 currentHorizontal = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
                float existingUphill = Vector3.Dot(currentHorizontal.normalized, slideDir);
                if (existingUphill < 0f)
                {
                    _rb.linearVelocity = new Vector3(
                        _rb.linearVelocity.x * 0.85f,
                        _rb.linearVelocity.y,
                        _rb.linearVelocity.z * 0.85f
                    );
                }
            }
            else 
            {
                desiredAccel *= _slideControl;
            }
        }

        _rb.AddForce(desiredAccel, ForceMode.Acceleration);
    }

    private bool CheckCornerJump(out Vector3 pushDirection)
    {
        pushDirection = Vector3.zero;
        Vector3 bottomEdge = transform.position + _collider.center + (Vector3.down * (_collider.height / 2f));
        
        Collider[] hits = Physics.OverlapSphere(bottomEdge, _cornerJumpRadius, _groundLayerMask);
        if (hits.Length > 0)
        {
            Vector3 closestPoint = hits[0].ClosestPoint(bottomEdge);
            Vector3 dir = bottomEdge - closestPoint;
            dir.y = 0f;
            
            if (dir.sqrMagnitude > 0.001f)
            {
                pushDirection = dir.normalized;
            }
            else
            {
                pushDirection = transform.forward; 
            }
            return true;
        }
        return false;
    }

    private void ApplyJump()
    {
        if (_isVaulting || _isClimbing) return;

        bool normalJumpReady = (CurrentState == PlayerState.Jumping && _jumpBuffered && !_hasJumpedThisState);
        bool cornerJumpReady = false;
        Vector3 cornerPushDir = Vector3.zero;

        if (!normalJumpReady && _jumpBuffered && !_isGrounded && _rb.linearVelocity.y <= 0f)
        {
            cornerJumpReady = CheckCornerJump(out cornerPushDir);
        }

        if (normalJumpReady || cornerJumpReady)
        {
            _rb.linearVelocity = new Vector3(_rb.linearVelocity.x, 0f, _rb.linearVelocity.z);
            _rb.AddForce(Vector3.up * _jumpForce, ForceMode.VelocityChange);
            
            if (cornerJumpReady && cornerPushDir != Vector3.zero)
            {
                _rb.AddForce(cornerPushDir * _cornerJumpPushForce, ForceMode.VelocityChange);
            }

            _hasJumpedThisState = true;
            _jumpBuffered = false; 
            _coyoteTimer = 0f; 
        }
    }

    private void HandleSlopeSlide()
    {
        if (_isGrounded)
        {
            float slopeAngle = Vector3.Angle(Vector3.up, _groundNormal);
            if (slopeAngle > _maxSlopeAngle || _touchingSteepSurface)
            {
                _isOnSteepSlope = true;
                _edgeSlipTimer += Time.fixedDeltaTime;
            }
            else
            {
                _isOnSteepSlope = false;
                _edgeSlipTimer = 0f;
            }
        }
        else
        {
            _isOnSteepSlope = _touchingSteepSurface;
            _edgeSlipTimer = 0f;
        }

        if (_isOnSteepSlope)
        {
            Vector3 slideDir = Vector3.ProjectOnPlane(Vector3.down, _groundNormal).normalized;
            Vector3 currentVel = _rb.linearVelocity;
            
            float uphillComponent = Vector3.Dot(new Vector3(currentVel.x, 0f, currentVel.z), slideDir);
            
            if (uphillComponent < 0f)
            {
                Vector3 uphillVec = slideDir * uphillComponent;
                _rb.linearVelocity = new Vector3(
                    currentVel.x - uphillVec.x,
                    currentVel.y,
                    currentVel.z - uphillVec.z
                );
            }

            if (_edgeSlipTimer > _edgeSlipDelay)
            {
                _rb.AddForce(slideDir * _slideSpeed, ForceMode.Acceleration);
            }
        }
    }

    private void HandleCrouchHeight()
    {
        float minHeight = _collider.radius * 2f;
        float targetHeight = (CurrentState == PlayerState.Crouching) ? Mathf.Max(_crouchHeight, minHeight) : Mathf.Max(_standingHeight, minHeight);
        
        if (Mathf.Abs(_collider.height - targetHeight) > 0.001f)
        {
            if (CurrentState == PlayerState.Crouching)
            {
                _collider.height = targetHeight;
                _collider.center = new Vector3(0f, targetHeight / 2f, 0f);
            }
            else
            {
                _collider.center = new Vector3(0f, targetHeight / 2f, 0f);
                _collider.height = targetHeight;
            }
        }
    }

    private bool CheckCeiling()
    {
        Vector3 sphereCenter = transform.position + _collider.center + (Vector3.up * _collider.radius);
        float maxDistance = _standingHeight - _collider.height;
        
        return Physics.SphereCast(
            sphereCenter, 
            _collider.radius, 
            Vector3.up, 
            out _, 
            maxDistance, 
            _obstacleMask, 
            QueryTriggerInteraction.Ignore
        );
    }

    private void OnCollisionStay(Collision collision)
    {
        // FIX: Ignora colisões com objetos Climbable — tratados pelo vault
        // NÃO REMOVER esta linha ao regenerar o arquivo
        if (((1 << collision.gameObject.layer) & _vaultMask) != 0) return;

        _touchingSteepSurface = false;
        foreach (ContactPoint contact in collision.contacts)
        {
            float angle = Vector3.Angle(Vector3.up, contact.normal);
            if (angle > _maxSlopeAngle)
            {
                _touchingSteepSurface = true;
                _groundNormal = contact.normal;
                break;
            }
        }
    }
}