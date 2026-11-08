
using UnityEngine;

public class CircleDetection : MonoBehaviour
{
    private WebCamTexture webcamTexture;
    public GameObject redSphere;

    private Color32[] pixels;

    [Header("Red Circle Detection")]
    [Range(0, 255)]
    public int redThreshold = 120;

    [Range(1f, 3f)]
    public float redDominance = 1.5f;

    public int minimumRedPixels = 50;

    // Depth estimation settings
    [Header("Depth Estimation")]
    public float referenceRedPixels = 2500f;

    // Sphere movement settings
    [Header("Sphere Movement")]
    public float movementScale = 3f;
    public float depthScale = 2f;
    public bool mirrorX = false;

    private Vector3 initialSpherePosition;

    // Smoothing settings
    public float smoothingTime = 0.12f;
    private Vector3 smoothingVelocity = Vector3.zero;

    // Detection results
    public bool HasTarget { get; private set; }
    public float CenterXPixel { get; private set; }
    public float CenterYPixel { get; private set; }
    public int RedPixelCount { get; private set; }
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }

    public float NormalizedX { get; private set; }
    public float NormalizedY { get; private set; }

    // Estimated relative depth
    public float EstimatedZ { get; private set; }

    void Start()
    {
        webcamTexture = new WebCamTexture();

        GetComponent<Renderer>().material.mainTexture =
            webcamTexture;

        webcamTexture.Play();

        // Remember the sphere's starting position
        if (redSphere != null)
        {
            initialSpherePosition = redSphere.transform.position;
        }
    }

    void Update()
    {
        if (webcamTexture.didUpdateThisFrame)
        {
            int width = webcamTexture.width;
            int height = webcamTexture.height;

            if (width <= 16 || height <= 16)
                return;

            FrameWidth = width;
            FrameHeight = height;

            if (pixels == null ||
                pixels.Length != width * height)
            {
                pixels = new Color32[width * height];
            }

            webcamTexture.GetPixels32(pixels);

            DetectRedCircle();
        }
        // Update sphere movement every frame
        MoveSphere();
    }

    void DetectRedCircle()
    {
        int width = FrameWidth;
        int height = FrameHeight;

        int redCount = 0;
        long sumX = 0;
        long sumY = 0;

        // Detect red pixels
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                int index = y * width + x;
                Color32 pixel = pixels[index];

                bool isRed =
                    pixel.r >= redThreshold &&
                    pixel.r > pixel.g * redDominance &&
                    pixel.r > pixel.b * redDominance;

                if (isRed)
                {
                    redCount++;
                    sumX += x;
                    sumY += y;
                }
            }
        }

        RedPixelCount = redCount;

        HasTarget = redCount >= minimumRedPixels;

        if (HasTarget)
        {
            // Calculate centroid
            CenterXPixel = (float)sumX / redCount;
            CenterYPixel = (float)sumY / redCount;

            // Normalize X and Y coordinates
            NormalizedX =
                (CenterXPixel / FrameWidth) * 2f - 1f;

            NormalizedY =
                (CenterYPixel / FrameHeight) * 2f - 1f;

            // Estimate Z using apparent target size
            EstimatedZ = Mathf.Sqrt(
                referenceRedPixels / RedPixelCount
            );

        }
        else
        {
            CenterXPixel = 0f;
            CenterYPixel = 0f;

            NormalizedX = 0f;
            NormalizedY = 0f;

            // Keep last sphere position when target is lost
        }

        // Print detection results
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log(
                "Detected: " + HasTarget +
                " | X: " + NormalizedX +
                " | Y: " + NormalizedY +
                " | Z: " + EstimatedZ +
                " | Red Pixels: " + RedPixelCount
            );
        }
    }

    // Map tracking coordinates to Unity space
    void MoveSphere()
    {
        if (redSphere == null || !HasTarget)
            return;

        float x = NormalizedX * movementScale;
        float y = NormalizedY * movementScale;

        if (mirrorX)
        {
            x = -x;
        }

        // Z increases as the marker moves further away
        float z = (EstimatedZ - 1f) * depthScale;

        Vector3 targetPosition = new Vector3(
            initialSpherePosition.x + x,
            initialSpherePosition.y + y,
            initialSpherePosition.z + z
        );

        // Smooth the sphere movement
        redSphere.transform.position = Vector3.SmoothDamp(
            redSphere.transform.position,
            targetPosition,
            ref smoothingVelocity,
            smoothingTime);
    }

    // Display tracking coordinates and red pixel count
    private void OnGUI()
    {
        GUI.Box(
            new Rect(10, Screen.height - 110, 220, 100),
            "X: " + NormalizedX.ToString("F2") + "\n" +
            "Y: " + NormalizedY.ToString("F2") + "\n" +
            "Z: " + EstimatedZ.ToString("F2") + "\n" +
            "Red pixels: " + RedPixelCount
            );
    }

    void OnDestroy()
    {
        if (webcamTexture != null &&
            webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}
