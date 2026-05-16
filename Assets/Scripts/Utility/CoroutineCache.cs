using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public static class CoroutineCache
{
    private static readonly Dictionary<float, WaitForSeconds> _waitForSecondsCache = new Dictionary<float, WaitForSeconds>();
    private static readonly Dictionary<float, WaitForSecondsRealtime> _waitForSecondsRealtimeCache = new Dictionary<float, WaitForSecondsRealtime>();
    private static readonly WaitForEndOfFrame _waitForEndOfFrame = new WaitForEndOfFrame();
    private static readonly WaitForFixedUpdate _waitForFixedUpdate = new WaitForFixedUpdate();

    public static WaitForSeconds GetWaitForSeconds(float seconds)
    {
        if (!_waitForSecondsCache.ContainsKey(seconds))
        {
            _waitForSecondsCache[seconds] = new WaitForSeconds(seconds);
        }
        return _waitForSecondsCache[seconds];
    }

    public static WaitForSecondsRealtime GetWaitForSecondsRealtime(float seconds)
    {
        if (!_waitForSecondsRealtimeCache.ContainsKey(seconds))
        {
            _waitForSecondsRealtimeCache[seconds] = new WaitForSecondsRealtime(seconds);
        }
        return _waitForSecondsRealtimeCache[seconds];
    }

    public static WaitForEndOfFrame GetWaitForEndOfFrame()
    {
        return _waitForEndOfFrame;
    }

    public static WaitForFixedUpdate GetWaitForFixedUpdate()
    {
        return _waitForFixedUpdate;
    }

    public static void ClearCache()
    {
        _waitForSecondsCache.Clear();
        _waitForSecondsRealtimeCache.Clear();
    }
}
