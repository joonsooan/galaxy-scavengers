using System;
using System.Collections;
using UnityEngine;

public class AetherTimerManager : MonoBehaviour
{
    public static event Action OnAetherTick;
    
    private void Start()
    {
        StartCoroutine(AetherTickCoroutine());
    }
    
    private IEnumerator AetherTickCoroutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1f);
            OnAetherTick?.Invoke();
        }
    }
}
