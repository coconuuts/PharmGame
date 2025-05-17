using UnityEngine;
using InventoryClass = Systems.Inventory.Inventory;
using Systems.Inventory; // Needed for ItemDetails
using System.Collections.Generic; // Needed for List
using System.Linq;

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
    /// </summary>
    public class StartMinigameResponse : InteractionResponse // Inherits from InteractionResponse
    {
        public Transform CameraTargetView { get; }
        public float CameraMoveDuration { get; }
        public GameObject MinigameUIRoot { get; }
        // Keep TargetClickCount property, but it will be calculated from the ItemsToScan list
        public int TargetClickCount { get; }
        public CashRegisterInteractable CashRegisterInteractable { get; } // Reference back to the register

        // --- ADDED: Field to store the list of items to scan ---
        public List<(ItemDetails details, int quantity)> ItemsToScan { get; }
        // -----------------------------------------------------

        /// <summary>
        /// Constructor for StartMinigameResponse.
        /// </summary>
        /// <param name="cameraTargetView">The transform for the minigame camera view.</param>
        /// <param name="cameraMoveDuration">The duration for the camera movement.</param>
        /// <param name="minigameUIRoot">The root GameObject for the minigame UI.</param>
        /// <param name="itemsToScan">The list of items the customer is purchasing.</param>
        /// <param name="cashRegisterInteractable">Reference to the initiating CashRegisterInteractable.</param>
        // --- Modified Constructor: Accept the item list instead of just the count ---
        public StartMinigameResponse(Transform cameraTargetView, float cameraMoveDuration, GameObject minigameUIRoot, List<(ItemDetails details, int quantity)> itemsToScan, CashRegisterInteractable cashRegisterInteractable) // : base(InteractionResult.StartMinigame) // Add if using base constructor
        {
            CameraTargetView = cameraTargetView;
            CameraMoveDuration = cameraMoveDuration;
            MinigameUIRoot = minigameUIRoot;
            ItemsToScan = itemsToScan; // Assign the item list
            // Calculate TargetClickCount from the assigned list for MinigameManager
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