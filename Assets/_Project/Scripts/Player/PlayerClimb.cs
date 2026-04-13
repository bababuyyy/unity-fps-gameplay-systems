// PlayerClimb.cs
using UnityEngine;
using System.Collections;

/// <summary>
/// Sistema de escalada livre para o jogador. Gerencia detecção de superfícies, 
/// aderência contínua, movimento projetado no plano da parede, saídas (wall jump, hop up, fall)
/// e ledge grab (subida automática em bordas).
/// </summary>
[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public class PlayerClimb : MonoBehaviour
{
    [Header("Detecção de Superfície")]
    [SerializeField] private float _wallCheckDistance = 0.6f;
    [SerializeField] private float _wallCheckRadius = 0.3f;
    [SerializeField] private LayerMask _climbableMask;
    [SerializeField] private float _minWallAngle = 70f;
    [SerializeField] private float _minApproachSpeed = 4f;

    [Header("Aderência")]
    [SerializeField] private float _wallOffset = 0.45f;
    [SerializeField] private float _wallReattachDistance = 0.8f;
    [SerializeField] private float _snapRotationSpeed = 10f;
    [SerializeField] private float _climbCooldownDuration = 0.3f;

    [Header("Movimento na Parede")]
    [SerializeField] private float _climbSpeed = 3f;

    [Header("Wall Jump")]
    [SerializeField] private float _wallJumpHorizontalForce = 6f;
    [SerializeField] private float _wallJumpVerticalForce = 7f;

    [Header("Hop Up")]
    [SerializeField] private float _hopUpForce = 3f;

    [Header("Ledge Grab")]
    [SerializeField] private float _chestHeight = 0.6f;
    [SerializeField] private float _foreheadHeight = 1.4f;
    [SerializeField] private float _ledgeCheckHeight = 2f;
    [SerializeField] private float _ledgeGrabDuration = 0.4f;
    [SerializeField] private LayerMask _ledgeGroundMask; // Default + Ground + Climbable

    private Rigidbody _rb;
    private CapsuleCollider _collider;
    
    // Referências aos scripts existentes exigidos pelo escopo
    private PlayerInputHandler _inputHandler;
    private PlayerMovement _playerMovement;

    private Vector3 _wallNormal;
    private Vector3 _wallPoint;
    private float _climbCooldown = 0f;
    private bool _climbJumpBuffered = false;
    private bool _isLedgeGrabbing = false;

    public bool IsClimbing { get; private set; }
    public Vector3 WallNormal => _wallNormal;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _collider = GetComponent<CapsuleCollider>();
        _inputHandler = GetComponent<PlayerInputHandler>();
        _playerMovement = GetComponent<PlayerMovement>();

        if (_rb == null) Debug.LogError("[PlayerClimb] Rigidbody não encontrado no Awake.");
        if (_collider == null) Debug.LogError("[PlayerClimb] CapsuleCollider não encontrado no Awake.");
        if (_inputHandler == null) Debug.LogError("[PlayerClimb] PlayerInputHandler não encontrado no Awake.");
        if (_playerMovement == null) Debug.LogError("[PlayerClimb] PlayerMovement não encontrado no Awake.");
    }

    private void Update()
    {
        if (_inputHandler == null) return;

        if (_inputHandler.JumpPressed && !IsClimbing)
        {
            _climbJumpBuffered = true;
        }
    }

    private void FixedUpdate()
    {
        if (_climbCooldown > 0f)
        {
            _climbCooldown -= Time.fixedDeltaTime;
        }

        if (IsClimbing)
        {
            if (!CheckWallAdhesion())
            {
                Fall();
                return;
            }

            // Verifica ledge grab antes dos outros comportamentos
            if (CheckLedgeGrab())
            {
                return;
            }

            CheckExits();

            if (IsClimbing)
            {
                HandleClimbMovement();
            }
        }
        else
        {
            if (_climbCooldown <= 0f)
            {
                CheckForClimbableSurface();
            }
        }
    }

    /// <summary>
    /// Verifica se há uma superfície válida para iniciar a escalada.
    /// </summary>
    private void CheckForClimbableSurface()
    {
        if (_isLedgeGrabbing) return;
        if (_playerMovement != null && _playerMovement.IsVaulting) return;

        bool shouldCheck = _climbJumpBuffered;

        // Caminho B: aproximação por velocidade, sem precisar de jump
        if (!shouldCheck && !_playerMovement.IsGrounded && _rb.linearVelocity.sqrMagnitude > (_minApproachSpeed * _minApproachSpeed))
        {
            float forwardDot = Vector3.Dot(_rb.linearVelocity.normalized, transform.forward);
            if (forwardDot > 0.5f)
            {
                shouldCheck = true;
            }
        }

        if (!shouldCheck) return;

        Vector3 origin = transform.position + _collider.center;

        // Tenta primeiro na direção forward (perto da parede)
        bool found = Physics.SphereCast(origin, _wallCheckRadius, transform.forward, out RaycastHit hit, _wallCheckDistance, _climbableMask);

        // Se não encontrou e está no ar, tenta na direção da velocidade com distância maior
        if (!found && !_playerMovement.IsGrounded && _rb.linearVelocity.sqrMagnitude > 1f)
        {
            Vector3 velDir = _rb.linearVelocity.normalized;
            found = Physics.SphereCast(origin, _wallCheckRadius, velDir, out hit, _wallCheckDistance * 2f, _climbableMask);
        }

        if (found)
        {
            float wallAngle = Vector3.Angle(Vector3.up, hit.normal);
            if (wallAngle >= _minWallAngle)
            {
                _inputHandler.ConsumeJump();
                EnterClimb(hit);
                _climbJumpBuffered = false;
                return;
            }
        }

        _climbJumpBuffered = false; // Limpa o buffer mesmo sem parede
    }

    /// <summary>
    /// Configura o estado inicial da escalada e adere o jogador à parede.
    /// </summary>
    private void EnterClimb(RaycastHit hit)
    {
        IsClimbing = true;
        _rb.useGravity = false;
        _rb.linearVelocity = Vector3.zero;

        // Ignora colisão entre Player e Climbable para evitar jitter do physics solver
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMaskToLayer(_climbableMask), true);

        _wallNormal = hit.normal;
        _wallPoint = hit.point;

        // Mantém a altura atual do jogador, só corrige distância perpendicular à parede
        float perpDist = Vector3.Dot(_rb.position - _wallPoint, _wallNormal);
        Vector3 targetPosition = _rb.position + _wallNormal * (_wallOffset - perpDist);
        _rb.MovePosition(targetPosition);
    }

    /// <summary>
    /// Raycast contínuo para manter a aderência do jogador à parede e atualizar a normal.
    /// </summary>
    private bool CheckWallAdhesion()
    {
        Vector3 origin = transform.position + _collider.center;

        if (Physics.SphereCast(origin, _wallCheckRadius, -_wallNormal, out RaycastHit hit, _wallReattachDistance, _climbableMask))
        {
            float wallAngle = Vector3.Angle(Vector3.up, hit.normal);
            if (wallAngle < _minWallAngle)
            {
                return false;
            }

            _wallNormal = hit.normal;
            _wallPoint = hit.point;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Verifica se o jogador atingiu o topo da parede e inicia o ledge grab.
    /// </summary>
    private bool CheckLedgeGrab()
    {
        Vector3 chestOrigin = transform.position + Vector3.up * _chestHeight;
        Vector3 foreheadOrigin = transform.position + Vector3.up * _foreheadHeight;

        bool chestHit = Physics.SphereCast(chestOrigin, _wallCheckRadius, -_wallNormal, out _, _wallCheckDistance, _climbableMask);
        bool foreheadHit = Physics.SphereCast(foreheadOrigin, _wallCheckRadius, -_wallNormal, out _, _wallCheckDistance, _climbableMask);

        if (chestHit && !foreheadHit)
        {
            // Parte de cima do jogador deslocada horizontalmente sobre a borda
            Vector3 ledgeRayOrigin = transform.position 
                - _wallNormal * _wallOffset      // alinha com a face da parede
                - _wallNormal * 0.15f            // passa levemente para o outro lado
                + Vector3.up * _ledgeCheckHeight; // sobe acima da borda
            
            if (Physics.Raycast(ledgeRayOrigin, Vector3.down, out RaycastHit ledgeHit, _ledgeCheckHeight + 1f, _ledgeGroundMask))
            {
                StartCoroutine(LedgeGrabRoutine(ledgeHit.point));
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Executa a animação procedural do ledge grab dividida em subida vertical e avanço horizontal.
    /// </summary>
    private IEnumerator LedgeGrabRoutine(Vector3 ledgeLandPoint)
    {
        _isLedgeGrabbing = true;
        IsClimbing = false;
        _rb.isKinematic = true;

        float halfDuration = _ledgeGrabDuration / 2f;
        float time = 0f;

        Vector3 startPos = _rb.position;
        Vector3 pullUpTarget = new Vector3(startPos.x, ledgeLandPoint.y + (_collider.height * 0.5f), startPos.z);

        // Fase 1 — Pull Up (sobe verticalmente)
        while (time < halfDuration)
        {
            time += Time.deltaTime;
            _rb.position = Vector3.Lerp(startPos, pullUpTarget, time / halfDuration);
            yield return null;
        }
        _rb.position = pullUpTarget;

        time = 0f;
        Vector3 stepStartPos = _rb.position;
        Vector3 stepOverTarget = ledgeLandPoint + Vector3.up * (_collider.height * 0.5f);

        // Fase 2 — Step Over (avança horizontalmente)
        while (time < halfDuration)
        {
            time += Time.deltaTime;
            _rb.position = Vector3.Lerp(stepStartPos, stepOverTarget, time / halfDuration);
            yield return null;
        }
        _rb.position = stepOverTarget;

        // Finaliza o ledge grab e restaura as físicas
        _rb.isKinematic = false;
        _rb.useGravity = true;
        _isLedgeGrabbing = false;
        _climbCooldown = _climbCooldownDuration;
        
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMaskToLayer(_climbableMask), false);
        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
    }

    /// <summary>
    /// Processa o movimento 2D projetado na superfície da parede e aplica limites físicos.
    /// </summary>
    private void HandleClimbMovement()
    {
        Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, _wallNormal).normalized;
        Vector3 wallRight = Vector3.Cross(_wallNormal, Vector3.up).normalized;

        float inputX = _inputHandler.MoveInput.x;
        float inputY = _inputHandler.MoveInput.y;

        Vector3 origin = transform.position + _collider.center;

        // Limite de movimento para baixo
        if (inputY < -0.1f)
        {
            Vector3 lowerOrigin = origin - Vector3.up * (_collider.height * 0.5f);
            if (!Physics.Raycast(lowerOrigin, -_wallNormal, _wallReattachDistance, _climbableMask))
            {
                Fall();
                return;
            }
        }

        Vector3 climbVelocity = (wallRight * inputX + wallUp * inputY) * _climbSpeed;
        
        Vector3 newPosition = _rb.position + climbVelocity * Time.fixedDeltaTime;
        
        float currentPerpDist = Vector3.Dot(newPosition - _wallPoint, _wallNormal);
        newPosition += _wallNormal * (_wallOffset - currentPerpDist);
        
        _rb.MovePosition(newPosition);

        Quaternion targetRotation = Quaternion.LookRotation(-_wallNormal);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, _snapRotationSpeed * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Checa as condições de saída da parede via inputs do jogador.
    /// </summary>
    private void CheckExits()
    {
        if (_isLedgeGrabbing) return;

        if (_inputHandler.IsCrouching)
        {
            Fall();
            return;
        }

        if (_inputHandler.JumpPressed)
        {
            if (_inputHandler.MoveInput.y > 0.1f)
            {
                HopUp();
            }
            else
            {
                WallJump();
            }
        }
    }

    private void WallJump()
    {
        IsClimbing = false;
        _rb.useGravity = true;
        _climbCooldown = _climbCooldownDuration;

        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMaskToLayer(_climbableMask), false);

        Vector3 jumpForce = (_wallNormal * _wallJumpHorizontalForce) + (Vector3.up * _wallJumpVerticalForce);
        _rb.AddForce(jumpForce, ForceMode.VelocityChange);
        
        _inputHandler.ConsumeJump();
    }

    private void HopUp()
    {
        Vector3 wallUp = Vector3.ProjectOnPlane(Vector3.up, _wallNormal).normalized;
        _rb.AddForce(wallUp * _hopUpForce, ForceMode.VelocityChange);
        
        _inputHandler.ConsumeJump();
    }

    private void Fall()
    {
        if (!IsClimbing) return;
        
        IsClimbing = false;
        _rb.useGravity = true;
        _climbCooldown = _climbCooldownDuration;

        transform.rotation = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
        Physics.IgnoreLayerCollision(gameObject.layer, LayerMaskToLayer(_climbableMask), false);

        _rb.AddForce(_wallNormal * 1f, ForceMode.VelocityChange);
    }

    /// <summary>
    /// Converte um LayerMask de um único bit para o índice inteiro do layer.
    /// </summary>
    private int LayerMaskToLayer(LayerMask mask)
    {
        int layerValue = mask.value;
        int layer = 0;
        while (layerValue > 1)
        {
            layerValue >>= 1;
            layer++;
        }
        return layer;
    }
}
