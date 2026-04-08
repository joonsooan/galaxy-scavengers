using UnityEngine;

public static class PowerStatusBillboardAlign
{
    private static bool IsPowerIconObject(Transform t)
    {
        string n = t.name;
        return n == "PowerDiscIcon" || n == "PowerInsuffIcon";
    }

    public static SpriteRenderer FindHostSpriteExcludingIcons(Transform hostRoot)
    {
        SpriteRenderer onRoot = hostRoot.GetComponent<SpriteRenderer>();
        if (onRoot != null) {
            return onRoot;
        }
        SpriteRenderer[] renderers = hostRoot.GetComponentsInChildren<SpriteRenderer>(true);
        for (int i = 0; i < renderers.Length; i++) {
            SpriteRenderer sr = renderers[i];
            if (sr == null || IsPowerIconObject(sr.transform)) {
                continue;
            }
            return sr;
        }
        return null;
    }

    public static void ApplySortingAndScale(Transform hostRoot, SpriteRenderer disc, SpriteRenderer insuff, float heightFractionOfHost)
    {
        if (disc == null || insuff == null) {
            return;
        }
        SpriteRenderer hostSr = FindHostSpriteExcludingIcons(hostRoot);
        if (hostSr != null && hostSr.sprite != null) {
            disc.sortingLayerID = hostSr.sortingLayerID;
            insuff.sortingLayerID = hostSr.sortingLayerID;
            disc.sortingOrder = hostSr.sortingOrder + 20;
            insuff.sortingOrder = hostSr.sortingOrder + 21;
            disc.transform.localScale = Vector3.one;
            insuff.transform.localScale = Vector3.one;
        }
    }

    public static void ApplyCanvasIconWorldHeight(Transform hostRoot, RectTransform iconRt, float heightFractionOfHost)
    {
        if (iconRt == null) {
            return;
        }
        iconRt.localScale = Vector3.one;
    }
}
