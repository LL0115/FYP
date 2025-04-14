// Create this as a new file: MapData.cs
using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "MapData", menuName = "Game/Map Data", order = 1)]
public class MapData : ScriptableObject
{
    [System.Serializable]
    public class MapInfo
    {
        public string MapKey;
        public string DisplayName;
        public string SceneName;
        [TextArea(3, 6)]
        public string Description;
        public Texture2D PreviewImage;
    }
    
    public List<MapInfo> maps = new List<MapInfo>();
    
    // Helper method to get map by key
    public MapInfo GetMap(string key)
    {
        return maps.Find(m => m.MapKey == key);
    }
    
    // Helper to get all map keys
    public List<string> GetAllMapKeys()
    {
        List<string> keys = new List<string>();
        foreach (var map in maps)
        {
            keys.Add(map.MapKey);
        }
        return keys;
    }
}