using UnityEngine;
using InventoryClass = Systems.Inventory.Inventory;

namespace Systems.Interaction
{
    public abstract class InteractionResponse {}

    public class OpenInventoryResponse : InteractionResponse
    {
        public InventoryClass InventoryComponent { get; }
        public GameObject InventoryUIRoot { get; }

        public OpenInventoryResponse(InventoryClass inventoryComponent, GameObject inventoryUIRoot)
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
        // REMOVED: Camera target position, rotation, duration fields
        // public Vector3 CameraTargetPosition { get; }
        // public Quaternion CameraTargetRotation { get; }
        // public float CameraMoveDuration { get; }

        public Transform CameraTargetView { get; } // ADDED field to hold the target Transform
        public GameObject ComputerUIRoot { get; }
        public ComputerInteractable ComputerInteractable { get; }

        // MODIFIED constructor to accept Transform and duration
        public EnterComputerResponse(Transform cameraTargetView, float cameraMoveDuration, GameObject computerUIRoot, ComputerInteractable computerInteractable) // MODIFIED PARAMETERS
        {
            CameraTargetView = cameraTargetView; // Assign the Transform
            // CameraMoveDuration = cameraMoveDuration; // We can use duration from the Transform's settings or a default in CameraManager

            // Let's use the duration directly in the response, as different computer views might have different animation times.
             CameraMoveDuration = cameraMoveDuration; // Keep duration in the response


            ComputerUIRoot = computerUIRoot;
            ComputerInteractable = computerInteractable;
        }
         // Add CameraMoveDuration property back if needed
        public float CameraMoveDuration { get; } // ADDED Property
    }

    public class ToggleLightResponse : InteractionResponse
    {
        public LightSwitch LightSwitch { get; }

        public ToggleLightResponse(LightSwitch lightSwitch)
        {
            LightSwitch = lightSwitch;
        }
    }

    /// <summary>
    /// Response indicating that a minigame should be started.
    /// Contains data needed for camera movement, UI activation, minigame setup, and a reference to the interactable.
    /// </summary>
    public class StartMinigameResponse : InteractionResponse
    {
        public Transform CameraTargetView { get; }
        public float CameraMoveDuration { get; }
        public GameObject MinigameUIRoot { get; }
        public int TargetClickCount { get; }
        public CashRegisterInteractable CashRegisterInteractable { get; } // ADD THIS FIELD

        // MODIFIED constructor to accept CashRegisterInteractable instance
        public StartMinigameResponse(Transform cameraTargetView, float cameraMoveDuration, GameObject minigameUIRoot, int targetClickCount, CashRegisterInteractable cashRegisterInteractable) // ADD cashRegisterInteractable PARAM
        {
            CameraTargetView = cameraTargetView;
            CameraMoveDuration = cameraMoveDuration;
            MinigameUIRoot = minigameUIRoot;
            TargetClickCount = targetClickCount;
            CashRegisterInteractable = cashRegisterInteractable; // Assign the instance
        }
    }
}