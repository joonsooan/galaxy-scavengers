using System;
using System.Collections.Generic;
using UnityEngine;

public class ConstructionManager : MonoBehaviour
{
    public static event Action<ConstructionSite> OnConstructionSiteRegistered;
    public static event Action<ConstructionSite> OnConstructionSiteUnregistered;
    private readonly List<ConstructionSite> _constructionSites = new ();
    private readonly List<Unit_Construct> _constructDrones = new ();
    
    public static ConstructionManager Instance { get; private set; }
    public IReadOnlyList<ConstructionSite> ConstructionSites => _constructionSites;
    public IReadOnlyList<Unit_Construct> ConstructDrones => _constructDrones;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
        }
    }
    
    private void Start()
    {
        Unit_Construct[] existingDrones = FindObjectsByType<Unit_Construct>(FindObjectsSortMode.None);
        foreach (var drone in existingDrones)
        {
            if (!_constructDrones.Contains(drone))
            {
                RegisterConstructDrone(drone);
            }
        }
        
        ConstructionSite[] existingSites = FindObjectsByType<ConstructionSite>(FindObjectsSortMode.None);
        foreach (var site in existingSites)
        {
            if (!_constructionSites.Contains(site))
            {
                RegisterConstructionSite(site);
            }
        }
        
        if (_constructionSites.Count > 0)
        {
            AssignDronesToSites();
        }
    }
    
    public void RegisterConstructionSite(ConstructionSite site)
    {
        if (site == null)
        {
            return;
        }
        
        if (!_constructionSites.Contains(site))
        {
            _constructionSites.Add(site);
            OnConstructionSiteRegistered?.Invoke(site);
            AssignDronesToSites();
        }
    }
    
    
    public void UnregisterConstructionSite(ConstructionSite site)
    {
        if (_constructionSites.Remove(site))
        {
            OnConstructionSiteUnregistered?.Invoke(site);
        }
    }
    
    public void RegisterConstructDrone(Unit_Construct drone)
    {
        if (drone == null)
        {
            return;
        }
        
        if (!_constructDrones.Contains(drone))
        {
            _constructDrones.Add(drone);
            AssignDroneToSite(drone);
        }
    }
    
    public void UnregisterConstructDrone(Unit_Construct drone)
    {
        _constructDrones.Remove(drone);
    }
    
    public void RequestTask(Unit_Construct drone, ConstructionSite prioritySite = null)
    {
        if (drone == null)
        {
            return;
        }
        
        TryAssignResourceFetchTask(drone, prioritySite);
    }
    
    private void TryAssignResourceFetchTask(Unit_Construct drone, ConstructionSite prioritySite)
    {
        if (prioritySite != null && !prioritySite.IsComplete && TryAssignResourceRequest(drone, prioritySite))
        {
            return;
        }
        
        foreach (var site in _constructionSites)
        {
            if (site != null && site != prioritySite && !site.IsComplete && TryAssignResourceRequest(drone, site))
            {
                return;
            }
        }
        
        drone.SetTaskRequestCooldown(1.0f);
    }
    
    private bool TryAssignResourceRequest(Unit_Construct drone, ConstructionSite site)
    {
        ConstructionSite.ConstructionRequest request = site.GetAndAssignNextResourceRequest(drone, drone.carryCapacity);
        if (request == null) return false;
        
        IStorage storage = ResourceManager.Instance?.FindClosestStorageWithResource(site.GetPosition(), request.type, request.amount);
        if (storage == null)
        {
            site.CancelRequest(request);
            return false;
        }
        
        drone.SetTask_FetchResource(request, site);
        return true;
    }
    
    private void AssignDronesToSites()
    {
        foreach (var drone in _constructDrones)
        {
            if (drone.IsIdle())
            {
                AssignDroneToSite(drone);
            }
        }
    }
    
    private void AssignDroneToSite(Unit_Construct drone)
    {
        if (drone == null || !drone.IsIdle()) return;
        
        drone.SetTaskRequestCooldown(0f);
        RequestTask(drone);
    }
    
    public void OnSiteResourceDelivered(ConstructionSite site, Unit_Construct deliveringDrone = null)
    {
        if (site == null || site.buildingData == null || site.AreAllPiecesConstructed())
        {
            return;
        }

        if (deliveringDrone != null && deliveringDrone.IsIdle())
        {
            RequestTask(deliveringDrone, site);
        }
        
        foreach (var drone in _constructDrones)
        {
            if (drone != deliveringDrone && drone.IsIdle())
            {
                RequestTask(drone, site);
            }
        }
    }
    
    public void OnPieceConstructed(ConstructionSite site)
    {
        if (site == null) return;

        if (site.AreAllPiecesConstructed())
        {
            if (site.buildingData != null && BuildingManager.Instance != null)
            {
                BuildingManager.Instance.CreateBuildingFromConstruction(site.cellPosition, site.buildingData);
                site.OnDestroy();
            }
        }
        else
        {
            AssignDronesToSites();
        }
    }
}