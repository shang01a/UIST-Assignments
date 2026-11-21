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

    public int minimumRedPixels = 150;
    public int pixelStep = 2;
    public bool useRoi = true;

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

    private Vector3 initialRigPosition;
    private Vector3 targetPosition;
    private Vector3 smoothingVelocity;

    private Vector2 referenceCenter;
    private float referenceRedPixels;
    private bool calibrated;

    private float lastCenterX;
    private float lastCenterY;
    private int lastRedCount;
    private bool hasTarget;

    private int frameWidth;
    private int frameHeight;

    void Start()
    {
        webcamTexture = new WebCamTexture(640, 480, 30);

        Renderer webcamRenderer = GetComponent<Renderer>();
        if (webcamRenderer != null)
        {
            webcamRenderer.material.mainTexture = webcamTexture;
        }

        webcamTexture.Play();

        initialRigPosition = stereoCameraRig.position;
        targetPosition = initialRigPosition;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.C))
        {
            calibrated = false;
        }

        if (webcamTexture != null && webcamTexture.didUpdateThisFrame)
        {
            frameWidth = webcamTexture.width;
            frameHeight = webcamTexture.height;

            if (frameWidth > 16 && frameHeight > 16)
            {
                if (pixels == null || pixels.Length != frameWidth * frameHeight)
                {
                    pixels = new Color32[frameWidth * frameHeight];
                }

                webcamTexture.GetPixels32(pixels);
                DetectRedLens();
            }
        }

        if (calibrated)
        {
            stereoCameraRig.position = Vector3.SmoothDamp(
                stereoCameraRig.position,
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

        if (r <= g || r <= b)
            return false;

        int min = Mathf.Min(g, b);
        int chroma = r - min;

        if (r < minValue * 255f)
            return false;

        if (chroma < minSaturation * r)
            return false;

        return Mathf.Abs(g - b) * 60f <= hueTolerance * chroma;
    }

    void DetectRedLens()
    {
        int step = Mathf.Max(1, pixelStep);

        int minX = 0;
        int maxX = frameWidth;
        int minY = 0;
        int maxY = frameHeight;

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

        int estimatedCount = redCount * step * step;

        if (estimatedCount < minimumRedPixels)
        {
            hasTarget = false;
            return;
        }

        hasTarget = true;

        lastCenterX = (float)sumX / redCount;
        lastCenterY = (float)sumY / redCount;
        lastRedCount = estimatedCount;

        float normalizedX = lastCenterX / frameWidth * 2f - 1f;
        float normalizedY = lastCenterY / frameHeight * 2f - 1f;

        if (webcamTexture.videoVerticallyMirrored)
            normalizedY = -normalizedY;

        if (mirrorX)
            normalizedX = -normalizedX;

        if (!calibrated)
        {
            referenceCenter = new Vector2(normalizedX, normalizedY);
            referenceRedPixels = estimatedCount;
            initialRigPosition = stereoCameraRig.position;
            targetPosition = initialRigPosition;
            smoothingVelocity = Vector3.zero;
            smoothedDepth = 0f;
            calibrated = true;
        }

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

        targetPosition = initialRigPosition + new Vector3(
            relativeX * horizontalScale,
            relativeY * verticalScale,
            z
        );
    }

    void OnGUI()
    {
        GUI.Box(
            new Rect(10, 10, 220, 75),
            "Tracking: " + hasTarget + "\n" +
            "Red pixels: " + lastRedCount + "\n" +
            "Press C to calibrate"
        );
    }

    void OnDestroy()
    {
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}