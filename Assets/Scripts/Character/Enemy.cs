using System.Collections;
using UnityEngine;
using DG.Tweening;

public class Enemy : BaseCharacter
{
    [Header("Target Indicator")]
    [SerializeField] private SpriteRenderer targetIndicator;

    private CharacterWorldHUD worldHUD;
    private int maxSupportCooldown = 2;
    private int currentSupportCooldown = 2;
    private bool isActingNext = false;
    private bool isTargetSelectionHighlighted = false;
    private Vector3 indicatorBaseScale = Vector3.one;
    private bool hasRecordedIndicatorScale = false;

    public int SupportCooldown => currentSupportCooldown;
    public bool IsActingNext => isActingNext;

    public override void InitStat(CharacterInfoSO statData = null)
    {
        EnsureWorldHUD();
        base.InitStat(statData);
    }

    private void EnsureWorldHUD()
    {
        if (worldHUD == null)
        {
            worldHUD = GetComponent<CharacterWorldHUD>();
            if (worldHUD == null) worldHUD = gameObject.AddComponent<CharacterWorldHUD>();
        }
        worldHUD.Initialize(true);
    }

    public void InitializeIntent(int initialCooldown, int maxCooldown, bool actingNext)
    {
        maxSupportCooldown = Mathf.Max(1, maxCooldown);
        currentSupportCooldown = Mathf.Max(1, initialCooldown);
        isActingNext = actingNext;
        EnsureWorldHUD();
        worldHUD.UpdateIntent(isActingNext, currentSupportCooldown);
        RefreshGroundRingVisual();
    }

    public void SetActingNext(bool actingNext)
    {
        isActingNext = actingNext;
        if (worldHUD != null)
        {
            worldHUD.UpdateIntent(isActingNext, currentSupportCooldown);
        }
        RefreshGroundRingVisual();
    }

    public void ResetSupportCooldown()
    {
        currentSupportCooldown = maxSupportCooldown;
        if (worldHUD != null)
        {
            worldHUD.UpdateIntent(isActingNext, currentSupportCooldown);
        }
    }

    /// <summary>
    /// Giảm hồi chiêu đánh phụ của quái hỗ trợ. Trả về true nếu sẵn sàng tung đòn đánh phụ trong lượt này.
    /// </summary>
    public bool TickSupportCooldown()
    {
        currentSupportCooldown--;
        if (currentSupportCooldown <= 0)
        {
            currentSupportCooldown = maxSupportCooldown;
            if (worldHUD != null) worldHUD.UpdateIntent(isActingNext, currentSupportCooldown);
            return true;
        }

        if (worldHUD != null) worldHUD.UpdateIntent(isActingNext, currentSupportCooldown);
        return false;
    }

    public override void BroadcastUIUpdate()
    {
        if (worldHUD != null)
        {
            worldHUD.RefreshStats(currentHealth, currentMaxHealth, currentShield);
        }
        if (BattleManager.Instance != null && BattleManager.Instance.GameplayUIManager != null)
        {
            BattleManager.Instance.GameplayUIManager.RefreshEnemyHP(this);
        }
    }

    protected override void Die()
    {
        if (worldHUD != null) worldHUD.SetVisible(false);
        base.Die();
    }

    protected override void DestroyOrDespawn()
    {
        StartCoroutine(DespawnAfterDelay(1f));
    }

    private IEnumerator DespawnAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        HideTargetIndicator();
        PoolingManager.Despawn(gameObject);
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        transform.DOKill();
        HideTargetIndicator();
        if (worldHUD != null) worldHUD.SetVisible(false);
    }

    protected override void SyncOriginalPosition()
    {
        // Quái tái sử dụng từ Object Pool luôn cập nhật vị trí gốc theo spawnPoint mới
        initialSpawnPosition = transform.position;
        hasRecordedInitialPos = true;
        originalPosition = transform.position;
    }

    public void ApplyDifficultyMultiplier(float multiplier)
    {
        ApplyDifficultyMultiplier(multiplier, multiplier, multiplier);
    }

    public void ApplyDifficultyMultiplier(float hpMultiplier, float damageMultiplier, float shieldMultiplier)
    {
        currentMaxHealth = RoundStat(currentMaxHealth * hpMultiplier);
        currentHealth = currentMaxHealth;
        currentDamage = RoundStat(currentDamage * damageMultiplier);
        currentShield = RoundStat(currentShield * shieldMultiplier);

        BroadcastUIUpdate();
    }

    public void ShowTargetIndicator()
    {
        isTargetSelectionHighlighted = true;
        RefreshGroundRingVisual();
    }

    public void HideTargetIndicator()
    {
        isTargetSelectionHighlighted = false;
        RefreshGroundRingVisual();
    }

    private void RefreshGroundRingVisual()
    {
        if (targetIndicator == null) return;

        if (!hasRecordedIndicatorScale)
        {
            indicatorBaseScale = targetIndicator.transform.localScale;
            if (indicatorBaseScale.sqrMagnitude < 0.0001f) indicatorBaseScale = Vector3.one;
            hasRecordedIndicatorScale = true;
        }

        targetIndicator.transform.DOKill();
        targetIndicator.transform.localScale = indicatorBaseScale;

        if (IsDead() || !gameObject.activeInHierarchy)
        {
            targetIndicator.gameObject.SetActive(false);
            return;
        }

        if (isTargetSelectionHighlighted)
        {
            targetIndicator.gameObject.SetActive(true);
            targetIndicator.color = new Color(1f, 0.28f, 0.22f, 1f);
            targetIndicator.transform
                .DOScale(indicatorBaseScale * 1.14f, 0.38f)
                .SetEase(Ease.InOutSine)
                .SetLoops(-1, LoopType.Yoyo);
        }
        else if (isActingNext)
        {
            // Vòng sáng hổ phách mờ báo hiệu quái sẽ đi cờ ở lượt kế tiếp
            targetIndicator.gameObject.SetActive(true);
            targetIndicator.color = new Color(1f, 0.76f, 0.24f, 0.46f);
        }
        else
        {
            targetIndicator.gameObject.SetActive(false);
        }
    }
}