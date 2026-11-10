# Assignment 2: 3D Tracking

## Setup

We calibrated tracking for RedCircle.png shown full-screen on a smartphone. 

Use natural indoor lightning and a plain background. Avoid bright light behind
the marker, reflections and other red objects. Keep the marker facing
the webcam and fully inside the image. 

## How to Test 

1. Open the project in Unity 6000.3.25f1. 
2. Open Assets/Scenes/SampleScene.unity. 
3. Press Play to start the webcam. 
4. Show the marker and move it left, right, up and down.
   The red sphere should follow it.
5. Move the marker closer to and further from the camera. 
   The sphere should also move in depth. 
6. Check X, Y, Z and the red pixel count in the bottom-left panel. 
7. Select Quad and change Smoothing Time in the Inspector. 

## Implementation 

The webcam image is shown on the Quad using WebCamTexture.
The script finds red pixels and uses their average position
to find the centre of the marker. 

The marker's centre is converted to X and Y values around -1 to 1.
The red pixel count is used to estimate depth: 

Z = sqrt(referenceRedPixels / RedPixelCount)

When the marker gets closer, Z decreases. When it moves further away,
Z increases. 

These values control the sphere's position. 
Vector3.SmoothDamp makes its movement smoother. 
The sphere's movement is updated every game frame. 
When the marker is lost, the sphere stays in place. 

The bottom-left panel shows X, Y, Z before smoothing
and the red pixel count. A green cross shows the detected centre of the marker. 

## Calibration Settings

Select Quad in the Hierarchy. Find these settings in the
Circle Detection component in the Inspector.

| Parameter | Default |
| --- | --- |
| redThreshold | 120 |
| redDominance | 1.5 |
| minimumRedPixels | 50 |
| referenceRedPixels | 2500 |
| movementScale | 3 |
| depthScale | 2 |
| mirrorX | false |
| smoothingTime | 0.2 |

Adjust the red detection settings for your lighting.

To set the depth reference, hold the marker at a comfortable distance.
Read the stable red pixel count in the panel.
Set referenceRedPixels to this number. Z should then be about 1.

Settings changed during Play Mode usually reset when you stop.
To keep your settings, enter them again outside Play Mode
and save the scene. 
