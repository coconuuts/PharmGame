using UnityEngine;
using Systems.Interaction; // ADD THIS USING

public class LightSwitch : MonoBehaviour, IInteractable
{
    [Header("Models")]
    public GameObject offModel;
    public GameObject onModel;

    [Header("Light Group Settings")]
    public GameObject lightGroup;

    [Header("Prompt Settings")]
    public Vector3 lightTextPromptOffset = Vector3.zero;
    public Vector3 lightTextPromptRotationOffset = Vector3.zero;

    [Header("Lightbulb Material Settings")]
    public Material onMaterial;
    private Material offMaterial; // Store the initial OFF material

    private bool isLightOn = false; // Current state
    private Renderer lightbulbRenderer; // Renderer for the lightbulb material change

    /// <summary>
    /// Returns the appropriate interaction prompt based on the current light state.
    /// </summary>
    public string InteractionPrompt => isLightOn ? "Turn off (E)" : "Turn on (E)";

    private void Start()
    {
        // Find the lightbulb renderer by tag
        GameObject lightbulbObject = GameObject.FindGameObjectWithTag("Lightbulb");
        if (lightbulbObject != null)
        {
            lightbulbRenderer = lightbulbObject.GetComponent<Renderer>();
            if (lightbulbRenderer != null)
            {
                // Store the initial material as the 'off' material
                offMaterial = lightbulbRenderer.material;
            }
            else
            {
                Debug.LogError("Lightbulb GameObject with tag 'Lightbulb' has no Renderer component!");
            }
        }
        else
        {
            Debug.LogError("No GameObject found with the tag 'Lightbulb'! Lightbulb material cannot be updated.");
        }

        // Initialize visual state based on isLightOn
        if (offModel != null) offModel.SetActive(!isLightOn);
        if (onModel != null) onModel.SetActive(isLightOn);
        if (lightGroup != null) lightGroup.SetActive(isLightOn);

        UpdateLightbulbMaterial(); // Set initial material
    }

    /// <summary>
    /// Activates the interaction prompt.
    /// </summary>
    public void ActivatePrompt()
    {
         if (PromptEditor.Instance != null) // Added null check
         {
             PromptEditor.Instance.DisplayPrompt(transform, InteractionPrompt, lightTextPromptOffset, lightTextPromptRotationOffset);
         }
         else
         {
             Debug.LogWarning($"LightSwitch ({gameObject.name}): PromptEditor.Instance is null. Cannot display prompt.");
         }
    }

    /// <summary>
    /// Deactivates (hides) the interaction prompt.
    /// </summary>
    public void DeactivatePrompt()
    {
         if (PromptEditor.Instance != null) // Added null check
         {
             PromptEditor.Instance.HidePrompt();
         }
    }

    /// <summary>
    /// Runs the object's specific interaction logic and returns a response describing the outcome.
    /// Changed to return InteractionResponse.
    /// This method NOW ONLY returns the response; the actual toggling logic is elsewhere.
    /// </summary>
    /// <returns>An InteractionResponse object describing the result of the interaction.</returns>
    public InteractionResponse Interact() // CHANGED RETURN TYPE
    {
        Debug.Log($"LightSwitch ({gameObject.name}): Interact called. Returning ToggleLightResponse.", this);

        // --- Create and return the response ---
        // The PlayerInteractionManager will receive this and pass it to the MenuManager
        return new ToggleLightResponse(this); // Pass a reference to this LightSwitch instance
        // --------------------------------------

        // REMOVED: Direct light toggling logic from here
        // isLightOn = !isLightOn;
        // if (offModel != null) offModel.SetActive(!isLightOn);
        // ... etc.
    }

    /// <summary>
    /// Toggles the light state (on/off) and updates the models, light group, and lightbulb material.
    /// This method is now called by the MenuManager (or a handler) based on the ToggleLightResponse.
    /// </summary>
    public void ToggleLightState() // NEW PUBLIC METHOD FOR TOGGLING
    {
         isLightOn = !isLightOn; // Toggle the state

         Debug.Log($"LightSwitch ({gameObject.name}): Toggling light to: {(isLightOn ? "On" : "Off")}.");

         // Update models
         if (offModel != null) offModel.SetActive(!isLightOn);
         if (onModel != null) onModel.SetActive(isLightOn);

         // Update light group active state
         if (lightGroup != null) lightGroup.SetActive(isLightOn);

         // Update lightbulb material
         UpdateLightbulbMaterial();

         // Optional: Update the interaction prompt immediately after toggling
         // This is automatically updated by the PlayerInteractionManager when it raycasts again,
         // but you could force an update here if needed for responsiveness.
         // DeactivatePrompt(); // Hide the old prompt
         // ActivatePrompt(); // Show the new prompt (requires PlayerInteractionManager to be active)
    }


    /// <summary>
    /// Updates the material of the lightbulb based on the current light state.
    /// </summary>
    private void UpdateLightbulbMaterial()
    {
        if (lightbulbRenderer != null && onMaterial != null && offMaterial != null) // Added null checks
        {
            lightbulbRenderer.material = isLightOn ? onMaterial : offMaterial;
        }
         else
         {
             if(lightbulbRenderer == null) Debug.LogWarning($"LightSwitch ({gameObject.name}): Lightbulb Renderer is null. Cannot update material.", this);
             if(onMaterial == null) Debug.LogWarning($"LightSwitch ({gameObject.name}): On Material is null. Cannot update material.", this);
             if(offMaterial == null) Debug.LogWarning($"LightSwitch ({gameObject.name}): Off Material is null. Cannot update material.", this);
         }
    }
}