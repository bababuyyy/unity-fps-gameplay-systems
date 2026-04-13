using UnityEngine;

/// <summary>
/// A basic interactive door that opens and closes smoothly using rotation.
/// </summary>
public class InteractableDoor : MonoBehaviour, IInteractable
{
    [Header("Door Settings")]
    [SerializeField] private float _openAngle = 90f;
    [SerializeField] private float _openSpeed = 2f;
    [SerializeField] private bool _isOpen = false;

    public string InteractionPrompt => _isOpen ? "Fechar" : "Abrir";

    public void Interact(PlayerInteraction interactor)
    {
        _isOpen = !_isOpen;
    }

    private void Update()
    {
        Quaternion targetRotation = _isOpen 
            ? Quaternion.Euler(0f, _openAngle, 0f) 
            : Quaternion.identity;
            
        transform.localRotation = Quaternion.Lerp(
            transform.localRotation, 
            targetRotation, 
            _openSpeed * Time.deltaTime
        );
    }
}