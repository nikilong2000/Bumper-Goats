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
        SceneManager.LoadScene("SettingsScene");
    }
}
