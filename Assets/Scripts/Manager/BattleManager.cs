using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using DG.Tweening;

public class BattleManager : Singleton<BattleManager>
{
    [Header("State")]
    [SerializeField] private GameState currentState;
    public GameState CurrentState => currentState;
    private GameState previousState;

    [SerializeField] private int maxActionPoints = 1;
    [SerializeField] private int currentActionPoints;

    [Header("Level Progress")]
    [SerializeField] private BackgroundScroller bgScroller;
    private IGameModeStrategy currentStrategy;
    public IGameModeStrategy CurrentStrategy => currentStrategy;

    [Header("References")]
    [SerializeField] private GameplayUIManager gameplayUIManager;
    public GameplayUIManager GameplayUIManager => gameplayUIManager;
    [SerializeField] private GameGrid gameGrid;
    [SerializeField] private Player player;
    public Player Player => player;
    [SerializeField] private List<Enemy> activeEnemies = new List<Enemy>();
    [SerializeField] private float multiplierPerGem = 0.25f;
    [SerializeField] private EnemySelectionManager enemySelectionManager;

    [Header("Balance Tuning")]
    [SerializeField] private BalanceConfigSO balanceConfig;
    [SerializeField] private float extraBlastGemFactor = 0.35f;
    [SerializeField] private float comboBonusPerMatch = 0.10f;
    [SerializeField] private float maxComboMultiplier = 1.45f;
    [SerializeField] private float shieldTurnDecayRate = 0.25f;
    [SerializeField] private float waveBuffDecayRate = 0.50f;
    [SerializeField] private float critOverflowToDamageBonus = 0.35f;
    [SerializeField] private float dodgeOverflowToShieldPercent = 0.05f;

    [Header("Tactical Combat & Pacing")]
    [SerializeField] private float overkillCleaveRatio = 0.50f;
    [SerializeField] private float supportStrikeDamageMultiplier = 0.50f;
    [SerializeField] private float enemyBasicAttackFallbackMultiplier = 0.65f;
    [SerializeField] private int baseSupportCooldown = 2;
    [SerializeField] private float exploreMinDuration = 1.5f;
    [SerializeField] private float exploreMaxDuration = 2.2f;
    [SerializeField, Range(0f, 1f)] private float exploreEventChance = 0.25f;
    [SerializeField] private float healingSpringPercent = 0.15f;
    [SerializeField] private float shrineShieldPercent = 0.20f;
    [SerializeField] private int baseChestGold = 12;

    private float currentBattleSpeed = 1f;
    public float CurrentBattleSpeed => currentBattleSpeed;
    public bool IsFastSpeed => currentBattleSpeed > 1.5f;

    private Enemy currentActingEnemy;
    private int enemyRotationCursor = 0;

    [Header("Enemy Spawner Settings")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private List<CharacterInfoSO> listEnemySO;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform characterParent;

    protected override void Awake()
    {
        base.Awake();
        if (maxActionPoints <= 0) maxActionPoints = 1;
        ApplyBalanceConfigIfAvailable();
        EnsureCharacterParent();
    }

    public void ApplyBalanceConfigIfAvailable()
    {
        if (balanceConfig == null)
        {
            balanceConfig = Resources.Load<BalanceConfigSO>("SO/BalanceConfig");
        }
        if (balanceConfig == null) return;

        multiplierPerGem = balanceConfig.multiplierPerGem;
        extraBlastGemFactor = balanceConfig.extraBlastGemFactor;
        comboBonusPerMatch = balanceConfig.comboBonusPerMatch;
        maxComboMultiplier = balanceConfig.maxComboMultiplier;
        shieldTurnDecayRate = balanceConfig.shieldTurnDecayRate;
        waveBuffDecayRate = balanceConfig.waveBuffDecayRate;
        critOverflowToDamageBonus = balanceConfig.critOverflowToDamageBonus;
        dodgeOverflowToShieldPercent = balanceConfig.dodgeOverflowToShieldPercent;
        overkillCleaveRatio = balanceConfig.overkillCleaveRatio;
        supportStrikeDamageMultiplier = balanceConfig.supportStrikeDamageMultiplier;
        enemyBasicAttackFallbackMultiplier = balanceConfig.enemyBasicAttackFallbackMultiplier;
        baseSupportCooldown = balanceConfig.baseSupportCooldown;
    }

    private void EnsureCharacterParent()
    {
        if (characterParent == null)
        {
            GameObject container = GameObject.Find("EnemiesContainer");
            if (container == null) container = new GameObject("EnemiesContainer");
            characterParent = container.transform;
        }
    }

    private void OnEnable()
    {
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnGemsMatched, HandleGemsMatched);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnPause, HandlePause);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnResume, HandleResume);
    }

    private void OnDisable()
    {
        ObserverManager<EventID>.RemoveAddListener(EventID.OnGemsMatched, HandleGemsMatched);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnPause, HandlePause);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnResume, HandleResume);
    }

    public void InitBattle()
    {
        CleanupBattle();
        
        if (bgScroller != null)
        {
            bgScroller.Init();
        }

        if (gameplayUIManager != null)
        {
            gameplayUIManager.Init();
        }
        else
        {
            gameplayUIManager = FindObjectOfType<GameplayUIManager>();
            if (gameplayUIManager != null) gameplayUIManager.Init();
        }

        if (gameGrid != null)
        {
            gameGrid.Init();
        }
        else
        {
            gameGrid = FindObjectOfType<GameGrid>();
            if (gameGrid != null) gameGrid.Init();
        }

        if (player == null)
        {
            player = FindObjectOfType<Player>(true);
            if (player == null)
            {
                Debug.LogError("[BattleManager] Không tìm thấy Player trong scene!");
                return;
            }
        }

        if (!player.gameObject.activeSelf)
        {
            player.gameObject.SetActive(true);
        }

        player.InitStat();

        Debug.Log("Initializing BattleManager with GameMode: " + (GameModeManager.Instance != null ? GameModeManager.Instance.CurrentMode.ToString() : "None"));

        if (GameModeManager.Instance != null)
        {
            if (GameModeManager.Instance.CurrentMode == GameModeType.Level)
            {
                Debug.Log("Selected Game Mode: Level");
                currentStrategy = new LevelModeStrategy();
            }
            else
            {
                Debug.Log("Selected Game Mode: Endless");
                currentStrategy = new EndlessModeStrategy();
            }
        }
        else
        {
            currentStrategy = new LevelModeStrategy();
        }

        Debug.Log("Initialized Strategy: " + currentStrategy.GetType().Name);
        currentState = GameState.Running;
        ApplyCurrentTimeScale();
        gameplayUIManager?.UpdateSpeedButtonUI(currentBattleSpeed);
        currentStrategy.Initialize(this);
        StartCoroutine(currentStrategy.OnWaveCleared(this));
    }

    public void ToggleBattleSpeed()
    {
        currentBattleSpeed = IsFastSpeed ? 1f : 2f;
        ApplyCurrentTimeScale();
        gameplayUIManager?.UpdateSpeedButtonUI(currentBattleSpeed);
    }

    public void ApplyCurrentTimeScale()
    {
        Time.timeScale = (currentState == GameState.Paused) ? 0f : currentBattleSpeed;
    }

    public void ResetTimeScaleToNormal()
    {
        Time.timeScale = 1f;
    }

    public IEnumerator ExploreRoutine()
    {
        currentState = GameState.Running;

        if (hasCompletedFirstWave && player != null && !player.IsDead())
        {
            player.DecayWaveBuffs(waveBuffDecayRate);
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlayMusicExplore();

        player.SetRunningAnimation(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPlayerFootstep(true);

        if (bgScroller != null) bgScroller.StartScrolling();

        float minWait = Mathf.Max(0.5f, exploreMinDuration);
        float maxWait = Mathf.Max(minWait, exploreMaxDuration);
        float waitTime = Random.Range(minWait, maxWait);

        if (hasCompletedFirstWave && player != null && !player.IsDead() && Random.value < exploreEventChance)
        {
            yield return new WaitForSeconds(waitTime * 0.45f);
            TriggerMidWaveExploreEvent();
            yield return new WaitForSeconds(waitTime * 0.55f);
        }
        else
        {
            yield return new WaitForSeconds(waitTime);
        }

        player.SetRunningAnimation(false);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPlayerFootstep(false);

        if (bgScroller != null) bgScroller.StopScrolling();

        hasCompletedFirstWave = true;
        isFirstTurnOfWave = true;
        isFirstEnemyTurnOfWave = true;

        SpawnEnemies();
        StartPlayerTurn();
    }

    private void TriggerMidWaveExploreEvent()
    {
        if (player == null || player.IsDead()) return;

        float hpRatio = player.CurrentHealth / Mathf.Max(1f, player.CurrentMaxHealth);
        bool rollHealSpring = hpRatio < 0.99f && (hpRatio < 0.70f ? Random.value < 0.65f : Random.value < 0.45f);

        if (rollHealSpring)
        {
            float healAmount = Mathf.Round(player.CurrentMaxHealth * healingSpringPercent * 10f) / 10f;
            player.Heal(healAmount);
            AudioManager.Instance?.PlayTargetSelected();
            gameplayUIManager?.ShowNotificationBanner(
                $"Healing Spring  +{Mathf.RoundToInt(healAmount)} HP",
                new Color(0.15f, 0.64f, 0.36f, 0.94f));
            return;
        }

        if (Random.value < 0.5f)
        {
            float shieldAmount = Mathf.Round(player.CurrentMaxHealth * shrineShieldPercent * 10f) / 10f;
            player.AddShield(shieldAmount);
            AudioManager.Instance?.PlayTargetSelected();
            gameplayUIManager?.ShowNotificationBanner(
                $"Ancient Shrine  +{Mathf.RoundToInt(shieldAmount)} Shield",
                new Color(0.14f, 0.52f, 0.82f, 0.94f));
        }
        else
        {
            float scale = currentStrategy != null ? currentStrategy.GetDifficultyMultiplier() : 1f;
            int bonusGold = Mathf.Max(5, Mathf.RoundToInt(baseChestGold * scale));
            if (DataManager.Instance != null && DataManager.Instance.GameData != null)
            {
                DataManager.Instance.GameData.AddResources(bonusGold, 0, 0);
                DataManager.Instance.GameData.Save();
            }
            AudioManager.Instance?.PlayWaveClear();
            gameplayUIManager?.ShowNotificationBanner(
                $"Treasure Chest  +{bonusGold} Gold",
                new Color(0.84f, 0.58f, 0.10f, 0.94f));
        }
    }

    private void SpawnEnemies()
    {
        bool isBoss = currentStrategy != null && currentStrategy.IsBossWave();

        if (isBoss)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusicBattleBoss();
        }
        else
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayMusicBattleNormal();
        }

        List<CharacterInfoSO> enemiesToSpawn = currentStrategy.GetEnemiesToSpawn(listEnemySO);

        if (enemiesToSpawn == null || enemiesToSpawn.Count == 0)
        {
            Debug.LogWarning("No enemies to spawn from strategy.");
            return;
        }

        float hpMultiplier = currentStrategy.GetDifficultyMultiplier();
        float dmgMultiplier = currentStrategy.GetDamageDifficultyMultiplier();
        float shieldMultiplier = currentStrategy.GetShieldDifficultyMultiplier();
        Debug.Log($"[BattleManager] Spawning {enemiesToSpawn.Count} enemies (HP x{hpMultiplier:F2}, DMG x{dmgMultiplier:F2}, Shield x{shieldMultiplier:F2})");

        EnsureCharacterParent();

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[BattleManager] spawnPoints chưa được gán!");
            return;
        }

        int spawnCount = Mathf.Min(enemiesToSpawn.Count, spawnPoints.Length);
        Vector3[] alignedPositions = BuildAlignedEnemyPositions(spawnCount);

        for (int i = 0; i < spawnCount; i++)
        {
            Enemy newEnemy = PoolingManager.Spawn(enemyPrefab, alignedPositions[i], Quaternion.identity, characterParent);
            newEnemy.InitStat(enemiesToSpawn[i]);
            newEnemy.ApplyDifficultyMultiplier(hpMultiplier, dmgMultiplier, shieldMultiplier);
            activeEnemies.Add(newEnemy);
        }

        InitializeWaveEnemyIntents();

        if (AudioManager.Instance != null) AudioManager.Instance.PlayRoundStart();
    }

    /// <summary>
    /// Chuẩn hóa đội hình 1-3 quái nằm trên cùng một đường ngang (bằng trục Y của Player) và cách đều nhau.
    /// </summary>
    private Vector3[] BuildAlignedEnemyPositions(int count)
    {
        float groundY = (player != null) ? player.InitialSpawnY : 2.15f;
        float minX = float.MaxValue;
        float maxX = float.MinValue;

        if (spawnPoints != null)
        {
            foreach (Transform sp in spawnPoints)
            {
                if (sp == null) continue;
                if (sp.position.x < minX) minX = sp.position.x;
                if (sp.position.x > maxX) maxX = sp.position.x;
            }
        }

        if (minX == float.MaxValue || maxX == float.MinValue || Mathf.Approximately(minX, maxX))
        {
            minX = 0.75f;
            maxX = 2.65f;
        }

        float centerX = (minX + maxX) * 0.5f;
        float span = maxX - minX;
        Vector3[] positions = new Vector3[count];

        if (count == 1)
        {
            positions[0] = new Vector3(centerX, groundY, 0f);
        }
        else if (count == 2)
        {
            float halfGap = Mathf.Clamp(span * 0.28f, 0.48f, 0.60f);
            positions[0] = new Vector3(centerX - halfGap, groundY, 0f);
            positions[1] = new Vector3(centerX + halfGap, groundY, 0f);
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                float t = (float)i / (count - 1);
                positions[i] = new Vector3(Mathf.Lerp(minX, maxX, t), groundY, 0f);
            }
        }

        return positions;
    }

    private void InitializeWaveEnemyIntents()
    {
        enemyRotationCursor = 0;
        currentActingEnemy = null;
        int safeBaseCooldown = Mathf.Max(2, baseSupportCooldown);

        for (int i = 0; i < activeEnemies.Count; i++)
        {
            Enemy enemy = activeEnemies[i];
            if (enemy == null) continue;

            bool isLeader = (i == 0);
            int initialCooldown = isLeader ? safeBaseCooldown : (safeBaseCooldown + (i - 1));
            enemy.InitializeIntent(initialCooldown, safeBaseCooldown, isLeader);
            if (isLeader) currentActingEnemy = enemy;
        }
    }

    private void RefreshActingEnemyIntent(bool advanceRotation)
    {
        activeEnemies.RemoveAll(e => e == null || e.IsDead());
        if (activeEnemies.Count == 0)
        {
            currentActingEnemy = null;
            return;
        }

        if (advanceRotation)
        {
            if (currentActingEnemy != null && activeEnemies.Contains(currentActingEnemy))
            {
                int currentIdx = activeEnemies.IndexOf(currentActingEnemy);
                enemyRotationCursor = (currentIdx + 1) % activeEnemies.Count;
            }
            else
            {
                enemyRotationCursor = enemyRotationCursor % activeEnemies.Count;
            }
            currentActingEnemy = activeEnemies[enemyRotationCursor];
        }
        else if (currentActingEnemy == null || currentActingEnemy.IsDead() || !activeEnemies.Contains(currentActingEnemy))
        {
            enemyRotationCursor = enemyRotationCursor % activeEnemies.Count;
            currentActingEnemy = activeEnemies[enemyRotationCursor];
        }

        foreach (Enemy enemy in activeEnemies)
        {
            enemy.SetActingNext(enemy == currentActingEnemy);
        }
    }

    private int currentTurnMatches = 0;
    private int turnDamageGroupCount = 0;
    private int maxDamageMatchCount = 0;
    private int maxAnyMatchCount = 0;
    private float turnDamage = 0f;
    private float turnHealFlat = 0f;
    private float turnHealPercent = 0f;
    private float turnShieldFlat = 0f;
    private float turnShieldPercent = 0f;
    private float turnCritRate = 0f;
    private float turnCritDamage = 0f;
    private float turnDodge = 0f;
    private bool isFirstTurnOfWave = true;
    private bool isFirstEnemyTurnOfWave = true;
    private bool hasCompletedFirstWave = false;

    private void StartPlayerTurn()
    {
        if (!isFirstTurnOfWave && player != null && !player.IsDead())
        {
            player.DecayShield(shieldTurnDecayRate);
            player.TickTurnBuffs();
        }
        isFirstTurnOfWave = false;

        currentState = GameState.PlayerTurn;
        currentActionPoints = maxActionPoints;
        ResetTurnAccumulators();
        ObserverManager<EventID>.PostEvent(EventID.OnPlayerTurnStart);
        gameplayUIManager?.UpdateTurnCount(currentActionPoints);
    }

    private void ResetTurnAccumulators()
    {
        currentTurnMatches = 0;
        turnDamageGroupCount = 0;
        maxDamageMatchCount = 0;
        maxAnyMatchCount = 0;
        turnDamage = 0f;
        turnHealFlat = 0f;
        turnHealPercent = 0f;
        turnShieldFlat = 0f;
        turnShieldPercent = 0f;
        turnCritRate = 0f;
        turnCritDamage = 0f;
        turnDodge = 0f;
    }

    private static float GetCascadeFalloff(int cascadeStep)
    {
        if (cascadeStep <= 1) return 1.00f;
        if (cascadeStep == 2) return 0.65f;
        if (cascadeStep == 3) return 0.45f;
        return 0.30f;
    }

    private void HandleGemsMatched(object param)
    {
        if (player == null || player.IsDead()) return;

        if (param is MatchEventData data)
        {
            float cascadeFalloff = GetCascadeFalloff(data.CascadeStep);
            float multiplier;
            if (data.IsExtraBlast)
            {
                multiplier = extraBlastGemFactor * cascadeFalloff;
            }
            else
            {
                multiplier = (1f + Mathf.Max(0, data.MatchCount - 3) * multiplierPerGem) * cascadeFalloff;
                currentTurnMatches++;
                if (data.MatchCount > maxAnyMatchCount)
                {
                    maxAnyMatchCount = data.MatchCount;
                }
                if (data.GemType == GemType.Damage)
                {
                    turnDamageGroupCount++;
                    if (data.MatchCount > maxDamageMatchCount)
                    {
                        maxDamageMatchCount = data.MatchCount;
                    }
                }
            }

            float totalPower = data.PowerValue * multiplier;
            float totalPercent = data.MaxHealthPercent * multiplier;

            switch (data.GemType)
            {
                case GemType.Damage: turnDamage += totalPower; break;
                case GemType.Health:
                    turnHealFlat += totalPower;
                    turnHealPercent += totalPercent;
                    break;
                case GemType.Shield:
                    turnShieldFlat += totalPower;
                    turnShieldPercent += totalPercent;
                    break;
                case GemType.CritRate: turnCritRate += totalPower; break;
                case GemType.CritDamage: turnCritDamage += totalPower; break;
                case GemType.Dodge: turnDodge += totalPower; break;
            }
        }
    }

    private void ApplyTurnSupportAndOverflow(BaseCharacter actor, float comboMultiplier)
    {
        if (turnHealFlat > 0f || turnHealPercent > 0f)
        {
            float healAmount = (turnHealFlat + actor.CurrentMaxHealth * turnHealPercent) * comboMultiplier;
            actor.Heal(healAmount);
        }

        if (turnShieldFlat > 0f || turnShieldPercent > 0f)
        {
            float shieldAmount = (turnShieldFlat + actor.CurrentMaxHealth * turnShieldPercent) * comboMultiplier;
            actor.AddShield(shieldAmount);
        }

        // Chuyển hóa phần chỉ số vượt trần để không có ngọc vô dụng trên bàn cờ
        if (turnCritRate > 0f)
        {
            float overflow = actor.AddCritRate(turnCritRate);
            if (overflow > 0f)
            {
                turnDamage += (overflow / 5f) * critOverflowToDamageBonus;
            }
        }

        if (turnCritDamage > 0f)
        {
            float overflow = actor.AddCritDamage(turnCritDamage);
            if (overflow > 0f)
            {
                turnDamage += (overflow / 10f) * critOverflowToDamageBonus;
            }
        }

        if (turnDodge > 0f)
        {
            float overflow = actor.AddDodge(turnDodge);
            if (overflow > 0f)
            {
                float bonusShield = (overflow / 3f) * actor.CurrentMaxHealth * dodgeOverflowToShieldPercent;
                actor.AddShield(bonusShield);
            }
        }
    }

    public void OnGridTurnCompleted(bool isPlayer)
    {
        StartCoroutine(TurnCompletedRoutine(isPlayer));
    }

    private IEnumerator TurnCompletedRoutine(bool isPlayer)
    {
        // 1. Execute aggregated turn actions once!
        if (currentTurnMatches > 0)
        {
            float comboMultiplier = Mathf.Min(maxComboMultiplier, 1f + Mathf.Max(0, currentTurnMatches - 1) * comboBonusPerMatch);

            if (isPlayer)
            {
                float gemMasteryMult = player != null ? player.GemPowerMultiplier : 1f;
                ApplyTurnSupportAndOverflow(player, comboMultiplier * gemMasteryMult);

                // Nội tại trang bị: Match-4+ hồi máu phụ (ví dụ Phoenix Ring)
                if (maxAnyMatchCount >= 4 && player != null && player.Match4BonusHeal > 0f)
                {
                    player.Heal(player.Match4BonusHeal);
                }

                // Player single combined attack
                if (turnDamage > 0f)
                {
                    currentState = GameState.SelectingTarget;
                    Enemy selectedTarget = null;

                    if (enemySelectionManager != null)
                    {
                        yield return StartCoroutine(
                            enemySelectionManager.SelectTarget(activeEnemies, (t) => selectedTarget = t)
                        );
                    }
                    else
                    {
                        selectedTarget = activeEnemies.Find(e => !e.IsDead());
                    }

                    currentState = GameState.Matching;

                    if (selectedTarget != null)
                    {
                        gameplayUIManager?.ShowEnemyInfo(selectedTarget);
                        yield return StartCoroutine(player.PerformAttackSequence(selectedTarget, turnDamage * comboMultiplier * gemMasteryMult));

                        // Sát thương lan (Overkill Cleave) khi ghép >= 2 cụm Damage hoặc Match-4+ Damage
                        bool allowOverkillCleave = turnDamageGroupCount >= 2 || maxDamageMatchCount >= 4;
                        if (allowOverkillCleave && selectedTarget.IsDead() && selectedTarget.LastOverkillDamage >= 1f)
                        {
                            float cleaveDamage = Mathf.Round(selectedTarget.LastOverkillDamage * overkillCleaveRatio * 10f) / 10f;
                            Enemy previousTarget = selectedTarget;
                            int cleaveHops = 0;

                            while (cleaveDamage >= 1f && cleaveHops < 2)
                            {
                                Enemy cleaveTarget = activeEnemies.Find(e => e != null && e != previousTarget && !e.IsDead());
                                if (cleaveTarget == null) break;

                                cleaveHops++;
                                yield return new WaitForSeconds(0.15f);
                                AudioManager.Instance?.PlayAttackHit();
                                cleaveTarget.TakeDamage(cleaveDamage, false);

                                if (cleaveTarget.IsDead() && cleaveTarget.LastOverkillDamage >= 1f)
                                {
                                    previousTarget = cleaveTarget;
                                    cleaveDamage = Mathf.Round(cleaveTarget.LastOverkillDamage * overkillCleaveRatio * 10f) / 10f;
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }

                        yield return new WaitForSeconds(0.3f);
                        selectedTarget.HideTargetIndicator();
                        gameplayUIManager?.HideEnemyInfo();
                    }
                }
            }
            else
            {
                // Enemy turn actions (Quái chủ công theo vòng xoay)
                Enemy actingEnemy = (currentActingEnemy != null && !currentActingEnemy.IsDead())
                    ? currentActingEnemy
                    : activeEnemies.Find(e => e != null && !e.IsDead());

                if (actingEnemy != null && player != null && !player.IsDead())
                {
                    ApplyTurnSupportAndOverflow(actingEnemy, comboMultiplier);

                    if (turnDamage > 0f)
                    {
                        yield return StartCoroutine(actingEnemy.PerformAttackSequence(player, turnDamage * comboMultiplier));
                        yield return new WaitForSeconds(0.3f);
                    }
                    else if (enemyBasicAttackFallbackMultiplier > 0f)
                    {
                        // Dù trên bàn không có ngọc Damage, quái chủ công vẫn tung 1 đòn đánh thường nhẹ để giữ áp lực
                        yield return StartCoroutine(actingEnemy.PerformAttackSequence(player, enemyBasicAttackFallbackMultiplier));
                        yield return new WaitForSeconds(0.25f);
                    }

                    actingEnemy.ResetSupportCooldown();

                    // Quái phụ giảm hồi chiêu và tung đòn đánh phụ (50% BaseDamage) khi sẵn sàng
                    List<Enemy> supporters = activeEnemies.FindAll(e => e != null && !e.IsDead() && e != actingEnemy);
                    foreach (Enemy supporter in supporters)
                    {
                        if (player == null || player.IsDead()) break;

                        if (supporter.TickSupportCooldown())
                        {
                            yield return StartCoroutine(supporter.PerformAttackSequence(player, supportStrikeDamageMultiplier));
                            yield return new WaitForSeconds(0.2f);
                        }
                    }
                }
            }
        }

        ResetTurnAccumulators();

        activeEnemies.RemoveAll(e => e == null || e.IsDead());

        if (activeEnemies.Count == 0)
        {
            currentActingEnemy = null;
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWaveClear();
            StartCoroutine(currentStrategy.OnWaveCleared(this));
            yield break;
        }

        if (isPlayer)
        {
            RefreshActingEnemyIntent(false);
            currentActionPoints--;
            gameplayUIManager?.UpdateTurnCount(currentActionPoints);

            if (currentActionPoints <= 0 && !player.IsDead())
            {
                StartCoroutine(EnemyTurnRoutine());
            }
            else if (!player.IsDead())
            {
                currentState = GameState.PlayerTurn;
            }
        }
        else
        {
            // Enemy turn completed -> xoay vòng Quái chủ công sang mục tiêu kế tiếp
            if (!currentStrategy.IsGameOver(player))
            {
                RefreshActingEnemyIntent(true);
                StartPlayerTurn();
            }
            else
            {
                currentState = GameState.Finished;
                gameplayUIManager?.ShowGameOver(player);
            }
        }
    }

    private IEnumerator EnemyTurnRoutine()
    {
        currentState = GameState.EnemyTurn;
        yield return new WaitForSeconds(0.6f);

        ObserverManager<EventID>.PostEvent(EventID.OnEnemyTurnStart);

        activeEnemies.RemoveAll(e => e == null || e.IsDead());
        if (activeEnemies.Count == 0 || player == null || player.IsDead()) yield break;

        if (!isFirstEnemyTurnOfWave)
        {
            foreach (var enemy in activeEnemies)
            {
                if (enemy != null && !enemy.IsDead())
                {
                    enemy.DecayShield(shieldTurnDecayRate);
                    enemy.TickTurnBuffs();
                }
            }
        }
        isFirstEnemyTurnOfWave = false;

        RefreshActingEnemyIntent(false);
        Enemy actingEnemy = currentActingEnemy != null ? currentActingEnemy : activeEnemies[0];

        // 1. AI selects best move from board
        var possibleMoves = (gameGrid != null) ? gameGrid.FindAllPossibleMoves() : null;

        if (possibleMoves != null && possibleMoves.Count > 0)
        {
            GameGrid.ValidMove bestMove = SelectBestEnemyMove(actingEnemy, possibleMoves);

            Debug.Log($"[Enemy AI] {actingEnemy.name} picked swap ({bestMove.from.x},{bestMove.from.y}) -> ({bestMove.to.x},{bestMove.to.y}) [Type: {bestMove.gemType}, Count: {bestMove.matchCount}]");

            // Execute the swap on the board (isPlayer = false)
            gameGrid.TrySwap(bestMove.from, bestMove.to, false);
            // GameGrid will call OnGridTurnCompleted(false) when done
        }
        else
        {
            // Fallback: If no moves on board, quái performs standard attack
            yield return StartCoroutine(actingEnemy.PerformAttackSequence(player, 1f));
            yield return new WaitForSeconds(0.5f);

            OnGridTurnCompleted(false);
        }
    }

    private GameGrid.ValidMove SelectBestEnemyMove(Enemy enemy, List<GameGrid.ValidMove> moves)
    {
        // 1. If enemy HP < 40%, prioritize Health or Shield
        float hpPercent = (enemy.CurrentHealth / enemy.CurrentMaxHealth) * 100f;
        if (hpPercent < 40f)
        {
            var healMove = moves.Find(m => m.gemType == GemType.Health);
            if (healMove.matchCount >= 3) return healMove;

            var shieldMove = moves.Find(m => m.gemType == GemType.Shield);
            if (shieldMove.matchCount >= 3) return shieldMove;
        }

        // 2. Prioritize Damage moves to strike Player
        var damageMoves = moves.FindAll(m => m.gemType == GemType.Damage);
        if (damageMoves.Count > 0)
        {
            damageMoves.Sort((a, b) => b.matchCount.CompareTo(a.matchCount));
            return damageMoves[0];
        }

        // 3. Otherwise pick the move with the highest match count
        moves.Sort((a, b) => b.matchCount.CompareTo(a.matchCount));
        return moves[0];
    }

    public void SetGameState(GameState state)
    {
        currentState = state;
    }

    private void HandlePause(object param)
    {
        if (currentState != GameState.Paused)
        {
            previousState = currentState;
            currentState = GameState.Paused;
        }
    }

    private void HandleResume(object param)
    {
        if (currentState == GameState.Paused)
        {
            currentState = previousState;
        }
    }

    public void CleanupBattle()
    {
        StopAllCoroutines();
        ResetTurnAccumulators();
        currentState = GameState.Running;
        previousState = GameState.Running;
        currentActingEnemy = null;
        enemyRotationCursor = 0;
        hasCompletedFirstWave = false;
        isFirstTurnOfWave = true;
        isFirstEnemyTurnOfWave = true;

        if (enemySelectionManager != null)
        {
            enemySelectionManager.StopAllCoroutines();
        }

        if (bgScroller != null)
        {
            bgScroller.StopScrolling();
        }

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayPlayerFootstep(false);
        }

        EnsureCharacterParent();

        // 1. Despawn active enemies list
        foreach (var enemy in activeEnemies)
        {
            if (enemy != null && enemy.gameObject != null)
            {
                enemy.transform.DOKill();
                enemy.StopAllCoroutines();
                enemy.HideTargetIndicator();
                if (enemy.gameObject.activeInHierarchy)
                {
                    PoolingManager.Despawn(enemy.gameObject);
                }
            }
        }
        activeEnemies.Clear();

        // 2. Despawn all children of characterParent
        if (characterParent != null)
        {
            List<GameObject> toDespawn = new List<GameObject>();
            foreach (Transform child in characterParent)
            {
                if (child != null && child.gameObject.activeInHierarchy)
                {
                    child.DOKill();
                    toDespawn.Add(child.gameObject);
                }
            }
            foreach (var go in toDespawn)
            {
                PoolingManager.Despawn(go);
            }
        }

        // 3. Fallback: Despawn any other Enemy instances found in scene
        Enemy[] lingeringEnemies = FindObjectsOfType<Enemy>(true);
        foreach (var enemy in lingeringEnemies)
        {
            if (enemy != null && enemy.gameObject.activeInHierarchy)
            {
                enemy.transform.DOKill();
                enemy.StopAllCoroutines();
                enemy.HideTargetIndicator();
                PoolingManager.Despawn(enemy.gameObject);
            }
        }

        if (gameGrid != null)
        {
            gameGrid.ClearGrid();
        }

        if (player != null)
        {
            player.StopAllCoroutines();
            player.transform.DOKill();
            player.InitStat();
        }

        if (gameplayUIManager != null)
        {
            gameplayUIManager.CleanupUI();
        }
    }
}