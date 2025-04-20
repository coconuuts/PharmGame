using UnityEngine;

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
    private Material offMaterial;

    private bool isLightOn = false;
    private Renderer lightbulbRenderer;

    /// <summary>
    /// Returns the appropriate interaction prompt based on the current light state.
    /// </summary>
    public string InteractionPrompt => isLightOn ? "Turn off (E)" : "Turn on (E)";

    private void Start()
    {
        GameObject lightbulbObject = GameObject.FindGameObjectWithTag("Lightbulb");
        if (lightbulbObject != null)
        {
            lightbulbRenderer = lightbulbObject.GetComponent<Renderer>();
            if (lightbulbRenderer != null)
            {
                offMaterial = lightbulbRenderer.material;
            }
            else
            {
                Debug.LogError("Lightbulb GameObject with tag 'Lightbulb' has no Renderer component!");
            }
        }
        else
        {
            Debug.LogError("No GameObject found with the tag 'Lightbulb'!");
        }

        if (offModel != null) offModel.SetActive(!isLightOn);
        if (onModel != null) onModel.SetActive(isLightOn);
        if (lightGroup != null) lightGroup.SetActive(isLightOn);

        UpdateLightbulbMaterial();
    }

    /// <summary>
    /// Activates the interaction prompt.
    /// </summary>
    public void ActivatePrompt()
    {
        PromptEditor.Instance.DisplayPrompt(transform, InteractionPrompt, lightTextPromptOffset, lightTextPromptRotationOffset);
    }

    /// <summary>
    /// Deactivates (hides) the interaction prompt.
    /// </summary>
    public void DeactivatePrompt()
    {
        PromptEditor.Instance.HidePrompt();
    }

    /// <summary>
    /// Toggles the light on or off and updates the models, light group, and lightbulb material.
    /// </summary>
    public void Interact()
    {
        isLightOn = !isLightOn;

        if (offModel != null) offModel.SetActive(!isLightOn);
        if (onModel != null) onModel.SetActive(isLightOn);
        if (lightGroup != null) lightGroup.SetActive(isLightOn);

        UpdateLightbulbMaterial();
    }

    /// <summary>
    /// Updates the material of the lightbulb based on the current light state.
    /// </summary>
    private void UpdateLightbulbMaterial()
    {
        if (lightbulbRenderer != null)
        {
            lightbulbRenderer.material = isLightOn ? onMaterial : offMaterial;
        }
    }
}