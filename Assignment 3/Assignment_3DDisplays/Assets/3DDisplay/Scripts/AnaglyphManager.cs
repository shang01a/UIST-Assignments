using UnityEngine;

public class AnaglyphManager : MonoBehaviour
{
    [Tooltip("Camera to render the left eye's image")]
    [SerializeField] private Camera leftEyeCamera;
    
    [Tooltip("Camera to render the right eye's image")]
    [SerializeField] private Camera rightEyeCamera;
    
    [Tooltip("Material that has the anaglyph shader to combine two images")]
    [SerializeField] private Material anaglyphMaterial;

    // Use these texture to pass the cameras' view to the shader
    private RenderTexture leftEyeTexture;
    private RenderTexture rightEyeTexture;
    
    void OnRenderImage(RenderTexture src, RenderTexture dest)
    {
        /*
         * ToDo
         *
         * - For each eye:
         *   - Create a render texture
         *   - Assign this render texture to the camera for the corresponding eye
         *   - Render the eye's camera view into this texture
         *   - Transfer the texture to the material
         *
         * - Apply the material, e.g.,  using the Graphics.Blit method
         * - Release the created textures
         */

    }
}
