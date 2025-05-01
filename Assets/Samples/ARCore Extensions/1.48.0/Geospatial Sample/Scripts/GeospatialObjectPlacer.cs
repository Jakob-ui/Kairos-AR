using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using UnityEngine.UI;
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;
using System;
using UnityEngine.Android;
using System.Collections;

public class GeospatialObjectPlacer : MonoBehaviour
{
    public List<GameObject> objectsToPlace; // List of objects to place
    public List<Vector3> objectPositions; // List of positions (Latitude, Longitude, Altitude)
    public Text positionsText; // UI text for position feedback
    public Text errorLogText; // UI text for error logs
    public ARAnchorManager anchorManager; // ARAnchorManager
    public AREarthManager earthManager; // AREarthManager
    private Dictionary<ARGeospatialAnchor, Vector3> originalAnchorPositions = new Dictionary<ARGeospatialAnchor, Vector3>();
    private Dictionary<ARGeospatialAnchor, Quaternion> originalAnchorRotations = new Dictionary<ARGeospatialAnchor, Quaternion>();
    private FirebaseFirestore db; // Firestore instance
    private List<ARGeospatialAnchor> placedAnchors = new List<ARGeospatialAnchor>();
    private bool isEarthStateReady = false; // Status of EarthState readiness

    void Start()
    {
        Debug.Log("Starting GeospatialObjectPlacer...");
        Debug.Log($"Starting GeospatialObjectPlacer on platform: {Application.platform}");
        LogToErrorText("Starting GeospatialObjectPlacer...", "success");
        LogToErrorText($"Starting GeospatialObjectPlacer on platform: {Application.platform}", "black");

        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            Debug.Log("Running on iOS.");
        }
        else if (Application.platform == RuntimePlatform.Android)
        {
            Debug.Log("Running on Android.");
        }
#if UNITY_ANDROID
        // Check for permissions on Android
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera) ||
            !Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Debug.Log("Requesting permissions...");
            LogToErrorText("Requesting permissions...", "black");
            Permission.RequestUserPermission(Permission.Camera);
            Permission.RequestUserPermission(Permission.FineLocation);
            StartCoroutine(WaitForPermissions());
            return;
        }
#elif UNITY_IOS
    // On iOS, permissions must be declared in Info.plist and are requested automatically
    Debug.Log("Ensure permissions are declared in Info.plist.");
    LogToErrorText("Ensure permissions are declared in Info.plist.", "black");

#endif

        InitializeApp();
    }

    private IEnumerator WaitForPermissions()
    {
        while (!Permission.HasUserAuthorizedPermission(Permission.Camera) ||
               !Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            LogToErrorText("Waiting for permissions...", "black");
            Debug.Log("Waiting for permissions...");
            yield return null;
        }

        LogToErrorText("Permissions granted!", "success");
        Debug.Log("Permissions granted!");
        InitializeApp();
    }

    private void InitializeApp()
    {
        Debug.Log("Initializing app...");
        LogToErrorText("Initializing app...", "black");

        // Initialize Firebase
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                Debug.Log("Firebase initialized successfully!");
                LogToErrorText("Firebase initialized successfully!", "success");
                db = FirebaseFirestore.DefaultInstance;

                StartCoroutine(WaitForARCoreInitialization());
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {task.Result}");
                LogToErrorText($"Could not resolve all Firebase dependencies: {task.Result}", "error");
            }
        });

        StartCoroutine(DelayedStart());
    }

    private IEnumerator DelayedStart()
    {
        Debug.Log("Waiting for ARCore initialization...");
        LogToErrorText("Waiting for ARCore initialization...", "black");

        // Warte auf die ARCore-Initialisierung
        yield return StartCoroutine(WaitForARCoreInitialization());

        // Initialisiere Komponenten
        earthManager = GetComponent<AREarthManager>();
        if (earthManager == null)
        {
            Debug.LogError("AREarthManager is not assigned!");
            LogToErrorText("AREarthManager is not assigned!", "error");
            yield break;
        }

        anchorManager = GetComponent<ARAnchorManager>();
        if (anchorManager == null)
        {
            Debug.LogError("ARAnchorManager is not assigned!");
            LogToErrorText("ARAnchorManager is not assigned!", "error");
            yield break;
        }

        if (positionsText == null)
        {
            positionsText = GameObject.Find("Position Feedback")?.GetComponent<Text>();
            if (positionsText == null)
            {
                Debug.LogError("Text element 'Position Feedback' not found!");
                LogToErrorText("Text element 'Position Feedback' not found!", "warning");
            }
        }

        if (errorLogText == null)
        {
            errorLogText = GameObject.Find("Error Log")?.GetComponent<Text>();
            if (errorLogText == null)
            {
                Debug.LogError("Text element 'Error Log' not found!");
            }
        }

        Debug.Log("Starting CheckEarthState...");
        LogToErrorText("Starting CheckEarthState...", "black");
        StartCoroutine(CheckEarthState());
    }

    private System.Collections.IEnumerator CheckEarthState()
    {
        Debug.Log("Checking EarthState...");
        LogToErrorText("Checking EarthState...", "black");
        while (earthManager.EarthState != EarthState.Enabled)
        {
            Debug.Log("EarthState is not yet enabled. Waiting...");
            LogToErrorText("EarthState is not yet enabled. Waiting...", "warning");
            yield return new WaitForSeconds(1.0f); // Wait 1 second and check again
        }

        Debug.Log("EarthState is enabled!");
        LogToErrorText("EarthState is enabled!", "success");
        isEarthStateReady = true;

        // Place all objects once EarthState is ready
        PlaceObjects();
    }

    void PlaceObjects()
    {
        if (!isEarthStateReady)
        {
            Debug.LogError("EarthState is not ready. Aborting.");
            LogToErrorText("EarthState is not ready. Aborting.", "error");
            return;
        }

        for (int i = 0; i < objectsToPlace.Count; i++)
        {
            GameObject objectToPlace = objectsToPlace[i];
            Vector3 position = objectPositions[i];


            Debug.Log($"Placing object {i + 1} at Lat={position.x}, Lon={position.z}, Alt={position.y}...");
            LogToErrorText($"Placing object {i + 1} at Lat={position.x}, Lon={position.z}, Alt={position.y}...", "success");
            var anchor = anchorManager.AddAnchor(position.x, position.z, position.y, Quaternion.identity) as ARGeospatialAnchor;
            if (anchor == null)
            {
                Debug.LogError($"Failed to create anchor for object {i + 1}. Check coordinates.");
                LogToErrorText($"Failed to create anchor for object {i + 1}. Check coordinates.", "error");
                continue;
            }

            Debug.Log($"Anchor created for object {i + 1} at position: {anchor.transform.position}");
            LogToErrorText($"Anchor created for object {i + 1}", "success");
            if (objectToPlace == null)
            {
                Debug.LogError($"Prefab for object {i + 1} is not assigned.");
                LogToErrorText($"Prefab for object {i + 1} is not assigned.", "error");
                continue;
            }

            var placedObject = Instantiate(objectToPlace, anchor.transform);
            if (placedObject == null)
            {
                Debug.LogError($"Failed to instantiate object {i + 1}.");
                LogToErrorText($"Failed to instantiate object {i + 1}.", "error");
                continue;
            }

            placedObject.transform.localRotation = Quaternion.identity;
            placedObject.transform.localPosition = Vector3.zero;

            // Store the anchor
            placedAnchors.Add(anchor);

            // Store the original position and rotation of the anchor
            originalAnchorPositions[anchor] = position;
            originalAnchorRotations[anchor] = placedObject.transform.localRotation;

            Pose anchorPose = new Pose(anchor.transform.position, anchor.transform.rotation);
            var geospatialPose = earthManager.Convert(anchorPose);

            Debug.Log($"Object {i + 1} placed at: Lat={position.x}, Lon={position.z}, Alt={position.y}");
            Debug.Log($"GeospatialPose.EunRotation: {geospatialPose.EunRotation}");
            LogToErrorText($"Object {i + 1} placed with GeospatialPose.EunRotation: {geospatialPose.EunRotation}", "success");
        }
    }

    public void ResetPlacedObjects()
    {
        Debug.Log("Reset button clicked. Restarting the script...");
        LogToErrorText("Reset button clicked. Restarting the script...", "black");

        // Remove all placed anchors
        foreach (var anchor in placedAnchors)
        {
            Destroy(anchor.gameObject);
        }

        // Clear the list of placed anchors
        placedAnchors.Clear();

        // Clear the dictionary of original anchor positions
        originalAnchorPositions.Clear();

        // Reset the UI
        if (positionsText != null)
        {
            positionsText.text = "No objects placed.";
        }

        if (errorLogText != null)
        {
            errorLogText.text = "Logs cleared.";
        }

        // Reset internal state
        isEarthStateReady = false;

        // Stop any running coroutines and restart the script
        StopAllCoroutines();
        StartCoroutine(DelayedStart());

        Debug.Log("Script has been restarted.");
        LogToErrorText("Script has been restarted.", "success");
    }

    private IEnumerator WaitForARCoreInitialization()
    {
        while (earthManager.EarthState != EarthState.Enabled)
        {
            Debug.Log("Waiting for ARCore to initialize...");
            LogToErrorText("Waiting for ARCore to initialize...", "warning");
            yield return new WaitForSeconds(1.0f);
        }

        Debug.Log("ARCore initialized successfully!");
        LogToErrorText("ARCore initialized successfully!", "success");
        isEarthStateReady = true;
    }

    public void OnGetPlacedObjectPositionsButtonClick()
    {
        if (placedAnchors.Count == 0)
        {
            Debug.LogError("No objects have been placed yet.");
            LogToErrorText("No objects have been placed yet.", "warning");
            return;
        }
        if (Camera.main == null)
        {
            Debug.LogError("Main camera is not found!");
            LogToErrorText("Main camera is not found!", "error");
            return;
        }
        // Get the current position of the device
        var cameraPosition = Camera.main.transform.position;

        // Find the nearest anchor
        float minDistance = float.MaxValue;
        ARGeospatialAnchor closestAnchor = null;

        foreach (var anchor in placedAnchors)
        {
            float distance = Vector3.Distance(cameraPosition, anchor.transform.position);
            if (distance < minDistance)
            {
                minDistance = distance;
                closestAnchor = anchor;
            }
        }

        if (closestAnchor != null)
        {
            // Get the current geospatial position of the anchor
            Pose anchorPose = new Pose(closestAnchor.transform.position, closestAnchor.transform.rotation);
            var geospatialPose = earthManager.Convert(anchorPose);

            // Get the original position and rotation of the anchor
            if (originalAnchorPositions.TryGetValue(closestAnchor, out Vector3 originalPosition) &&
                originalAnchorRotations.TryGetValue(closestAnchor, out Quaternion originalRotation))
            {
                // Get the prefab name
                string prefabName = closestAnchor.transform.GetChild(0).gameObject.name + "_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss");

                // Get the current rotation of the placed object
                Quaternion currentRotation = geospatialPose.EunRotation;

                // Calculate the difference in accuracy
                float positionAccuracy = Vector3.Distance(originalPosition, new Vector3((float)geospatialPose.Latitude, (float)geospatialPose.Altitude, (float)geospatialPose.Longitude));

                float rotationAccuracy = Quaternion.Angle(originalRotation, currentRotation);
                positionsText.text += $"Rotation Difference: {rotationAccuracy:F2} degrees\n";

                // Display the original and current positions, rotations, and the accuracy difference
                positionsText.text = $"Closest object: {prefabName}\n";
                positionsText.text += $"Original Position: Lat={originalPosition.x}, Lon={originalPosition.z}, Alt={originalPosition.y}\n";
                positionsText.text += $"Current Position: Lat={geospatialPose.Latitude}, Lon={geospatialPose.Longitude}, Alt={geospatialPose.Altitude}\n";
                positionsText.text += $"Original Rotation: {originalRotation.eulerAngles}\n";
                positionsText.text += $"Current Rotation: {currentRotation.eulerAngles}\n";
                positionsText.text += $"Accuracy Difference: {positionAccuracy:F2} meters\n";

                // Save both original and current positions and rotations to Firestore
                SaveObjectPosition(prefabName, originalPosition, new Vector3((float)geospatialPose.Latitude, (float)geospatialPose.Altitude, (float)geospatialPose.Longitude), positionAccuracy, rotationAccuracy, originalRotation, currentRotation);
            }
            else
            {
                Debug.LogError("Original position or rotation of the closest anchor not found.");
                LogToErrorText("Original position or rotation of the closest anchor not found.", "error");
            }
        }
    }

    public void SaveObjectPosition(string objectName, Vector3 originalPosition, Vector3 currentPosition, float positionAcc, float rotationAcc, Quaternion originalRotation, Quaternion currentRotation)
    {
        if (db == null)
        {
            Debug.LogError("Firestore is not initialized yet.");
            LogToErrorText("Firestore is not initialized yet.", "warning");
            return;
        }

        // Reference to the Firestore collection
        var docRef = db.Collection("android-placed-objects").Document(objectName);

        // Data to save
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "name", objectName },
            { "timestamp", FieldValue.ServerTimestamp },
            { "original-latitude", originalPosition.x },
            { "original-longitude", originalPosition.z },
            { "original-altitude", originalPosition.y },
            { "current-latitude", currentPosition.x },
            { "current-longitude", currentPosition.z },
            { "current-altitude", currentPosition.y },
            { "accuracy", positionAcc },
            { "original-rotation-x", originalRotation.eulerAngles.x },
            { "original-rotation-y", originalRotation.eulerAngles.y },
            { "original-rotation-z", originalRotation.eulerAngles.z },
            { "current-rotation-x", currentRotation.eulerAngles.x },
            { "current-rotation-y", currentRotation.eulerAngles.y },
            { "current-rotation-z", currentRotation.eulerAngles.z }
        };

        docRef.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log($"Object '{objectName}' position and rotation saved successfully!");
                LogToErrorText($"Object '{objectName}' position and rotation saved successfully!", "success");
            }
            else
            {
                Debug.LogError($"Failed to save object '{objectName}' position and rotation: {task.Exception}");
                LogToErrorText($"Failed to save object '{objectName}' position and rotation: {task.Exception}", "error");
            }
        });
    }
    private void LogToErrorText(string message, string severity)
    {
        Debug.Log(message);

        if (errorLogText != null)
        {
            string color = "black"; // Standardfarbe

            // Farbe basierend auf der Schwere festlegen
            switch (severity.ToLower())
            {
                case "black":
                    color = "black"; // Weiß für normale Informationen
                    break;
                case "success":
                    color = "green"; // Weiß für normale Informationen
                    break;
                case "warning":
                    color = "yellow"; // Gelb für Warnungen
                    break;
                case "error":
                    color = "red"; // Rot für Fehler
                    break;
            }

            // Nachricht mit Farbe hinzufügen
            errorLogText.text += $"<color={color}>{message}</color>\n";
        }
    }
}