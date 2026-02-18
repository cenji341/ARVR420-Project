using System;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

[CreateAssetMenu(fileName = "WeaponData", menuName = "ScriptableObjects/WeaponData")]
public class WeaponData : ScriptableObject
{
    [Serializable]
    public class AttachmentModifier
    {
        [InspectorName("TargetValueName")]
        public string targetValueName;

        [InspectorName("TargetValueValue")]
        public string targetValueValue;

        [InspectorName("Add")]
        public bool Add;

        [InspectorName("Set")]
        public bool Set = true;
    }

    [Serializable]
    public class AttachmentData
    {
        [InspectorName("Attachment Name")]
        public string attachmentName;

        [InspectorName("Is Unlocked")]
        public bool isUnlocked;

        [InspectorName("Is Equipped")]
        public bool isEquipped;

        [InspectorName("Attachment Prefab")]
        public GameObject attachmentPrefab;

        [InspectorName("Equipped Local Position")]
        public Vector3 equippedLocalPosition;

        [InspectorName("Equipped Local Rotation (Euler)")]
        public Vector3 equippedLocalRotationEuler;

        [InspectorName("Aim Down Sight Local Position")]
        public Vector3 aimDownSightLocalPosition;

        [InspectorName("EquippedAttachmentModifiers")]
        public List<AttachmentModifier> equippedAttachmentModifiers = new List<AttachmentModifier>();
    }

    [Header("Magazine")]
    public List<AttachmentData> magazine = new List<AttachmentData>();

    [Header("Foregrip")]
    public List<AttachmentData> foregrip = new List<AttachmentData>();

    [Header("Picatinny")]
    public List<AttachmentData> picatinny = new List<AttachmentData>();

    [Header("Scope")]
    public List<AttachmentData> scope = new List<AttachmentData>();

    [Header("Muzzle")]
    public List<AttachmentData> muzzle = new List<AttachmentData>();

    void OnValidate()
    {
        EnforceSectionRules(magazine);
        EnforceSectionRules(foregrip);
        EnforceSectionRules(picatinny);
        EnforceSectionRules(scope);
        EnforceSectionRules(muzzle);
    }

    void EnforceSectionRules(List<AttachmentData> list)
    {
        if (list == null) return;

        int equippedIndex = -1;

        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a == null) continue;

            if (!a.isUnlocked && a.isEquipped)
                a.isEquipped = false;

            if (a.isEquipped)
            {
                if (equippedIndex == -1) equippedIndex = i;
                else a.isEquipped = false;
            }
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WeaponData))]
public class WeaponDataEditor : Editor
{
    bool showMagazine = true;
    bool showForegrip = true;
    bool showPicatinny = true;
    bool showScope = true;
    bool showMuzzle = true;

    SerializedProperty magazineProp;
    SerializedProperty foregripProp;
    SerializedProperty picatinnyProp;
    SerializedProperty scopeProp;
    SerializedProperty muzzleProp;

    void OnEnable()
    {
        magazineProp = serializedObject.FindProperty("magazine");
        foregripProp = serializedObject.FindProperty("foregrip");
        picatinnyProp = serializedObject.FindProperty("picatinny");
        scopeProp = serializedObject.FindProperty("scope");
        muzzleProp = serializedObject.FindProperty("muzzle");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        showMagazine = EditorGUILayout.Foldout(showMagazine, "Magazine", true);
        if (showMagazine)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(magazineProp, true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        showForegrip = EditorGUILayout.Foldout(showForegrip, "Foregrip", true);
        if (showForegrip)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(foregripProp, true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        showPicatinny = EditorGUILayout.Foldout(showPicatinny, "Picatinny", true);
        if (showPicatinny)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(picatinnyProp, true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        showScope = EditorGUILayout.Foldout(showScope, "Scope", true);
        if (showScope)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(scopeProp, true);
            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        showMuzzle = EditorGUILayout.Foldout(showMuzzle, "Muzzle", true);
        if (showMuzzle)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(muzzleProp, true);
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
