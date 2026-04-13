using UnityEngine;

/// <summary>
/// An interactive item that can be "collected" by the player.
/// </summary>
public class InteractableItem : MonoBehaviour, IInteractable
{
    [Header("Item Settings")]
    [SerializeField] private string _itemName;
    [SerializeField] private string _customPrompt;

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

    /// <summary>
    /// Collects the item, logging the action and disabling the GameObject.
    /// </summary>
    /// <param name="interactor">The player interacting with the item.</param>
    public void Interact(PlayerInteraction interactor)
    {
        Debug.Log($"[InteractableItem] Collected: {_itemName}");
        gameObject.SetActive(false);
    }
}