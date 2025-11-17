using UnityEngine;
using UnityEngine.UI;

public class BtnManager_SeedCoreConfig : MonoBehaviour
{
    [SerializeField] private Button gameStartButton;
    [SerializeField] private Button previousSceneBtn;

    private void Awake()
    {
        if (gameStartButton != null)
        {
            gameStartButton.onClick.AddListener(StartGame);
        }

        if (previousSceneBtn != null)
        {
            previousSceneBtn.onClick.AddListener(MapSelectScene);
        }
    }

    private void OnDestroy()
    {
        if (gameStartButton != null)
        {
            gameStartButton.onClick.RemoveListener(StartGame);
        }
        
        if (previousSceneBtn != null)
        {
            previousSceneBtn.onClick.AddListener(MapSelectScene);
        }
    }

    private void StartGame()
    {
        SceneLoader.Instance.LoadGameScene();
    }

    private void MapSelectScene()
    {
        SceneLoader.Instance.LoadMapSelectScene();
    }
}