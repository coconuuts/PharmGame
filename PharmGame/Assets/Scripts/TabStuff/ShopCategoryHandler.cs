using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ShopCategoryHandler : MonoBehaviour
{
    [Header("UI References")]
    [SerializeField] private Button headerButton;
    [SerializeField] private TextMeshProUGUI headerText;
    [SerializeField] private GameObject contentContainer; // The object with FlexibleGridLayout
    [SerializeField] private Image arrowImage; // Optional: To rotate an arrow

    private bool isExpanded = true;

    private void Awake()
    {
        if (headerButton != null)
        {
            headerButton.onClick.AddListener(ToggleCategory);
        }
    }

    public void Setup(string categoryName)
    {
        if (headerText != null) headerText.text = categoryName;
        
        // Default to expanded or collapsed? Let's default to collapsed to save space.
        SetExpanded(false); 
    }

    public Transform GetItemContainer()
    {
        return contentContainer.transform;
    }

    public void ToggleCategory()
    {
        SetExpanded(!isExpanded);
    }

    private void SetExpanded(bool expanded)
    {
        isExpanded = expanded;
        if (contentContainer != null)
        {
            contentContainer.SetActive(isExpanded);
        }

        // Optional: Rotate arrow
        if (arrowImage != null)
        {
            arrowImage.rectTransform.rotation = Quaternion.Euler(0, 0, isExpanded ? -90 : 0);
        }
        
        // --- Force rebuild layout from the bottom up ---
        if (contentContainer != null)
        {
            // 1. Force the newly activated grid to calculate its size first
            LayoutRebuilder.ForceRebuildLayoutImmediate(contentContainer.GetComponent<RectTransform>());
        }

        // 2. Force this specific category container to wrap around the newly sized grid
        LayoutRebuilder.ForceRebuildLayoutImmediate(GetComponent<RectTransform>());

        // 3. Finally, force the parent scroll view to space all the categories out properly
        if (transform.parent != null)
        {
            LayoutRebuilder.ForceRebuildLayoutImmediate(transform.parent.GetComponent<RectTransform>());
        }
    }
}