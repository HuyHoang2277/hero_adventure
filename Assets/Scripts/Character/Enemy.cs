using System.Collections;
using UnityEngine;
using DG.Tweening;

public class Enemy : BaseCharacter
{
    [Header("Target Indicator")]
    [SerializeField] private SpriteRenderer targetIndicator;

    public override void BroadcastUIUpdate()
    {
        BattleManager.Instance.GameplayUIManager.RefreshEnemyHP(this);
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
    }

    public void ApplyDifficultyMultiplier(float multiplier)
    {
        currentMaxHealth = RoundStat(currentMaxHealth * multiplier);
        currentHealth = currentMaxHealth;
        currentDamage = RoundStat(currentDamage * multiplier);
        currentShield = RoundStat(currentShield * multiplier);

        BroadcastUIUpdate();
    }

    public void ShowTargetIndicator()
    {
        if (targetIndicator != null)
            targetIndicator.gameObject.SetActive(true);
    }

    public void HideTargetIndicator()
    {
        if (targetIndicator != null)
            targetIndicator.gameObject.SetActive(false);
    }
}