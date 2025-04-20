using UnityEngine;

public class PlayerCam : MonoBehaviour
{
    public float mouseSens = 200f;
    public Transform playerBody;
    float xRotation;
    float yRotation;
    private bool canMoveCamera = true;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
    }


    // Update is called once per frame
    void Update()
    {
        if (canMoveCamera)
        {
            MoveCam();
        }
    }

    public void MoveCam()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSens * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSens * Time.deltaTime;

        xRotation -= mouseY;
        yRotation += mouseX;

        xRotation = Mathf.Clamp(xRotation, -75f, 60f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * (mouseX));
    }

    public void DisableCameraMovement()
    {
        canMoveCamera = false;
    }

    public void EnableCameraMovement()
    {
        canMoveCamera = true;
    }
}
