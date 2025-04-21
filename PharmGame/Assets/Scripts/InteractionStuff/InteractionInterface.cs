using Systems.Interaction; // ADD THIS USING

public interface IInteractable
{
    /// <summary>
    /// A message to be shown when the player is looking at the object.
    /// </summary>
    string InteractionPrompt { get; }

    /// <summary>
    /// Activates the interaction prompt.
    /// </summary>
    void ActivatePrompt();

    /// <summary>
    /// Deactivates (hides) the interaction prompt.
    /// </summary>
    void DeactivatePrompt();

    /// <summary>
    /// Runs the object's specific interaction logic and returns a response describing the outcome.
    /// </summary>
    /// <returns>An InteractionResponse object describing the result of the interaction.</returns>
    InteractionResponse Interact(); // CHANGED RETURN TYPE
}