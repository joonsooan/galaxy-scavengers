using UnityEngine;
using UnityEngine.SceneManagement;

public class SceneLoader : MonoBehaviour
{
    public static SceneLoader Instance { get; private set; }

    [SerializeField] private string titleSceneName = "TitleScene";
    [SerializeField] private string mapSelectSceneName = "MapSelectScene";
    [SerializeField] private string seedCoreConfigSceneName = "SeedCoreConfigScene";
    [SerializeField] private string gameSceneName = "GameScene";
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public void LoadTitleScene()
    {
        SceneManager.LoadScene(titleSceneName);
    }

    public void LoadMapSelectScene()
    {
        SceneManager.LoadScene(mapSelectSceneName);
    }

    public void LoadSeedCoreConfigScene()
    {
        SceneManager.LoadScene(seedCoreConfigSceneName);
    }

    public void LoadGameScene()
    {
        SceneManager.LoadScene(gameSceneName);
    }
    
    public void QuitGame()
    {
        Application.Quit();
    }
}