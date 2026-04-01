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
            float hostH = hostSr.bounds.size.y;
            float iconH = Mathf.Max(disc.bounds.size.y, 0.001f);
            float scale = Mathf.Clamp(hostH * heightFractionOfHost / iconH, 0.2f, 4f);
            disc.transform.localScale = Vector3.one * scale;
            insuff.transform.localScale = Vector3.one * scale;
        }
    }

    public static void ApplyCanvasIconWorldHeight(Transform hostRoot, RectTransform iconRt, float heightFractionOfHost)
    {
        if (iconRt == null || heightFractionOfHost <= 0f) {
            return;
        }
        SpriteRenderer hostSr = FindHostSpriteExcludingIcons(hostRoot);
        if (hostSr == null || hostSr.sprite == null) {
            return;
        }
        float desiredWorldH = hostSr.bounds.size.y * heightFractionOfHost;
        Vector3[] corners = new Vector3[4];
        iconRt.GetWorldCorners(corners);
        float h = Mathf.Abs(corners[1].y - corners[0].y);
        if (h < 1e-5f) {
            return;
        }
        float k = desiredWorldH / h;
        iconRt.localScale = iconRt.localScale * k;
    }
}
