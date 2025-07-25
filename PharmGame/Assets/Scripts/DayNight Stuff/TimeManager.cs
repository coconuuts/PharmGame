// --- START OF FILE TimeManager.cs ---

using System;
using UnityEngine;
using TMPro;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal; // Assuming URP for Volume

public class TimeManager : MonoBehaviour {
    public static TimeManager Instance { get; private set; }

    [SerializeField] TextMeshProUGUI timeText;

    [SerializeField] Light sun;
    [SerializeField] Light moon; // Ensure you have a Light component assigned here for the moon
    [SerializeField] AnimationCurve lightIntensityCurve;
    [SerializeField] float maxSunIntensity = 1;
    [SerializeField] float maxMoonIntensity = 0.5f;

    [SerializeField] Color dayAmbientLight;
    [SerializeField] Color nightAmbientLight;
    [SerializeField] Volume volume; // Ensure you have a Volume component assigned
    [SerializeField] Material skyboxMaterial; // Ensure you have the Skybox Material assigned (using the HDRI shader)

    ColorAdjustments colorAdjustments; // Used for ambient color tint via Volume

    [SerializeField] TimeSettings timeSettings; // Link your TimeSettings ScriptableObject here

    // --- Day Tracking ---
    [SerializeField]
    private int currentDay = 1; // Start at Day 1
    public int CurrentDay => currentDay; // Public getter for the current day

    // Event for when the day increments
    public event Action<int> OnDayChanged;
    // -------------------


    // Events are delegated to the TimeService instance
    public event Action OnSunrise {
        add => service.OnSunrise += value;
        remove => service.OnSunrise -= value;
    }

    public event Action OnSunset {
        add => service.OnSunset += value;
        remove => service.OnSunset -= value;
    }

    public event Action OnHourChange {
        add => service.OnHourChange += value;
        remove => service.OnHourChange -= value;
    }

    TimeService service; // <-- Keep as field

    public DateTime CurrentGameTime => service?.CurrentTime ?? DateTime.MinValue;

    void Awake()
    {
        // Implement the singleton pattern
        if (Instance == null)
        {
            Instance = this;
            // Optional: If you want this manager to persist across scenes
            // DontDestroyOnLoad(gameObject);
        }
        else
        {
            // If an instance already exists, destroy this new one
            Debug.LogWarning("Multiple TimeManager instances found. Destroying duplicate.");
            Destroy(gameObject);
            return; // Stop execution for this object
        }

        // Ensure TimeSettings is assigned
        if (timeSettings == null) {
            Debug.LogError("TimeSettings ScriptableObject is not assigned to the TimeManager!");
            enabled = false; // Disable the script if essential settings are missing
            return;
        }

        // Initialize TimeService in Awake
        service = new TimeService(timeSettings); // <-- MOVED INITIALIZATION TO AWAKE

        Debug.Log("TimeManager: Awake completed. TimeService initialized."); // Added log
    }

    void Start() {
        // Try to get ColorAdjustments from the Volume profile
        if (volume != null && volume.profile != null) {
            volume.profile.TryGet(out colorAdjustments);
        } else {
             Debug.LogWarning("Volume or Volume Profile not assigned/found on TimeManager. Ambient color tinting will not work.");
        }

        // --- Subscribe to OnSunset for Day Tracking ---
        service.OnSunset += HandleSunset;
        // ---------------------------------------------


        // Perform initial update to set lights/sky/UI based on starting time
        UpdateTimeOfDay(); // This will also trigger events if the initial time is at a boundary
        UpdateLightRotation(); // Use the new method name
        UpdateLightSettings();
        UpdateSkyBlend();

        Debug.Log("TimeManager: Start completed."); // Added log
    }

    void Update() {
        // Advance game time
        service.UpdateTime(Time.deltaTime); // Update time first
        // Update visual elements based on new time
        UpdateLightRotation(); // Rotates both sun and moon
        UpdateLightSettings(); // Adjusts intensity and ambient light
        UpdateSkyBlend(); // Adjusts skybox blend

        // Update UI last
        if (timeText != null) {
            // Format the time as hh:mm tt (12-hour with AM/PM)
            // Using "HH:mm" for 24-hour format as in the original code
            timeText.text = service.CurrentTime.ToString("h:mm tt") + $" - Day {currentDay}"; // Added Day display
        }
    }

    // --- Handler for the OnSunset event ---
    private void HandleSunset()
    {
        currentDay++; // Increment the day count
        Debug.Log($"Day {currentDay} has begun!"); // Log the new day
        OnDayChanged?.Invoke(currentDay); // Invoke the day changed event
    }
    // -------------------------------------


    // Updates the blend factor for the skybox material
    void UpdateSkyBlend() {
        if (skyboxMaterial == null) return;

        float dotProduct = Vector3.Dot(sun.transform.forward, Vector3.up);
        float blend = Mathf.Lerp(0, 1, lightIntensityCurve.Evaluate(dotProduct));

        skyboxMaterial.SetFloat("_Blend", blend);
    }

    // Adjusts sun/moon intensity and ambient light color
    void UpdateLightSettings() {
        if (sun == null || moon == null) return;

        // Dot product between sun's forward vector and Vector3.down
        // 1 when sun is directly overhead (pointing down), -1 when sun is directly below (pointing up)
        // 0 when sun is horizontal (sunrise/sunset)
        float dotProduct = Vector3.Dot(sun.transform.forward, Vector3.down);

        // Evaluate the curve based on this dot product
        // Assuming the curve is set up such that higher dotProduct (sun pointing down) corresponds to high intensity (day)
        float lightIntensityFactor = lightIntensityCurve.Evaluate(dotProduct);

        // Sun intensity is high when lightIntensityFactor is high (midday)
        sun.intensity = Mathf.Lerp(0, maxSunIntensity, lightIntensityFactor);

        // Moon intensity is high when lightIntensityFactor is low (midnight)
        // We use 1 - lightIntensityFactor or Lerp from maxMoonIntensity to 0
        moon.intensity = Mathf.Lerp(maxMoonIntensity, 0, lightIntensityFactor);

        // Ambient light color lerps between night and day based on the same factor
        if (colorAdjustments != null) {
             colorAdjustments.colorFilter.value = Color.Lerp(nightAmbientLight, dayAmbientLight, lightIntensityFactor);
        }
    }

    // Calculates sun angle and applies rotation to both sun and moon
    void UpdateLightRotation() { // Renamed from RotateSun
        // Get the sun's rotation angle from the service
        float sunRotation = service.CalculateSunAngle();

        // Apply the rotation to the sun around the X-axis (Vector3.right)
        sun.transform.rotation = Quaternion.AngleAxis(sunRotation, Vector3.right);

        // Calculate the moon's rotation angle (180 degrees opposite the sun)
        float moonRotation = sunRotation + 180f;

        // Apply the rotation to the moon around the same axis
        moon.transform.rotation = Quaternion.AngleAxis(moonRotation, Vector3.right);
    }

    // Advances the time simulation and updates the time display (UI update moved to Update)
    void UpdateTimeOfDay() {
        // service.UpdateTime(Time.deltaTime); // Time update is now in the main Update loop
        // UI update moved to Update
    }

    // Optional: Clean up the TimeService and unsubscribe when the TimeManager is destroyed
    void OnDestroy() {
        // When the singleton instance is destroyed, clear the static reference
        if (Instance == this)
        {
            Instance = null;
        }

        // --- Unsubscribe from OnSunset ---
        if (service != null)
        {
             service.OnSunset -= HandleSunset;
        }
        // --------------------------------


        // service.Dispose(); // If TimeService had a Dispose method for event cleanup
    }
}
// --- END OF FILE TimeManager.cs ---