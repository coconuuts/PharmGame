using UnityEngine;
using Systems.Inventory; // Make sure this namespace matches your scripts

public class InventoryTester : MonoBehaviour
{
    [Tooltip("Drag the GameObject with the Inventory component here.")]
    [SerializeField] private Inventory targetInventory;

    [Tooltip("Drag your 'Test1' ItemDetails ScriptableObject asset here.")]
    [SerializeField] private ItemDetails testItem1Details;

    [Tooltip("Drag your 'Test2' ItemDetails ScriptableObject asset here.")]
    [SerializeField] private ItemDetails testItem2Details;

    void Start()
    {
        // Basic check to ensure references are set
        if (targetInventory == null || targetInventory.Combiner == null)
        {
            Debug.LogError("InventoryTester requires a valid Inventory component reference with a Combiner assigned in the inspector.", this);
            enabled = false; // Disable the script if not set up
            return;
        }

        // Optional: Check if ItemDetails assets are assigned (warnings, not errors)
        if (testItem1Details == null)
        {
            Debug.LogWarning("InventoryTester: 'Test1' ItemDetails asset is not assigned in the inspector.", this);
        }

        if (testItem2Details == null)
        {
            Debug.LogWarning("InventoryTester: 'Test2' ItemDetails asset is not assigned in the inspector.", this);
        }

        Debug.Log("InventoryTester initialized. Press '1' to add Test1, '2' to add Test2.");
    }

    void Update()
    {
        // Ensure the inventory system is valid before trying to add items
        if (targetInventory == null || targetInventory.Combiner == null)
        {
            return; // Don't execute test logic if setup failed
        }

        // --- Test adding Test1 item ---
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            if (testItem1Details != null)
            {
                // Create a new runtime Item instance from the ScriptableObject details
                // We create a quantity of 1 for simplicity in this basic test
                Item newItemInstance = testItem1Details.Create(1);

                // Attempt to add the item instance to the inventory using the Combiner component
                bool added = targetInventory.Combiner.AddItem(newItemInstance);

                if (added)
                {
                    Debug.Log($"Successfully added {testItem1Details.Name} to inventory.");
                }
            }
            else
            {
                Debug.LogWarning("Cannot add Test1 item: Test1 ItemDetails asset not assigned in the Inspector.");
            }
        }

        // --- Test adding Test2 item ---
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            if (testItem2Details != null)
            {
                // Create a new runtime Item instance
                Item newItemInstance = testItem2Details.Create(1);

                // Attempt to add the item instance
                bool added = targetInventory.Combiner.AddItem(newItemInstance);

                 if (added)
                {
                    Debug.Log($"Successfully added {testItem2Details.Name} to inventory.");
                }
                else
                {
                     // AddItem logs a warning inside Combiner if it fails, but this confirms it here too
                     Debug.LogWarning($"Failed to add {testItem2Details.Name}. Inventory might be full.");
                }
            }
             else
            {
                 Debug.LogWarning("Cannot add Test2 item: Test2 ItemDetails asset not assigned in the Inspector.");
            }
        }
    }
}