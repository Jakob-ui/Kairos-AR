using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StartMenu : MonoBehaviour
{
    public GameObject startScreenPanel;
    public Button startButton;
    public Toggle startCheckbox;
    public GameObject InfoPanel;
    public GameObject SnackBar;

    private void Start()
    {

        // Button-Listener hinzufügen
        startButton.onClick.AddListener(OnStartButtonClicked);
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
}