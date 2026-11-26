using UnityEngine;

[RequireComponent(typeof(Camera))]
public class AnaglyphManager : MonoBehaviour
{
    [Header("Stereo Cameras")]
    [SerializeField] private Camera leftEyeCamera;
    [SerializeField] private Camera rightEyeCamera;

    [Header("Anaglyph Material")]
    [SerializeField] private Material anaglyphMaterial;

    // Cache shader property IDs
    private static readonly int LeftEyeTexID =
        Shader.PropertyToID("_LeftEyeTex");

    private static readonly int RightEyeTexID =
        Shader.PropertyToID("_RightEyeTex");

    void Start()
    {
        // Check required references
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

        // Disable automatic rendering of both stereo cameras
        leftEyeCamera.enabled = false;
        rightEyeCamera.enabled = false;

        // Prevent output camera from rendering scene objects
        // Only display the combined stereo image
        Camera outputCamera = GetComponent<Camera>();
        outputCamera.cullingMask = 0;
        outputCamera.clearFlags = CameraClearFlags.SolidColor;
        outputCamera.backgroundColor = Color.black;
    }

    // Postprocessing of the output camera image
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        // Fallback to original image if references are missing
        if (leftEyeCamera == null ||
            rightEyeCamera == null ||
            anaglyphMaterial == null)
        {
            Graphics.Blit(src, dest);
            return;
        }

        // Separate render textures for both eyes
        RenderTexture leftEyeTexture = null;
        RenderTexture rightEyeTexture = null;

        try
        {
            // Allocate temporary textures matching the output resolution
            leftEyeTexture = RenderTexture.GetTemporary(
                src.width, src.height, 24,
                RenderTextureFormat.ARGB32
            );

            rightEyeTexture = RenderTexture.GetTemporary(
                src.width, src.height, 24,
                RenderTextureFormat.ARGB32
            );

            // Render lefteye and righteye camera image into texture
            leftEyeCamera.targetTexture = leftEyeTexture;
            leftEyeCamera.Render();
            rightEyeCamera.targetTexture = rightEyeTexture;
            rightEyeCamera.Render();

            // Pass both rendered images to the shader
            anaglyphMaterial.SetTexture(
                LeftEyeTexID, leftEyeTexture
            );
            anaglyphMaterial.SetTexture(
                RightEyeTexID, rightEyeTexture
            );

            // Combine both images into anaglyph output
            Graphics.Blit(src, dest, anaglyphMaterial);
        }
        finally
        {
            // Detach textures from stereo cameras
            leftEyeCamera.targetTexture = null;
            rightEyeCamera.targetTexture = null;

            // Release temporary textures even if rendering fails
            if (leftEyeTexture != null)
                RenderTexture.ReleaseTemporary(leftEyeTexture);

            if (rightEyeTexture != null)
                RenderTexture.ReleaseTemporary(rightEyeTexture);
        }
    }
}