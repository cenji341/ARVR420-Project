using System;
using UnityEngine;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "ControlData", menuName = "ScriptableObjects/ControlData")]
public class ControlData : ScriptableObject
{
    [Serializable]
    public class ControlValue
    {
        [InspectorName("Action Name")]
        public string actionName;

        [InspectorName("Is Keybind")]
        public bool isKeybind = true;

        [InspectorName("Use Mouse Button")]
        public bool useMouseButton;

        [Header("Keyboard (Input System)")]
        [InspectorName("Default Key")]
        public Key defaultKey = Key.None;

        [InspectorName("Bound Key")]
        public Key boundKey = Key.None;

        [Header("Mouse (Index)")]
        [InspectorName("Default Mouse Button")]
        public int defaultMouseButton = 0;

        [InspectorName("Bound Mouse Button")]
        public int boundMouseButton = 0;
    }

    [SerializeField, InspectorName("Control Values")]
    public ControlValue[] controlValues;

    [SerializeField, InspectorName("Look Speed")]
    public float lookSpeed = 5.0f;
}

#if UNITY_EDITOR
[CustomEditor(typeof(ControlData))]
public class ControlDataEditor : Editor
{
    bool showControlSettings = true;

    SerializedProperty controlValuesProp;
    SerializedProperty lookSpeedProp;

    void OnEnable()
    {
        controlValuesProp = serializedObject.FindProperty("controlValues");
        lookSpeedProp = serializedObject.FindProperty("lookSpeed");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        showControlSettings = EditorGUILayout.Foldout(showControlSettings, "Control Settings", true);
        if (showControlSettings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(controlValuesProp, true);
            EditorGUILayout.PropertyField(lookSpeedProp, new GUIContent("Look Speed"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
