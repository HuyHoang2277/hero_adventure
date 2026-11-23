using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using DG.Tweening;

public class GameGrid : MonoBehaviour
{
    [SerializeField] private int gridSize = 7;
    public int GridSize => gridSize;

    [SerializeField] private int matchThreshold = 3;
    public int MatchThreshold => matchThreshold;

    [SerializeField] private int maxCascades = 15;

    [SerializeField] private float spacing = 0.75f;
    [SerializeField] private float startY = 0f;

    [SerializeField] private GemSpawner gemSpawner;

    public Gem[,] gridGems { get; private set; }
    private float startX;

    private bool isProcessing = false;
    public bool IsProcessing => isProcessing;

    public struct ValidMove
    {
        public Vector2Int from;
        public Vector2Int to;
        public GemType gemType;
        public int matchCount;
    }

    public void Init()
    {
        if (matchThreshold < 3) matchThreshold = 3;
        if (maxCascades < 5) maxCascades = 15;

        ClearGrid();
        startX = -(gridSize - 1) * spacing / 2f;
        CreateGrid();
    }

    public void ClearGrid()
    {
        isProcessing = false;
        StopAllCoroutines();

        if (gridGems != null)
        {
            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    if (gridGems[x, y] != null)
                    {
                        gridGems[x, y].transform.DOKill();
                        gemSpawner.DespawnGem(gridGems[x, y]);
                        gridGems[x, y] = null;
                    }
                }
            }
        }
    }

    private void CreateGrid()
    {
        gridGems = new Gem[gridSize, gridSize];

        GemData[,] chosenData = GenerateValidBoardData();

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                SpawnGemAtPosition(x, y, chosenData[x, y], true);
            }
        }
    }

    private GemData[,] GenerateValidBoardData()
    {
        int attempts = 0;
        while (attempts < 200)
        {
            attempts++;
            GemData[,] candidate = new GemData[gridSize, gridSize];

            for (int x = 0; x < gridSize; x++)
            {
                for (int y = 0; y < gridSize; y++)
                {
                    List<GemType> excluded = new List<GemType>();

                    if (x >= 2 && candidate[x - 1, y] != null && candidate[x - 2, y] != null
                        && candidate[x - 1, y].gemType == candidate[x - 2, y].gemType)
                    {
                        excluded.Add(candidate[x - 1, y].gemType);
                    }

                    if (y >= 2 && candidate[x, y - 1] != null && candidate[x, y - 2] != null
                        && candidate[x, y - 1].gemType == candidate[x, y - 2].gemType)
                    {
                        excluded.Add(candidate[x, y - 1].gemType);
                    }

                    candidate[x, y] = gemSpawner.GetRandomGemDataExcept(excluded);
                }
            }

            if (CountMatchesInData(candidate) == 0 && HasValidMovesInData(candidate))
            {
                return candidate;
            }
        }

        return GenerateFallbackBoard();
    }

    private int CountMatchesInData(GemData[,] data)
    {
        // Horizontal matches of 3 or more
        for (int y = 0; y < gridSize; y++)
        {
            int len = 1;
            for (int x = 0; x < gridSize; x++)
            {
                bool isLast = (x == gridSize - 1);
                bool same = !isLast && data[x, y] != null && data[x + 1, y] != null
                            && data[x, y].gemType == data[x + 1, y].gemType;
                if (same)
                {
                    len++;
                }
                else
                {
                    if (len >= 3) return len;
                    len = 1;
                }
            }
        }

        // Vertical matches of 3 or more
        for (int x = 0; x < gridSize; x++)
        {
            int len = 1;
            for (int y = 0; y < gridSize; y++)
            {
                bool isLast = (y == gridSize - 1);
                bool same = !isLast && data[x, y] != null && data[x, y + 1] != null
                            && data[x, y].gemType == data[x, y + 1].gemType;
                if (same)
                {
                    len++;
                }
                else
                {
                    if (len >= 3) return len;
                    len = 1;
                }
            }
        }

        return 0;
    }

    private bool HasValidMovesInData(GemData[,] data)
    {
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (x + 1 < gridSize)
                {
                    SwapData(data, x, y, x + 1, y);
                    bool match = CountMatchesInData(data) > 0;
                    SwapData(data, x, y, x + 1, y);
                    if (match) return true;
                }

                if (y + 1 < gridSize)
                {
                    SwapData(data, x, y, x, y + 1);
                    bool match = CountMatchesInData(data) > 0;
                    SwapData(data, x, y, x, y + 1);
                    if (match) return true;
                }
            }
        }
        return false;
    }

    private void SwapData(GemData[,] data, int x1, int y1, int x2, int y2)
    {
        GemData temp = data[x1, y1];
        data[x1, y1] = data[x2, y2];
        data[x2, y2] = temp;
    }

    private GemData[,] GenerateFallbackBoard()
    {
        GemData[,] board = new GemData[gridSize, gridSize];
        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                List<GemType> excluded = new List<GemType>();
                if (x >= 2 && board[x - 1, y] != null && board[x - 2, y] != null
                    && board[x - 1, y].gemType == board[x - 2, y].gemType)
                    excluded.Add(board[x - 1, y].gemType);
                if (y >= 2 && board[x, y - 1] != null && board[x, y - 2] != null
                    && board[x, y - 1].gemType == board[x, y - 2].gemType)
                    excluded.Add(board[x, y - 1].gemType);

                board[x, y] = gemSpawner.GetRandomGemDataExcept(excluded);
            }
        }
        return board;
    }

    private void SpawnGemAtPosition(int x, int y, GemData data, bool animate = true)
    {
        Vector2 finalPosition = GetWorldPosition(x, y);
        Vector2 spawnPosition = animate ? GetWorldPosition(x, -4) : finalPosition;

        Gem newGem = gemSpawner.SpawnGem(spawnPosition, this.transform);

        if (newGem != null)
        {
            newGem.Init(data, new Vector2Int(x, y));
            gridGems[x, y] = newGem;

            if (animate)
            {
                newGem.transform.DOMove(finalPosition, 0.4f).SetEase(Ease.OutBounce);
            }
        }
    }

    public Vector2 GetWorldPosition(int x, int y)
    {
        float posX = startX + (x * spacing);
        float posY = startY - (y * spacing);
        return new Vector2(posX, posY);
    }

    public bool IsValidGridPos(Vector2Int pos)
    {
        return pos.x >= 0 && pos.x < gridSize && pos.y >= 0 && pos.y < gridSize;
    }

    public Gem GetGem(Vector2Int pos)
    {
        if (IsValidGridPos(pos)) return gridGems[pos.x, pos.y];
        return null;
    }

    #region SWAP LOGIC

    public bool TrySwap(Vector2Int from, Vector2Int to, bool isPlayer = true, Action<bool> onComplete = null)
    {
        if (isProcessing) return false;

        if (!IsValidGridPos(from) || !IsValidGridPos(to)) return false;

        // Must be adjacent (4-direction)
        int dist = Mathf.Abs(from.x - to.x) + Mathf.Abs(from.y - to.y);
        if (dist != 1) return false;

        StartCoroutine(SwapRoutine(from, to, isPlayer, onComplete));
        return true;
    }

    private IEnumerator SwapRoutine(Vector2Int posA, Vector2Int posB, bool isPlayer, Action<bool> onComplete)
    {
        isProcessing = true;

        Gem gemA = gridGems[posA.x, posA.y];
        Gem gemB = gridGems[posB.x, posB.y];

        if (gemA == null || gemB == null)
        {
            isProcessing = false;
            onComplete?.Invoke(false);
            yield break;
        }

        // Swap references in array
        gridGems[posA.x, posA.y] = gemB;
        gridGems[posB.x, posB.y] = gemA;
        gemA.SetGridPosition(posB);
        gemB.SetGridPosition(posA);

        // Animate swap
        Vector2 targetPosA = GetWorldPosition(posA.x, posA.y);
        Vector2 targetPosB = GetWorldPosition(posB.x, posB.y);

        gemA.transform.DOMove(targetPosB, 0.2f).SetEase(Ease.OutQuad);
        gemB.transform.DOMove(targetPosA, 0.2f).SetEase(Ease.OutQuad);

        yield return new WaitForSeconds(0.22f);

        List<List<Gem>> matches = FindAllMatches();

        if (matches.Count == 0)
        {
            // Swap back
            gridGems[posA.x, posA.y] = gemA;
            gridGems[posB.x, posB.y] = gemB;
            gemA.SetGridPosition(posA);
            gemB.SetGridPosition(posB);

            gemA.transform.DOMove(targetPosA, 0.2f).SetEase(Ease.OutQuad);
            gemB.transform.DOMove(targetPosB, 0.2f).SetEase(Ease.OutQuad);

            yield return new WaitForSeconds(0.22f);

            isProcessing = false;
            onComplete?.Invoke(false);
        }
        else
        {
            onComplete?.Invoke(true);
            yield return StartCoroutine(ProcessCascadeRoutine(isPlayer));
        }
    }

    #endregion

    #region MATCH & CASCADE

    public List<List<Gem>> FindAllMatches()
    {
        bool[,] matched = new bool[gridSize, gridSize];

        // Horizontal match search
        for (int y = 0; y < gridSize; y++)
        {
            int matchLen = 1;
            for (int x = 0; x < gridSize; x++)
            {
                bool isLast = (x == gridSize - 1);
                bool sameType = !isLast && gridGems[x, y] != null && gridGems[x + 1, y] != null
                                && gridGems[x, y].GetGemData() != null && gridGems[x + 1, y].GetGemData() != null
                                && gridGems[x, y].GetGemData().gemType == gridGems[x + 1, y].GetGemData().gemType;

                if (sameType)
                {
                    matchLen++;
                }
                else
                {
                    if (matchLen >= matchThreshold)
                    {
                        for (int k = 0; k < matchLen; k++)
                        {
                            matched[x - k, y] = true;
                        }
                    }
                    matchLen = 1;
                }
            }
        }

        // Vertical match search
        for (int x = 0; x < gridSize; x++)
        {
            int matchLen = 1;
            for (int y = 0; y < gridSize; y++)
            {
                bool isLast = (y == gridSize - 1);
                bool sameType = !isLast && gridGems[x, y] != null && gridGems[x, y + 1] != null
                                && gridGems[x, y].GetGemData() != null && gridGems[x, y + 1].GetGemData() != null
                                && gridGems[x, y].GetGemData().gemType == gridGems[x, y + 1].GetGemData().gemType;

                if (sameType)
                {
                    matchLen++;
                }
                else
                {
                    if (matchLen >= matchThreshold)
                    {
                        for (int k = 0; k < matchLen; k++)
                        {
                            matched[x, y - k] = true;
                        }
                    }
                    matchLen = 1;
                }
            }
        }

        // Group connected matched gems via BFS (handles L, T, + shapes)
        bool[,] visited = new bool[gridSize, gridSize];
        List<List<Gem>> matchGroups = new List<List<Gem>>();

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (matched[x, y] && !visited[x, y] && gridGems[x, y] != null)
                {
                    GemType type = gridGems[x, y].GetGemData().gemType;
                    List<Gem> group = new List<Gem>();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();

                    queue.Enqueue(new Vector2Int(x, y));
                    visited[x, y] = true;

                    while (queue.Count > 0)
                    {
                        Vector2Int curr = queue.Dequeue();
                        group.Add(gridGems[curr.x, curr.y]);

                        Vector2Int[] neighbors = {
                            new Vector2Int(curr.x + 1, curr.y),
                            new Vector2Int(curr.x - 1, curr.y),
                            new Vector2Int(curr.x, curr.y + 1),
                            new Vector2Int(curr.x, curr.y - 1)
                        };

                        foreach (var n in neighbors)
                        {
                            if (n.x >= 0 && n.x < gridSize && n.y >= 0 && n.y < gridSize)
                            {
                                if (matched[n.x, n.y] && !visited[n.x, n.y] && gridGems[n.x, n.y] != null
                                    && gridGems[n.x, n.y].GetGemData().gemType == type)
                                {
                                    visited[n.x, n.y] = true;
                                    queue.Enqueue(n);
                                }
                            }
                        }
                    }

                    matchGroups.Add(group);
                }
            }
        }

        return matchGroups;
    }

    private IEnumerator ProcessCascadeRoutine(bool isPlayer)
    {
        int cascadeCount = 0;

        while (cascadeCount < maxCascades)
        {
            List<List<Gem>> matches = FindAllMatches();
            if (matches.Count == 0) break;

            cascadeCount++;

            foreach (List<Gem> group in matches)
            {
                GemData gemData = group[0].GetGemData();
                ObserverManager<EventID>.PostEvent(EventID.OnGemsMatched, new MatchEventData
                {
                    GemType = gemData.gemType,
                    MatchCount = group.Count,
                    PowerValue = gemData.powerValue,
                    IsPlayerMatch = isPlayer
                });

                if (AudioManager.Instance != null)
                {
                    AudioManager.Instance.PlayGemMatch(group.Count);
                }
            }

            // Despawn matched gems
            HashSet<Gem> allMatchedGems = new HashSet<Gem>();
            foreach (var group in matches)
            {
                foreach (var gem in group)
                {
                    allMatchedGems.Add(gem);
                }
            }

            foreach (Gem gem in allMatchedGems)
            {
                gridGems[gem.gridPosition.x, gem.gridPosition.y] = null;
                gem.transform.DOKill();
                Gem captured = gem;
                captured.transform.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).OnComplete(() =>
                {
                    gemSpawner.DespawnGem(captured);
                });
            }

            yield return new WaitForSeconds(0.25f);

            // Drop existing gems and spawn new gems from top
            yield return StartCoroutine(DropAndFillRoutine());

            yield return new WaitForSeconds(0.35f);
        }

        // Clean up: If cascade limit reached but matches still exist, resolve them
        while (FindAllMatches().Count > 0)
        {
            List<List<Gem>> leftoverMatches = FindAllMatches();
            foreach (var group in leftoverMatches)
            {
                foreach (var gem in group)
                {
                    if (gem != null)
                    {
                        gridGems[gem.gridPosition.x, gem.gridPosition.y] = null;
                        gemSpawner.DespawnGem(gem);
                    }
                }
            }
            yield return StartCoroutine(DropAndFillRoutine());
            yield return new WaitForSeconds(0.2f);
        }

        if (!HasValidMoves())
        {
            yield return StartCoroutine(ShuffleRoutine());
        }

        isProcessing = false;
        BattleManager.Instance?.OnGridTurnCompleted(isPlayer);
    }

    private IEnumerator DropAndFillRoutine()
    {
        for (int x = 0; x < gridSize; x++)
        {
            int emptySpots = 0;

            for (int y = gridSize - 1; y >= 0; y--)
            {
                if (gridGems[x, y] == null)
                {
                    emptySpots++;
                }
                else if (emptySpots > 0)
                {
                    Gem gem = gridGems[x, y];
                    int targetY = y + emptySpots;

                    gridGems[x, targetY] = gem;
                    gridGems[x, y] = null;

                    gem.SetGridPosition(new Vector2Int(x, targetY));
                    gem.transform.DOMove(GetWorldPosition(x, targetY), 0.3f).SetEase(Ease.OutBounce);
                }
            }

            for (int y = 0; y < emptySpots; y++)
            {
                int targetY = emptySpots - 1 - y;
                Vector2 spawnPos = GetWorldPosition(x, -(y + 1));
                Vector2 finalPos = GetWorldPosition(x, targetY);

                List<GemType> excluded = new List<GemType>();

                // Avoid horizontal match-3 with left neighbors
                if (x >= 2 && gridGems[x - 1, targetY] != null && gridGems[x - 2, targetY] != null
                    && gridGems[x - 1, targetY].GetGemData() != null && gridGems[x - 2, targetY].GetGemData() != null
                    && gridGems[x - 1, targetY].GetGemData().gemType == gridGems[x - 2, targetY].GetGemData().gemType)
                {
                    excluded.Add(gridGems[x - 1, targetY].GetGemData().gemType);
                }

                // Avoid vertical match-3 with bottom neighbors
                if (targetY + 2 < gridSize && gridGems[x, targetY + 1] != null && gridGems[x, targetY + 2] != null
                    && gridGems[x, targetY + 1].GetGemData() != null && gridGems[x, targetY + 2].GetGemData() != null
                    && gridGems[x, targetY + 1].GetGemData().gemType == gridGems[x, targetY + 2].GetGemData().gemType)
                {
                    excluded.Add(gridGems[x, targetY + 1].GetGemData().gemType);
                }

                Gem newGem = gemSpawner.SpawnGem(spawnPos, this.transform);
                if (newGem != null)
                {
                    GemData data = (excluded.Count > 0)
                        ? gemSpawner.GetRandomGemDataExcept(excluded)
                        : gemSpawner.GetRandomGemData();

                    newGem.Init(data, new Vector2Int(x, targetY));
                    gridGems[x, targetY] = newGem;
                    newGem.transform.DOMove(finalPos, 0.35f).SetEase(Ease.OutBounce);
                }
            }
        }

        yield return null;
    }

    #endregion

    #region VALID MOVES & AI

    public List<ValidMove> FindAllPossibleMoves()
    {
        List<ValidMove> validMoves = new List<ValidMove>();

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                // Test swap right
                if (x + 1 < gridSize)
                {
                    TestMove(new Vector2Int(x, y), new Vector2Int(x + 1, y), validMoves);
                }

                // Test swap down
                if (y + 1 < gridSize)
                {
                    TestMove(new Vector2Int(x, y), new Vector2Int(x, y + 1), validMoves);
                }
            }
        }

        return validMoves;
    }

    private void TestMove(Vector2Int a, Vector2Int b, List<ValidMove> validMoves)
    {
        Gem gemA = gridGems[a.x, a.y];
        Gem gemB = gridGems[b.x, b.y];

        if (gemA == null || gemB == null) return;
        if (gemA.GetGemData().gemType == gemB.GetGemData().gemType) return;

        // Temporary swap
        gridGems[a.x, a.y] = gemB;
        gridGems[b.x, b.y] = gemA;

        List<List<Gem>> matches = FindAllMatches();

        if (matches.Count > 0)
        {
            foreach (var group in matches)
            {
                validMoves.Add(new ValidMove
                {
                    from = a,
                    to = b,
                    gemType = group[0].GetGemData().gemType,
                    matchCount = group.Count
                });
            }
        }

        // Revert swap
        gridGems[a.x, a.y] = gemA;
        gridGems[b.x, b.y] = gemB;
    }

    public bool HasValidMoves()
    {
        return FindAllPossibleMoves().Count > 0;
    }

    public IEnumerator ShuffleRoutine()
    {
        Debug.Log("[GameGrid] No valid moves detected! Shuffling board cleanly...");

        GemData[,] newData = GenerateValidBoardData();

        for (int x = 0; x < gridSize; x++)
        {
            for (int y = 0; y < gridSize; y++)
            {
                if (gridGems[x, y] != null)
                {
                    gridGems[x, y].Init(newData[x, y], new Vector2Int(x, y));
                    gridGems[x, y].transform.DOPunchScale(Vector3.one * 0.2f, 0.3f);
                }
            }
        }

        yield return new WaitForSeconds(0.35f);
    }

    #endregion
}
