using UnityEngine;

/// <summary>
/// Asset tập trung toàn bộ hệ số cân bằng chiến đấu & Endless Mode.
/// Hỗ trợ xuất/nạp JSON để tích hợp trực tiếp với Unity Remote Config (UGS LiveOps) mà không cần build lại game.
/// </summary>
[CreateAssetMenu(fileName = "BalanceConfig", menuName = "Game Data/Balance Config")]
public class BalanceConfigSO : ScriptableObject
{
    [Header("Match & Cascade Multipliers")]
    public float multiplierPerGem = 0.25f;
    public float extraBlastGemFactor = 0.35f;
    public float comboBonusPerMatch = 0.10f;
    public float maxComboMultiplier = 1.45f;
    public float cascadeFalloffStep2 = 0.65f;
    public float cascadeFalloffStep3 = 0.45f;
    public float cascadeFalloffStep4Plus = 0.30f;

    [Header("Defense & Buff Rules")]
    public float shieldTurnDecayRate = 0.25f;
    public float waveBuffDecayRate = 0.50f;
    public float critOverflowToDamageBonus = 0.35f;
    public float dodgeOverflowToShieldPercent = 0.05f;

    [Header("Tactical Combat")]
    public float overkillCleaveRatio = 0.50f;
    public float supportStrikeDamageMultiplier = 0.50f;
    public float enemyBasicAttackFallbackMultiplier = 0.65f;
    public int baseSupportCooldown = 2;

    [Header("Endless Scaling")]
    public float endlessHpIncrementPerRound = 0.14f;
    public float endlessDamageIncrementPerRound = 0.08f;
    public float endlessShieldIncrementPerRound = 0.10f;

    public string ToRemoteConfigJson()
    {
        return JsonUtility.ToJson(this, true);
    }

    public bool ApplyRemoteConfigJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return false;
        try
        {
            JsonUtility.FromJsonOverwrite(json, this);
            return true;
        }
        catch (System.Exception ex)
        {
            Debug.LogWarning($"[BalanceConfigSO] Không thể parse JSON Remote Config: {ex.Message}");
            return false;
        }
    }
}
