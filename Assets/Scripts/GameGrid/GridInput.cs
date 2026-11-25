using UnityEngine;

public class GridInput : MonoBehaviour
{
    [SerializeField] private LineRendererManager lineManager; // Kept for inspector compatibility
    [SerializeField] private GameGrid gameGrid;
    [SerializeField] private LayerMask gemLayer;
    [SerializeField] private float swipeThreshold = 0.35f;
    [SerializeField] private float idleHintDelay = 4.5f;

    private Gem selectedGem;
    private Vector3 dragStartPos;
    private bool isDragging = false;

    private float idleTimer = 0f;
    private Gem hintGemA;
    private Gem hintGemB;

    private void Update()
    {
        HandleInput();
    }

    private void OnDisable()
    {
        ClearIdleHint();
    }

    private void HandleInput()
    {
        // Don't accept input if not player turn or if grid is animating/processing
        if (BattleManager.Instance != null && BattleManager.Instance.CurrentState != GameState.PlayerTurn)
        {
            ClearIdleHint();
            DeselectCurrent();
            return;
        }

        if (gameGrid != null && gameGrid.IsProcessing)
        {
            ClearIdleHint();
            DeselectCurrent();
            return;
        }

        UpdateIdleHintTimer();

        Vector3 pointerWorldPos = InputHelper.GetPointerWorldPosition();

        if (InputHelper.GetPointerDown())
        {
            ClearIdleHint();

            if (InputHelper.IsPointerOverUI())
            {
                DeselectCurrent();
                return;
            }

            Gem hitGem = GetGemAtPosition(pointerWorldPos);
            if (hitGem != null)
            {
                if (selectedGem == null)
                {
                    SelectGem(hitGem, pointerWorldPos);
                }
                else
                {
                    // If tapped an adjacent gem, execute swap!
                    if (IsAdjacent(selectedGem, hitGem))
                    {
                        Vector2Int from = selectedGem.gridPosition;
                        Vector2Int to = hitGem.gridPosition;
                        DeselectCurrent();
                        gameGrid.TrySwap(from, to, true);
                    }
                    else if (selectedGem == hitGem)
                    {
                        DeselectCurrent();
                    }
                    else
                    {
                        // Selected a new non-adjacent gem
                        DeselectCurrent();
                        SelectGem(hitGem, pointerWorldPos);
                    }
                }
            }
            else
            {
                DeselectCurrent();
            }
        }

        if (InputHelper.GetPointerHeld() && isDragging && selectedGem != null)
        {
            Vector3 delta = pointerWorldPos - dragStartPos;

            if (delta.magnitude >= swipeThreshold)
            {
                Vector2Int dir = Vector2Int.zero;

                if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y))
                {
                    dir = delta.x > 0 ? new Vector2Int(1, 0) : new Vector2Int(-1, 0);
                }
                else
                {
                    // In grid coords, world Y up is grid y - 1, world Y down is grid y + 1
                    dir = delta.y > 0 ? new Vector2Int(0, -1) : new Vector2Int(0, 1);
                }

                Vector2Int targetPos = selectedGem.gridPosition + dir;

                if (gameGrid.IsValidGridPos(targetPos))
                {
                    Vector2Int from = selectedGem.gridPosition;
                    DeselectCurrent();
                    gameGrid.TrySwap(from, targetPos, true);
                }
                else
                {
                    DeselectCurrent();
                }
            }
        }

        if (InputHelper.GetPointerUp())
        {
            isDragging = false;
        }
    }

    private void SelectGem(Gem gem, Vector3 worldPos)
    {
        selectedGem = gem;
        dragStartPos = worldPos;
        isDragging = true;
        selectedGem.SetSelected(true);

        if (AudioManager.Instance != null)
        {
            AudioManager.Instance.PlayGemSelect();
        }
    }

    private void DeselectCurrent()
    {
        if (selectedGem != null)
        {
            selectedGem.SetSelected(false);
            selectedGem = null;
        }
        isDragging = false;
    }

    private Gem GetGemAtPosition(Vector2 pos)
    {
        Collider2D hit = Physics2D.OverlapPoint(pos, gemLayer);
        if (hit != null)
        {
            return hit.GetComponent<Gem>();
        }
        return null;
    }

    private bool IsAdjacent(Gem g1, Gem g2)
    {
        int dist = Mathf.Abs(g1.gridPosition.x - g2.gridPosition.x) + Mathf.Abs(g1.gridPosition.y - g2.gridPosition.y);
        return dist == 1;
    }

    private void UpdateIdleHintTimer()
    {
        if (selectedGem != null || isDragging)
        {
            idleTimer = 0f;
            return;
        }

        if (hintGemA != null || hintGemB != null) return;

        idleTimer += Time.unscaledDeltaTime;
        if (idleTimer < idleHintDelay || gameGrid == null) return;

        var moves = gameGrid.FindAllPossibleMoves();
        if (moves != null && moves.Count > 0)
        {
            // Prefer highest match count (Match-4/5) or Damage gem as a helpful hint
            moves.Sort((a, b) =>
            {
                int cmp = b.matchCount.CompareTo(a.matchCount);
                if (cmp != 0) return cmp;
                bool aDmg = a.gemType == GemType.Damage;
                bool bDmg = b.gemType == GemType.Damage;
                return bDmg.CompareTo(aDmg);
            });

            hintGemA = gameGrid.GetGem(moves[0].from);
            hintGemB = gameGrid.GetGem(moves[0].to);
            if (hintGemA != null) hintGemA.SetHintPulse(true);
            if (hintGemB != null) hintGemB.SetHintPulse(true);
        }
        else
        {
            idleTimer = 0f;
        }
    }

    private void ClearIdleHint()
    {
        idleTimer = 0f;
        if (hintGemA != null)
        {
            hintGemA.SetHintPulse(false);
            hintGemA = null;
        }
        if (hintGemB != null)
        {
            hintGemB.SetHintPulse(false);
            hintGemB = null;
        }
    }
}
