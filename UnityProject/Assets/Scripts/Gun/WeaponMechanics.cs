using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class WeaponMechanics : MonoBehaviour
{
    public enum FireMode
    {
        Safe,
        SemiAuto,
        FullAuto
    }

    public enum WeaponInHand
    {
        Right,
        Left
    }

    [Header("Data")]
    public WeaponData weaponData;

    [Header("Controls")]
    public ControlData controlData;

    [Header("Runtime")]
    public bool clearSpawnedOnStart = true;

    [Header("Fire Mode Settings")]
    public GameObject FireControlSwitch;

    [InspectorName("Fire Mode Text Object")]
    public TMP_Text fireModeTextObject;

    public bool AllowSafe = true;
    [SerializeField] float safeSwitchRotationX = 0f;

    public bool AllowSemiAuto = true;
    [SerializeField] float semiAutoSwitchRotationX = 0f;

    public bool AllowFullAuto = false;
    [SerializeField] float fullAutoSwitchRotationX = 0f;

    [SerializeField]
    public FireMode fireMode = FireMode.Safe;

    [InspectorName("Next Fire Mode")]
    public UnityEvent onNextFireMode;

    [Header("Ammo Settings")]
    [SerializeField, InspectorName("Magazine Capacity")]
    int magazineCapacity;

    [SerializeField, InspectorName("Current Magazine Ammo")]
    int currentMagazineAmmo;

    [SerializeField, InspectorName("Reserve Ammo")]
    int reserveAmmo;

    [SerializeField, InspectorName("Max Reserve Ammo")]
    public int maxReserveAmmo;

    [SerializeField, InspectorName("MagazineAmmoText")]
    TMP_Text MagazineAmmoText;

    [SerializeField, InspectorName("AmmoReserveText")]
    TMP_Text AmmoReserveText;

    [Header("Ergonomics Settings")]
    [SerializeField, InspectorName("Reload Duration")]
    float reloadDuration = 0f;

    [SerializeField, InspectorName("Aim In Duration")]
    float aimInDuration = 0.5f;

    [SerializeField, InspectorName("Aim Out Duration")]
    float aimOutDuration = 0.4f;

    [SerializeField, InspectorName("Weapon In Hand")]
    WeaponInHand weaponInHand = WeaponInHand.Right;

    [SerializeField, InspectorName("Hand Swap Duration")]
    float handSwapDuration = 0f;

    [SerializeField, InspectorName("Left Hand Local Position")]
    Vector3 leftHandLocalPosition = Vector3.zero;

    [SerializeField, InspectorName("Right Hand Local Position")]
    Vector3 rightHandLocalPosition = Vector3.zero;

    [SerializeField, InspectorName("Aim Down Sight Local Position")]
    Vector3 aimDownSightLocalPosition = Vector3.zero;

    [SerializeField, InspectorName("Is Aiming")]
    bool isAiming = false;

    [Header("ReloadEvents")]
    [InspectorName("MagDropEvent")]
    public UnityEvent MagDropEvent;

    [InspectorName("MagInsertEvent")]
    public UnityEvent MagInsertEvent;

    struct Binding
    {
        public bool useMouse;
        public Key key;
        public int mouseButton;
    }

    readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>(16);

    List<GameObject> spawnedAttachments = new List<GameObject>();

    GameObject spawnedMagazine;
    Vector3 magazineEquippedLocalPosition;
    Coroutine reloadRoutine;

    Coroutine handSwapRoutine;
    Coroutine aimRoutine;

    FireMode lastFireMode;
    Coroutine switchRotateRoutine;

    bool lastAimHeld;

    void Start()
    {
        LoadBindingsFromControlData();

        if (clearSpawnedOnStart)
            ClearSpawnedAttachments();

        SpawnEquippedAttachments();

        ApplyEquippedAttachmentModifiers();

        EnforceFireModeAllowed();

        if (onNextFireMode == null) onNextFireMode = new UnityEvent();

        if (MagDropEvent == null) MagDropEvent = new UnityEvent();
        if (MagInsertEvent == null) MagInsertEvent = new UnityEvent();

        lastFireMode = fireMode;
        ApplySwitchRotationImmediate();
        UpdateFireModeText();

        Initialize();

        UpdateAmmoTexts();
    }

    public void Initialize()
    {
        currentMagazineAmmo = magazineCapacity;
        reserveAmmo = maxReserveAmmo;
        UpdateAmmoTexts();
    }

    public void DebugSetAmmo(int value)
    {
        currentMagazineAmmo = value;
        UpdateAmmoTexts();
    }

    void OnValidate()
    {
        EnforceFireModeAllowed();
    }

    void Update()
    {
        if (fireMode != lastFireMode)
        {
            EnforceFireModeAllowed();

            if (fireMode != lastFireMode)
            {
                AnimateFireControlSwitchToMode(fireMode);
                lastFireMode = fireMode;
                UpdateFireModeText();
            }
        }

        bool aimHeld = IsPressed("aimWeapon");
        if (aimHeld && !lastAimHeld)
        {
            LogAction("aimWeapon");
            StartAimIn();
        }
        else if (!aimHeld && lastAimHeld)
        {
            StartAimOut();
        }
        lastAimHeld = aimHeld;

        if (IsPressedDown("shootWeapon")) LogAction("shootWeapon");

        if (IsPressedDown("reloadWeapon"))
        {
            LogAction("reloadWeapon");
            PlayReloadMagazineAnimation();
        }

        if (IsPressedDown("checkAmmo")) LogAction("checkAmmo");

        if (IsPressedDown("cycleFireMode"))
        {
            LogAction("cycleFireMode");
            NextFireMode();
        }

        if (IsPressedDown("swapShoulder"))
        {
            LogAction("swapShoulder");
            TrySwapShoulder();
        }

        UpdateAmmoTexts();
    }

    void UpdateAmmoTexts()
    {
        if (MagazineAmmoText != null)
            MagazineAmmoText.text = currentMagazineAmmo.ToString() + "/" + magazineCapacity.ToString();

        if (AmmoReserveText != null)
            AmmoReserveText.text = reserveAmmo.ToString();
    }

    void StartAimIn()
    {
        isAiming = true;

        if (aimRoutine != null)
            StopCoroutine(aimRoutine);

        if (handSwapRoutine != null)
        {
            StopCoroutine(handSwapRoutine);
            handSwapRoutine = null;
        }

        aimRoutine = StartCoroutine(MoveWeaponToLocalPosition(aimDownSightLocalPosition, aimInDuration));
    }

    void StartAimOut()
    {
        isAiming = false;

        Vector3 target = weaponInHand == WeaponInHand.Right ? rightHandLocalPosition : leftHandLocalPosition;

        if (aimRoutine != null)
            StopCoroutine(aimRoutine);

        aimRoutine = StartCoroutine(MoveWeaponToLocalPosition(target, aimOutDuration));
    }

    void TrySwapShoulder()
    {
        if (isAiming) return;

        weaponInHand = weaponInHand == WeaponInHand.Right ? WeaponInHand.Left : WeaponInHand.Right;

        Vector3 target = weaponInHand == WeaponInHand.Right ? rightHandLocalPosition : leftHandLocalPosition;

        if (handSwapRoutine != null)
            StopCoroutine(handSwapRoutine);

        handSwapRoutine = StartCoroutine(MoveWeaponToLocalPosition(target, handSwapDuration));
    }

    IEnumerator MoveWeaponToLocalPosition(Vector3 targetLocalPos, float duration)
    {
        Transform t = transform;

        Vector3 start = t.localPosition;

        if (duration <= 0f)
        {
            t.localPosition = targetLocalPos;
            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float u = Mathf.Clamp01(time / duration);
            t.localPosition = Vector3.Lerp(start, targetLocalPos, u);
            yield return null;
        }

        t.localPosition = targetLocalPos;
    }

    void PlayReloadMagazineAnimation()
    {
        if (spawnedMagazine == null) return;
        if (reloadRoutine != null) return;
        if (reserveAmmo <= 0) return;

        reloadRoutine = StartCoroutine(ReloadMagazineRoutine());
    }

    IEnumerator ReloadMagazineRoutine()
    {
        MagDropEvent.Invoke();

        reserveAmmo += currentMagazineAmmo;

        Transform t = spawnedMagazine.transform;

        Vector3 equipped = magazineEquippedLocalPosition;

        Vector3 down = equipped;
        down.y = -0.25f;

        float total = reloadDuration;
        if (total <= 0f)
        {
            t.localPosition = down;
            t.localPosition = equipped;

            int load = Mathf.Min(magazineCapacity, reserveAmmo);
            reserveAmmo -= load;
            currentMagazineAmmo = load;

            UpdateAmmoTexts();

            MagInsertEvent.Invoke();
            reloadRoutine = null;
            yield break;
        }

        float half = total * 0.5f;

        yield return MoveLocalPositionOverTime(t, equipped, down, half);
        yield return MoveLocalPositionOverTime(t, down, equipped, half);

        t.localPosition = equipped;

        int load2 = Mathf.Min(magazineCapacity, reserveAmmo);
        reserveAmmo -= load2;
        currentMagazineAmmo = load2;

        UpdateAmmoTexts();

        MagInsertEvent.Invoke();

        reloadRoutine = null;
    }

    IEnumerator MoveLocalPositionOverTime(Transform t, Vector3 from, Vector3 to, float duration)
    {
        if (duration <= 0f)
        {
            t.localPosition = to;
            yield break;
        }

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float u = Mathf.Clamp01(time / duration);
            t.localPosition = Vector3.Lerp(from, to, u);
            yield return null;
        }

        t.localPosition = to;
    }

    public void NextFireMode()
    {
        FireMode start = fireMode;

        for (int i = 0; i < 3; i++)
        {
            fireMode = GetNextMode(fireMode);
            if (IsFireModeAllowed(fireMode))
                break;
        }

        if (!IsFireModeAllowed(fireMode))
            fireMode = start;

        AnimateFireControlSwitchToMode(fireMode);
        lastFireMode = fireMode;

        UpdateFireModeText();

        onNextFireMode.Invoke();
    }

    FireMode GetNextMode(FireMode mode)
    {
        if (mode == FireMode.Safe) return FireMode.SemiAuto;
        if (mode == FireMode.SemiAuto) return FireMode.FullAuto;
        return FireMode.Safe;
    }

    void EnforceFireModeAllowed()
    {
        if (IsFireModeAllowed(fireMode)) return;

        if (AllowSafe) { fireMode = FireMode.Safe; return; }
        if (AllowSemiAuto) { fireMode = FireMode.SemiAuto; return; }
        if (AllowFullAuto) { fireMode = FireMode.FullAuto; return; }

        fireMode = FireMode.Safe;
    }

    bool IsFireModeAllowed(FireMode mode)
    {
        if (mode == FireMode.Safe) return AllowSafe;
        if (mode == FireMode.SemiAuto) return AllowSemiAuto;
        if (mode == FireMode.FullAuto) return AllowFullAuto;
        return false;
    }

    void UpdateFireModeText()
    {
        if (fireModeTextObject == null) return;
        fireModeTextObject.text = fireMode.ToString();
    }

    void ApplySwitchRotationImmediate()
    {
        if (FireControlSwitch == null) return;

        float targetX = GetSwitchRotationForMode(fireMode);

        Vector3 e = FireControlSwitch.transform.localEulerAngles;
        e.x = targetX;
        FireControlSwitch.transform.localEulerAngles = e;
    }

    void AnimateFireControlSwitchToMode(FireMode mode)
    {
        if (FireControlSwitch == null) return;

        float targetX = GetSwitchRotationForMode(mode);

        if (switchRotateRoutine != null)
            StopCoroutine(switchRotateRoutine);

        switchRotateRoutine = StartCoroutine(RotateSwitchXOverTime(targetX, 0.1f));
    }

    float GetSwitchRotationForMode(FireMode mode)
    {
        if (mode == FireMode.Safe) return safeSwitchRotationX;
        if (mode == FireMode.SemiAuto) return semiAutoSwitchRotationX;
        return fullAutoSwitchRotationX;
    }

    IEnumerator RotateSwitchXOverTime(float targetX, float duration)
    {
        Transform t = FireControlSwitch.transform;

        Vector3 startE = t.localEulerAngles;
        float startX = startE.x;

        float time = 0f;
        while (time < duration)
        {
            time += Time.deltaTime;
            float u = duration <= 0f ? 1f : Mathf.Clamp01(time / duration);

            float x = Mathf.LerpAngle(startX, targetX, u);

            Vector3 e = t.localEulerAngles;
            e.x = x;
            t.localEulerAngles = e;

            yield return null;
        }

        Vector3 endE = t.localEulerAngles;
        endE.x = targetX;
        t.localEulerAngles = endE;

        switchRotateRoutine = null;
    }

    public void ApplyEquippedAttachmentModifiers()
    {
        if (weaponData == null) return;

        ApplyModifiersFromList(weaponData.magazine);
        ApplyModifiersFromList(weaponData.foregrip);
        ApplyModifiersFromList(weaponData.picatinny);
        ApplyModifiersFromList(weaponData.scope);
        ApplyModifiersFromList(weaponData.muzzle);
    }

    void ApplyModifiersFromList(List<WeaponData.AttachmentData> list)
    {
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a == null) continue;
            if (!a.isUnlocked) continue;
            if (!a.isEquipped) continue;

            if (a.equippedAttachmentModifiers == null) continue;

            for (int m = 0; m < a.equippedAttachmentModifiers.Count; m++)
            {
                var mod = a.equippedAttachmentModifiers[m];
                if (mod == null) continue;

                string targetName = mod.targetValueName ?? string.Empty;
                if (string.IsNullOrWhiteSpace(targetName)) continue;

                string valueStr = mod.targetValueValue ?? string.Empty;

                bool applied = TryApplyModifierToWeaponMechanics(targetName, valueStr, mod.Add, mod.Set);

                if (applied)
                    Debug.Log("Attachment Modifier Applied | " + targetName + " = " + valueStr + " | Add=" + mod.Add + " Set=" + mod.Set);
                else
                    Debug.Log("Attachment Modifier Failed | " + targetName + " = " + valueStr + " | Add=" + mod.Add + " Set=" + mod.Set);
            }
        }
    }

    bool TryApplyModifierToWeaponMechanics(string targetValueName, string targetValueValue, bool add, bool set)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        Type t = GetType();

        FieldInfo f = t.GetField(targetValueName, flags);
        if (f != null)
            return ApplyAddOrSetToMember(f.FieldType, () => f.GetValue(this), v => f.SetValue(this, v), targetValueValue, add, set);

        PropertyInfo p = t.GetProperty(targetValueName, flags);
        if (p != null && p.CanWrite)
            return ApplyAddOrSetToMember(p.PropertyType, () => p.GetValue(this), v => p.SetValue(this, v), targetValueValue, add, set);

        return false;
    }

    bool ApplyAddOrSetToMember(Type memberType, Func<object> getter, Action<object> setter, string valueStr, bool add, bool set)
    {
        if (!add && !set) return false;

        if (set)
        {
            if (TryConvertStringToType(valueStr, memberType, out object convertedSet))
            {
                setter(convertedSet);
                return true;
            }
            return false;
        }

        if (memberType == typeof(int))
        {
            if (!int.TryParse(valueStr, NumberStyles.Integer, CultureInfo.InvariantCulture, out int addVal)) return false;
            object curObj = getter();
            int cur = curObj == null ? 0 : (int)curObj;
            setter(cur + addVal);
            return true;
        }

        if (memberType == typeof(float))
        {
            if (!float.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out float addVal)) return false;
            object curObj = getter();
            float cur = curObj == null ? 0f : (float)curObj;
            setter(cur + addVal);
            return true;
        }

        if (memberType == typeof(double))
        {
            if (!double.TryParse(valueStr, NumberStyles.Float, CultureInfo.InvariantCulture, out double addVal)) return false;
            object curObj = getter();
            double cur = curObj == null ? 0d : (double)curObj;
            setter(cur + addVal);
            return true;
        }

        if (memberType == typeof(Vector3))
        {
            if (!TryParseVector3(valueStr, out Vector3 addVal)) return false;
            object curObj = getter();
            Vector3 cur = curObj == null ? Vector3.zero : (Vector3)curObj;
            setter(cur + addVal);
            return true;
        }

        return false;
    }

    bool TryConvertStringToType(string value, Type targetType, out object converted)
    {
        converted = null;

        if (targetType == typeof(string))
        {
            converted = value;
            return true;
        }

        if (targetType == typeof(bool))
        {
            if (bool.TryParse(value, out bool b))
            {
                converted = b;
                return true;
            }
            return false;
        }

        if (targetType == typeof(int))
        {
            if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out int i))
            {
                converted = i;
                return true;
            }
            return false;
        }

        if (targetType == typeof(float))
        {
            if (float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float f))
            {
                converted = f;
                return true;
            }
            return false;
        }

        if (targetType == typeof(double))
        {
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double d))
            {
                converted = d;
                return true;
            }
            return false;
        }

        if (targetType == typeof(Vector3))
        {
            if (TryParseVector3(value, out Vector3 v3))
            {
                converted = v3;
                return true;
            }
            return false;
        }

        if (targetType.IsEnum)
        {
            try
            {
                converted = Enum.Parse(targetType, value, true);
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }

    bool TryParseVector3(string s, out Vector3 v)
    {
        v = Vector3.zero;
        if (string.IsNullOrWhiteSpace(s)) return false;

        string cleaned = s.Trim();
        cleaned = cleaned.Replace("(", "").Replace(")", "");
        cleaned = cleaned.Replace("[", "").Replace("]", "");

        char[] seps = new char[] { ',', ' ' };
        string[] parts = cleaned.Split(seps, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length != 3) return false;

        if (!float.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out float x)) return false;
        if (!float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float y)) return false;
        if (!float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float z)) return false;

        v = new Vector3(x, y, z);
        return true;
    }

    void LogAction(string actionName)
    {
        string binding = GetBindingString(actionName);
        Debug.Log(actionName + " performed | Binding: " + binding);
    }

    string GetBindingString(string actionName)
    {
        if (!bindings.TryGetValue(actionName, out var b)) return "None";

        if (b.useMouse)
        {
            if (b.mouseButton == 0) return "Mouse0";
            if (b.mouseButton == 1) return "Mouse1";
            if (b.mouseButton == 2) return "Mouse2";
            if (b.mouseButton == 3) return "Mouse3";
            if (b.mouseButton == 4) return "Mouse4";
            return "Mouse" + b.mouseButton;
        }

        return b.key.ToString();
    }

    public void LoadBindingsFromControlData()
    {
        bindings.Clear();

        if (controlData == null) return;
        if (controlData.controlValues == null) return;

        for (int i = 0; i < controlData.controlValues.Length; i++)
        {
            var cv = controlData.controlValues[i];
            if (cv == null) continue;

            string action = cv.actionName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(action)) continue;

            if (!cv.isKeybind) continue;

            if (action != "aimWeapon" &&
                action != "shootWeapon" &&
                action != "reloadWeapon" &&
                action != "checkAmmo" &&
                action != "cycleFireMode" &&
                action != "swapShoulder")
                continue;

            Binding b = new Binding();

            if (cv.useMouseButton)
            {
                b.useMouse = true;
                b.mouseButton = cv.boundMouseButton;
            }
            else
            {
                b.useMouse = false;
                b.key = cv.boundKey != Key.None ? cv.boundKey : cv.defaultKey;
            }

            bindings[action] = b;
        }

        Debug.Log("Loaded aimWeapon | " + GetBindingString("aimWeapon"));
        Debug.Log("Loaded shootWeapon | " + GetBindingString("shootWeapon"));
        Debug.Log("Loaded reloadWeapon | " + GetBindingString("reloadWeapon"));
        Debug.Log("Loaded checkAmmo | " + GetBindingString("checkAmmo"));
        Debug.Log("Loaded cycleFireMode | " + GetBindingString("cycleFireMode"));
        Debug.Log("Loaded swapShoulder | " + GetBindingString("swapShoulder"));
    }

    bool IsPressedDown(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName)) return false;
        if (!bindings.TryGetValue(actionName, out var b)) return false;
        return IsPressedDown(b);
    }

    bool IsPressed(string actionName)
    {
        if (string.IsNullOrWhiteSpace(actionName)) return false;
        if (!bindings.TryGetValue(actionName, out var b)) return false;
        return IsPressed(b);
    }

    bool IsPressed(Binding b)
    {
        if (b.useMouse)
        {
            if (Mouse.current == null) return false;
            var btn = GetMouseButtonControl(b.mouseButton);
            return btn != null && btn.isPressed;
        }

        if (Keyboard.current == null) return false;
        var keyCtrl = Keyboard.current[b.key];
        return keyCtrl != null && keyCtrl.isPressed;
    }

    bool IsPressedDown(Binding b)
    {
        if (b.useMouse)
        {
            if (Mouse.current == null) return false;
            var btn = GetMouseButtonControl(b.mouseButton);
            return btn != null && btn.wasPressedThisFrame;
        }

        if (Keyboard.current == null) return false;
        var keyCtrl = Keyboard.current[b.key];
        return keyCtrl != null && keyCtrl.wasPressedThisFrame;
    }

    ButtonControl GetMouseButtonControl(int index)
    {
        if (Mouse.current == null) return null;

        if (index == 0) return Mouse.current.leftButton;
        if (index == 1) return Mouse.current.rightButton;
        if (index == 2) return Mouse.current.middleButton;
        if (index == 3) return Mouse.current.forwardButton;
        if (index == 4) return Mouse.current.backButton;

        return null;
    }

    public void SpawnEquippedAttachments()
    {
        if (weaponData == null) return;

        spawnedMagazine = null;
        magazineEquippedLocalPosition = Vector3.zero;

        SpawnFromList(weaponData.magazine);
        SpawnFromList(weaponData.foregrip);
        SpawnFromList(weaponData.picatinny);
        SpawnFromList(weaponData.scope);
        SpawnFromList(weaponData.muzzle);
    }

    void SpawnFromList(List<WeaponData.AttachmentData> list)
    {
        if (list == null) return;

        for (int i = 0; i < list.Count; i++)
        {
            var a = list[i];
            if (a == null) continue;
            if (!a.isUnlocked) continue;
            if (!a.isEquipped) continue;
            if (a.attachmentPrefab == null) continue;

            GameObject instance = Instantiate(a.attachmentPrefab, transform);
            instance.transform.localPosition = a.equippedLocalPosition;
            instance.transform.localEulerAngles = a.equippedLocalRotationEuler;

            spawnedAttachments.Add(instance);

            if (weaponData != null && ReferenceEquals(list, weaponData.magazine))
            {
                spawnedMagazine = instance;
                magazineEquippedLocalPosition = a.equippedLocalPosition;
            }

            if (weaponData != null && ReferenceEquals(list, weaponData.scope))
            {
                aimDownSightLocalPosition = a.aimDownSightLocalPosition;
            }
        }
    }

    public void ClearSpawnedAttachments()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        if (handSwapRoutine != null)
        {
            StopCoroutine(handSwapRoutine);
            handSwapRoutine = null;
        }

        if (aimRoutine != null)
        {
            StopCoroutine(aimRoutine);
            aimRoutine = null;
        }

        for (int i = spawnedAttachments.Count - 1; i >= 0; i--)
        {
            if (spawnedAttachments[i] != null)
                Destroy(spawnedAttachments[i]);
        }

        spawnedAttachments.Clear();

        spawnedMagazine = null;
        magazineEquippedLocalPosition = Vector3.zero;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(WeaponMechanics))]
public class WeaponMechanicsEditor : Editor
{
    bool showFireModeSettings = true;
    bool showAmmoSettings = true;
    bool showErgonomicsSettings = true;
    bool showReloadEvents = true;

    SerializedProperty weaponDataProp;
    SerializedProperty controlDataProp;
    SerializedProperty clearSpawnedOnStartProp;

    SerializedProperty fireControlSwitchProp;
    SerializedProperty fireModeTextObjectProp;

    SerializedProperty allowSafeProp;
    SerializedProperty allowSemiAutoProp;
    SerializedProperty allowFullAutoProp;

    SerializedProperty safeSwitchRotationXProp;
    SerializedProperty semiAutoSwitchRotationXProp;
    SerializedProperty fullAutoSwitchRotationXProp;

    SerializedProperty fireModeProp;
    SerializedProperty onNextFireModeProp;

    SerializedProperty magazineCapacityProp;
    SerializedProperty currentMagazineAmmoProp;
    SerializedProperty reserveAmmoProp;
    SerializedProperty maxReserveAmmoProp;
    SerializedProperty magazineAmmoTextProp;
    SerializedProperty ammoReserveTextProp;

    SerializedProperty reloadDurationProp;
    SerializedProperty aimInDurationProp;
    SerializedProperty aimOutDurationProp;
    SerializedProperty weaponInHandProp;
    SerializedProperty handSwapDurationProp;
    SerializedProperty leftHandLocalPositionProp;
    SerializedProperty rightHandLocalPositionProp;
    SerializedProperty aimDownSightLocalPositionProp;
    SerializedProperty isAimingProp;

    SerializedProperty magDropEventProp;
    SerializedProperty magInsertEventProp;

    void OnEnable()
    {
        weaponDataProp = serializedObject.FindProperty("weaponData");
        controlDataProp = serializedObject.FindProperty("controlData");
        clearSpawnedOnStartProp = serializedObject.FindProperty("clearSpawnedOnStart");

        fireControlSwitchProp = serializedObject.FindProperty("FireControlSwitch");
        fireModeTextObjectProp = serializedObject.FindProperty("fireModeTextObject");

        allowSafeProp = serializedObject.FindProperty("AllowSafe");
        allowSemiAutoProp = serializedObject.FindProperty("AllowSemiAuto");
        allowFullAutoProp = serializedObject.FindProperty("AllowFullAuto");

        safeSwitchRotationXProp = serializedObject.FindProperty("safeSwitchRotationX");
        semiAutoSwitchRotationXProp = serializedObject.FindProperty("semiAutoSwitchRotationX");
        fullAutoSwitchRotationXProp = serializedObject.FindProperty("fullAutoSwitchRotationX");

        fireModeProp = serializedObject.FindProperty("fireMode");
        onNextFireModeProp = serializedObject.FindProperty("onNextFireMode");

        magazineCapacityProp = serializedObject.FindProperty("magazineCapacity");
        currentMagazineAmmoProp = serializedObject.FindProperty("currentMagazineAmmo");
        reserveAmmoProp = serializedObject.FindProperty("reserveAmmo");
        maxReserveAmmoProp = serializedObject.FindProperty("maxReserveAmmo");
        magazineAmmoTextProp = serializedObject.FindProperty("MagazineAmmoText");
        ammoReserveTextProp = serializedObject.FindProperty("AmmoReserveText");

        reloadDurationProp = serializedObject.FindProperty("reloadDuration");
        aimInDurationProp = serializedObject.FindProperty("aimInDuration");
        aimOutDurationProp = serializedObject.FindProperty("aimOutDuration");
        weaponInHandProp = serializedObject.FindProperty("weaponInHand");
        handSwapDurationProp = serializedObject.FindProperty("handSwapDuration");
        leftHandLocalPositionProp = serializedObject.FindProperty("leftHandLocalPosition");
        rightHandLocalPositionProp = serializedObject.FindProperty("rightHandLocalPosition");
        aimDownSightLocalPositionProp = serializedObject.FindProperty("aimDownSightLocalPosition");
        isAimingProp = serializedObject.FindProperty("isAiming");

        magDropEventProp = serializedObject.FindProperty("MagDropEvent");
        magInsertEventProp = serializedObject.FindProperty("MagInsertEvent");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(weaponDataProp);
        EditorGUILayout.PropertyField(controlDataProp);
        EditorGUILayout.PropertyField(clearSpawnedOnStartProp);

        EditorGUILayout.Space(8);

        showFireModeSettings = EditorGUILayout.Foldout(showFireModeSettings, "Fire Mode Settings", true);
        if (showFireModeSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(fireModeProp, new GUIContent("Fire Mode"));
            EditorGUILayout.PropertyField(fireControlSwitchProp, new GUIContent("FireControlSwitch"));
            EditorGUILayout.PropertyField(fireModeTextObjectProp, new GUIContent("Fire Mode Text Object"));

            EditorGUILayout.Space(4);

            EditorGUILayout.PropertyField(allowSafeProp, new GUIContent("AllowSafe"));
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(safeSwitchRotationXProp, new GUIContent("Switch Rotation"));
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(allowSemiAutoProp, new GUIContent("AllowSemiAuto"));
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(semiAutoSwitchRotationXProp, new GUIContent("Switch Rotation"));
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(allowFullAutoProp, new GUIContent("AllowFullAuto"));
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(fullAutoSwitchRotationXProp, new GUIContent("Switch Rotation"));
            EditorGUI.indentLevel--;

            EditorGUILayout.PropertyField(onNextFireModeProp, new GUIContent("Next Fire Mode"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);

        showAmmoSettings = EditorGUILayout.Foldout(showAmmoSettings, "Ammo Settings", true);
        if (showAmmoSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUI.BeginDisabledGroup(true);
            EditorGUILayout.PropertyField(magazineCapacityProp, new GUIContent("Magazine Capacity"));
            EditorGUILayout.PropertyField(currentMagazineAmmoProp, new GUIContent("Current Magazine Ammo"));
            EditorGUILayout.PropertyField(reserveAmmoProp, new GUIContent("Reserve Ammo"));
            EditorGUI.EndDisabledGroup();

            EditorGUILayout.PropertyField(maxReserveAmmoProp, new GUIContent("Max Reserve Ammo"));
            EditorGUILayout.PropertyField(magazineAmmoTextProp, new GUIContent("MagazineAmmoText"));
            EditorGUILayout.PropertyField(ammoReserveTextProp, new GUIContent("AmmoReserveText"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);

        showErgonomicsSettings = EditorGUILayout.Foldout(showErgonomicsSettings, "Ergonomics Settings", true);
        if (showErgonomicsSettings)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(reloadDurationProp, new GUIContent("Reload Duration"));
            EditorGUILayout.PropertyField(aimInDurationProp, new GUIContent("Aim In Duration"));
            EditorGUILayout.PropertyField(aimOutDurationProp, new GUIContent("Aim Out Duration"));

            EditorGUILayout.PropertyField(weaponInHandProp, new GUIContent("Weapon In Hand"));
            EditorGUILayout.PropertyField(handSwapDurationProp, new GUIContent("Hand Swap Duration"));
            EditorGUILayout.PropertyField(leftHandLocalPositionProp, new GUIContent("Left Hand Local Position"));
            EditorGUILayout.PropertyField(rightHandLocalPositionProp, new GUIContent("Right Hand Local Position"));
            EditorGUILayout.PropertyField(aimDownSightLocalPositionProp, new GUIContent("Aim Down Sight Local Position"));
            EditorGUILayout.PropertyField(isAimingProp, new GUIContent("Is Aiming"));

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(8);

        showReloadEvents = EditorGUILayout.Foldout(showReloadEvents, "ReloadEvents", true);
        if (showReloadEvents)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(magDropEventProp, new GUIContent("MagDropEvent"));
            EditorGUILayout.PropertyField(magInsertEventProp, new GUIContent("MagInsertEvent"));

            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
