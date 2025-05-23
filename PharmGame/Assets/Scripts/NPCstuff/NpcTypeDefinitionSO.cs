// --- FIXES IN NpcTypeDefinitionSO.cs ---
using UnityEngine;
using System;
using System.Collections.Generic;
using Game.NPC.States; // Needed for NpcStateSO
// Needed for System.Enum and System.Collections.Generic.Dictionary is covered by System

namespace Game.NPC.Types
{
     /// <summary>
     /// Defines a collection of states associated with a specific NPC type (e.g., General, Customer, Guard).
     /// Can reference a base type for hierarchical state loading.
     /// </summary>
     [CreateAssetMenu(fileName = "NpcTypeDefinition", menuName = "NPC/Type Definition", order = 0)]
     public class NpcTypeDefinitionSO : ScriptableObject
     {
          [Header("Type Settings")]
          [Tooltip("Optional: A base type definition to inherit states from.")]
          [SerializeField] private NpcTypeDefinitionSO baseType;

          // Use a serializable wrapper for the dictionary to expose it in the inspector
          [Tooltip("States associated with this specific type, overriding base states if IDs match.")]
          [SerializeField] private NpcStateDictionary stateCollection = new NpcStateDictionary();

          [Header("Starting State")]
          [Tooltip("If this type is the primary type for an NPC, this is the first state it enters after generic initialization.")]
          [SerializeField] private string primaryStartingStateEnumKey; // Enum key as string
          [Tooltip("The Type name of the Enum key for the primary starting state (e.g., Game.NPC.CustomerState).")]
          [SerializeField] private string primaryStartingStateEnumType; // Enum Type name as string

          /// <summary>
          /// Gets the configured primary starting state Enum key.
          /// </summary>
          public string PrimaryStartingStateEnumKey => primaryStartingStateEnumKey;

          /// <summary>
          /// Gets the configured primary starting state Enum Type name.
          /// </summary>
          public string PrimaryStartingStateEnumType => primaryStartingStateEnumType;

          // Property to access the states defined directly on this type
          // Accessing the internal list for iteration requires making SerializableKeyValuePair public or providing an iterator.
          // Let's make the SerializableKeyValuePair public for now, or provide an iterator/public list getter in NpcStateDictionary.
          // Making SerializableKeyValuePair public is simpler for Unity serialization access.
          public List<SerializableKeyValuePair> GetStateEntries() // Provide a public getter for the entries list
          {
               return stateCollection.entries; // Access the internal list via a public getter
          }

          /// <summary>
          /// Gets the base type definition, if any.
          /// </summary>
          public NpcTypeDefinitionSO BaseType => baseType;


          // --- Helper classes for Unity Serialization ---

          [Serializable]
          public class NpcStateDictionary
          {
               // MAKE THE ENTRIES LIST PUBLIC for access by NpcTypeDefinitionSO
               public List<SerializableKeyValuePair> entries = new List<SerializableKeyValuePair>(); // <-- FIX 1: Make entries public
          }

          // MAKE THE STRUCT PUBLIC for access by the outer class and Unity serialization
          [Serializable]
          public struct SerializableKeyValuePair // <-- FIX 2: Make struct public
          {
               // Fields remain private, but accessed via public properties/getters
               [SerializeField] private string stateEnumName;
               [SerializeField] private string stateEnumType;
               [SerializeField] private NpcStateSO stateSO;

               // Public properties to access the private fields
               public string StateEnumName => stateEnumName; // <-- FIX 4: Public property for stateEnumName
               public string StateEnumType => stateEnumType; // <-- FIX 5: Public property for stateEnumType
               public NpcStateSO StateSO => stateSO;         // <-- FIX 6: Public property for stateSO


               // Runtime property to get the actual Enum value (remains the same)
               public Enum StateEnum
               {
                    get
                    {
                         if (string.IsNullOrEmpty(stateEnumName) || string.IsNullOrEmpty(stateEnumType) || stateSO == null) return null;

                         try
                         {
                              Type enumType = Type.GetType(stateEnumType);
                              if (enumType == null || !enumType.IsEnum) { return null; }
                              return (Enum)Enum.Parse(enumType, stateEnumName);
                         }
                         catch (Exception e) // <-- FIX: Use discard '_' instead of 'e'
                         {
                              Debug.LogError($"NpcTypeDefinitionSO: Failed to parse enum '{stateEnumName}' of type '{stateEnumType}': {e.Message}", stateSO);
                              return null;
                         }
                    }
               }

               // Constructor for creating entries in code if needed
               public SerializableKeyValuePair(Enum stateEnum, NpcStateSO stateSO)
               {
                    this.stateEnumName = stateEnum.ToString();
                    this.stateEnumType = stateEnum.GetType().AssemblyQualifiedName; // Use AssemblyQualifiedName for reliability
                    this.stateSO = stateSO;
               }

               // Need a way to get the Runtime Type of the enum (e.g. typeof(CustomerState))
               public Type GetEnumType() // <-- FIX 7: Add method to get Enum Type
               {
                    if (string.IsNullOrEmpty(stateEnumType)) return null;
                    return Type.GetType(stateEnumType);
               }
          }

          // Consider adding methods to validate the stateCollection dictionary after loading (e.g., in OnEnable or a custom editor script)
          // public void OnEnable() { ... validate ... }

          // Method to get all states from this type definition, including base types (for Runner to use)
          public Dictionary<Enum, NpcStateSO> GetAllStates()
          {
               var allStates = new Dictionary<Enum, NpcStateSO>();

               // 1. Get states from base type first (if any)
               if (baseType != null)
               {
                    // Check for circular dependencies (simple check)
                    if (baseType == this)
                    {
                         Debug.LogError($"NpcTypeDefinitionSO ({name}): Circular dependency detected! 'baseType' is set to itself.", this);
                    }
                    else
                    {
                         foreach (var pair in baseType.GetAllStates()) // Recursively get states from the base type tree
                         {
                              // Ensure the key is valid before adding
                              if (pair.Key != null && pair.Value != null)
                              {
                                   allStates[pair.Key] = pair.Value; // Add base states
                              }
                              else
                              {
                                   Debug.LogWarning($"NpcTypeDefinitionSO ({name}): Found invalid state pair ({pair.Key?.ToString() ?? "NULL Key"}, {pair.Value?.name ?? "NULL Value"}) from base type '{baseType.name}'. Skipping.", this);
                              }
                         }
                    }
               }

               // 2. Add/Override states from this type
               // Access the internal list using the new public getter or directly if needed
               foreach (var entry in stateCollection.entries) // Access the internal list directly (now public)
               {
                    // Get the runtime Enum key
                    Enum stateEnumKey = entry.StateEnum; // Use the public property

                    if (stateEnumKey != null)
                    {
                         if (entry.StateSO != null) // Use the public property
                         {
                              // Add or overwrite existing state from base type
                              allStates[stateEnumKey] = entry.StateSO;
                         }
                         else
                         {
                              Debug.LogWarning($"NpcTypeDefinitionSO ({name}): Found null state SO for enum '{stateEnumKey.ToString()}' of type '{entry.StateEnumType}'. Skipping this entry.", this); // Use public property StateEnumType
                         }
                    }
                    else
                    {
                         // Warning for entries where StateEnum is null (parsing failed or empty strings)
                         Debug.LogWarning($"NpcTypeDefinitionSO ({name}): Found invalid state entry (Enum parsing failed or missing strings: Name='{entry.StateEnumName}', Type='{entry.StateEnumType}'). Skipping.", this); // Use public properties StateEnumName, StateEnumType
                    }
               }

               return allStates;
          }

          // Optional: Add a method to validate the enum entries after deserialization
          private void OnValidate()
          {
               // This runs in the editor whenever the SO changes
               foreach (var entry in stateCollection.entries)
               {
                    // Attempt to parse the enum string to see if it's valid
                    Enum parsedEnum = entry.StateEnum; // Access the property which handles parsing and logging errors internally
                    if (parsedEnum != null)
                    {
                         // Optionally check if the HandledState of the assigned SO matches the enum key
                         // This adds another layer of validation.
                         if (entry.StateSO != null && entry.StateSO.HandledState.GetType() == parsedEnum.GetType())
                         {
                              // Compare HandledState if they are of the same enum type
                              if (!entry.StateSO.HandledState.Equals(parsedEnum))
                              {
                                   Debug.LogWarning($"NpcTypeDefinitionSO ({name}): State entry enum '{entry.StateEnumName}' ({entry.StateEnumType}) does NOT match the HandledState '{entry.StateSO.HandledState}' defined in the assigned State SO '{entry.StateSO.name}'! This may cause unexpected behavior.", this);
                              }
                         }
                         else if (entry.StateSO != null)
                         {
                              // Log if the enum types don't match at all (e.g., assigned a GuardStateSO but specified CustomerState enum name)
                              Debug.LogWarning($"NpcTypeDefinitionSO ({name}): State entry enum type '{entry.StateEnumType}' does NOT match the HandledState enum type '{entry.StateSO.HandledState.GetType().Name}' in assigned State SO '{entry.StateSO.name}'!", this);
                         }
                    }
                    // else: Error was logged during parsing attempt in StateEnum property getter
               }
          }
         
         // Helper to parse the primary starting state Enum key
         public Enum ParsePrimaryStartingStateEnum()
         {
               if (string.IsNullOrEmpty(primaryStartingStateEnumKey) || string.IsNullOrEmpty(primaryStartingStateEnumType)) return null;

               try
               {
                   Type enumType = Type.GetType(primaryStartingStateEnumType);
                   if (enumType == null || !enumType.IsEnum)
                   {
                       Debug.LogError($"NpcTypeDefinitionSO ({name}): Primary Starting State Enum Type '{primaryStartingStateEnumType}' is invalid!", this);
                       return null;
                   }
                   return (Enum)Enum.Parse(enumType, primaryStartingStateEnumKey);
               }
               catch (Exception e)
               {
                   Debug.LogError($"NpcTypeDefinitionSO ({name}): Error parsing Primary Starting State config: {e}", this);
                   return null;
               }
         }
    }
}