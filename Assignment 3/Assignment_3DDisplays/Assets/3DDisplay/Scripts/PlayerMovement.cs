using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : MonoBehaviour
{
    public float moveSpeed = 2f;
    public float gravity = -9.81f;

    private CharacterController controller;
    private float verticalVelocity;

    void Start()
    {
        controller = GetComponent<CharacterController>();
    }

    void Update()
    {
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // WASD movement relative to the player's direction
        Vector3 movement = transform.right * horizontal
                         + transform.forward * vertical;

        movement.y = 0f;
        movement = Vector3.ClampMagnitude(movement, 1f);

        // Keep the controller grounded
        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }

        verticalVelocity += gravity * Time.deltaTime;

        movement *= moveSpeed;
        movement.y = verticalVelocity;

        controller.Move(movement * Time.deltaTime);
    }
}
