using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(TechData))]
public class TechDataEditor : Editor
{
    private SerializedProperty _techIndex;
    private SerializedProperty _useExistingData;
    private SerializedProperty _existingDisplayableData;
    private SerializedProperty _existingUnitData;
    private SerializedProperty _manualName;
    private SerializedProperty _manualDescription;
    private SerializedProperty _manualIcon;
    private SerializedProperty _researchCosts;
    private SerializedProperty _researchDuration;
    private SerializedProperty _prerequisiteTechIndices;
    private SerializedProperty _successorTechIndices;

    private void OnEnable()
    {
        _techIndex = serializedObject.FindProperty("techIndex");
        _useExistingData = serializedObject.FindProperty("useExistingData");
        _existingDisplayableData = serializedObject.FindProperty("existingDisplayableData");
        _existingUnitData = serializedObject.FindProperty("existingUnitData");
        _manualName = serializedObject.FindProperty("manualName");
        _manualDescription = serializedObject.FindProperty("manualDescription");
        _manualIcon = serializedObject.FindProperty("manualIcon");
        _researchCosts = serializedObject.FindProperty("researchCosts");
        _researchDuration = serializedObject.FindProperty("researchDuration");
        _prerequisiteTechIndices = serializedObject.FindProperty("prerequisiteTechIndices");
        _successorTechIndices = serializedObject.FindProperty("successorTechIndices");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(_techIndex);
        EditorGUILayout.PropertyField(_useExistingData);

        bool useExisting = _useExistingData.boolValue;

        if (useExisting)
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Existing Data Reference", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_existingDisplayableData);
            EditorGUILayout.PropertyField(_existingUnitData);
        }
        else
        {
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Manual Data", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(_manualName);
            EditorGUILayout.PropertyField(_manualDescription);
            EditorGUILayout.PropertyField(_manualIcon);
        }

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Research Cost", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_researchCosts, true);
        EditorGUILayout.PropertyField(_researchDuration);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Tech Tree", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_prerequisiteTechIndices, true);
        EditorGUILayout.PropertyField(_successorTechIndices, true);

        serializedObject.ApplyModifiedProperties();
    }
}
