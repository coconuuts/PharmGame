// --- START OF FILE ShopButtonUIHelper.cs ---
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Helper script to hold references to UI elements within a shop item button prefab.
/// This makes it easier for the ComputerInteractable script to configure generated buttons.
/// </summary>
public class ShopButtonUIHelper : MonoBehaviour
{
    [Tooltip("The Button component on this GameObject.")]
    public Button button;

    [Tooltip("The Image component that will display the item's icon. Usually a child GameObject.")]
    public Image iconImage;

    [Tooltip("The TextMeshProUGUI component that will display the item's name. Usually a child GameObject.")]
    public TextMeshProUGUI nameText;

    void Awake()
    {
        // Auto-populate references if not explicitly set in the Inspector.
        // This makes the prefab setup slightly more forgiving.
        if (button == null)
        {
            button = GetComponent<Button>();
        }
        if (iconImage == null)
        {
            // Tries to find the first Image component on children, or on itself if it's the button's background.
            // Adjust this if your icon image is specifically named or deeply nested.
            iconImage = GetComponentInChildren<Image>();
        }
        if (nameText == null)
        {
            nameText = GetComponentInChildren<TextMeshProUGUI>();
        }
    }
}
// --- END OF FILE ShopButtonUIHelper.cs ---