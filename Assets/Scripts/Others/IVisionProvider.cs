using UnityEngine;

/// <summary>
/// Interface for objects that provide vision in the fog of war system.
/// Buildings and units should implement this interface to reveal areas around them.
/// </summary>
public interface IVisionProvider
{
    /// <summary>
    /// Gets the world position of the vision provider.
    /// </summary>
    Vector3 GetPosition();
    
    /// <summary>
    /// Gets the vision range in world units.
    /// </summary>
    float GetVisionRange();
    
    /// <summary>
    /// Checks if the vision provider is currently active (should provide vision).
    /// </summary>
    bool CheckIsActive();
}

