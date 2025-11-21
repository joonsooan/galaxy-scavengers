using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestInfoPanel : MonoBehaviour
{
    [SerializeField] private GameObject infoPanel;
    [SerializeField] private TMP_Text questNameText;
    [SerializeField] private TMP_Text questDescriptionText;

    private void Awake()
    {
        ChangeInfo();
    }

    private void ChangeInfo()
    {
        LayoutRebuilder.ForceRebuildLayoutImmediate(infoPanel.GetComponent<RectTransform>());
        LayoutRebuilder.ForceRebuildLayoutImmediate(questNameText.rectTransform);
        LayoutRebuilder.ForceRebuildLayoutImmediate(questDescriptionText.rectTransform);
    }
}
