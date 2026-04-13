using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Recebe dano localizado em um colisor específico, gerenciando seu próprio HP, fraquezas e resistências.
/// </summary>
public class BodyPartReceiver : MonoBehaviour
{
    [Header("Part Configuration")]
    [SerializeField] private BodyPart _partType;
    [SerializeField] private float _maxHP = 100f;

    [Header("Damage Modifiers")]
    [SerializeField] private DamageType[] _weaknesses;
    [SerializeField] private DamageType[] _resistances;

    /// <summary>
    /// Disparado quando a parte recebe dano. Parâmetros: (Membro atingido, Quantidade de dano final)
    /// </summary>
    public event UnityAction<BodyPart, float> OnDamageReceived;
    
    /// <summary>
    /// Disparado quando o HP desta parte chega a zero. Parâmetro: (Membro destruído)
    /// </summary>
    public event UnityAction<BodyPart> OnPartDestroyed;

    private float _currentHP;
    private bool _isDestroyed;
    private Vector3 _lastHitDirection;

    public BodyPart PartType => _partType;
    public float CurrentHP => _currentHP;
    public bool IsDestroyed => _isDestroyed;
    public Vector3 LastHitDirection => _lastHitDirection;

    private void Awake()
    {
        _currentHP = _maxHP;
    }

    /// <summary>
    /// Calcula e aplica o dano recebido baseando-se no tipo e nos modificadores de vulnerabilidade.
    /// </summary>
    /// <param name="amount">Dano base bruto.</param>
    /// <param name="type">O tipo elemental ou físico do dano.</param>
    /// <param name="hitDirection">Vetor de direção do impacto (opcional, útil para ragdoll futuro).</param>
    public void TakeDamage(float amount, DamageType type, Vector3 hitDirection = default)
    {
        if (_isDestroyed) return;

        _lastHitDirection = hitDirection;
        float finalDamage = amount;

        // Modificadores padrão: x2 para fraqueza, x0.5 para resistência
        if (IsWeakTo(type)) 
        {
            finalDamage *= 2f; 
        }
        else if (IsResistantTo(type)) 
        {
            finalDamage *= 0.5f;
        }

        _currentHP -= finalDamage;
        OnDamageReceived?.Invoke(_partType, finalDamage);

        if (_currentHP <= 0f)
        {
            _currentHP = 0f;
            _isDestroyed = true;
            OnPartDestroyed?.Invoke(_partType);
        }
    }

    private bool IsWeakTo(DamageType type)
    {
        if (_weaknesses == null) return false;
        for (int i = 0; i < _weaknesses.Length; i++)
        {
            if (_weaknesses[i] == type) return true;
        }
        return false;
    }

    private bool IsResistantTo(DamageType type)
    {
        if (_resistances == null) return false;
        for (int i = 0; i < _resistances.Length; i++)
        {
            if (_resistances[i] == type) return true;
        }
        return false;
    }
}
