using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using Cursor = UnityEngine.Cursor;
using Photon.Pun;

public class PlayerMov : MonoBehaviourPun
{
    public CharacterController controller;
    public float speed = 12f;
    public float jumpHeight = 3f;
    public float gravity = -40f;
    private Vector3 velocity;
    private bool isGrounded;
    private bool isCursorVisible = false;
    private bool wasAltPressed = false;

    void Start()
    {
        if (photonView.IsMine)
        {
            SetCursorState(false);
        }
    }

    void Update()
    {

        if (!photonView.IsMine)
        {
            return;
        }
        if (Input.GetKeyDown(KeyCode.LeftAlt))
        {
            SetCursorState(true);
            wasAltPressed = true;
        }
        else if (Input.GetKeyUp(KeyCode.LeftAlt) && !IsShopOpen())
        {
            SetCursorState(false);
            wasAltPressed = false;
        }

        // Only process movement when cursor is locked
        if (!isCursorVisible)
        {
            ProcessMovement();
        }
    }

    public bool IsCursorVisible()
    {
        return isCursorVisible;
    }

    public void SetCursorState(bool visible)
    {
        if (!photonView.IsMine)
        {
            return;
        }
        isCursorVisible = visible;
        Cursor.visible = visible;
        Cursor.lockState = visible ? CursorLockMode.None : CursorLockMode.Locked;
        Debug.Log($"Cursor State Changed - Visible: {visible}, LockState: {Cursor.lockState}");
    }

    private bool IsShopOpen()
    {
        GameUIEvent gameUI = FindObjectOfType<GameUIEvent>();
        if (gameUI != null)
        {
            return gameUI._shopUI != null && gameUI._shopUI.style.display == DisplayStyle.Flex;
        }
        return false;
    }

    private void ProcessMovement()
    {
        isGrounded = controller.isGrounded;

        if (isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }

        float x = Input.GetAxis("Horizontal");
        float z = Input.GetAxis("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        // Speed Up
        if (Input.GetKey(KeyCode.LeftShift))
        {
            controller.Move(move * speed * 1.5f * Time.deltaTime);
        }
        else
        {
            controller.Move(move * speed * Time.deltaTime);
        }
        // Check if the player is grounded before allowing jump
        if (isGrounded && Input.GetButtonDown("Jump"))
        {
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }

        velocity.y += gravity * Time.deltaTime;
        controller.Move(velocity * Time.deltaTime);
    }
}