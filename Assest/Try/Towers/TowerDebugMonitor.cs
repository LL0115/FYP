using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;

public class TowerDebugMonitor : MonoBehaviour
{
    [Tooltip("Enable to see detailed logs about tower status")]
    public bool enableDetailedLogs = true;
    
    [Tooltip("How often to check tower status (seconds)")]
    public float checkInterval = 5f;
    
    private float nextCheckTime = 0f;
    
    void Update()
    {
        if (!enableDetailedLogs) return;
        
        if (Time.time > nextCheckTime)
        {
            nextCheckTime = Time.time + checkInterval;
            CheckAllTowers();
        }
    }
    
    void CheckAllTowers()
    {
        TowerBehaviour[] allTowers = FindObjectsOfType<TowerBehaviour>();
        Debug.Log($"<color=yellow>Tower Status Check: Found {allTowers.Length} towers</color>");
        
        foreach (TowerBehaviour tower in allTowers)
        {
            if (tower == null) continue;
            
            string targetInfo = tower.Target != null ? 
                $"Targeting {tower.Target.name} (Health: {tower.Target.Health})" : 
                "No target";
                
            string pivotInfo = tower.TowerPivot != null ?
                $"Pivot OK, rotation: {tower.TowerPivot.rotation.eulerAngles}" :
                "NO PIVOT";
                
            string networkInfo = tower.photonView != null ?
                $"PhotonView {tower.photonView.ViewID}, IsMine: {tower.photonView.IsMine}" :
                "NO PHOTONVIEW";
                
            Debug.Log($"<color=cyan>Tower {tower.gameObject.name}</color>: " +
                      $"Path {tower.ownerPathIndex}, " + 
                      $"{targetInfo}, " +
                      $"{pivotInfo}, " + 
                      $"{networkInfo}");
        }
    }
    
    
    // Add this to your GameloopManager or other persistent object
    [ContextMenu("Fix All Tower Pivots")]
    public void FixAllTowerPivots()
    {
        TowerBehaviour[] allTowers = FindObjectsOfType<TowerBehaviour>();
        Debug.Log($"Attempting to fix pivots for {allTowers.Length} towers");
        
        foreach (TowerBehaviour tower in allTowers)
        {
            if (tower == null) continue;
            
            if (tower.TowerPivot == null)
            {
                Debug.Log($"Fixing missing pivot for tower {tower.gameObject.name}");
                // Try to find a suitable pivot
                Transform pivot = tower.transform.Find("Pivot");
                if (pivot == null) pivot = tower.transform.Find("TowerPivot");
                if (pivot == null && tower.transform.childCount > 0) pivot = tower.transform.GetChild(0);
                
                if (pivot != null)
                {
                    tower.TowerPivot = pivot;
                    Debug.Log($"  Found pivot: {pivot.name}");
                }
                else
                {
                    Debug.LogWarning($"  No suitable pivot found!");
                }
            }
        }
    }
}