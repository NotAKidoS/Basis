using System;
using UnityEngine;

/// <summary>
/// Manual exclusion component for the TransformHider (FPR) system.
/// Allows you to manually hide and show a transform that would otherwise be hidden.
/// </summary>
public class FPRExclusion : MonoBehaviour
{
    public Transform target;

    [NonSerialized]
    internal int id;
}