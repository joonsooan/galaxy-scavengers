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
                    "\uC2DC\uB4DC \uCF54\uC5B4\uB97C \uAD00\uB9AC\uD558\uACE0, \uBC1C\uC0AC \uC900\uBE44\uB97C \uD558\uB294 \uC7A5\uC18C\uC785\uB2C8\uB2E4.");
                continue;
            }

            if (tmp.gameObject.name == "Core Title Text")
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.coreHangar",
                    "\uCF54\uC5B4 \uACA9\uB0A9\uACE0");
                continue;
            }

            if (tmp.gameObject.name == "Module Title Text")
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.moduleStation",
                    "\uBAA8\uB4C8 \uC2A4\uD14C\uC774\uC158");
                continue;
            }

            if (tmp.gameObject.name == "Title Text" && tmp.transform.parent != null
                && tmp.transform.parent.name == "Module Inventory Panel")
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.modulesOwned",
                    "\uBCF4\uC720 \uC911\uC778 \uBAA8\uB4C8");
                continue;
            }

            if (tmp.gameObject.name == "Core Name Text" && tmp.transform.parent != null
                && tmp.transform.parent.name == "Core Name")
            {
                tmp.text = GameLocalization.GetOrDefault("UI_Common", "base.moduleEffects",
                    "\uBAA8\uB4C8 \uD6A8\uACFC");
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
                text.text = GameLocalization.GetOrDefault("UI_Common", "base.moduleCraft", "\uBAA8\uB4C8 \uC81C\uC791");
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
            text.text = GameLocalization.GetOrDefault("UI_Common", "base.coreManage", "\uCF54\uC5B4 \uAD00\uB9AC");
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
            text.text = GameLocalization.GetOrDefault("UI_Common", "title.mapSelection", "\uB9F5 \uC120\uD0DD");
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
