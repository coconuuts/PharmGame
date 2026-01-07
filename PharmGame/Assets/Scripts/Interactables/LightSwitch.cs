// --- START OF FILE LightSwitch.cs ---

using UnityEngine;
using Systems.Interaction; // Needed for IInteractable, InteractionResponse, and the new InteractionManager
using Systems.GameStates; // Needed for PromptEditor
using Systems.Persistence; 
using Systems.Inventory;

public class LightSwitch : MonoBehaviour, IInteractable, ISavableComponent
{
    [field: SerializeField] public SerializableGuid Id { get; set; } = SerializableGuid.NewGuid();

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
    private Material offMaterial; 

    [Tooltip("Should this interactable be enabled by default when registered?")]
    [SerializeField] private bool enableOnStart = true;
    public bool EnableOnStart => enableOnStart;

    private bool isLightOn = false; // Current state
    private Renderer lightbulbRenderer; // Renderer for the lightbulb material change

    /// <summary>
    /// Returns the appropriate interaction prompt based on the current light state.
    /// </summary>
    public string InteractionPrompt => isLightOn ? "Turn off (E)" : "Turn on (E)";

    private void Awake()
    {
         if (Systems.Interaction.InteractionManager.Instance != null)
         {
             Systems.Interaction.InteractionManager.Instance.RegisterInteractable(this);
         }
         else
         {
             // This error is critical as the component won't be managed
             Debug.LogError($"LightSwitch on {gameObject.name}: InteractionManager.Instance is null in Awake! Cannot register.", this);
             // Optionally disable here if registration is absolutely required for function
             // enabled = false;
         }
    }

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

        UpdateVisuals();
    }

    // OnDestroy Method for Unregistration ---
    private void OnDestroy()
    {
         // Unregister from the singleton InteractionManager ---
         if (Systems.Interaction.InteractionManager.Instance != null)
         {
             Systems.Interaction.InteractionManager.Instance.UnregisterInteractable(this);
         }
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
        return new ToggleLightResponse(this); // Pass a reference to this LightSwitch instance
    }

    /// <summary>
    /// Toggles the light state (on/off) and updates the models, light group, and lightbulb material.
    /// This method is now called by the MenuManager (or a handler) based on the ToggleLightResponse.
    /// </summary>
    public void ToggleLightState() // NEW PUBLIC METHOD FOR TOGGLING
    {
         isLightOn = !isLightOn; // Toggle the state

         Debug.Log($"LightSwitch ({gameObject.name}): Toggling light to: {(isLightOn ? "On" : "Off")}.");

         UpdateVisuals();
    }

    private void UpdateVisuals()
    {
         // Update models
         if (offModel != null) offModel.SetActive(!isLightOn);
         if (onModel != null) onModel.SetActive(isLightOn);

         // Update light group active state
         if (lightGroup != null) lightGroup.SetActive(isLightOn);

         // Update lightbulb material
         UpdateLightbulbMaterial();
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

    // ISavableComponent Implementation ---
    // Create Data
    public ISaveable CreateSaveData()
    {
        return new InteractableObjectData
        {
            Id = this.Id,
            IsStateOn = this.isLightOn
        };
    }

    // Bind Data
    public void Bind(ISaveable data)
    {
        if (data is InteractableObjectData saveData)
        {
            this.isLightOn = saveData.IsStateOn;
            
            // Force visuals to match the loaded state
            UpdateVisuals();
            
            Debug.Log($"LightSwitch ({gameObject.name}): Loaded state. Light is now {(isLightOn ? "On" : "Off")}.");
        }
    }
}
// --- END OF FILE LightSwitch.cs ---