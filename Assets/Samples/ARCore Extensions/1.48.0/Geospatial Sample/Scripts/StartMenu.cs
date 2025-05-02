using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StartMenu : MonoBehaviour
{
    public GameObject startScreenPanel;
    public Button startButton;
    public Toggle startCheckbox;
    public TMP_Dropdown Dropdown;
    public GameObject InfoPanel;
    public GameObject SnackBar;
    public string selectedDatabase;
    public GeospatialObjectPlacer geospatialObjectPlacer;

    private void Start()
    {

        // Button-Listener hinzufügen
        startButton.onClick.AddListener(OnStartButtonClicked);
        Dropdown.onValueChanged.AddListener(OnDropdownValueChanged);
    }

    public void OnStartButtonClicked()
    {
        // Prüfen, ob die Checkbox aktiviert ist (optional)
        if (startCheckbox.isOn)
        {
            Debug.Log("Checkbox ist aktiviert. Szene wird gestartet.");

            InfoPanel.SetActive(true);
            SnackBar.SetActive(true);
        }
        else
        {
            Debug.Log("Checkbox ist nicht aktiviert. Szene wird trotzdem gestartet.");

            InfoPanel.SetActive(false);
            SnackBar.SetActive(false);
        }
        startScreenPanel.SetActive(false);
    }
    private void OnDropdownValueChanged(int index)
    {
        // Setze den Wert basierend auf der Dropdown-Auswahl
        switch (index)
        {
            case 0:
                selectedDatabase = "ar-pictures-Wien";
                break;
            case 1:
                selectedDatabase = "ar-pictures-SchlossSchönbrunn";
                break;
            case 2:
                selectedDatabase = "ar-pictures-fhstp";
                break;
            default:
                selectedDatabase = "ar-pictures-Wien";
                break;
        }

        Debug.Log($"Selected database: {selectedDatabase}");

        // Übergabe an den GeospatialObjectPlacer
        if (geospatialObjectPlacer != null)
        {
            geospatialObjectPlacer.SetSelectedDatabase(selectedDatabase);
        }
    }
    public void GetSelectedDatabase(string database)
    {
        selectedDatabase = database;
    }
}