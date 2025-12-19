using System.Collections;
using UnityEngine;

public class Unit_Scout : UnitBase
{
    [Header("Scout Settings")]
    [SerializeField] private UnitMovement unitMovement;
    [SerializeField] private float arrivalTolerance = 0.2f;
    
    private Beacon _assignedBeacon;
    private BeaconWaypointGroup _assignedWaypointGroup;
    private Vector3 _homePosition;
    private Coroutine _beaconCoroutine;
    private bool _isReturningHome;
    
    public bool IsAssignedToBeacon => _assignedBeacon != null || _assignedWaypointGroup != null;
    
    protected override void Awake()
    {
        base.Awake();
        unitMovement = GetComponent<UnitMovement>();
    }
    
    private void Start()
    {
        MainStructure mainStructure = FindFirstObjectByType<MainStructure>();
        
        if (mainStructure != null)
        {
            _homePosition = mainStructure.transform.position;
        }
    }
    
    private void Update()
    {
        if (currentState == UnitState.Moving)
        {
            CheckBeaconArrival();
        }
    }
    
    protected override void OnDisable()
    {
        base.OnDisable();
        
        if (_beaconCoroutine != null)
        {
            StopCoroutine(_beaconCoroutine);
            _beaconCoroutine = null;
        }
    }
    
    private void OnDestroy()
    {
        if (BeaconManager.Instance != null)
        {
            BeaconManager.Instance.OnScoutDestroyed(this);
        }
        
        if (_assignedBeacon != null)
        {
            _assignedBeacon.UnassignUnit();
        }
        
        if (_assignedWaypointGroup != null)
        {
            _assignedWaypointGroup.UnassignUnit();
        }
    }
    
    public void AssignToBeacon(Beacon beacon)
    {
        if (beacon == null) return;
        
        ClearAssignments();
        
        _assignedBeacon = beacon;
        _isReturningHome = false;
        currentState = UnitState.Moving;
        
        if (unitMovement != null)
        {
            unitMovement.SetNewTarget(beacon.Position);
        }
    }
    
    public void AssignToWaypointGroup(BeaconWaypointGroup group)
    {
        if (group == null || group.Waypoints.Count == 0) return;
        
        ClearAssignments();
        
        _assignedWaypointGroup = group;
        _isReturningHome = false;
        currentState = UnitState.Moving;
        
        Beacon firstWaypoint = group.Waypoints[0];
        if (unitMovement != null)
        {
            unitMovement.SetNewTarget(firstWaypoint.Position);
        }
    }
    
    private void ClearAssignments()
    {
        if (_assignedBeacon != null)
        {
            _assignedBeacon.UnassignUnit();
            _assignedBeacon = null;
        }
        
        if (_assignedWaypointGroup != null)
        {
            _assignedWaypointGroup.UnassignUnit();
            _assignedWaypointGroup = null;
        }
    }
    
    private void CheckBeaconArrival()
    {
        if (unitMovement == null) return;
        
        Vector3 targetPosition = Vector3.zero;
        bool shouldCheckArrival = false;
        
        if (_assignedBeacon != null && !_isReturningHome)
        {
            targetPosition = _assignedBeacon.Position;
            shouldCheckArrival = true;
        }
        else if (_assignedWaypointGroup != null && !_isReturningHome)
        {
            Beacon currentWaypoint = _assignedWaypointGroup.CurrentWaypoint;
            if (currentWaypoint != null)
            {
                targetPosition = currentWaypoint.Position;
                shouldCheckArrival = true;
            }
        }
        else if (_isReturningHome)
        {
            targetPosition = _homePosition;
            shouldCheckArrival = true;
        }
        
        if (shouldCheckArrival && Vector3.Distance(transform.position, targetPosition) <= arrivalTolerance)
        {
            OnArrivedAtTarget();
        }
    }
    
    private void OnArrivedAtTarget()
    {
        if (_isReturningHome)
        {
            currentState = UnitState.Idle;
            _isReturningHome = false;
            ClearAssignments();
            return;
        }
        
        if (_assignedBeacon != null)
        {
            if (_beaconCoroutine != null)
            {
                StopCoroutine(_beaconCoroutine);
            }
            _beaconCoroutine = StartCoroutine(StayAtBeacon(_assignedBeacon));
        }
        else if (_assignedWaypointGroup != null)
        {
            Beacon currentWaypoint = _assignedWaypointGroup.CurrentWaypoint;
            if (currentWaypoint != null)
            {
                if (_beaconCoroutine != null)
                {
                    StopCoroutine(_beaconCoroutine);
                }
                _beaconCoroutine = StartCoroutine(StayAtWaypoint(currentWaypoint));
            }
            else
            {
                ReturnHome();
            }
        }
    }
    
    private IEnumerator StayAtBeacon(Beacon beacon)
    {
        currentState = UnitState.Idle;
        unitMovement?.StopMovement();
        
        yield return new WaitForSeconds(beacon.StayInterval);
        
        ReturnHome();
    }
    
    private IEnumerator StayAtWaypoint(Beacon waypoint)
    {
        currentState = UnitState.Idle;
        unitMovement?.StopMovement();
        
        yield return new WaitForSeconds(waypoint.StayInterval);
        
        if (waypoint != null)
        {
            waypoint.UnassignUnit();
            waypoint.DestroyBeacon();
        }
        
        if (_assignedWaypointGroup != null && _assignedWaypointGroup.HasMoreWaypoints())
        {
            Beacon nextWaypoint = _assignedWaypointGroup.GetNextWaypoint();
            if (nextWaypoint != null && unitMovement != null)
            {
                nextWaypoint.AssignUnit(this);
                currentState = UnitState.Moving;
                unitMovement.SetNewTarget(nextWaypoint.Position);
            }
            else
            {
                ReturnHome();
            }
        }
        else
        {
            if (BeaconManager.Instance != null)
            {
                BeaconManager.Instance.CompleteWaypointGroup(_assignedWaypointGroup);
            }
            ReturnHome();
        }
    }
    
    private void ReturnHome()
    {
        _isReturningHome = true;
        currentState = UnitState.Moving;
        
        if (_assignedBeacon != null)
        {
            _assignedBeacon.UnassignUnit();
            _assignedBeacon.DestroyBeacon();
            _assignedBeacon = null;
        }
        
        if (_assignedWaypointGroup != null)
        {
            Beacon currentWaypoint = _assignedWaypointGroup.CurrentWaypoint;
            if (currentWaypoint != null)
            {
                currentWaypoint.UnassignUnit();
            }
            _assignedWaypointGroup.UnassignUnit();
            _assignedWaypointGroup = null;
        }
        
        if (unitMovement != null)
        {
            unitMovement.SetNewTarget(_homePosition);
        }
    }
    
    public void OnBeaconDestroyed(Beacon beacon)
    {
        if (_assignedBeacon == beacon)
        {
            _assignedBeacon = null;
            ReturnHome();
        }
        
        if (_assignedWaypointGroup != null)
        {
            bool wasInGroup = false;
            foreach (var waypoint in _assignedWaypointGroup.Waypoints)
            {
                if (waypoint == beacon)
                {
                    wasInGroup = true;
                    break;
                }
            }
            
            if (wasInGroup)
            {
                Beacon currentWaypoint = _assignedWaypointGroup.CurrentWaypoint;
                if (beacon == currentWaypoint || 
                    _assignedWaypointGroup.Waypoints.IndexOf(beacon) > _assignedWaypointGroup.Waypoints.IndexOf(currentWaypoint))
                {
                    _assignedWaypointGroup.UnassignUnit();
                    _assignedWaypointGroup = null;
                    ReturnHome();
                }
            }
        }
    }
}

