using UnityEngine;

/// <summary>
/// Conecta o sistema de dano (EntityHealth e BodyPartReceivers) ao Animator da entidade.
/// Dispara gatilhos de impacto e atualiza o estado de vida puramente para fins de animação.
/// </summary>
[RequireComponent(typeof(EntityHealth))]
public class EnemyAnimator : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Referência ao Animator do modelo (ex: UAL1_Standard).")]
    [SerializeField] private Animator _animator;

    private EntityHealth _entityHealth;
    private BodyPartReceiver[] _bodyParts;

    // Hashes em cache para otimização de performance nas chamadas do Animator
    private readonly int _takeHitHash = Animator.StringToHash("TakeHit");
    private readonly int _isAliveHash = Animator.StringToHash("IsAlive");

    private void Awake()
    {
        _entityHealth = GetComponent<EntityHealth>();

        if (_entityHealth == null)
        {
            Debug.LogError("[EnemyAnimator] EntityHealth não encontrado no Awake.");
        }
        else
        {
            _entityHealth.OnEntityDeath += HandleEntityDeath;
        }

        if (_animator == null)
        {
            Debug.LogError("[EnemyAnimator] Referência do Animator não foi atribuída no Inspector.");
        }

        _bodyParts = GetComponentsInChildren<BodyPartReceiver>();

        if (_bodyParts.Length == 0)
        {
            Debug.LogWarning("[EnemyAnimator] Nenhum BodyPartReceiver encontrado nos filhos.");
        }
        else
        {
            foreach (var part in _bodyParts)
            {
                part.OnDamageReceived += HandleDamageReceived;
            }
        }
    }

    private void OnDestroy()
    {
        // Limpeza rigorosa para prevenir memory leaks
        if (_entityHealth != null)
        {
            _entityHealth.OnEntityDeath -= HandleEntityDeath;
        }

        if (_bodyParts != null)
        {
            foreach (var part in _bodyParts)
            {
                if (part != null)
                {
                    part.OnDamageReceived -= HandleDamageReceived;
                }
            }
        }
    }

    /// <summary>
    /// Escuta eventos de dano e aciona a animação de impacto caso a entidade ainda esteja viva.
    /// </summary>
    private void HandleDamageReceived(BodyPart part, float damageAmount)
    {
        if (_entityHealth != null && _entityHealth.IsAlive && _animator != null)
        {
            _animator.SetTrigger(_takeHitHash);
        }
    }

    /// <summary>
    /// Atualiza o boolean de estado vital no Animator quando a entidade morre.
    /// </summary>
    private void HandleEntityDeath(BodyPart fatalPart, Vector3 hitDirection)
    {
        if (_animator != null)
        {
            _animator.SetBool(_isAliveHash, false);
        }
    }
}