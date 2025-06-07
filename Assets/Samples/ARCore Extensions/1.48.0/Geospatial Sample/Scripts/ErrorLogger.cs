using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
public static class ErrorLogger
{
    public static void LogToErrorText(string message, string severity, Text uiText = null)
    {
        // Log to the console
        Debug.Log(message);

        if (uiText != null)
        {
            string color = "black"; // Default color

            // Set color based on severity
            switch (severity.ToLower())
            {
                case "success":
                    color = "green";
                    break;
                case "warning":
                    color = "yellow";
                    break;
                case "error":
                    color = "red";
                    break;
                case "black":
                default:
                    color = "black";
                    break;
            }

            // Append the message to the UI Text element with the specified color
            uiText.text += $"<color={color}>{message}</color>\n";
        }
    }
}
