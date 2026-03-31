using UnityEngine;

public static class PowerGridGeometry
{
    public static BoundsInt ComputeSquareCoverage(Vector3Int anchorMin, int n)
    {
        if (n <= 0) {
            return new BoundsInt(anchorMin.x, anchorMin.y, anchorMin.z, 0, 0, 1);
        }

        return new BoundsInt(anchorMin.x, anchorMin.y, anchorMin.z, n, n, 1);
    }

    public static bool CoverageRangesTouchOrOverlap(BoundsInt a, BoundsInt b)
    {
        int sepX = AxisSeparationExclusive(a.xMin, a.xMax, b.xMin, b.xMax);
        int sepY = AxisSeparationExclusive(a.yMin, a.yMax, b.yMin, b.yMax);
        int cheb = sepX > sepY ? sepX : sepY;
        return cheb <= 1;
    }

    private static int AxisSeparationExclusive(int aMin, int aMaxEx, int bMin, int bMaxEx)
    {
        if (aMaxEx <= bMin) {
            return bMin - aMaxEx;
        }

        if (bMaxEx <= aMin) {
            return aMin - bMaxEx;
        }

        return 0;
    }
}
