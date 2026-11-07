Assignment 2: 3D Tracking

Setup
We used the red circle png on a smartphone screen with natural indoor lighting and a white wall as the background. Other red objects should be avoided as they might interfere with the detection.

To test the project:
1. Open the Unity project and load the sample scene.
2. Press Play to start the webcam.
3. Hold the red circle in front of the webcam.
4. Move it left/right, up/down and closer/further away to test the tracking.

Implementation
The webcam feed is displayed on the Quad using WebCamTexture. In CircleDetection.cs, red pixels are detected based on their RGB values. Their average position is used to calculate the centroid.

TODO (Person 2): Add a short explanation of coordinate mapping, depth estimation and sphere movement.
TODO (Person 3): Add a short explanation of smoothing and the Debug HUD.

Calibration Settings
These values can be adjusted in the Unity Inspector:
- Parameter: Default
- redThreshold: 120
- redDominance: 1.5
- minimumRedPixels: 50

These settings worked for our testing setup but might need adjusting depending on the lighting.

TODO before submission: Add any further calibration parameters, recommended camera distance and additional setup instructions needed for the finished project.
