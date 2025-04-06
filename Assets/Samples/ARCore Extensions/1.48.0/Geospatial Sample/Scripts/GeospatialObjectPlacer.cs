using UnityEngine;
using Google.XR.ARCoreExtensions;
using Google.XR.ARCoreExtensions.Samples.Geospatial;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using UnityEngine.UI;
using TMPro;

public class GeospatialObjectPlacer : MonoBehaviour
{
    public GameObject objectToPlace; // Das zu platzierende Objekt (Prefab)
    public double latitude; // Geografische Breite
    public double longitude; // Geografische Länge
    public double altitude; // Höhe
    public Text positionsText; // UI-Textfeld für die Ausgabe
    public ARAnchorManager anchorManager; // ARAnchorManager
    private AREarthManager earthManager; // AREarthManager

    // Listen für die Datenerfassung
    private List<Vector3> placedObjectPositions = new List<Vector3>();
    private List<Vector3> desiredPositions = new List<Vector3>();

    void Start()
    {
        // Initialisiere den EarthManager
        earthManager = GetComponent<AREarthManager>();
        if (anchorManager == null)
        {
            Debug.LogError("ARAnchorManager ist nicht zugewiesen!");
            return;
        }

        if (earthManager == null)
        {
            Debug.LogError("AREarthManager ist nicht zugewiesen!");
            return;
        }

        if (positionsText == null)
        {
            positionsText = GameObject.Find("Position Feedback").GetComponent<Text>();
            if (positionsText == null)
            {
                Debug.LogError("Text-Element 'Feedback' konnte nicht gefunden werden!");
            }
        }

        // Platziere das Objekt beim Start
        PlaceObject();
    }

    void PlaceObject()
    {
        if (earthManager.EarthState == EarthState.Enabled)
        {
            var anchor = anchorManager.AddAnchor(latitude, longitude, altitude, Quaternion.identity);
            if (anchor != null)
            {
                // Instanziere das Objekt und setze es als Kind des Anchors
                var placedObject = Instantiate(objectToPlace, anchor.transform);
                placedObject.transform.localPosition = Vector3.zero; // Nullt die lokale Position
                placedObject.transform.localRotation = Quaternion.identity; // Nullt die lokale Rotation

                // Speichere die gewünschte und tatsächliche Position
                desiredPositions.Add(new Vector3((float)latitude, (float)altitude, (float)longitude));
                placedObjectPositions.Add(anchor.transform.position);

                Debug.Log($"Objekt platziert an: Lat={latitude}, Lon={longitude}, Alt={altitude}");
            }
            else
            {
                Debug.LogError("Fehler beim Erstellen des Geospatial Anchors.");
            }
        }
        else
        {
            Debug.LogError("Earth State ist nicht aktiviert.");
        }
    }

    // Methode, die beim Button-Klick aufgerufen wird
    public void OnGetPlacedObjectPositionsButtonClick()
    {
        if (placedObjectPositions.Count == 0)
        {
            positionsText.text = "Es wurde noch kein Objekt platziert.";
            Debug.LogError("Es wurde noch kein Objekt platziert.");
            return;
        }

        // Hole die letzte platzierte Position
        Vector3 lastPosition = placedObjectPositions[placedObjectPositions.Count - 1];
        Quaternion lastRotation = objectToPlace.transform.rotation;

        // Ausgabe der Informationen
        positionsText.text = "Letztes platziertes Objekt:\n";
        positionsText.text += $"Position (Lat, Lon, Alt): {lastPosition.x}, {lastPosition.z}, {lastPosition.y}\n";
        positionsText.text += $"Rotation: {lastRotation.eulerAngles}\n";

        Debug.Log($"Letztes Objekt: Position (Lat, Lon, Alt): {lastPosition.x}, {lastPosition.z}, {lastPosition.y}, Rotation: {lastRotation.eulerAngles}");
    }
}

/*
using UnityEngine;
using Google.XR.ARCoreExtensions;
using UnityEngine.XR.ARFoundation;
using System.Collections.Generic;
using UnityEngine.UI;

public class GeospatialObjectPlacer : MonoBehaviour
{
    public GameObject objectToPlace; // Das zu platzierende Objekt (Prefab)
    public double latitude; // Geografische Breite
    public double longitude; // Geografische Länge
    public double altitude; // Höhe
    public Text positionsText; // UI-Textfeld für die Ausgabe
    public ARAnchorManager anchorManager; // ARAnchorManager
    private AREarthManager earthManager; // AREarthManager

    private bool isEarthStateReady = false; // Status, ob EarthState aktiviert ist

    void Start()
    {
        Debug.Log("Starting GeospatialObjectPlacer...");
        StartCoroutine(DelayedStart());
    }

    private System.Collections.IEnumerator DelayedStart()
    {
        Debug.Log("Waiting for ARCore initialization...");
        yield return new WaitForSeconds(1.0f); // Wait 1 second for ARCore initialization

        // Initialize components
        earthManager = GetComponent<AREarthManager>();
        if (earthManager == null)
        {
            LogToScreen("AREarthManager is not assigned!");
            yield break;
        }

        if (anchorManager == null)
        {
            LogToScreen("ARAnchorManager is not assigned!");
            yield break;
        }

        if (positionsText == null)
        {
            positionsText = GameObject.Find("Position Feedback")?.GetComponent<Text>();
            if (positionsText == null)
            {
                Debug.LogError("Text element 'Position Feedback' not found!");
            }
        }

        Debug.Log("Starting CheckEarthState...");
        StartCoroutine(CheckEarthState());
    }

    System.Collections.IEnumerator CheckEarthState()
    {
        LogToScreen("Checking EarthState...");
        while (earthManager.EarthState != EarthState.Enabled)
        {
            LogToScreen("EarthState is not yet enabled. Waiting...");
            yield return new WaitForSeconds(1.0f); // Wait 1 second and check again
        }

        LogToScreen("EarthState is enabled!");
        isEarthStateReady = true;

        // Place the object once EarthState is ready
        PlaceObject();
    }

    void PlaceObject()
    {
        if (!isEarthStateReady)
        {
            LogToScreen("EarthState is not ready. Aborting.");
            return;
        }

        LogToScreen("Placing object...");
        var anchor = anchorManager.AddAnchor(latitude, longitude, altitude, Quaternion.identity);
        if (anchor == null)
        {
            LogToScreen("Failed to create anchor. Check coordinates.");
            return;
        }

        LogToScreen("Anchor created successfully.");
        if (objectToPlace == null)
        {
            LogToScreen("Prefab 'objectToPlace' is not assigned.");
            return;
        }

        var placedObject = Instantiate(objectToPlace, anchor.transform);
        if (placedObject == null)
        {
            LogToScreen("Failed to instantiate the placed object.");
            return;
        }

        placedObject.transform.localPosition = Vector3.zero;
        placedObject.transform.localRotation = Quaternion.identity;

        LogToScreen($"Object placed at: Lat={latitude}, Lon={longitude}, Alt={altitude}");
    }

    private void LogToScreen(string message)
    {
        if (positionsText != null)
        {
            positionsText.text += message + "\n";
        }
        Debug.Log(message);
    }
}

*/