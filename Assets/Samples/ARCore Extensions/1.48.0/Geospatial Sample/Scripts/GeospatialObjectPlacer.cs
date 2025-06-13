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
using TMPro;

public class GeospatialObjectPlacer : MonoBehaviour
{
    public List<GameObject> objectsToPlace;
    public List<Vector3> objectPositions;
    public List<Quaternion> objectRotations = new List<Quaternion>();
    public Text positionsText;
    public Text samplingTimerText;
    private float samplingElapsedTime = 0f;
    public Text errorLogText;
    public ARAnchorManager anchorManager;
    public AREarthManager earthManager;
    private Dictionary<ARGeospatialAnchor, Vector3> originalAnchorPositions = new Dictionary<ARGeospatialAnchor, Vector3>();
    private Dictionary<ARGeospatialAnchor, Quaternion> originalAnchorRotations = new Dictionary<ARGeospatialAnchor, Quaternion>();
    private FirebaseFirestore db;
    public StartMenu StartMenu;
    private List<ARGeospatialAnchor> placedAnchors = new List<ARGeospatialAnchor>();
    private bool isEarthStateReady = false;
    string databaseSaving;
    string selectedDatabase = "ar-pictures-Wien";

    // ...existing code...
    // ...existing code...
    public class PositionSample
    {
        public string objectName;
        public Vector3 originalPosition;
        public Vector3 currentPosition;
        public float positionAcc;
        public float rotationAcc;
        public Quaternion originalRotation;
        public Quaternion currentRotation;
        public DateTime timestamp;

        // Kamera-Position und -Rotation
        public Vector3 cameraPosition;
        public Quaternion cameraRotation;

    }

    private List<PositionSample> positionSamples = new List<PositionSample>();
    private Coroutine samplingCoroutine;
    public float samplingInterval = 1f;

    public Button samplingButton;
    public Image samplingButtonImage;
    public TMP_Text samplingButtonText;
    private bool isSampling = false;


    void Start()
    {
        Debug.Log("Starting GeospatialObjectPlacer...");
        Debug.Log($"Starting GeospatialObjectPlacer on platform: {Application.platform}");
        ErrorLogger.LogToErrorText("Starting GeospatialObjectPlacer...", "success", errorLogText);
        StartMenu.GetSelectedDatabase(selectedDatabase);
        ErrorLogger.LogToErrorText($"Selected Database: {selectedDatabase}", "black", errorLogText);
        ErrorLogger.LogToErrorText($"Starting GeospatialObjectPlacer on platform: {Application.platform}", "black", errorLogText);

        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            Debug.Log("Running on iOS.");
            ErrorLogger.LogToErrorText("Running on iOS.", "success", errorLogText);
        }
        else if (Application.platform == RuntimePlatform.Android)
        {
            Debug.Log("Running on Android.");
            ErrorLogger.LogToErrorText("Running on Android.", "success", errorLogText);
        }
#if UNITY_ANDROID
        // Check for permissions on Android
        if (!Permission.HasUserAuthorizedPermission(Permission.Camera) ||
            !Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            Debug.Log("Requesting permissions...");
            ErrorLogger.LogToErrorText("Requesting permissions...", "black", errorLogText);
            Permission.RequestUserPermission(Permission.Camera);
            Permission.RequestUserPermission(Permission.FineLocation);
            StartCoroutine(WaitForPermissions());
            return;
        }
        databaseSaving = "android-placed-objects";
#elif UNITY_IOS
        // On iOS, permissions must be declared in Info.plist and are requested automatically
        Debug.Log("Ensure permissions are declared in Info.plist.");
        ErrorLogger.LogToErrorText("Ensure permissions are declared in Info.plist.", "black", errorLogText);
        databaseSaving = "ios-placed-objects";
#endif

        InitializeApp();
    }

    private IEnumerator WaitForPermissions()
    {
        while (!Permission.HasUserAuthorizedPermission(Permission.Camera) ||
               !Permission.HasUserAuthorizedPermission(Permission.FineLocation))
        {
            ErrorLogger.LogToErrorText("Waiting for permissions...", "black", errorLogText);
            Debug.Log("Waiting for permissions...");
            yield return null;
        }

        ErrorLogger.LogToErrorText("Permissions granted!", "success", errorLogText);
        Debug.Log("Permissions granted!");
        InitializeApp();
    }

    public void SetSelectedDatabase(string database)
    {
        selectedDatabase = database;
        ErrorLogger.LogToErrorText($"Selected Database set to: {selectedDatabase}", "black", errorLogText);
    }

    private void InitializeApp()
    {
        Debug.Log("Initializing app...");
        ErrorLogger.LogToErrorText("Initializing app...", "black", errorLogText);

        FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
        {
            if (task.Result == DependencyStatus.Available)
            {
                Debug.Log("Firebase initialized successfully!");
                ErrorLogger.LogToErrorText("Firebase initialized successfully!", "success", errorLogText);
                db = FirebaseFirestore.DefaultInstance;

                StartCoroutine(WaitForARCoreInitialization());
            }
            else
            {
                Debug.LogError($"Could not resolve all Firebase dependencies: {task.Result}");
                ErrorLogger.LogToErrorText($"Could not resolve all Firebase dependencies: {task.Result}", "error", errorLogText);
            }
        });

        StartCoroutine(DelayedStart());
    }

    private IEnumerator DelayedStart()
    {
        Debug.Log("Waiting for ARCore initialization...");
        ErrorLogger.LogToErrorText("Waiting for ARCore initialization...", "black", errorLogText);

        yield return StartCoroutine(WaitForARCoreInitialization());

        earthManager = GetComponent<AREarthManager>();
        if (earthManager == null)
        {
            Debug.LogError("AREarthManager is not assigned!");
            ErrorLogger.LogToErrorText("AREarthManager is not assigned!", "error", errorLogText);
            yield break;
        }

        anchorManager = GetComponent<ARAnchorManager>();
        if (anchorManager == null)
        {
            Debug.LogError("ARAnchorManager is not assigned!");
            ErrorLogger.LogToErrorText("ARAnchorManager is not assigned!", "error", errorLogText);
            yield break;
        }

        if (positionsText == null)
        {
            positionsText = GameObject.Find("Position Feedback")?.GetComponent<Text>();
            if (positionsText == null)
            {
                Debug.LogError("Text element 'Position Feedback' not found!");
                ErrorLogger.LogToErrorText("Text element 'Position Feedback' not found!", "warning", errorLogText);
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
        ErrorLogger.LogToErrorText("Starting CheckEarthState...", "black", errorLogText);
        StartCoroutine(CheckEarthState());
    }

    private System.Collections.IEnumerator CheckEarthState()
    {
        Debug.Log("Checking EarthState...");
        ErrorLogger.LogToErrorText("Checking EarthState...", "black", errorLogText);
        while (earthManager.EarthState != EarthState.Enabled)
        {
            Debug.Log("EarthState is not yet enabled. Waiting...");
            ErrorLogger.LogToErrorText("EarthState is not yet enabled. Waiting...", "warning", errorLogText);
            yield return new WaitForSeconds(1.0f); // Wait 1 second and check again
        }

        Debug.Log("EarthState is enabled!");
        ErrorLogger.LogToErrorText("EarthState is enabled!", "success", errorLogText);
        isEarthStateReady = true;

        // Place all objects once EarthState is ready
        PlaceObjects();
    }

    void PlaceObjects()
    {
        if (!isEarthStateReady)
        {
            Debug.LogError("EarthState is not ready. Aborting.");
            ErrorLogger.LogToErrorText("EarthState is not ready. Aborting.", "error", errorLogText);
            return;
        }

        for (int i = 0; i < objectsToPlace.Count; i++)
        {
            GameObject objectToPlace = objectsToPlace[i];
            Vector3 position = objectPositions[i];
            Quaternion rotation = objectRotations[i];


            Debug.Log($"Placing object {i + 1} at Lat={position.x}, Lon={position.y}, Alt={position.z}...");
            ErrorLogger.LogToErrorText($"Placing object {i + 1} at Lat={position.x}, Lon={position.y}, Alt={position.z}...", "success", errorLogText);

            var anchor = anchorManager.AddAnchor(position.x, position.y, position.z, rotation) as ARGeospatialAnchor;
            if (anchor == null)
            {
                Debug.LogError($"Failed to create anchor for object {i + 1}. Check coordinates.");
                ErrorLogger.LogToErrorText($"Failed to create anchor for object {i + 1}. Check coordinates.", "error", errorLogText);
                continue;
            }

            Debug.Log($"Anchor created for object {i + 1} at position: {anchor.transform.position}");
            ErrorLogger.LogToErrorText($"Anchor created for object {i + 1}", "success", errorLogText);
            if (objectToPlace == null)
            {
                Debug.LogError($"Prefab for object {i + 1} is not assigned.");
                ErrorLogger.LogToErrorText($"Prefab for object {i + 1} is not assigned.", "error", errorLogText);
                continue;
            }

            var placedObject = Instantiate(objectToPlace, anchor.transform);
            if (placedObject == null)
            {
                Debug.LogError($"Failed to instantiate object {i + 1}.");
                ErrorLogger.LogToErrorText($"Failed to instantiate object {i + 1}.", "error", errorLogText);
                continue;
            }

            placedObject.transform.localRotation = Quaternion.identity;
            placedObject.transform.localPosition = Vector3.zero;

            placedAnchors.Add(anchor);

            originalAnchorPositions[anchor] = position;

            // Extrahiere die GeospatialPose und speichere die EunRotation
            Pose anchorPose = new Pose(anchor.transform.position, anchor.transform.rotation);
            var geospatialPose = earthManager.Convert(anchorPose);
            Quaternion eunRotation = geospatialPose.EunRotation;

            if (!originalAnchorRotations.ContainsKey(anchor))
            {
                originalAnchorRotations[anchor] = objectRotations[i];
            }
            objectRotations[i] = eunRotation; // Aktualisiere die Rotation in der Liste
            Debug.Log($"Saved original rotation for anchor: {eunRotation.eulerAngles}");

            Debug.Log($"Object {i + 1} placed at: Lat={position.x}, Lon={position.y}, Alt={position.z}");
            Debug.Log($"Extracted EunRotation: {eunRotation.eulerAngles}");
            ErrorLogger.LogToErrorText($"Object {i + 1} placed at: Lat={position.x}, Lon={position.y}, Alt={position.z}", "success", errorLogText);
            ErrorLogger.LogToErrorText($"Object {i + 1} Extracted EunRotation: {eunRotation.eulerAngles}", "success", errorLogText);
        }
    }

    public void ResetPlacedObjects()
    {
        Debug.Log("Reset button clicked. Restarting the script...");
        ErrorLogger.LogToErrorText("Reset button clicked. Restarting the script...", "black", errorLogText);
        ErrorLogger.LogToErrorText($"Selected Database: {selectedDatabase}", "black", errorLogText);
        samplingTimerText.text = $"Laufzeit: Start reading Position";

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
        ErrorLogger.LogToErrorText("Script has been restarted.", "success", errorLogText);
    }

    private IEnumerator WaitForARCoreInitialization()
    {
        while (earthManager.EarthState != EarthState.Enabled)
        {
            Debug.Log("Waiting for ARCore to initialize...");
            ErrorLogger.LogToErrorText("Waiting for ARCore to initialize...", "warning", errorLogText);
            yield return new WaitForSeconds(1.0f);
        }

        Debug.Log("ARCore initialized successfully!");
        ErrorLogger.LogToErrorText("ARCore initialized successfully!", "success", errorLogText);
        isEarthStateReady = true;
    }

    //Hilfsfunktionen für OnGetPlacedObjectPositionsButtonClick
    public void StartSampling()
    {
        if (samplingCoroutine == null)
        {
            samplingElapsedTime = 0f;
            UpdateSamplingTimerText();
            samplingCoroutine = StartCoroutine(SamplePositionsCoroutine());
        }
    }

    public void StopSamplingAndSendBatch()
    {
        if (samplingCoroutine != null)
        {
            StopCoroutine(samplingCoroutine);
            samplingCoroutine = null;
            UpdateSamplingTimerText();
            SendToFirestore();
        }
    }

    private IEnumerator SamplePositionsCoroutine()
    {
        while (true)
        {
            SaveCurrentPositionToBatch();
            samplingElapsedTime += samplingInterval;
            UpdateSamplingTimerText();
            yield return new WaitForSeconds(samplingInterval);
        }
    }
    private void UpdateSamplingTimerText()
    {
        if (samplingTimerText != null)
        {
            TimeSpan t = TimeSpan.FromSeconds(samplingElapsedTime);
            samplingTimerText.text = $"Laufzeit: {t:mm\\:ss}";
        }
    }

    //Button Controll
    public void OnSamplingButtonClick()
    {
        if (!isSampling)
        {
            StartSampling();
            isSampling = true;
            samplingButtonText.text = "Stopp";
            samplingButtonImage.color = Color.red;
        }
        else
        {
            StopSamplingAndSendBatch();
            isSampling = false;
            samplingButtonText.text = "Start reading";
            samplingButtonImage.color = Color.blue;
        }
    }

    //save function
    private void SaveCurrentPositionToBatch()
    {
        if (placedAnchors.Count == 0)
        {
            Debug.LogError("No objects have been placed yet.");
            ErrorLogger.LogToErrorText("No objects have been placed yet.", "warning", errorLogText);
            return;
        }
        if (Camera.main == null)
        {
            Debug.LogError("Main camera is not found!");
            ErrorLogger.LogToErrorText("Main camera is not found!", "error", errorLogText);
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
                Quaternion currentRotation = geospatialPose.EunRotation;

                // Calculate the difference in accuracy
                float positionAccuracy = Vector3.Distance(originalPosition, new Vector3((float)geospatialPose.Latitude, (float)geospatialPose.Longitude, (float)geospatialPose.Altitude));
                float rotationAccuracy = Quaternion.Angle(originalRotation, currentRotation);

                Vector3 camPos = Camera.main.transform.position;
                Quaternion camRot = Camera.main.transform.rotation;

                /*positionSamples.Add(new PositionSample
                {
                    objectName = prefabName,
                    originalPosition = originalPosition,
                    currentPosition = new Vector3((float)geospatialPose.Latitude, (float)geospatialPose.Altitude, (float)geospatialPose.Longitude),
                    positionAcc = positionAccuracy,
                    rotationAcc = rotationAccuracy,
                    originalRotation = originalRotation,
                    currentRotation = currentRotation,
                    timestamp = DateTime.UtcNow,
                    cameraPosition = camPos,
                    cameraRotation = camRot
                });*/
                Vector3 currentPosition = new Vector3((float)geospatialPose.Latitude, (float)geospatialPose.Altitude, (float)geospatialPose.Longitude);

                SaveObjectPosition(
                    prefabName,
                    originalPosition,
                    currentPosition,
                    positionAccuracy,
                    rotationAccuracy,
                    originalRotation,
                    currentRotation,
                    camPos,
                    camRot
                );

                positionsText.text = $"Closest object: {prefabName}\n";
                positionsText.text += $"Original Position: Lat={originalPosition.x}, Lon={originalPosition.y}, Alt={originalPosition.z}\n";
                positionsText.text += $"Current Position: Lat={geospatialPose.Latitude}, Lon={geospatialPose.Longitude}, Alt={geospatialPose.Altitude}\n";
                positionsText.text += $"Original Rotation (Eun): {originalRotation.eulerAngles}\n";
                positionsText.text += $"Current Rotation (Eun): {currentRotation.eulerAngles}\n";
                positionsText.text += $"Accuracy Difference: {positionAccuracy:F2} meters\n";
                positionsText.text += $"Rotation Difference: {rotationAccuracy:F2} degrees\n";
            }
            else
            {
                Debug.LogError("Original position or rotation of the closest anchor not found.");
                ErrorLogger.LogToErrorText("Original position or rotation of the closest anchor not found.", "error", errorLogText);
            }
        }
    }

    private void SendToFirestore()
    {
        foreach (var sample in positionSamples)
        {
            // Erzeuge einen eindeutigen Namen für jedes Sample
            string uniqueName = $"{sample.objectName}_{sample.timestamp:yyyyMMdd_HHmmss_fff}";
            SaveObjectPosition(
            uniqueName,
            sample.originalPosition,
            sample.currentPosition,
            sample.positionAcc,
            sample.rotationAcc,
            sample.originalRotation,
            sample.currentRotation,
            sample.cameraPosition,
            sample.cameraRotation
        );
        }
        positionSamples.Clear();
    }

    public void SaveObjectPosition(string objectName, Vector3 originalPosition, Vector3 currentPosition, float positionAcc, float rotationAcc, Quaternion originalRotation, Quaternion currentRotation, Vector3 cameraPosition, Quaternion cameraRotation)
    {
        if (db == null)
        {
            Debug.LogError("Firestore is not initialized yet.");
            ErrorLogger.LogToErrorText("Firestore is not initialized yet.", "warning", errorLogText);
            return;
        }

        var cameraGeoPose = earthManager.CameraGeospatialPose;

        // Reference to the Firestore collection
        var docRef = db.Collection(databaseSaving).Document(objectName);

        // Data to save
        Dictionary<string, object> data = new Dictionary<string, object>
        {
            { "name", objectName },
            { "timestamp", FieldValue.ServerTimestamp },
            { "original-latitude", originalPosition.x },
            { "original-longitude", originalPosition.y },
            { "original-altitude", originalPosition.z },
            { "current-latitude", currentPosition.x },
            { "current-longitude", currentPosition.z },
            { "current-altitude", currentPosition.y },
            { "position-accuracy", positionAcc },
            { "rotation-accuracy", rotationAcc },
            { "original-rotation-x", originalRotation.eulerAngles.x },
            { "original-rotation-y", originalRotation.eulerAngles.y },
            { "original-rotation-z", originalRotation.eulerAngles.z },
            { "current-rotation-x", currentRotation.eulerAngles.x },
            { "current-rotation-y", currentRotation.eulerAngles.y },
            { "current-rotation-z", currentRotation.eulerAngles.z },
            //Kamera stuff
            { "camera-position-x", cameraPosition.x },
        { "camera-position-y", cameraPosition.y },
        { "camera-position-z", cameraPosition.z },
        { "camera-rotation-x", cameraRotation.eulerAngles.x },
        { "camera-rotation-y", cameraRotation.eulerAngles.y },
        { "camera-rotation-z", cameraRotation.eulerAngles.z },
            //Geolocation of the camera
         { "camera-geo-latitude", cameraGeoPose.Latitude },
        { "camera-geo-longitude", cameraGeoPose.Longitude },
        { "camera-geo-altitude", cameraGeoPose.Altitude }
        };

        docRef.SetAsync(data).ContinueWithOnMainThread(task =>
        {
            if (task.IsCompleted)
            {
                Debug.Log($"Object '{objectName}' position and rotation saved successfully!");
                ErrorLogger.LogToErrorText($"Object '{objectName}' position and rotation saved successfully!", "success", errorLogText);
            }
            else
            {
                Debug.LogError($"Failed to save object '{objectName}' position and rotation: {task.Exception}");
                ErrorLogger.LogToErrorText($"Failed to save object '{objectName}' position and rotation: {task.Exception}", "error", errorLogText);
            }
        });
    }
}