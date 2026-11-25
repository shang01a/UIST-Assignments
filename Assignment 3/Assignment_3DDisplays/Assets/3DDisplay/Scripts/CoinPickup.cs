
using UnityEngine;

public class CoinPickup : MonoBehaviour
{
    // Shared progress for coins
    public static int CollectedCoins { get; private set; }
    public static int TotalCoins { get; private set; }
    private bool collected;
    
    // Reset progress when the scene starts
    public static void ResetProgress()
    {
        CollectedCoins = 0;
        TotalCoins = 0;
    }

    void Start()
    {
        TotalCoins++;
    }
    public Camera interactionCamera;
    public AudioClip pickupSound;

    [Header("Pickup Settings")]
    public float pickupDistance = 10f;
    public LayerMask pickupLayers = ~0;

    void Update()
    {
        if (!collected && Input.GetMouseButtonDown(0))
        {
            if (interactionCamera == null)
            {
                Debug.LogWarning(
                    "CoinPickup: InteractionCamera is missing!"
                );
                return;
            }

            // Use the interaction camera to get
            // the mouse ray direction
            Ray ray = interactionCamera.ScreenPointToRay(
                Input.mousePosition
            );

            // Visualize the ray in the Scene View
            Debug.DrawRay(
                ray.origin,
                ray.direction * pickupDistance,
                Color.green,
                1f
            );

            if (Physics.Raycast(
                ray,
                out RaycastHit hit,
                pickupDistance,
                pickupLayers,
                QueryTriggerInteraction.Collide
            ))
            {
                // Show which object was hit
                Debug.Log(
                    "Raycast hit: " + hit.collider.name +
                    " | Layer: " +
                    LayerMask.LayerToName(hit.collider.gameObject.layer) +
                    " | Distance: " + hit.distance +
                    " | Mask: " + pickupLayers.value
                );

                // Check if we clicked this coin or one of its children.
                if (hit.collider.transform == transform ||
                    hit.collider.transform.IsChildOf(transform))
                {
                    Debug.Log(
                        "Coin picked up: " + gameObject.name
                    );

                    if (pickupSound != null)
                    {
                        AudioSource.PlayClipAtPoint(
                            pickupSound,
                            transform.position
                        );
                    }

                    // Count this coin before removing it
                    collected = true;
                    CollectedCoins++;
                    Destroy(gameObject);
                }
            }
            else
            {
                Debug.Log(
                    "Raycast hit nothing" +
                    " | Mask: " + pickupLayers.value
                );
            }
        }
    }
}
