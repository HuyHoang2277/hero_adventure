using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum GemType
{
    Damage,
    Health,
    Shield,
    CritDamage,
    CritRate,
    Dodge
}

[CreateAssetMenu(fileName = "GemData", menuName = "ScriptableObjects/GemData")]
public class GemData : ScriptableObject
{
    public GemType gemType;
    public Sprite gemSprite;
    public Color lineColor;
    public float powerValue;

    [Tooltip("Tỉ lệ cộng thêm theo MaxHealth (vd: 0.08 = 8% MaxHP). Áp dụng cho Health và Shield.")]
    [Range(0f, 0.5f)]
    public float maxHealthPercent = 0f;

    [Header("Spawn Settings")]
    [Range(1, 10)]
    public int spawnWeight = 1;
}
