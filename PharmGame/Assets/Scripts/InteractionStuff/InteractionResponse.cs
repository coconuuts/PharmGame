using UnityEngine;
using InventoryClass = Systems.Inventory.Inventory;
using Systems.Inventory; // Needed for ItemDetails
using System.Collections.Generic; // Needed for List
using System.Linq;
using Systems.Minigame;

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
    /// Response indicating that a minigame should be started.
    /// Contains data needed for camera movement, UI activation, minigame setup, and a reference to the interactable.
    /// MODIFIED: Now includes a MinigameType to specify which minigame to start.
    /// </summary>
    public class StartMinigameResponse : InteractionResponse // Inherits from InteractionResponse
    {
        // Keep existing fields for camera, UI root, click count (derived), and register
        public Transform CameraTargetView { get; }
        public float CameraMoveDuration { get; }
        // Removed MinigameUIRoot - UI activation is handled by UIManager based on state and minigame component
        public int TargetClickCount { get; } // Still useful for the BarcodeMinigame setup data
        public CashRegisterInteractable CashRegisterInteractable { get; }

        // --- ADDED: Field to specify the type of minigame ---
        public MinigameType Type { get; }
        // --------------------------------------------------

        // Field to store the list of items to scan (specific to BarcodeScanning)
        public List<(ItemDetails details, int quantity)> ItemsToScan { get; }


        /// <summary>
        /// Constructor for StartMinigameResponse.
        /// MODIFIED: Added MinigameType parameter. Removed minigameUIRoot parameter.
        /// </summary>
        /// <param name="minigameType">The type of minigame to start.</param>
        /// <param name="cameraTargetView">The transform for the minigame camera view.</param>
        /// <param name="cameraMoveDuration">The duration for the camera movement.</param>
        /// <param name="itemsToScan">The list of items the customer is purchasing (for BarcodeScanning).</param>
        /// <param name="cashRegisterInteractable">Reference to the initiating CashRegisterInteractable.</param>
        // --- Modified Constructor: Accept MinigameType, Removed UI Root ---
        public StartMinigameResponse(MinigameType minigameType, Transform cameraTargetView, float cameraMoveDuration, List<(ItemDetails details, int quantity)> itemsToScan, CashRegisterInteractable cashRegisterInteractable) // : base(...) // Add if using a base constructor in InteractionResponse
        {
            Type = minigameType; // Assign the minigame type
            CameraTargetView = cameraTargetView;
            CameraMoveDuration = cameraMoveDuration;
            ItemsToScan = itemsToScan; // Assign the item list (used by BarcodeMinigame)
            // Calculate TargetClickCount from the assigned list for BarcodeMinigame setup data
            TargetClickCount = ItemsToScan != null ? ItemsToScan.Sum(item => item.quantity) : 0;
            CashRegisterInteractable = cashRegisterInteractable; // Assign the register instance
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
}