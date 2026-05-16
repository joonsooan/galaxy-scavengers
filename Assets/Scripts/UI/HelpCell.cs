using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HelpCell : MonoBehaviour
{
    [SerializeField] private Button cellButton;
    [SerializeField] private TMP_Text cellNameText;

    private HelpData _helpData;
    private HelpUIManager _manager;

    private void Awake()
    {
        if (cellButton != null)
        {
            cellButton.onClick.AddListener(OnCellClicked);
        }
    }

    private void OnDestroy()
    {
        if (cellButton != null)
        {
            cellButton.onClick.RemoveAllListeners();
        }
    }

    public void Initialize(HelpData data, HelpUIManager manager)
    {
        _helpData = data;
        _manager = manager;

        if (cellNameText != null)
        {
            cellNameText.text = data.helpName;
        }
    }

    private void OnCellClicked()
    {
        if (_helpData != null && _manager != null)
        {
            _manager.SelectEntry(_helpData);
        }
    }
}
