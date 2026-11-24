using UnityEngine;

public class Player : BaseCharacter
{
    private CharacterWorldHUD worldHUD;

    public float GemPowerMultiplier { get; private set; } = 1f;
    public float Match4BonusHeal { get; private set; } = 0f;

    public override void InitStat(CharacterInfoSO statData = null)
    {
        EnsureWorldHUD();
        base.InitStat(statData);
        ApplyMetaProgressionBonuses();
    }

    private void ApplyMetaProgressionBonuses()
    {
        SecureGameData data = DataManager.Instance != null ? DataManager.Instance.GameData : null;
        PlayerMetaBonuses bonuses = MetaProgressionSystem.CalculatePlayerBonuses(data);

        currentMaxHealth = RoundStat(currentMaxHealth + bonuses.MaxHealth);
        currentHealth = currentMaxHealth;

        float maxShieldCap = RoundStat(currentMaxHealth * MAX_SHIELD_RATIO);
        currentShield = RoundStat(Mathf.Min(maxShieldCap, currentShield + bonuses.Shield + bonuses.StartBattleShield));
        currentDamage = RoundStat(currentDamage + bonuses.Damage);

        effectiveBaseCritRate = RoundStat(Mathf.Min(HARD_CAP_CRIT_RATE, effectiveBaseCritRate + bonuses.CritRate));
        effectiveBaseCritDamage = RoundStat(Mathf.Min(HARD_CAP_CRIT_DAMAGE, effectiveBaseCritDamage + bonuses.CritDamage));
        effectiveBaseBlockRate = RoundStat(Mathf.Min(HARD_CAP_DODGE, effectiveBaseBlockRate + bonuses.Dodge));

        RecalculateBuffedStats();

        GemPowerMultiplier = 1f + bonuses.GemPowerBonusPercent;
        Match4BonusHeal = bonuses.Match4BonusHeal;

        BroadcastUIUpdate();
    }

    private void EnsureWorldHUD()
    {
        if (worldHUD == null)
        {
            worldHUD = GetComponent<CharacterWorldHUD>();
            if (worldHUD == null) worldHUD = gameObject.AddComponent<CharacterWorldHUD>();
        }
        worldHUD.Initialize(false);
    }

    public override void BroadcastUIUpdate()
    {
        if (worldHUD != null)
        {
            worldHUD.RefreshStats(currentHealth, currentMaxHealth, currentShield);
        }
        ObserverManager<EventID>.PostEvent(EventID.OnUpdatePlayerStats, this);
    }

    protected override void Die()
    {
        if (worldHUD != null) worldHUD.SetVisible(false);
        base.Die();
    }

    protected override void DestroyOrDespawn()
    {
        // Tuyệt đối không Destroy(gameObject) của Player để có thể hồi sinh khi bấm Retry hoặc chơi lại màn mới.
    }
}