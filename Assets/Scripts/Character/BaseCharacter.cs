using UnityEngine;
using System;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening;

public class BaseCharacter : MonoBehaviour
{
    [Header("Base Data")]
    [SerializeField] protected CharacterInfoSO baseStatData;
    [SerializeField] protected Animator animator;
    [SerializeField] protected SpriteRenderer spriteRenderer;

    [Header("Current Runtime Stats")]
    protected float currentMaxHealth;
    protected float currentHealth;
    protected float currentShield;
    protected float currentDamage;
    protected float currentCritRate;
    protected float currentCritDamage;
    protected float currentBlockRate;

    // Chỉ số gốc hiệu dụng (gồm baseStatData + nâng cấp vĩnh viễn từ Level/Talent/Equipment)
    protected float effectiveBaseCritRate;
    protected float effectiveBaseCritDamage;
    protected float effectiveBaseBlockRate;

    protected const float MAX_SHIELD_RATIO = 0.50f;
    protected const float MAX_IN_BATTLE_CRIT_RATE_BONUS = 15f;
    protected const float MAX_IN_BATTLE_CRIT_DAMAGE_BONUS = 30f;
    protected const float MAX_IN_BATTLE_DODGE_BONUS = 12f;
    protected const float HARD_CAP_CRIT_RATE = 60f;
    protected const float HARD_CAP_CRIT_DAMAGE = 140f;
    protected const float HARD_CAP_DODGE = 35f;
    protected const float BUFF_DISCHARGE_ON_PROC_RATIO = 0.50f;
    protected const int DEFAULT_BUFF_DURATION_TURNS = 4;
    protected const float DEFAULT_SHIELD_DECAY_RATE = 0.25f;
    protected const float DEFAULT_WAVE_BUFF_DECAY_RATE = 0.50f;

    protected enum BuffStatType
    {
        CritRate,
        CritDamage,
        Dodge
    }

    protected class TimedStatBuff
    {
        public BuffStatType StatType;
        public float Amount;
        public int RemainingTurns;
    }

    protected readonly List<TimedStatBuff> activeTimedBuffs = new List<TimedStatBuff>();

    protected Vector3 originalPosition;
    protected Vector3 initialSpawnPosition;
    protected bool hasRecordedInitialPos = false;

    public float InitialSpawnY => hasRecordedInitialPos ? initialSpawnPosition.y : transform.position.y;

    public float CurrentHealth => currentHealth;
    public float CurrentMaxHealth => currentMaxHealth;
    public float CurrentShield => currentShield;
    public float CurrentDamage => currentDamage;
    public float CurrentCritRate => currentCritRate;
    public float CurrentCritDamage => currentCritDamage;
    public float CurrentBlockRate => currentBlockRate;
    public float LastOverkillDamage { get; protected set; }
    public Sprite CharacterSprite => spriteRenderer.sprite;

    public virtual void BroadcastUIUpdate() { }

    public virtual void InitStat(CharacterInfoSO statData = null)
    {
        if (statData != null) baseStatData = statData;
        
        if (baseStatData == null)
        {
            Debug.LogError($"[BaseCharacter] Lỗi: baseStatData (CharacterInfoSO) chưa được gán cho {gameObject.name}!");
            return;
        }

        activeTimedBuffs.Clear();
        currentMaxHealth = baseStatData.maxHealth;
        currentHealth = currentMaxHealth;
        currentShield = baseStatData.baseShield;
        currentDamage = baseStatData.baseDamage;
        effectiveBaseCritRate = baseStatData.baseCritRate;
        effectiveBaseCritDamage = baseStatData.baseCritDamage;
        effectiveBaseBlockRate = baseStatData.baseBlockRate;
        currentCritRate = effectiveBaseCritRate;
        currentCritDamage = effectiveBaseCritDamage;
        currentBlockRate = effectiveBaseBlockRate;
        
        if (animator != null) animator.runtimeAnimatorController = baseStatData.characterAnim;
        if (spriteRenderer != null) spriteRenderer.sprite = baseStatData.defaultCharacterSprite;

        SyncOriginalPosition();

        if (animator != null)
        {
            if (animator.isActiveAndEnabled)
            {
                animator.Rebind();
                animator.Update(0f);
            }
            animator.SetBool(GameConstants.AnimParams.IsDie, false);
            animator.SetBool(GameConstants.AnimParams.IsRunning, false);
            animator.SetBool(GameConstants.AnimParams.IsHurting, false);
            animator.SetBool(GameConstants.AnimParams.IsBlocking, false);
            animator.SetBool(GameConstants.AnimParams.IsBaseAttacking, false);
            animator.SetBool(GameConstants.AnimParams.IsCritAttacking, false);
        }

        BroadcastUIUpdate();
    }

    protected virtual void SyncOriginalPosition()
    {
        if (!hasRecordedInitialPos)
        {
            initialSpawnPosition = transform.position;
            hasRecordedInitialPos = true;
        }
        else
        {
            transform.position = initialSpawnPosition;
        }
        originalPosition = initialSpawnPosition;
    }

    protected float RoundStat(float value)
    {

        return Mathf.Round(value * 10f) / 10f;

    }

    public IEnumerator PerformAttackSequence(BaseCharacter target, float damageMultiplier)
    {
        Vector3 direction = (transform.position - target.transform.position).normalized;
        Vector3 attackPosition = target.transform.position + direction * 1.25f;
        yield return StartCoroutine(MoveToPosition(attackPosition, 10f));

        bool isCrit;
        float rawDamage = CalculateDamage(out isCrit) * damageMultiplier;
        string animParam = isCrit ? GameConstants.AnimParams.IsCritAttacking : GameConstants.AnimParams.IsBaseAttacking;

        if (AudioManager.Instance != null)
        {
            if (isCrit) AudioManager.Instance.PlayAttackCritSwing();
            else AudioManager.Instance.PlayAttackSwing();
        }

        animator.SetBool(animParam, true);
        yield return null;

        AnimatorStateInfo stateInfo = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
        float animLength = stateInfo.length;

        float impactDelay = animLength * 0.6f;
        yield return new WaitForSeconds(impactDelay);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayAttackHit();
        }

        target.TakeDamage(rawDamage, isCrit);

        yield return new WaitForSeconds(animLength - impactDelay);
        animator.SetBool(animParam, false);

        yield return StartCoroutine(MoveToPosition(originalPosition, 10f));
    }

    private IEnumerator MoveToPosition(Vector3 targetPosition, float speed)
    {
        animator.SetBool(GameConstants.AnimParams.IsRunning, true);
        float distance = Vector3.Distance(transform.position, targetPosition);
        float duration = distance / speed;

        yield return transform.DOMove(targetPosition, duration).SetEase(Ease.Linear).WaitForCompletion();

        transform.position = targetPosition;
        animator.SetBool(GameConstants.AnimParams.IsRunning, false);
        yield return new WaitForSeconds(0.1f);
    }

    public virtual void TakeDamage(float rawDamage, bool isCrit)
    {
        LastOverkillDamage = 0f;

        if (UnityEngine.Random.Range(0f, 100f) <= currentBlockRate)
        {
            if (AudioManager.Instance != null)
            {
                if (this is Player) AudioManager.Instance.PlayPlayerBlock();
                else AudioManager.Instance.PlayEnemyBlock();
            }
            StartCoroutine(PlayAnimationBool(GameConstants.AnimParams.IsBlocking));
            transform.DOShakePosition(0.2f, 0.1f, 15);
            DischargeDodgeBonusOnProc();

            ObserverManager<EventID>.PostEvent(EventID.OnShowDamagePopup, new DamagePopupData
            {
                Position = transform.position + Vector3.up * 0.9f,
                PopupType = FloatingPopupType.Dodge,
                CustomText = "DODGE!"
            });
            CombatVFXManager.Instance?.PlaySupportSparkle(transform.position, new Color(0.68f, 0.94f, 0.28f, 1f));
            return;
        }

        // Pure Barrier 1:1 — Khiên hấp thụ sát thương trực tiếp trước khi trừ vào HP
        float damageToHealth = rawDamage;
        if (currentShield > 0f && damageToHealth > 0f)
        {
            float absorbed = Mathf.Min(currentShield, damageToHealth);
            currentShield = RoundStat(Mathf.Max(0f, currentShield - absorbed));
            damageToHealth = RoundStat(Mathf.Max(0f, damageToHealth - absorbed));
        }

        if (damageToHealth > 0f)
        {
            float hpBefore = currentHealth;
            if (damageToHealth > hpBefore)
            {
                LastOverkillDamage = RoundStat(damageToHealth - hpBefore);
            }

            currentHealth -= damageToHealth;
            currentHealth = RoundStat(Mathf.Clamp(currentHealth, 0f, currentMaxHealth));

            ObserverManager<EventID>.PostEvent(EventID.OnShowDamagePopup, new DamagePopupData
            {
                Position = transform.position + Vector3.up * 0.95f,
                Damage = Mathf.RoundToInt(damageToHealth),
                IsCritical = isCrit,
                PopupType = isCrit ? FloatingPopupType.CriticalDamage : FloatingPopupType.NormalDamage
            });

            CombatVFXManager.Instance?.PlayHitImpact(transform.position, isCrit);
            if (isCrit)
            {
                CombatVFXManager.Instance?.TriggerHitStop(0.055f);
                CombatVFXManager.Instance?.TriggerHaptic(40);
            }

            if (currentHealth <= 0f)
            {
                if (AudioManager.Instance != null)
                {
                    if (this is Player) AudioManager.Instance.PlayPlayerDie();
                    else AudioManager.Instance.PlayEnemyDie();
                }
                animator.SetBool(GameConstants.AnimParams.IsDie, true);
                Die();
            }
            else
            {
                if (AudioManager.Instance != null)
                {
                    if (this is Player) AudioManager.Instance.PlayPlayerHurt();
                    else AudioManager.Instance.PlayEnemyHurt();
                }
                StartCoroutine(PlayAnimationBool(GameConstants.AnimParams.IsHurting));
                transform.DOShakePosition(0.3f, 0.3f, 20);
            }
        }
        else if (rawDamage > 0f)
        {
            // Khiên chặn hoàn toàn sát thương
            if (AudioManager.Instance != null)
            {
                if (this is Player) AudioManager.Instance.PlayPlayerBlock();
                else AudioManager.Instance.PlayEnemyBlock();
            }
            StartCoroutine(PlayAnimationBool(GameConstants.AnimParams.IsBlocking));
            transform.DOShakePosition(0.2f, 0.12f, 15);

            ObserverManager<EventID>.PostEvent(EventID.OnShowDamagePopup, new DamagePopupData
            {
                Position = transform.position + Vector3.up * 0.9f,
                Damage = 0,
                IsCritical = false,
                PopupType = FloatingPopupType.Blocked,
                CustomText = "BLOCKED"
            });
            CombatVFXManager.Instance?.PlaySupportSparkle(transform.position, new Color(0.32f, 0.78f, 0.99f, 1f));
        }

        BroadcastUIUpdate();
    }

    public virtual void Heal(float amount)
    {
        float before = currentHealth;
        currentHealth = RoundStat(Mathf.Min(currentMaxHealth, currentHealth + amount));
        float actualHealed = currentHealth - before;

        if (actualHealed >= 0.5f)
        {
            ObserverManager<EventID>.PostEvent(EventID.OnShowDamagePopup, new DamagePopupData
            {
                Position = transform.position + Vector3.up * 0.85f,
                Damage = Mathf.RoundToInt(actualHealed),
                PopupType = FloatingPopupType.Heal,
                CustomText = $"+{Mathf.RoundToInt(actualHealed)} HP"
            });
            CombatVFXManager.Instance?.PlaySupportSparkle(transform.position, new Color(0.28f, 0.88f, 0.48f, 1f));
        }
        BroadcastUIUpdate();
    }

    public virtual void AddDamage(float amount)
    {
        currentDamage = RoundStat(currentDamage + amount);
        BroadcastUIUpdate();
    }

    public virtual void AddShield(float amount)
    {
        float before = currentShield;
        float maxShieldCap = RoundStat(currentMaxHealth * MAX_SHIELD_RATIO);
        currentShield = RoundStat(Mathf.Min(maxShieldCap, currentShield + amount));
        float actualAdded = currentShield - before;

        if (actualAdded >= 0.5f)
        {
            ObserverManager<EventID>.PostEvent(EventID.OnShowDamagePopup, new DamagePopupData
            {
                Position = transform.position + Vector3.up * 0.85f,
                Damage = Mathf.RoundToInt(actualAdded),
                PopupType = FloatingPopupType.Shield,
                CustomText = $"+{Mathf.RoundToInt(actualAdded)} SHD"
            });
            CombatVFXManager.Instance?.PlaySupportSparkle(transform.position, new Color(0.32f, 0.78f, 0.99f, 1f));
        }
        BroadcastUIUpdate();
    }

    public virtual void DecayShield(float decayRate = DEFAULT_SHIELD_DECAY_RATE)
    {
        if (currentShield <= 0f) return;

        float retained = RoundStat(currentShield * (1f - Mathf.Clamp01(decayRate)));
        currentShield = retained < 0.5f ? 0f : retained;
        BroadcastUIUpdate();
    }

    public virtual void TickTurnBuffs()
    {
        if (activeTimedBuffs.Count == 0) return;

        bool changed = false;
        for (int i = activeTimedBuffs.Count - 1; i >= 0; i--)
        {
            activeTimedBuffs[i].RemainingTurns--;
            if (activeTimedBuffs[i].RemainingTurns <= 0)
            {
                activeTimedBuffs.RemoveAt(i);
                changed = true;
            }
        }

        if (changed)
        {
            RecalculateBuffedStats();
            BroadcastUIUpdate();
        }
    }

    public virtual void DecayWaveBuffs(float decayRate = DEFAULT_WAVE_BUFF_DECAY_RATE)
    {
        if (baseStatData == null || activeTimedBuffs.Count == 0) return;

        float retain = 1f - Mathf.Clamp01(decayRate);
        for (int i = activeTimedBuffs.Count - 1; i >= 0; i--)
        {
            activeTimedBuffs[i].Amount *= retain;
            if (activeTimedBuffs[i].Amount < 0.3f)
            {
                activeTimedBuffs.RemoveAt(i);
            }
        }

        RecalculateBuffedStats();
        BroadcastUIUpdate();
    }

    protected void RecalculateBuffedStats()
    {
        float baseCritRate = effectiveBaseCritRate;
        float baseCritDmg = effectiveBaseCritDamage;
        float baseDodge = effectiveBaseBlockRate;

        float bonusCritRate = 0f;
        float bonusCritDmg = 0f;
        float bonusDodge = 0f;

        for (int i = 0; i < activeTimedBuffs.Count; i++)
        {
            TimedStatBuff buff = activeTimedBuffs[i];
            switch (buff.StatType)
            {
                case BuffStatType.CritRate: bonusCritRate += buff.Amount; break;
                case BuffStatType.CritDamage: bonusCritDmg += buff.Amount; break;
                case BuffStatType.Dodge: bonusDodge += buff.Amount; break;
            }
        }

        float critRateCap = Mathf.Min(HARD_CAP_CRIT_RATE, baseCritRate + MAX_IN_BATTLE_CRIT_RATE_BONUS);
        float critDmgCap = Mathf.Min(HARD_CAP_CRIT_DAMAGE, baseCritDmg + MAX_IN_BATTLE_CRIT_DAMAGE_BONUS);
        float dodgeCap = Mathf.Min(HARD_CAP_DODGE, baseDodge + MAX_IN_BATTLE_DODGE_BONUS);

        currentCritRate = RoundStat(Mathf.Clamp(baseCritRate + bonusCritRate, 0f, critRateCap));
        currentCritDamage = RoundStat(Mathf.Clamp(baseCritDmg + bonusCritDmg, 50f, critDmgCap));
        currentBlockRate = RoundStat(Mathf.Clamp(baseDodge + bonusDodge, 0f, dodgeCap));
    }

    /// <summary>
    /// Cộng tỉ lệ chí mạng có thời hạn (4 lượt, tối đa +15% so với gốc). Trả về phần dư vượt trần để chuyển hóa.
    /// </summary>
    public virtual float AddCritRate(float amount)
    {
        float cap = Mathf.Min(HARD_CAP_CRIT_RATE, effectiveBaseCritRate + MAX_IN_BATTLE_CRIT_RATE_BONUS);
        float availableRoom = Mathf.Max(0f, cap - currentCritRate);
        float applied = Mathf.Min(amount, availableRoom);
        float overflow = Mathf.Max(0f, amount - applied);

        if (applied > 0.01f)
        {
            activeTimedBuffs.Add(new TimedStatBuff
            {
                StatType = BuffStatType.CritRate,
                Amount = applied,
                RemainingTurns = DEFAULT_BUFF_DURATION_TURNS
            });
            RecalculateBuffedStats();
            ObserverManager<EventID>.PostEvent(EventID.OnShowDamagePopup, new DamagePopupData
            {
                Position = transform.position + Vector3.up * 0.85f,
                PopupType = FloatingPopupType.Buff,
                CustomText = $"+{applied:0.#}% CRIT"
            });
            BroadcastUIUpdate();
        }
        return overflow;
    }

    /// <summary>
    /// Cộng sát thương chí mạng có thời hạn (4 lượt, tối đa +30% so với gốc -> trần đầu game 80%). Trả về phần dư vượt trần.
    /// </summary>
    public virtual float AddCritDamage(float amount)
    {
        float cap = Mathf.Min(HARD_CAP_CRIT_DAMAGE, effectiveBaseCritDamage + MAX_IN_BATTLE_CRIT_DAMAGE_BONUS);
        float availableRoom = Mathf.Max(0f, cap - currentCritDamage);
        float applied = Mathf.Min(amount, availableRoom);
        float overflow = Mathf.Max(0f, amount - applied);

        if (applied > 0.01f)
        {
            activeTimedBuffs.Add(new TimedStatBuff
            {
                StatType = BuffStatType.CritDamage,
                Amount = applied,
                RemainingTurns = DEFAULT_BUFF_DURATION_TURNS
            });
            RecalculateBuffedStats();
            ObserverManager<EventID>.PostEvent(EventID.OnShowDamagePopup, new DamagePopupData
            {
                Position = transform.position + Vector3.up * 0.85f,
                PopupType = FloatingPopupType.Buff,
                CustomText = $"+{applied:0.#}% CDMG"
            });
            BroadcastUIUpdate();
        }
        return overflow;
    }

    /// <summary>
    /// Cộng tỉ lệ né đòn có thời hạn (4 lượt, tối đa +12% so với gốc -> trần đầu game 17%). Trả về phần dư vượt trần.
    /// </summary>
    public virtual float AddDodge(float amount)
    {
        float cap = Mathf.Min(HARD_CAP_DODGE, effectiveBaseBlockRate + MAX_IN_BATTLE_DODGE_BONUS);
        float availableRoom = Mathf.Max(0f, cap - currentBlockRate);
        float applied = Mathf.Min(amount, availableRoom);
        float overflow = Mathf.Max(0f, amount - applied);

        if (applied > 0.01f)
        {
            activeTimedBuffs.Add(new TimedStatBuff
            {
                StatType = BuffStatType.Dodge,
                Amount = applied,
                RemainingTurns = DEFAULT_BUFF_DURATION_TURNS
            });
            RecalculateBuffedStats();
            ObserverManager<EventID>.PostEvent(EventID.OnShowDamagePopup, new DamagePopupData
            {
                Position = transform.position + Vector3.up * 0.85f,
                PopupType = FloatingPopupType.Buff,
                CustomText = $"+{applied:0.#}% DGE"
            });
            BroadcastUIUpdate();
        }
        return overflow;
    }

    protected virtual void DischargeCritBonusOnProc()
    {
        if (activeTimedBuffs.Count == 0) return;

        float retain = 1f - BUFF_DISCHARGE_ON_PROC_RATIO;
        bool changed = false;
        for (int i = activeTimedBuffs.Count - 1; i >= 0; i--)
        {
            if (activeTimedBuffs[i].StatType == BuffStatType.CritRate ||
                activeTimedBuffs[i].StatType == BuffStatType.CritDamage)
            {
                activeTimedBuffs[i].Amount *= retain;
                changed = true;
                if (activeTimedBuffs[i].Amount < 0.3f)
                {
                    activeTimedBuffs.RemoveAt(i);
                }
            }
        }

        if (changed)
        {
            RecalculateBuffedStats();
            BroadcastUIUpdate();
        }
    }

    protected virtual void DischargeDodgeBonusOnProc()
    {
        if (activeTimedBuffs.Count == 0) return;

        float retain = 1f - BUFF_DISCHARGE_ON_PROC_RATIO;
        bool changed = false;
        for (int i = activeTimedBuffs.Count - 1; i >= 0; i--)
        {
            if (activeTimedBuffs[i].StatType == BuffStatType.Dodge)
            {
                activeTimedBuffs[i].Amount *= retain;
                changed = true;
                if (activeTimedBuffs[i].Amount < 0.3f)
                {
                    activeTimedBuffs.RemoveAt(i);
                }
            }
        }

        if (changed)
        {
            RecalculateBuffedStats();
            BroadcastUIUpdate();
        }
    }

    public virtual float CalculateDamage(out bool isCrit)
    {
        float damageOut = currentDamage;
        isCrit = false;
        if (UnityEngine.Random.Range(0f, 100f) <= currentCritRate)
        {
            damageOut += (damageOut * (currentCritDamage / 100f));
            isCrit = true;
            DischargeCritBonusOnProc();
        }
        damageOut = Mathf.Round(damageOut * 10f) / 10f;
        return damageOut;
    }

    public bool IsDead() => currentHealth <= 0;

    protected virtual void Die()
    {
        transform.DOKill();
        DestroyOrDespawn();
    }

    protected virtual void DestroyOrDespawn()
    {
        Destroy(gameObject, 1f);
    }

    public void SetRunningAnimation(bool isRunning)
    {
        if (animator != null) animator.SetBool(GameConstants.AnimParams.IsRunning, isRunning);
    }

    public IEnumerator PlayAnimationBool(string paramName)
    {
        if (animator != null)
        {
            animator.SetBool(paramName, true);
            yield return null;
            AnimatorStateInfo stateInfo = animator.IsInTransition(0) ? animator.GetNextAnimatorStateInfo(0) : animator.GetCurrentAnimatorStateInfo(0);
            yield return new WaitForSeconds(stateInfo.length);
            animator.SetBool(paramName, false);
        }
    }
}