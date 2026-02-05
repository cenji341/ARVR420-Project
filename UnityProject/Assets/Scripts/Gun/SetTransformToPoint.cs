using UnityEngine;
using System.Collections;

public class SetTransformToPoint : MonoBehaviour
{
    [System.Serializable]
    public class Point
    {
        [Header("Local Position")]
        public Vector3 localPosition;
        public bool usePosX = true;
        public bool usePosY = true;
        public bool usePosZ = true;
        public float posXDuration = 0f;
        public float posYDuration = 0f;
        public float posZDuration = 0f;
        public bool positionLinear = true;

        [Header("Local Rotation (Euler)")]
        public Vector3 localRotation;
        public bool useRotX = true;
        public bool useRotY = true;
        public bool useRotZ = true;
        public float rotXDuration = 0f;
        public float rotYDuration = 0f;
        public float rotZDuration = 0f;
        public bool rotationLinear = true;
    }

    [Header("Points")]
    public Point[] points;

    private Coroutine moveRoutine;
    private int currentIndex = -1;

    public void SetToPoint(int index)
    {
        if (points == null || index < 0 || index >= points.Length) return;
        if (moveRoutine != null) StopCoroutine(moveRoutine);
        currentIndex = index;
        moveRoutine = StartCoroutine(ApplyPoint(points[index]));
    }
    private void Start()
    {
        SetToPoint(0);
    }
    public void NextPoint()
    {
        if (points == null || points.Length == 0) return;
        int next = (currentIndex + 1) % points.Length;
        SetToPoint(next);
    }

    private IEnumerator ApplyPoint(Point p)
    {
        Vector3 startPos = transform.localPosition;
        Vector3 targetPos = new Vector3(
            p.usePosX ? p.localPosition.x : startPos.x,
            p.usePosY ? p.localPosition.y : startPos.y,
            p.usePosZ ? p.localPosition.z : startPos.z
        );

        Vector3 startEuler = transform.localEulerAngles;
        Vector3 targetEuler = new Vector3(
            p.useRotX ? p.localRotation.x : startEuler.x,
            p.useRotY ? p.localRotation.y : startEuler.y,
            p.useRotZ ? p.localRotation.z : startEuler.z
        );

        float elapsed = 0f;
        bool anyDuration =
            (p.usePosX && p.posXDuration > 0f) ||
            (p.usePosY && p.posYDuration > 0f) ||
            (p.usePosZ && p.posZDuration > 0f) ||
            (p.useRotX && p.rotXDuration > 0f) ||
            (p.useRotY && p.rotYDuration > 0f) ||
            (p.useRotZ && p.rotZDuration > 0f);

        if (!anyDuration)
        {
            transform.localPosition = targetPos;
            transform.localRotation = Quaternion.Euler(targetEuler);
            moveRoutine = null;
            yield break;
        }

        while (true)
        {
            bool allDone = true;
            elapsed += Time.deltaTime;

            Vector3 newPos = transform.localPosition;
            if (p.usePosX)
            {
                if (p.posXDuration <= 0f) newPos.x = targetPos.x;
                else
                {
                    float t = Mathf.Clamp01(elapsed / p.posXDuration);
                    t = p.positionLinear ? t : EaseCubic(t);
                    newPos.x = Mathf.Lerp(startPos.x, targetPos.x, t);
                    if (t < 1f) allDone = false;
                }
            }
            if (p.usePosY)
            {
                if (p.posYDuration <= 0f) newPos.y = targetPos.y;
                else
                {
                    float t = Mathf.Clamp01(elapsed / p.posYDuration);
                    t = p.positionLinear ? t : EaseCubic(t);
                    newPos.y = Mathf.Lerp(startPos.y, targetPos.y, t);
                    if (t < 1f) allDone = false;
                }
            }
            if (p.usePosZ)
            {
                if (p.posZDuration <= 0f) newPos.z = targetPos.z;
                else
                {
                    float t = Mathf.Clamp01(elapsed / p.posZDuration);
                    t = p.positionLinear ? t : EaseCubic(t);
                    newPos.z = Mathf.Lerp(startPos.z, targetPos.z, t);
                    if (t < 1f) allDone = false;
                }
            }

            Vector3 newEuler = transform.localEulerAngles;
            if (p.useRotX)
            {
                if (p.rotXDuration <= 0f) newEuler.x = targetEuler.x;
                else
                {
                    float t = Mathf.Clamp01(elapsed / p.rotXDuration);
                    t = p.rotationLinear ? t : EaseCubic(t);
                    newEuler.x = Mathf.LerpAngle(startEuler.x, targetEuler.x, t);
                    if (t < 1f) allDone = false;
                }
            }
            if (p.useRotY)
            {
                if (p.rotYDuration <= 0f) newEuler.y = targetEuler.y;
                else
                {
                    float t = Mathf.Clamp01(elapsed / p.rotYDuration);
                    t = p.rotationLinear ? t : EaseCubic(t);
                    newEuler.y = Mathf.LerpAngle(startEuler.y, targetEuler.y, t);
                    if (t < 1f) allDone = false;
                }
            }
            if (p.useRotZ)
            {
                if (p.rotZDuration <= 0f) newEuler.z = targetEuler.z;
                else
                {
                    float t = Mathf.Clamp01(elapsed / p.rotZDuration);
                    t = p.rotationLinear ? t : EaseCubic(t);
                    newEuler.z = Mathf.LerpAngle(startEuler.z, targetEuler.z, t);
                    if (t < 1f) allDone = false;
                }
            }

            transform.localPosition = newPos;
            transform.localRotation = Quaternion.Euler(newEuler);

            if (allDone) break;
            yield return null;
        }

        moveRoutine = null;
    }

    private float EaseCubic(float t)
    {
        return t * t * (3f - 2f * t);
    }
}
