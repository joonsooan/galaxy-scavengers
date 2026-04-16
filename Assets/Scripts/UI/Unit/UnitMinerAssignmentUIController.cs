using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class UnitMinerAssignmentUIController : MonoBehaviour
{
    [SerializeField] private MinerAssignmentSystem assignmentSystem;

    [SerializeField] private Toggle ratioModeToggle;

    [SerializeField] private TMP_InputField[] countInputs;
    [SerializeField] private TMP_InputField[] ratioInputs;

    private bool _suppress;
    private readonly List<TMP_InputField> _tabOrderedInputs = new List<TMP_InputField>();
    private UnityAction<string>[] _countEndEditHandlers;

    private void OnEnable()
    {
        if (assignmentSystem == null)
        {
            assignmentSystem = FindFirstObjectByType<MinerAssignmentSystem>(FindObjectsInactive.Include);
        }

        UnitManager.OnUnitCountChanged += OnAllyUnitCountChanged;

        ConfigureInputFields();
        RebuildTabOrder();
        WireListeners(true);
        RefreshUIFromSystem();
    }

    private void OnDisable()
    {
        UnitManager.OnUnitCountChanged -= OnAllyUnitCountChanged;

        WireListeners(false);
    }

    private void OnAllyUnitCountChanged(UnitBase _)
    {
        assignmentSystem?.RefreshAssignments();
        RefreshUIFromSystem();
    }

    private void Update()
    {
        HandleTabNavigation();
    }

    private void ConfigureInputFields()
    {
        ApplyNumericInputRules(countInputs);
        ApplyNumericInputRules(ratioInputs);
    }

    private void ApplyNumericInputRules(TMP_InputField[] inputs)
    {
        if (inputs == null)
        {
            return;
        }

        for (int i = 0; i < inputs.Length; i++)
        {
            TMP_InputField field = inputs[i];
            if (field == null)
            {
                continue;
            }

            field.contentType = TMP_InputField.ContentType.Custom;
            field.characterValidation = TMP_InputField.CharacterValidation.None;
            field.onValidateInput = ValidateDigitsOnly;
            field.characterLimit = 3;
        }
    }

    private static char ValidateDigitsOnly(string _, int __, char addedChar)
    {
        return char.IsDigit(addedChar) ? addedChar : '\0';
    }

    private void RebuildTabOrder()
    {
        _tabOrderedInputs.Clear();
        AppendInputs(countInputs);
        AppendInputs(ratioInputs);
    }

    private void AppendInputs(TMP_InputField[] inputs)
    {
        if (inputs == null)
        {
            return;
        }

        for (int i = 0; i < inputs.Length; i++)
        {
            TMP_InputField field = inputs[i];
            if (field != null)
            {
                _tabOrderedInputs.Add(field);
            }
        }
    }

    private void HandleTabNavigation()
    {
        if (!Input.GetKeyDown(KeyCode.Tab) || EventSystem.current == null || _tabOrderedInputs.Count == 0)
        {
            return;
        }

        GameObject selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null)
        {
            return;
        }

        TMP_InputField currentField = selected.GetComponent<TMP_InputField>();
        if (currentField == null)
        {
            return;
        }

        int currentIndex = _tabOrderedInputs.IndexOf(currentField);
        if (currentIndex < 0)
        {
            return;
        }

        bool reverse = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        int delta = reverse ? -1 : 1;
        int nextIndex = (currentIndex + delta + _tabOrderedInputs.Count) % _tabOrderedInputs.Count;
        TMP_InputField nextField = _tabOrderedInputs[nextIndex];
        if (nextField == null)
        {
            return;
        }

        EventSystem.current.SetSelectedGameObject(nextField.gameObject);
        nextField.ActivateInputField();
        nextField.MoveTextEnd(false);
    }

    private void EnsureCountEndHandlers()
    {
        if (_countEndEditHandlers != null)
        {
            return;
        }

        _countEndEditHandlers = new UnityAction<string>[4];
        for (int i = 0; i < 4; i++)
        {
            int slotIndex = i;
            _countEndEditHandlers[i] = text => OnCountEndEditAtIndex(slotIndex, text);
        }
    }

    private void WireListeners(bool add)
    {
        if (ratioModeToggle != null)
        {
            ratioModeToggle.onValueChanged.RemoveListener(OnRatioModeChanged);
            if (add)
            {
                ratioModeToggle.onValueChanged.AddListener(OnRatioModeChanged);
            }
        }

        if (countInputs != null)
        {
            EnsureCountEndHandlers();
            for (int i = 0; i < countInputs.Length && i < 4; i++)
            {
                TMP_InputField field = countInputs[i];
                if (field == null)
                {
                    continue;
                }

                UnityAction<string> handler = _countEndEditHandlers[i];
                field.onEndEdit.RemoveListener(handler);
                if (add)
                {
                    field.onEndEdit.AddListener(handler);
                }
            }
        }

        if (ratioInputs != null)
        {
            for (int i = 0; i < ratioInputs.Length && i < 4; i++)
            {
                TMP_InputField field = ratioInputs[i];
                if (field == null)
                {
                    continue;
                }

                field.onEndEdit.RemoveListener(OnRatioEndEdit);
                if (add)
                {
                    field.onEndEdit.AddListener(OnRatioEndEdit);
                }
            }
        }
    }

    private void OnRatioModeChanged(bool isRatioOn)
    {
        if (_suppress || assignmentSystem == null)
        {
            return;
        }

        if (isRatioOn)
        {
            int[] weights = ReadRatioInputsAsIntArray();
            assignmentSystem.SetRatioWeights(weights);
            assignmentSystem.RatioMode = true;
        }
        else
        {
            assignmentSystem.SetDirectCounts(assignmentSystem.LastSlotCounts.ToArray());
            assignmentSystem.RatioMode = false;
        }

        RefreshUIFromSystem();
    }

    private void OnCountEndEditAtIndex(int editIndex, string editedText)
    {
        if (_suppress || assignmentSystem == null)
        {
            return;
        }

        if (assignmentSystem.RatioMode)
        {
            RefreshUIFromSystem();
            return;
        }

        if (editIndex < 0 || editIndex >= 4)
        {
            return;
        }

        int minerCount = UnitManager.Instance != null
            ? UnitManager.Instance.AllyUnits.Count(u => u is Unit_Miner)
            : 0;

        int[] previous = assignmentSystem.GetDirectCountsCopy();
        int requested = ParseInput(editedText);
        int allocated = Mathf.Min(requested, minerCount);
        int remaining = minerCount - allocated;

        int[] next = new int[4];
        next[editIndex] = allocated;

        int otherCount = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i != editIndex)
            {
                otherCount++;
            }
        }

        int[] weights = new int[otherCount];
        int[] otherIndices = new int[otherCount];
        int w = 0;
        for (int i = 0; i < 4; i++)
        {
            if (i != editIndex)
            {
                otherIndices[w] = i;
                weights[w] = previous[i];
                w++;
            }
        }

        int[] distributed = DistributeIntegersByWeights(remaining, weights);
        for (int j = 0; j < otherCount; j++)
        {
            next[otherIndices[j]] = distributed[j];
        }

        assignmentSystem.SetDirectCounts(next);
        RefreshUIFromSystem();
    }

    private static int[] DistributeIntegersByWeights(int total, int[] weights)
    {
        if (weights == null || weights.Length == 0)
        {
            return System.Array.Empty<int>();
        }

        int n = weights.Length;
        int[] result = new int[n];
        if (total <= 0)
        {
            return result;
        }

        int sumW = 0;
        for (int i = 0; i < n; i++)
        {
            sumW += weights[i];
        }

        if (sumW <= 0)
        {
            return SplitEqualIntegers(total, n);
        }

        int assigned = 0;
        for (int i = 0; i < n; i++)
        {
            result[i] = Mathf.FloorToInt(total * (float)weights[i] / sumW);
            assigned += result[i];
        }

        int rem = total - assigned;
        int idx = 0;
        while (rem > 0)
        {
            result[idx % n]++;
            rem--;
            idx++;
        }

        return result;
    }

    private static int[] SplitEqualIntegers(int total, int count)
    {
        int[] result = new int[count];
        if (count <= 0)
        {
            return result;
        }

        int baseEach = total / count;
        int rem = total % count;
        for (int i = 0; i < count; i++)
        {
            result[i] = baseEach + (i < rem ? 1 : 0);
        }

        return result;
    }

    private void OnRatioEndEdit(string _)
    {
        if (_suppress || assignmentSystem == null)
        {
            return;
        }

        assignmentSystem.SetRatioWeights(ReadRatioInputsAsIntArray());
        RefreshUIFromSystem();
    }

    private int[] ReadRatioInputsAsIntArray()
    {
        int[] next = new int[4];
        if (ratioInputs == null)
        {
            return next;
        }

        for (int i = 0; i < 4 && i < ratioInputs.Length; i++)
        {
            TMP_InputField f = ratioInputs[i];
            if (f == null)
            {
                continue;
            }

            next[i] = ParseInput(f.text);
        }

        return next;
    }

    public void RefreshUIFromSystem()
    {
        if (assignmentSystem == null)
        {
            return;
        }

        _suppress = true;
        if (ratioModeToggle != null)
        {
            ratioModeToggle.SetIsOnWithoutNotify(assignmentSystem.RatioMode);
        }

        int[] direct = assignmentSystem.GetDirectCountsCopy();
        int[] ratios = assignmentSystem.GetRatioWeightsCopy();
        IReadOnlyList<int> lastSlots = assignmentSystem.LastSlotCounts;

        if (countInputs != null)
        {
            for (int i = 0; i < 4 && i < countInputs.Length; i++)
            {
                TMP_InputField f = countInputs[i];
                if (f == null)
                {
                    continue;
                }

                int display = lastSlots[i];
                f.text = Mathf.Clamp(display, 0, 100).ToString();
            }
        }

        if (ratioInputs != null)
        {
            for (int i = 0; i < 4 && i < ratioInputs.Length; i++)
            {
                TMP_InputField f = ratioInputs[i];
                if (f == null)
                {
                    continue;
                }

                f.text = Mathf.Clamp(ratios[i], 0, 100).ToString();
            }
        }

        _suppress = false;
    }

    private static int ParseInput(string raw)
    {
        if (!int.TryParse(raw, out int parsed))
        {
            return 0;
        }

        return Mathf.Clamp(parsed, 0, 100);
    }
}
