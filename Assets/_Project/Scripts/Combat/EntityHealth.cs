using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Agregador central de saúde. Monitora todos os BodyPartReceivers filhos e gerencia o estado global de vida.
/// </summary>
public class EntityHealth : MonoBehaviour
{
    [Header("Vital Parts")]
    [Tooltip("Partes que, se destruídas, causam a morte imediata da entidade.")]
    [SerializeField] private BodyPart[] _vitalParts = { BodyPart.Head, BodyPart.Torso };

    /// <summary>
    /// Disparado quando uma parte vital chega a zero HP. Parâmetros: (Parte fatal, Direção do impacto fatal)
    /// </summary>
    public event UnityAction<BodyPart, Vector3> OnEntityDeath;

    private BodyPartReceiver[] _bodyParts;
    private bool _isAlive = true;

    public bool IsAlive => _isAlive;

    private void Awake()
    {
        _bodyParts = GetComponentsInChildren<BodyPartReceiver>();

        if (_bodyParts == null || _bodyParts.Length == 0)
        {
            Debug.LogError("[EntityHealth] No BodyPartReceiver found in children on Awake.");
            return;
        }

        // Inscreve a entidade central para escutar a destruição de qualquer membro filho
        foreach (var part in _bodyParts)
        {
            part.OnPartDestroyed += HandlePartDestroyed;
        }
    }

    private void OnDestroy()
    {
        if (_bodyParts == null) return;
        
        // Evita memory leaks limpando as inscrições dos eventos
        foreach (var part in _bodyParts)
        {
            if (part != null)
            {
                part.OnPartDestroyed -= HandlePartDestroyed;
            }
        }
    }

    /// <summary>
    /// Avalia se a parte destruída é vital. Se for, engatilha o evento global de morte.
    /// </summary>
    private void HandlePartDestroyed(BodyPart destroyedPart)
    {
        if (!_isAlive) return;

        bool isVital = false;
        for (int i = 0; i < _vitalParts.Length; i++)
        {
            if (_vitalParts[i] == destroyedPart)
            {
                isVital = true;
                break;
            }
        }

        if (isVital)
        {
            _isAlive = false;

            // Busca a direção do último hit na parte que foi destruída para repassar ao evento
            Vector3 fatalHitDirection = Vector3.zero;
            foreach (var part in _bodyParts)
            {
                if (part.PartType == destroyedPart)
                {
                    fatalHitDirection = part.LastHitDirection;
                    break;
                }
            }

            OnEntityDeath?.Invoke(destroyedPart, fatalHitDirection);
        }
    }
}