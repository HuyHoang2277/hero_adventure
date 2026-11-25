using UnityEngine;
using DG.Tweening;

public class Gem : MonoBehaviour
{
    [SerializeField] private SpriteRenderer _spriteRenderer;
    private GemData _gemData;
    private Tween _hintTween;

    public Vector2Int gridPosition { get; set; }

    public void SetGridPosition(Vector2Int gridPos)
    {
        gridPosition = gridPos;
    }

    public void SetSelected(bool isSelected)
    {
        StopHintPulse();
        transform.localScale = isSelected ? Vector3.one * 1.15f : Vector3.one;
    }

    public void SetHintPulse(bool active)
    {
        if (!active)
        {
            StopHintPulse();
            return;
        }

        if (_hintTween != null && _hintTween.IsActive()) return;

        transform.localScale = Vector3.one;
        _hintTween = transform
            .DOScale(Vector3.one * 1.11f, 0.45f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo);
    }

    private void StopHintPulse()
    {
        if (_hintTween != null)
        {
            _hintTween.Kill();
            _hintTween = null;
        }
        transform.localScale = Vector3.one;
    }

    public void Init(GemData data, Vector2Int gridPos)
    {
        StopHintPulse();
        _gemData = data;
        gridPosition = gridPos;
        transform.localScale = Vector3.one;

        if (_spriteRenderer != null && _gemData != null)
        {
            _spriteRenderer.sprite = _gemData.gemSprite;
        }
    }

    private void OnDisable()
    {
        StopHintPulse();
    }

    public GemData GetGemData() => _gemData;
}