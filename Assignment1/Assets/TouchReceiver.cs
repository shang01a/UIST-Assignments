
using UnityEngine;
using System.Collections;
using TUIO;
using System.Collections.Generic;
using Assets;

public partial class TouchReceiver : MonoBehaviour
{
    [SerializeField]
    Mesh debugMesh;
    [SerializeField]
    Material startMat;
    [SerializeField]
    Material currentMat;
    [SerializeField]
    GestureState currentState = GestureState.None;

    public TouchPoint FirstTouchPoint { get; set; }
    public TouchPoint SecondTouchPoint { get; set; }

    Vector3 startTouch1;
    Vector3 startTouch2;
    Vector3 startPosition;
    Vector3 startScale;
    Quaternion startRotation;

    Vector3 touchOffset;

    int previousTouchCount = 0;
    int activeFinger = 0;

    void Update()
    {
        DrawDebugMeshes();
        HandleTouchInput();
    }

    void HandleTouchInput()
    {
        bool firstActive = FirstTouchPoint != null &&
                           FirstTouchPoint.Active;

        bool secondActive = SecondTouchPoint != null &&
                            SecondTouchPoint.Active;

        int touchCount = 0;

        if (firstActive) touchCount++;
        if (secondActive) touchCount++;

        if (touchCount == 0)
        {
            previousTouchCount = 0;
            activeFinger = 0;
            return;
        }

        int finger = firstActive ? 1 : 2;

        // Start again when the number of fingers changes
        // or when the remaining finger changes.
        if (touchCount != previousTouchCount ||
            (touchCount == 1 && finger != activeFinger))
        {
            StartGesture(firstActive, secondActive);
        }

        if (touchCount == 1)
        {
            Vector3 touch = firstActive
                ? FirstTouchPoint.fromTUIO()
                : SecondTouchPoint.fromTUIO();

            HandleSingleTouch(touch);
        }
        else
        {
            HandleTwoTouches();
        }

        previousTouchCount = touchCount;
        activeFinger = touchCount == 1 ? finger : 0;
    }

    void StartGesture(bool firstActive, bool secondActive)
    {
        startPosition = transform.position;
        startScale = transform.localScale;
        startRotation = transform.rotation;

        if (firstActive)
            startTouch1 = FirstTouchPoint.fromTUIO();

        if (secondActive)
            startTouch2 = SecondTouchPoint.fromTUIO();

        if (firstActive && !secondActive)
            touchOffset = startPosition - startTouch1;

        if (!firstActive && secondActive)
            touchOffset = startPosition - startTouch2;
    }

    void HandleSingleTouch(Vector3 touch)
    {
        transform.position = touch + touchOffset;
    }

    void HandleTwoTouches()
    {
        Vector3 touch1 = FirstTouchPoint.fromTUIO();
        Vector3 touch2 = SecondTouchPoint.fromTUIO();

        Vector3 startVector = startTouch2 - startTouch1;
        Vector3 currentVector = touch2 - touch1;

        float initialDistance = startVector.magnitude;
        float currentDistance = currentVector.magnitude;

        if (initialDistance < 0.0001f)
            return;

        // Scale according to the distance between fingers
        float scaleFactor = currentDistance / initialDistance;
        transform.localScale = startScale * scaleFactor;

        // Rotate according to the angle between fingers
        float angle = Vector3.SignedAngle(
            startVector, currentVector, Vector3.forward
        );

        Quaternion rotationChange =
            Quaternion.AngleAxis(angle, Vector3.forward);

        transform.rotation = rotationChange * startRotation;

        // Move the picture while keeping the fingers anchored
        Vector3 startMiddle = (startTouch1 + startTouch2) / 2f;
        Vector3 currentMiddle = (touch1 + touch2) / 2f;

        Vector3 offset = startPosition - startMiddle;

        transform.position = currentMiddle +
                             rotationChange * (offset * scaleFactor);
    }

    // Original TUIO event handling
    void processEvents(ArrayList events)
    {
        foreach (BBCursorEvent cursorEvent in events)
        {
            TuioCursor myCursor = cursorEvent.cursor;

            if (myCursor.getCursorID() == 0)
            {
                FirstTouchPoint = new TouchPoint(myCursor);
            }

            if (myCursor.getCursorID() == 1)
            {
                SecondTouchPoint = new TouchPoint(myCursor);
            }
        }
    }

    void DrawDebugMeshes()
    {
        DrawDebugMeshForTouchPoint(FirstTouchPoint);
        DrawDebugMeshForTouchPoint(SecondTouchPoint);
    }

    private void DrawDebugMeshForTouchPoint(TouchPoint touchPoint)
    {
        if (touchPoint != null && touchPoint.Active)
        {
            DrawDebugMeshFor(touchPoint.fromTUIO());
        }
    }

    private void DrawDebugMeshFor(Vector3 touchPointVector)
    {
        Graphics.DrawMesh(
            debugMesh,
            Matrix4x4.TRS(
                touchPointVector,
                Quaternion.identity,
                new Vector3(0.05f, 0.05f, 0.05f)
            ),
            currentMat,
            0
        );
    }
}
