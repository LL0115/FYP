using UnityEngine;
using Photon.Pun;

public class PlayerCam : MonoBehaviour 
{
    public float mouseSpeed = 100.0f;
    public Transform playerBody;
    float xRotation = 0f;

    private Camera playerCamera;
    private AudioListener audioListener;
    private PhotonView parentPhotonView; 

    void Start()
    {
        playerCamera = GetComponent<Camera>();
        audioListener = GetComponent<AudioListener>();

        // Set the MainCamera tag
        if (playerCamera != null)
        {
            gameObject.tag = "MainCamera";
        }

        // Get the PhotonView component from the player
        parentPhotonView = GetComponentInParent<PhotonView>();

        if (parentPhotonView == null)
        {
            Debug.LogError("No PhotonView found on parent object!");
            return;
        }

       
        if (!parentPhotonView.IsMine)
        {
            // disable for non local player
            if (playerCamera != null) playerCamera.enabled = false;
            if (audioListener != null) audioListener.enabled = false;
            Debug.Log($"Disabled camera for remote player ID: {parentPhotonView.ViewID}");
            return;
        }

        // This is our local player's camera
        Debug.Log("Enabling camera for local player");
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // Early return if parentPhotonView is null or not local
        if (parentPhotonView == null || !parentPhotonView.IsMine)
        {
            return;
        }

        // Only process camera movement if cursor is locked
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            float mouseX = Input.GetAxis("Mouse X") * mouseSpeed * Time.deltaTime;
            float mouseY = Input.GetAxis("Mouse Y") * mouseSpeed * Time.deltaTime;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, -60f, 60f);

            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
            playerBody.Rotate(Vector3.up * mouseX);
        }
    }
}