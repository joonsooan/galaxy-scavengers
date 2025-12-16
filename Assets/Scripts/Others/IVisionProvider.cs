using UnityEngine;

public interface IVisionProvider
{
    Vector3 GetPosition();
    float GetVisionRange();
    bool CheckIsActive();
}

