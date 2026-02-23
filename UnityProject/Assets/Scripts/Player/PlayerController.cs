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

    [InspectorName("BobRoot")]
    public Transform bobRoot;

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

    [InspectorName("Lean Offset")]
    public float leanOffset = 0.25f;

    [InspectorName("Lean Smooth Time")]
    public float leanSmoothTime = 0.08f;

    [InspectorName("Lean Collision Radius")]
    public float leanCollisionRadius = 0.12f;

    [InspectorName("Lean Collision Mask")]
    public LayerMask leanCollisionMask = ~0;

    [InspectorName("Lean Collision Padding")]
    public float leanCollisionPadding = 0.01f;

    [Header("Head Bob")]
    [InspectorName("Enable Head Bob")]
    public bool enableHeadBob = true;

    [InspectorName("Require Grounded For Bob")]
    public bool requireGroundedForBob = true;

    [InspectorName("Use Physics Ground Check")]
    public bool usePhysicsGroundCheck = true;

    [InspectorName("Ground Check Distance")]
    public float groundCheckDistance = 0.08f;

    [InspectorName("Ground Check Radius Scale")]
    public float groundCheckRadiusScale = 0.95f;

    [InspectorName("Ground Check Mask")]
    public LayerMask groundCheckMask = ~0;

    [InspectorName("Bob Min Amplitude")]
    public float bobMinAmplitude = 0.015f;

    [InspectorName("Bob Max Amplitude")]
    public float bobMaxAmplitude = 0.045f;

    [InspectorName("Bob Min Frequency")]
    public float bobMinFrequency = 6f;

    [InspectorName("Bob Max Frequency")]
    public float bobMaxFrequency = 12f;

    [InspectorName("Bob Return Speed")]
    public float bobReturnSpeed = 12f;

    [InspectorName("Bob Axis Multipliers (X,Y,Z)")]
    public Vector3 bobAxisMultipliers = new Vector3(0.6f, 1f, 0.35f);

    [InspectorName("Bob Z Phase Multiplier")]
    public float bobZPhaseMultiplier = 1.0f;

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

    float viewYaw;

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

    Vector3 headBaseLocalPos;
    Vector3 bobBaseLocalPos;
    Vector3 cameraBaseLocalPos;

    float currentLeanRoll;
    float currentLeanOffset;

    float leanRollVel;
    float leanOffsetVel;

    float bobPhase;
    Vector3 currentBobLocalOffset;

    void Awake()
    {
        characterController = GetComponent<CharacterController>();

        if (playerCamera != null)
        {
            cameraPitch = NormalizeAngle(playerCamera.localEulerAngles.x);
            cameraBaseLocalPos = playerCamera.localPosition;
        }

        if (headRoot != null)
        {
            Vector3 e = headRoot.localEulerAngles;
            headBaseX = NormalizeAngle(e.x);
            headBaseY = NormalizeAngle(e.y);
            headBaseLocalPos = headRoot.localPosition;
        }

        if (bobRoot != null)
            bobBaseLocalPos = bobRoot.localPosition;
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
        HandleHeadBob();
    }

    void LateUpdate()
    {
        ApplyRigPose();
    }

    bool IsGroundedForBob()
    {
        if (characterController == null) return false;

        if (!usePhysicsGroundCheck)
            return characterController.isGrounded;

        float radius = characterController.radius * groundCheckRadiusScale;
        float height = characterController.height;

        Vector3 centerWorld = transform.TransformPoint(characterController.center);

        float bottom = centerWorld.y - (height * 0.5f) + radius;
        Vector3 origin = new Vector3(centerWorld.x, bottom + 0.02f, centerWorld.z);

        return Physics.SphereCast(origin, radius, Vector3.down, out _, groundCheckDistance, groundCheckMask, QueryTriggerInteraction.Ignore);
    }

    void HandleLean()
    {
        if (headRoot == null) return;

        bool leftHeld = GetAction("leanLeft");
        bool rightHeld = GetAction("leanRight");

        float targetDir = 0f;
        if (leftHeld && !rightHeld) targetDir = 1f;
        else if (rightHeld && !leftHeld) targetDir = -1f;

        float targetRoll = targetDir * leanAngle;
        float targetOffset = -targetDir * leanOffset;

        float newRoll = Mathf.SmoothDamp(currentLeanRoll, targetRoll, ref leanRollVel, leanSmoothTime, Mathf.Infinity, Time.deltaTime);
        float newOffset = Mathf.SmoothDamp(currentLeanOffset, targetOffset, ref leanOffsetVel, leanSmoothTime, Mathf.Infinity, Time.deltaTime);

        float safeOffset = newOffset;

        if (playerCamera != null && Mathf.Abs(newOffset) > 0.0001f)
        {
            Vector3 origin = playerCamera.position;
            Vector3 desired = origin + (transform.right * newOffset);
            Vector3 dir = desired - origin;
            float dist = dir.magnitude;

            if (dist > 0.0001f)
            {
                dir /= dist;

                if (Physics.SphereCast(origin, leanCollisionRadius, dir, out RaycastHit hit, dist, leanCollisionMask, QueryTriggerInteraction.Ignore))
                {
                    float allowed = hit.distance - leanCollisionPadding;
                    if (allowed < 0f) allowed = 0f;
                    safeOffset = Mathf.Sign(newOffset) * allowed;
                }
            }
        }

        currentLeanRoll = newRoll;
        currentLeanOffset = safeOffset;

        if (Mathf.Abs(currentLeanRoll) < 0.01f && Mathf.Abs(viewYaw) > 0.0001f)
        {
            transform.Rotate(0f, viewYaw, 0f, Space.Self);
            viewYaw = 0f;
        }
    }

    void HandleHeadBob()
    {
        if (!enableHeadBob)
        {
            currentBobLocalOffset = Vector3.zero;
            return;
        }

        bool grounded = IsGroundedForBob();

        if (requireGroundedForBob && !grounded)
        {
            currentBobLocalOffset = Vector3.Lerp(currentBobLocalOffset, Vector3.zero, bobReturnSpeed * Time.deltaTime);
            bobPhase = Mathf.Lerp(bobPhase, 0f, bobReturnSpeed * Time.deltaTime);
            return;
        }

        float forward = 0f;
        if (GetAction("walkForward")) forward += 1f;
        if (GetAction("walkBackward")) forward -= 1f;

        float right = 0f;
        if (GetAction("walkRight")) right += 1f;
        if (GetAction("walkLeft")) right -= 1f;

        bool moving = (Mathf.Abs(forward) + Mathf.Abs(right)) > 0.001f;

        float clamped = Mathf.Clamp(moveSpeed, minMoveSpeed, maxMoveSpeed);
        float speed01 = Mathf.InverseLerp(minMoveSpeed, maxMoveSpeed, clamped);

        float amp = Mathf.Lerp(bobMinAmplitude, bobMaxAmplitude, speed01);
        float freq = Mathf.Lerp(bobMinFrequency, bobMaxFrequency, speed01);

        if (moving)
        {
            bobPhase += Time.deltaTime * freq;

            float x = Mathf.Cos(bobPhase * 0.5f) * (amp * bobAxisMultipliers.x);
            float y = Mathf.Abs(Mathf.Sin(bobPhase)) * (amp * bobAxisMultipliers.y);
            float z = Mathf.Sin(bobPhase * bobZPhaseMultiplier) * (amp * bobAxisMultipliers.z);

            currentBobLocalOffset.x = x;
            currentBobLocalOffset.y = y;
            currentBobLocalOffset.z = z;
        }
        else
        {
            currentBobLocalOffset = Vector3.Lerp(currentBobLocalOffset, Vector3.zero, bobReturnSpeed * Time.deltaTime);
            bobPhase = Mathf.Lerp(bobPhase, 0f, bobReturnSpeed * Time.deltaTime);
        }
    }

    void ApplyRigPose()
    {
        if (headRoot != null)
        {
            Vector3 p = headBaseLocalPos;
            p.x += currentLeanOffset;
            headRoot.localPosition = p;

            headRoot.localRotation = Quaternion.Euler(headBaseX, headBaseY, 0f);
        }

        if (bobRoot != null)
        {
            bobRoot.localPosition = bobBaseLocalPos + currentBobLocalOffset;
        }

        if (playerCamera != null)
        {
            playerCamera.localPosition = cameraBaseLocalPos;

            bool isLeaning = Mathf.Abs(currentLeanRoll) > 0.01f || Mathf.Abs(currentLeanOffset) > 0.0001f;

            if (isLeaning)
            {
                Quaternion roll = Quaternion.AngleAxis(currentLeanRoll, Vector3.forward);
                Quaternion yaw = Quaternion.AngleAxis(viewYaw, Vector3.up);
                Quaternion pitch = Quaternion.AngleAxis(cameraPitch, Vector3.right);
                playerCamera.localRotation = roll * yaw * pitch;
            }
            else
            {
                playerCamera.localRotation = Quaternion.Euler(cameraPitch, 0f, 0f);
            }
        }
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

        cameraPitch -= mouseY;
        cameraPitch = Mathf.Clamp(cameraPitch, -89f, 89f);

        bool isLeaning = Mathf.Abs(currentLeanRoll) > 0.01f || Mathf.Abs(currentLeanOffset) > 0.0001f;

        if (isLeaning)
        {
            viewYaw += mouseX;
        }
        else
        {
            if (Mathf.Abs(viewYaw) > 0.0001f)
            {
                transform.Rotate(0f, viewYaw, 0f, Space.Self);
                viewYaw = 0f;
            }

            transform.Rotate(0f, mouseX, 0f, Space.Self);
        }
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
    SerializedProperty bobRootProp;

    SerializedProperty moveSpeedProp;
    SerializedProperty minMoveSpeedProp;
    SerializedProperty maxMoveSpeedProp;
    SerializedProperty speedChangeStepProp;

    SerializedProperty leanAngleProp;
    SerializedProperty leanRotationSpeedProp;
    SerializedProperty leanOffsetProp;
    SerializedProperty leanSmoothTimeProp;
    SerializedProperty leanCollisionRadiusProp;
    SerializedProperty leanCollisionMaskProp;
    SerializedProperty leanCollisionPaddingProp;

    SerializedProperty enableHeadBobProp;
    SerializedProperty requireGroundedForBobProp;
    SerializedProperty usePhysicsGroundCheckProp;
    SerializedProperty groundCheckDistanceProp;
    SerializedProperty groundCheckRadiusScaleProp;
    SerializedProperty groundCheckMaskProp;
    SerializedProperty bobMinAmplitudeProp;
    SerializedProperty bobMaxAmplitudeProp;
    SerializedProperty bobMinFrequencyProp;
    SerializedProperty bobMaxFrequencyProp;
    SerializedProperty bobReturnSpeedProp;
    SerializedProperty bobAxisMultipliersProp;
    SerializedProperty bobZPhaseMultiplierProp;

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
        bobRootProp = serializedObject.FindProperty("bobRoot");

        moveSpeedProp = serializedObject.FindProperty("moveSpeed");
        minMoveSpeedProp = serializedObject.FindProperty("minMoveSpeed");
        maxMoveSpeedProp = serializedObject.FindProperty("maxMoveSpeed");
        speedChangeStepProp = serializedObject.FindProperty("speedChangeStep");

        leanAngleProp = serializedObject.FindProperty("leanAngle");
        leanRotationSpeedProp = serializedObject.FindProperty("leanRotationSpeed");
        leanOffsetProp = serializedObject.FindProperty("leanOffset");
        leanSmoothTimeProp = serializedObject.FindProperty("leanSmoothTime");
        leanCollisionRadiusProp = serializedObject.FindProperty("leanCollisionRadius");
        leanCollisionMaskProp = serializedObject.FindProperty("leanCollisionMask");
        leanCollisionPaddingProp = serializedObject.FindProperty("leanCollisionPadding");

        enableHeadBobProp = serializedObject.FindProperty("enableHeadBob");
        requireGroundedForBobProp = serializedObject.FindProperty("requireGroundedForBob");
        usePhysicsGroundCheckProp = serializedObject.FindProperty("usePhysicsGroundCheck");
        groundCheckDistanceProp = serializedObject.FindProperty("groundCheckDistance");
        groundCheckRadiusScaleProp = serializedObject.FindProperty("groundCheckRadiusScale");
        groundCheckMaskProp = serializedObject.FindProperty("groundCheckMask");
        bobMinAmplitudeProp = serializedObject.FindProperty("bobMinAmplitude");
        bobMaxAmplitudeProp = serializedObject.FindProperty("bobMaxAmplitude");
        bobMinFrequencyProp = serializedObject.FindProperty("bobMinFrequency");
        bobMaxFrequencyProp = serializedObject.FindProperty("bobMaxFrequency");
        bobReturnSpeedProp = serializedObject.FindProperty("bobReturnSpeed");
        bobAxisMultipliersProp = serializedObject.FindProperty("bobAxisMultipliers");
        bobZPhaseMultiplierProp = serializedObject.FindProperty("bobZPhaseMultiplier");

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
        EditorGUILayout.PropertyField(bobRootProp, new GUIContent("BobRoot"));

        EditorGUILayout.Space(6);

        EditorGUILayout.PropertyField(moveSpeedProp);
        EditorGUILayout.PropertyField(minMoveSpeedProp);
        EditorGUILayout.PropertyField(maxMoveSpeedProp);
        EditorGUILayout.PropertyField(speedChangeStepProp);

        EditorGUILayout.Space(6);

        EditorGUILayout.PropertyField(leanAngleProp, new GUIContent("Lean Angle"));
        EditorGUILayout.PropertyField(leanRotationSpeedProp, new GUIContent("Lean Rotation Speed"));
        EditorGUILayout.PropertyField(leanOffsetProp, new GUIContent("Lean Offset"));
        EditorGUILayout.PropertyField(leanSmoothTimeProp, new GUIContent("Lean Smooth Time"));
        EditorGUILayout.PropertyField(leanCollisionRadiusProp, new GUIContent("Lean Collision Radius"));
        EditorGUILayout.PropertyField(leanCollisionMaskProp, new GUIContent("Lean Collision Mask"));
        EditorGUILayout.PropertyField(leanCollisionPaddingProp, new GUIContent("Lean Collision Padding"));

        EditorGUILayout.Space(6);

        EditorGUILayout.PropertyField(enableHeadBobProp, new GUIContent("Enable Head Bob"));
        EditorGUILayout.PropertyField(requireGroundedForBobProp, new GUIContent("Require Grounded For Bob"));
        EditorGUILayout.PropertyField(usePhysicsGroundCheckProp, new GUIContent("Use Physics Ground Check"));
        EditorGUILayout.PropertyField(groundCheckDistanceProp, new GUIContent("Ground Check Distance"));
        EditorGUILayout.PropertyField(groundCheckRadiusScaleProp, new GUIContent("Ground Check Radius Scale"));
        EditorGUILayout.PropertyField(groundCheckMaskProp, new GUIContent("Ground Check Mask"));

        EditorGUILayout.PropertyField(bobMinAmplitudeProp, new GUIContent("Bob Min Amplitude"));
        EditorGUILayout.PropertyField(bobMaxAmplitudeProp, new GUIContent("Bob Max Amplitude"));
        EditorGUILayout.PropertyField(bobMinFrequencyProp, new GUIContent("Bob Min Frequency"));
        EditorGUILayout.PropertyField(bobMaxFrequencyProp, new GUIContent("Bob Max Frequency"));
        EditorGUILayout.PropertyField(bobReturnSpeedProp, new GUIContent("Bob Return Speed"));
        EditorGUILayout.PropertyField(bobAxisMultipliersProp, new GUIContent("Bob Axis Multipliers (X,Y,Z)"));
        EditorGUILayout.PropertyField(bobZPhaseMultiplierProp, new GUIContent("Bob Z Phase Multiplier"));

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
