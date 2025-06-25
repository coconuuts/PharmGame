// --- START OF FILE DrugRecipeMappingSO.cs ---

using UnityEngine;
using System.Collections.Generic; // Needed for List
using System; // Needed for Serializable, StringComparison
using Systems.Inventory; // Needed for CraftingRecipe and ItemDetails
using System.Linq; // Needed for LINQ (FirstOrDefault, Contains)

namespace Systems.Crafting // Place in a suitable namespace for crafting configurations
{
    /// <summary>
    /// Defines a mapping between a Crafting Recipe name, the expected ItemDetails of the crafted output item,
    /// and a list of multiple prescription drug names that map to this recipe/output.
    /// --- MODIFIED: Structure changed to map multiple drug names to one recipe/output. ---
    /// </summary>
    [Serializable]
    public struct RecipeMappingEntry // <-- RENAMED struct
    {
        [Tooltip("The exact string name of the CraftingRecipe required to make the item(s) associated with the listed drugs (matches CraftingRecipe.recipeName).")]
        public string craftingRecipeName; // <-- Represents the recipe

        [Tooltip("The ItemDetails of the finished, crafted item that the player needs to deliver for any of the listed drugs.")]
        public ItemDetails craftedOutputItemDetails; // Reference to the ItemDetails asset

        [Tooltip("The list of exact string names of the prescribed drugs from PrescriptionOrder that map to this recipe and output item.")]
        public List<string> prescribedDrugNames; // <-- NEW: List of drug names


        // Optional: Add validation if needed (e.g., check if recipe outputs match craftedOutputItemDetails)
        // Note: A simple constructor might be less useful here due to the list.
    }

    /// <summary>
    /// A ScriptableObject holding a collection of mappings between prescription drug names,
    /// crafting recipes, and expected output items.
    /// --- MODIFIED: Now maps multiple prescription drug names to a single recipe/output. ---
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
        [Tooltip("The list of mappings between crafting details (recipe, output) and the list of prescription drug names that use them.")]
        public List<RecipeMappingEntry> mappings = new List<RecipeMappingEntry>(); // <-- Uses the new struct

        /// <summary>
        /// Attempts to find the RecipeMappingEntry that contains the given prescribed drug name.
        /// --- MODIFIED: Searches the list of drug names within each entry. ---
        /// </summary>
        /// <param name="drugName">The name of the prescribed drug.</param>
        /// <param name="mappingEntry">Output: The found mapping entry, or default if not found.</param>
        /// <returns>True if a mapping containing the drug name was found, false otherwise.</returns>
        public bool TryGetRecipeMappingForDrug(string drugName, out RecipeMappingEntry mappingEntry) // <-- RENAMED method
        {
            if (string.IsNullOrEmpty(drugName))
            {
                mappingEntry = default; // Return default struct
                return false;
            }

            // Iterate through the new mapping entries
            foreach (var entry in mappings)
            {
                // Check if the prescribedDrugNames list in this entry contains the target drugName (case-insensitive)
                if (entry.prescribedDrugNames != null && entry.prescribedDrugNames.Any(name => name.Equals(drugName, StringComparison.OrdinalIgnoreCase))) // Use Any for robustness
                {
                    mappingEntry = entry;
                    // Optional: Add validation here that recipe name and output details are not null/empty
                    if (string.IsNullOrEmpty(mappingEntry.craftingRecipeName))
                    {
                         Debug.LogWarning($"DrugRecipeMappingSO ({name}): Mapping found for drug '{drugName}' (via list), but Crafting Recipe Name is null or empty!", this);
                         // Decide if this should count as a valid mapping - returning true but logging warning
                    }
                     if (mappingEntry.craftedOutputItemDetails == null)
                    {
                         Debug.LogWarning($"DrugRecipeMappingSO ({name}): Mapping found for drug '{drugName}' (via list), but Crafted Output Item Details are null!", this);
                         // Decide if this should count as a valid mapping - returning true but logging warning
                    }
                    return true; // Found the entry containing this drug name
                }
            }

            mappingEntry = default; // Return default struct
            return false; // No mapping entry found that contains this drug name
        }
        // --- END MODIFIED ---


        /// <summary>
        /// Attempts to find the CraftingRecipe for a given prescribed drug name.
        /// Looks up the recipe by name using the referenced allCraftingRecipes SO,
        /// after finding the correct mapping entry via the drug name list.
        /// --- MODIFIED: Uses the new lookup logic via TryGetRecipeMappingForDrug. ---
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

            // Use the new method to find the mapping entry based on the drug name
            if (TryGetRecipeMappingForDrug(drugName, out RecipeMappingEntry entry)) // <-- Use new lookup method
            {
                 if (!string.IsNullOrEmpty(entry.craftingRecipeName))
                 {
                      // Find the recipe in the allCraftingRecipes list by the recipe name found in the mapping entry
                      CraftingRecipe foundRecipe = allCraftingRecipes.recipes.FirstOrDefault(r => r != null && r.recipeName == entry.craftingRecipeName);

                      if (foundRecipe == null)
                      {
                           Debug.LogWarning($"DrugRecipeMappingSO ({name}): Mapping found for drug '{drugName}' referencing recipe name '{entry.craftingRecipeName}', but no recipe with that name was found in allCraftingRecipes!", this);
                      }
                      return foundRecipe;
                 }
                 else
                 {
                      Debug.LogWarning($"DrugRecipeMappingSO ({name}): Mapping found for drug '{drugName}', but craftingRecipeName is null or empty in the mapping entry.", this);
                      return null;
                 }
            }
            return null; // No mapping entry found for the given drug name
        }
        // --- END MODIFIED ---

        /// <summary>
        /// Attempts to find the expected crafted output ItemDetails for a given prescribed drug name.
        /// --- MODIFIED: Uses the new lookup logic via TryGetRecipeMappingForDrug. ---
        /// </summary>
        /// <param name="drugName">The name of the prescribed drug.</param>
        /// <returns>The ItemDetails, or null if no mapping or output details are found.</returns>
        public ItemDetails GetCraftedOutputItemDetailsForDrug(string drugName)
        {
            // Use the new method to find the mapping entry based on the drug name
            if (TryGetRecipeMappingForDrug(drugName, out RecipeMappingEntry entry)) // <-- Use new lookup method
            {
                // Return the stored ItemDetails reference from the found mapping entry
                return entry.craftedOutputItemDetails;
            }
            return null; // No mapping entry found for the given drug name
        }
        // --- END MODIFIED ---
    }
}