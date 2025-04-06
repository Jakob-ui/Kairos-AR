using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using UnityEngine.UI;

public class GeospatialObjectPlacer : MonoBehaviour
{
    public List<GameObject> objectsToPlace; // Liste der zu platzierenden Objekte
    public List<Vector3> objectPositions; // Liste der Positionen (Latitude, Longitude, Altitude)
    public Text positionsText; // UI-Textfeld für die Ausgabe
    public Text errorLogText; // UI-Textfeld für Fehler-Logs
    public ARAnchorManager anchorManager; // ARAnchorManager
    public AREarthManager earthManager; // AREarthManager

    private List<ARGeospatialAnchor> placedAnchors = new List<ARGeospatialAnchor>();

    private bool isEarthStateReady = false; // Status, ob EarthState aktiviert ist

    void Start()
    {
        LogError("Starting GeospatialObjectPlacer...");
        StartCoroutine(DelayedStart());
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

            placedObject.transform.localPosition = Vector3.zero;
            placedObject.transform.localRotation = Quaternion.identity;

            // Store the anchor
            placedAnchors.Add(anchor);
            LogError($"Object {i + 1} placed at: Lat={position.x}, Lon={position.y}, Alt={position.z}");
        }
    }

    public void OnGetPlacedObjectPositionsButtonClick()
    {
        if (placedAnchors.Count == 0)
        {
            LogError("No objects have been placed yet.");
            return;
        }

        // Hole die aktuelle Position des Geräts
        var cameraPosition = Camera.main.transform.position;

        // Finde das nächstgelegene Objekt
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
            // Erstelle einen Pose aus der Position und Rotation des Ankers
            Pose anchorPose = new Pose(closestAnchor.transform.position, closestAnchor.transform.rotation);

            // Abrufen der geographischen Koordinaten
            var geospatialPose = earthManager.Convert(anchorPose);

            // Ausgabe der Koordinaten des nächstgelegenen Objekts
            positionsText.text = "Closest object:\n";
            positionsText.text += $"Latitude: {geospatialPose.Latitude}\n";
            positionsText.text += $"Longitude: {geospatialPose.Longitude}\n";
            positionsText.text += $"Altitude: {geospatialPose.Altitude}\n";
            positionsText.text += $"Distance: {minDistance:F2} meters\n";

            Debug.Log($"Closest object: Latitude={geospatialPose.Latitude}, Longitude={geospatialPose.Longitude}, Altitude={geospatialPose.Altitude}, Distance={minDistance:F2} meters");
        }
        else
        {
            positionsText.text = "No closest object found.";
            Debug.LogError("No closest object found.");
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