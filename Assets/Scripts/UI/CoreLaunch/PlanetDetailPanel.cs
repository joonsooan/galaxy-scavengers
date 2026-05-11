using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UI;

public class PlanetDetailPanel : MonoBehaviour
{
    [SerializeField] private Image planetImage;
    [SerializeField] private TMP_Text planetNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private TMP_Text extractableDataText;
    [SerializeField] private Transform dataCellRoot;
    [SerializeField] private BaseInventoryCell dataCellPrefab;

    private readonly List<BaseInventoryCell> _spawnedCells = new();
    private PlanetData _currentPlanetData;

    public BaseInventoryCell DataCellPrefab => dataCellPrefab;

    private void Awake()
    {
        EnsureReferences();
        ApplyStaticLabels();
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnSelectedLocaleChanged;
        EnsureReferences();
        ApplyStaticLabels();

        PlanetData selectedPlanet = PlanetSelectionState.SelectedPlanet;
        if (selectedPlanet == null)
        {
            return;
        }

        Bind(selectedPlanet);
    }

    public void Bind(PlanetData planetData)
    {
        if (planetData == null)
        {
            return;
        }

        _currentPlanetData = planetData;
        ApplyStaticLabels();

        if (planetImage != null)
        {
            planetImage.sprite = planetData.PlanetImage;
            planetImage.enabled = planetData.PlanetImage != null;
        }

        if (planetNameText != null)
        {
            planetNameText.text = planetData.PlanetName;
        }

        if (descriptionText != null)
        {
            descriptionText.text = planetData.DescriptionText;
        }

        RenderDataCells(planetData);
    }

    private void RenderDataCells(PlanetData planetData)
    {
        PlanetResourceCellsRenderer.RenderCells(dataCellRoot, dataCellPrefab, planetData, _spawnedCells);
    }

    private void ClearDataCells()
    {
        PlanetResourceCellsRenderer.ClearSpawnedCells(_spawnedCells);
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnSelectedLocaleChanged;
        ClearDataCells();
    }

    private void OnSelectedLocaleChanged(Locale _)
    {
        ApplyStaticLabels();
        if (_currentPlanetData != null)
        {
            Bind(_currentPlanetData);
        }
    }

    private void ApplyStaticLabels()
    {
        if (extractableDataText != null)
        {
            extractableDataText.text = GameLocalization.GetOrDefault("UI_Common", "base.extractableData",
                "\uCD94\uCD9C \uAC00\uB2A5\uD55C \uB370\uC774\uD130");
        }
    }

    private void EnsureReferences()
    {
        if (extractableDataText != null)
        {
            return;
        }

        TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            if (texts[i].gameObject.name == "Extractable Data Text")
            {
                extractableDataText = texts[i];
                return;
            }
        }
    }
}
