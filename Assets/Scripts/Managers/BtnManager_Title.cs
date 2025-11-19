using UnityEngine;
using UnityEngine.UI;

public class BtnManager_Title : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(MapSelectScene);
        }
        
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(MapSelectScene);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }
    }

    private void MapSelectScene()
    {
        SceneLoader.Instance.LoadMapSelectScene();
    }

    private void QuitGame()
    {
        SceneLoader.Instance.QuitGame();
    }
}