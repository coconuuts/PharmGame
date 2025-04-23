using UnityEngine;
using TMPro; // Required for accessing UI elements like Text

public class FPSDisplay : MonoBehaviour
{
    // Assign a UI Text element in the inspector to display the FPS
    public TextMeshProUGUI fpsText;

    // How often to update the FPS display (in seconds)
    public float updateInterval = 0.5f;

    private float timeSinceLastUpdate = 0.0f;
    private int frameCount = 0;
    private float fps = 0.0f;

    void Update()
    {
        // Increment the frame count
        frameCount++;

        // Accumulate the time since the last frame
        timeSinceLastUpdate += Time.deltaTime;

        // Check if the update interval has passed
        if (timeSinceLastUpdate >= updateInterval)
        {
            // Calculate the FPS: frames divided by the time taken
            fps = frameCount / timeSinceLastUpdate;

            // Update the UI Text element with the calculated FPS
            // Use string formatting to display the FPS with one decimal place
            if (fpsText != null)
            {
                fpsText.text = "FPS: " + fps.ToString("F1");
            }
            else
            {
                // Log a warning if the Text element is not assigned
                Debug.LogWarning("FPSDisplay: fpsText is not assigned!");
            }

            // Reset the counters for the next interval
            frameCount = 0;
            timeSinceLastUpdate = 0.0f;
        }
    }
}
