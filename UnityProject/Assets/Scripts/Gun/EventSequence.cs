using UnityEngine;
using UnityEngine.Events;
using System.Collections;

[System.Serializable]
public struct EventPoint
{
    public UnityEvent Event;
    public float delay;
}

public class EventSequence : MonoBehaviour
{
    [SerializeField, InspectorName("Event Points")]
    public EventPoint[] eventPoints;

    Coroutine seq;

    public void StartEventSequence()
    {
        if (seq != null) StopCoroutine(seq);
        seq = StartCoroutine(RunSequence());
    }

    IEnumerator RunSequence()
    {
        for (int i = 0; i < eventPoints.Length; i++)
        {
            var p = eventPoints[i];
            p.Event.Invoke();
            if (p.delay > 0f) yield return new WaitForSeconds(p.delay);
        }
        seq = null;
    }

    public void StopEventSequence()
    {
        if (seq != null) StopCoroutine(seq);
        seq = null;
    }
}
