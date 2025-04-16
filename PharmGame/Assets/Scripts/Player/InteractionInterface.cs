using UnityEngine;
using TMPro;

public interface IInteractable
{
    /// <summary>
    /// A message to be shown when the player is looking at the object.
    /// </summary>
    string InteractionPrompt { get; }

    /// <summary>
    /// Activates the shared prompt text and positions it relative to the object.
    /// </summary>
    /// <param name="prompt">The shared TMP_Text element to update.</param>
    void ActivatePrompt(TMP_Text prompt);

    /// <summary>
    /// Deactivates (hides) the shared prompt text.
    /// </summary>
    /// <param name="prompt">The shared TMP_Text element to update.</param>
    void DeactivatePrompt(TMP_Text prompt);

    /// <summary>
    /// Runs the object's specific interaction logic.
    /// </summary>
    void Interact();
}
