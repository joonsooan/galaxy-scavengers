using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlanetDetailPanel : MonoBehaviour
{
    [SerializeField] private Image planetImage;
    [SerializeField] private TMP_Text planetNameText;
    [SerializeField] private TMP_Text descriptionText;
    [SerializeField] private Transform dataCellRoot;
    [SerializeField] private BaseInventoryCell dataCellPrefab;

    private readonly List<BaseInventoryCell> _spawnedCells = new();

    public BaseInventoryCell DataCellPrefab => dataCellPrefab;

    private void OnEnable()
    {
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
        ClearDataCells();
    }
}
