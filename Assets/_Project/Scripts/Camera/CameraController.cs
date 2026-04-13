// CameraController.cs
using UnityEngine;

/// <summary>
/// Handles first-person camera rotation, FOV shifting, and visual smoothing 
/// for crouch, landing bob, look-ahead, and vaulting mechanics.
/// </summary>
public class CameraController : MonoBehaviour
{
    [Header("Look Settings")]
    [SerializeField] private float _mouseSensitivity = 0.15f;
    [SerializeField] private float _verticalClampMin = -80f;
    [SerializeField] private float _verticalClampMax = 80f;

    [Header("Deadzones")]
    [SerializeField] private float _lookDeadzone = 0.05f;

    [Header("Look Smoothing")]
    [SerializeField] private float _lookSmoothTime = 0.05f;

    [Header("References")]
    [Tooltip("Assign the CameraRoot transform here. Do not assign the Main Camera directly if using Cinemachine.")]
    [SerializeField] private Transform _cameraRoot;
    [SerializeField] private Camera _camera;

    [Header("Crouch Visuals")]
    [SerializeField] private float _crouchCameraHeight = 0.0f;
    [SerializeField] private float _standingCameraHeight = 0.6f;
    [SerializeField] private float _crouchCameraTransitionSpeed = 8f;

    [Header("Landing Bob")]
    [SerializeField] private float _landingBobAmount = 0.08f;
    [SerializeField] private float _landingBobSpeed = 10f;
    [SerializeField] private float _hardLandingThreshold = -8f;

    [Header("Vault Dip")]
    [SerializeField] private float _vaultDipAmount = 0.06f;
    [SerializeField] private float _vaultDipSpeed = 12f;

    [Header("FOV")]
    [SerializeField] private float _baseFOV = 60f;
    [SerializeField] private float _sprintFOVAdd = 10f;
    [SerializeField] private float _fovLerpSpeed = 5f;

    [Header("Look Ahead")]
    [SerializeField] private float _lookAheadAmount = 0.15f;
    [SerializeField] private float _lookAheadSpeed = 3f;

    [Header("Head Bob")]
    [SerializeField] private float _bobFrequencyWalk = 1.8f;
    [SerializeField] private float _bobAmplitudeYWalk = 0.04f;
    [SerializeField] private float _bobAmplitudeXWalk = 0.02f;
    [SerializeField] private float _bobFrequencySprint = 2.8f;
    [SerializeField] private float _bobAmplitudeYSprint = 0.06f;
    [SerializeField] private float _bobAmplitudeXSprint = 0.03f;
    [SerializeField] private float _bobReturnSpeed = 8f;

    [Header("Strafing Tilt")]
    [SerializeField] private float _strafeTiltAngle = 2f;
    [SerializeField] private float _strafeTiltSpeed = 6f;

    [Header("Anti-Clip")]
    [SerializeField] private float _clipSphereRadius = 0.15f;
    [SerializeField] private float _clipDistance = 0.25f;
    // CORREÇÃO: Removido padrão ~0 para evitar detecção do próprio jogador. Configurar no Inspector.
    [SerializeField] private LayerMask _clipMask; 

    private PlayerInputHandler _inputHandler;
    private PlayerMovement _playerMovement;
    private Rigidbody _playerRb;
    private PlayerClimb _playerClimb;

    private float _verticalRotation;
    
    // Rastreamento de Estado
    private float _currentYHeight;
    private float _landingBobTimer = 0f;
    private float _currentBobMultiplier = 1f;
    private bool _wasGrounded = false;
    private float _lastVelocityY = 0f;
    private Vector3 _lookAheadOffset = Vector3.zero;
    
    private Vector2 _smoothedLookInput;
    private Vector2 _lookInputVelocity;

    private float _bobTimer = 0f;
    private Vector3 _bobOffset = Vector3.zero;
    private float _currentTilt = 0f;

    private float _vaultDipOffset = 0f;
    private bool _wasVaulting = false;

    private bool _isClimbing => _playerClimb != null && _playerClimb.IsClimbing;
    private float _climbYaw = 0f;
    private bool _wasClimbing = false;

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _playerMovement = GetComponent<PlayerMovement>();
        _playerRb = GetComponent<Rigidbody>(); 
        _playerClimb = GetComponent<PlayerClimb>();

        if (_inputHandler == null || _playerMovement == null || _cameraRoot == null || _playerRb == null)
        {
            Debug.LogError("[CameraController] Missing core references on Awake.");
            return;
        }

        if (_playerClimb == null)
        {
            Debug.LogError("[CameraController] PlayerClimb não encontrado no Awake.");
        }

        _currentYHeight = _standingCameraHeight;

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Update()
    {
        if (_inputHandler == null || _playerMovement == null || _cameraRoot == null) return;

        if (_wasClimbing && !_isClimbing)
        {
            // Corrige o Yaw
            // Transfere o offset visual da câmera para a rotação do PlayerRoot no instante em que solta a parede.
            // Isso previne que a câmera de um "snap" violento de volta para o centro.
            transform.Rotate(Vector3.up * _climbYaw);
            _climbYaw = 0f;
        }
        _wasClimbing = _isClimbing;

        HandleRotation();
        HandleFOV();
        HandleLandingBob();
        HandleHeadBob();
        HandleStrafeTilt();
        HandleVaultDip();
        
        ApplyCameraPosition();

        _lastVelocityY = _playerRb.linearVelocity.y;
    }

    private void HandleRotation()
    {
        Vector2 lookInput = _inputHandler.LookInput;

        if (Mathf.Abs(lookInput.x) < _lookDeadzone) lookInput.x = 0f;
        if (Mathf.Abs(lookInput.y) < _lookDeadzone) lookInput.y = 0f;

        if (_isClimbing)
        {
            _smoothedLookInput.x = Mathf.SmoothDamp(_smoothedLookInput.x, lookInput.x, ref _lookInputVelocity.x, _lookSmoothTime);
            _smoothedLookInput.y = Mathf.SmoothDamp(_smoothedLookInput.y, lookInput.y, ref _lookInputVelocity.y, _lookSmoothTime);

            _climbYaw += _smoothedLookInput.x * _mouseSensitivity;
            _climbYaw = Mathf.Clamp(_climbYaw, -80f, 80f);

            _verticalRotation -= _smoothedLookInput.y * _mouseSensitivity;
            _verticalRotation = Mathf.Clamp(_verticalRotation, _verticalClampMin, _verticalClampMax);

            return;
        }

        _smoothedLookInput.x = Mathf.SmoothDamp(_smoothedLookInput.x, lookInput.x, ref _lookInputVelocity.x, _lookSmoothTime);
        _smoothedLookInput.y = Mathf.SmoothDamp(_smoothedLookInput.y, lookInput.y, ref _lookInputVelocity.y, _lookSmoothTime);

        transform.Rotate(Vector3.up * _smoothedLookInput.x * _mouseSensitivity);

        _verticalRotation -= _smoothedLookInput.y * _mouseSensitivity;
        _verticalRotation = Mathf.Clamp(_verticalRotation, _verticalClampMin, _verticalClampMax);
        
        Vector3 targetOffset = Vector3.right * _inputHandler.MoveInput.x * _lookAheadAmount;
        _lookAheadOffset = Vector3.Lerp(_lookAheadOffset, targetOffset, _lookAheadSpeed * Time.deltaTime);
    }

    private void HandleFOV()
    {
        if (_camera == null) return;

        if (_isClimbing)
        {
            _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, _baseFOV, _fovLerpSpeed * Time.deltaTime);
            return;
        }

        float targetFOV = _baseFOV;
        
        if (_playerMovement.CurrentState == PlayerState.Sprinting && _inputHandler.MoveInput.y > 0.1f)
        {
            targetFOV += _sprintFOVAdd;
        }

        _camera.fieldOfView = Mathf.Lerp(_camera.fieldOfView, targetFOV, _fovLerpSpeed * Time.deltaTime);
    }

    private void HandleLandingBob()
    {
        if (_isClimbing) return;

        bool isGrounded = _playerMovement.IsGrounded;

        if (!_wasGrounded && isGrounded)
        {
            _landingBobTimer = Mathf.PI / _landingBobSpeed;
            _currentBobMultiplier = (_lastVelocityY < _hardLandingThreshold) ? 2f : 1f;
        }

        if (_landingBobTimer > 0f)
        {
            _landingBobTimer -= Time.deltaTime;
            if (_landingBobTimer < 0f) _landingBobTimer = 0f;
        }

        _wasGrounded = isGrounded;
    }

    private void HandleHeadBob()
    {
        if (_isClimbing)
        {
            _bobTimer = 0f;
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, _bobReturnSpeed * Time.deltaTime);
            return;
        }

        if (_playerMovement.IsGrounded && _inputHandler.MoveInput != Vector2.zero && _playerMovement.CurrentState != PlayerState.Crouching)
        {
            float freq = (_playerMovement.CurrentState == PlayerState.Sprinting) ? _bobFrequencySprint : _bobFrequencyWalk;
            float ampY = (_playerMovement.CurrentState == PlayerState.Sprinting) ? _bobAmplitudeYSprint : _bobAmplitudeYWalk;
            float ampX = (_playerMovement.CurrentState == PlayerState.Sprinting) ? _bobAmplitudeXSprint : _bobAmplitudeXWalk;

            _bobTimer += Time.deltaTime * freq;
            float bobY = Mathf.Sin(_bobTimer) * ampY;
            float bobX = Mathf.Cos(_bobTimer * 0.5f) * ampX;

            _bobOffset = Vector3.Lerp(_bobOffset, new Vector3(bobX, bobY, 0f), 12f * Time.deltaTime);
        }
        else
        {
            _bobTimer = 0f;
            _bobOffset = Vector3.Lerp(_bobOffset, Vector3.zero, _bobReturnSpeed * Time.deltaTime);
        }
    }

    private void HandleStrafeTilt()
    {
        if (_isClimbing)
        {
            _currentTilt = Mathf.Lerp(_currentTilt, 0f, _strafeTiltSpeed * Time.deltaTime);
            return;
        }

        float targetTilt = -_inputHandler.MoveInput.x * _strafeTiltAngle;
        _currentTilt = Mathf.Lerp(_currentTilt, targetTilt, _strafeTiltSpeed * Time.deltaTime);
    }

    private void HandleVaultDip()
    {
        if (_isClimbing) return;

        if (_playerMovement.IsVaulting && !_wasVaulting)
        {
            _vaultDipOffset = -_vaultDipAmount;
        }
        _vaultDipOffset = Mathf.Lerp(_vaultDipOffset, 0f, _vaultDipSpeed * Time.deltaTime);
        _wasVaulting = _playerMovement.IsVaulting;
    }

    private void ApplyCameraPosition()
    {
        float targetHeight = (_playerMovement.CurrentState == PlayerState.Crouching) ? _crouchCameraHeight : _standingCameraHeight;
        _currentYHeight = Mathf.Lerp(_currentYHeight, targetHeight, _crouchCameraTransitionSpeed * Time.deltaTime);

        float bobOffset = 0f;
        if (_landingBobTimer > 0f)
        {
            bobOffset = -Mathf.Sin(_landingBobTimer * _landingBobSpeed) * (_landingBobAmount * _currentBobMultiplier);
        }

        _cameraRoot.localPosition = new Vector3(
            _lookAheadOffset.x + _bobOffset.x,
            _currentYHeight + bobOffset + _bobOffset.y + _vaultDipOffset,
            _lookAheadOffset.z
        );
        
        _cameraRoot.localRotation = Quaternion.Euler(
            _verticalRotation,
            _climbYaw,
            _currentTilt
        );

        // Anti-clip com SphereCast.
        // Puxa a câmera filha (_camera) temporariamente para trás caso o near-clip tente atravessar a parede.
        Vector3 clipRayOrigin = _cameraRoot.position - _cameraRoot.forward * _clipDistance;
        
        if (Physics.SphereCast(clipRayOrigin, _clipSphereRadius, _cameraRoot.forward, out RaycastHit hit, _clipDistance, _clipMask, QueryTriggerInteraction.Ignore))
        {
            float pushBack = _clipDistance - hit.distance;
            _camera.transform.localPosition = new Vector3(0f, 0f, -pushBack);
        }
        else
        {
            _camera.transform.localPosition = Vector3.Lerp(_camera.transform.localPosition, Vector3.zero, Time.deltaTime * 15f);
        }
    }
}
