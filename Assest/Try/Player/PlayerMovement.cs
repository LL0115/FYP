using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerMovement : MonoBehaviour
{
    private Vector3 velocity;
    private Vector3 PlayerMovementInput;
    private Vector2 PlayerMouseInput;
    private float xRotation;

    [SerializeField] private CharacterController controller;
    [SerializeField] private Transform playerCamera;
    [SerializeField] private float speed;
    [SerializeField] private float jumpforce;
    [SerializeField] private float sensitivity;
    [SerializeField] private float gravity = -9.81f;

    // Update is called once per frame
    void Update()
    {
        PlayerMovementInput = new Vector3(Input.GetAxis("Horizontal"), 0, Input.GetAxis("Vertical"));
        PlayerMouseInput = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));

        MovePalyer();
        MoveCamera();
    }

    private void MovePalyer()
    {
        Vector3 movevector = transform.TransformDirection(PlayerMovementInput);
        if(Input.GetKey(KeyCode.Space))
        {
            velocity.y = jumpforce;
        }
        else if(Input.GetKey(KeyCode.LeftShift))
        {
            velocity.y = -jumpforce;
        }
        
        controller.Move(speed * Time.deltaTime * movevector);
        controller.Move(velocity * Time.deltaTime);
        velocity.y = 0f;
    }

    private void MoveCamera()
    {
        if (Input.GetMouseButton(1))
        {
            xRotation -= PlayerMouseInput.y * sensitivity;
            transform.Rotate(0f, PlayerMouseInput.x * sensitivity, 0f);
            playerCamera.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }
}
