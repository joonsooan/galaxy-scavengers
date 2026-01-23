using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class BeaconManager : MonoBehaviour
{
    public static BeaconManager Instance { get; private set; }
    public static event Action<Beacon> OnBeaconPlacedForScout;
    
    [Header("Beacon Settings")]
    [SerializeField] private GameObject beaconPrefab;
    [SerializeField] private Transform beaconParent;
    
    private readonly List<Beacon> _beacons = new ();
    private readonly List<BeaconWaypointGroup> _waypointGroups = new ();
    private BeaconWaypointGroup _currentWaypointGroup;
    private Camera _mainCamera;
    private Grid _grid;
    
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        
        Instance = this;
        _mainCamera = Camera.main;
    }
    
    private void Start()
    {
        if (_grid == null && BuildingManager.Instance != null)
        {
            _grid = BuildingManager.Instance.grid;
        }
    }
    
    private void Update()
    {
        HandleBeaconInput();
    }
    
    private void HandleBeaconInput()
    {
        if (_mainCamera == null || _grid == null) return;

        if (IsLoadingScreenActive()) return;
        
        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        
        if (Input.GetMouseButtonDown(2))
        {
            if (isShiftHeld)
            {
                PlaceWaypointBeacon();
            }
            else
            {
                _currentWaypointGroup = null;
                PlaceSingleBeacon();
            }
        }
        
        if (_currentWaypointGroup != null && !isShiftHeld)
        {
            _currentWaypointGroup = null;
        }
    }
    
    private void PlaceSingleBeacon()
    {
        Vector3 worldPos = GetMouseWorldPosition();
        if (worldPos == Vector3.zero) return;
        
        Vector3Int cellPos = _grid.WorldToCell(worldPos);
        Vector3 beaconPos = _grid.GetCellCenterWorld(cellPos);

        if (beaconPrefab == null) return;
        if (!IsCellWalkable(cellPos)) return;
        
        GameObject beaconObj = Instantiate(beaconPrefab, beaconPos, Quaternion.identity, beaconParent);
        Beacon beacon = beaconObj.GetComponent<Beacon>();
        
        _beacons.Add(beacon);
        AssignClosestScoutToBeacon(beacon);
    }
    
    private bool IsCellWalkable(Vector3Int cell)
    {
        if (BuildingManager.Instance == null) return true;
        
        if (BuildingManager.Instance.IsTerrainCell(cell) ||
            BuildingManager.Instance.IsResourceTile(cell) || 
            BuildingManager.Instance.IsBuildingTile(cell))
        {
            return false;
        }
        
        if (BuildingManager.Instance.GetBuildingAt(cell, out _))
        {
            return false;
        }
        
        BuildingPiece piece = BuildingManager.Instance.GetPieceAt(cell);
        if (piece != null)
        {
            return false;
        }
        
        return true;
    }
    
    private void PlaceWaypointBeacon()
    {
        Vector3 worldPos = GetMouseWorldPosition();
        if (worldPos == Vector3.zero) return;
        
        Vector3Int cellPos = _grid.WorldToCell(worldPos);
        Vector3 beaconPos = _grid.GetCellCenterWorld(cellPos);

        
        if (_currentWaypointGroup == null)
        {
            _currentWaypointGroup = new BeaconWaypointGroup();
            _waypointGroups.Add(_currentWaypointGroup);
        }
        
        GameObject beaconObj = Instantiate(beaconPrefab, beaconPos, Quaternion.identity, beaconParent);
        Beacon beacon = beaconObj.GetComponent<Beacon>();
        
        if (beacon == null)
        {
            beacon = beaconObj.AddComponent<Beacon>();
        }
        
        _beacons.Add(beacon);
        _currentWaypointGroup.AddWaypoint(beacon);
        
        if (!_currentWaypointGroup.IsAssigned)
        {
            AssignClosestScoutToWaypointGroup(_currentWaypointGroup);
        }
    }
    
    private Vector3 GetMouseWorldPosition()
    {
        if (_mainCamera == null) return Vector3.zero;
        
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = _mainCamera.nearClipPlane;
        Vector3 worldPos = _mainCamera.ScreenToWorldPoint(mousePos);
        worldPos.z = 0;
        
        return worldPos;
    }
    
    private void AssignClosestScoutToBeacon(Beacon beacon)
    {
        Unit_Scout closestScout = FindClosestAvailableScout(beacon.Position);
        
        if (closestScout != null)
        {
            closestScout.AssignToBeacon(beacon);
            beacon.AssignUnit(closestScout);
            OnBeaconPlacedForScout?.Invoke(beacon);
        }
    }
    
    private void AssignClosestScoutToWaypointGroup(BeaconWaypointGroup group)
    {
        if (group.Waypoints.Count == 0) return;
        
        Vector3 firstWaypointPos = group.Waypoints[0].Position;
        Unit_Scout closestScout = FindClosestAvailableScout(firstWaypointPos);
        
        if (closestScout != null)
        {
            closestScout.AssignToWaypointGroup(group);
            group.AssignUnit(closestScout);
        }
    }
    
    private Unit_Scout FindClosestAvailableScout(Vector3 targetPosition)
    {
        if (UnitManager.Instance == null) return null;
        
        Unit_Scout closestScout = null;
        float closestDistance = float.MaxValue;
        
        foreach (var unit in UnitManager.Instance.AllyUnits)
        {
            if (unit is Unit_Scout scout && !scout.IsAssignedToBeacon)
            {
                float distance = Vector3.Distance(scout.transform.position, targetPosition);
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    closestScout = scout;
                }
            }
        }
        
        return closestScout;
    }
    
    public void RemoveBeacon(Beacon beacon)
    {
        if (beacon == null) return;
        
        _beacons.Remove(beacon);
        
        foreach (var group in _waypointGroups)
        {
            if (group.Waypoints.Contains(beacon))
            {
                if (group == _currentWaypointGroup)
                {
                    _currentWaypointGroup = null;
                }
                
                if (group.Waypoints.Count <= 1)
                {
                    group.DestroyGroup();
                    _waypointGroups.Remove(group);
                }
                break;
            }
        }
    }
    
    public void CompleteWaypointGroup(BeaconWaypointGroup group)
    {
        if (group == null) return;
        
        if (group == _currentWaypointGroup)
        {
            _currentWaypointGroup = null;
        }
        
        group.DestroyGroup();
        _waypointGroups.Remove(group);
    }
    
    public void OnScoutDestroyed(Unit_Scout scout)
    {
        foreach (var beacon in _beacons)
        {
            if (beacon.AssignedUnit == scout)
            {
                beacon.UnassignUnit();
                AssignClosestScoutToBeacon(beacon);
            }
        }
        
        foreach (var group in _waypointGroups)
        {
            if (group.AssignedUnit == scout)
            {
                group.UnassignUnit();
                AssignClosestScoutToWaypointGroup(group);
            }
        }
    }
    
    public Beacon FindAvailableBeacon()
    {
        foreach (var beacon in _beacons)
        {
            if (beacon != null && beacon.AssignedUnit == null)
            {
                foreach (var group in _waypointGroups)
                {
                    if (group.Waypoints.Contains(beacon) && group.Waypoints[0] == beacon)
                    {
                        return beacon;
                    }
                }
                return beacon;
            }
        }
        return null;
    }

    private bool IsLoadingScreenActive()
    {
        if (LoadingUIManager.Instance == null)
        {
            return false;
        }

        LoadingScreen loadingScreen = LoadingUIManager.Instance.GetLoadingScreenComponent();
        if (loadingScreen == null)
        {
            return false;
        }

        return loadingScreen.gameObject.activeSelf;
    }
}

