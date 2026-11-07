
using UnityEngine;

public class CircleDetection : MonoBehaviour
{
    private WebCamTexture webcamTexture;
    public GameObject redSphere; // Reference to your red sphere object

    private Color32[] pixels;

    // Values can be adjusted in the Inspector depending on lighting.
    [Header("Red Circle Detection")]
    [Range(0, 255)]
    public int redThreshold = 120;

    [Range(1f, 3f)]
    public float redDominance = 1.5f;

    public int minimumRedPixels = 50;

    // Detection results for the other scripts.
    public bool HasTarget { get; private set; }
    public float CenterXPixel { get; private set; }
    public float CenterYPixel { get; private set; }
    public int RedPixelCount { get; private set; }
    public int FrameWidth { get; private set; }
    public int FrameHeight { get; private set; }


    // Start is called before the first frame update
    void Start()
    {
        webcamTexture = new WebCamTexture();
        GetComponent<Renderer>().material.mainTexture = webcamTexture;
        webcamTexture.Play();
    }

    // Update is called once per frame
    void Update()
    {
        if (webcamTexture.didUpdateThisFrame)
        {
            // Capture the webcam texture
            int width = webcamTexture.width;
            int height = webcamTexture.height;

            // The webcam can take a moment to return a valid image size.
            if (width <= 16 || height <= 16)
                return;

            FrameWidth = width;
            FrameHeight = height;

            // Reuse the pixel array instead of creating a new texture each frame.
            if (pixels == null || pixels.Length != width * height)
            {
                pixels = new Color32[width * height];
            }

            webcamTexture.GetPixels32(pixels);

            // Perform image processing on the captured frame
            DetectRedCircle();
        }
    }

    void DetectRedCircle()
    {
        int width = FrameWidth;
        int height = FrameHeight;

        int redCount = 0;
        long sumX = 0;
        long sumY = 0;

        //TODO: Implement tracking of a red circle -> implemented

        // Check each pixel to see if it is mainly red.
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

        // Ignore small red areas that are likely caused by noise.
        HasTarget = redCount >= minimumRedPixels;

        if (HasTarget)
        {
            // The centroid is the average position of all detected red pixels.
            CenterXPixel = (float)sumX / redCount;
            CenterYPixel = (float)sumY / redCount;
        }
        else
        {
            CenterXPixel = 0f;
            CenterYPixel = 0f;
        }


        // TODO: Move the tracking object (e.g., sphere) to the detected circle's center

        // Part 2 will use the centroid and pixel count to calculate
        // the 3D coordinates and move the sphere


        // Print detection results occasionally while testing.
        // The Debug HUD will later display these values on screen
        if (Time.frameCount % 30 == 0)
        {
            Debug.Log(
                "Detected: " + HasTarget +
                " | X: " + CenterXPixel +
                " | Y: " + CenterYPixel +
                " | Red pixels: " + RedPixelCount
            );
        }
    }

    void OnDestroy()
    {
        // Stop the webcam when the object is removed.
        if (webcamTexture != null && webcamTexture.isPlaying)
        {
            webcamTexture.Stop();
        }
    }
}
