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
        if (dataCellRoot == null || dataCellPrefab == null)
        {
            return;
        }

        ClearDataCells();

        IReadOnlyList<ResourceType> dataTypes = planetData.ObtainableDataTypes;
        if (dataTypes == null || dataTypes.Count == 0)
        {
            return;
        }

        for (int i = 0; i < dataTypes.Count; i++)
        {
            BaseInventoryCell cell = Instantiate(dataCellPrefab, dataCellRoot);
            cell.Initialize(null);
            cell.SetResource(dataTypes[i], planetData.ExpeditionDataAmount);
            _spawnedCells.Add(cell);
        }
    }

    private void ClearDataCells()
    {
        for (int i = 0; i < _spawnedCells.Count; i++)
        {
            BaseInventoryCell cell = _spawnedCells[i];
            if (cell != null)
            {
                Destroy(cell.gameObject);
            }
        }

        _spawnedCells.Clear();
    }

    private void OnDisable()
    {
        ClearDataCells();
    }
}
