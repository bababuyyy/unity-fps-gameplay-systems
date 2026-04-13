using UnityEngine;

/// <summary>
/// Handles the detection and execution of interactions with IInteractable objects in the world.
/// </summary>
public class PlayerInteraction : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float _interactionDistance = 2.5f;
    [SerializeField] private float _interactionRadius = 0.2f;
    [SerializeField] private LayerMask _interactableMask;

    [Header("References")]
    [SerializeField] private Transform _cameraRoot;

    private PlayerInputHandler _inputHandler;
    private PlayerGrab _playerGrab;

    /// <summary>
    /// Gets the currently focused interactable object. Null if none is in focus.
    /// </summary>
    public IInteractable CurrentInteractable { get; private set; }

    /// <summary>
    /// Gets the current interaction prompt text. Empty if no object is in focus.
    /// </summary>
    public string CurrentPrompt { get; private set; }

    private void Awake()
    {
        _inputHandler = GetComponent<PlayerInputHandler>();
        _playerGrab = GetComponent<PlayerGrab>();

        if (_inputHandler == null)
        {
            Debug.LogError("[PlayerInteraction] PlayerInputHandler is missing on Awake.");
        }

        if (_cameraRoot == null)
        {
            Debug.LogError("[PlayerInteraction] _cameraRoot is not assigned in the Inspector.");
        }
    }

    private void Update()
    {
        if (_cameraRoot == null || _inputHandler == null) return;

        // Grab cancels interaction — hide prompt and block input while holding object
        if (_playerGrab != null && _playerGrab.IsHoldingObject)
        {
            CurrentInteractable = null;
            CurrentPrompt = string.Empty;
            return;
        }

        HandleInteractionDetection();
        HandleInteractionInput();
    }

    /// <summary>
    /// Casts a sphere forward from the camera to detect interactable objects.
    /// </summary>
    private void HandleInteractionDetection()
    {
        if (Physics.SphereCast(_cameraRoot.position, _interactionRadius, _cameraRoot.forward, out RaycastHit hit, _interactionDistance, _interactableMask))
        {
            IInteractable interactable = hit.collider.GetComponentInParent<IInteractable>();
            
            if (interactable != null)
            {
                CurrentInteractable = interactable;
                CurrentPrompt = interactable.InteractionPrompt;
                return;
            }
        }

        // Reset state if no interactable is found
        CurrentInteractable = null;
        CurrentPrompt = string.Empty;
    }

    /// <summary>
    /// Listens for the interaction input and triggers the interactable object if one is focused.
    /// </summary>
    private void HandleInteractionInput()
    {
        if (_inputHandler.InteractPressed && CurrentInteractable != null)
        {
            CurrentInteractable.Interact(this);
        }
    }
}