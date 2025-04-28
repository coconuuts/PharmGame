using UnityEngine;
using Systems.Inventory;
using System.Collections.Generic;
using System.Linq; // Needed for FirstOrDefault

namespace Prescription // Use the Prescription namespace
{
    /// <summary>
    /// Abstract base class for handling the unique processing sequence
    /// for a specific type of prescription item.
    /// Listens to OnProcessButtonClicked event.
    /// </summary>
    public abstract class PrescriptionProcessor : MonoBehaviour
    {
        [Tooltip("The ItemLabel type this processor handles (e.g., PillStock). This must match the main inventory item label that triggers this processor.")]
        [SerializeField]
        protected ItemLabel processorType;

        // References needed for processing. Set during Initialize.
        protected Inventory specificInventory; // The specific inventory for this type (where the secondary ingredient is)
        protected Inventory outputInventory; // The inventory where the crafted item goes
        protected Dictionary<ItemDetails, CraftingRecipe> recipeMap; // The map of recipes

        public ItemLabel ProcessorType => processorType;

        // --- Event Subscription ---
        protected virtual void OnEnable()
        {
            // Subscribe to the event that signals the process button was clicked
            PrescriptionEvents.OnProcessButtonClicked += HandleProcessButtonClicked;
            Debug.Log($"{GetType().Name}: Subscribed to OnProcessButtonClicked.", this);
        }

        protected virtual void OnDisable()
        {
            // Unsubscribe when disabled or destroyed
            PrescriptionEvents.OnProcessButtonClicked -= HandleProcessButtonClicked;
            Debug.Log($"{GetType().Name}: Unsubscribed from OnProcessButtonClicked.", this);
        }
        // --- End Event Subscription ---


        /// <summary>
        /// Initializes the processor with necessary inventory references and the recipe map.
        /// Called by the PrescriptionTableManager during setup.
        /// </summary>
        /// <param name="specificInv">The Inventory component for this specific prescription type.</param>
        /// <param name="outputInv">The main output Inventory component.</param>
        /// <param name="recipeMap">The dictionary mapping secondary ingredients to crafting recipes.</param>
        public virtual void Initialize(Inventory specificInv, Inventory outputInv, Dictionary<ItemDetails, CraftingRecipe> recipeMap)
        {
            specificInventory = specificInv; // Store a reference to *this* processor's specific inventory
            outputInventory = outputInv;
            this.recipeMap = recipeMap;

            // Validation checks for assigned references
            if (specificInventory == null) Debug.LogError($"{GetType().Name}: Specific Inventory is null after Initialize!", this);
            if (outputInventory == null) Debug.LogError($"{GetType().Name}: Output Inventory is null after Initialize!", this);
            if (recipeMap == null) Debug.LogError($"{GetType().Name}: Recipe Map is null after Initialize!", this);

            Debug.Log($"{GetType().Name}: Initialized with references.", this);
        }

        /// <summary>
        /// Event handler for OnProcessButtonClicked. Checks if the event is for this processor type
        /// and triggers the processing logic if it matches.
        /// </summary>
        private void HandleProcessButtonClicked(ItemLabel label, Inventory mainInventory, Item mainIngredient, Inventory specificInventory, Item secondaryIngredient)
        {
            // Check if the event is relevant to this specific processor instance
            if (label == processorType)
            {
                 Debug.Log($"{GetType().Name}: Handling OnProcessButtonClicked for matching label {label}.", this);
                 // Call the core processing logic
                 // Pass the inventories and items received from the event
                 Process(mainInventory, mainIngredient, specificInventory, secondaryIngredient);
            }
            // else { Debug.Log($"DEBUG: {GetType().Name}: Ignoring OnProcessButtonClicked for label {label}. My type is {processorType}.", this); }
        }


        /// <summary>
        /// Executes the unique processing sequence for this prescription type.
        /// Finds the specific recipe, consumes ingredients, and creates the result.
        /// This is the core logic called by the event handler.
        /// Derived classes can override this for custom logic beyond simple crafting if needed.
        /// </summary>
        /// <param name="mainInventory">The main prescription table Inventory.</param>
        /// <param name="mainIngredient">The Item instance from the main inventory that triggered this processor.</param>
        /// <param name="specificInventory">The specific inventory instance currently active (where the secondary ingredient is).</param>
        /// <param name="secondaryIngredient">The Item instance from the specific inventory that acts as the key ingredient.</param>
        public virtual void Process(Inventory mainInventory, Item mainIngredient, Inventory specificInventory, Item secondaryIngredient)
        {
             // --- Input validation checks ---
             // These should ideally also be checked in the event invoker, but double-check here for safety.
             if (mainInventory == null || mainInventory.Combiner == null) { Debug.LogError($"{GetType().Name}: Process called with null main inventory or combiner.", this); PrescriptionEvents.InvokeCraftingFailed(processorType, "Null main inventory."); return; } // Added failure event
             if (specificInventory == null || specificInventory.Combiner == null) { Debug.LogError($"{GetType().Name}: Process called with null specific inventory or combiner.", this); PrescriptionEvents.InvokeCraftingFailed(processorType, "Null specific inventory."); return; } // Added failure event
             if (mainIngredient == null || mainIngredient.details == null) { Debug.LogError($"{GetType().Name}: Process called with null or detail-less main ingredient.", this); PrescriptionEvents.InvokeCraftingFailed(processorType, "Invalid main ingredient."); return; } // Added failure event
             if (secondaryIngredient == null || secondaryIngredient.details == null) { Debug.LogError($"{GetType().Name}: Process called with null or detail-less secondary ingredient.", this); PrescriptionEvents.InvokeCraftingFailed(processorType, "Invalid secondary ingredient."); return; } // Added failure event
             if (outputInventory == null || outputInventory.Combiner == null) { Debug.LogError($"{GetType().Name}: Output Inventory or its Combiner is null. Cannot craft item.", this); PrescriptionEvents.InvokeCraftingFailed(processorType, "Null output inventory."); return; } // Added failure event
             if (recipeMap == null) { Debug.LogError($"{GetType().Name}: Recipe Map is null. Cannot find recipe.", this); PrescriptionEvents.InvokeCraftingFailed(processorType, "Recipe map is null."); return; } // Added failure event


             Debug.Log($"--- Processing Recipe: Main='{mainIngredient.details.Name}', Secondary='{secondaryIngredient.details.Name}' ---", this);

             // 1. Find the recipe based on the secondary ingredient details
             if (!recipeMap.TryGetValue(secondaryIngredient.details, out CraftingRecipe recipe))
             {
                 Debug.LogWarning($"{GetType().Name}: No crafting recipe found for secondary ingredient: '{secondaryIngredient.details.Name}'. Aborting process.", this);
                 PrescriptionEvents.InvokeCraftingFailed(processorType, $"No recipe found for secondary ingredient '{secondaryIngredient.details.Name}'."); // Fire failure event
                 return;
             }

             // Log recipe details found
             Debug.Log($"Found Recipe: Consumes {recipe.mainQuantityToConsume}x '{recipe.mainIngredientToConsume?.Name}' from main, {recipe.secondaryQuantityToConsume}x '{recipe.secondaryIngredient?.Name}' from specific. Produces {recipe.resultQuantity}x '{recipe.resultItem?.Name}'.", this);


             // 2. Consume ingredients using TryRemoveQuantity from the correct Inventory's Combiner
             // TryRemoveQuantity returns the actual quantity removed. Check if it matches the required quantity.
             // Quantities should ideally be checked BEFORE event is fired (in PrescriptionTableManager), but re-check here for safety/robustness.
             int removedMainQty = mainInventory.Combiner.TryRemoveQuantity(recipe.mainIngredientToConsume, recipe.mainQuantityToConsume);
             int removedSecondaryQty = specificInventory.Combiner.TryRemoveQuantity(recipe.secondaryIngredient, recipe.secondaryQuantityToConsume);


             if (removedMainQty != recipe.mainQuantityToConsume)
             {
                  Debug.LogError($"{GetType().Name}: Failed to consume full quantity of main ingredient '{recipe.mainIngredientToConsume?.Name}'. Needed {recipe.mainQuantityToConsume}, removed {removedMainQty}. Aborting crafting.", this);
                  // TODO: Implement rollback logic if one consumption fails after the other succeeds.
                  // For now, we'll leave the partial consumption.
                  PrescriptionEvents.InvokeCraftingFailed(processorType, $"Failed to consume main ingredient {recipe.mainIngredientToConsume?.Name}. Needed {recipe.mainQuantityToConsume}, removed {removedMainQty}."); // Fire failure event
                  return; // Abort crafting on failed consumption
             }
             if (removedSecondaryQty != recipe.secondaryQuantityToConsume)
              {
                   Debug.LogError($"{GetType().Name}: Failed to consume full quantity of secondary ingredient '{recipe.secondaryIngredient?.Name}'. Needed {recipe.secondaryQuantityToConsume}, removed {removedSecondaryQty}. Aborting crafting.", this);
                  // TODO: Implement rollback logic if one consumption fails after the other succeeds.
                   PrescriptionEvents.InvokeCraftingFailed(processorType, $"Failed to consume secondary ingredient {recipe.secondaryIngredient?.Name}. Needed {recipe.secondaryQuantityToConsume}, removed {removedSecondaryQty}."); // Fire failure event
                   return; // Abort crafting on failed consumption
              }

             Debug.Log("Ingredients consumed successfully.", this);

             // Fire OnIngredientsConsumed Event after successful consumption
             PrescriptionEvents.InvokeIngredientsConsumed(processorType, recipe.resultItem, recipe.resultQuantity);


             // 3. Create and add the result item to the output inventory
             if (recipe.resultItem != null)
             {
                  Item craftedItem = new Item(recipe.resultItem, recipe.resultQuantity); // Create new item instance

                  if (outputInventory.Combiner.AddItem(craftedItem))
                  {
                      Debug.Log($"Successfully crafted and added {craftedItem.quantity}x '{craftedItem.details?.Name}' to Output Inventory.", this);

                      // *** Fire OnCraftingComplete Event, including the processor type label ***
                      PrescriptionEvents.InvokeCraftingComplete(processorType, craftedItem);

                      // TODO: Trigger crafting complete animations, sounds, etc. (Maybe these listen to OnCraftingComplete)

                  }
                  else
                  {
                      Debug.LogWarning($"{GetType().Name}: Failed to add crafted item '{craftedItem.details?.Name}' to Output Inventory. Output inventory might be full.", this);
                      // TODO: Provide feedback to the player (e.g., UI message)
                      // TODO: Decide what happens if crafting succeeds but output inventory is full (lose ingredients? drop item?)
                      // For now, ingredients are consumed even if output is full.
                      PrescriptionEvents.InvokeCraftingFailed(processorType, $"Failed to add crafted item '{craftedItem.details?.Name}' to output inventory."); // Fire failure event
                  }
             }
             else
             {
                 Debug.LogWarning($"{GetType().Name}: Recipe for secondary ingredient '{secondaryIngredient.details.Name}' has a null result item. Nothing was crafted.", this);
                 PrescriptionEvents.InvokeCraftingFailed(processorType, $"Recipe has null result item for secondary ingredient '{secondaryIngredient.details.Name}'."); // Fire failure event
             }

            Debug.Log($"--- Processing Logic Complete ---", this);
        }

        // Derived classes can override HandleProcessButtonClicked for unique pre-processing (like minigame)
        // or override Process for unique consumption/creation logic, but typically override HandleProcessButtonClicked
        // to add steps BEFORE or AFTER the base.HandleProcessButtonClicked (which calls base.Process)
        /*
        private void HandleProcessButtonClicked(ItemLabel label, Inventory mainInventory, Item mainIngredient, Inventory specificInventory, Item secondaryIngredient)
        {
             if (label == processorType)
             {
                 Debug.Log($"PillProcessor: Handling OnProcessButtonClicked for matching label {label}. Starting minigame...", this);
                 // --- Unique Pre-processing Steps (e.g., trigger minigame) ---
                 // Play your minigame here...
                 bool minigameSuccessful = true; // Placeholder

                 if (minigameSuccessful)
                 {
                      Debug.Log($"PillProcessor: Minigame successful. Proceeding with crafting.", this);
                     // Call the base handler which executes the core crafting logic (consumption, creation, firing events)
                     base.HandleProcessButtonClicked(label, mainInventory, mainIngredient, specificInventory, secondaryIngredient);

                     // --- Unique Post-processing Steps (e.g., play special effects) ---
                      Debug.Log($"PillProcessor: Minigame and crafting complete. Playing unique effects.", this);
                      // Trigger unique particle effects, sounds, etc. (e.g., instantiate particles using a serialized prefab field)
                      // if (pillProcessingEffectPrefab != null) { Instantiate(pillProcessingEffectPrefab, transform.position, Quaternion.identity); }
                 }
                 else
                 {
                      Debug.LogWarning($"PillProcessor: Minigame failed. Crafting aborted.", this);
                      PrescriptionEvents.InvokeCraftingFailed(processorType, "Minigame failed."); // Fire failure event
                 }
             }
        }
        */
    }
}