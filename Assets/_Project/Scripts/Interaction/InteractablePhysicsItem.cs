using UnityEngine;

/// <summary>
/// A physical interactive item that requires a Rigidbody. 
/// Can be configured to require being grabbed before it can be collected/interacted with.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
public class InteractablePhysicsItem : MonoBehaviour, IInteractable
{
    [Header("Item Settings")]
    [SerializeField] private string _itemName;
    [SerializeField] private string _customPrompt;
    [SerializeField] private bool _requiresGrabFirst;

    private Rigidbody _rb;

    /// <summary>
    /// Gets the prompt text. Uses the custom prompt if provided, otherwise defaults to "Coletar {_itemName}".
    /// </summary>
    public string InteractionPrompt
    {
        get
        {
            if (!string.IsNullOrEmpty(_customPrompt))
            {
                return _customPrompt;
            }
            return $"Coletar {_itemName}";
        }
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        
        if (_rb == null)
        {
            Debug.LogError($"[InteractablePhysicsItem] Rigidbody is missing on {_itemName}.");
        }
    }

    /// <summary>
    /// Executes the interaction logic. Defends against premature interaction if _requiresGrabFirst is true.
    /// </summary>
    /// <param name="interactor">The player interacting with the item.</param>
    public void Interact(PlayerInteraction interactor)
    {
        if (_requiresGrabFirst)
        {
            PlayerGrab playerGrab = interactor.GetComponent<PlayerGrab>();
            
            // Validate that the player is grabbing something, and that something is specifically THIS object
            if (playerGrab == null || !playerGrab.IsHoldingObject || playerGrab.HeldObject != _rb)
            {
                Debug.Log($"[InteractablePhysicsItem] Must grab first: {_itemName}");
                return;
            }
        }

        Debug.Log($"[InteractablePhysicsItem] Collected: {_itemName}");
        gameObject.SetActive(false);
        
        // TODO: Inventory — adicionar ao sistema de inventário quando implementado
    }
}