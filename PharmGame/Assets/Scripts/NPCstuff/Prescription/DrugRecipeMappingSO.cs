// --- START OF FILE DrugRecipeMappingSO.cs ---

using UnityEngine;
using System.Collections.Generic; // Needed for List
using System; // Needed for Serializable
using Systems.Inventory; // Needed for CraftingRecipe and ItemDetails
using System.Linq; // Needed for LINQ

namespace Systems.Crafting // Place in a suitable namespace for crafting configurations
{
    /// <summary>
    /// Defines a mapping between a prescription drug name, the required crafting recipe name,
    /// and the expected ItemDetails of the crafted output item.
    /// --- MODIFIED: Stores recipeName string instead of CraftingRecipe reference. ---
    /// </summary>
    [Serializable]
    public struct DrugMappingEntry
    {
        [Tooltip("The exact string name of the prescribed drug from the PrescriptionOrder.")]
        public string prescribedDrugName;

        [Tooltip("The exact string name of the CraftingRecipe required to make this drug (matches CraftingRecipe.recipeName).")]
        public string craftingRecipeName; // <-- MODIFIED: Store recipe name string

        [Tooltip("The ItemDetails of the finished, crafted item that the player needs to deliver.")]
        public ItemDetails craftedOutputItemDetails; // Reference to the ItemDetails asset

        // Optional: Add validation if needed (e.g., check if recipe outputs match craftedOutputItemDetails)
    }

    /// <summary>
    /// A ScriptableObject holding a collection of mappings between prescription drug names,
    /// crafting recipes, and expected output items.
    /// --- MODIFIED: Requires reference to all CraftingRecipesSO to look up recipes by name. ---
    /// </summary>
    [CreateAssetMenu(fileName = "New Drug Recipe Mapping", menuName = "Crafting/Drug Recipe Mapping")]
    public class DrugRecipeMappingSO : ScriptableObject
    {
        // --- NEW: Reference to all crafting recipes ---
        [Header("References")]
        [Tooltip("Reference to the ScriptableObject containing all crafting recipes, needed to look up recipes by name.")]
        [SerializeField] private CraftingRecipesSO allCraftingRecipes;
        // --- END NEW ---

        [Header("Mappings")]
        [Tooltip("The list of mappings between prescription drug names and crafting details.")]
        public List<DrugMappingEntry> mappings = new List<DrugMappingEntry>();

        /// <summary>
        /// Attempts to find a DrugMappingEntry for a given prescribed drug name.
        /// </summary>
        /// <param name="drugName">The name of the prescribed drug.</param>
        /// <param name="mappingEntry">Output: The found mapping entry, or default if not found.</param>
        /// <returns>True if a mapping was found, false otherwise.</returns>
        public bool TryGetMapping(string drugName, out DrugMappingEntry mappingEntry)
        {
            if (string.IsNullOrEmpty(drugName))
            {
                mappingEntry = default; // Return default struct
                return false;
            }

            foreach (var entry in mappings)
            {
                if (entry.prescribedDrugName.Equals(drugName, StringComparison.OrdinalIgnoreCase)) // Case-insensitive comparison
                {
                    mappingEntry = entry;
                    // Optional: Add validation here that recipe name and output details are not null/empty
                    if (string.IsNullOrEmpty(mappingEntry.craftingRecipeName))
                    {
                         Debug.LogWarning($"DrugRecipeMappingSO ({name}): Mapping found for drug '{drugName}', but Crafting Recipe Name is null or empty!", this);
                         // Decide if this should count as a valid mapping - let's return true but log warning
                    }
                     if (mappingEntry.craftedOutputItemDetails == null)
                    {
                         Debug.LogWarning($"DrugRecipeMappingSO ({name}): Mapping found for drug '{drugName}', but Crafted Output Item Details are null!", this);
                         // Decide if this should count as a valid mapping - let's return true but log warning
                    }
                    return true;
                }
            }

            mappingEntry = default; // Return default struct
            return false; // No mapping found
        }

        /// <summary>
        /// Attempts to find the CraftingRecipe for a given prescribed drug name.
        /// Looks up the recipe by name using the referenced allCraftingRecipes SO.
        /// --- MODIFIED: Uses recipeName string and looks up in allCraftingRecipes. ---
        /// </summary>
        /// <param name="drugName">The name of the prescribed drug.</param>
        /// <returns>The CraftingRecipe, or null if no mapping, recipe name, or recipe is found.</returns>
        public CraftingRecipe GetCraftingRecipeForDrug(string drugName)
        {
            if (allCraftingRecipes == null)
            {
                Debug.LogError($"DrugRecipeMappingSO ({name}): allCraftingRecipes reference is null! Cannot look up recipe by name.", this);
                return null;
            }

            if (TryGetMapping(drugName, out DrugMappingEntry entry))
            {
                 if (!string.IsNullOrEmpty(entry.craftingRecipeName))
                 {
                      // Find the recipe in the allCraftingRecipes list by name
                      CraftingRecipe foundRecipe = allCraftingRecipes.recipes.FirstOrDefault(r => r != null && r.recipeName == entry.craftingRecipeName);

                      if (foundRecipe == null)
                      {
                           Debug.LogWarning($"DrugRecipeMappingSO ({name}): Mapping found for drug '{drugName}' referencing recipe name '{entry.craftingRecipeName}', but no recipe with that name was found in allCraftingRecipes!", this);
                      }
                      return foundRecipe;
                 }
                 else
                 {
                      Debug.LogWarning($"DrugRecipeMappingSO ({name}): Mapping found for drug '{drugName}', but craftingRecipeName is null or empty.", this);
                      return null;
                 }
            }
            return null; // No mapping found
        }
        // --- END MODIFIED ---

        /// <summary>
        /// Attempts to find the expected crafted output ItemDetails for a given prescribed drug name.
        /// --- MODIFIED: Uses the updated DrugMappingEntry struct. ---
        /// </summary>
        /// <param name="drugName">The name of the prescribed drug.</param>
        /// <returns>The ItemDetails, or null if no mapping or output details are found.</returns>
        public ItemDetails GetCraftedOutputItemDetailsForDrug(string drugName)
        {
            if (TryGetMapping(drugName, out DrugMappingEntry entry))
            {
                return entry.craftedOutputItemDetails; // Return the stored ItemDetails reference
            }
            return null; // No mapping found
        }
        // --- END MODIFIED ---
    }
}
// --- END OF FILE DrugRecipeMappingSO.cs ---