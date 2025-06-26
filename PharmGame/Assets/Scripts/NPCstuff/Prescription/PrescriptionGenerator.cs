// --- START OF FILE PrescriptionGenerator.cs ---

using UnityEngine;
using System.Collections.Generic; // Needed for List, HashSet
using System; // Needed for Serializable, StringComparison
using Game.Prescriptions; // Needed for PrescriptionOrder, PrescriptionManager // <-- Added PrescriptionManager
using Game.NPC.TI; // Needed for TiNpcManager
using Random = UnityEngine.Random; // Specify UnityEngine.Random
using System.Linq; // Needed for LINQ (ToList, Contains, FirstOrDefault, Where)

namespace Game.Prescriptions // Place in the Prescription namespace
{
    /// <summary>
    /// Defines the price per unit for a specific drug.
    /// </summary>
    [Serializable]
    public struct DrugPriceEntry
    {
        [Tooltip("The exact string name of the drug.")]
        public string drugName;
        [Tooltip("The price per unit of this drug.")]
        public float pricePerUnit;
    }

    /// <summary>
    /// Manages the generation of prescription orders, including patient names,
    /// prescribed drugs, and calculating the order's monetary worth.
    /// Handles the pool of random patient names and their replacement upon 'death'.
    /// MODIFIED: Coordinates with PrescriptionManager to ensure unique patient names. // <-- Added note
    /// </summary>
    public class PrescriptionGenerator : MonoBehaviour
    {
        // --- Singleton Instance (Optional but common for managers) ---
        // We'll make this a simple component for now as per the plan,
        // but keep in mind it might become a singleton later if needed elsewhere.
        // public static PrescriptionGenerator Instance { get; private set; } // Keep commented out if not needed as singleton


        [Header("References")]
        [Tooltip("Reference to the TiNpcManager instance in the scene.")]
        [SerializeField] private TiNpcManager tiNpcManager; // Will get via Instance if not assigned
        // --- NEW: Reference to PrescriptionManager ---
        [Tooltip("Reference to the PrescriptionManager instance in the scene.")]
        [SerializeField] private PrescriptionManager prescriptionManager; // Will get via Instance if not assigned
        // --- END NEW ---


        [Header("Patient Name Settings")]
        [Tooltip("Library of possible first names for random patient generation.")]
        [SerializeField] private List<string> firstNamesLibrary = new List<string> // <-- Populated list
        {
            "Alice", "Bob", "Charlie", "Diana", "Ethan", "Fiona", "George", "Hannah", "Isaac", "Jasmine",
            "Kevin", "Lily", "Michael", "Nora", "Oliver", "Penelope", "Quentin", "Rachel", "Samuel", "Tina",
            "Ursula", "Victor", "Wendy", "Xavier", "Yara", "Zachary", "Sophia", "Liam", "Olivia", "Noah"
        };
        [Tooltip("Library of possible last names for random patient generation.")]
        [SerializeField] private List<string> lastNamesLibrary = new List<string> // <-- Populated list
        {
            "Smith", "Johnson", "Williams", "Jones", "Brown", "Davis", "Miller", "Wilson", "Moore", "Taylor",
            "Anderson", "Thomas", "Jackson", "White", "Harris", "Martin", "Thompson", "Garcia", "Martinez", "Robinson",
            "Clark", "Rodriguez", "Lewis", "Lee", "Walker", "Hall", "Allen", "Young", "Hernandez", "King"
        };
        [Tooltip("The pool of randomly generated full names to draw from for transient patients.")]
        [SerializeField] private List<string> randomNamesPool = new List<string>(); // This list will be populated once
        [Tooltip("The target size for the random names pool.")]
        [SerializeField] private int randomNamesPoolSize = 50;
        [Tooltip("Maximum attempts to generate a unique random name before giving up.")]
        [SerializeField] private int maxRandomNameGenerationAttempts = 100; // Safeguard
        [Tooltip("Chance (0-1) for an order to be assigned to a TI NPC vs a random name, IF both are available and unique.")] // <-- Clarified tooltip
        [Range(0f, 1f)]
        [SerializeField] private float tiPatientChance = 0.5f; // 50% chance for a TI patient


        [Header("Drug Settings")]
        [Tooltip("List of common prescription drug names to choose from.")]
        [SerializeField] private List<string> commonDrugsList = new List<string> // <-- Populated list (10 drugs)
        {
            "Amoxicillin", "Lisinopril", "Atorvastatin", "Levothyroxine", "Metformin",
            "Gabapentin", "Hydrochlorothiazide", "Omeprazole", "Sertraline", "Albuterol"
        };
        [Tooltip("List of drug names and their corresponding prices per unit.")]
        [SerializeField] private List<DrugPriceEntry> drugPrices = new List<DrugPriceEntry> // <-- Populated list with example prices
        {
            new DrugPriceEntry { drugName = "Amoxicillin", pricePerUnit = 2.5f },
            new DrugPriceEntry { drugName = "Lisinopril", pricePerUnit = 1.2f },
            new DrugPriceEntry { drugName = "Atorvastatin", pricePerUnit = 3.8f },
            new DrugPriceEntry { drugName = "Levothyroxine", pricePerUnit = 0.9f },
            new DrugPriceEntry { drugName = "Metformin", pricePerUnit = 1.5f },
            new DrugPriceEntry { drugName = "Gabapentin", pricePerUnit = 2.1f },
            new DrugPriceEntry { drugName = "Hydrochlorothiazide", pricePerUnit = 0.7f },
            new DrugPriceEntry { drugName = "Omeprazole", pricePerUnit = 1.9f },
            new DrugPriceEntry { drugName = "Sertraline", pricePerUnit = 4.5f },
            new DrugPriceEntry { drugName = "Albuterol", pricePerUnit = 3.1f }
        };
        [Tooltip("Default price per unit if a drug is not found in the drugPrices list.")]
        [SerializeField] private float defaultPricePerUnit = 1.0f;


        [Header("Order Value Settings")]
        [Tooltip("Minimum dose per day for a generated order.")]
        [SerializeField] private int minDosePerDay = 1;
        [Tooltip("Maximum dose per day for a generated order.")]
        [SerializeField] private int maxDosePerDay = 3; // Max is exclusive in Random.Range(int, int)
        [Tooltip("Minimum length of treatment in days for a generated order.")]
        [SerializeField] private int minLengthOfTreatmentDays = 3;
        [Tooltip("Maximum length of treatment in days for a generated order.")]
        [SerializeField] private int maxLengthOfTreatmentDays = 6; // Max is exclusive


        // --- Internal Data ---
        // TI NPC IDs will be fetched dynamically from TiNpcManager


        private void Awake()
        {
            // If making this a singleton:
            // if (Instance == null)
            // {
            //     Instance = this;
            // }
            // else
            // {
            //     Debug.LogWarning("PrescriptionGenerator: Duplicate instance found. Destroying this one.", this);
            //     Destroy(gameObject);
            // }

            Debug.Log("PrescriptionGenerator: Awake completed.");
        }

        private void Start()
        {
            // Get reference to TiNpcManager singleton
            if (tiNpcManager == null)
            {
                tiNpcManager = TiNpcManager.Instance;
            }
            if (tiNpcManager == null)
            {
                Debug.LogError("PrescriptionGenerator: TiNpcManager instance not found or not assigned! Cannot generate TI patient names.", this);
                // Do NOT disable, random names and drugs can still be generated.
            }

            // --- NEW: Get reference to PrescriptionManager singleton ---
            if (prescriptionManager == null)
            {
                 prescriptionManager = PrescriptionManager.Instance;
            }
            if (prescriptionManager == null)
            {
                Debug.LogError("PrescriptionGenerator: PrescriptionManager instance not found or not assigned! Cannot ensure unique patient names.", this);
                // This is a critical dependency for name uniqueness. Generation might produce duplicates.
            }
            // --- END NEW ---


            // Populate the random names pool if it's not already populated
            if (randomNamesPool.Count < randomNamesPoolSize)
            {
                PopulateRandomNamesPool();
            }

            Debug.Log("PrescriptionGenerator: Start completed. Manager references acquired.");
        }

        /// <summary>
        /// Populates the random names pool by combining first and last names.
        /// Called once on start if the pool is not already full.
        /// </summary>
        private void PopulateRandomNamesPool()
        {
            if (firstNamesLibrary == null || firstNamesLibrary.Count == 0)
            {
                Debug.LogError("PrescriptionGenerator: First names library is empty! Cannot populate random names pool.", this);
                return;
            }
             if (lastNamesLibrary == null || lastNamesLibrary.Count == 0)
            {
                Debug.LogError("PrescriptionGenerator: Last names library is empty! Cannot populate random names pool.", this);
                return;
            }

            Debug.Log($"PrescriptionGenerator: Populating random names pool to size {randomNamesPoolSize}...");

            // Clear existing names if any, to ensure we reach the target size from scratch
            randomNamesPool.Clear();

            int attempts = 0;
            while (randomNamesPool.Count < randomNamesPoolSize && attempts < maxRandomNameGenerationAttempts * randomNamesPoolSize) // Add safeguard
            {
                string randomFirstName = firstNamesLibrary[Random.Range(0, firstNamesLibrary.Count)];
                string randomLastName = lastNamesLibrary[Random.Range(0, lastNamesLibrary.Count)];
                string fullName = $"{randomFirstName} {randomLastName}";

                // Check for uniqueness within the pool being built
                if (!randomNamesPool.Contains(fullName))
                {
                    randomNamesPool.Add(fullName);
                    // Debug.Log($"PrescriptionGenerator: Added '{fullName}' to random names pool. Pool size: {randomNamesPool.Count}"); // Too noisy
                }
                attempts++;
            }

            if (randomNamesPool.Count < randomNamesPoolSize)
            {
                Debug.LogWarning($"PrescriptionGenerator: Could only generate {randomNamesPool.Count} unique random names for the pool after {attempts} attempts. Libraries might be too small or max attempts too low.", this);
            }
            else
            {
                 Debug.Log($"PrescriptionGenerator: Successfully populated random names pool with {randomNamesPool.Count} names.");
            }
        }

        /// <summary>
        /// Attempts to find the price per unit for a given drug name.
        /// Returns defaultPricePerUnit if the drug is not found in the list.
        /// </summary>
        private float GetDrugPricePerUnit(string drugName)
        {
            if (string.IsNullOrEmpty(drugName))
            {
                Debug.LogWarning("PrescriptionGenerator: Attempted to get price for null or empty drug name. Returning default price.", this);
                return defaultPricePerUnit;
            }

            // Use LINQ FirstOrDefault to find the entry
            DrugPriceEntry? entry = drugPrices.FirstOrDefault(e => e.drugName.Equals(drugName, StringComparison.OrdinalIgnoreCase));

            // Check if a matching entry was found
            if (entry.HasValue)
            {
                return entry.Value.pricePerUnit;
            }
            else
            {
                Debug.LogWarning($"PrescriptionGenerator: Drug '{drugName}' not found in drugPrices list. Returning default price ({defaultPricePerUnit}).", this);
                return defaultPricePerUnit;
            }
        }


        /// <summary>
        /// Generates a new prescription order with a patient name (TI or random),
        /// a common drug, random dosage/length, and calculated money worth.
        /// Ensures the patient name is unique among currently active/unassigned orders.
        /// </summary>
        /// <returns>A new PrescriptionOrder struct, or a default/invalid struct if a unique name could not be generated.</returns>
        public PrescriptionOrder GenerateNewOrder()
        {
            string patientName = null; // Start with null to indicate no name found yet
            string prescribedDrug = "Unknown Drug"; // Default fallback
            int dosePerDay = minDosePerDay;
            int lengthOfTreatmentDays = minLengthOfTreatmentDays;
            float moneyWorth = 0f;
            bool illegal = false; // Keep false for now

            if (prescriptionManager == null)
            {
                Debug.LogError("PrescriptionGenerator: PrescriptionManager reference is null! Cannot ensure unique patient names. Returning default order.", this);
                return default(PrescriptionOrder); // Return invalid order
            }

            // --- Get currently used names from the manager ---
            HashSet<string> usedNames = prescriptionManager.GetCurrentlyUsedPatientNames();

            // --- Get available TI and Random names that are NOT currently used ---
            List<string> availableTiIds = new List<string>();
            if (tiNpcManager != null)
            {
                availableTiIds = tiNpcManager.GetTiNpcIds().Where(id => !usedNames.Contains(id)).ToList();
            }

            List<string> availableRandomNames = randomNamesPool.Where(name => !usedNames.Contains(name)).ToList();

            bool canUseTi = availableTiIds.Count > 0;
            bool canUseRandom = availableRandomNames.Count > 0;

            // --- Select Patient Name (Attempt to find a unique one) ---
            if (!canUseTi && !canUseRandom)
            {
                Debug.LogWarning("PrescriptionGenerator: Cannot generate a unique patient name. All available TI and random names are currently in use. Returning default order.", this);
                return default(PrescriptionOrder); // No unique name found, return invalid order
            }

            // Attempt to pick a name, prioritizing based on chance if both are available
            bool picked = false;
            int attempts = 0;
            int maxAttemptsForThisOrder = 10; // Limit attempts for a single order generation

            while (!picked && attempts < maxAttemptsForThisOrder)
            {
                attempts++;
                bool tryTi = canUseTi && (!canUseRandom || Random.value < tiPatientChance);

                if (tryTi)
                {
                    // Try picking from available TI names
                    if (availableTiIds.Count > 0)
                    {
                         string potentialName = availableTiIds[Random.Range(0, availableTiIds.Count)];
                         // This check is technically redundant because availableTiIds is already filtered,
                         // but keeping it adds robustness if the filtering logic changes or fails.
                         if (!usedNames.Contains(potentialName))
                         {
                             patientName = potentialName;
                             picked = true;
                             // Debug.Log($"PrescriptionGenerator: Picked unique TI patient name: {patientName} (Attempt {attempts})"); // Too noisy
                         } else {
                             // Should not happen if filtering is correct
                             Debug.LogWarning($"PrescriptionGenerator: Picked '{potentialName}' from available TI list, but it's marked as used. This indicates a logic error in filtering or tracking.", this);
                         }
                    } else {
                         // If we tried TI but the filtered list was empty (shouldn't happen if canUseTi was true),
                         // fall through to try random names.
                         tryTi = false; // Force next check to be random
                    }
                }

                if (!picked && canUseRandom)
                {
                    // Try picking from available random names
                    if (availableRandomNames.Count > 0)
                    {
                        string potentialName = availableRandomNames[Random.Range(0, availableRandomNames.Count)];
                         // This check is technically redundant because availableRandomNames is already filtered,
                         // but keeping it adds robustness.
                         if (!usedNames.Contains(potentialName))
                         {
                             patientName = potentialName;
                             picked = true;
                             // Debug.Log($"PrescriptionGenerator: Picked unique random patient name: {patientName} (Attempt {attempts})"); // Too noisy
                         } else {
                             // Should not happen if filtering is correct
                             Debug.LogWarning($"PrescriptionGenerator: Picked '{potentialName}' from available random list, but it's marked as used. This indicates a logic error in filtering or tracking.", this);
                         }
                    } else {
                        // If we tried random but the filtered list was empty (shouldn't happen if canUseRandom was true),
                        // and we couldn't use TI, the loop will eventually finish without picking.
                    }
                }

                // If neither TI nor Random pool yielded a unique name in this attempt,
                // and we are still within attempts, the loop continues.
                // If attempts run out, picked remains false.
            }

            if (!picked || string.IsNullOrEmpty(patientName))
            {
                 Debug.LogWarning($"PrescriptionGenerator: Failed to find a unique patient name after {maxAttemptsForThisOrder} attempts. Available TI: {availableTiIds.Count}, Available Random: {availableRandomNames.Count}. Returning default order.", this);
                 return default(PrescriptionOrder); // Failed to find a unique name within attempts
            }


            // --- Select Prescribed Drug ---
            if (commonDrugsList != null && commonDrugsList.Count > 0)
            {
                prescribedDrug = commonDrugsList[Random.Range(0, commonDrugsList.Count)];
            }
            else
            {
                Debug.LogWarning("PrescriptionGenerator: Common drugs list is empty! Using default drug name.", this);
            }

            // --- Determine Dosage and Length ---
            // Ensure min/max are valid
            if (minDosePerDay > maxDosePerDay) minDosePerDay = maxDosePerDay;
            if (minLengthOfTreatmentDays > maxLengthOfTreatmentDays) minLengthOfTreatmentDays = maxLengthOfTreatmentDays;

            dosePerDay = Random.Range(minDosePerDay, maxDosePerDay + 1); // +1 because max is exclusive
            lengthOfTreatmentDays = Random.Range(minLengthOfTreatmentDays, maxLengthOfTreatmentDays + 1); // +1 because max is exclusive

            // --- Calculate Money Worth ---
            float pricePerUnit = GetDrugPricePerUnit(prescribedDrug);
            int totalUnits = dosePerDay * lengthOfTreatmentDays;
            moneyWorth = (pricePerUnit * totalUnits) + 15.0f;

            // Create and return the new order
            PrescriptionOrder newOrder = new PrescriptionOrder(patientName, prescribedDrug, dosePerDay, lengthOfTreatmentDays, illegal, moneyWorth);
            return newOrder;
        }


        /// <summary>
        /// Removes a specific name from the random names pool and replaces it with a new one.
        /// Called when a transient NPC with a name from this pool is marked as 'dead'.
        /// </summary>
        /// <param name="nameToRemove">The name to remove from the pool.</param>
        public void RemoveRandomNameAndReplace(string nameToRemove)
        {
             if (string.IsNullOrEmpty(nameToRemove))
             {
                  Debug.LogWarning("PrescriptionGenerator: Attempted to remove null or empty name from random names pool. Ignoring.", this);
                  return;
             }

             if (randomNamesPool.Contains(nameToRemove))
             {
                  Debug.Log($"PrescriptionGenerator: Removing '{nameToRemove}' from random names pool.", this);
                  randomNamesPool.Remove(nameToRemove);

                  // Generate a single new unique name to replace it
                  if (firstNamesLibrary == null || firstNamesLibrary.Count == 0 || lastNamesLibrary == null || lastNamesLibrary.Count == 0)
                  {
                       Debug.LogError("PrescriptionGenerator: First or last names library is empty! Cannot generate replacement name for random names pool.", this);
                       return;
                  }

                  string newFullName = null;
                  int attempts = 0;
                  // Loop until a unique name is generated or max attempts reached
                  // Need to also check against names currently *in use* by the manager,
                  // not just names already in the pool.
                  HashSet<string> usedNames = prescriptionManager?.GetCurrentlyUsedPatientNames() ?? new HashSet<string>(); // Get used names, handle null manager

                  while (newFullName == null && attempts < maxRandomNameGenerationAttempts)
                  {
                       string randomFirstName = firstNamesLibrary[Random.Range(0, firstNamesLibrary.Count)];
                       string randomLastName = lastNamesLibrary[Random.Range(0, lastNamesLibrary.Count)];
                       string generatedName = $"{randomFirstName} {randomLastName}";

                       // Check for uniqueness against the CURRENT pool AND currently used names
                       if (!randomNamesPool.Contains(generatedName) && !usedNames.Contains(generatedName))
                       {
                           newFullName = generatedName;
                       }
                       attempts++;
                  }

                  if (newFullName != null)
                  {
                       randomNamesPool.Add(newFullName);
                       Debug.Log($"PrescriptionGenerator: Replaced '{nameToRemove}' with new random name '{newFullName}'. Pool size: {randomNamesPool.Count}.", this);
                  }
                  else
                  {
                       Debug.LogWarning($"PrescriptionGenerator: Could not generate a unique replacement name after {maxRandomNameGenerationAttempts} attempts. Random names pool size is now {randomNamesPool.Count}. Libraries might be too small or all generated names are already in use.", this);
                  }
             }
             else
             {
                  // This might happen if a TI NPC somehow got a random name assigned, or if the name was already removed.
                  // It's not necessarily an error, but worth noting.
                  Debug.LogWarning($"PrescriptionGenerator: Attempted to remove name '{nameToRemove}' from random names pool, but it was not found. Ignoring.", this);
             }
        }
    }
}