using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;

#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(CharacterController))]
public class PlayerController : MonoBehaviour
{
    [Header("References")]
    public ControlData controlData;

    [InspectorName("Player Camera")]
    public Transform playerCamera;

    [InspectorName("HeadRoot")]
    public Transform headRoot;

    [Header("Movement")]
    public float moveSpeed = 5f;

    [InspectorName("Min Move Speed")]
    public float minMoveSpeed = 2f;

    [InspectorName("Max Move Speed")]
    public float maxMoveSpeed = 10f;

    [InspectorName("Speed Change Step")]
    public float speedChangeStep = 1f;

    [Header("Lean")]
    [InspectorName("Lean Angle")]
    public float leanAngle = 30f;

    [InspectorName("Lean Rotation Speed")]
    public float leanRotationSpeed = 40f;

    CharacterController characterController;

    struct Binding
    {
        public bool useMouse;
        public Key key;
        public int mouseButton;
    }

    struct RuntimeInput
    {
        public string actionName;
        public Binding binding;
        public UnityEvent onPressed;
    }

    readonly List<RuntimeInput> runtimeInputs = new List<RuntimeInput>(64);
    readonly Dictionary<string, Binding> bindings = new Dictionary<string, Binding>(64);

    float lookSpeed = 5f;
    float cameraPitch;

    [Header("Input Events")]
    [InspectorName("On Walk Forward Pressed")]
    public UnityEvent onWalkForwardPressed;

    [InspectorName("On Walk Backward Pressed")]
    public UnityEvent onWalkBackwardPressed;

    [InspectorName("On Walk Left Pressed")]
    public UnityEvent onWalkLeftPressed;

    [InspectorName("On Walk Right Pressed")]
    public UnityEvent onWalkRightPressed;

    [InspectorName("On Shoot Weapon Pressed")]
    public UnityEvent onShootWeaponPressed;

    [InspectorName("On Aim Weapon Pressed")]
    public UnityEvent onAimWeaponPressed;

    [InspectorName("On Lean Left Pressed")]
    public UnityEvent onLeanLeftPressed;

    [InspectorName("On Lean Right Pressed")]
    public UnityEvent onLeanRightPressed;

    [InspectorName("On Scroll Up Pressed")]
    public UnityEvent onScrollUpPressed;

    [InspectorName("On Scroll Down Pressed")]
    public UnityEvent onScrollDownPressed;

    [Header("UI")]
    [InspectorName("Move Speed Bar")]
    public GameObject moveSpeedBar;

    float headBaseX;
    float headBaseY;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera != null)
            cameraPitch = NormalizeAngle(playerCamera.localEulerAngles.x);

        if (headRoot != null)
        {
            Vector3 e = headRoot.localEulerAngles;
            headBaseX = NormalizeAngle(e.x);
            headBaseY = NormalizeAngle(e.y);
        }
    }

    void Start()
    {
        LoadBindingsFromControlData();

        if (onScrollUpPressed == null) onScrollUpPressed = new UnityEvent();
        if (onScrollDownPressed == null) onScrollDownPressed = new UnityEvent();

        onScrollUpPressed.AddListener(IncreaseWalkSpeed);
        onScrollDownPressed.AddListener(DecreaseWalkSpeed);

        UpdateMoveSpeedBar();
    }

    void Update()
    {
        FirePressedEvents();
        HandleScrollSpeed();
        MouseLook();
        HandleWalking();
        UpdateMoveSpeedBar();
        HandleLean();
    }

    void HandleLean()
    {
        if (headRoot == null) return;

        bool leftHeld = GetAction("leanLeft");
        bool rightHeld = GetAction("leanRight");

        float targetZ = 0f;

        if (leftHeld && !rightHeld) targetZ = leanAngle;
        else if (rightHeld && !leftHeld) targetZ = -leanAngle;

        Vector3 e = headRoot.localEulerAngles;
        float currentZ = NormalizeAngle(e.z);

        float newZ = Mathf.MoveTowards(currentZ, targetZ, leanRotationSpeed * Time.deltaTime);

        headRoot.localRotation = Quaternion.Euler(headBaseX, headBaseY, newZ);
    }

    void UpdateMoveSpeedBar()
    {
        if (moveSpeedBar == null) return;

        float t = Mathf.InverseLerp(minMoveSpeed, maxMoveSpeed, moveSpeed);

        Vector3 s = moveSpeedBar.transform.localScale;
        s.x = t;
        moveSpeedBar.transform.localScale = s;
    }

    public void IncreaseWalkSpeed()
    {
        moveSpeed += speedChangeStep;
        moveSpeed = Mathf.Clamp(moveSpeed, minMoveSpeed, maxMoveSpeed);
        UpdateMoveSpeedBar();
    }

    public void DecreaseWalkSpeed()
    {
        moveSpeed -= speedChangeStep;
        moveSpeed = Mathf.Clamp(moveSpeed, minMoveSpeed, maxMoveSpeed);
        UpdateMoveSpeedBar();
    }

    public void LoadBindingsFromControlData()
    {
        bindings.Clear();
        runtimeInputs.Clear();
        lookSpeed = 5f;

        if (controlData == null) return;

        lookSpeed = controlData.lookSpeed;

        if (controlData.controlValues == null) return;

        for (int i = 0; i < controlData.controlValues.Length; i++)
        {
            var cv = controlData.controlValues[i];
            if (cv == null) continue;

            string action = cv.actionName ?? string.Empty;
            if (string.IsNullOrWhiteSpace(action)) continue;

            if (action == "upWalkSpeed") continue;
            if (action == "downWalkSpeed") continue;

            if (!cv.isKeybind) continue;

            Binding b = new Binding();

            if (cv.useMouseButton)
            {
                b.useMouse = true;
                b.mouseButton = cv.boundMouseButton;
            }
            else
            {
                b.useMouse = false;
                b.key = cv.boundKey;
            }

            bindings[action] = b;

            UnityEvent evt = GetEventForAction(action);
            if (evt != null)
            {
                RuntimeInput ri = new RuntimeInput();
                ri.actionName = action;
                ri.binding = b;
                ri.onPressed = evt;
                runtimeInputs.Add(ri);
            }
        }
    }

    void HandleWalking()
    {
        float forward = 0f;
        if (GetAction("walkForward")) forward += 1f;
        if (GetAction("walkBackward")) forward -= 1f;

        float right = 0f;
        if (GetAction("walkRight")) right += 1f;
        if (GetAction("walkLeft")) right -= 1f;

        Vector3 move = (transform.forward * forward) + (transform.right * right);
        if (move.sqrMagnitude > 1f) move.Normalize();

        float clampedSpeed = Mathf.Clamp(moveSpeed, minMoveSpeed, maxMoveSpeed);
        characterController.Move(move * clampedSpeed * Time.deltaTime);
    }

    void FirePressedEvents()
    {
        for (int i = 0; i < runtimeInputs.Count; i++)
        {
            var ri = runtimeInputs[i];
            if (IsPressedDown(ri.binding))
                ri.onPressed.Invoke();
        }
    }

    void HandleScrollSpeed()
    {
        if (Mouse.current == null) return;

        float scrollY = Mouse.current.scroll.ReadValue().y;

        if (scrollY > 0f)
        {
            if (onScrollUpPressed != null) onScrollUpPressed.Invoke();
        }
        else if (scrollY < 0f)
        {
            if (onScrollDownPressed != null) onScrollDownPressed.Invoke();
        }
    }

    void MouseLook()
    {
        if (playerCamera == null) return;
        if (Mouse.current == null) return;

        Vector2 delta = Mouse.current.delta.ReadValue();

        float mouseX = delta.x * lookSpeed * Time.deltaTime;
        float mouseY = delta.y * lookSpeed * Time.deltaTime;

        transform.Rotate(0f, mouseX, 0f, Space.Self);

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f);

        playerCamera.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
    }

    UnityEvent GetEventForAction(string actionName)
    {
        if (actionName == "walkForward") return onWalkForwardPressed;
        if (actionName == "walkBackward") return onWalkBackwardPressed;
        if (actionName == "walkLeft") return onWalkLeftPressed;
        if (actionName == "walkRight") return onWalkRightPressed;
        if (actionName == "shootWeapon") return onShootWeaponPressed;
        if (actionName == "aimWeapon") return onAimWeaponPressed;
        if (actionName == "leanLeft") return onLeanLeftPressed;
        if (actionName == "leanRight") return onLeanRightPressed;
        return null;
    }

    bool GetAction(string actionName)
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

    static float NormalizeAngle(float degrees)
    {
        while (degrees > 180f) degrees -= 360f;
        while (degrees < -180f) degrees += 360f;
        return degrees;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(PlayerController))]
public class PlayerControllerEditor : Editor
{
    bool showInputEvents = true;
    bool showUI = true;

    SerializedProperty controlDataProp;
    SerializedProperty playerCameraProp;
    SerializedProperty headRootProp;

    SerializedProperty moveSpeedProp;
    SerializedProperty minMoveSpeedProp;
    SerializedProperty maxMoveSpeedProp;
    SerializedProperty speedChangeStepProp;

    SerializedProperty leanAngleProp;
    SerializedProperty leanRotationSpeedProp;

    SerializedProperty onWalkForwardPressedProp;
    SerializedProperty onWalkBackwardPressedProp;
    SerializedProperty onWalkLeftPressedProp;
    SerializedProperty onWalkRightPressedProp;
    SerializedProperty onShootWeaponPressedProp;
    SerializedProperty onAimWeaponPressedProp;
    SerializedProperty onLeanLeftPressedProp;
    SerializedProperty onLeanRightPressedProp;

    SerializedProperty onScrollUpPressedProp;
    SerializedProperty onScrollDownPressedProp;

    SerializedProperty moveSpeedBarProp;

    void OnEnable()
    {
        controlDataProp = serializedObject.FindProperty("controlData");
        playerCameraProp = serializedObject.FindProperty("playerCamera");
        headRootProp = serializedObject.FindProperty("headRoot");

        moveSpeedProp = serializedObject.FindProperty("moveSpeed");
        minMoveSpeedProp = serializedObject.FindProperty("minMoveSpeed");
        maxMoveSpeedProp = serializedObject.FindProperty("maxMoveSpeed");
        speedChangeStepProp = serializedObject.FindProperty("speedChangeStep");

        leanAngleProp = serializedObject.FindProperty("leanAngle");
        leanRotationSpeedProp = serializedObject.FindProperty("leanRotationSpeed");

        onWalkForwardPressedProp = serializedObject.FindProperty("onWalkForwardPressed");
        onWalkBackwardPressedProp = serializedObject.FindProperty("onWalkBackwardPressed");
        onWalkLeftPressedProp = serializedObject.FindProperty("onWalkLeftPressed");
        onWalkRightPressedProp = serializedObject.FindProperty("onWalkRightPressed");
        onShootWeaponPressedProp = serializedObject.FindProperty("onShootWeaponPressed");
        onAimWeaponPressedProp = serializedObject.FindProperty("onAimWeaponPressed");
        onLeanLeftPressedProp = serializedObject.FindProperty("onLeanLeftPressed");
        onLeanRightPressedProp = serializedObject.FindProperty("onLeanRightPressed");

        onScrollUpPressedProp = serializedObject.FindProperty("onScrollUpPressed");
        onScrollDownPressedProp = serializedObject.FindProperty("onScrollDownPressed");

        moveSpeedBarProp = serializedObject.FindProperty("moveSpeedBar");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        EditorGUILayout.PropertyField(controlDataProp);
        EditorGUILayout.PropertyField(playerCameraProp, new GUIContent("Player Camera"));
        EditorGUILayout.PropertyField(headRootProp, new GUIContent("HeadRoot"));

        EditorGUILayout.Space(6);

        EditorGUILayout.PropertyField(moveSpeedProp);
        EditorGUILayout.PropertyField(minMoveSpeedProp);
        EditorGUILayout.PropertyField(maxMoveSpeedProp);
        EditorGUILayout.PropertyField(speedChangeStepProp);

        EditorGUILayout.Space(6);

        EditorGUILayout.PropertyField(leanAngleProp, new GUIContent("Lean Angle"));
        EditorGUILayout.PropertyField(leanRotationSpeedProp, new GUIContent("Lean Rotation Speed"));

        EditorGUILayout.Space(6);

        showInputEvents = EditorGUILayout.Foldout(showInputEvents, "Input Events", true);
        if (showInputEvents)
        {
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(onWalkForwardPressedProp);
            EditorGUILayout.PropertyField(onWalkBackwardPressedProp);
            EditorGUILayout.PropertyField(onWalkLeftPressedProp);
            EditorGUILayout.PropertyField(onWalkRightPressedProp);
            EditorGUILayout.PropertyField(onShootWeaponPressedProp);
            EditorGUILayout.PropertyField(onAimWeaponPressedProp);
            EditorGUILayout.PropertyField(onLeanLeftPressedProp);
            EditorGUILayout.PropertyField(onLeanRightPressedProp);

            EditorGUILayout.Space(4);
            EditorGUILayout.PropertyField(onScrollUpPressedProp);
            EditorGUILayout.PropertyField(onScrollDownPressedProp);

            EditorGUI.indentLevel--;
        }

        EditorGUILayout.Space(6);

        showUI = EditorGUILayout.Foldout(showUI, "UI", true);
        if (showUI)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.LabelField("Movement", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(moveSpeedBarProp, new GUIContent("Move Speed Bar"));
            EditorGUI.indentLevel--;
        }

        serializedObject.ApplyModifiedProperties();
    }
}
#endif
