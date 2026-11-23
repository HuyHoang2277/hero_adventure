using System.Collections.Generic;
using UnityEngine;

public class GemSpawner : MonoBehaviour
{
    [SerializeField] private Gem gemPrefab;
    [SerializeField] private List<GemData> gemDataList = new List<GemData>();

    public Gem SpawnGem(Vector2 position, Transform parent)
    {
        Gem newGem = PoolingManager.Spawn<Gem>(gemPrefab, position, Quaternion.identity, parent);
        return newGem;
    }

    public void DespawnGem(Gem gem)
    {
        PoolingManager.Despawn(gem.gameObject);
    }

    public GemData GetRandomGemData()
    {
        return GetRandomGemDataExcept(null);
    }

    public GemData GetRandomGemDataExcept(List<GemType> excludedTypes)
    {
        List<GemData> filtered = new List<GemData>();
        int totalWeight = 0;

        foreach (GemData data in gemDataList)
        {
            if (data == null) continue;
            if (excludedTypes != null && excludedTypes.Contains(data.gemType))
                continue;

            filtered.Add(data);
            int weight = (data.spawnWeight > 0) ? data.spawnWeight : 1;
            totalWeight += weight;
        }

        if (filtered.Count == 0)
        {
            return gemDataList[Random.Range(0, gemDataList.Count)];
        }

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;

        foreach (GemData data in filtered)
        {
            int weight = (data.spawnWeight > 0) ? data.spawnWeight : 1;
            cumulative += weight;
            if (roll < cumulative)
            {
                return data;
            }
        }

        return filtered[filtered.Count - 1];
    }
}