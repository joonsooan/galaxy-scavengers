using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class CoreManagePlanetSummary : MonoBehaviour
{
    [SerializeField] private Image planetImage;
    [SerializeField] private TMP_Text planetNameText;
    [SerializeField] private Transform dataCellRoot;
    [SerializeField] private BaseInventoryCell dataCellPrefab;

    private readonly List<BaseInventoryCell> _spawnedCells = new();

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        PlanetSelectionState.SelectedPlanetChanged += OnSelectedPlanetChanged;
        Refresh();
    }

    private void OnDisable()
    {
        PlanetSelectionState.SelectedPlanetChanged -= OnSelectedPlanetChanged;
        PlanetResourceCellsRenderer.ClearSpawnedCells(_spawnedCells);
    }

    private void OnSelectedPlanetChanged(PlanetData _)
    {
        Refresh();
    }

    private void EnsureReferences()
    {
        if (planetImage != null && planetNameText != null && dataCellRoot != null && dataCellPrefab != null)
        {
            return;
        }

        Transform info = transform.Find("Core Manage Panel/Module Title Panel/Planet Info Panel");
        if (info == null)
        {
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i].name == "Planet Info Panel")
                {
                    info = all[i];
                    break;
                }
            }
        }

        if (info == null)
        {
            return;
        }

        if (planetImage == null)
        {
            Transform t = info.Find("Planet Image");
            if (t != null)
            {
                planetImage = t.GetComponent<Image>();
            }
        }

        if (planetNameText == null)
        {
            Transform t = info.Find("Planet Name Text");
            if (t != null)
            {
                planetNameText = t.GetComponent<TMP_Text>();
            }
        }

        if (dataCellRoot == null)
        {
            Transform t = info.Find("Data Grid");
            if (t != null)
            {
                dataCellRoot = t;
            }
        }

        if (dataCellPrefab == null)
        {
            PlanetDetailPanel detail = FindFirstObjectByType<PlanetDetailPanel>(FindObjectsInactive.Include);
            if (detail != null)
            {
                dataCellPrefab = detail.DataCellPrefab;
            }
        }
    }

    private void Refresh()
    {
        EnsureReferences();
        PlanetData planet = PlanetSelectionState.SelectedPlanet;
        if (planet == null)
        {
            if (planetImage != null)
            {
                planetImage.sprite = null;
                planetImage.enabled = false;
            }

            if (planetNameText != null)
            {
                planetNameText.text = string.Empty;
            }

            PlanetResourceCellsRenderer.ClearSpawnedCells(_spawnedCells);
            return;
        }

        if (planetImage != null)
        {
            Sprite s = planet.PlanetThumbnail != null ? planet.PlanetThumbnail : planet.PlanetImage;
            planetImage.sprite = s;
            planetImage.enabled = s != null;
        }

        if (planetNameText != null)
        {
            planetNameText.text = planet.PlanetName;
        }

        PlanetResourceCellsRenderer.RenderCells(dataCellRoot, dataCellPrefab, planet, _spawnedCells);
    }
}
