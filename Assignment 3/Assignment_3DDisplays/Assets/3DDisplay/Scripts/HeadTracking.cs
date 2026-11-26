using UnityEngine;

public class HeadTracking : MonoBehaviour
{
    private WebCamTexture webcamTexture;
    private Color32[] pixels;

    [Header("Camera Rig")]
    [SerializeField] private Transform stereoCameraRig;

    [Header("Red Lens Detection")]
    [Range(1f, 60f)]
    public float hueTolerance = 18f;

    [Range(0f, 1f)]
    public float minSaturation = 0.45f;

    [Range(0f, 1f)]
    public float minValue = 0.25f;

    // Minimum red area for valid tracking
    public int minimumRedPixels = 150;
    // Skip pixels for faster detection
    public int pixelStep = 2;
    // Region of interest around last detected position
    public bool useRoi = true;

    // Enable diagnostic 
    [Header("Detection Preview")]
    public bool showDetectionPreview = false;

    private Texture2D detectionMask;
    private Color32[] maskPixels;

    [Header("Movement")]
    public float horizontalScale = 4f;
    public float verticalScale = 2f;
    public float depthScale = 1.5f;
    public bool mirrorX = false;

    [Header("Depth Smoothing")]
    [Range(0.01f, 1f)]
    public float depthSmoothing = 0.15f;

    private float smoothedDepth;

    [Header("Smoothing")]
    public float smoothingTime = 0.12f;

    // Limited head movement only relevant for the game scene
    [Header("Movement Limits")]
    public bool limitMovement = false;
    public float maxHorizontalOffset = 0.2f;
    public float maxVerticalOffset = 0.15f;
    public float maxDepthOffset = 0.2f;

    private Vector3 initialRigPosition;
    private Vector3 targetPosition;
    private Vector3 smoothingVelocity;

    // Reference position and red area for calibration
    private Vector2 referenceCenter;
    private float referenceRedPixels;
    private bool calibrated;

    private float lastCenterX;
    private float lastCenterY;
    private int lastRedCount;
    private bool hasTarget;

    private int frameWidth;
    private int frameHeight;

    // Reset coin counter
    void Awake()
    {
        CoinPickup.ResetProgress();
    }

    void Start()
    {
        // Requested webcam resolution and frame rate
        webcamTexture = new WebCamTexture(640, 480, 30);

        // Display webcam feed if renderer is available
        Renderer webcamRenderer = GetComponent<Renderer>();
        if (webcamRenderer != null)
        {
            webcamRenderer.material.mainTexture = webcamTexture;
        }

        webcamTexture.Play();

        initialRigPosition = stereoCameraRig.localPosition;
        targetPosition = initialRigPosition;
    }

    void Update()
    {
        // Recalibrate with C
        if (Input.GetKeyDown(KeyCode.C))
        {
            calibrated = false;
        }

        // Process new webcam frames only
        if (webcamTexture != null && webcamTexture.didUpdateThisFrame)
        {
            frameWidth = webcamTexture.width;
            frameHeight = webcamTexture.height;

            // Wait for valid webcam resolution
            if (frameWidth > 16 && frameHeight > 16)
            {
                if (pixels == null || pixels.Length != frameWidth * frameHeight)
                {
                    pixels = new Color32[frameWidth * frameHeight];
                }

                webcamTexture.GetPixels32(pixels);
                DetectRedLens();
                UpdateDetectionMask();
            }
        }

        // Smooth camera rig movement
        if (calibrated)
        {
            stereoCameraRig.localPosition = Vector3.SmoothDamp(
                stereoCameraRig.localPosition,
                targetPosition,
                ref smoothingVelocity,
                smoothingTime
            );
        }
    }

    bool IsRed(Color32 pixel)
    {
        int r = pixel.r;
        int g = pixel.g;
        int b = pixel.b;

        // Red as dominant colour
        if (r <= g || r <= b)
            return false;

        int min = Mathf.Min(g, b);
        int chroma = r - min;

        // Filter dark pixels
        if (r < minValue * 255f)
            return false;

        // Filter low saturation
        if (chroma < minSaturation * r)
            return false;

        // Check hue tolerance around red
        return Mathf.Abs(g - b) * 60f <= hueTolerance * chroma;
    }

    void DetectRedLens()
    {
        int step = Mathf.Max(1, pixelStep);

        int minX = 0;
        int maxX = frameWidth;
        int minY = 0;
        int maxY = frameHeight;

        // Restrict search area around last detected lens position
        if (useRoi && hasTarget)
        {
            float radius = Mathf.Sqrt(lastRedCount / Mathf.PI);
            int range = Mathf.CeilToInt(Mathf.Max(90f, radius * 3.5f));

            minX = Mathf.Max(0, Mathf.FloorToInt(lastCenterX - range));
            maxX = Mathf.Min(frameWidth, Mathf.CeilToInt(lastCenterX + range));
            minY = Mathf.Max(0, Mathf.FloorToInt(lastCenterY - range));
            maxY = Mathf.Min(frameHeight, Mathf.CeilToInt(lastCenterY + range));
        }

        int redCount = 0;
        long sumX = 0;
        long sumY = 0;

        for (int y = minY; y < maxY; y += step)
        {
            for (int x = minX; x < maxX; x += step)
            {
                Color32 pixel = pixels[y * frameWidth + x];

                if (IsRed(pixel))
                {
                    redCount++;
                    sumX += x;
                    sumY += y;
                }
            }
        }

        // Estimate total red area from sampled pixels
        int estimatedCount = redCount * step * step;

        // Target lost, full frame search on next update
        if (estimatedCount < minimumRedPixels)
        {
            hasTarget = false;
            return;
        }

        hasTarget = true;

        // Calculate centre of detected red pixels
        lastCenterX = (float)sumX / redCount;
        lastCenterY = (float)sumY / redCount;
        lastRedCount = estimatedCount;

        // Normalize coordinates to [-1, 1]
        float normalizedX = lastCenterX / frameWidth * 2f - 1f;
        float normalizedY = lastCenterY / frameHeight * 2f - 1f;

        // Correct mirrored coordinates
        if (webcamTexture.videoVerticallyMirrored)
            normalizedY = -normalizedY;

        if (mirrorX)
            normalizedX = -normalizedX;

        // Use current lens position and size as reference
        if (!calibrated)
        {
            referenceCenter = new Vector2(normalizedX, normalizedY);
            referenceRedPixels = estimatedCount;

            // Keep the original rig position when movement is limited.
            if (!limitMovement)
            {
                initialRigPosition = stereoCameraRig.localPosition;
            }

            targetPosition = initialRigPosition;
            smoothingVelocity = Vector3.zero;
            smoothedDepth = 0f;
            calibrated = true;
        }

        // Movement relative to calibrated position
        float relativeX = normalizedX - referenceCenter.x;
        float relativeY = normalizedY - referenceCenter.y;

        // Estimate distance from the apparent size of the red lens.
        float relativeDepth = Mathf.Sqrt(
            referenceRedPixels / (float)estimatedCount
        );

        // Moving closer increases the detected red area,
        // so relativeDepth becomes smaller.
        float targetDepth = (1f - relativeDepth) * depthScale;

        // Limit forward/backward movement.
        targetDepth = Mathf.Clamp(targetDepth, -1.5f, 1.5f);

        // Smooth fluctuations in the depth measurement.
        smoothedDepth = Mathf.Lerp(
            smoothedDepth,
            targetDepth,
            depthSmoothing
        );

        float z = smoothedDepth;

        // Map head movement to camera rig offset
        Vector3 offset = new Vector3(
            relativeX * horizontalScale,
            relativeY * verticalScale,
            z
        );

        // Limit head movement for the game scene only
        if (limitMovement)
        {
            offset.x = Mathf.Clamp(
                offset.x,
                -maxHorizontalOffset,
                maxHorizontalOffset
            );

            offset.y = Mathf.Clamp(
                offset.y,
                -maxVerticalOffset,
                maxVerticalOffset
            );

            offset.z = Mathf.Clamp(
                offset.z,
                -maxDepthOffset,
                maxDepthOffset
            );
        }

        // Target position relative to initial rig position
        targetPosition = initialRigPosition + offset;
    }

    // Vizualize red colour detection
    void UpdateDetectionMask()
    {
        if (!showDetectionPreview)
            return;

        // Create mask when camera resolution changes
        if (detectionMask == null ||
            detectionMask.width != frameWidth ||
            detectionMask.height != frameHeight)
        {
            if (detectionMask != null)
                Destroy(detectionMask);

            detectionMask = new Texture2D(
                frameWidth, frameHeight,
                TextureFormat.RGBA32, false
            );

            maskPixels = new Color32[pixels.Length];
        }

        // Accepted pixels are red others are black
        for (int i = 0; i < pixels.Length; i++)
        {
            maskPixels[i] = IsRed(pixels[i])
                ? new Color32(255, 0, 0, 255)
                : new Color32(0, 0, 0, 255);
        }

        detectionMask.SetPixels32(maskPixels);
        detectionMask.Apply(false);
    }

    // Show only mask in diagnostic mode
    void OnGUI()
    {
        if (showDetectionPreview)
        {
            GUI.DrawTexture(
                new Rect(0, 0, Screen.width, Screen.height),
                Texture2D.blackTexture
            );

            if (detectionMask != null)
            {
                GUI.DrawTexture(
                    new Rect(0, 0, Screen.width, Screen.height),
                    detectionMask,
                    ScaleMode.ScaleToFit
                );
            }

            return;
        }
        // Tracking information in every scena
        string panelText =
            "Tracking: " + hasTarget + "\n" +
            "Red pixels: " + lastRedCount + "\n" +
            "C - calibrate";

        float panelHeight = 75f;

        // Add game information only if there are coins
        if (CoinPickup.TotalCoins > 0)
        {
            panelText +=
                $"\n\nCoins: {CoinPickup.CollectedCoins}/{CoinPickup.TotalCoins}\n" +
                "WASD - move\n" +
                "Left click - collect";

            panelHeight = 145f;

            // Display completion when all coins collected
            if (CoinPickup.CollectedCoins >= CoinPickup.TotalCoins)
            {
                panelText += "\n\nAll coins collected!";
                panelHeight = 185f;
            }
        }

        GUI.Box(
            new Rect(10, 10, 280, panelHeight),
            panelText
        );
    }

    // Release mask texture and stop webcam
    void OnDestroy()
    {
        if (detectionMask != null)
            Destroy(detectionMask);

        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}
