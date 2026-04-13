// EnemyRagdoll.cs
using System.Collections;
using UnityEngine;

/// <summary>
/// Estrutura para mapear as partes lógicas do corpo (BodyPart) para os Transforms físicos (ossos) do esqueleto.
/// </summary>
[System.Serializable]
public struct BodyPartBoneMap
{
    public BodyPart Part;
    public Transform Bone;
}

/// <summary>
/// Gerencia a transição do estado de animação para o estado de ragdoll físico no momento da morte da entidade.
/// Simula uma fase de "agonia" inicial com alta resistência física antes de relaxar completamente.
/// </summary>
[RequireComponent(typeof(EntityHealth))]
public class EnemyRagdoll : MonoBehaviour
{
    [Header("References")]
    [Tooltip("Referência ao Animator do modelo (ex: UAL1_Standard).")]
    [SerializeField] private Animator _animator;
    [Tooltip("Transform raiz do esqueleto que contém os Rigidbodies gerados pelo Ragdoll Wizard.")]
    [SerializeField] private Transform _armatureRoot;

    [Header("Ragdoll Settings")]
    [Tooltip("Tempo em segundos que o corpo leva para relaxar completamente após a morte.")]
    [SerializeField] private float _agonyDuration = 1.5f;
    [Tooltip("Resistência linear aplicada durante a agonia para evitar colapso instantâneo.")]
    [SerializeField] private float _agonyLinearDamping = 8f;
    [Tooltip("Força do impacto aplicada no osso fatal para jogar o corpo na direção do golpe.")]
    [SerializeField] private float _impulseForce = 5f;

    [Header("Bone Mapping")]
    [Tooltip("Mapeia qual osso físico deve receber a força de impacto dependendo da parte atingida fatalmente.")]
    [SerializeField] private BodyPartBoneMap[] _boneMap;

    private EntityHealth _entityHealth;
    private Rigidbody[] _ragdollRigidbodies;
    private Collider[] _hurtboxColliders;
    private Collider[] _ragdollColliders;

    private void Awake()
    {
        _entityHealth = GetComponent<EntityHealth>();
        
        if (_entityHealth == null)
        {
            Debug.LogError("[EnemyRagdoll] EntityHealth não encontrado no Awake.");
        }

        if (_animator == null)
        {
            Debug.LogError("[EnemyRagdoll] Referência do Animator não foi atribuída no Inspector.");
        }

        if (_armatureRoot == null)
        {
            Debug.LogError("[EnemyRagdoll] Referência do _armatureRoot não foi atribuída no Inspector.");
        }
        else
        {
            // Busca todos os Rigidbodies gerados no esqueleto
            _ragdollRigidbodies = _armatureRoot.GetComponentsInChildren<Rigidbody>();
            
            // Busca os colliders específicos da malha física do ragdoll
            _ragdollColliders = _armatureRoot.GetComponentsInChildren<Collider>();
            
            if (_ragdollRigidbodies.Length == 0)
            {
                Debug.LogWarning("[EnemyRagdoll] Nenhum Rigidbody encontrado nos filhos do _armatureRoot.");
            }

            if (_ragdollColliders.Length == 0)
            {
                Debug.LogWarning("[EnemyRagdoll] Nenhum Collider físico encontrado nos filhos do _armatureRoot.");
            }

            // Garante que o ragdoll inicie desativado (controlado pela animação)
            foreach (var rb in _ragdollRigidbodies)
            {
                rb.isKinematic = true;
            }

            // Desativa os colliders físicos dos ossos — NavMeshAgent controla a posição enquanto vivo
            foreach (var col in _ragdollColliders)
            {
                if (col != null) col.enabled = false;
            }

            // Desativa a colisão interna entre os próprios ossos do ragdoll
            DisableRagdollSelfCollision();
        }

        // Busca absolutamente todos os colliders na hierarquia do inimigo (incluindo hurtboxes e ragdoll)
        _hurtboxColliders = GetComponentsInChildren<Collider>();

        // Inscreve no evento de morte
        if (_entityHealth != null)
        {
            _entityHealth.OnEntityDeath += HandleEntityDeath;
        }
    }

    private void OnDestroy()
    {
        // Limpa a inscrição para evitar memory leaks
        if (_entityHealth != null)
        {
            _entityHealth.OnEntityDeath -= HandleEntityDeath;
        }
    }

    /// <summary>
    /// Itera sobre todos os pares de colisores físicos do ragdoll e instrui a engine a ignorar colisões entre eles,
    /// evitando o feedback loop que causa a "explosão" dos ossos no frame de ativação.
    /// </summary>
    private void DisableRagdollSelfCollision()
    {
        if (_ragdollColliders == null || _ragdollColliders.Length == 0) return;

        for (int i = 0; i < _ragdollColliders.Length; i++)
        {
            for (int j = i + 1; j < _ragdollColliders.Length; j++)
            {
                if (_ragdollColliders[i] != null && _ragdollColliders[j] != null)
                {
                    Physics.IgnoreCollision(_ragdollColliders[i], _ragdollColliders[j], true);
                }
            }
        }
    }

    /// <summary>
    /// Escuta o evento de morte, desativa o Animator e engatilha a coroutine para transição física segura.
    /// </summary>
    private void HandleEntityDeath(BodyPart fatalPart, Vector3 hitDirection)
    {
        if (_animator != null)
        {
            _animator.enabled = false;
        }

        if (_ragdollRigidbodies == null || _ragdollRigidbodies.Length == 0) return;

        // Inicia a coroutine para aguardar o frame físico antes de soltar o ragdoll
        StartCoroutine(EnableRagdollCoroutine(fatalPart, hitDirection));
    }

    /// <summary>
    /// Aguarda um FixedUpdate para garantir que o Animator soltou os Transforms antes de ativar a física.
    /// Desativa colisores de dano e assegura a ativação exclusiva dos colisores físicos.
    /// </summary>
    private IEnumerator EnableRagdollCoroutine(BodyPart fatalPart, Vector3 hitDirection)
    {
        yield return new WaitForFixedUpdate();

        // 1. Desativa TODOS os colliders da hierarquia (desliga as hurtboxes externas)
        if (_hurtboxColliders != null)
        {
            foreach (var col in _hurtboxColliders)
            {
                if (col != null) col.enabled = false;
            }
        }

        // 2. Reativa IMEDIATAMENTE os colliders que pertencem aos ossos do ragdoll
        if (_ragdollColliders != null)
        {
            foreach (var col in _ragdollColliders)
            {
                if (col != null) col.enabled = true;
            }
        }

        // Ativa a física de todos os ossos e aplica o damping de "agonia"
        foreach (var rb in _ragdollRigidbodies)
        {
            rb.isKinematic = false;
            rb.linearDamping = _agonyLinearDamping; // Unity 6 API
        }

        // Busca o osso específico mapeado para a parte que causou a morte e aplica a força do golpe
        if (_boneMap != null)
        {
            foreach (var map in _boneMap)
            {
                if (map.Part == fatalPart && map.Bone != null)
                {
                    Rigidbody fatalBoneRb = map.Bone.GetComponent<Rigidbody>();
                    if (fatalBoneRb != null)
                    {
                        // Usa ForceMode.Impulse pois é um impacto instantâneo
                        fatalBoneRb.AddForce(hitDirection * _impulseForce, ForceMode.Impulse);
                    }
                    break;
                }
            }
        }

        // Inicia o contador para o relaxamento total do corpo
        StartCoroutine(AgonyCoroutine());
    }

    /// <summary>
    /// Aguarda a duração da agonia antes de zerar o damping, permitindo que o corpo relaxe na gravidade.
    /// </summary>
    private IEnumerator AgonyCoroutine()
    {
        yield return new WaitForSeconds(_agonyDuration);

        if (_ragdollRigidbodies == null) yield break;

        foreach (var rb in _ragdollRigidbodies)
        {
            rb.linearDamping = 0f;  // Unity 6 API
            rb.angularDamping = 0f; // Unity 6 API
        }
    }
}