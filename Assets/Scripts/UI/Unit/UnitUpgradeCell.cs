using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class UnitUpgradeCell : MonoBehaviour
{
    [SerializeField] private TMP_Text titleText;
    [SerializeField] private TMP_Text descText;
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text levelText;
    [SerializeField] private string maxLevelLabel = "최대 레벨";
    [SerializeField] private Transform resourceContent;
    [SerializeField] private GameObject resourceInfoCellPrefab;
    [SerializeField] private Button upgradeButton;

    private UnitUpgradeLineData _line;
    private UnitUpgradeProgress _progress;
    private Button _resolvedUpgradeButton;
    private bool _hasCachedUpgradeButtonLabel;
    private string _cachedUpgradeButtonLabel;
    private const string UpgradeInProgressLabel = "업그레이드 중…";

    private void OnDestroy()
    {
        Button btn = GetUpgradeButton();
        if (btn != null)
        {
            btn.onClick.RemoveListener(OnUpgradeClicked);
        }
    }

    public void Initialize(UnitUpgradeLineData line, UnitUpgradeProgress progress)
    {
        _line = line;
        _progress = progress;
        if (_progress == null)
        {
            _progress = UnitUpgradeProgress.Instance;
        }

        if (_progress == null)
        {
            _progress = FindFirstObjectByType<UnitUpgradeProgress>(FindObjectsInactive.Include);
        }

        WireUpgradeButtonClick(true);
        RefreshDisplay();
    }

    private Button GetUpgradeButton()
    {
        if (_resolvedUpgradeButton != null)
        {
            return _resolvedUpgradeButton;
        }

        if (upgradeButton != null)
        {
            _resolvedUpgradeButton = upgradeButton;
            return _resolvedUpgradeButton;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        for (int i = 0; i < buttons.Length; i++)
        {
            Button candidate = buttons[i];
            if (candidate == null)
            {
                continue;
            }

            if (resourceContent != null && candidate.transform.IsChildOf(resourceContent))
            {
                continue;
            }

            _resolvedUpgradeButton = candidate;
            return _resolvedUpgradeButton;
        }

        return null;
    }

    private void WireUpgradeButtonClick(bool add)
    {
        Button btn = GetUpgradeButton();
        if (btn == null)
        {
            return;
        }

        btn.onClick.RemoveListener(OnUpgradeClicked);
        if (add)
        {
            btn.onClick.AddListener(OnUpgradeClicked);
        }
    }

    public void RefreshDisplay()
    {
        if (_line == null)
        {
            return;
        }

        UnitUpgradeProgress progress = ResolveProgressReference();
        if (progress != null)
        {
            _progress = progress;
        }

        if (titleText != null)
        {
            titleText.text = _line.displayName;
        }

        if (iconImage != null)
        {
            iconImage.sprite = _line.icon;
            iconImage.enabled = _line.icon != null;
        }

        int level = progress != null ? progress.GetLevel(_line.statType) : 0;
        bool maxed = _line.tiers == null || level >= _line.tiers.Length;

        if (levelText != null)
        {
            levelText.text = maxed ? maxLevelLabel : $"Lv {level}.";
        }

        if (descText != null)
        {
            descText.text = GetDescriptionTextForDisplay(level, maxed);
        }

        ClearResourceCells();
        UnitUpgradeTier nextTier = GetNextTier(level);

        if (nextTier != null && nextTier.costs != null && resourceContent != null && resourceInfoCellPrefab != null)
        {
            foreach (ResourceCost cost in nextTier.costs)
            {
                GameObject cellObj = Instantiate(resourceInfoCellPrefab, resourceContent);
                ResourceInfoCell cell = cellObj.GetComponent<ResourceInfoCell>();
                if (cell != null)
                {
                    cell.SetInfo(cost.resourceType, cost.amount, false);
                }
            }

            foreach (Transform child in resourceContent)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(child as RectTransform);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(resourceContent as RectTransform);
        }

        Button btn = GetUpgradeButton();
        if (btn != null)
        {
            if (progress == null)
            {
                btn.interactable = false;
            }
            else
            {
                bool blockedByOtherLine = progress.IsAnyUpgradeInProgress() &&
                    !progress.IsUpgradeInProgress(_line.statType);
                bool canAffordResources = !maxed && TierResourceCostsSatisfied(nextTier);
                btn.interactable = !maxed && !blockedByOtherLine && canAffordResources;
            }

            ApplyUpgradeButtonLabel(btn, progress);
        }
    }

    private void ApplyUpgradeButtonLabel(Button btn, UnitUpgradeProgress progress)
    {
        TMP_Text label = btn != null ? btn.GetComponentInChildren<TMP_Text>(true) : null;
        if (label == null)
        {
            return;
        }

        bool upgrading = progress != null && progress.IsUpgradeInProgress(_line.statType);
        if (upgrading)
        {
            if (!_hasCachedUpgradeButtonLabel)
            {
                _cachedUpgradeButtonLabel = label.text;
                _hasCachedUpgradeButtonLabel = true;
            }

            label.text = UpgradeInProgressLabel;
        }
        else if (_hasCachedUpgradeButtonLabel)
        {
            label.text = _cachedUpgradeButtonLabel;
        }
    }

    private static bool TierResourceCostsSatisfied(UnitUpgradeTier tier)
    {
        if (tier == null)
        {
            return false;
        }

        if (tier.costs == null || tier.costs.Length == 0)
        {
            return true;
        }

        return ResourceManager.Instance != null &&
            ResourceManager.Instance.HasEnoughResources(tier.costs);
    }

    private static UnitUpgradeProgress ResolveProgressReference()
    {
        if (UnitUpgradeProgress.Instance != null)
        {
            return UnitUpgradeProgress.Instance;
        }

        return FindFirstObjectByType<UnitUpgradeProgress>(FindObjectsInactive.Include);
    }

    private string GetDescriptionTextForDisplay(int currentLevel, bool isMaxed)
    {
        if (_line == null || _line.tiers == null || _line.tiers.Length == 0)
        {
            return string.Empty;
        }

        if (isMaxed)
        {
            UnitUpgradeTier last = _line.tiers[_line.tiers.Length - 1];
            return last != null && last.description != null ? last.description : string.Empty;
        }

        UnitUpgradeTier next = _line.tiers[currentLevel];
        return next != null && next.description != null ? next.description : string.Empty;
    }

    private UnitUpgradeTier GetNextTier(int currentLevel)
    {
        if (_line == null || _line.tiers == null)
        {
            return null;
        }

        if (currentLevel >= _line.tiers.Length)
        {
            return null;
        }

        return _line.tiers[currentLevel];
    }

    private void ClearResourceCells()
    {
        if (resourceContent == null)
        {
            return;
        }

        for (int i = resourceContent.childCount - 1; i >= 0; i--)
        {
            Destroy(resourceContent.GetChild(i).gameObject);
        }
    }

    private void OnUpgradeClicked()
    {
        if (_line == null)
        {
            return;
        }

        UnitUpgradeProgress progress = ResolveProgressReference();
        if (progress == null)
        {
            return;
        }

        _progress = progress;

        if (progress.IsUpgradeInProgress(_line.statType))
        {
            return;
        }

        progress.TryQueueUpgrade(_line);
        RefreshDisplay();
    }
}
