using UnityEngine;

public static class PowerStatusBillboardAlign
{
    public static void ApplyCanvasIconWorldHeight(Transform hostRoot, RectTransform iconRt, float heightFractionOfHost)
    {
        if (iconRt == null) {
            return;
        }
        iconRt.localScale = Vector3.one;
    }
}
