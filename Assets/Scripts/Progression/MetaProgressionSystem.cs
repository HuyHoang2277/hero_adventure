using System.Collections.Generic;
using UnityEngine;

public enum EquipmentSlotType
{
    Weapon = 0,
    Armor = 1,
    Helmet = 2,
    Boots = 3,
    Ring = 4,
    Amulet = 5
}

public enum ItemRarity
{
    Common,
    Rare,
    Epic
}

public enum ShopOfferSection
{
    Coin = 0,
    Diamond = 1,
    Other = 2
}

public enum ShopRewardType
{
    Gold,
    Exp,
    SpecificEquipment,
    CommonEquipmentChest,
    EpicEquipmentChest
}

public struct TalentDefinition
{
    public int Index;
    public string Title;
    public string ShortDesc;
    public int MaxLevel;
    public int BaseGoldCost;
    public int GoldCostStep;
    public Color AccentColor;
}

public struct EquipmentDefinition
{
    public int Id;
    public string Name;
    public string ShortTag;
    public EquipmentSlotType Slot;
    public ItemRarity Rarity;
    public float MaxHealth;
    public float Shield;
    public float Damage;
    public float CritRate;
    public float CritDamage;
    public float Dodge;
    public float StartBattleShield;
    public float Match4BonusHeal;
}

public struct RewardMilestoneDefinition
{
    public int Index;
    public string Title;
    public string ConditionText;
    public bool IsEndlessMilestone;
    public int TargetValue;
    public int GoldReward;
    public int DiamondReward;
    public int BonusEquipmentId; // -1 nếu không tặng trang bị
}

public struct ShopOfferDefinition
{
    public ShopOfferSection Section;
    public int SlotIndex; // 0..3 trong từng mục
    public string Label;
    public string AmountLabel;
    public int Price;
    public bool UsesDiamond;
    public ShopRewardType RewardType;
    public int RewardParam; // Số Gold/EXP hoặc ItemId
}

public struct PlayerMetaBonuses
{
    public float MaxHealth;
    public float Shield;
    public float Damage;
    public float CritRate;
    public float CritDamage;
    public float Dodge;
    public float GemPowerBonusPercent;
    public float StartBattleShield;
    public float Match4BonusHeal;
}

public struct PlayerTotalStatsPreview
{
    public int PlayerLevel;
    public float MaxHealth;
    public float Shield;
    public float Damage;
    public float CritRate;
    public float CritDamage;
    public float Dodge;
    public float GemPowerBonusPercent;
    public float Match4BonusHeal;
}

/// <summary>
/// Trung tâm dữ liệu & tính toán Meta-Progression: cấp nhân vật, 3 nhánh Talent, 12 Trang bị (6 ô), Shop và 8 Mốc thưởng.
/// </summary>
public static class MetaProgressionSystem
{
    public const int MAX_TALENT_LEVEL = 10;
    public const int MAX_EQUIPMENT_LEVEL = 5;

    public static readonly TalentDefinition[] Talents = new TalentDefinition[]
    {
        new TalentDefinition
        {
            Index = 0,
            Title = "Attack Mastery",
            ShortDesc = "+1.5 DMG & +0.5% Crit / Lv",
            MaxLevel = MAX_TALENT_LEVEL,
            BaseGoldCost = 40,
            GoldCostStep = 35,
            AccentColor = new Color(0.86f, 0.29f, 0.22f, 1f)
        },
        new TalentDefinition
        {
            Index = 1,
            Title = "Vitality & Guard",
            ShortDesc = "+14 HP & +2 Shield / Lv",
            MaxLevel = MAX_TALENT_LEVEL,
            BaseGoldCost = 40,
            GoldCostStep = 35,
            AccentColor = new Color(0.16f, 0.64f, 0.36f, 1f)
        },
        new TalentDefinition
        {
            Index = 2,
            Title = "Gem Mastery",
            ShortDesc = "+5% Gem Power (DMG/HP/Shield) / Lv",
            MaxLevel = MAX_TALENT_LEVEL,
            BaseGoldCost = 50,
            GoldCostStep = 45,
            AccentColor = new Color(0.54f, 0.27f, 0.80f, 1f)
        }
    };

    public static readonly EquipmentDefinition[] Equipments = new EquipmentDefinition[]
    {
        // Slot 0: Weapon
        new EquipmentDefinition { Id = 0, Name = "Iron Sword", ShortTag = "WPN", Slot = EquipmentSlotType.Weapon, Rarity = ItemRarity.Common, Damage = 3f, CritRate = 2f },
        new EquipmentDefinition { Id = 1, Name = "Dragon Blade", ShortTag = "WPN+", Slot = EquipmentSlotType.Weapon, Rarity = ItemRarity.Epic, Damage = 7f, CritRate = 5f, CritDamage = 10f },
        // Slot 1: Armor
        new EquipmentDefinition { Id = 2, Name = "Leather Vest", ShortTag = "ARM", Slot = EquipmentSlotType.Armor, Rarity = ItemRarity.Common, MaxHealth = 20f, Shield = 5f },
        new EquipmentDefinition { Id = 3, Name = "Knight Plate", ShortTag = "ARM+", Slot = EquipmentSlotType.Armor, Rarity = ItemRarity.Epic, MaxHealth = 45f, Shield = 12f },
        // Slot 2: Helmet
        new EquipmentDefinition { Id = 4, Name = "Scout Helm", ShortTag = "HLM", Slot = EquipmentSlotType.Helmet, Rarity = ItemRarity.Common, MaxHealth = 15f, Dodge = 2f },
        new EquipmentDefinition { Id = 5, Name = "Paladin Crown", ShortTag = "HLM+", Slot = EquipmentSlotType.Helmet, Rarity = ItemRarity.Rare, MaxHealth = 35f, Shield = 8f, Dodge = 4f },
        // Slot 3: Boots
        new EquipmentDefinition { Id = 6, Name = "Swift Boots", ShortTag = "BTS", Slot = EquipmentSlotType.Boots, Rarity = ItemRarity.Common, MaxHealth = 12f, Dodge = 3f },
        new EquipmentDefinition { Id = 7, Name = "Wind Greaves", ShortTag = "BTS+", Slot = EquipmentSlotType.Boots, Rarity = ItemRarity.Rare, MaxHealth = 26f, CritRate = 3f, Dodge = 5f },
        // Slot 4: Ring
        new EquipmentDefinition { Id = 8, Name = "Ruby Ring", ShortTag = "RNG", Slot = EquipmentSlotType.Ring, Rarity = ItemRarity.Common, Damage = 2f, CritDamage = 8f },
        new EquipmentDefinition { Id = 9, Name = "Phoenix Ring", ShortTag = "RNG+", Slot = EquipmentSlotType.Ring, Rarity = ItemRarity.Epic, Damage = 5f, CritDamage = 14f, Match4BonusHeal = 6f },
        // Slot 5: Amulet
        new EquipmentDefinition { Id = 10, Name = "Guard Charm", ShortTag = "AML", Slot = EquipmentSlotType.Amulet, Rarity = ItemRarity.Rare, Shield = 8f, StartBattleShield = 12f },
        new EquipmentDefinition { Id = 11, Name = "Star Amulet", ShortTag = "AML+", Slot = EquipmentSlotType.Amulet, Rarity = ItemRarity.Epic, MaxHealth = 22f, Shield = 14f, StartBattleShield = 22f }
    };

    public static readonly RewardMilestoneDefinition[] Milestones = new RewardMilestoneDefinition[]
    {
        new RewardMilestoneDefinition { Index = 0, Title = "First Steps", ConditionText = "Earn 3 Stars", IsEndlessMilestone = false, TargetValue = 3, GoldReward = 50, DiamondReward = 2, BonusEquipmentId = -1 },
        new RewardMilestoneDefinition { Index = 1, Title = "Rising Hero", ConditionText = "Earn 6 Stars", IsEndlessMilestone = false, TargetValue = 6, GoldReward = 100, DiamondReward = 4, BonusEquipmentId = -1 },
        new RewardMilestoneDefinition { Index = 2, Title = "Battle Veteran", ConditionText = "Earn 9 Stars", IsEndlessMilestone = false, TargetValue = 9, GoldReward = 180, DiamondReward = 6, BonusEquipmentId = 5 },
        new RewardMilestoneDefinition { Index = 3, Title = "Master Tactician", ConditionText = "Earn 12 Stars", IsEndlessMilestone = false, TargetValue = 12, GoldReward = 280, DiamondReward = 10, BonusEquipmentId = -1 },
        new RewardMilestoneDefinition { Index = 4, Title = "Perfect Conqueror", ConditionText = "Earn 15 Stars", IsEndlessMilestone = false, TargetValue = 15, GoldReward = 450, DiamondReward = 15, BonusEquipmentId = 1 },
        new RewardMilestoneDefinition { Index = 5, Title = "Endless Survivor", ConditionText = "Clear Endless Round 3", IsEndlessMilestone = true, TargetValue = 3, GoldReward = 120, DiamondReward = 3, BonusEquipmentId = -1 },
        new RewardMilestoneDefinition { Index = 6, Title = "Golem Slayer", ConditionText = "Clear Endless Round 5", IsEndlessMilestone = true, TargetValue = 5, GoldReward = 250, DiamondReward = 8, BonusEquipmentId = 10 },
        new RewardMilestoneDefinition { Index = 7, Title = "Legend of the Board", ConditionText = "Clear Endless Round 10", IsEndlessMilestone = true, TargetValue = 10, GoldReward = 500, DiamondReward = 15, BonusEquipmentId = 3 }
    };

    public static readonly ShopOfferDefinition[] ShopOffers = new ShopOfferDefinition[]
    {
        // CoinPurchase (mua bằng Gold)
        new ShopOfferDefinition { Section = ShopOfferSection.Coin, SlotIndex = 0, Label = "Iron Chest", AmountLabel = "Gear x1", Price = 80, UsesDiamond = false, RewardType = ShopRewardType.CommonEquipmentChest, RewardParam = 0 },
        new ShopOfferDefinition { Section = ShopOfferSection.Coin, SlotIndex = 1, Label = "Leather Vest", AmountLabel = "+20 HP", Price = 70, UsesDiamond = false, RewardType = ShopRewardType.SpecificEquipment, RewardParam = 2 },
        new ShopOfferDefinition { Section = ShopOfferSection.Coin, SlotIndex = 2, Label = "Scout Helm", AmountLabel = "+15 HP", Price = 70, UsesDiamond = false, RewardType = ShopRewardType.SpecificEquipment, RewardParam = 4 },
        new ShopOfferDefinition { Section = ShopOfferSection.Coin, SlotIndex = 3, Label = "Ruby Ring", AmountLabel = "+2 DMG", Price = 90, UsesDiamond = false, RewardType = ShopRewardType.SpecificEquipment, RewardParam = 8 },

        // DiamondPurchase (mua bằng Diamond)
        new ShopOfferDefinition { Section = ShopOfferSection.Diamond, SlotIndex = 0, Label = "Epic Chest", AmountLabel = "Epic x1", Price = 8, UsesDiamond = true, RewardType = ShopRewardType.EpicEquipmentChest, RewardParam = 0 },
        new ShopOfferDefinition { Section = ShopOfferSection.Diamond, SlotIndex = 1, Label = "Gold Pouch", AmountLabel = "+150 G", Price = 3, UsesDiamond = true, RewardType = ShopRewardType.Gold, RewardParam = 150 },
        new ShopOfferDefinition { Section = ShopOfferSection.Diamond, SlotIndex = 2, Label = "Gold Vault", AmountLabel = "+450 G", Price = 8, UsesDiamond = true, RewardType = ShopRewardType.Gold, RewardParam = 450 },
        new ShopOfferDefinition { Section = ShopOfferSection.Diamond, SlotIndex = 3, Label = "Hero Tome", AmountLabel = "+150 XP", Price = 5, UsesDiamond = true, RewardType = ShopRewardType.Exp, RewardParam = 150 },

        // OtherPurchase (trang bị đặc biệt)
        new ShopOfferDefinition { Section = ShopOfferSection.Other, SlotIndex = 0, Label = "Swift Boots", AmountLabel = "+3% Dge", Price = 80, UsesDiamond = false, RewardType = ShopRewardType.SpecificEquipment, RewardParam = 6 },
        new ShopOfferDefinition { Section = ShopOfferSection.Other, SlotIndex = 1, Label = "Guard Charm", AmountLabel = "+12 Shd", Price = 110, UsesDiamond = false, RewardType = ShopRewardType.SpecificEquipment, RewardParam = 10 },
        new ShopOfferDefinition { Section = ShopOfferSection.Other, SlotIndex = 2, Label = "Phoenix Ring", AmountLabel = "M4 Heal", Price = 12, UsesDiamond = true, RewardType = ShopRewardType.SpecificEquipment, RewardParam = 9 },
        new ShopOfferDefinition { Section = ShopOfferSection.Other, SlotIndex = 3, Label = "Star Amulet", AmountLabel = "+22 Shd", Price = 12, UsesDiamond = true, RewardType = ShopRewardType.SpecificEquipment, RewardParam = 11 }
    };

    public static int GetTalentUpgradeCost(int talentIndex, int currentLevel)
    {
        if (talentIndex < 0 || talentIndex >= Talents.Length) return 9999;
        TalentDefinition def = Talents[talentIndex];
        return def.BaseGoldCost + currentLevel * def.GoldCostStep;
    }

    public static int GetEquipmentUpgradeCost(int currentLevel)
    {
        return 50 + Mathf.Max(0, currentLevel - 1) * 45;
    }

    public static float GetEquipmentLevelScale(int level)
    {
        if (level <= 0) return 0f;
        return 1f + (level - 1) * 0.20f;
    }

    public static Color GetRarityColor(ItemRarity rarity)
    {
        switch (rarity)
        {
            case ItemRarity.Rare: return new Color(0.22f, 0.58f, 0.94f, 1f);
            case ItemRarity.Epic: return new Color(0.68f, 0.30f, 0.88f, 1f);
            default: return new Color(0.42f, 0.50f, 0.58f, 1f);
        }
    }

    public static string GetSlotShortName(EquipmentSlotType slot)
    {
        switch (slot)
        {
            case EquipmentSlotType.Weapon: return "WPN";
            case EquipmentSlotType.Armor: return "ARM";
            case EquipmentSlotType.Helmet: return "HLM";
            case EquipmentSlotType.Boots: return "BTS";
            case EquipmentSlotType.Ring: return "RNG";
            case EquipmentSlotType.Amulet: return "AML";
            default: return "EQP";
        }
    }

    public static string BuildEquipmentStatSummary(EquipmentDefinition eq, int level)
    {
        float scale = GetEquipmentLevelScale(Mathf.Max(1, level));
        List<string> parts = new List<string>();
        if (eq.MaxHealth > 0f) parts.Add($"+{Mathf.RoundToInt(eq.MaxHealth * scale)} HP");
        if (eq.Damage > 0f) parts.Add($"+{(eq.Damage * scale):0.#} DMG");
        if (eq.Shield > 0f) parts.Add($"+{Mathf.RoundToInt(eq.Shield * scale)} SHD");
        if (eq.CritRate > 0f) parts.Add($"+{(eq.CritRate * scale):0.#}% Crit");
        if (eq.CritDamage > 0f) parts.Add($"+{Mathf.RoundToInt(eq.CritDamage * scale)}% CDmg");
        if (eq.Dodge > 0f) parts.Add($"+{(eq.Dodge * scale):0.#}% Dodge");
        if (eq.StartBattleShield > 0f) parts.Add($"Start +{Mathf.RoundToInt(eq.StartBattleShield * scale)} SHD");
        if (eq.Match4BonusHeal > 0f) parts.Add($"M4+ Heal {Mathf.RoundToInt(eq.Match4BonusHeal * scale)}");
        return string.Join(" | ", parts);
    }

    public static PlayerMetaBonuses CalculatePlayerBonuses(SecureGameData data)
    {
        PlayerMetaBonuses bonuses = new PlayerMetaBonuses();
        if (data == null) return bonuses;

        // 1. Thưởng chỉ số nền từ PlayerLevel (Level 1 = +0 để giữ chuẩn cân bằng ban đầu)
        int extraLevels = Mathf.Max(0, data.PlayerLevel - 1);
        bonuses.MaxHealth += extraLevels * 8f;
        bonuses.Damage += extraLevels * 1.2f;
        bonuses.Shield += extraLevels * 2f;

        // 2. Thưởng từ 3 nhánh Talent
        int atkLv = data.GetTalentLevel(0);
        int defLv = data.GetTalentLevel(1);
        int gemLv = data.GetTalentLevel(2);

        bonuses.Damage += atkLv * 1.5f;
        bonuses.CritRate += atkLv * 0.5f;
        bonuses.MaxHealth += defLv * 14f;
        bonuses.Shield += defLv * 2f;
        bonuses.GemPowerBonusPercent += gemLv * 0.05f;

        // 3. Thưởng từ các Trang bị đang mặc (6 ô)
        for (int slot = 0; slot < SecureGameData.EQUIPMENT_SLOT_COUNT; slot++)
        {
            int itemId = data.GetEquippedItemId(slot);
            if (itemId < 0 || itemId >= Equipments.Length) continue;

            int eqLevel = data.GetEquipmentLevel(itemId);
            if (eqLevel <= 0) continue;

            EquipmentDefinition eq = Equipments[itemId];
            float scale = GetEquipmentLevelScale(eqLevel);

            bonuses.MaxHealth += eq.MaxHealth * scale;
            bonuses.Shield += eq.Shield * scale;
            bonuses.Damage += eq.Damage * scale;
            bonuses.CritRate += eq.CritRate * scale;
            bonuses.CritDamage += eq.CritDamage * scale;
            bonuses.Dodge += eq.Dodge * scale;
            bonuses.StartBattleShield += eq.StartBattleShield * scale;
            bonuses.Match4BonusHeal += eq.Match4BonusHeal * scale;
        }

        return bonuses;
    }

    public static PlayerTotalStatsPreview CalculateTotalPlayerStats(CharacterInfoSO baseStat, SecureGameData data)
    {
        float baseHp = baseStat != null ? baseStat.maxHealth : 150f;
        float baseShd = baseStat != null ? baseStat.baseShield : 10f;
        float baseDmg = baseStat != null ? baseStat.baseDamage : 15f;
        float baseCrit = baseStat != null ? baseStat.baseCritRate : 5f;
        float baseCritDmg = baseStat != null ? baseStat.baseCritDamage : 50f;
        float baseDodge = baseStat != null ? baseStat.baseBlockRate : 5f;

        PlayerMetaBonuses b = CalculatePlayerBonuses(data);
        float totalHp = Mathf.Round((baseHp + b.MaxHealth) * 10f) / 10f;

        return new PlayerTotalStatsPreview
        {
            PlayerLevel = data != null ? data.PlayerLevel : 1,
            MaxHealth = totalHp,
            Shield = Mathf.Round(Mathf.Min(totalHp * 0.5f, baseShd + b.Shield + b.StartBattleShield) * 10f) / 10f,
            Damage = Mathf.Round((baseDmg + b.Damage) * 10f) / 10f,
            CritRate = Mathf.Round(Mathf.Min(60f, baseCrit + b.CritRate) * 10f) / 10f,
            CritDamage = Mathf.Round(Mathf.Min(140f, baseCritDmg + b.CritDamage) * 10f) / 10f,
            Dodge = Mathf.Round(Mathf.Min(35f, baseDodge + b.Dodge) * 10f) / 10f,
            GemPowerBonusPercent = b.GemPowerBonusPercent,
            Match4BonusHeal = b.Match4BonusHeal
        };
    }

    public static bool TryUpgradeTalent(int talentIndex, out string feedback)
    {
        feedback = "";
        SecureGameData data = DataManager.Instance?.GameData;
        if (data == null || talentIndex < 0 || talentIndex >= Talents.Length) return false;

        TalentDefinition def = Talents[talentIndex];
        int curLv = data.GetTalentLevel(talentIndex);
        if (curLv >= def.MaxLevel)
        {
            feedback = $"{def.Title} is already MAX!";
            return false;
        }

        int cost = GetTalentUpgradeCost(talentIndex, curLv);
        if (!data.TrySpendGold(cost))
        {
            feedback = $"Need {cost} Gold!";
            return false;
        }

        data.SetTalentLevel(talentIndex, curLv + 1);
        feedback = $"{def.Title} upgraded to Lv.{curLv + 1}!";
        return true;
    }

    public static bool TryToggleEquip(int itemId, out string feedback)
    {
        feedback = "";
        SecureGameData data = DataManager.Instance?.GameData;
        if (data == null || itemId < 0 || itemId >= Equipments.Length) return false;
        if (data.GetEquipmentLevel(itemId) <= 0) return false;

        EquipmentDefinition eq = Equipments[itemId];
        int slot = (int)eq.Slot;
        int currentlyEquipped = data.GetEquippedItemId(slot);

        if (currentlyEquipped == itemId)
        {
            data.SetEquippedItemId(slot, -1);
            feedback = $"Unequipped {eq.Name}";
        }
        else
        {
            data.SetEquippedItemId(slot, itemId);
            feedback = $"Equipped {eq.Name}!";
        }
        return true;
    }

    public static bool TryUpgradeEquipment(int itemId, out string feedback)
    {
        feedback = "";
        SecureGameData data = DataManager.Instance?.GameData;
        if (data == null || itemId < 0 || itemId >= Equipments.Length) return false;

        int curLv = data.GetEquipmentLevel(itemId);
        if (curLv <= 0) return false;
        if (curLv >= MAX_EQUIPMENT_LEVEL)
        {
            feedback = $"{Equipments[itemId].Name} is MAX Lv.{MAX_EQUIPMENT_LEVEL}!";
            return false;
        }

        int cost = GetEquipmentUpgradeCost(curLv);
        if (!data.TrySpendGold(cost))
        {
            feedback = $"Need {cost} Gold to upgrade!";
            return false;
        }

        data.GrantOrUpgradeEquipment(itemId, MAX_EQUIPMENT_LEVEL);
        feedback = $"{Equipments[itemId].Name} -> Lv.{curLv + 1}!";
        return true;
    }

    public static bool TryPurchaseShopOffer(ShopOfferDefinition offer, out string feedback)
    {
        feedback = "";
        SecureGameData data = DataManager.Instance?.GameData;
        if (data == null) return false;

        if (offer.RewardType == ShopRewardType.SpecificEquipment)
        {
            int eqLv = data.GetEquipmentLevel(offer.RewardParam);
            if (eqLv >= MAX_EQUIPMENT_LEVEL)
            {
                feedback = $"{Equipments[offer.RewardParam].Name} is already MAX!";
                return false;
            }
        }

        bool paid = offer.UsesDiamond ? data.TrySpendDiamond(offer.Price) : data.TrySpendGold(offer.Price);
        if (!paid)
        {
            feedback = offer.UsesDiamond ? $"Need {offer.Price} Diamonds!" : $"Need {offer.Price} Gold!";
            return false;
        }

        switch (offer.RewardType)
        {
            case ShopRewardType.Gold:
                data.AddResources(offer.RewardParam, 0, 0);
                data.Save();
                feedback = $"Received +{offer.RewardParam} Gold!";
                break;

            case ShopRewardType.Exp:
                data.AddResources(0, 0, offer.RewardParam);
                data.Save();
                feedback = $"Received +{offer.RewardParam} EXP!";
                break;

            case ShopRewardType.SpecificEquipment:
                data.GrantOrUpgradeEquipment(offer.RewardParam, MAX_EQUIPMENT_LEVEL);
                feedback = $"Got {Equipments[offer.RewardParam].Name} (Lv.{data.GetEquipmentLevel(offer.RewardParam)})!";
                break;

            case ShopRewardType.CommonEquipmentChest:
            {
                int rolled = RollChestItem(false);
                data.GrantOrUpgradeEquipment(rolled, MAX_EQUIPMENT_LEVEL);
                feedback = $"Chest: {Equipments[rolled].Name} (Lv.{data.GetEquipmentLevel(rolled)})!";
                break;
            }

            case ShopRewardType.EpicEquipmentChest:
            {
                int rolled = RollChestItem(true);
                data.GrantOrUpgradeEquipment(rolled, MAX_EQUIPMENT_LEVEL);
                feedback = $"Epic Chest: {Equipments[rolled].Name} (Lv.{data.GetEquipmentLevel(rolled)})!";
                break;
            }
        }

        return true;
    }

    private static int RollChestItem(bool epicOnly)
    {
        List<int> pool = new List<int>();
        for (int i = 0; i < Equipments.Length; i++)
        {
            if (epicOnly)
            {
                if (Equipments[i].Rarity == ItemRarity.Rare || Equipments[i].Rarity == ItemRarity.Epic)
                    pool.Add(i);
            }
            else
            {
                if (Equipments[i].Rarity == ItemRarity.Common || Equipments[i].Rarity == ItemRarity.Rare)
                    pool.Add(i);
            }
        }
        return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : 0;
    }

    public static int GetMilestoneCurrentProgress(RewardMilestoneDefinition milestone, SecureGameData data)
    {
        if (data == null) return 0;
        return milestone.IsEndlessMilestone ? data.BestEndlessRound : data.GetTotalStars();
    }

    public static bool TryClaimMilestone(int milestoneIndex, out string feedback)
    {
        feedback = "";
        SecureGameData data = DataManager.Instance?.GameData;
        if (data == null || milestoneIndex < 0 || milestoneIndex >= Milestones.Length) return false;

        RewardMilestoneDefinition m = Milestones[milestoneIndex];
        if (data.IsRewardClaimed(milestoneIndex))
        {
            feedback = "Already claimed!";
            return false;
        }

        int current = GetMilestoneCurrentProgress(m, data);
        if (current < m.TargetValue)
        {
            feedback = $"Progress: {current}/{m.TargetValue}";
            return false;
        }

        data.AddResources(m.GoldReward, m.DiamondReward, 0);
        if (m.BonusEquipmentId >= 0 && m.BonusEquipmentId < Equipments.Length)
        {
            data.GrantOrUpgradeEquipment(m.BonusEquipmentId, MAX_EQUIPMENT_LEVEL);
        }
        data.MarkRewardClaimed(milestoneIndex);

        feedback = m.BonusEquipmentId >= 0
            ? $"Claimed +{m.GoldReward}G, +{m.DiamondReward}D & {Equipments[m.BonusEquipmentId].Name}!"
            : $"Claimed +{m.GoldReward}G & +{m.DiamondReward}D!";
        return true;
    }
}
