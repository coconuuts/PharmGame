using UnityEngine;
using TMPro;
using Systems.GameStates;
using Systems.Player;
using Systems.CameraControl;

public class DeveloperConsole : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("The parent GameObject containing the console UI.")]
    [SerializeField] private GameObject consolePanel;
    
    [Tooltip("The TMP_InputField where commands are typed.")]
    [SerializeField] private TMP_InputField inputField;
    
    [Tooltip("The TextMeshProUGUI element where previous commands and outputs are logged.")]
    [SerializeField] private TextMeshProUGUI historyText;

    private bool isConsoleOpen = false;

    private void Start()
    {
        if (consolePanel != null) consolePanel.SetActive(false);
        if (historyText != null) historyText.text = ""; // Clear history on start
        
        if (inputField != null)
        {
            inputField.onSubmit.AddListener(OnSubmitCommand);
        }
    }

    private void Update()
    {
        // Toggle with F4
        if (Input.GetKeyDown(KeyCode.F4))
        {
            ToggleConsole();
        }
    }

    private void OnDisable()
    {
        // Safety check to ensure we unregister if the object is destroyed/disabled
        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.OnProcessEscape -= HandleEscapeInput;
        }
    }

    private void ToggleConsole()
    {
        isConsoleOpen = !isConsoleOpen;
        consolePanel.SetActive(isConsoleOpen);

        PlayerMovement playerMovement = GetPlayerMovement();

        if (isConsoleOpen)
        {
            inputField.text = "";
            inputField.ActivateInputField();
            inputField.Select();
            
            if (playerMovement != null) playerMovement.SetMovementEnabled(false);

            if (CameraManager.Instance != null)
            {
                CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.Locked);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Intercept Escape key
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape += HandleEscapeInput;
            }
        }
        else
        {
            if (MenuManager.Instance != null && MenuManager.Instance.currentState == MenuManager.GameState.Playing)
            {
                if (playerMovement != null) playerMovement.SetMovementEnabled(true);

                if (CameraManager.Instance != null)
                {
                    CameraManager.Instance.SetCameraMode(CameraManager.CameraMode.MouseLook);
                }
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }

            // Release Escape key interception
            if (MenuManager.Instance != null)
            {
                MenuManager.Instance.OnProcessEscape -= HandleEscapeInput;
            }
        }
    }

    // This gets called by MenuManager when Escape is pressed
    private bool HandleEscapeInput()
    {
        // Returning true tells MenuManager the input was consumed. 
        // Because we don't put any other code here, pressing Escape simply does nothing.
        // (If you ever wanted Escape to close the console instead, you could just call ToggleConsole() here!)
        return true; 
    }

    private void OnSubmitCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command)) return;

        string cleanCommand = command.Trim().ToLower();
        LogToHistory("> " + command);

        if (cleanCommand == "debugmode")
        {
            PlayerMovement playerMovement = GetPlayerMovement();
            if (playerMovement != null)
            {
                playerMovement.isDebugModeActive = !playerMovement.isDebugModeActive; 
                LogToHistory($"debugmode: {playerMovement.isDebugModeActive.ToString().ToLower()}");
            }
            else
            {
                LogToHistory("Error: PlayerMovement not found.");
            }
        }
        else
        {
            LogToHistory($"Unknown command '{cleanCommand}'");
        }

        inputField.text = "";
        inputField.ActivateInputField();
        inputField.Select();
    }

    private void LogToHistory(string message)
    {
        if (historyText != null)
        {
            historyText.text += message + "\n";
        }
        Debug.Log($"Developer Console: {message}"); 
    }

    private PlayerMovement GetPlayerMovement()
    {
        if (MenuManager.Instance != null && MenuManager.Instance.player != null)
        {
            return MenuManager.Instance.player.GetComponent<PlayerMovement>();
        }
        return null;
    }
}