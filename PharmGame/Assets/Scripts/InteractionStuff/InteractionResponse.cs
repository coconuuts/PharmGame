using UnityEngine;
using InventoryClass = Systems.Inventory.Inventory;
using Systems.Inventory; // Needed for ItemDetails
using System.Collections.Generic; // Needed for List
using System.Linq;
using Systems.Minigame;
using Systems.CraftingMinigames;

// Assuming this is within your Systems.Interaction namespace file (e.g., InteractionResponses.cs)
namespace Systems.Interaction
{
    public abstract class InteractionResponse
    {
        // Add an InteractionResult enum and base constructor if needed
        // public enum InteractionResult { None, OpenInventory, EnterComputer, ToggleLight, StartMinigame }
        // public InteractionResult ResultType { get; protected set; }
        // public InteractionResponse(InteractionResult resultType) { ResultType = resultType; }
        // If not using a base constructor, remove ": base(...)" from derived constructors
    }

    public class OpenInventoryResponse : InteractionResponse
    {
        public InventoryClass InventoryComponent { get; }
        public GameObject InventoryUIRoot { get; }

        public OpenInventoryResponse(InventoryClass inventoryComponent, GameObject inventoryUIRoot) // : base(InteractionResult.OpenInventory) // Add if using base constructor
        {
            InventoryComponent = inventoryComponent;
            InventoryUIRoot = inventoryUIRoot;
        }
    }

    /// <summary>
    /// Response indicating that the player is interacting with a computer.
    /// Contains data needed for camera movement (target transform) and UI activation.
    /// </summary>
    public class EnterComputerResponse : InteractionResponse
    {
        public Transform CameraTargetView { get; }
        public float CameraMoveDuration { get; }
        public GameObject ComputerUIRoot { get; }
        public ComputerInteractable ComputerInteractable { get; }

        public EnterComputerResponse(Transform cameraTargetView, float cameraMoveDuration, GameObject computerUIRoot, ComputerInteractable computerInteractable) // : base(InteractionResult.EnterComputer) // Add if using base constructor
        {
            CameraTargetView = cameraTargetView;
            CameraMoveDuration = cameraMoveDuration;
            ComputerUIRoot = computerUIRoot;
            ComputerInteractable = computerInteractable;
        }
    }

    public class ToggleLightResponse : InteractionResponse
    {
        public LightSwitch LightSwitch { get; }

        public ToggleLightResponse(LightSwitch lightSwitch) // : base(InteractionResult.ToggleLight) // Add if using base constructor
        {
            LightSwitch = lightSwitch;
        }
    }

    /// <summary>
    /// Response indicating that a general (non-crafting) minigame should be started.
    /// Contains data needed for camera movement, minigame setup, and a reference to the interactable.
    /// --- MODIFIED: Ensures camera properties are carried ---
    /// </summary>
    public class StartMinigameResponse : InteractionResponse
    {
        // --- MODIFIED: Renamed CameraTargetView to InitialCameraTarget for consistency ---
        public Transform InitialCameraTarget { get; }
        // --- MODIFIED: Renamed CameraMoveDuration to InitialCameraDuration for consistency ---
        public float InitialCameraDuration { get; }
        // Removed MinigameUIRoot - UI activation is handled by the minigame's config

        public int TargetClickCount { get; } // Still useful for the BarcodeMinigame setup data
        public CashRegisterInteractable CashRegisterInteractable { get; } // Reference to the initiating Interactable

        public MinigameType Type { get; } // The type of minigame to start

        public List<(ItemDetails details, int quantity)> ItemsToScan { get; } // Specific data for BarcodeScanning


        /// <summary>
        /// Constructor for StartMinigameResponse.
        /// --- MODIFIED: Parameter names updated for consistency ---
        /// </summary>
        /// <param name="minigameType">The type of minigame to start.</param>
        /// <param name="initialCameraTarget">The transform for the minigame camera view.</param>
        /// <param name="initialCameraDuration">The duration for the camera movement.</param>
        /// <param name="itemsToScan">The list of items the customer is purchasing (for BarcodeScanning).</param>
        /// <param name="cashRegisterInteractable">Reference to the initiating CashRegisterInteractable.</param>
        public StartMinigameResponse(MinigameType minigameType, Transform initialCameraTarget, float initialCameraDuration, List<(ItemDetails details, int quantity)> itemsToScan, CashRegisterInteractable cashRegisterInteractable)
        {
            Type = minigameType;
            InitialCameraTarget = initialCameraTarget; // Assign the camera target
            InitialCameraDuration = initialCameraDuration; // Assign the camera duration
            ItemsToScan = itemsToScan;
            TargetClickCount = ItemsToScan != null ? ItemsToScan.Sum(item => item.quantity) : 0;
            CashRegisterInteractable = cashRegisterInteractable;
        }
    }

    /// <summary>
    /// Response indicating that the player is interacting with a crafting station.
    /// Contains a reference to the CraftingStation component.
    /// </summary>
    public class OpenCraftingResponse : InteractionResponse
    {
        public CraftingStation CraftingStationComponent { get; }

        /// <summary>
        /// Constructor for OpenCraftingResponse.
        /// </summary>
        /// <param name="craftingStation">The CraftingStation component that was interacted with.</param>
        public OpenCraftingResponse(CraftingStation craftingStation) // : base(...) // Add if using a base constructor in InteractionResponse
        {
            CraftingStationComponent = craftingStation;
        }
    }
    
    /// <summary>
    /// Response indicating that a CRAFTING minigame should be started.
    /// Contains a reference to the instantiated minigame component.
    /// </summary>
    public class StartCraftingMinigameResponse : InteractionResponse
    {
        public CraftingMinigameBase MinigameComponent { get; }
        public CraftingRecipe Recipe { get; }
        public int Batches { get; }

        public Transform InitialCameraTarget { get; }
        public float InitialCameraDuration { get; }


        public StartCraftingMinigameResponse(CraftingMinigameBase minigameComponent, CraftingRecipe recipe, int batches, Transform initialCameraTarget, float initialCameraDuration)
        {
            MinigameComponent = minigameComponent;
            Recipe = recipe;
            Batches = batches;
            InitialCameraTarget = initialCameraTarget;
            InitialCameraDuration = initialCameraDuration;
        }
    }
}