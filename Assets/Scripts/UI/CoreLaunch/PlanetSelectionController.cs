using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class PlanetSelectionController : MonoBehaviour
{
    private enum TabType
    {
        CoreManage,
        MapSelect
    }

    [Header("Tab")]
    [SerializeField] private Button coreManageTabButton;
    [SerializeField] private Button mapSelectTabButton;
    [SerializeField] private GameObject coreManagePanel;
    [SerializeField] private GameObject mapSelectPanel;

    [Header("Planet")]
    [SerializeField] private PlanetUnlockProvider unlockProvider;
    [SerializeField] private PlanetDetailPanel detailPanel;
    [SerializeField] private Transform mapButtonRoot;
    [SerializeField] private Button launchButton;

    private readonly Dictionary<Button, PlanetData> _buttonToPlanet = new();
    private readonly List<Button> _boundButtons = new();
    private List<PlanetData> _unlockedPlanets = new();
    private TabType _currentTab = TabType.CoreManage;

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyTabButtonLabels();
        ApplyCoreManageStaticChrome();
        ApplyPlanetButtonLabels();

        PlanetData selectedPlanet = PlanetSelectionState.SelectedPlanet;
        if (selectedPlanet != null && detailPanel != null)
        {
            detailPanel.Bind(selectedPlanet);
        }
    }

    private void Awake()
    {
        EnsureTabButtons();
        EnsurePanels();
        ApplyTabButtonLabels();
        ApplyCoreManageStaticChrome();
        BindTabEvents();
        BindPlanetButtons();
        SelectDefaultPlanet();
        ShowCoreManageTab();
    }

    private void EnsureTabButtons()
    {
        if (coreManageTabButton != null && mapSelectTabButton != null)
        {
            return;
        }

        Button[] buttons = GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            if (coreManageTabButton == null && (button.name.Contains("Shop") || button.name.Contains("CoreManage")))
            {
                coreManageTabButton = button;
            }

            if (mapSelectTabButton == null && (button.name.Contains("Quest") || button.name.Contains("MapSelect")))
            {
                mapSelectTabButton = button;
            }
        }
    }

    private void EnsurePanels()
    {
        if (coreManagePanel == null)
        {
            Transform target = transform.Find("CoreManagePanel");
            if (target != null)
            {
                coreManagePanel = target.gameObject;
            }
        }

        if (mapSelectPanel == null)
        {
            Transform target = transform.Find("MapSelectPanel");
            if (target != null)
            {
                mapSelectPanel = target.gameObject;
            }
        }

        if (coreManagePanel == null || mapSelectPanel == null)
        {
            RectTransform[] rects = GetComponentsInChildren<RectTransform>(true);
            foreach (RectTransform rect in rects)
            {
                string name = rect.name;
                if (coreManagePanel == null && (name.Contains("CoreManage") || name.Contains("CoreCustomPanel") || name.Contains("Shop")))
                {
                    coreManagePanel = rect.gameObject;
                }

                if (mapSelectPanel == null && (name.Contains("MapSelect") || name.Contains("Quest")))
                {
                    mapSelectPanel = rect.gameObject;
                }
            }
        }

        if (mapButtonRoot == null && mapSelectPanel != null)
        {
            mapButtonRoot = mapSelectPanel.transform;
        }
    }

    private void ApplyTabButtonLabels()
    {
        RenameCoreManageTabLabel();
        RenameMapTabLabel();
        LocalizeModuleCraftTabButton();
    }

    private void ApplyCoreManageStaticChrome()
    {
        foreach (TMP_Text tmp in GetComponentsInChildren<TMP_Text>(true))
        {
            if (tmp == null)
            {
                continue;
            }

            if (tmp.gameObject.name == "Core Desc Text")
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.coreManageDescription",
                    "시드 코어를 관리하고, 발사 준비를 하는 장소입니다.");
                continue;
            }

            if (tmp.gameObject.name == "Core Title Text")
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.coreHangar",
                    "코어 격납고");
                continue;
            }

            if (tmp.gameObject.name == "Module Title Text")
            {
                tmp.text = GameLocalization.GetOrDefault("GameData", "moduleStation.default.name",
                    "모듈 스테이션");
                continue;
            }

            if (tmp.gameObject.name == "Title Text" && tmp.transform.parent != null
                && tmp.transform.parent.name == "Module Inventory Panel")
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.modulesOwned",
                    "보유 중인 모듈");
                continue;
            }

            if (tmp.gameObject.name == "Core Name Text" && tmp.transform.parent != null
                && tmp.transform.parent.name == "Core Name")
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.moduleEffects",
                    "모듈 효과");
            }
        }
    }

    private void LocalizeModuleCraftTabButton()
    {
        foreach (Button button in GetComponentsInChildren<Button>(true))
        {
            if (button == mapSelectTabButton || button == coreManageTabButton)
            {
                continue;
            }

            if (!IsModuleCraftButton(button.gameObject.name))
            {
                continue;
            }

            TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
            if (text != null)
            {
                text.text = GameLocalization.GetOrDefault("UI_Common", "base.moduleCraft", "모듈 제작");
            }
        }
    }

    private static bool IsModuleCraftButton(string objectName)
    {
        return objectName.Contains("Button_Shop") || objectName.Contains("Button Shop");
    }

    private void RenameCoreManageTabLabel()
    {
        if (coreManageTabButton == null)
        {
            return;
        }

        TMP_Text text = coreManageTabButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = GameLocalization.GetOrDefault("UI_Common", "base.coreManage", "코어 관리");
        }
    }

    private void RenameMapTabLabel()
    {
        if (mapSelectTabButton == null)
        {
            return;
        }

        TMP_Text text = mapSelectTabButton.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = GameLocalization.GetOrDefault("UI_Common", "title.mapSelection", "맵 선택");
        }
    }

    private void BindTabEvents()
    {
        if (coreManageTabButton != null)
        {
            coreManageTabButton.onClick.RemoveListener(ShowCoreManageTab);
            coreManageTabButton.onClick.AddListener(ShowCoreManageTab);
        }

        if (mapSelectTabButton != null)
        {
            mapSelectTabButton.onClick.RemoveListener(ShowMapSelectTab);
            mapSelectTabButton.onClick.AddListener(ShowMapSelectTab);
        }
    }

    private void BindPlanetButtons()
    {
        _buttonToPlanet.Clear();
        _boundButtons.Clear();
        _unlockedPlanets = unlockProvider != null ? unlockProvider.GetUnlockedPlanets() : new List<PlanetData>();

        if (mapButtonRoot == null)
        {
            ToggleLaunchButton(false);
            return;
        }

        Button[] buttons = mapButtonRoot.GetComponentsInChildren<Button>(true);
        foreach (Button button in buttons)
        {
            string buttonName = button.name;
            PlanetData planetData = FindPlanetByButtonName(buttonName);
            if (planetData == null)
            {
                continue;
            }

            _buttonToPlanet[button] = planetData;
            _boundButtons.Add(button);
            button.onClick.AddListener(() => OnPlanetClicked(button));
            button.interactable = true;
            SetPlanetButtonLabel(button, planetData);
        }

        ToggleLaunchButton(_unlockedPlanets.Count > 0);
    }

    private void ApplyPlanetButtonLabels()
    {
        for (int i = 0; i < _boundButtons.Count; i++)
        {
            Button button = _boundButtons[i];
            if (button == null || !_buttonToPlanet.TryGetValue(button, out PlanetData planet))
            {
                continue;
            }

            SetPlanetButtonLabel(button, planet);
        }
    }

    private static void SetPlanetButtonLabel(Button button, PlanetData planet)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);
        if (text != null)
        {
            text.text = planet.PlanetName;
        }
    }

    private PlanetData FindPlanetByButtonName(string buttonName)
    {
        foreach (PlanetData planet in _unlockedPlanets)
        {
            if (planet == null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(planet.MapButtonName) && planet.MapButtonName == buttonName)
            {
                return planet;
            }
        }

        return null;
    }

    private void SelectDefaultPlanet()
    {
        if (_unlockedPlanets.Count == 0)
        {
            PlanetSelectionState.ClearSelection();
            return;
        }

        PlanetData firstPlanet = _unlockedPlanets[0];
        PlanetSelectionState.SetSelectedPlanet(firstPlanet);
        if (detailPanel != null)
        {
            detailPanel.Bind(firstPlanet);
        }
    }

    private void OnPlanetClicked(Button button)
    {
        if (!_buttonToPlanet.TryGetValue(button, out PlanetData planet))
        {
            return;
        }

        PlanetSelectionState.SetSelectedPlanet(planet);
        if (detailPanel != null)
        {
            detailPanel.Bind(planet);
        }
    }

    private void ToggleLaunchButton(bool canLaunch)
    {
        if (launchButton != null)
        {
            launchButton.interactable = canLaunch;
        }
    }

    public void ShowCoreManageTab()
    {
        _currentTab = TabType.CoreManage;
        ApplyTabVisibility();
    }

    public void ShowMapSelectTab()
    {
        _currentTab = TabType.MapSelect;
        ApplyTabVisibility();
    }

    private void ApplyTabVisibility()
    {
        bool isCoreManage = _currentTab == TabType.CoreManage;

        if (coreManagePanel != null)
        {
            coreManagePanel.SetActive(isCoreManage);
        }

        if (mapSelectPanel != null)
        {
            mapSelectPanel.SetActive(!isCoreManage);
        }
    }
}
