using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using UnityEngine.UI;
using Firebase;
using Firebase.Extensions;
using Firebase.Firestore;

public class GeospatialObjectPlacer : MonoBehaviour
{
    public List<GameObject> objectsToPlace; // Liste der zu platzierenden Objekte
    public List<Vector3> objectPositions; // Liste der Positionen (Latitude, Longitude, Altitude)
    public Text positionsText; // UI-Textfeld für die Ausgabe
    public Text errorLogText; // UI-Textfeld für Fehler-Logs
    public ARAnchorManager anchorManager; // ARAnchorManager
    public AREarthManager earthManager; // AREarthManager
    private Dictionary<ARGeospatialAnchor, Vector3> originalAnchorPositions = new Dictionary<ARGeospatialAnchor, Vector3>(); //store original Anchor Positions
    private Dictionary<ARGeospatialAnchor, Quaternion> originalAnchorRotations = new Dictionary<ARGeospatialAnchor, Quaternion>(); // Store original Rotation
    private FirebaseFirestore db; // Firestore-Instanz
    private List<ARGeospatialAnchor> placedAnchors = new List<ARGeospatialAnchor>();
    private bool isEarthStateReady = false; // Status, ob EarthState aktiviert ist

    void Start()
    {
        LogError("Starting GeospatialObjectPlacer...");

        // Firebase initialisieren
        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                Debug.Log("Firebase initialized successfully!");
                Debug.LogError("Firebase initialized successfully!");
                db = FirebaseFirestore.DefaultInstance;
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {task.Result}");
            }
        });

        StartCoroutine(DelayedStart());
    }

    public void SaveObjectPosition(string objectName, Vector3 originalPosition, Vector3 currentPosition, float accuracy, Quaternion originalRotation, Quaternion currentRotation)
    {
        if (db == null)
        {
            Debug.LogError("Firestore is not initialized yet.");
            return;
        }

        // Referenz auf die Collection "android-placed-objects"
        var docRef = db.Collection("android-placed-objects").Document(objectName);

        // Daten, die gespeichert werden sollen
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
        { "accuracy", accuracy },
        /*
        { "original-rotation-x", originalRotation.eulerAngles.x },
        { "original-rotation-y", originalRotation.eulerAngles.y },
        { "original-rotation-z", originalRotation.eulerAngles.z },
        { "current-rotation-x", currentRotation.eulerAngles.x },
        { "current-rotation-y", currentRotation.eulerAngles.y },
        { "current-rotation-z", currentRotation.eulerAngles.z }*/
    };

        docRef.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log($"Object '{objectName}' position and rotation saved successfully!");
            }
            else
            {
                Debug.LogError($"Failed to save object '{objectName}' position and rotation: {task.Exception}");
            }
        });
    }

    private System.Collections.IEnumerator DelayedStart()
    {
        LogError("Waiting for ARCore initialization...");
        yield return new WaitForSeconds(1.0f); // Wait 1 second for ARCore initialization

        // Initialize components
        earthManager = GetComponent<AREarthManager>();
        if (earthManager == null)
        {
            LogError("AREarthManager is not assigned!");
            yield break;
        }

        if (anchorManager == null)
        {
            LogError("ARAnchorManager is not assigned!");
            yield break;
        }

        if (positionsText == null)
        {
            positionsText = GameObject.Find("Position Feedback")?.GetComponent<Text>();
            if (positionsText == null)
            {
                LogError("Text element 'Position Feedback' not found!");
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

        LogError("Starting CheckEarthState...");
        StartCoroutine(CheckEarthState());
    }

    System.Collections.IEnumerator CheckEarthState()
    {
        LogError("Checking EarthState...");
        while (earthManager.EarthState != EarthState.Enabled)
        {
            LogError("EarthState is not yet enabled. Waiting...");
            yield return new WaitForSeconds(1.0f); // Wait 1 second and check again
        }

        LogError("EarthState is enabled!");
        isEarthStateReady = true;

        // Place all objects once EarthState is ready
        PlaceObjects();
    }

    void PlaceObjects()
    {
        if (!isEarthStateReady)
        {
            LogError("EarthState is not ready. Aborting.");
            return;
        }

        for (int i = 0; i < objectsToPlace.Count; i++)
        {
            GameObject objectToPlace = objectsToPlace[i];
            Vector3 position = objectPositions[i];

            LogError($"Placing object {i + 1} at Lat={position.x}, Lon={position.y}, Alt={position.z}...");
            var anchor = anchorManager.AddAnchor(position.x, position.y, position.z, Quaternion.identity) as ARGeospatialAnchor;
            if (anchor == null)
            {
                LogError($"Failed to create anchor for object {i + 1}. Check coordinates.");
                continue;
            }

            LogError($"Anchor created for object {i + 1} at position: {anchor.transform.position}");
            if (objectToPlace == null)
            {
                LogError($"Prefab for object {i + 1} is not assigned.");
                continue;
            }

            var placedObject = Instantiate(objectToPlace, anchor.transform);
            if (placedObject == null)
            {
                LogError($"Failed to instantiate object {i + 1}.");
                continue;
            }

            // Set the rotation of the placed object (example: 45 degrees around the Y-axis)
            placedObject.transform.localRotation = Quaternion.Euler(0, 45, 0);

            placedObject.transform.localPosition = Vector3.zero;

            // Store the anchor
            placedAnchors.Add(anchor);

            // Store the original position and rotation of the anchor
            originalAnchorPositions[anchor] = position;
            originalAnchorRotations[anchor] = placedObject.transform.localRotation;

            LogError($"Object {i + 1} placed at: Lat={position.x}, Lon={position.y}, Alt={position.z}");
        }
    }

    public void ResetPlacedObjects()
    {
        Debug.Log("Reset button clicked. Restarting the script...");

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
    }

    public void OnGetPlacedObjectPositionsButtonClick()
    {
        if (placedAnchors.Count == 0)
        {
            LogError("No objects have been placed yet.");
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
                string prefabName = closestAnchor.transform.GetChild(0).gameObject.name;

                // Get the current rotation of the placed object
                Quaternion currentRotation = closestAnchor.transform.GetChild(0).localRotation;

                // Calculate the difference in accuracy
                float accuracyDifference = Vector3.Distance(originalPosition, new Vector3((float)geospatialPose.Latitude, (float)geospatialPose.Altitude, (float)geospatialPose.Longitude));

                // Display the original and current positions, rotations, and the accuracy difference
                positionsText.text = "Closest object:\n";
                positionsText.text += $"Original Position: Lat={originalPosition.x}, Lon={originalPosition.z}, Alt={originalPosition.y}\n";
                positionsText.text += $"Current Position: Lat={geospatialPose.Latitude}, Lon={geospatialPose.Longitude}, Alt={geospatialPose.Altitude}\n";
                positionsText.text += $"Original Rotation: {originalRotation.eulerAngles}\n";
                positionsText.text += $"Current Rotation: {currentRotation.eulerAngles}\n";
                positionsText.text += $"Accuracy Difference: {accuracyDifference:F2} meters\n";

                // Save both original and current positions and rotations to Firestore
                SaveObjectPosition(prefabName, originalPosition, new Vector3((float)geospatialPose.Latitude, (float)geospatialPose.Altitude, (float)geospatialPose.Longitude), accuracyDifference, originalRotation, currentRotation);
            }
            else
            {
                LogError("Original position or rotation of the closest anchor not found.");
            }
        }
    }

    private void LogError(string message)
    {
        if (errorLogText != null)
        {
            errorLogText.text += message + "\n";
        }
        Debug.LogError(message);
    }
}