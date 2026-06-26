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
    private SerializedProperty _isUnlockedByDefault;
    private SerializedProperty _unlocksBuildings;
    private SerializedProperty _unlocksUnits;
    private SerializedProperty _unlocksResources;
    private SerializedProperty _grantStatTypes;
    private SerializedProperty _grantStatValues;

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
        _isUnlockedByDefault = serializedObject.FindProperty("isUnlockedByDefault");
        _unlocksBuildings = serializedObject.FindProperty("unlocksBuildings");
        _unlocksUnits = serializedObject.FindProperty("unlocksUnits");
        _unlocksResources = serializedObject.FindProperty("unlocksResources");
        _grantStatTypes = serializedObject.FindProperty("grantStatTypes");
        _grantStatValues = serializedObject.FindProperty("grantStatValues");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        TechData tech = (TechData)target;

        // Live preview — visible without entering Play mode
        using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
        {
            EditorGUILayout.LabelField("Preview", EditorStyles.boldLabel);

            Sprite icon = tech.GetTechIcon();
            if (icon != null)
            {
                Texture2D preview = AssetPreview.GetAssetPreview(icon);
                if (preview != null)
                {
                    Rect iconRect = GUILayoutUtility.GetRect(64, 64, GUILayout.ExpandWidth(false));
                    GUI.DrawTexture(iconRect, preview, ScaleMode.ScaleToFit);
                }
            }

            string techName = tech.GetTechName();
            EditorGUILayout.LabelField("Name", string.IsNullOrEmpty(techName) ? "(none)" : techName);

            string techDesc = tech.GetTechDescription();
            EditorGUILayout.LabelField("Description", string.IsNullOrEmpty(techDesc) ? "(none)" : techDesc, EditorStyles.wordWrappedLabel);
        }

        EditorGUILayout.Space();

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

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Unlock on Completion", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_isUnlockedByDefault);
        EditorGUILayout.PropertyField(_unlocksBuildings, true);
        EditorGUILayout.PropertyField(_unlocksUnits, true);
        EditorGUILayout.PropertyField(_unlocksResources, true);

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Stat Bonuses on Completion", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(_grantStatTypes, true);
        EditorGUILayout.PropertyField(_grantStatValues, true);

        serializedObject.ApplyModifiedProperties();
    }
}
