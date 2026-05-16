using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class QuestChoiceCellController : MonoBehaviour
{
    [SerializeField] private Image resourceIconImage;
    [SerializeField] private TMP_Text requiredAmountText;
    [SerializeField] private GameObject tokenRewardRoot;
    [SerializeField] private Image tokenIconImage;
    [SerializeField] private TMP_Text tokenRewardText;
    [SerializeField] private Sprite questTokenIconSprite;
    [SerializeField] private Button acceptButton;
    [SerializeField] private TMP_Text acceptButtonLabel;

    private const string LocalizationTable = "UI_Common";
    private const string KeyAccept = "quest.choice.accept";

    private int _questId;
    private Action<int> _onAccept;

    private void Awake()
    {
        if (acceptButton != null)
        {
            acceptButton.onClick.RemoveAllListeners();
            acceptButton.onClick.AddListener(HandleAcceptClicked);
        }

        ApplyPassiveLocaleRefresh();
    }

    public void ApplyPassiveLocaleRefresh()
    {
        if (acceptButtonLabel != null)
        {
            acceptButtonLabel.text = GameLocalization.Get(LocalizationTable, KeyAccept);
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

        int tokenReward = 0;
        if (choice.rewardSpecs != null)
        {
            for (int i = 0; i < choice.rewardSpecs.Count; i++)
            {
                ProceduralQuestRewardSpec spec = choice.rewardSpecs[i];
                if (spec != null && spec.kind == ProceduralQuestRewardKind.Token && spec.amount > 0)
                {
                    tokenReward += spec.amount;
                }
            }
        }

        bool showToken = tokenReward > 0;
        if (tokenRewardRoot != null)
        {
            tokenRewardRoot.SetActive(showToken);
        }

        if (tokenIconImage != null)
        {
            if (showToken && questTokenIconSprite != null)
            {
                tokenIconImage.sprite = questTokenIconSprite;
                tokenIconImage.enabled = true;
            }
            else
            {
                tokenIconImage.sprite = null;
                tokenIconImage.enabled = false;
            }
        }

        if (tokenRewardText != null)
        {
            tokenRewardText.gameObject.SetActive(showToken);
            if (showToken)
            {
                tokenRewardText.text = $"+{tokenReward}";
            }
        }
    }

    private void HandleAcceptClicked()
    {
        _onAccept?.Invoke(_questId);
    }
}
