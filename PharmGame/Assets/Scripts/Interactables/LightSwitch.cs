using UnityEngine;
using TMPro;

public class LightSwitch : MonoBehaviour, IInteractable
{
    [Header("Models")]
    public GameObject offModel;
    public GameObject onModel;

    [Header("Light Group Settings")]
    public GameObject lightGroup;

    [Header("Prompt Settings")]
    public Vector3 textPromptOffset = Vector3.zero;
    public Vector3 textPromptRotationOffset = Vector3.zero; // New rotation offset
    private Quaternion initialPromptRotation;

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

        // Store the initial rotation of the prompt text element
        if (FindAnyObjectByType<PlayerInteractionManager>()?.promptText != null)
        {
            initialPromptRotation = FindAnyObjectByType<PlayerInteractionManager>().promptText.transform.rotation;
        }
        else
        {
            Debug.LogWarning("PlayerInteractionManager or promptText not found. Cannot store initial prompt rotation.");
        }

        UpdateLightbulbMaterial();
    }

    /// <summary>
    /// Activates the prompt by positioning and rotating the shared TMP_Text element.
    /// </summary>
    /// <param name="prompt">The shared TMP_Text element.</param>
    public void ActivatePrompt(TMP_Text prompt)
    {
        prompt.transform.position = transform.position + textPromptOffset;
        // Apply the rotation offset in world space
        prompt.transform.rotation = initialPromptRotation * Quaternion.Euler(textPromptRotationOffset);
        prompt.text = InteractionPrompt;
        prompt.enabled = true;
    }

    /// <summary>
    /// Deactivates (hides) the prompt text.
    /// </summary>
    /// <param name="prompt">The shared TMP_Text element.</param>
    public void DeactivatePrompt(TMP_Text prompt)
    {
        prompt.text = "";
        prompt.enabled = false;
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

    /// <summary>
    /// Allows adjustment of the prompt offset via code.
    /// </summary>
    /// <param name="positionOffset">A new Vector3 position offset value.</param>
    /// <param name="rotationOffset">A new Vector3 rotation offset value.</param>
    public void SetTextPromptOffset(Vector3 positionOffset, Vector3 rotationOffset)
    {
        textPromptOffset = positionOffset;
        textPromptRotationOffset = rotationOffset;
    }

    /// <summary>
    /// Allows adjustment of the prompt position offset via code.
    /// </summary>
    /// <param name="offset">A new Vector3 position offset value.</param>
    public void SetTextPromptOffset(Vector3 offset)
    {
        textPromptOffset = offset;
    }
}