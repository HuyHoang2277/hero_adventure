using System;
using System.Collections.Generic;
using UnityEngine;

public enum SimGearPreset
{
    None,
    StarterIronSword,
    MidGameRareSet,
    FullEpicMaxed
}

public struct SimConfig
{
    public int PlayerLevel;
    public int AttackTalentLevel;
    public int DefenseTalentLevel;
    public int GemTalentLevel;
    public SimGearPreset GearPreset;
    public int Runs;
    public float MultiplierPerGem;
    public float ExtraBlastGemFactor;
    public float ComboBonusPerMatch;
    public float MaxComboMultiplier;
    public float ShieldTurnDecayRate;
    public float WaveBuffDecayRate;
    public float OverkillCleaveRatio;
    public float SupportStrikeMultiplier;
    public float EnemyBasicFallbackMultiplier;

    public static SimConfig CreateDefault(BalanceConfigSO so = null)
    {
        return new SimConfig
        {
            PlayerLevel = 1,
            AttackTalentLevel = 0,
            DefenseTalentLevel = 0,
            GemTalentLevel = 0,
            GearPreset = SimGearPreset.None,
            Runs = 500,
            MultiplierPerGem = so != null ? so.multiplierPerGem : 0.25f,
            ExtraBlastGemFactor = so != null ? so.extraBlastGemFactor : 0.35f,
            ComboBonusPerMatch = so != null ? so.comboBonusPerMatch : 0.10f,
            MaxComboMultiplier = so != null ? so.maxComboMultiplier : 1.45f,
            ShieldTurnDecayRate = so != null ? so.shieldTurnDecayRate : 0.25f,
            WaveBuffDecayRate = so != null ? so.waveBuffDecayRate : 0.50f,
            OverkillCleaveRatio = so != null ? so.overkillCleaveRatio : 0.50f,
            SupportStrikeMultiplier = so != null ? so.supportStrikeDamageMultiplier : 0.50f,
            EnemyBasicFallbackMultiplier = so != null ? so.enemyBasicAttackFallbackMultiplier : 0.65f
        };
    }
}

public struct LevelSimulationReport
{
    public int LevelID;
    public bool IsBossLevel;
    public int Runs;
    public float WinRatePercent;
    public float ThreeStarRatePercent;
    public float AvgTurns;
    public float AvgRemainingHpPercent;
    public string Diagnosis;
}

public struct EndlessSimulationReport
{
    public int Runs;
    public float AvgClearedRounds;
    public int MedianClearedRounds;
    public int MaxClearedRounds;
    public float Round5BossClearRatePercent;
    public string Diagnosis;
}

/// <summary>
/// Bộ mô phỏng Monte-Carlo thuần C# dùng đúng công thức chiến đấu của Hero's Adventure
/// để đánh giá Win Rate, TTK (số lượt), tỉ lệ 3 Sao và sức bền Endless trong tích tắc.
/// </summary>
public static class CombatSimulator
{
    private class SimTimedBuff
    {
        public int Type; // 0=CritRate, 1=CritDmg, 2=Dodge
        public float Amount;
        public int RemainingTurns;
    }

    private class SimUnit
    {
        public float MaxHp;
        public float Hp;
        public float Shield;
        public float BaseDamage;
        public float BaseCritRate;
        public float BaseCritDamage;
        public float BaseDodge;
        public float CurCritRate;
        public float CurCritDamage;
        public float CurDodge;
        public float GemMultiplier = 1f;
        public float Match4BonusHeal = 0f;
        public int SupportCooldown = 2;
        public int MaxSupportCooldown = 2;
        public float LastOverkill = 0f;
        public readonly List<SimTimedBuff> Buffs = new List<SimTimedBuff>();

        public bool IsDead => Hp <= 0f;

        public void RecalculateBuffs()
        {
            float bCrit = 0f, bCdmg = 0f, bDge = 0f;
            for (int i = 0; i < Buffs.Count; i++)
            {
                if (Buffs[i].Type == 0) bCrit += Buffs[i].Amount;
                else if (Buffs[i].Type == 1) bCdmg += Buffs[i].Amount;
                else bDge += Buffs[i].Amount;
            }
            CurCritRate = Math.Min(60f, Math.Min(BaseCritRate + 15f, BaseCritRate + bCrit));
            CurCritDamage = Math.Min(140f, Math.Min(BaseCritDamage + 30f, BaseCritDamage + bCdmg));
            CurDodge = Math.Min(35f, Math.Min(BaseDodge + 12f, BaseDodge + bDge));
        }

        public void TickTurn(float shieldDecay)
        {
            if (Shield > 0f)
            {
                Shield *= (1f - shieldDecay);
                if (Shield < 0.5f) Shield = 0f;
            }
            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                Buffs[i].RemainingTurns--;
                if (Buffs[i].RemainingTurns <= 0) Buffs.RemoveAt(i);
            }
            RecalculateBuffs();
        }

        public void DecayWaveBuffs(float decayRate)
        {
            float retain = 1f - decayRate;
            for (int i = Buffs.Count - 1; i >= 0; i--)
            {
                Buffs[i].Amount *= retain;
                if (Buffs[i].Amount < 0.3f) Buffs.RemoveAt(i);
            }
            RecalculateBuffs();
        }

        public float AddBuff(int type, float amount, float maxBonus, float hardCap)
        {
            float baseVal = type == 0 ? BaseCritRate : (type == 1 ? BaseCritDamage : BaseDodge);
            float curVal = type == 0 ? CurCritRate : (type == 1 ? CurCritDamage : CurDodge);
            float cap = Math.Min(hardCap, baseVal + maxBonus);
            float room = Math.Max(0f, cap - curVal);
            float applied = Math.Min(amount, room);
            if (applied > 0.01f)
            {
                Buffs.Add(new SimTimedBuff { Type = type, Amount = applied, RemainingTurns = 4 });
                RecalculateBuffs();
            }
            return Math.Max(0f, amount - applied);
        }

        public void Heal(float amount)
        {
            Hp = Math.Min(MaxHp, Hp + amount);
        }

        public void AddShield(float amount)
        {
            Shield = Math.Min(MaxHp * 0.50f, Shield + amount);
        }

        public float RollAttackDamage(float multiplier, System.Random rng)
        {
            float dmg = BaseDamage;
            if ((float)(rng.NextDouble() * 100.0) <= CurCritRate)
            {
                dmg += dmg * (CurCritDamage / 100f);
                // Xả 50% buff CritRate & CritDamage khi nổ chí mạng
                for (int i = Buffs.Count - 1; i >= 0; i--)
                {
                    if (Buffs[i].Type == 0 || Buffs[i].Type == 1)
                    {
                        Buffs[i].Amount *= 0.5f;
                        if (Buffs[i].Amount < 0.3f) Buffs.RemoveAt(i);
                    }
                }
                RecalculateBuffs();
            }
            return dmg * multiplier;
        }

        public void TakeDamage(float rawDamage, System.Random rng)
        {
            LastOverkill = 0f;
            if ((float)(rng.NextDouble() * 100.0) <= CurDodge)
            {
                for (int i = Buffs.Count - 1; i >= 0; i--)
                {
                    if (Buffs[i].Type == 2)
                    {
                        Buffs[i].Amount *= 0.5f;
                        if (Buffs[i].Amount < 0.3f) Buffs.RemoveAt(i);
                    }
                }
                RecalculateBuffs();
                return;
            }

            float dmg = rawDamage;
            if (Shield > 0f)
            {
                float absorbed = Math.Min(Shield, dmg);
                Shield -= absorbed;
                dmg -= absorbed;
            }
            if (dmg > 0f)
            {
                if (dmg > Hp) LastOverkill = dmg - Hp;
                Hp = Math.Max(0f, Hp - dmg);
            }
        }
    }

    public static LevelSimulationReport SimulateLevel(
        CharacterInfoSO playerBaseSO,
        LevelConfig levelConfig,
        List<CharacterInfoSO> fallbackEnemies,
        SimConfig cfg,
        int seed = 1337)
    {
        System.Random rng = new System.Random(seed + (levelConfig != null ? levelConfig.LevelID * 97 : 0));
        int runs = Math.Max(10, cfg.Runs);
        int wins = 0;
        int threeStars = 0;
        int totalTurns = 0;
        float totalWinHpPct = 0f;

        for (int r = 0; r < runs; r++)
        {
            SimUnit player = BuildPlayerUnit(playerBaseSO, cfg);
            int maxWaves = levelConfig != null ? Math.Max(1, levelConfig.MaxWaves) : 3;
            bool wonLevel = true;
            int battleTurns = 0;

            for (int wave = 1; wave <= maxWaves; wave++)
            {
                if (wave > 1) player.DecayWaveBuffs(cfg.WaveBuffDecayRate);

                float hpMult = levelConfig != null
                    ? levelConfig.levelDifficultyMultiplier + (wave - 1) * levelConfig.waveScalingIncrement
                    : 1f;
                float dmgMult = 1f + (hpMult - 1f) * 0.90f;
                float shdMult = 1f + (hpMult - 1f) * 0.85f;

                List<SimUnit> enemies = BuildLevelWaveEnemies(levelConfig, fallbackEnemies, wave, maxWaves, hpMult, dmgMult, shdMult, rng);
                bool waveWon = SimulateWave(player, enemies, cfg, rng, ref battleTurns);
                if (!waveWon)
                {
                    wonLevel = false;
                    break;
                }
            }

            totalTurns += battleTurns;
            if (wonLevel && !player.IsDead)
            {
                wins++;
                float hpPct = (player.Hp / Math.Max(1f, player.MaxHp)) * 100f;
                totalWinHpPct += hpPct;
                float thresh3 = levelConfig != null ? levelConfig.threeStarHPThreshold : 70f;
                if (hpPct + 0.01f >= thresh3) threeStars++;
            }
        }

        float winRate = (wins * 100f) / runs;
        float threeStarRate = (threeStars * 100f) / runs;
        float avgTurns = (float)totalTurns / runs;
        float avgWinHp = wins > 0 ? totalWinHpPct / wins : 0f;

        return new LevelSimulationReport
        {
            LevelID = levelConfig != null ? levelConfig.LevelID : 0,
            IsBossLevel = levelConfig != null && levelConfig.IsBossLevel,
            Runs = runs,
            WinRatePercent = winRate,
            ThreeStarRatePercent = threeStarRate,
            AvgTurns = avgTurns,
            AvgRemainingHpPercent = avgWinHp,
            Diagnosis = DiagnoseLevel(winRate, threeStarRate, avgTurns)
        };
    }

    public static EndlessSimulationReport SimulateEndless(
        CharacterInfoSO playerBaseSO,
        List<CharacterInfoSO> enemyPool,
        CharacterInfoSO bossSO,
        SimConfig cfg,
        int seed = 4242)
    {
        System.Random rng = new System.Random(seed);
        int runs = Math.Max(10, cfg.Runs);
        List<int> clearedList = new List<int>(runs);
        int boss5Clears = 0;
        int maxCleared = 0;
        int sumCleared = 0;

        List<CharacterInfoSO> sortedPool = new List<CharacterInfoSO>();
        if (enemyPool != null)
        {
            foreach (var e in enemyPool)
            {
                if (e != null && e != bossSO) sortedPool.Add(e);
            }
        }
        sortedPool.Sort((a, b) => a.maxHealth.CompareTo(b.maxHealth));

        for (int r = 0; r < runs; r++)
        {
            SimUnit player = BuildPlayerUnit(playerBaseSO, cfg);
            int cleared = 0;
            int dummyTurns = 0;

            for (int round = 1; round <= 40; round++)
            {
                if (round > 1) player.DecayWaveBuffs(cfg.WaveBuffDecayRate);

                int completed = round - 1;
                float hpMult = 1f + completed * 0.14f;
                float dmgMult = 1f + completed * 0.08f;
                float shdMult = 1f + completed * 0.10f;

                List<SimUnit> enemies = BuildEndlessWaveEnemies(sortedPool, bossSO, round, hpMult, dmgMult, shdMult, rng);
                bool won = SimulateWave(player, enemies, cfg, rng, ref dummyTurns);
                if (!won) break;

                cleared = round;
            }

            clearedList.Add(cleared);
            sumCleared += cleared;
            if (cleared > maxCleared) maxCleared = cleared;
            if (cleared >= 5) boss5Clears++;
        }

        clearedList.Sort();
        int median = clearedList[clearedList.Count / 2];
        float avg = (float)sumCleared / runs;
        float boss5Pct = (boss5Clears * 100f) / runs;

        string diag = avg < 3.0f
            ? "Qua kho o dau Endless (nên giam scaling Round 1-4)"
            : (avg > 14.0f ? "Qua de / keo dai (co the tang nhe DMG scaling)" : "Can bang tot");

        return new EndlessSimulationReport
        {
            Runs = runs,
            AvgClearedRounds = avg,
            MedianClearedRounds = median,
            MaxClearedRounds = maxCleared,
            Round5BossClearRatePercent = boss5Pct,
            Diagnosis = diag
        };
    }

    private static bool SimulateWave(SimUnit player, List<SimUnit> enemies, SimConfig cfg, System.Random rng, ref int turnCounter)
    {
        int rotationIdx = 0;
        for (int turn = 1; turn <= 80; turn++)
        {
            turnCounter++;

            // 1. Lượt Player
            if (turn > 1) player.TickTurn(cfg.ShieldTurnDecayRate);
            SimulateBoardMove(player, enemies, true, cfg, rng);

            enemies.RemoveAll(e => e.IsDead);
            if (enemies.Count == 0) return true;

            // 2. Lượt Enemy (luân phiên chủ công + hỗ trợ)
            if (turn > 1)
            {
                for (int i = 0; i < enemies.Count; i++) enemies[i].TickTurn(cfg.ShieldTurnDecayRate);
            }

            rotationIdx = rotationIdx % enemies.Count;
            SimUnit acting = enemies[rotationIdx];
            List<SimUnit> playerTargetList = new List<SimUnit> { player };
            SimulateBoardMove(acting, playerTargetList, false, cfg, rng);
            acting.SupportCooldown = acting.MaxSupportCooldown;

            if (player.IsDead) return false;

            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i] == acting || enemies[i].IsDead) continue;
                enemies[i].SupportCooldown--;
                if (enemies[i].SupportCooldown <= 0)
                {
                    enemies[i].SupportCooldown = enemies[i].MaxSupportCooldown;
                    float supDmg = enemies[i].RollAttackDamage(cfg.SupportStrikeMultiplier, rng);
                    player.TakeDamage(supDmg, rng);
                    if (player.IsDead) return false;
                }
            }

            rotationIdx = (rotationIdx + 1) % enemies.Count;
        }
        return false;
    }

    private static void SimulateBoardMove(SimUnit actor, List<SimUnit> targets, bool isPlayer, SimConfig cfg, System.Random rng)
    {
        // Mô phỏng nước đi có chủ đích (ưu tiên Damage hoặc Heal khi thấp máu) + 0-3 nhịp cascade trên bàn 8x8 (2/7 Damage)
        float hpPct = actor.Hp / Math.Max(1f, actor.MaxHp);
        GemType primaryGem;

        if (hpPct < 0.45f && rng.NextDouble() < 0.55)
            primaryGem = rng.NextDouble() < 0.6 ? GemType.Health : GemType.Shield;
        else if (rng.NextDouble() < (isPlayer ? 0.68 : 0.60))
            primaryGem = GemType.Damage;
        else
            primaryGem = RollWeightedGem(rng);

        // Xác suất Match-3 (78%), Match-4 (16%), Match-5+ (6%)
        double sizeRoll = rng.NextDouble();
        int primaryCount = sizeRoll < 0.78 ? 3 : (sizeRoll < 0.94 ? 4 : 5);

        int cascades = 1;
        double cascRoll = rng.NextDouble();
        if (cascRoll < 0.32) cascades = 2;
        else if (cascRoll < 0.44) cascades = 3;
        else if (cascRoll < 0.49) cascades = 4;

        float turnDamage = 0f;
        float turnHeal = 0f;
        float turnShield = 0f;
        float turnCrit = 0f;
        float turnCdmg = 0f;
        float turnDodge = 0f;
        int matchGroups = 0;
        int damageGroups = 0;
        int maxDamageMatch = 0;
        int maxAnyMatch = primaryCount;

        for (int step = 1; step <= cascades; step++)
        {
            float falloff = step <= 1 ? 1.00f : (step == 2 ? 0.65f : (step == 3 ? 0.45f : 0.30f));
            GemType gType = step == 1 ? primaryGem : RollWeightedGem(rng);
            int gCount = step == 1 ? primaryCount : (rng.NextDouble() < 0.88 ? 3 : 4);
            if (gCount > maxAnyMatch) maxAnyMatch = gCount;

            float mult = (1f + Math.Max(0, gCount - 3) * cfg.MultiplierPerGem) * falloff;
            matchGroups++;
            if (gType == GemType.Damage)
            {
                damageGroups++;
                if (gCount > maxDamageMatch) maxDamageMatch = gCount;
            }

            AccumulateGem(gType, mult, actor.MaxHp, ref turnDamage, ref turnHeal, ref turnShield, ref turnCrit, ref turnCdmg, ref turnDodge);

            // Ngọc nổ lan cho Match-4 (+4 viên) hoặc Match-5+ (+9 viên)
            int extraGems = gCount == 4 ? 4 : (gCount >= 5 ? 9 : 0);
            for (int e = 0; e < extraGems; e++)
            {
                GemType extraType = RollWeightedGem(rng);
                AccumulateGem(extraType, cfg.ExtraBlastGemFactor * falloff, actor.MaxHp, ref turnDamage, ref turnHeal, ref turnShield, ref turnCrit, ref turnCdmg, ref turnDodge);
            }
        }

        float comboMult = Math.Min(cfg.MaxComboMultiplier, 1f + Math.Max(0, matchGroups - 1) * cfg.ComboBonusPerMatch);
        float gemMult = isPlayer ? actor.GemMultiplier : 1f;

        if (turnHeal > 0f) actor.Heal(turnHeal * comboMult * gemMult);
        if (turnShield > 0f) actor.AddShield(turnShield * comboMult * gemMult);
        if (isPlayer && maxAnyMatch >= 4 && actor.Match4BonusHeal > 0f) actor.Heal(actor.Match4BonusHeal);

        if (turnCrit > 0f)
        {
            float over = actor.AddBuff(0, turnCrit, 15f, 60f);
            if (over > 0f) turnDamage += (over / 5f) * 0.35f;
        }
        if (turnCdmg > 0f)
        {
            float over = actor.AddBuff(1, turnCdmg, 30f, 140f);
            if (over > 0f) turnDamage += (over / 10f) * 0.35f;
        }
        if (turnDodge > 0f)
        {
            float over = actor.AddBuff(2, turnDodge, 12f, 35f);
            if (over > 0f) actor.AddShield((over / 3f) * actor.MaxHp * 0.05f);
        }

        // Chọn mục tiêu thấp máu nhất (focus fire chiến thuật)
        SimUnit target = null;
        for (int i = 0; i < targets.Count; i++)
        {
            if (targets[i].IsDead) continue;
            if (target == null || targets[i].Hp < target.Hp) target = targets[i];
        }
        if (target == null) return;

        if (turnDamage > 0f)
        {
            float rawDmg = actor.RollAttackDamage(turnDamage * comboMult * gemMult, rng);
            target.TakeDamage(rawDmg, rng);

            if (isPlayer && (damageGroups >= 2 || maxDamageMatch >= 4) && target.IsDead && target.LastOverkill >= 1f)
            {
                float cleave = target.LastOverkill * cfg.OverkillCleaveRatio;
                for (int i = 0; i < targets.Count; i++)
                {
                    if (!targets[i].IsDead && targets[i] != target)
                    {
                        targets[i].TakeDamage(cleave, rng);
                        break;
                    }
                }
            }
        }
        else if (!isPlayer && cfg.EnemyBasicFallbackMultiplier > 0f)
        {
            float fallbackDmg = actor.RollAttackDamage(cfg.EnemyBasicFallbackMultiplier, rng);
            target.TakeDamage(fallbackDmg, rng);
        }
    }

    private static GemType RollWeightedGem(System.Random rng)
    {
        // Tổng trọng số 7: Damage=2, 5 loại còn lại=1
        int roll = rng.Next(0, 7);
        if (roll < 2) return GemType.Damage;
        if (roll == 2) return GemType.Health;
        if (roll == 3) return GemType.Shield;
        if (roll == 4) return GemType.CritRate;
        if (roll == 5) return GemType.CritDamage;
        return GemType.Dodge;
    }

    private static void AccumulateGem(
        GemType type,
        float mult,
        float actorMaxHp,
        ref float dmg,
        ref float heal,
        ref float shield,
        ref float crit,
        ref float cdmg,
        ref float dodge)
    {
        switch (type)
        {
            case GemType.Damage: dmg += 1f * mult; break;
            case GemType.Health: heal += (5f + actorMaxHp * 0.08f) * mult; break;
            case GemType.Shield: shield += (6f + actorMaxHp * 0.10f) * mult; break;
            case GemType.CritRate: crit += 5f * mult; break;
            case GemType.CritDamage: cdmg += 10f * mult; break;
            case GemType.Dodge: dodge += 3f * mult; break;
        }
    }

    private static SimUnit BuildPlayerUnit(CharacterInfoSO baseSO, SimConfig cfg)
    {
        float maxHp = baseSO != null ? baseSO.maxHealth : 150f;
        float shield = baseSO != null ? baseSO.baseShield : 10f;
        float dmg = baseSO != null ? baseSO.baseDamage : 15f;
        float crit = baseSO != null ? baseSO.baseCritRate : 5f;
        float cdmg = baseSO != null ? baseSO.baseCritDamage : 50f;
        float dodge = baseSO != null ? baseSO.baseBlockRate : 5f;

        int extraLv = Math.Max(0, cfg.PlayerLevel - 1);
        maxHp += extraLv * 8f + cfg.DefenseTalentLevel * 14f;
        dmg += extraLv * 1.2f + cfg.AttackTalentLevel * 1.5f;
        shield += extraLv * 2f + cfg.DefenseTalentLevel * 2f;
        crit += cfg.AttackTalentLevel * 0.5f;

        float gemMult = 1f + cfg.GemTalentLevel * 0.05f;
        float startShield = 0f;
        float m4Heal = 0f;

        int[] presetIds = null;
        int presetLv = 1;
        if (cfg.GearPreset == SimGearPreset.StarterIronSword) { presetIds = new[] { 0 }; presetLv = 1; }
        else if (cfg.GearPreset == SimGearPreset.MidGameRareSet) { presetIds = new[] { 0, 2, 5, 7, 8, 10 }; presetLv = 2; }
        else if (cfg.GearPreset == SimGearPreset.FullEpicMaxed) { presetIds = new[] { 1, 3, 5, 7, 9, 11 }; presetLv = 5; }

        if (presetIds != null)
        {
            float scale = MetaProgressionSystem.GetEquipmentLevelScale(presetLv);
            for (int i = 0; i < presetIds.Length; i++)
            {
                EquipmentDefinition eq = MetaProgressionSystem.Equipments[presetIds[i]];
                maxHp += eq.MaxHealth * scale;
                shield += eq.Shield * scale;
                dmg += eq.Damage * scale;
                crit += eq.CritRate * scale;
                cdmg += eq.CritDamage * scale;
                dodge += eq.Dodge * scale;
                startShield += eq.StartBattleShield * scale;
                m4Heal += eq.Match4BonusHeal * scale;
            }
        }

        SimUnit u = new SimUnit
        {
            MaxHp = maxHp,
            Hp = maxHp,
            Shield = Math.Min(maxHp * 0.5f, shield + startShield),
            BaseDamage = dmg,
            BaseCritRate = Math.Min(60f, crit),
            BaseCritDamage = Math.Min(140f, cdmg),
            BaseDodge = Math.Min(35f, dodge),
            GemMultiplier = gemMult,
            Match4BonusHeal = m4Heal
        };
        u.RecalculateBuffs();
        return u;
    }

    private static List<SimUnit> BuildLevelWaveEnemies(
        LevelConfig cfg,
        List<CharacterInfoSO> fallback,
        int wave,
        int maxWaves,
        float hpMult,
        float dmgMult,
        float shdMult,
        System.Random rng)
    {
        List<CharacterInfoSO> pool = (cfg != null && cfg.PossibleEnemies != null && cfg.PossibleEnemies.Count > 0)
            ? cfg.PossibleEnemies
            : fallback;

        int count;
        if (maxWaves == 1) count = (cfg != null && cfg.IsBossLevel) ? 2 : 1;
        else if (maxWaves == 2) count = (wave == 1) ? 1 : ((cfg != null && cfg.IsBossLevel) ? 3 : 2);
        else count = Mathf.Clamp(Mathf.RoundToInt(Mathf.Lerp(1f, 3f, (float)(wave - 1) / (maxWaves - 1))), 1, 3);

        bool isBossWave = cfg != null && cfg.IsBossLevel && wave == maxWaves;
        List<SimUnit> list = new List<SimUnit>();

        if (isBossWave)
        {
            for (int i = 0; i < Math.Max(0, count - 1); i++)
            {
                list.Add(CreateEnemyUnit(pool[rng.Next(0, pool.Count)], hpMult, dmgMult, shdMult, i));
            }
            CharacterInfoSO boss = cfg.BossEnemy != null ? cfg.BossEnemy : pool[pool.Count - 1];
            list.Add(CreateEnemyUnit(boss, hpMult, dmgMult, shdMult, Math.Max(0, count - 1)));
        }
        else
        {
            for (int i = 0; i < count; i++)
            {
                list.Add(CreateEnemyUnit(pool[rng.Next(0, pool.Count)], hpMult, dmgMult, shdMult, i));
            }
        }
        return list;
    }

    private static List<SimUnit> BuildEndlessWaveEnemies(
        List<CharacterInfoSO> sortedPool,
        CharacterInfoSO bossSO,
        int round,
        float hpMult,
        float dmgMult,
        float shdMult,
        System.Random rng)
    {
        List<SimUnit> list = new List<SimUnit>();
        if (sortedPool == null || sortedPool.Count == 0) return list;

        if (round % 5 == 0)
        {
            int minions = Math.Min(2, 1 + round / 10);
            for (int i = 0; i < minions; i++)
                list.Add(CreateEnemyUnit(sortedPool[rng.Next(0, sortedPool.Count)], hpMult, dmgMult, shdMult, i));
            list.Add(CreateEnemyUnit(bossSO != null ? bossSO : sortedPool[sortedPool.Count - 1], hpMult, dmgMult, shdMult, minions));
            return list;
        }

        int count = round <= 1 ? 1 : (round <= 4 ? 2 : Math.Min(3, 1 + round / 3));
        int minIdx = (round >= 8 && sortedPool.Count >= 3) ? 1 : 0;
        int maxExcl = (round <= 2) ? Math.Min(2, sortedPool.Count) : sortedPool.Count;

        for (int i = 0; i < count; i++)
            list.Add(CreateEnemyUnit(sortedPool[rng.Next(minIdx, maxExcl)], hpMult, dmgMult, shdMult, i));

        return list;
    }

    private static SimUnit CreateEnemyUnit(CharacterInfoSO so, float hpMult, float dmgMult, float shdMult, int idx)
    {
        int cd = idx == 0 ? 2 : 2 + (idx - 1);
        SimUnit u = new SimUnit
        {
            MaxHp = so.maxHealth * hpMult,
            Hp = so.maxHealth * hpMult,
            Shield = so.baseShield * shdMult,
            BaseDamage = so.baseDamage * dmgMult,
            BaseCritRate = so.baseCritRate,
            BaseCritDamage = so.baseCritDamage,
            BaseDodge = so.baseBlockRate,
            SupportCooldown = cd,
            MaxSupportCooldown = 2
        };
        u.RecalculateBuffs();
        return u;
    }

    private static string DiagnoseLevel(float winRate, float threeStarRate, float avgTurns)
    {
        if (winRate < 40f) return "Qua kho (can nang Meta hoac giam mult)";
        if (winRate > 97f && threeStarRate > 85f) return "Rat de (tot cho man tutorial)";
        if (winRate >= 55f && winRate <= 92f) return "Can bang tot";
        if (winRate < 55f) return "Hoi cang (khuyen khich nang Talent/Gear)";
        return "Thuan loi (Win Rate cao)";
    }
}
