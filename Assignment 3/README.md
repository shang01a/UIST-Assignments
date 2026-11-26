# Assignment 3: Stereoscopic 3D and Head Tracking

## Setup

Use red-cyan anaglyph glasses.
Use natural indoor lighting and a plain background. Avoid bright light behind you, 
reflections on the glasses and other red objects in the webcam image.
Sit at a comfortable distance from the webcam. Keep the red lens visible and fully inside the webcam image.

## How to Test

### Cube Scene

1. Open the project in Unity 6000.3.25f1.
2. Open Assets/Scenes/CubeScene.unity.
3. Put on the anaglyph glasses and press Play to start the webcam.
4. Look straight at the screen and press C to calibrate.
5. Look at the cubes through the glasses to see the 3D effect.
6. Move your head left, right, up and down. The camera perspective should change, allowing you to look around the cubes.
7. Move your head closer to and further away from the webcam to test depth tracking.
8. Check the tracking status and red pixel count in the top left panel.

### Application Scene (Bakery Game)

1. Open Assets/Scenes/ApplicationScene.unity.
2. Put on the anaglyph glasses and press Play.
3. Press C to calibrate in a comfortable starting position.
4. Use WASD to move around the bakery.
5. Move your head to look behind objects and find hidden coins.
6. Left click on a coin within pickup range to collect it.
7. Check the coin counter in the top left panel.
8. Find and collect all coins to complete the game.

## Implementation

### Stereoscopic Rendering

The StereoCameraRig contains two cameras representing the left and right eye. The cameras are positioned at -0.0375 and 0.0375 on the local X axis, based on an estimated interpupillary distance (IPD) of 7.5 cm.

AnaglyphManager renders both camera images into separate temporary RenderTextures and passes them to the Anaglyph3D shader.
The fragment shader takes the red colour channel from the left camera image and the green and blue colour channels from the right camera image.
The combined image is displayed by the output camera. When wearing red-cyan glasses, each eye sees a slightly different perspective, creating the 3D effect.

### Head Tracking

The webcam image is captured using WebCamTexture. The HeadTracking script detects the red lens of the glasses based on hue, saturation and brightness.
The average position of the detected red pixels is used to find the centre of the lens. The X and Y coordinates are normalized to values between -1 and 1.
For faster detection, only every second pixel in each direction is checked. The script also supports a region of interest (ROI) around the last detected lens position. This option is currently disabled.
During calibration, the current lens position and red pixel count are saved as reference values.
The red pixel count is used to estimate depth:

relativeDepth = sqrt(referenceRedPixels / estimatedRedPixels)

targetDepth = (1 - relativeDepth) * depthScale

When the head moves closer to the webcam, the detected red area increases. When the head moves further away, the detected red area decreases.
The calculated X, Y and Z offsets control the local position of the StereoCameraRig.
Mathf.Lerp smooths the depth estimation and Vector3.SmoothDamp makes the camera movement smoother.
When the red lens is lost, the last target position is kept until the lens is detected again.
In the ApplicationScene, head movement is limited on all three axes to prevent excessive camera movement while moving around the bakery.

## Calibration Settings

Select the GameObject containing the HeadTracking component in the Inspector.

The following settings were used for our Cubesetup:

| Parameter | Value |
| --- | --- |
| hueTolerance | 18 |
| minSaturation | 0.5 |
| minValue | 0.3 |
| minimumRedPixels | 100 |
| pixelStep | 2 |
| useRoi | true |
| horizontalScale | 4 |
| verticalScale | 2 |
| depthScale | 1.6 |
| mirrorX | true |
| depthSmoothing | 0.15 |
| smoothingTime | 0.12 |

The following settings were used for our Application:

| Parameter | Value |
| --- | --- |
| hueTolerance | 12 |
| minSaturation | 0.65 |
| minValue | 0.1 |
| minimumRedPixels | 100 |
| pixelStep | 2 |
| useRoi | true |
| horizontalScale | 1 |
| verticalScale | 0.5 |
| depthScale | 0.2 |
| mirrorX | true |
| depthSmoothing | 0.15 |
| smoothingTime | 0.12 |

Tracking is calibrated automatically when the red lens is first detected.
To recalibrate, look straight at the screen from a comfortable distance and press C.
If horizontal head movement is inverted, change Mirror X in the Inspector.

### Game Scene Movement Limits

Limit Movement is only enabled in the ApplicationScene.

| Parameter | Value |
| --- | --- |
| maxHorizontalOffset | 0.5 |
| maxVerticalOffset | 0.3 |
| maxDepthOffset | 0.5 |

## Interactive 3D Showcase (Bakery Game)

The application is a small coin collection game set in a bakery. The goal is to explore the bakery and collect all coins. Some coins are hidden behind objects and are not immediately visible. Objects and coins are placed at different distances throughout the bakery to create foreground, midground and background layers.

### Interactive Mechanics

**Player Movement**

The player can move around the bakery using WASD. The PlayerMovement script uses a CharacterController to handle movement, collisions and gravity.

**Coin Collection**

The CoinPickup script uses a raycast from the interaction camera through the mouse position to detect clicked coins.
When a coin is collected, it disappears, the coin counter increases and a pickup sound plays if assigned. Once all coins have been collected, a completion message is displayed.

### Controls

| Input | Action |
| --- | --- |
| WASD | Move around the bakery |
| Head movement | Look around objects |
| Left click | Collect a coin |
| C | Recalibrate head tracking |

### 3D Value Justification

The 3D effect helps the player judge the distance between coins and other objects in the bakery. Since some coins are hidden behind objects, the player can move their head to the side to look behind them without having to move the entire character. This is especially useful when searching for coins that are not visible from the initial perspective. Compared to a fixed 2D view, head tracking and stereoscopic depth make exploring the bakery more natural and immersive.

## Troubleshooting

If the tracking does not work properly, open Assets/Scenes/TrackingCalibration.unity and press Play.
Detected red pixels are shown in red and all other pixels in black. This helps check whether the script detects the red lens or other objects in the webcam image.
Avoid reflections on the glasses by lowering the screen brightness or changing the room lighting.
Depending on the webcam and lighting conditions, Hue Tolerance, Min Saturation, Min Value and Minimum Red Pixels may need to be adjusted.
Return to CubeScene or ApplicationScene and press C to recalibrate.