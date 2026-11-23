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

    [Header("Enemy Spawner Settings")]
    [SerializeField] private Enemy enemyPrefab;
    [SerializeField] private List<CharacterInfoSO> listEnemySO;
    [SerializeField] private Transform[] spawnPoints;
    [SerializeField] private Transform characterParent;

    protected override void Awake()
    {
        base.Awake();
        if (maxActionPoints <= 0) maxActionPoints = 1;
        EnsureCharacterParent();
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
            player = FindObjectOfType<Player>();
            if (player == null) return;
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
        currentStrategy.Initialize(this);
        StartCoroutine(currentStrategy.OnWaveCleared(this));
    }

    public IEnumerator ExploreRoutine()
    {
        currentState = GameState.Running;

        if (AudioManager.Instance != null) AudioManager.Instance.PlayMusicExplore();

        player.SetRunningAnimation(true);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPlayerFootstep(true);

        if (bgScroller != null) bgScroller.StartScrolling();

        float waitTime = Random.Range(3f, 5f);
        yield return new WaitForSeconds(waitTime);

        player.SetRunningAnimation(false);
        if (AudioManager.Instance != null) AudioManager.Instance.PlayPlayerFootstep(false);

        if (bgScroller != null) bgScroller.StopScrolling();

        SpawnEnemies();
        StartPlayerTurn();
    }

    private void SpawnEnemies()
    {
        bool isBoss = GameModeManager.Instance != null &&
                      GameModeManager.Instance.CurrentMode == GameModeType.Level &&
                      GameModeManager.Instance.CurrentLevelConfig != null &&
                      GameModeManager.Instance.CurrentLevelConfig.IsBossLevel;

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

        float difficultyMultiplier = currentStrategy.GetDifficultyMultiplier();
        Debug.Log($"[BattleManager] Spawning {enemiesToSpawn.Count} enemies with difficulty multiplier: {difficultyMultiplier:F2}");

        EnsureCharacterParent();

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogError("[BattleManager] spawnPoints chưa được gán!");
            return;
        }

        if (enemiesToSpawn.Count == 1)
        {
            Enemy newEnemy = PoolingManager.Spawn(enemyPrefab, spawnPoints[spawnPoints.Length - 1].position, Quaternion.identity, characterParent);
            newEnemy.InitStat(enemiesToSpawn[0]);
            newEnemy.ApplyDifficultyMultiplier(difficultyMultiplier);
            activeEnemies.Add(newEnemy);
        }
        else
        {
            for (int i = 0; i < enemiesToSpawn.Count; i++)
            {
                if (i >= spawnPoints.Length) break;

                Enemy newEnemy = PoolingManager.Spawn(enemyPrefab, spawnPoints[i].position, Quaternion.identity, characterParent);
                newEnemy.InitStat(enemiesToSpawn[i]);
                newEnemy.ApplyDifficultyMultiplier(difficultyMultiplier);
                activeEnemies.Add(newEnemy);
            }
        }

        if (AudioManager.Instance != null) AudioManager.Instance.PlayRoundStart();
    }

    private int currentTurnMatches = 0;
    private float turnDamage = 0f;
    private float turnHeal = 0f;
    private float turnShield = 0f;
    private float turnCritRate = 0f;
    private float turnCritDamage = 0f;
    private float turnDodge = 0f;

    private void StartPlayerTurn()
    {
        currentState = GameState.PlayerTurn;
        currentActionPoints = maxActionPoints;
        ResetTurnAccumulators();
        ObserverManager<EventID>.PostEvent(EventID.OnPlayerTurnStart);
        gameplayUIManager?.UpdateTurnCount(currentActionPoints);
    }

    private void ResetTurnAccumulators()
    {
        currentTurnMatches = 0;
        turnDamage = 0f;
        turnHeal = 0f;
        turnShield = 0f;
        turnCritRate = 0f;
        turnCritDamage = 0f;
        turnDodge = 0f;
    }

    private void HandleGemsMatched(object param)
    {
        if (player == null || player.IsDead()) return;

        if (param is MatchEventData data)
        {
            float multiplier = 1f + (data.MatchCount - 3) * multiplierPerGem;
            float totalPower = data.PowerValue * multiplier;

            currentTurnMatches++;

            switch (data.GemType)
            {
                case GemType.Damage: turnDamage += totalPower; break;
                case GemType.Health: turnHeal += totalPower; break;
                case GemType.Shield: turnShield += totalPower; break;
                case GemType.CritRate: turnCritRate += totalPower; break;
                case GemType.CritDamage: turnCritDamage += totalPower; break;
                case GemType.Dodge: turnDodge += totalPower; break;
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
            float comboMultiplier = 1f + Mathf.Max(0, currentTurnMatches - 1) * 0.2f;

            if (isPlayer)
            {
                // Player buffs and healing
                if (turnHeal > 0) player.Heal(turnHeal * comboMultiplier);
                if (turnShield > 0) player.AddShield(turnShield * comboMultiplier);
                if (turnCritRate > 0) player.AddCritRate(turnCritRate);
                if (turnCritDamage > 0) player.AddCritDamage(turnCritDamage);
                if (turnDodge > 0) player.AddDodge(turnDodge);

                // Player single combined attack
                if (turnDamage > 0)
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
                        yield return StartCoroutine(player.PerformAttackSequence(selectedTarget, turnDamage * comboMultiplier));
                        yield return new WaitForSeconds(0.3f);
                        selectedTarget.HideTargetIndicator();
                        gameplayUIManager?.HideEnemyInfo();
                    }
                }
            }
            else
            {
                // Enemy turn actions
                Enemy actingEnemy = activeEnemies.Find(e => !e.IsDead());
                if (actingEnemy != null && player != null && !player.IsDead())
                {
                    if (turnHeal > 0) actingEnemy.Heal(turnHeal * comboMultiplier);
                    if (turnShield > 0) actingEnemy.AddShield(turnShield * comboMultiplier);
                    if (turnCritRate > 0) actingEnemy.AddCritRate(turnCritRate);
                    if (turnCritDamage > 0) actingEnemy.AddCritDamage(turnCritDamage);
                    if (turnDodge > 0) actingEnemy.AddDodge(turnDodge);

                    if (turnDamage > 0)
                    {
                        yield return StartCoroutine(actingEnemy.PerformAttackSequence(player, turnDamage * comboMultiplier));
                        yield return new WaitForSeconds(0.3f);
                    }
                }
            }
        }

        ResetTurnAccumulators();

        activeEnemies.RemoveAll(e => e.IsDead());

        if (activeEnemies.Count == 0)
        {
            if (AudioManager.Instance != null) AudioManager.Instance.PlayWaveClear();
            StartCoroutine(currentStrategy.OnWaveCleared(this));
            yield break;
        }

        if (isPlayer)
        {
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
            // Enemy turn completed
            if (!currentStrategy.IsGameOver(player))
            {
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

        activeEnemies.RemoveAll(e => e.IsDead());
        if (activeEnemies.Count == 0 || player == null || player.IsDead()) yield break;

        Enemy actingEnemy = activeEnemies[0];

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
            gameplayUIManager.HideEnemyInfo();
        }
    }
}