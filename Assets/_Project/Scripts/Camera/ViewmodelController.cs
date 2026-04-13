// ViewmodelController.cs
using UnityEngine;

/// <summary>
/// Sincroniza o ViewmodelRoot com a MainCamera do jogador via LateUpdate.
/// Mantém os braços fixos na frente da câmera com offset configurável no Inspector.
/// </summary>
public class ViewmodelController : MonoBehaviour
{
    [Header("Referências")]
    [SerializeField] private Transform _mainCamera;
    [SerializeField] private Transform _viewmodelRoot;

    [Header("Offset de Posição (espaço local da câmera)")]
    [SerializeField] private Vector3 _positionOffset = new Vector3(0.2f, -0.25f, 0.4f);

    [Header("Offset de Rotação (graus)")]
    [SerializeField] private Vector3 _rotationOffset = Vector3.zero;

    private void Awake()
    {
        if (_mainCamera == null)
            Debug.LogError("[ViewmodelController] _mainCamera não atribuído.");
        if (_viewmodelRoot == null)
            Debug.LogError("[ViewmodelController] _viewmodelRoot não atribuído.");
    }

    private void LateUpdate()
    {
        if (_mainCamera == null || _viewmodelRoot == null) return;

        // Posição: câmera + offset transformado pro espaço de mundo da câmera
        _viewmodelRoot.position = _mainCamera.position
            + _mainCamera.TransformDirection(_positionOffset);

        // Rotação: herda a rotação da câmera + offset opcional
        _viewmodelRoot.rotation = _mainCamera.rotation
            * Quaternion.Euler(_rotationOffset);
    }
}