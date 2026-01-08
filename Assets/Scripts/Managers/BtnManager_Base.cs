using UnityEngine;
using UnityEngine.UI;

public class BtnManager_Base : MonoBehaviour
{
    [SerializeField] private Button startButton;
    [SerializeField] private Button titleButton;

    private void Awake()
    {
        if (startButton != null)
        {
            startButton.onClick.AddListener(GameScene);
        }
        
        if (titleButton != null)
        {
            titleButton.onClick.AddListener(BackToTitle);
        }
    }

    private void OnDestroy()
    {
        if (startButton != null)
        {
            startButton.onClick.RemoveListener(GameScene);
        }
        if (titleButton != null)
        {
            titleButton.onClick.RemoveListener(BackToTitle);
        }
    }

    private void GameScene()
    {
        SceneLoader.Instance.LoadGameScene();
    }

    private void BackToTitle()
    {
        SceneLoader.Instance.LoadTitleScene();
    }
}