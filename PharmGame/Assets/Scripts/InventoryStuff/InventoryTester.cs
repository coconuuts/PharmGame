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
        [Tooltip("Drag your 'Test3' ItemDetails ScriptableObject asset here.")] // Corrected tooltip
    [SerializeField] private ItemDetails testItem3Details;

    void Start()
    {
        // Basic check to ensure references are set
        if (targetInventory == null || targetInventory.Combiner == null)
        {
            // Check for Combiner specifically as Inventory depends on it
            if (targetInventory == null) Debug.LogError("InventoryTester requires a valid Inventory component reference assigned in the inspector.", this);
            else Debug.LogError($"InventoryTester requires the assigned Inventory component on {targetInventory.gameObject.name} to have a Combiner.", this);

            enabled = false; // Disable the script if not set up
            return;
        }
         // Check if ItemDetails are assigned
         if (testItem1Details == null) Debug.LogWarning("InventoryTester: Test1 ItemDetails asset not assigned. F1 will not work.", this);
         if (testItem2Details == null) Debug.LogWarning("InventoryTester: Test2 ItemDetails asset not assigned. F2 will not work.", this);
         if (testItem3Details == null) Debug.LogWarning("InventoryTester: Test3 ItemDetails asset not assigned. F3 will not work.", this);
    }

    void Update()
    {
        // Ensure the inventory system is valid before trying to add items
        if (targetInventory == null || targetInventory.Combiner == null)
        {
            return; // Don't execute test logic if setup failed
        }

        // --- Test adding Test1 item ---
        if (Input.GetKeyDown(KeyCode.F1))
        {
            if (testItem1Details != null)
            {
                // Create a new runtime Item instance from the ScriptableObject details
                // We create a quantity of 1 for simplicity in this basic test
                Item newItemInstance = testItem1Details.Create(1);

                // Attempt to add the item instance to the inventory using the public AddItem method on Inventory
                // Inventory.AddItem will internally handle if it's stackable or non-stackable
                bool added = targetInventory.AddItem(newItemInstance); // *** MODIFIED ***

                if (added)
                {
                    Debug.Log($"Successfully added {testItem1Details.Name} to inventory. Remaining on instance: {newItemInstance.quantity}"); // Log remaining quantity
                }
                 else
                 {
                      Debug.LogWarning($"Failed to add {testItem1Details.Name}. Inventory might be full or filtering disallowed. Remaining on instance: {newItemInstance.quantity}"); // Log remaining quantity on failure
                 }
            }
            else
            {
                Debug.LogWarning("Cannot add Test1 item: Test1 ItemDetails asset not assigned in the Inspector.");
            }
        }

        // --- Test adding Test2 item ---
        if (Input.GetKeyDown(KeyCode.F2))
        {
            if (testItem2Details != null)
            {
                // Create a new runtime Item instance
                Item newItemInstance = testItem2Details.Create(1);

                // Attempt to add the item instance using the public AddItem method on Inventory
                 bool added = targetInventory.AddItem(newItemInstance); // *** MODIFIED ***

                 if (added)
                {
                    Debug.Log($"Successfully added {testItem2Details.Name} to inventory. Remaining on instance: {newItemInstance.quantity}"); // Log remaining quantity
                }
                else
                {
                     Debug.LogWarning($"Failed to add {testItem2Details.Name}. Inventory might be full or filtering disallowed. Remaining on instance: {newItemInstance.quantity}"); // Log remaining quantity on failure
                }
            }
             else
            {
                 Debug.LogWarning("Cannot add Test2 item: Test2 ItemDetails asset not assigned in the Inspector.");
            }
        }

        // --- Test adding Test3 item ---
        if (Input.GetKeyDown(KeyCode.F3))
        {
            if (testItem3Details != null)
            {
                // Create a new runtime Item instance
                Item newItemInstance = testItem3Details.Create(1);

                // Attempt to add the item instance using the public AddItem method on Inventory
                 bool added = targetInventory.AddItem(newItemInstance); // *** MODIFIED ***

                 if (added)
                {
                    Debug.Log($"Successfully added {testItem3Details.Name} to inventory. Remaining on instance: {newItemInstance.quantity}"); // Log remaining quantity
                }
                else
                {
                     Debug.LogWarning($"Failed to add {testItem3Details.Name}. Inventory might be full or filtering disallowed. Remaining on instance: {newItemInstance.quantity}"); // Log remaining quantity on failure
                }
            }
             else
            {
                 Debug.LogWarning("Cannot add Test3 item: Test3 ItemDetails asset not assigned in the Inspector."); // Corrected log message
            }
        }
    }
}