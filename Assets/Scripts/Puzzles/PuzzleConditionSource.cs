using System;
using UnityEngine;

// Base class for anything that can be used as a puzzle condition.
// Examples: a pressed button, a lever that is turned on, or a sensor that has enough water.
public abstract class PuzzleConditionSource : MonoBehaviour
{
    // The condition group reads this value without knowing what kind of condition this is.
    public abstract bool IsSatisfied { get; }

    // Raised only when the condition state changes.
    public event Action Changed;

    // Child classes call this after their satisfied state changes.
    protected void NotifyChanged()
    {
        Changed?.Invoke();
    }
}
