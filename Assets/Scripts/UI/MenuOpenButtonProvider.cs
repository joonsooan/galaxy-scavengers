using UnityEngine;
using UnityEngine.UI;

public class MenuOpenButtonProvider : MonoBehaviour
{
    [SerializeField] public Button menuOpenButton;

    private void Awake()
    {
        if (GameMenuManager.Instance != null)
        {
            GameMenuManager.Instance.SetMenuOpenButton(menuOpenButton);
        }
    }

    private void OnEnable()
    {
        if (GameMenuManager.Instance != null)
        {
            GameMenuManager.Instance.SetMenuOpenButton(menuOpenButton);
        }
    }
}
