// File: Assets/Editor/SOTools/SOGeneratorWindow.cs
using UnityEngine;
using UnityEditor;
using System;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

namespace Systems.Inventory.EditorTools
{
    /// <summary>
    /// Unity Editor Window for generating ScriptableObject assets.
    /// Allows selecting a base SO type and creating instances of it,
    /// with an option to apply a preset from an existing SO template.
    /// </summary>
    public class SOGeneratorWindow : EditorWindow
    {
        private SOGeneratorSettings _settings;

        [MenuItem("Tools/Inventory/SO Generator")]
        public static void ShowWindow()
        {
            GetWindow<SOGeneratorWindow>("SO Generator").minSize = new Vector2(300, 250); // Increased minSize
        }

        private void OnEnable()
        {
            _settings = SOGeneratorSettings.Instance;
        }

        private void OnGUI()
        {
            GUILayout.Label("ScriptableObject Generator", EditorStyles.boldLabel);

            EditorGUILayout.Space();

            // --- Display Selected Type ---
            string selectedTypeName = _settings.SelectedSOType != null ? _settings.SelectedSOType.Name : "None Selected";
            EditorGUILayout.LabelField("Currently Selected SO Type:", selectedTypeName, EditorStyles.wordWrappedLabel);

            // --- Button to Select Type ---
            if (GUILayout.Button("Change Selected SO Type"))
            {
                ShowTypeSelectionPopup();
            }

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // --- PRESET CONFIGURATION ---
            GUILayout.Label("Preset Configuration", EditorStyles.boldLabel);

            GUI.enabled = _settings.SelectedSOType != null; // Only enable preset options if a type is selected

            // Object field for the template SO
            EditorGUI.BeginChangeCheck();
            ScriptableObject newTemplateSO = EditorGUILayout.ObjectField(
                "Preset Template SO",
                _settings.TemplateSO,
                typeof(ScriptableObject), // Allow any ScriptableObject here, validation happens below
                false
            ) as ScriptableObject;

            if (EditorGUI.EndChangeCheck())
            {
                _settings.TemplateSO = newTemplateSO;
            }

            // Validate the selected template SO
            if (_settings.TemplateSO != null)
            {
                if (_settings.SelectedSOType == null)
                {
                    EditorGUILayout.HelpBox("Select an SO type first to use this template.", MessageType.Warning);
                    if (GUILayout.Button("Clear Template"))
                    {
                        _settings.TemplateSO = null;
                    }
                }
                else if (_settings.TemplateSO.GetType() != _settings.SelectedSOType)
                {
                    EditorGUILayout.HelpBox($"Template type ({_settings.TemplateSO.GetType().Name}) does not match selected SO type ({_settings.SelectedSOType.Name}). Clear or select a matching template.", MessageType.Warning);
                    if (GUILayout.Button("Clear Template"))
                    {
                        _settings.TemplateSO = null;
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox($"Using '{_settings.TemplateSO.name}' as a preset for new '{_settings.SelectedSOType.Name}' assets.", MessageType.Info);
                    if (GUILayout.Button("Clear Template"))
                    {
                        _settings.TemplateSO = null;
                    }
                }
            }
            else if (_settings.SelectedSOType != null)
            {
                EditorGUILayout.HelpBox($"Drag an existing {_settings.SelectedSOType.Name} asset here to use it as a preset.", MessageType.Info);
            }

            GUI.enabled = true; // Reset GUI enabled state

            EditorGUILayout.Space();
            EditorGUILayout.Space();

            // --- Create Instance Button ---
            GUI.enabled = _settings.SelectedSOType != null; // Enable only if a type is selected
            string buttonText = _settings.SelectedSOType != null
                ? $"Create New {_settings.SelectedSOType.Name} Asset{( _settings.TemplateSO != null && _settings.TemplateSO.GetType() == _settings.SelectedSOType ? " (from preset)" : "")}"
                : "Select a Type to Create";

            if (GUILayout.Button(buttonText))
            {
                CreateSOAsset(_settings.SelectedSOType);
            }
            GUI.enabled = true; // Reset GUI enabled state
        }

        private void ShowTypeSelectionPopup()
        {
            var soTypes = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(s => s.GetTypes())
                .Where(p => typeof(ScriptableObject).IsAssignableFrom(p)
                         && !p.IsAbstract
                         && !p.IsGenericTypeDefinition
                         && p.IsPublic)
                .OrderBy(p => p.FullName)
                .ToList();

            GenericMenu menu = new GenericMenu();

            if (!soTypes.Any())
            {
                menu.AddItem(new GUIContent("No ScriptableObject types found."), false, null);
            }
            else
            {
                foreach (Type type in soTypes)
                {
                    bool isSelected = _settings.SelectedSOType == type;
                    menu.AddItem(new GUIContent(type.FullName), isSelected, OnTypeSelected, type);
                }
            }

            menu.ShowAsContext();
        }

        private void OnTypeSelected(object userData)
        {
            Type selectedType = userData as Type;
            if (selectedType != null)
            {
                _settings.SelectedSOType = selectedType;
                // If the currently assigned template SO is not of the newly selected type, clear it.
                if (_settings.TemplateSO != null && _settings.TemplateSO.GetType() != selectedType)
                {
                    Debug.Log($"SO Generator: Cleared template because its type ({_settings.TemplateSO.GetType().Name}) no longer matches the new selected type ({selectedType.Name}).");
                    _settings.TemplateSO = null;
                }
                Repaint();
            }
        }

        /// <summary>
        /// Creates a new instance of the specified ScriptableObject type and saves it as an asset.
        /// If a valid template is set, it copies the values from the template.
        /// </summary>
        private void CreateSOAsset(Type type)
        {
            if (type == null)
            {
                EditorUtility.DisplayDialog("Error", "No ScriptableObject type selected.", "OK");
                return;
            }

            string path = "Assets/";
            if (Selection.activeObject != null)
            {
                string assetPath = AssetDatabase.GetAssetPath(Selection.activeObject);
                if (!string.IsNullOrEmpty(assetPath))
                {
                    if (AssetDatabase.IsValidFolder(assetPath))
                    {
                        path = assetPath;
                    }
                    else
                    {
                        path = System.IO.Path.GetDirectoryName(assetPath);
                    }
                    if (!path.EndsWith("/"))
                    {
                        path += "/";
                    }
                }
            }

            string defaultFileName = $"New {type.Name}.asset";
            string fullPath = EditorUtility.SaveFilePanelInProject("Save ScriptableObject", defaultFileName, "asset", "Please enter a file name for the new ScriptableObject.", path);

            if (string.IsNullOrEmpty(fullPath))
            {
                return;
            }

            ScriptableObject asset = ScriptableObject.CreateInstance(type);

            // --- APPLY PRESET LOGIC ---
            if (_settings.TemplateSO != null && _settings.TemplateSO.GetType() == type)
            {
                // Copy all serialized fields from the template to the new asset
                EditorUtility.CopySerialized(_settings.TemplateSO, asset);
                Debug.Log($"SO Generator: Applied preset from '{_settings.TemplateSO.name}' to new '{type.Name}' asset.");

                // SPECIAL HANDLING FOR ItemDetails: Generate a new unique ID
                // This is crucial because CopySerialized would copy the template's ID
                if (asset is ItemDetails itemDetailsAsset)
                {
                    itemDetailsAsset.Id = SerializableGuid.NewGuid();
                    // Mark dirty to ensure the new GUID is saved
                    EditorUtility.SetDirty(itemDetailsAsset);
                    Debug.Log($"SO Generator: Generated new unique ID for ItemDetails asset: {itemDetailsAsset.Id}");
                }
            }
            // --- END APPLY PRESET LOGIC ---


            AssetDatabase.CreateAsset(asset, fullPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            EditorUtility.FocusProjectWindow();
            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            Debug.Log($"SO Generator: Created new {type.Name} asset at: {fullPath}");
        }
    }
}