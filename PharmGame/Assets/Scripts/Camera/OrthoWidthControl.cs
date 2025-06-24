using UnityEngine;

[ExecuteInEditMode] // Allows it to work in the Scene view
public class OrthoWidthHeightControl : MonoBehaviour
{
    public Camera targetCamera;
    [Tooltip("The desired width of the camera's view frustum in world units.")]
    public float targetWidth = 10f; // Set your desired width here
    [Tooltip("The desired height of the camera's view frustum in world units.")]
    public float targetHeight = 10f; // Set your desired height here

    private Matrix4x4 originalProjectionMatrix;
    private bool hasOverriddenMatrix = false;

    void OnEnable()
    {
        if (targetCamera == null)
        {
            targetCamera = GetComponent<Camera>();
        }

        if (targetCamera != null)
        {
            if (!targetCamera.orthographic)
            {
                Debug.LogWarning("OrthoWidthHeightControl script requires an Orthographic Camera.", this);
                enabled = false;
                return;
            }
            // Store the original matrix if we haven't already
            // This handles cases where the script is enabled/disabled repeatedly
            if (!hasOverriddenMatrix)
            {
                 originalProjectionMatrix = targetCamera.projectionMatrix;
            }
        }
    }

    void OnDisable()
    {
        // Restore the original projection matrix when the script is disabled
        if (targetCamera != null && hasOverriddenMatrix)
        {
             targetCamera.projectionMatrix = originalProjectionMatrix;
             hasOverriddenMatrix = false;
        }
    }

    // OnPreCull is called before the camera culls the scene.
    // This is the standard place to override the projection matrix.
    void OnPreCull()
    {
        if (targetCamera != null && targetCamera.orthographic)
        {
            ApplyCustomProjection();
        }
    }

     // OnPostRender is called after the camera finishes rendering.
     // Restore the original matrix here to avoid affecting other rendering steps
     // or editor functionality that might happen after culling but before or after rendering.
    void OnPostRender()
    {
         if (targetCamera != null && hasOverriddenMatrix)
         {
             targetCamera.projectionMatrix = originalProjectionMatrix;
             hasOverriddenMatrix = false; // Reset flag
         }
    }

    // OnPreRender is called before the camera starts rendering.
    // Re-apply the custom matrix just in case something reset it between OnPreCull and rendering.
    // This can sometimes help depending on render pipeline intricacies.
    void OnPreRender()
    {
         if (targetCamera != null && targetCamera.orthographic)
         {
             ApplyCustomProjection();
         }
    }


    void ApplyCustomProjection()
    {
        // Ensure targetWidth and targetHeight are positive
        float adjustedTargetWidth = Mathf.Max(0.001f, targetWidth);
        float adjustedTargetHeight = Mathf.Max(0.001f, targetHeight);

        // Calculate the bounds for the custom matrix
        // Orthographic bounds are symmetric around the center of the view
        float right = adjustedTargetWidth / 2f;
        float left = -right;
        float top = adjustedTargetHeight / 2f;
        float bottom = -top;

        // Get current clipping planes
        float near = targetCamera.nearClipPlane;
        float far = targetCamera.farClipPlane;

        // Create the custom orthographic projection matrix
        // Matrix4x4.Ortho(left, right, bottom, top, near, far)
        Matrix4x4 customProjection = Matrix4x4.Ortho(left, right, bottom, top, near, far);

        // Store the original matrix BEFORE applying the custom one
        if (!hasOverriddenMatrix)
        {
            originalProjectionMatrix = targetCamera.projectionMatrix;
        }

        // Apply the custom matrix
        targetCamera.projectionMatrix = customProjection;
        hasOverriddenMatrix = true; // Set flag indicating we've overridden it

        // IMPORTANT NOTE: After this, camera.projectionMatrix is custom.
        // The camera.orthographicSize property in the Inspector/script
        // will NOT reflect the actual view height anymore.
        // It effectively becomes unused by the rendering pipeline once the matrix is overridden.
        // Similarly, camera.aspect will reflect the render target's aspect, not the aspect of targetWidth/targetHeight.
    }

    // Optional: Update in Update or LateUpdate in Edit Mode to see changes immediately
    #if UNITY_EDITOR
    void Update()
    {
         if (Application.isEditor && !Application.isPlaying)
         {
             if (targetCamera != null && targetCamera.orthographic)
             {
                 ApplyCustomProjection();
             }
         }
    }
    #endif
}