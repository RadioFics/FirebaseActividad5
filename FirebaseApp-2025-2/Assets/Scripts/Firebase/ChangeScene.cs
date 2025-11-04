using UnityEngine;
using UnityEngine.SceneManagement;

public class ChangeScene : MonoBehaviour
{
    [Tooltip("Nombre de la escena a cargar (añadir la escena en __Build Settings__).")]
    [SerializeField] private string sceneName = "SceneName";
    public void LoadScene()
    {
        LoadSceneByName(sceneName);
    }

    public void LoadSceneByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogWarning("ChangeScene: el nombre de la escena está vacío.");
            return;
        }

        SceneManager.LoadScene(name);
    }
    public void LoadSceneAsyncByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            Debug.LogWarning("ChangeScene: el nombre de la escena está vacío.");
            return;
        }

        SceneManager.LoadSceneAsync(name);
    }
}
