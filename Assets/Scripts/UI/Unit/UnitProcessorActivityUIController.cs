using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class UnitProcessorActivityUIController : MonoBehaviour
{
    [SerializeField] private UnitProcessorActivityCellView[] cells;
    [SerializeField] private float refreshInterval = 0.5f;

    private float _nextRefresh;

    private void OnEnable()
    {
        _nextRefresh = 0f;
    }

    private void Update()
    {
        if (Time.unscaledTime < _nextRefresh)
        {
            return;
        }

        _nextRefresh = Time.unscaledTime + refreshInterval;
        Refresh();
    }

    public void Refresh()
    {
        if (cells == null || cells.Length == 0)
        {
            return;
        }

        Dictionary<ResourceType, float> rateByType = new Dictionary<ResourceType, float>();
        Processor[] processors = FindObjectsByType<Processor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (Processor processor in processors)
        {
            if (processor == null)
            {
                continue;
            }

            IReadOnlyList<ActiveRecipe> recipes = processor.ActiveRecipes;
            if (recipes == null)
            {
                continue;
            }

            foreach (ActiveRecipe recipe in recipes)
            {
                if (recipe?.recipeData == null)
                {
                    continue;
                }

                if (!recipe.isProcessing && recipe.assignedDrone == null)
                {
                    continue;
                }

                ProcessorRecipe data = recipe.recipeData;
                float time = data.processingTime;
                if (time <= 0f)
                {
                    continue;
                }

                float perMin = data.produceAmount / time * 60f;
                ResourceType rt = data.resourceType;
                if (rateByType.TryGetValue(rt, out float existing))
                {
                    rateByType[rt] = existing + perMin;
                }
                else
                {
                    rateByType[rt] = perMin;
                }
            }
        }

        List<KeyValuePair<ResourceType, float>> sorted = rateByType
            .OrderBy(kvp => (int)kvp.Key)
            .ToList();

        ResourceManager rm = ResourceManager.Instance;
        for (int i = 0; i < cells.Length; i++)
        {
            UnitProcessorActivityCellView cell = cells[i];
            if (cell == null)
            {
                continue;
            }

            if (i >= sorted.Count)
            {
                cell.SetVisible(false);
                continue;
            }

            KeyValuePair<ResourceType, float> entry = sorted[i];
            Sprite icon = rm != null ? rm.GetResourceIcon(entry.Key) : null;
            cell.SetVisible(true);
            cell.SetData(icon, entry.Value);
        }
    }
}
