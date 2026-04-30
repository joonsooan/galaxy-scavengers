using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestChoiceCellController : MonoBehaviour
{
    [SerializeField] private Image resourceIconImage;
    [SerializeField] private TMP_Text requiredAmountText;
    [SerializeField] private Button acceptButton;

    private int _questId;
    private Action<int> _onAccept;

    private void Awake()
    {
        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(HandleAcceptClicked);
        }
    }

    public void Bind(ProceduralQuestChoiceData choice, Action<int> onAccept)
    {
        _questId = choice.questId;
        _onAccept = onAccept;

        if (ResourceManager.Instance != null)
        {
            if (resourceIconImage != null)
            {
                resourceIconImage.sprite = ResourceManager.Instance.GetResourceIcon(choice.targetResourceType);
            }
        }

        if (requiredAmountText != null)
        {
            requiredAmountText.text = choice.requiredAmount.ToString();
        }
    }

    private void HandleAcceptClicked()
    {
        _onAccept?.Invoke(_questId);
    }
}
