using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuScript : MonoBehaviour
{
    public void OnPlayButtonPressed()
    {
        SceneManager.LoadScene(1);
    }

    public void OnExitButtonPressed()
    {
        Application.Quit();
    }

    public void OnSettingsButtonPressed()
    {
        Debug.Log("Settings button pressed!"); // Debug: Check if method is called
        
        // Enable settings panel and disable main menu
        // FindGameObjectWithTag doesn't find inactive objects, so we search all objects
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject settingsPanel = null;
        
        foreach (GameObject obj in allObjects)
        {
            if (obj.CompareTag("SettingsUI"))
            {
                settingsPanel = obj;
                break;
            }
        }
        
        if (settingsPanel != null)
        {
            Debug.Log("Settings panel found, activating it."); // Debug: Panel found
            settingsPanel.SetActive(true);
        }
        else
        {
            Debug.LogError("Settings panel not found! Make sure a GameObject has the 'SettingsUI' tag."); // Debug: Panel missing
        }
    }

    public void OnCloseSettingsButtonPressed()
    {
        Debug.Log("Close settings button pressed!"); // Debug: Check if method is called
        
        // Disable settings panel and re-enable to main menu
        GameObject[] allObjects = Resources.FindObjectsOfTypeAll<GameObject>();
        GameObject settingsPanel = null;
        
        foreach (GameObject obj in allObjects)
        {
            if (obj.CompareTag("SettingsUI"))
            {
                settingsPanel = obj;
                break;
            }
        }
        
        if (settingsPanel != null)
        {
            Debug.Log("Settings panel found, deactivating it."); // Debug: Panel found
            settingsPanel.SetActive(false);
        }
        else
        {
            Debug.LogError("Settings panel not found!"); // Debug: Panel missing
        }
    }
}
