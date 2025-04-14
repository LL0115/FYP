using UnityEngine;

public class LevelManager : MonoBehaviour
{
    private static LevelManager instance;
    private LevelData[] levels;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
            LoadLevelsFromResources();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void LoadLevelsFromResources()
    {
        // Load all LevelData assets from the Resources folder
        levels = Resources.LoadAll<LevelData>("Wave");

        // Validate levels array
        if (levels == null || levels.Length == 0)
        {
            Debug.LogError("No LevelData found in Resources/Wave folder!");
            return;
        }

        // Validate each level
        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] == null)
            {
                Debug.LogError($"Level {i} is null in LevelManager!");
                return;
            }

            if (levels[i].waves == null || levels[i].waves.Count == 0)
            {
                Debug.LogError($"Level {i} has no waves configured in LevelManager!");
                return;
            }
        }

        Debug.Log($"Successfully loaded {levels.Length} levels from Resources folder");
    }

    public static LevelData[] GetLevels()
    {
        if (instance == null)
        {
            Debug.LogError("LevelManager instance is null!");
            return null;
        }
        return instance.levels;
    }

    // Optional: Method to get a specific level
    public static LevelData GetLevel(int index)
    {
        if (instance == null)
        {
            Debug.LogError("LevelManager instance is null!");
            return null;
        }
        
        if (index < 0 || index >= instance.levels.Length)
        {
            Debug.LogError($"Level index {index} is out of range!");
            return null;
        }
        
        return instance.levels[index];
    }
}