using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[DefaultExecutionOrder(10)]
public class MinerAssignmentSystem : MonoBehaviour
{
    public static MinerAssignmentSystem Instance { get; private set; }

    private static readonly ResourceType[] BaseResourceOrder =
    {
        ResourceType.Ferrite,
        ResourceType.Aether,
        ResourceType.Biomass,
        ResourceType.CryoCrystal
    };

    [SerializeField] private bool ratioMode;
    [SerializeField] private int[] directCounts = { 0, 0, 0, 0 };
    [SerializeField] private int[] ratioWeights = { 1, 1, 1, 1 };

    private int[] _lastSlotCounts = { 0, 0, 0, 0 };

    public IReadOnlyList<int> LastSlotCounts => _lastSlotCounts;

    public bool RatioMode
    {
        get => ratioMode;
        set
        {
            ratioMode = value;
            RefreshAssignments();
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnEnable()
    {
        if (UnitManager.Instance != null)
        {
            UnitManager.OnUnitCountChanged += OnUnitCountChanged;
        }

        RefreshAssignments();
    }

    private void OnDisable()
    {
        if (UnitManager.Instance != null)
        {
            UnitManager.OnUnitCountChanged -= OnUnitCountChanged;
        }
    }

    private void OnUnitCountChanged(UnitBase _)
    {
        RefreshAssignments();
    }

    public IReadOnlyList<ResourceType> BaseResourceTypes => BaseResourceOrder;

    public int[] GetDirectCountsCopy()
    {
        return (int[])directCounts.Clone();
    }

    public void SetDirectCounts(int[] counts)
    {
        if (counts == null || counts.Length != 4)
        {
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            directCounts[i] = Mathf.Clamp(counts[i], 0, 100);
        }

        RefreshAssignments();
    }

    public int[] GetRatioWeightsCopy()
    {
        return (int[])ratioWeights.Clone();
    }

    public void SetRatioWeights(int[] weights)
    {
        if (weights == null || weights.Length != 4)
        {
            return;
        }

        for (int i = 0; i < 4; i++)
        {
            ratioWeights[i] = Mathf.Clamp(weights[i], 0, 100);
        }

        RefreshAssignments();
    }

    public void RefreshAssignments()
    {
        if (UnitManager.Instance == null)
        {
            return;
        }

        List<Unit_Miner> miners = UnitManager.Instance.AllyUnits
            .OfType<Unit_Miner>()
            .Where(m => m != null)
            .OrderBy(m => m.GetInstanceID())
            .ToList();

        int n = miners.Count;
        int[] slotCounts = ComputeSlotCounts(n);
        for (int i = 0; i < 4; i++)
        {
            _lastSlotCounts[i] = slotCounts[i];
        }

        List<ResourceType> unionForManager = BuildUnionMineableTypes(slotCounts);
        UnitManager.Instance.UpdateAllLifterMineableTypes(unionForManager);

        if (n == 0)
        {
            return;
        }

        ResourceType[] flex = BuildFlexibleMineableTypes(slotCounts);
        foreach (Unit_Miner miner in miners)
        {
            miner.ApplyMineableTypes(flex);
        }
    }

    private ResourceType[] BuildFlexibleMineableTypes(int[] slotCounts)
    {
        List<ResourceType> list = new List<ResourceType>();
        for (int i = 0; i < 4; i++)
        {
            if (slotCounts[i] > 0)
            {
                list.Add(BaseResourceOrder[i]);
            }
        }

        if (list.Count == 0)
        {
            return (ResourceType[])BaseResourceOrder.Clone();
        }

        return list.ToArray();
    }

    private List<ResourceType> BuildUnionMineableTypes(int[] slotCounts)
    {
        HashSet<ResourceType> set = new HashSet<ResourceType>();
        for (int i = 0; i < 4; i++)
        {
            if (slotCounts[i] > 0)
            {
                set.Add(BaseResourceOrder[i]);
            }
        }

        if (set.Count == 0)
        {
            foreach (ResourceType t in BaseResourceOrder)
            {
                set.Add(t);
            }
        }

        return BaseResourceOrder.Where(set.Contains).ToList();
    }

    private int[] ComputeSlotCounts(int minerCount)
    {
        int[] result = { 0, 0, 0, 0 };
        if (minerCount <= 0)
        {
            return result;
        }

        if (ratioMode)
        {
            int sumW = ratioWeights.Sum();
            if (sumW <= 0)
            {
                DistributeEqual(minerCount, result);
                return result;
            }

            int assigned = 0;
            float[] exact = new float[4];
            for (int i = 0; i < 4; i++)
            {
                exact[i] = minerCount * (float)ratioWeights[i] / sumW;
                result[i] = Mathf.FloorToInt(exact[i]);
                assigned += result[i];
            }

            int remainder = minerCount - assigned;
            int r = 0;
            while (remainder > 0)
            {
                result[r % 4]++;
                remainder--;
                r++;
            }

            return result;
        }

        int sumDirect = directCounts.Sum();
        if (sumDirect <= 0)
        {
            DistributeEqual(minerCount, result);
            return result;
        }

        if (sumDirect == minerCount)
        {
            for (int i = 0; i < 4; i++)
            {
                result[i] = directCounts[i];
            }

            return result;
        }

        if (sumDirect > minerCount)
        {
            float scale = minerCount / (float)sumDirect;
            int assigned = 0;
            float[] frac = new float[4];
            for (int i = 0; i < 4; i++)
            {
                frac[i] = directCounts[i] * scale;
                result[i] = Mathf.FloorToInt(frac[i]);
                assigned += result[i];
            }

            int rem = minerCount - assigned;
            int idx = 0;
            while (rem > 0)
            {
                result[idx % 4]++;
                rem--;
                idx++;
            }

            return result;
        }

        for (int i = 0; i < 4; i++)
        {
            result[i] = directCounts[i];
        }

        int shortfall = minerCount - sumDirect;
        int s = 0;
        while (shortfall > 0)
        {
            result[s % 4]++;
            shortfall--;
            s++;
        }

        return result;
    }

    private static void DistributeEqual(int minerCount, int[] result)
    {
        int baseEach = minerCount / 4;
        int rem = minerCount % 4;
        for (int i = 0; i < 4; i++)
        {
            result[i] = baseEach + (i < rem ? 1 : 0);
        }
    }
}
