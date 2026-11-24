using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class EndlessModeStrategy : IGameModeStrategy
{
    private int currentRound;
    private float hpDifficultyMultiplier;
    private float damageDifficultyMultiplier;
    private float shieldDifficultyMultiplier;
    private CharacterInfoSO cachedBossSO;

    public int CurrentRound => currentRound;

    private const float DefaultHpIncrementPerRound = 0.14f;
    private const float DefaultDamageIncrementPerRound = 0.08f;
    private const float DefaultShieldIncrementPerRound = 0.10f;
    private const int BossRoundInterval = 5;

    private float hpIncrementPerRound = DefaultHpIncrementPerRound;
    private float damageIncrementPerRound = DefaultDamageIncrementPerRound;
    private float shieldIncrementPerRound = DefaultShieldIncrementPerRound;

    public void Initialize(BattleManager battleManager)
    {
        currentRound = 0;
        hpDifficultyMultiplier = 1.0f;
        damageDifficultyMultiplier = 1.0f;
        shieldDifficultyMultiplier = 1.0f;
        cachedBossSO = Resources.Load<CharacterInfoSO>("SO/CharacterInfo/Golem");

        BalanceConfigSO balanceSO = Resources.Load<BalanceConfigSO>("SO/BalanceConfig");
        hpIncrementPerRound = balanceSO != null ? balanceSO.endlessHpIncrementPerRound : DefaultHpIncrementPerRound;
        damageIncrementPerRound = balanceSO != null ? balanceSO.endlessDamageIncrementPerRound : DefaultDamageIncrementPerRound;
        shieldIncrementPerRound = balanceSO != null ? balanceSO.endlessShieldIncrementPerRound : DefaultShieldIncrementPerRound;
    }

    public List<CharacterInfoSO> GetEnemiesToSpawn(List<CharacterInfoSO> availableEnemies)
    {
        List<CharacterInfoSO> selectedEnemies = new List<CharacterInfoSO>();
        if (availableEnemies == null || availableEnemies.Count == 0) return selectedEnemies;

        // Sắp xếp quái theo máu tăng dần để mở khóa từng bậc theo Round
        List<CharacterInfoSO> sortedPool = new List<CharacterInfoSO>();
        foreach (var enemy in availableEnemies)
        {
            if (enemy != null && enemy != cachedBossSO)
                sortedPool.Add(enemy);
        }
        if (sortedPool.Count == 0) sortedPool.AddRange(availableEnemies);
        sortedPool.Sort((a, b) => a.maxHealth.CompareTo(b.maxHealth));

        if (IsBossWave())
        {
            int minionCount = Mathf.Min(2, 1 + (currentRound / 10));
            for (int i = 0; i < minionCount; i++)
            {
                selectedEnemies.Add(sortedPool[Random.Range(0, sortedPool.Count)]);
            }

            CharacterInfoSO boss = cachedBossSO != null ? cachedBossSO : sortedPool[sortedPool.Count - 1];
            selectedEnemies.Add(boss);
            return selectedEnemies;
        }

        int enemyCount = currentRound <= 1 ? 1 : (currentRound <= 4 ? 2 : Mathf.Min(3, 1 + (currentRound / 3)));

        // Round 1-2: chỉ gặp quái bậc thấp (Slime/Skeleton), từ Round 3 mở Orc, từ Round 8 bỏ quái yếu nhất
        int minIndex = (currentRound >= 8 && sortedPool.Count >= 3) ? 1 : 0;
        int maxExclusive = (currentRound <= 2) ? Mathf.Min(2, sortedPool.Count) : sortedPool.Count;

        for (int i = 0; i < enemyCount; i++)
        {
            selectedEnemies.Add(sortedPool[Random.Range(minIndex, maxExclusive)]);
        }

        return selectedEnemies;
    }

    public IEnumerator OnWaveCleared(BattleManager battleManager)
    {
        currentRound++;
        int completedRounds = Mathf.Max(0, currentRound - 1);
        if (completedRounds > 0 && DataManager.Instance != null && DataManager.Instance.GameData != null)
        {
            DataManager.Instance.GameData.UpdateBestEndlessRound(completedRounds);
            DataManager.Instance.GameData.Save();
        }
        hpDifficultyMultiplier = 1.0f + completedRounds * hpIncrementPerRound;
        damageDifficultyMultiplier = 1.0f + completedRounds * damageIncrementPerRound;
        shieldDifficultyMultiplier = 1.0f + completedRounds * shieldIncrementPerRound;

        ObserverManager<EventID>.PostEvent(EventID.OnUpdateRoundCount, currentRound);

        yield return battleManager.StartCoroutine(battleManager.ExploreRoutine());
    }

    public bool IsGameOver(Player player)
    {
        return player.IsDead();
    }

    public float GetDifficultyMultiplier()
    {
        return hpDifficultyMultiplier;
    }

    public float GetDamageDifficultyMultiplier()
    {
        return damageDifficultyMultiplier;
    }

    public float GetShieldDifficultyMultiplier()
    {
        return shieldDifficultyMultiplier;
    }

    public bool IsBossWave()
    {
        return currentRound > 0 && currentRound % BossRoundInterval == 0;
    }

    public int GetCompletedWaves()
    {
        return Mathf.Max(0, currentRound - 1);
    }

    public int GetTotalWaves()
    {
        return Mathf.Max(1, currentRound);
    }
}
