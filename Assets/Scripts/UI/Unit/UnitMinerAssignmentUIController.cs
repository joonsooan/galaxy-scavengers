using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
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

    private void OnEnable()
    {
        if (assignmentSystem == null)
        {
            assignmentSystem = FindFirstObjectByType<MinerAssignmentSystem>(FindObjectsInactive.Include);
        }

        ConfigureInputFields();
        RebuildTabOrder();
        WireListeners(true);
        RefreshUIFromSystem();
    }

    private void OnDisable()
    {
        WireListeners(false);
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

            field.contentType = TMP_InputField.ContentType.IntegerNumber;
            field.characterValidation = TMP_InputField.CharacterValidation.Integer;
            field.characterLimit = 3;
        }
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
            for (int i = 0; i < countInputs.Length && i < 4; i++)
            {
                TMP_InputField field = countInputs[i];
                if (field == null)
                {
                    continue;
                }

                field.onEndEdit.RemoveListener(OnCountEndEdit);
                if (add)
                {
                    field.onEndEdit.AddListener(OnCountEndEdit);
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

    private void OnRatioModeChanged(bool _)
    {
        if (_suppress || assignmentSystem == null)
        {
            return;
        }

        bool isRatioOn = ratioModeToggle != null && ratioModeToggle.isOn;
        if (isRatioOn)
        {
            assignmentSystem.RatioMode = true;
        }
        else
        {
            assignmentSystem.SetDirectCounts(assignmentSystem.LastSlotCounts.ToArray());
            assignmentSystem.RatioMode = false;
        }
        RefreshUIFromSystem();
    }

    private void OnCountEndEdit(string _)
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

        int[] next = new int[4];
        if (countInputs != null)
        {
            for (int i = 0; i < 4 && i < countInputs.Length; i++)
            {
                TMP_InputField f = countInputs[i];
                if (f == null)
                {
                    continue;
                }

                next[i] = ParseInput(f.text);
            }
        }

        assignmentSystem.SetDirectCounts(next);
        RefreshUIFromSystem();
    }

    private void OnRatioEndEdit(string _)
    {
        if (_suppress || assignmentSystem == null)
        {
            return;
        }

        int[] next = new int[4];
        if (ratioInputs != null)
        {
            for (int i = 0; i < 4 && i < ratioInputs.Length; i++)
            {
                TMP_InputField f = ratioInputs[i];
                if (f == null)
                {
                    continue;
                }

                next[i] = ParseInput(f.text);
            }
        }

        assignmentSystem.SetRatioWeights(next);
        RefreshUIFromSystem();
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

        bool ratio = assignmentSystem.RatioMode;
        if (countInputs != null)
        {
            for (int i = 0; i < 4 && i < countInputs.Length; i++)
            {
                TMP_InputField f = countInputs[i];
                if (f == null)
                {
                    continue;
                }

                int display = ratio ? lastSlots[i] : direct[i];
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
