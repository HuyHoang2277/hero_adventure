using UnityEngine;

public class Gem : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    private GemData _gemData;

    public Vector2Int gridPosition { get; set; }

    public void SetGridPosition(Vector2Int gridPos)
    {
        gridPosition = gridPos;
    }

    public void SetSelected(bool isSelected)
    {
        transform.localScale = isSelected ? Vector3.one * 1.15f : Vector3.one;
    }

    public void Init(GemData data, Vector2Int gridPos)
    {
        _gemData = data;
        gridPosition = gridPos;
        transform.localScale = Vector3.one;

        if (_spriteRenderer != null && _gemData != null)
        {
            _spriteRenderer.sprite = _gemData.gemSprite;
        }
    }

    public GemData GetGemData() => _gemData;
}