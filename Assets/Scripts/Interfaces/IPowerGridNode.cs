using UnityEngine;

public interface IPowerGridNode
{
    BoundsInt GetPowerCoverageBounds();
    bool IsActivePowerSource();
}
