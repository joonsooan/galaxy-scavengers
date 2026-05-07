using System.Collections.Generic;
using TMPro;
using UnityEngine;
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

    private void Awake()
    {
        EnsureTabButtons();
        EnsurePanels();
        RenameMapTabLabel();
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
        }

        ToggleLaunchButton(_unlockedPlanets.Count > 0);
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
