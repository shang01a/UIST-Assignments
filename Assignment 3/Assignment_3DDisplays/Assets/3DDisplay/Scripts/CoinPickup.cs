using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    public Camera leftEyeCamera;
    public Camera rightEyeCamera;
    public AudioClip pickupSound;

    void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (leftEyeCamera == null || rightEyeCamera == null)
                return;

            // Midpoint between the two eye cameras
            Vector3 midpoint = (
                leftEyeCamera.transform.position +
                rightEyeCamera.transform.position
            ) * 0.5f;

            // Use the left camera's projection to get
            // the mouse ray direction
            Ray mouseRay = leftEyeCamera.ScreenPointToRay(
                Input.mousePosition
            );

            // Shift the ray origin to the midpoint
            // This keeps our click detection between both eyes
            Ray ray = new Ray(midpoint, mouseRay.direction);

            if (Physics.Raycast(ray, out RaycastHit hit, 10f))
            {
                // Check if we clicked this coin or one of its children.
                if (hit.collider.transform == transform ||
                    hit.collider.transform.IsChildOf(transform))
                {
                    if (pickupSound != null)
                    {
                        AudioSource.PlayClipAtPoint(
                            pickupSound,
                            transform.position
                        );
                    }

                    Destroy(gameObject);
                }
            }
        }
    }
}
