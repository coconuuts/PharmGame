// File: Assets/Editor/SOTools/SOGeneratorSettings.cs
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;

namespace Systems.Inventory.EditorTools
{
    /// <summary>
    /// Stores the settings for the SO Generator Editor Window,
    /// specifically the AssemblyQualifiedName of the currently selected ScriptableObject type
    /// and an optional template ScriptableObject to copy values from.
    /// </summary>
    public class SOGeneratorSettings : ScriptableObject
    {
        [SerializeField]
        private string _selectedSOTypeName; // Stores AssemblyQualifiedName

        // Editor-only property to get/set the actual Type
        private Type _selectedSOTypeCache; // Cache the Type object
        public Type SelectedSOType
        {
            get
            {
                // If cache is null, and we have a name, try to load it
                if (_selectedSOTypeCache == null && !string.IsNullOrEmpty(_selectedSOTypeName))
                {
                    _selectedSOTypeCache = Type.GetType(_selectedSOTypeName);
                    if (_selectedSOTypeCache == null)
                    {
                        Debug.LogWarning($"SO Generator: Could not load type from name: '{_selectedSOTypeName}'. Clearing selection.");
                        _selectedSOTypeName = null; // Clear if type can't be found (e.g., assembly removed)
                    }
                }
                return _selectedSOTypeCache;
            }
            set
            {
                // Only update if the type has actually changed
                if (_selectedSOTypeCache != value)
                {
                    _selectedSOTypeCache = value;
                    _selectedSOTypeName = value?.AssemblyQualifiedName; // Store the full name
                    EditorUtility.SetDirty(this); // Mark settings dirty for saving
                }
            }
        }

        // --- NEW FIELD FOR PRESET TEMPLATE ---
        [SerializeField]
        private ScriptableObject _templateSO; // Reference to the template SO asset

        public ScriptableObject TemplateSO
        {
            get => _templateSO;
            set
            {
                if (_templateSO != value)
                {
                    _templateSO = value;
                    EditorUtility.SetDirty(this);
                }
            }
        }
        // --- END NEW FIELD ---


        // Singleton-like access pattern for Editor-only SO
        private static SOGeneratorSettings _instance;
        public static SOGeneratorSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Try to load existing settings asset
                    string[] guids = AssetDatabase.FindAssets($"t:{nameof(SOGeneratorSettings)}");
                    if (guids.Length > 0)
                    {
                        string path = AssetDatabase.GUIDToAssetPath(guids[0]);
                        _instance = AssetDatabase.LoadAssetAtPath<SOGeneratorSettings>(path);
                    }

                    // Create new settings if none found
                    if (_instance == null)
                    {
                        _instance = ScriptableObject.CreateInstance<SOGeneratorSettings>();
                        // Store the settings in an Editor folder
                        string editorPath = "Assets/Editor/SOTools/";
                        if (!AssetDatabase.IsValidFolder(editorPath))
                        {
                            AssetDatabase.CreateFolder("Assets/Editor", "SOTools");
                        }
                        AssetDatabase.CreateAsset(_instance, $"{editorPath}{nameof(SOGeneratorSettings)}.asset");
                        AssetDatabase.SaveAssets();
                        Debug.Log($"SO Generator: Created new {nameof(SOGeneratorSettings)} asset at {editorPath}{nameof(SOGeneratorSettings)}.asset.");
                    }
                }
                return _instance;
            }
        }
    }
}