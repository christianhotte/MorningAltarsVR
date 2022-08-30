using System;
using UnityEngine;

/// <summary>
/// Three-dimensional bool used for indicating axial constraints (like what Rigidbody has).
/// </summary>
[Serializable]
public struct AxisConstraints
{
    public bool X;
    public bool Y;
    public bool Z;
}
