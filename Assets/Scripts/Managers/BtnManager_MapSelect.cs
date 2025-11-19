using UnityEngine;
using UnityEngine.UI;

public class BtnManager_MapSelect : MonoBehaviour
{
    [SerializeField] private Button[] mapSelectButtons;
    [SerializeField] private Button previousSceneBtn;

    private void Awake()
    {
        if (mapSelectButtons != null)
        {
            foreach (Button btn in mapSelectButtons)
            {
                btn.onClick.AddListener(SeedCoreConfigScene);
            }
        }

        if (previousSceneBtn != null)
        {
            previousSceneBtn.onClick.AddListener(TitleScene);
        }
    }

    private void OnDestroy()
    {
        if (mapSelectButtons != null)
        {
            foreach (Button btn in mapSelectButtons)
            {
                btn.onClick.RemoveListener(SeedCoreConfigScene);
            }
        }
        
        if (previousSceneBtn != null)
        {
            previousSceneBtn.onClick.AddListener(TitleScene);
        }
    }

    private void SeedCoreConfigScene()
    {
        SceneLoader.Instance.LoadSeedCoreConfigScene();
    }

    private void TitleScene()
    {
        SceneLoader.Instance.LoadTitleScene();
    }
}