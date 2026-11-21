
using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AnaglyphManager : MonoBehaviour
{
    [Header("Stereo Cameras")]
    [SerializeField] private Camera leftEyeCamera;
    [SerializeField] private Camera rightEyeCamera;

    [Header("Anaglyph Material")]
    [SerializeField] private Material anaglyphMaterial;

    private static readonly int LeftEyeTexID =
        Shader.PropertyToID("_LeftEyeTex");

    private static readonly int RightEyeTexID =
        Shader.PropertyToID("_RightEyeTex");

    void Start()
    {
        if (leftEyeCamera == null ||
            rightEyeCamera == null ||
            anaglyphMaterial == null)
        {
            Debug.LogError(
                "AnaglyphManager: Missing camera or material reference."
            );
            enabled = false;
            return;
        }

        // We render these cameras manually.
        // This prevents them from rendering directly to the screen.
        leftEyeCamera.enabled = false;
        rightEyeCamera.enabled = false;

        // The output camera should not render the cubes itself.
        // It only displays the combined stereo image.
        Camera outputCamera = GetComponent<Camera>();
        outputCamera.cullingMask = 0;
        outputCamera.clearFlags = CameraClearFlags.SolidColor;
        outputCamera.backgroundColor = Color.black;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        if (leftEyeCamera == null ||
            rightEyeCamera == null ||
            anaglyphMaterial == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        RenderTexture leftEyeTexture = null;
        RenderTexture rightEyeTexture = null;

        try
        {
            // Allocate temporary textures matching the output.
            leftEyeTexture = RenderTexture.GetTemporary(
                src.width, src.height, 24,
                RenderTextureFormat.ARGB32
            );

            rightEyeTexture = RenderTexture.GetTemporary(
                src.width, src.height, 24,
                RenderTextureFormat.ARGB32
            );

            // Render the left eye.
            leftEyeCamera.targetTexture = leftEyeTexture;
            leftEyeCamera.Render();

            // Render the right eye.
            rightEyeCamera.targetTexture = rightEyeTexture;
            rightEyeCamera.Render();

            // Supply both rendered images to the shader.
            anaglyphMaterial.SetTexture(
                LeftEyeTexID, leftEyeTexture
            );

            anaglyphMaterial.SetTexture(
                RightEyeTexID, rightEyeTexture
            );

            // Combine the images into red-cyan output.
            Graphics.Blit(src, dest, anaglyphMaterial);
        }
        finally
        {
            // Detach before releasing the temporary textures.
            leftEyeCamera.targetTexture = null;
            rightEyeCamera.targetTexture = null;

            if (leftEyeTexture != null)
                RenderTexture.ReleaseTemporary(leftEyeTexture);

            if (rightEyeTexture != null)
                RenderTexture.ReleaseTemporary(rightEyeTexture);
        }
    }
}
