using UnityEngine;

/// <summary>
/// Webcam-based red-lens tracking for a stereoscopic camera rig.
///
/// Pipeline (runs once per new webcam frame):
///   1. Grab the frame into a reusable Color32[] buffer (GetPixels32, no per-frame allocations).
///   2. Segment "red" pixels with an HSV test (hue around 0°, minimum saturation and brightness).
///   3. Compute the 2D centroid and pixel count of the marker. A second, local pass inside a
///      region of interest (ROI) around the marker rejects stray red pixels elsewhere in the image.
///   4. Map the centroid to camera-centred normalised coordinates:
///        (0,0) = image centre, (-1, 1) = upper-left, (1, -1) = lower-right.
///   5. Estimate depth from the apparent size: radius = sqrt(area / PI), z ~ 1 / radius.
///   6. Smooth x, y, z over time (One Euro filter or exponential moving average).
///   7. Move the stereo camera rig and draw a debug HUD (coordinates, pixel count, mask view).
///
/// Keyboard shortcuts (Game view must have focus):
///   C = recalibrate reference lens size and neutral head position
///   D = toggle segmentation mask view
///   H = toggle HUD
///   M = toggle horizontal mirroring of the output coordinates
///   R = reset smoothing filters
///
/// Written for Unity 2019.4 (C# 7.3).
/// </summary>
public class CircleDetection : MonoBehaviour
{
    public enum SmoothingMode { None, Exponential, OneEuro }

    // ------------------------------------------------------------------ Inspector

    [Header("Scene References")]
    [Tooltip("The root StereoCameraRig object in CubeScene.")]
    public Transform stereoCameraRig;
    private Vector3 rigStartPosition;
    private Vector3 neutralHeadPosition;
    private bool neutralHeadSet;

    [Header("Webcam")]
    [Tooltip("Index into WebCamTexture.devices. Use the HUD to see which cameras are available.")]
    public int webcamDeviceIndex = 0;
    public int requestedWidth = 640;
    public int requestedHeight = 480;
    public int requestedFps = 30;

    [Header("Red Segmentation (HSV)")]
    [Tooltip("Max hue distance from pure red, in degrees. Larger = accepts more orange/pink.")]
    [Range(1f, 60f)] public float hueTolerance = 18f;
    [Tooltip("Minimum saturation (0-1). Raise this if skin or wood is detected as red.")]
    [Range(0f, 1f)] public float minSaturation = 0.45f;
    [Tooltip("Minimum brightness / value (0-1). Raise this if dark shadows are detected as red.")]
    [Range(0f, 1f)] public float minValue = 0.25f;
    [Tooltip("Only every n-th pixel in x and y is examined. 2 = 4x faster with almost no precision loss.")]
    [Range(1, 8)] public int pixelStep = 2;
    [Tooltip("Minimum number of red pixels (full-resolution estimate) before the marker counts as detected.")]
    public int minPixelCount = 150;

    [Header("Outlier Rejection")]
    [Tooltip("Run a second pass in a window around the marker so that other red objects do not pull the centroid.")]
    public bool useRoiRefinement = true;
    [Tooltip("ROI half-size as a multiple of the marker radius.")]
    [Range(1.2f, 4f)] public float roiScale = 1.8f;

    [Header("Coordinate Mapping")]
    [Tooltip("Flip the x output (use if your webcam image is mirrored compared to the real world).")]
    public bool mirrorX = false;

    [Header("Depth Estimation")]
    [Tooltip("Distance (any unit, e.g. cm) at which you calibrate. z is reported in the same unit.")]
    public float referenceDistance = 30f;
    [Tooltip("Marker radius in pixels at the reference distance. 0 = auto-calibrate on first detection. Press C to recalibrate.")]
    public float referenceRadiusPx = 0f;
    [Tooltip("Number of frames averaged during calibration.")]
    public int calibrationFrames = 15;

    [Header("Temporal Smoothing")]
    public SmoothingMode smoothing = SmoothingMode.OneEuro;
    [Tooltip("Exponential: weight of the newest sample (lower = smoother, more lag).")]
    [Range(0.01f, 1f)] public float emaAlpha = 0.25f;
    [Tooltip("One Euro: cutoff frequency at rest in Hz (lower = less jitter when still).")]
    [Range(0.05f, 10f)] public float oneEuroMinCutoff = 1.0f;
    [Tooltip("One Euro: speed coefficient (higher = less lag on fast movements).")]
    [Range(0f, 10f)] public float oneEuroBeta = 1.5f;
    [Tooltip("One Euro: extra smoothing for depth, which is noisier than x/y. Multiplies Min Cutoff for z.")]
    [Range(0.1f, 1f)] public float depthCutoffScale = 0.5f;

    [Header("Stereo Rig Movement")]
    [Tooltip("Unity units moved per normalized webcam X/Y unit.")]
    public Vector2 movementRange = new Vector2(0.8f, 0.8f);
    [Tooltip("Unity units moved per relative change in tracked depth.")]
    public float depthMovementRange = 1.5f;

    [Header("Debug HUD")]
    public bool showHud = true;
    public bool showReticle = true;
    public bool showMask = false;

    // ------------------------------------------------------------------ Public read-only state

    /// <summary>Raw camera-centred coordinates of the latest detection.</summary>
    public Vector3 RawPosition { get; private set; }
    /// <summary>Smoothed camera-centred coordinates (x/y normalized; z in referenceDistance units).</summary>
    public Vector3 SmoothedPosition { get; private set; }
    public bool IsTracking { get; private set; }
    public int RedPixelCount { get; private set; }

    // ------------------------------------------------------------------ Private state

    private WebCamTexture webcamTexture;
    private Color32[] pixelBuffer;          // reused every frame -> no GC
    private int frameWidth, frameHeight;

    private Texture2D maskTexture;          // debug view of segmented pixels (downsampled)
    private Color32[] maskBuffer;
    private Texture2D whiteTexture;         // for drawing HUD lines/boxes

    // Raw image-space results (pixels, origin bottom-left as delivered by GetPixels32)
    private float centroidPxX, centroidPxY, radiusPx;
    private bool hadDetectionLastFrame;
    private float lastDetectionTime = -999f;

    // Calibration
    private int calibCount;
    private float calibSum;
    private bool calibrating;

    // Filters
    private readonly OneEuroFilter filterX = new OneEuroFilter();
    private readonly OneEuroFilter filterY = new OneEuroFilter();
    private readonly OneEuroFilter filterZ = new OneEuroFilter();
    private Vector3 emaState;
    private bool emaInitialised;
    private float lastSampleTime;

    // Stats
    private float processingMs;
    private float trackingFps;
    private float lastFrameTime;
    private string statusMessage = "";

    private int maskStep;                   // pixelStep the mask texture was built for
    private GUIStyle hudStyle;

    // ================================================================== Unity lifecycle

    void Start()
    {
        WebCamDevice[] devices = WebCamTexture.devices;
        if (devices.Length == 0)
        {
            statusMessage = "No webcam found!";
            Debug.LogError(statusMessage);
            return;
        }

        webcamDeviceIndex = Mathf.Clamp(webcamDeviceIndex, 0, devices.Length - 1);
        webcamTexture = new WebCamTexture(devices[webcamDeviceIndex].name,
                                          requestedWidth, requestedHeight, requestedFps);
        // A webcam-preview quad is optional; the tracker also works on an empty GameObject.
        Renderer previewRenderer = GetComponent<Renderer>();
        if (previewRenderer != null)
            previewRenderer.material.mainTexture = webcamTexture;
        webcamTexture.Play();

        whiteTexture = Texture2D.whiteTexture;

        if (stereoCameraRig != null)
            rigStartPosition = stereoCameraRig.localPosition;
        else
            Debug.LogWarning("CircleDetection: Assign StereoCameraRig in the Inspector.");

        // Auto-calibrate on first detection if no reference radius was entered.
        calibrating = referenceRadiusPx <= 0f;
    }

    void Update()
    {
        HandleKeyboard();

        if (webcamTexture == null || !webcamTexture.isPlaying) return;

        // WebCamTexture reports 16x16 until the first real frame arrives.
        if (webcamTexture.width <= 16) return;

        if (webcamTexture.didUpdateThisFrame)
        {
            float t0 = Time.realtimeSinceStartup;

            EnsureBuffers(webcamTexture.width, webcamTexture.height);
            webcamTexture.GetPixels32(pixelBuffer);   // fast, fills the existing buffer
            DetectRedCircle();

            processingMs = (Time.realtimeSinceStartup - t0) * 1000f;
            float now = Time.time;
            float dt = now - lastFrameTime;
            if (dt > 0f) trackingFps = Mathf.Lerp(trackingFps, 1f / dt, 0.1f);
            lastFrameTime = now;
        }
    }

    void OnDestroy()
    {
        if (webcamTexture != null) webcamTexture.Stop();
        if (maskTexture != null) Destroy(maskTexture);
    }

    // ================================================================== Setup helpers

    void EnsureBuffers(int w, int h)
    {
        if (pixelBuffer != null && w == frameWidth && h == frameHeight && maskStep == pixelStep) return;

        if (pixelBuffer == null || w != frameWidth || h != frameHeight)
            pixelBuffer = new Color32[w * h];
        frameWidth = w;
        frameHeight = h;
        maskStep = pixelStep;

        int mw = Mathf.Max(1, (w + pixelStep - 1) / pixelStep);
        int mh = Mathf.Max(1, (h + pixelStep - 1) / pixelStep);
        if (maskTexture != null) Destroy(maskTexture);
        maskTexture = new Texture2D(mw, mh, TextureFormat.RGBA32, false);
        maskTexture.filterMode = FilterMode.Point;
        maskBuffer = new Color32[mw * mh];
    }

    // ================================================================== Computer vision

    /// HSV red test on integer RGB without converting every pixel to floats.
    /// Red means: R is the largest channel, hue within +-hueTolerance of 0°,
    /// saturation >= minSaturation and value >= minValue.

    bool IsRed(Color32 c)
    {
        int r = c.r, g = c.g, b = c.b;
        if (r <= g || r <= b) return false;                 // R must dominate

        int min = g < b ? g : b;
        int chroma = r - min;                               // max - min, max == r
        if (r < minValue * 255f) return false;              // value  = max / 255
        if (chroma < minSaturation * r) return false;       // saturation = chroma / max

        // Hue (degrees) for max == R is 60 * (g - b) / chroma, range (-60, 60).
        // |60 * (g - b) / chroma| <= tol  <=>  |g - b| * 60 <= tol * chroma
        int diff = g - b;
        if (diff < 0) diff = -diff;
        return diff * 60 <= hueTolerance * chroma;
    }

    void DetectRedCircle()
    {
        int width = frameWidth;
        int height = frameHeight;
        int step = Mathf.Max(1, pixelStep);
        bool writeMask = showMask && maskBuffer != null;
        int maskW = maskTexture != null ? maskTexture.width : 0;

        // global segmentation over the whole frame (subsampled) 
        long sumX = 0, sumY = 0;
        int count = 0;
        Color32 on = new Color32(255, 40, 40, 255);
        Color32 off = new Color32(0, 0, 0, 200);

        for (int y = 0, my = 0; y < height; y += step, my++)
        {
            int row = y * width;
            int maskRow = my * maskW;
            for (int x = 0, mx = 0; x < width; x += step, mx++)
            {
                bool red = IsRed(pixelBuffer[row + x]);
                if (red)
                {
                    sumX += x;
                    sumY += y;
                    count++;
                }
                if (writeMask) maskBuffer[maskRow + mx] = red ? on : off;
            }
        }

        if (writeMask)
        {
            maskTexture.SetPixels32(maskBuffer);
            maskTexture.Apply(false);
        }

        float stepArea = step * step;               // one sample stands for step*step pixels
        int globalCount = Mathf.RoundToInt(count * stepArea);

        bool found = false;
        float cx = 0f, cy = 0f;
        int finalCount = 0;

        if (globalCount >= minPixelCount)
        {
            cx = (float)sumX / count;
            cy = (float)sumY / count;
            finalCount = globalCount;
            found = true;
        }

        // local refinement inside a ROI around the marker 
        // Seed with last frame's marker (temporal coherence) if we were tracking, otherwise
        // with the global centroid. This ignores red objects far away from the marker.
        if (useRoiRefinement && (found || hadDetectionLastFrame))
        {
            bool refined = false;
            if (hadDetectionLastFrame)
                refined = RefineInRoi(centroidPxX, centroidPxY, radiusPx, step, ref cx, ref cy, ref finalCount);
            if (!refined && found)
                refined = RefineInRoi(cx, cy, Mathf.Sqrt(finalCount / Mathf.PI), step, ref cx, ref cy, ref finalCount);
            found = refined || found;
        }

        RedPixelCount = found ? finalCount : globalCount;

        if (!found)
        {
            IsTracking = false;
            hadDetectionLastFrame = false;
            return;
        }

        // Apparent radius from the area of a disc: A = PI * r^2  ->  r = sqrt(A / PI).
        // Area is more robust than a bounding box because it uses every pixel.
        centroidPxX = cx;
        centroidPxY = cy;
        radiusPx = Mathf.Sqrt(finalCount / Mathf.PI);
        hadDetectionLastFrame = true;
        lastDetectionTime = Time.time;

        UpdateCalibration();
        if (calibrating || referenceRadiusPx <= 0f)
        {
            IsTracking = false;
            return;
        }

        // ---------- Camera-centred normalised coordinates ----------
        // GetPixels32 rows start at the bottom of the image, so y grows upwards already.
        float nx = (cx / (width - 1)) * 2f - 1f;    // -1 = left,   +1 = right
        float ny = (cy / (height - 1)) * 2f - 1f;   // -1 = bottom, +1 = top
        if (webcamTexture.videoVerticallyMirrored) ny = -ny;
        if (mirrorX) nx = -nx;

        // ---------- Depth from apparent size ----------
        // Pinhole model: apparent radius is inversely proportional to distance.
        // z = referenceDistance * (referenceRadius / currentRadius)
        // Closer -> larger radius -> smaller z.  Further -> smaller radius -> larger z.
        float z = referenceDistance * (referenceRadiusPx / Mathf.Max(radiusPx, 0.001f));

        RawPosition = new Vector3(nx, ny, z);
        SmoothedPosition = Smooth(RawPosition, Time.time);
        IsTracking = true;

        MoveCameraRig(SmoothedPosition);
    }

    /// Re-runs segmentation in a square window around a seed point and replaces the centroid/count
    /// if enough red pixels are found there. Returns false if the ROI does not contain the marker.

    bool RefineInRoi(float seedX, float seedY, float seedRadius, int step,
                     ref float cx, ref float cy, ref int pixelCount)
    {
        float half = Mathf.Max(seedRadius * roiScale, 20f);
        int x0 = Mathf.Max(0, Mathf.FloorToInt(seedX - half));
        int x1 = Mathf.Min(frameWidth - 1, Mathf.CeilToInt(seedX + half));
        int y0 = Mathf.Max(0, Mathf.FloorToInt(seedY - half));
        int y1 = Mathf.Min(frameHeight - 1, Mathf.CeilToInt(seedY + half));

        // Align to the sampling grid so pass 1 and pass 2 see the same pixels.
        x0 -= x0 % step;
        y0 -= y0 % step;

        long sumX = 0, sumY = 0;
        int count = 0;
        for (int y = y0; y <= y1; y += step)
        {
            int row = y * frameWidth;
            for (int x = x0; x <= x1; x += step)
            {
                if (IsRed(pixelBuffer[row + x]))
                {
                    sumX += x;
                    sumY += y;
                    count++;
                }
            }
        }

        int estimated = Mathf.RoundToInt(count * step * step);
        if (estimated < minPixelCount) return false;

        cx = (float)sumX / count;
        cy = (float)sumY / count;
        pixelCount = estimated;
        return true;
    }

    // ================================================================== Calibration

    /// <summary>Start a depth calibration: the next few detections define referenceRadiusPx.</summary>
    [ContextMenu("Calibrate Depth Now")]
    public void StartCalibration()
    {
        calibrating = true;
        calibCount = 0;
        calibSum = 0f;
        neutralHeadSet = false;
        if (stereoCameraRig != null)
            stereoCameraRig.localPosition = rigStartPosition;
        ResetFilters();
    }

    void UpdateCalibration()
    {
        if (!calibrating) return;
        calibSum += radiusPx;
        calibCount++;
        if (calibCount >= Mathf.Max(1, calibrationFrames))
        {
            referenceRadiusPx = calibSum / calibCount;
            calibrating = false;
            calibCount = 0;
            calibSum = 0f;
            ResetFilters();
            Debug.Log("Depth calibrated: reference radius = " + referenceRadiusPx.ToString("F1") +
                      " px at " + referenceDistance + " units.");
        }
    }

    // ================================================================== Smoothing

    Vector3 Smooth(Vector3 raw, float time)
    {
        // After losing the marker for a while, start fresh instead of sliding from the old position.
        if (time - lastSampleTime > 0.5f) ResetFilters();
        float dt = Mathf.Max(time - lastSampleTime, 1f / 120f);
        lastSampleTime = time;

        switch (smoothing)
        {
            case SmoothingMode.Exponential:
                if (!emaInitialised) { emaState = raw; emaInitialised = true; }
                else emaState = Vector3.Lerp(emaState, raw, emaAlpha);
                return emaState;

            case SmoothingMode.OneEuro:
                // z is filtered relative to the reference distance so the filter sees a ~1.0-scaled
                // signal like x and y; it gets a lower cutoff because size-based depth is noisier.
                float zRel = raw.z / referenceDistance;
                return new Vector3(
                    filterX.Filter(raw.x, dt, oneEuroMinCutoff, oneEuroBeta),
                    filterY.Filter(raw.y, dt, oneEuroMinCutoff, oneEuroBeta),
                    filterZ.Filter(zRel, dt, oneEuroMinCutoff * depthCutoffScale, oneEuroBeta) * referenceDistance);

            default:
                return raw;
        }
    }

    [ContextMenu("Reset Filters")]
    public void ResetFilters()
    {
        filterX.Reset();
        filterY.Reset();
        filterZ.Reset();
        emaInitialised = false;
    }

    // ================================================================== Output

    void MoveCameraRig(Vector3 p)
    {
        if (stereoCameraRig == null) return;

        // The first stable tracking sample establishes the neutral head position.
        if (!neutralHeadSet)
        {
            neutralHeadPosition = p;
            neutralHeadSet = true;
        }

        Vector3 offset = p - neutralHeadPosition;

        // Webcam Z is distance from camera: moving closer lowers Z.
        // Our Unity cameras look toward +Z, so closer means rig moves +Z.
        float x = offset.x * movementRange.x;
        float y = offset.y * movementRange.y;
        float z = -(offset.z / Mathf.Max(referenceDistance, 0.001f))
                  * depthMovementRange;

        stereoCameraRig.localPosition = rigStartPosition + new Vector3(x, y, z);
    }

    void HandleKeyboard()
    {
        if (Input.GetKeyDown(KeyCode.C)) StartCalibration();
        if (Input.GetKeyDown(KeyCode.D)) showMask = !showMask;
        if (Input.GetKeyDown(KeyCode.H)) showHud = !showHud;
        if (Input.GetKeyDown(KeyCode.M)) { mirrorX = !mirrorX; ResetFilters(); }
        if (Input.GetKeyDown(KeyCode.R)) ResetFilters();
    }

    // ================================================================== Debug HUD (IMGUI)

    void OnGUI()
    {
        if (!showHud) return;

        if (showReticle && hadDetectionLastFrame) DrawReticle();
        if (showMask && maskTexture != null) DrawMask();

        if (hudStyle == null) hudStyle = new GUIStyle(GUI.skin.label) { fontSize = 14, richText = true };
        GUIStyle style = hudStyle;
        const float w = 330f;
        GUILayout.BeginArea(new Rect(10, 10, w, 330), GUI.skin.box);

        if (!string.IsNullOrEmpty(statusMessage))
        {
            GUILayout.Label("<color=#ff5555><b>" + statusMessage + "</b></color>", style);
            GUILayout.EndArea();
            return;
        }

        string state;
        if (calibrating) state = "<color=#ffcc33>CALIBRATING (hold marker at " + referenceDistance + ")</color>";
        else if (IsTracking) state = "<color=#55ff55>TRACKING</color>";
        else state = "<color=#ff5555>LOST</color> (" + (Time.time - lastDetectionTime).ToString("F1") + " s)";

        Vector3 s = SmoothedPosition;
        Vector3 r = RawPosition;

        GUILayout.Label("<b>Red Lens Tracker</b>   " + state, style);
        GUILayout.Label("<b>x</b> " + s.x.ToString("+0.000;-0.000") + "   <color=#aaaaaa>raw " + r.x.ToString("+0.000;-0.000") + "</color>", style);
        GUILayout.Label("<b>y</b> " + s.y.ToString("+0.000;-0.000") + "   <color=#aaaaaa>raw " + r.y.ToString("+0.000;-0.000") + "</color>", style);
        GUILayout.Label("<b>z</b> " + s.z.ToString("0.0") + "   <color=#aaaaaa>raw " + r.z.ToString("0.0") + "  (ref " + referenceDistance + ")</color>", style);
        GUILayout.Label("Red pixels: <b>" + RedPixelCount + "</b>   (min " + minPixelCount + ")", style);
        GUILayout.Label("Radius: " + radiusPx.ToString("F1") + " px   ref " + referenceRadiusPx.ToString("F1") + " px", style);
        GUILayout.Label("Centroid: (" + centroidPxX.ToString("F0") + ", " + centroidPxY.ToString("F0") + ") px", style);
        GUILayout.Label("Camera: " + frameWidth + "x" + frameHeight + "   " + trackingFps.ToString("F0") + " fps   " + processingMs.ToString("F1") + " ms", style);
        GUILayout.Label("Smoothing: " + smoothing + (mirrorX ? "   (mirrored X)" : ""), style);
        GUILayout.Label("<color=#aaaaaa>[C] calibrate  [D] mask  [M] mirror  [R] reset  [H] hide</color>", style);
        GUILayout.EndArea();
    }

    /// <summary>Converts a point on an optional webcam preview quad to GUI coordinates.</summary>
    bool ImageToGui(float px, float py, out Vector2 gui)
    {
        gui = Vector2.zero;
        Camera cam = Camera.main;
        if (cam == null || frameWidth == 0) return false;

        // Unity's quad mesh spans -0.5..0.5 in local x and y; texture (0,0) is bottom-left.
        Vector3 local = new Vector3(px / frameWidth - 0.5f, py / frameHeight - 0.5f, 0f);
        Vector3 screen = cam.WorldToScreenPoint(transform.TransformPoint(local));
        if (screen.z < 0f) return false;
        gui = new Vector2(screen.x, Screen.height - screen.y);   // GUI y goes downwards
        return true;
    }

    void DrawReticle()
    {
        Vector2 c, edge;
        if (!ImageToGui(centroidPxX, centroidPxY, out c)) return;
        if (!ImageToGui(centroidPxX + radiusPx, centroidPxY, out edge)) return;
        float rad = Mathf.Max(4f, Vector2.Distance(c, edge));

        Color old = GUI.color;
        GUI.color = IsTracking ? Color.green : Color.yellow;
        // Crosshair
        GUI.DrawTexture(new Rect(c.x - rad - 6, c.y - 1, 2 * rad + 12, 2), whiteTexture);
        GUI.DrawTexture(new Rect(c.x - 1, c.y - rad - 6, 2, 2 * rad + 12), whiteTexture);
        // Bounding square of the estimated circle
        GUI.DrawTexture(new Rect(c.x - rad, c.y - rad, 2 * rad, 2), whiteTexture);
        GUI.DrawTexture(new Rect(c.x - rad, c.y + rad - 2, 2 * rad, 2), whiteTexture);
        GUI.DrawTexture(new Rect(c.x - rad, c.y - rad, 2, 2 * rad), whiteTexture);
        GUI.DrawTexture(new Rect(c.x + rad - 2, c.y - rad, 2, 2 * rad), whiteTexture);
        GUI.color = old;
    }

    void DrawMask()
    {
        float w = 240f;
        float h = w * maskTexture.height / Mathf.Max(1, maskTexture.width);
        Rect rect = new Rect(Screen.width - w - 10, 10, w, h);
        GUI.Box(new Rect(rect.x - 2, rect.y - 2, rect.width + 4, rect.height + 22), GUIContent.none);
        // Texture row 0 is the bottom, which DrawTexture also expects, so no flipping needed.
        GUI.DrawTexture(rect, maskTexture, ScaleMode.StretchToFill, true);
        GUI.Label(new Rect(rect.x + 4, rect.yMax + 2, w, 20), "Segmentation mask");
    }

    // ================================================================== One Euro Filter

    /// One Euro filter (Casiez, Roussel & Vogel, CHI 2012): an adaptive low-pass filter.
    /// When the signal is slow, the cutoff is low -> jitter is removed.
    /// When the signal moves fast, the cutoff rises -> lag stays small.
    private class OneEuroFilter
    {
        private const float DerivativeCutoff = 1f;
        private bool initialised;
        private float prevValue;
        private float prevDerivative;

        public void Reset() { initialised = false; }

        public float Filter(float value, float dt, float minCutoff, float beta)
        {
            if (!initialised)
            {
                initialised = true;
                prevValue = value;
                prevDerivative = 0f;
                return value;
            }

            // Smoothed speed of the signal
            float derivative = (value - prevValue) / dt;
            float dAlpha = Alpha(dt, DerivativeCutoff);
            prevDerivative = prevDerivative + dAlpha * (derivative - prevDerivative);

            // Cutoff grows with speed
            float cutoff = minCutoff + beta * Mathf.Abs(prevDerivative);
            float a = Alpha(dt, cutoff);
            prevValue = prevValue + a * (value - prevValue);
            return prevValue;
        }

        private static float Alpha(float dt, float cutoff)
        {
            float tau = 1f / (2f * Mathf.PI * cutoff);
            return 1f / (1f + tau / dt);
        }
    }
}