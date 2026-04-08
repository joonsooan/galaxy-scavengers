using System.Collections.Generic;
using UnityEngine;

public static class PowerGridGeometry
{
    /// <summary>Draws the coverage square in world space using the grid's cell basis (supports rotation).</summary>
    public static void DrawCoverageOutline(BoundsInt coverage, Grid grid, Color color, float vertexLift = 0.02f)
    {
        if (grid == null || coverage.size.x <= 0 || coverage.size.y <= 0) {
            return;
        }

        Vector3 origin = grid.CellToWorld(new Vector3Int(coverage.xMin, coverage.yMin, coverage.zMin));
        Vector3 dx = grid.CellToWorld(new Vector3Int(coverage.xMin + 1, coverage.yMin, coverage.zMin)) - origin;
        Vector3 dy = grid.CellToWorld(new Vector3Int(coverage.xMin, coverage.yMin + 1, coverage.zMin)) - origin;
        if (dx.sqrMagnitude < 1e-8f || dy.sqrMagnitude < 1e-8f) {
            return;
        }

        Vector3 lift = grid.transform.forward * vertexLift;
        Vector3 p0 = origin + lift;
        Vector3 p1 = origin + dx * coverage.size.x + lift;
        Vector3 p2 = origin + dy * coverage.size.y + lift;
        Vector3 p3 = origin + dx * coverage.size.x + dy * coverage.size.y + lift;

        Color prev = Gizmos.color;
        Gizmos.color = color;
        Gizmos.DrawLine(p0, p1);
        Gizmos.DrawLine(p1, p3);
        Gizmos.DrawLine(p3, p2);
        Gizmos.DrawLine(p2, p0);
        Gizmos.color = prev;
    }

    /// <summary>nxn square with <paramref name="anchorMin"/> as the minimum corner cell (legacy / tile-aligned).</summary>
    public static BoundsInt ComputeSquareCoverage(Vector3Int anchorMin, int n)
    {
        if (n <= 0) {
            return new BoundsInt(anchorMin.x, anchorMin.y, anchorMin.z, 0, 0, 1);
        }

        return new BoundsInt(anchorMin.x, anchorMin.y, anchorMin.z, n, n, 1);
    }

    /// <summary>nxn square on the grid centered on <paramref name="centerCell"/> (odd n: exact center; even n: slight left/bottom bias).</summary>
    public static BoundsInt ComputeSquareCoverageCentered(Vector3Int centerCell, int n)
    {
        if (n <= 0) {
            return new BoundsInt(centerCell.x, centerCell.y, centerCell.z, 0, 0, 1);
        }

        int offset = (n - 1) / 2;
        return new BoundsInt(centerCell.x - offset, centerCell.y - offset, centerCell.z, n, n, 1);
    }

    public static BoundsInt ComputeSquareCoverageCenteredOnFootprint(Grid grid, List<Vector3Int> occupiedCells, int n,
        Vector2Int centerCellOffset = default)
    {
        if (grid == null || occupiedCells == null || occupiedCells.Count == 0 || n <= 0) {
            return default;
        }

        Vector3 sumWorld = Vector3.zero;
        int count = occupiedCells.Count;
        for (int i = 0; i < count; i++) {
            sumWorld += grid.GetCellCenterWorld(occupiedCells[i]);
        }
        Vector3Int centerCell = grid.WorldToCell(sumWorld / count);
        centerCell.x += centerCellOffset.x;
        centerCell.y += centerCellOffset.y;
        return ComputeSquareCoverageCentered(centerCell, n);
    }

    public static bool CoverageRangesTouchOrOverlap(BoundsInt a, BoundsInt b)
    {
        int sepX = AxisSeparationExclusive(a.xMin, a.xMax, b.xMin, b.xMax);
        int sepY = AxisSeparationExclusive(a.yMin, a.yMax, b.yMin, b.yMax);
        return sepX <= 0 && sepY <= 0;
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
