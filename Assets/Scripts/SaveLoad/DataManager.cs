using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class DataManager : Singleton<DataManager>
{
    public SecureGameData GameData { get; private set; }

    protected override void Awake()
    {
        base.KeepActive(true);
        base.Awake();
        GameData = new SecureGameData();
        GameData.Load();
    }

    private void OnApplicationQuit()
    {
        GameData.Save();
    }

    private void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus && GameData != null)
        {
            GameData.Save();
        }
    }
}

[System.Serializable]
public class SecureGameData
{
    [SerializeField] private float _musicVolume = 0.5f;
    [SerializeField] private float _soundVolume = 0.5f;
    [SerializeField] private bool _vibration = true;

    [Header("Player Progression")]
    [SerializeField] private int _gold = 0;
    [SerializeField] private int _diamond = 0;
    [SerializeField] private int _playerLevel = 1;
    [SerializeField] private int _currentExp = 0;
    [SerializeField] private int _maxUnlockedLevel = 1;
    [SerializeField] private int[] _levelStars = new int[100];

    [Header("Meta Progression (Phase 3)")]
    [SerializeField] private int _bestEndlessRound = 0;
    [SerializeField] private int[] _talentLevels = new int[3];
    [SerializeField] private int[] _ownedEquipmentLevels = new int[12];
    [SerializeField] private int[] _equippedItemIds = new int[6] { -1, -1, -1, -1, -1, -1 };
    [SerializeField] private int _claimedRewardMask = 0;
    [SerializeField] private bool _starterGearGranted = false;

    public const int TALENT_COUNT = 3;
    public const int EQUIPMENT_COUNT = 12;
    public const int EQUIPMENT_SLOT_COUNT = 6;

    #region CONST
    private const float DEFAULT_MUSIC_VOLUME = 0.5f;
    private const float DEFAULT_SOUND_VOLUME = 0.5f;
    private const bool DEFAULT_VIBRATION = true;
    
    private const int DEFAULT_GOLD = 0;
    private const int DEFAULT_DIAMOND = 0;
    private const int DEFAULT_PLAYER_LEVEL = 1;
    private const int DEFAULT_EXP = 0;
    private const int DEFAULT_UNLOCKED_LEVEL = 1;
    #endregion

    #region PROPERTIES
    public float MusicVolume
    {
        get { return _musicVolume; } 
        set { _musicVolume = value; }
    }

    public float SoundVolume
    {
        get { return _soundVolume; }
        set { _soundVolume = value; }
    }

    public bool Vibration
    {
        get { return _vibration; }
        set { _vibration = value; }
    }

    public int Gold { get => _gold; set => _gold = value; }
    public int Diamond { get => _diamond; set => _diamond = value; }
    public int PlayerLevel { get => _playerLevel; set => _playerLevel = value; }
    public int CurrentExp { get => _currentExp; set => _currentExp = value; }
    public int MaxUnlockedLevel { get => _maxUnlockedLevel; set => _maxUnlockedLevel = value; }
    public int BestEndlessRound { get => _bestEndlessRound; set => _bestEndlessRound = value; }

    #endregion

    #region SAVE/LOAD

    private const eData SaveFileKey = eData.SecureGameData;

    public void Save()
    {
        var saveUtil = new SaveUtility<SecureGameData>();
        saveUtil.SaveData(SaveFileKey, this);
    }

    public void Load()
    {
        var saveUtil = new SaveUtility<SecureGameData>();
        SecureGameData loadedData = new SecureGameData();
        saveUtil.LoadData(SaveFileKey, ref loadedData);
        CopyFrom(loadedData);
    }

    private void CopyFrom(SecureGameData other)
    {
        if (other == null) return;

        this._musicVolume = other._musicVolume;
        this._soundVolume = other._soundVolume;
        this._vibration = other._vibration;

        this._gold = other._gold;
        this._diamond = other._diamond;
        this._playerLevel = other._playerLevel;
        this._currentExp = other._currentExp;
        this._maxUnlockedLevel = Mathf.Max(1, other._maxUnlockedLevel);

        if (other._levelStars != null)
        {
            this._levelStars = other._levelStars;
        }

        this._bestEndlessRound = Mathf.Max(0, other._bestEndlessRound);
        this._claimedRewardMask = other._claimedRewardMask;
        this._starterGearGranted = other._starterGearGranted;

        this._talentLevels = NormalizeIntArray(other._talentLevels, TALENT_COUNT, 0);
        this._ownedEquipmentLevels = NormalizeIntArray(other._ownedEquipmentLevels, EQUIPMENT_COUNT, 0);
        this._equippedItemIds = NormalizeIntArray(other._equippedItemIds, EQUIPMENT_SLOT_COUNT, -1);

        EnsureStarterGear();
    }

    private static int[] NormalizeIntArray(int[] source, int targetLength, int defaultValue)
    {
        int[] result = new int[targetLength];
        for (int i = 0; i < targetLength; i++) result[i] = defaultValue;
        if (source != null)
        {
            int len = Mathf.Min(source.Length, targetLength);
            for (int i = 0; i < len; i++) result[i] = source[i];
        }
        return result;
    }

    private void EnsureStarterGear()
    {
        if (!_starterGearGranted)
        {
            _starterGearGranted = true;
            // Tặng sẵn 1 thanh Kiếm Sắt cấp 1 trong kho để người chơi làm quen ngay với tab Trang Bị
            if (_ownedEquipmentLevels[0] <= 0)
            {
                _ownedEquipmentLevels[0] = 1;
            }
        }
    }

    public void ClearAllData()
    {
        SaveGameManager.DeleteSave(SaveFileKey);
        Reset();
    }

    private void Reset()
    {
        _musicVolume = DEFAULT_MUSIC_VOLUME;
        _soundVolume = DEFAULT_SOUND_VOLUME;
        _vibration = DEFAULT_VIBRATION;

        _gold = DEFAULT_GOLD;
        _diamond = DEFAULT_DIAMOND;
        _playerLevel = DEFAULT_PLAYER_LEVEL;
        _currentExp = DEFAULT_EXP;
        _maxUnlockedLevel = DEFAULT_UNLOCKED_LEVEL;
        _levelStars = new int[100];

        _bestEndlessRound = 0;
        _talentLevels = new int[TALENT_COUNT];
        _ownedEquipmentLevels = new int[EQUIPMENT_COUNT];
        _equippedItemIds = new int[EQUIPMENT_SLOT_COUNT] { -1, -1, -1, -1, -1, -1 };
        _claimedRewardMask = 0;
        _starterGearGranted = false;
        EnsureStarterGear();
    }

    #endregion

    #region LOGIC PROGRESSION
    
    public void AddResources(int addGold, int addDiamond, int addExp)
    {
        _gold = Mathf.Max(0, _gold + addGold);
        _diamond = Mathf.Max(0, _diamond + addDiamond);
        _currentExp = Mathf.Max(0, _currentExp + addExp);

        int maxExpForCurrentLevel = _playerLevel * 100;
        while (_currentExp >= maxExpForCurrentLevel)
        {
            _currentExp -= maxExpForCurrentLevel;
            _playerLevel++;
            maxExpForCurrentLevel = _playerLevel * 100;
            Debug.Log($"Level Up! Current Level: {_playerLevel}");
        }

        ObserverManager<EventID>.PostEvent(EventID.OnResourcesChanged);
    }

    public bool TrySpendGold(int amount)
    {
        if (amount < 0 || _gold < amount) return false;
        _gold -= amount;
        Save();
        ObserverManager<EventID>.PostEvent(EventID.OnResourcesChanged);
        return true;
    }

    public bool TrySpendDiamond(int amount)
    {
        if (amount < 0 || _diamond < amount) return false;
        _diamond -= amount;
        Save();
        ObserverManager<EventID>.PostEvent(EventID.OnResourcesChanged);
        return true;
    }

    public void UpdateBestEndlessRound(int clearedRounds)
    {
        if (clearedRounds > _bestEndlessRound)
        {
            _bestEndlessRound = clearedRounds;
        }
    }

    public int GetTotalStars()
    {
        if (_levelStars == null) return 0;
        int total = 0;
        for (int i = 0; i < _levelStars.Length; i++) total += Mathf.Clamp(_levelStars[i], 0, 3);
        return total;
    }

    public int GetTalentLevel(int talentIndex)
    {
        if (_talentLevels == null) _talentLevels = new int[TALENT_COUNT];
        if (talentIndex < 0 || talentIndex >= _talentLevels.Length) return 0;
        return _talentLevels[talentIndex];
    }

    public void SetTalentLevel(int talentIndex, int level)
    {
        if (_talentLevels == null) _talentLevels = new int[TALENT_COUNT];
        if (talentIndex < 0 || talentIndex >= _talentLevels.Length) return;
        _talentLevels[talentIndex] = Mathf.Max(0, level);
        Save();
        ObserverManager<EventID>.PostEvent(EventID.OnMetaProgressionChanged);
    }

    public int GetEquipmentLevel(int itemId)
    {
        if (_ownedEquipmentLevels == null) _ownedEquipmentLevels = new int[EQUIPMENT_COUNT];
        if (itemId < 0 || itemId >= _ownedEquipmentLevels.Length) return 0;
        return _ownedEquipmentLevels[itemId];
    }

    public void GrantOrUpgradeEquipment(int itemId, int maxLevel = 5)
    {
        if (_ownedEquipmentLevels == null) _ownedEquipmentLevels = new int[EQUIPMENT_COUNT];
        if (itemId < 0 || itemId >= _ownedEquipmentLevels.Length) return;
        _ownedEquipmentLevels[itemId] = Mathf.Clamp(_ownedEquipmentLevels[itemId] + 1, 1, maxLevel);
        Save();
        ObserverManager<EventID>.PostEvent(EventID.OnMetaProgressionChanged);
    }

    public int GetEquippedItemId(int slotIndex)
    {
        if (_equippedItemIds == null) _equippedItemIds = NormalizeIntArray(null, EQUIPMENT_SLOT_COUNT, -1);
        if (slotIndex < 0 || slotIndex >= _equippedItemIds.Length) return -1;
        return _equippedItemIds[slotIndex];
    }

    public void SetEquippedItemId(int slotIndex, int itemId)
    {
        if (_equippedItemIds == null) _equippedItemIds = NormalizeIntArray(null, EQUIPMENT_SLOT_COUNT, -1);
        if (slotIndex < 0 || slotIndex >= _equippedItemIds.Length) return;
        _equippedItemIds[slotIndex] = itemId;
        Save();
        ObserverManager<EventID>.PostEvent(EventID.OnMetaProgressionChanged);
    }

    public bool IsRewardClaimed(int milestoneIndex)
    {
        if (milestoneIndex < 0 || milestoneIndex >= 31) return false;
        return (_claimedRewardMask & (1 << milestoneIndex)) != 0;
    }

    public void MarkRewardClaimed(int milestoneIndex)
    {
        if (milestoneIndex < 0 || milestoneIndex >= 31) return;
        _claimedRewardMask |= (1 << milestoneIndex);
        Save();
        ObserverManager<EventID>.PostEvent(EventID.OnResourcesChanged);
    }

    public void UnlockNextLevel(int currentLevelId)
    {
        if (currentLevelId >= _maxUnlockedLevel)
        {
            _maxUnlockedLevel = currentLevelId + 1;
        }
    }

    /// <summary>
    /// Lưu số sao cao nhất cho màn chơi (levelId là 1-based từ LevelConfig.LevelID).
    /// </summary>
    public void SaveLevelStar(int levelId, int starCount)
    {
        if (_levelStars == null)
            _levelStars = new int[100];

        int index = levelId - 1;
        if (index >= 0 && index < _levelStars.Length)
        {
            int clampedStars = Mathf.Clamp(starCount, 0, 3);
            if (clampedStars > _levelStars[index])
            {
                _levelStars[index] = clampedStars;
            }
        }
    }

    /// <summary>
    /// Lấy số sao đã đạt của màn chơi (levelIndex là 0-based từ LevelUI).
    /// </summary>
    public int GetLevelStar(int levelIndex)
    {
        if (_levelStars != null && levelIndex >= 0 && levelIndex < _levelStars.Length)
        {
            return _levelStars[levelIndex];
        }
        return 0;
    }

    #endregion
}
