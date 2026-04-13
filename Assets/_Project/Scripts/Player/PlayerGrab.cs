using UnityEngine;

/// <summary>
/// Handles grabbing, holding, and throwing physical objects (Rigidbodies).
/// Operates entirely via physics forces to preserve collision integrity and prevent feedback loops.
/// </summary>
[RequireComponent(typeof(PlayerInputHandler))]
[RequireComponent(typeof(Collider))]
public class PlayerGrab : MonoBehaviour
{
    [Header("Grab Settings")]
    [SerializeField] private float _grabDistance = 3f;
    [SerializeField] private float _holdSpring = 25f;
    [SerializeField] private float _maxHoldSpeed = 20f;
    [SerializeField] private float _throwForce = 20f;
    [SerializeField] private float _maxGrabMass = 20f;
    [SerializeField] private LayerMask _grabbableMask;

    [Header("References")]
    [SerializeField] private Transform _grabPoint;

    private Rigidbody _heldObject;
    private Collider _heldCollider;
    private Collider _collider;
    private PlayerInputHandler _inputHandler;
    private PlayerInteraction _playerInteraction;
    private Camera _camera;
    private float _grabCooldown = 0f;
    
    // Suaviza o alvo de grab para evitar jitter com movimentos bruscos da camera
    private Vector3 _smoothedGrabTarget;
    // Armazena a interpolacao original do objeto para restaurar ao soltar
    private RigidbodyInterpolation _heldObjectOriginalInterpolation;

    /// <summary>
    /// Gets a value indicating whether the player is currently holding an object.
    /// </summary>
    public bool IsHoldingObject => _heldObject != null;

    /// <summary>
    /// Gets the Rigidbody of the currently held object.
    /// </summary>
    public Rigidbody HeldObject => _heldObject;

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _playerInteraction = GetComponent<PlayerInteraction>();
        _collider = GetComponent<Collider>();
        
        if (_inputHandler == null)
        {
            Debug.LogError("[PlayerGrab] PlayerInputHandler is missing on Awake.");
        }

        if (_playerInteraction == null)
        {
            Debug.LogError("[PlayerGrab] PlayerInteraction is missing on Awake.");
        }

        if (_collider == null)
        {
            Debug.LogError("[PlayerGrab] Collider is missing on Awake.");
        }

        if (_grabPoint == null)
        {
            Debug.LogError("[PlayerGrab] _grabPoint is not assigned in the Inspector.");
        }

        _camera = GetComponentInChildren<Camera>();
        if (_camera == null)
        {
            Debug.LogError("[PlayerGrab] Camera is missing in children on Awake.");
        }
    }

    private void Update()
    {
        if (_inputHandler == null || _camera == null) return;

        HandleGrabInput();
    }

    private void FixedUpdate()
    {
        if (!IsHoldingObject) return;

        float distance = Vector3.Distance(_heldObject.position, _grabPoint.position);
        
        // Auto-release se o objeto ficar muito longe (preso em algum cenario)
        if (distance > _grabDistance * 1.5f)
        {
            ReleaseObject(false);
            return;
        }

        MoveHeldObject();
    }

    /// <summary>
    /// Processes player input to grab, drop, or throw objects, including a cooldown to prevent instant re-grabs.
    /// Evaluates interactions on the held object if the interact button is pressed.
    /// </summary>
    private void HandleGrabInput()
    {
        if (_grabCooldown > 0f)
        {
            _grabCooldown -= Time.deltaTime;
            return;
        }

        if (_inputHandler.IsGrabbing && !IsHoldingObject)
        {
            TryGrab();
            return;
        }

        if (IsHoldingObject)
        {
            // Throw takes priority over drop
            if (_inputHandler.ThrowPressed)
            {
                ReleaseObject(true);
                _grabCooldown = 0.3f; // Bloqueia re-grab por 0.3s após throw
                return;
            }

            if (!_inputHandler.IsGrabbing)
            {
                ReleaseObject(false);
                return;
            }

            // Interact with the held object if requested
            if (_inputHandler.InteractPressed)
            {
                IInteractable interactable = _heldObject.GetComponent<IInteractable>();
                if (interactable != null)
                {
                    interactable.Interact(_playerInteraction);
                    // Não chama ReleaseObject — o Interact decide se desativa o objeto
                }
            }
        }
    }

    /// <summary>
    /// Casts a sphere from the camera to detect and attach a valid Rigidbody.
    /// </summary>
    private void TryGrab()
    {
        if (Physics.SphereCast(_camera.transform.position, 0.3f, _camera.transform.forward, out RaycastHit hit, _grabDistance, _grabbableMask))
        {
            Rigidbody rb = hit.collider.attachedRigidbody;
            
            if (rb != null && rb.mass <= _maxGrabMass)
            {
                _heldObject = rb;
                _heldObject.useGravity = false;
                
                // Use linearDamping (Unity 6+) to prevent bouncing/oscillation
                _heldObject.linearDamping = 8f; 
                
                // Garante que o Rigidbody seja interpolado para evitar ghosting visual
                _heldObjectOriginalInterpolation = _heldObject.interpolation;
                _heldObject.interpolation = RigidbodyInterpolation.Interpolate;

                // Ignora colisao entre player e objeto segurado para evitar feedback loop na fisica
                Physics.IgnoreCollision(_collider, hit.collider, true);
                _heldCollider = hit.collider;

                // Inicializa o target suavizado na posicao exata do GrabPoint no momento do grab
                _smoothedGrabTarget = _grabPoint.position;
            }
        }
    }

    /// <summary>
    /// Applies force to the held object to pull it towards the smoothed grab target.
    /// Uses direct snapping via MovePosition when extremely close to prevent micro-oscillations and ghosting.
    /// </summary>
    private void MoveHeldObject()
    {
        // Suaviza o alvo para nao teleportar quando a camera gira rapido
        _smoothedGrabTarget = Vector3.Lerp(_smoothedGrabTarget, _grabPoint.position, Time.fixedDeltaTime * 20f);
        
        Vector3 direction = _smoothedGrabTarget - _heldObject.position;
        float distance = direction.magnitude;

        // Muito perto: snap direto respeitando interpolacao, sem spring
        if (distance < 0.05f)
        {
            _heldObject.linearVelocity = Vector3.zero;
            _heldObject.MovePosition(_smoothedGrabTarget); // respeita interpolacao e previne ghosting
            _heldObject.angularVelocity = Vector3.zero;
            return;
        }

        // direction.normalized desacopla a magnitude do vetor de direção
        Vector3 targetVelocity = direction.normalized * Mathf.Min(distance * _holdSpring, _maxHoldSpeed);
        Vector3 velocityDelta = targetVelocity - _heldObject.linearVelocity;
        
        // Uso da ForceMode.VelocityChange conforme padrao de forcas fisicas do projeto
        _heldObject.AddForce(velocityDelta, ForceMode.VelocityChange);
        _heldObject.angularVelocity = Vector3.zero;

        // Damping progressivo conforme se aproxima
        float dampFactor = Mathf.Lerp(0.6f, 1f, distance / 0.5f);
        _heldObject.linearVelocity *= dampFactor;
    }

    /// <summary>
    /// Releases the held object, restoring its original physics properties.
    /// Optionally applies an impulse force to throw it.
    /// </summary>
    /// <param name="throwObject">If true, adds a forward impulse force.</param>
    private void ReleaseObject(bool throwObject)
    {
        _heldObject.useGravity = true;
        _heldObject.linearDamping = 0f;

        if (throwObject)
        {
            _heldObject.linearVelocity = Vector3.zero;
            _heldObject.AddForce(_camera.transform.forward * _throwForce, ForceMode.Impulse);
        }
        else
        {
            // Preserve momentum from camera movement on drop
            _heldObject.linearVelocity = _camera.transform.forward * Mathf.Clamp(_heldObject.linearVelocity.magnitude, 0f, 5f);
        }

        _heldObject.angularVelocity = Vector3.zero;
        
        // Restaura a interpolacao original do objeto
        _heldObject.interpolation = _heldObjectOriginalInterpolation;
        _heldObject = null;

        // Restaura a colisao entre o player e o objeto
        if (_heldCollider != null)
        {
            Physics.IgnoreCollision(_collider, _heldCollider, false);
            _heldCollider = null;
        }
    }
}