/// <summary>
/// Defines the contract for any object that can be interacted with by the player.
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// Gets the text prompt to display to the player when looking at this object.
    /// </summary>
    string InteractionPrompt { get; }

    /// <summary>
    /// Executes the interaction logic for this object.
    /// </summary>
    /// <param name="interactor">The PlayerInteraction component that initiated the interaction.</param>
    void Interact(PlayerInteraction interactor);
}