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
            startButton.onClick.AddListener(BaseScene);
        }
        
        if (quitButton != null)
        {
            quitButton.onClick.AddListener(QuitGame);
        }
    }

    private void Start()
    {
        if (BgmManager.Instance != null)
        {
            BgmManager.Instance.PlayTitleBgm();
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(BaseScene);
        }
        if (quitButton != null)
        {
            quitButton.onClick.RemoveListener(QuitGame);
        }
    }

    private void BaseScene()
    {
        SceneLoader.Instance.LoadBaseScene();
    }

    private void QuitGame()
    {
        SceneLoader.Instance.QuitGame();
    }
}