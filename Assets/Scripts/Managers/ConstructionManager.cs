using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class ConstructionManager : MonoBehaviour
{
    private readonly List<ConstructionSite> _constructionSites = new List<ConstructionSite>();
    private readonly List<Unit_Construct> _constructDrones = new List<Unit_Construct>();
    
    public static ConstructionManager Instance { get; private set; }
    
    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            Debug.Log("[ConstructionManager] Instance initialized");
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
        
        if (_constructionSites.Count > 0)
        {
            AssignDronesToSites();
        }
    }
    
    public void RegisterConstructionSite(ConstructionSite site)
    {
        if (site == null)
        {
            Debug.LogWarning("[ConstructionManager] Attempted to register null construction site");
            return;
        }
        
        if (!_constructionSites.Contains(site))
        {
            _constructionSites.Add(site);
            Debug.Log($"[ConstructionManager] Registered construction site at {site.cellPosition}. Total sites: {_constructionSites.Count}, Available drones: {_constructDrones.Count}");
            
            AssignDronesToSites();
        }
        else
        {
            Debug.LogWarning($"[ConstructionManager] Construction site at {site.cellPosition} already registered");
        }
    }
    
    public void UnregisterConstructionSite(ConstructionSite site)
    {
        _constructionSites.Remove(site);
    }
    
    public void RegisterConstructDrone(Unit_Construct drone)
    {
        if (drone == null)
        {
            Debug.LogWarning("[ConstructionManager] Attempted to register null construct drone");
            return;
        }
        
        if (!_constructDrones.Contains(drone))
        {
            _constructDrones.Add(drone);
            Debug.Log($"[ConstructionManager] Registered construct drone: {drone.name}. Total drones: {_constructDrones.Count}, Active sites: {_constructionSites.Count}");
            
            AssignDroneToSite(drone);
        }
        else
        {
            Debug.LogWarning($"[ConstructionManager] Construct drone {drone.name} already registered");
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
            Debug.LogWarning("[ConstructionManager] RequestTask called with null drone");
            return;
        }
        
        TryAssignResourceFetchTask(drone, prioritySite);
    }
    
    private bool TryAssignResourceFetchTask(Unit_Construct drone, ConstructionSite prioritySite)
    {
        if (prioritySite != null && !prioritySite.IsComplete && TryAssignResourceRequest(drone, prioritySite, true))
        {
            return true;
        }
        
        foreach (var site in _constructionSites)
        {
            if (site != null && !site.IsComplete && TryAssignResourceRequest(drone, site, false))
            {
                return true;
            }
        }
        
        return false;
    }
    
    private bool TryAssignResourceRequest(Unit_Construct drone, ConstructionSite site, bool isPriority)
    {
        ConstructionSite.ConstructionRequest request = site.GetAndAssignNextResourceRequest(drone, drone.carryCapacity);
        if (request == null) return false;
        
        IStorage storage = ResourceManager.Instance?.FindClosestStorageWithResource(site.GetPosition(), request.type, 1);
        if (storage == null)
        {
            site.CancelRequest(request);
            drone.SetTaskRequestCooldown(1.0f);
            return false;
        }
        
        drone.SetTask_FetchResource(request, site);
        string priority = isPriority ? " (priority)" : "";
        Debug.Log($"[ConstructionManager] Assigned drone '{drone.name}' to fetch {request.amount} {request.type} for site at {site.cellPosition}{priority}");
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
        
        RequestTask(drone);
    }
    
    public void OnSiteResourceDelivered(ConstructionSite site, Unit_Construct deliveringDrone = null)
    {
        if (site == null || site.comboCardData == null || site.AreAllPiecesConstructed())
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
            if (site.comboCardData != null && BuildingManager.Instance != null)
            {
                BuildingManager.Instance.CreateComboBuildingFromConstruction(site.cellPosition, site.comboCardData);
                Debug.Log($"[ConstructionManager] All pieces constructed for '{site.comboCardData.displayName}', spawning combo prefab");
            }
        }
        else
        {
            AssignDronesToSites();
        }
    }
}

