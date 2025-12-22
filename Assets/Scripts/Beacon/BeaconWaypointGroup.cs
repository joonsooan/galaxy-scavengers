using System.Collections.Generic;
using UnityEngine;

public class BeaconWaypointGroup
{
    private readonly List<Beacon> _waypoints = new ();
    private Unit_Scout _assignedUnit;
    private int _currentWaypointIndex;
    
    public List<Beacon> Waypoints => _waypoints;
    public bool IsAssigned => _assignedUnit != null;
    public Unit_Scout AssignedUnit => _assignedUnit;
    public Beacon CurrentWaypoint => _currentWaypointIndex < _waypoints.Count ? _waypoints[_currentWaypointIndex] : null;
    
    public void AddWaypoint(Beacon beacon)
    {
        if (beacon != null && !_waypoints.Contains(beacon))
        {
            _waypoints.Add(beacon);
        }
    }
    
    public void AssignUnit(Unit_Scout unit)
    {
        _assignedUnit = unit;
        _currentWaypointIndex = 0;
        
        if (_waypoints.Count > 0)
        {
            _waypoints[0].AssignUnit(unit);
        }
    }
    
    public void UnassignUnit()
    {
        foreach (var waypoint in _waypoints)
        {
            waypoint.UnassignUnit();
        }
        
        _assignedUnit = null;
        _currentWaypointIndex = 0;
    }
    
    public Beacon GetNextWaypoint()
    {
        if (_currentWaypointIndex >= _waypoints.Count - 1)
        {
            return null;
        }
        
        _currentWaypointIndex++;
        return _waypoints[_currentWaypointIndex];
    }
    
    public bool HasMoreWaypoints()
    {
        return _currentWaypointIndex < _waypoints.Count - 1;
    }
    
    public void DestroyGroup()
    {
        UnassignUnit();
        
        foreach (var waypoint in _waypoints)
        {
            if (waypoint != null)
            {
                Object.Destroy(waypoint.gameObject);
            }
        }
        
        _waypoints.Clear();
    }
}

