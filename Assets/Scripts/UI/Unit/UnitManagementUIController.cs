using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class UnitManagementUIController : MonoBehaviour
{
    private const int TabCount = 3;
    private static int s_lastSelectedTabIndex;
    private string _lastPopulationSummary;

    [Header("Default Settings")]
    [SerializeField] private TMP_Text totalAllyUnitCountText;

    [Header("Miner Settings")]
    [SerializeField] private Button minerTabButton;
    [SerializeField] private TMP_Text minerTabCountText;
    [SerializeField] private GameObject minerSubPanel;
    [SerializeField] private UnitMinerAssignmentUIController minerAssignmentUI;

    [Header("Upgrade Settings")]
    [SerializeField] private Button unitUpgradeTabButton;
    [SerializeField] private GameObject unitUpgradeSubPanel;
    [SerializeField] private UnitUpgradeUIController unitUpgradeUI;
    [SerializeField] private TMP_Text upgradeTokenCountText;

    [Header("Charge Settings")]
    [SerializeField] private Button unitChargeTabButton;
    [SerializeField] private GameObject unitChargeSubPanel;
    [SerializeField] private UnitChargeManagementUIController unitChargeUI;

    private void OnEnable()
    {
        UnitManager.OnUnitCountChanged += OnUnitCountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged += OnUpgradeStateChanged;
        GameplayTokenWallet.EnsureExists(this);
        GameplayTokenWallet.OnBalanceChanged += OnTokenBalanceChanged;
        WireTabButtons(true);
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        ApplyLocalizedStaticTexts();
        RefreshSummary();
        ShowTab(GetValidTabIndex(s_lastSelectedTabIndex));
    }

    private void OnDisable()
    {
        UnitManager.OnUnitCountChanged -= OnUnitCountChanged;
        UnitUpgradeProgress.OnUpgradeStateChanged -= OnUpgradeStateChanged;
        GameplayTokenWallet.OnBalanceChanged -= OnTokenBalanceChanged;
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        WireTabButtons(false);
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyLocalizedStaticTexts();
        RefreshSummary();
        ShowTab(GetValidTabIndex(s_lastSelectedTabIndex));
    }

    private void OnUpgradeStateChanged()
    {
        RefreshSummary();
    }

    private void OnTokenBalanceChanged()
    {
        RefreshTokenCount();
    }

    private void WireTabButtons(bool add)
    {
        if (minerTabButton != null)
        {
            minerTabButton.onClick.RemoveListener(OnMinerTabClicked);
            if (add)
            {
                minerTabButton.onClick.AddListener(OnMinerTabClicked);
            }
        }

        if (unitUpgradeTabButton != null)
        {
            unitUpgradeTabButton.onClick.RemoveListener(OnUnitUpgradeTabClicked);
            if (add)
            {
                unitUpgradeTabButton.onClick.AddListener(OnUnitUpgradeTabClicked);
            }
        }

        if (unitChargeTabButton != null)
        {
            unitChargeTabButton.onClick.RemoveListener(OnUnitChargeTabClicked);
            if (add)
            {
                unitChargeTabButton.onClick.AddListener(OnUnitChargeTabClicked);
            }
        }

    }

    private void ApplyLocalizedStaticTexts()
    {
        SetTextByName("Title Text", "game.unitManage", "유닛 관리");
        SetButtonLabel(minerTabButton, "unitManage.minerInfo", "채굴 정보");
        SetButtonLabel(unitChargeTabButton, "unitManage.charge", "충전");
        SetButtonLabel(unitUpgradeTabButton, "unitManage.upgrade", "업그레이드");

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text == null)
            {
                continue;
            }

            if (!IsInNamedHierarchy(text.transform, "Resource Text Panel"))
            {
                continue;
            }

            if (text.gameObject.name == "text_1")
            {
                text.text = GameLocalization.GetOrDefault("UI_Common", "unitManage.text1", "자원 아이콘을 클릭하여\n유닛이 채굴 가능한 자원을\n지정해줄 수 있습니다");
            }
            else if (text.gameObject.name == "text_2")
            {
                text.text = GameLocalization.GetOrDefault("UI_Common", "unitManage.text2", "투입 가능한 자원");
            }
            else if (text.gameObject.name == "text_3")
            {
                text.text = GameLocalization.GetOrDefault("UI_Common", "unitManage.text3", "필요한 자원");
            }
        }
    }

    private void SetTextByName(string objectName, string key, string fallback)
    {
        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        foreach (TMP_Text text in texts)
        {
            if (text != null && text.gameObject.name == objectName)
            {
                text.text = GameLocalization.GetOrDefault("UI_Common", key, fallback);
                return;
            }
        }
    }

    private static void SetButtonLabel(Button button, string key, string fallback)
    {
        if (button == null)
        {
            return;
        }

        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.text = GameLocalization.GetOrDefault("UI_Common", key, fallback);
        }
    }

    private static bool IsInNamedHierarchy(Transform transform, string objectName)
    {
        Transform current = transform;
        while (current != null)
        {
            if (current.name == objectName)
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private void OnMinerTabClicked()
    {
        ShowTab(0);
    }

    private void OnUnitUpgradeTabClicked()
    {
        ShowTab(1);
    }

    private void OnUnitChargeTabClicked()
    {
        ShowTab(2);
    }

    private void OnUnitCountChanged(UnitBase _)
    {
        RefreshSummary();
    }

    public void RefreshSummary()
    {
        if (UnitManager.Instance == null)
        {
            return;
        }

        int total = UnitManager.Instance.GetPopulationCountedAllyCount();
        int max = UnitManager.Instance.GetMaxPopulation();
        int miners = UnitManager.Instance.AllyUnits.Count(u => u is Unit_Miner);

        if (totalAllyUnitCountText != null)
        {
            string nextSummary = $"{total} / {max}";
            totalAllyUnitCountText.text = nextSummary;
            if (_lastPopulationSummary != nextSummary)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(totalAllyUnitCountText.rectTransform);
                _lastPopulationSummary = nextSummary;
            }
        }

        if (minerTabCountText != null)
        {
            minerTabCountText.text = miners.ToString();
        }

        RefreshTokenCount();
    }

    private void ShowTab(int index)
    {
        int safeIndex = GetValidTabIndex(index);
        s_lastSelectedTabIndex = safeIndex;

        if (minerSubPanel != null)
        {
            minerSubPanel.SetActive(safeIndex == 0);
        }

        if (unitUpgradeSubPanel != null)
        {
            unitUpgradeSubPanel.SetActive(safeIndex == 1);
        }

        if (unitChargeSubPanel != null)
        {
            unitChargeSubPanel.SetActive(safeIndex == 2);
        }

        if (safeIndex == 0 && minerAssignmentUI != null)
        {
            minerAssignmentUI.RefreshUIFromSystem();
        }

        if (safeIndex == 1 && unitUpgradeUI != null)
        {
            unitUpgradeUI.Refresh();
        }

        if (safeIndex == 2 && unitChargeUI != null)
        {
            unitChargeUI.Refresh();
        }
    }

    private static int GetValidTabIndex(int index)
    {
        return Mathf.Clamp(index, 0, TabCount - 1);
    }

    private void RefreshTokenCount()
    {
        if (upgradeTokenCountText == null)
        {
            return;
        }

        int tokenBalance = GameplayTokenWallet.Instance != null ? GameplayTokenWallet.Instance.Balance : 0;
        upgradeTokenCountText.text = tokenBalance.ToString();
    }
}
