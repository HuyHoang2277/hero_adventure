using DG.Tweening;
using UnityEngine;
using UnityEngine.Audio;

public class AudioManager : Singleton<AudioManager>
{
    [Header("Audio Sources")]
    [SerializeField] private AudioSource _musicSource;
    public AudioSource MusicSource => _musicSource;

    [SerializeField] private AudioSource _soundSource;
    public AudioSource SoundSource => _soundSource;

    [Header("Optional Audio Mixer (Exposed: MusicVol, SFXVol)")]
    [SerializeField] private AudioMixer _mainMixer;
    [SerializeField] private string _musicVolParam = "MusicVol";
    [SerializeField] private string _sfxVolParam = "SFXVol";

    [Header("Music")]
    [SerializeField] private AudioClip _musicIntro;
    [SerializeField] private AudioClip _musicInMenu;
    [SerializeField] private AudioClip _musicBattleNormal;
    [SerializeField] private AudioClip _musicBattleBoss;
    [SerializeField] private AudioClip _musicExplore;

    [Header("General UI")]
    [SerializeField] private AudioClip _buttonClick;
    [SerializeField] private AudioClip _winSound;
    [SerializeField] private AudioClip _loseSound;
    [SerializeField] private AudioClip _popupOpen;
    [SerializeField] private AudioClip _popupClose;

    [Header("Grid & Puzzle SFX")]
    [SerializeField] private AudioClip _gemSelect;
    [SerializeField] private AudioClip _gemDrag;
    [SerializeField] private AudioClip _gemMatchBase;

    [Header("Combat SFX")]
    [SerializeField] private AudioClip _playerFootstep;
    [SerializeField] private AudioClip _attackSwing;
    [SerializeField] private AudioClip _attackCritSwing;
    [SerializeField] private AudioClip _attackHit;
    [SerializeField] private AudioClip _playerHurt;
    [SerializeField] private AudioClip _enemyHurt;
    [SerializeField] private AudioClip _playerBlock;
    [SerializeField] private AudioClip _enemyBlock;
    [SerializeField] private AudioClip _playerDie;
    [SerializeField] private AudioClip _enemyDie;

    [Header("System & Targets")]
    [SerializeField] private AudioClip _roundStart;
    [SerializeField] private AudioClip _targetSelected;
    [SerializeField] private AudioClip _waveClear;

    private const int SFX_POOL_SIZE = 4;
    private AudioSource[] _sfxPool;
    private int _sfxPoolCursor = 0;
    private AudioSource _loopSource;

    private float _musicGain = 0.5f;
    private float _sfxGain = 0.5f;
    private float _duckMultiplier = 1f;
    private bool _isExploreDucked = false;

    protected override void Awake()
    {
        base.KeepActive(true);
        base.Awake();

        if (_musicSource == null)
        {
            _musicSource = gameObject.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
        }
        if (_soundSource == null)
        {
            _soundSource = gameObject.AddComponent<AudioSource>();
            _soundSource.playOnAwake = false;
        }

        InitializeAudioChannels();
    }

    private void OnEnable()
    {
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnPause, HandlePause);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnResume, HandleResume);
    }

    private void OnDisable()
    {
        ObserverManager<EventID>.RemoveAddListener(EventID.OnPause, HandlePause);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnResume, HandleResume);
    }

    private void InitializeAudioChannels()
    {
        // Tách kênh loop (bước chân) độc lập để StopSFX không bao giờ cắt cụt tiếng ngọc/UI
        if (_loopSource == null)
        {
            _loopSource = gameObject.AddComponent<AudioSource>();
            _loopSource.playOnAwake = false;
            _loopSource.loop = true;
            if (_soundSource != null) _loopSource.outputAudioMixerGroup = _soundSource.outputAudioMixerGroup;
        }

        // Pool 4 kênh SFX riêng giúp mỗi cú nổ combo giữ đúng cao độ tăng dần (pitch) không bị reset đè
        if (_sfxPool == null)
        {
            _sfxPool = new AudioSource[SFX_POOL_SIZE];
            for (int i = 0; i < SFX_POOL_SIZE; i++)
            {
                AudioSource voice = gameObject.AddComponent<AudioSource>();
                voice.playOnAwake = false;
                voice.loop = false;
                if (_soundSource != null) voice.outputAudioMixerGroup = _soundSource.outputAudioMixerGroup;
                _sfxPool[i] = voice;
            }
        }
    }

    private void Start()
    {
        if (DataManager.Instance != null && DataManager.Instance.GameData != null)
        {
            SetMusicVolume(DataManager.Instance.GameData.MusicVolume);
            SetSoundVolume(DataManager.Instance.GameData.SoundVolume);
        }
        else
        {
            SetMusicVolume(0.5f);
            SetSoundVolume(0.5f);
        }
        PlayMusicIntro();
    }

    private void HandlePause(object param)
    {
        SetPauseDucking(true);
    }

    private void HandleResume(object param)
    {
        SetPauseDucking(false);
    }

    /// <summary>
    /// Giảm nhẹ âm lượng nhạc nền (-6 dB ≈ 0.5x biên độ) khi mở Pause Popup trên unscaled time.
    /// Không ghi đè tham số MusicVol của Slider trong Settings.
    /// </summary>
    public void SetPauseDucking(bool paused)
    {
        _duckMultiplier = paused ? 0.5f : 1f;
        ApplyEffectiveMusicVolume(0.2f);
    }

    private float GetTargetMusicSourceVolume()
    {
        bool usesMixer = HasExposedMixerParam(_musicVolParam);
        float baseAmp = usesMixer ? 1f : _musicGain;
        float exploreFactor = _isExploreDucked ? 0.6f : 1f;
        return baseAmp * exploreFactor * _duckMultiplier;
    }

    private void ApplyEffectiveMusicVolume(float fadeDuration)
    {
        if (_musicSource == null) return;
        float target = GetTargetMusicSourceVolume();
        _musicSource.DOKill();
        if (fadeDuration > 0f && _musicSource.isPlaying)
        {
            _musicSource.DOFade(target, fadeDuration).SetUpdate(true);
        }
        else
        {
            _musicSource.volume = target;
        }
    }

    private bool HasExposedMixerParam(string paramName)
    {
        return _mainMixer != null && !string.IsNullOrEmpty(paramName) && _mainMixer.GetFloat(paramName, out _);
    }

    private static float LinearToDecibel(float linear)
    {
        return Mathf.Log10(Mathf.Clamp(linear, 0.0001f, 1f)) * 20f;
    }

    public void SetMusicVolume(float volume)
    {
        _musicGain = Mathf.Clamp01(volume);

        if (HasExposedMixerParam(_musicVolParam))
        {
            _mainMixer.SetFloat(_musicVolParam, LinearToDecibel(_musicGain));
        }

        ApplyEffectiveMusicVolume(0f);

        if (DataManager.Instance != null && DataManager.Instance.GameData != null)
        {
            DataManager.Instance.GameData.MusicVolume = _musicGain;
        }
    }

    public void SetSoundVolume(float volume)
    {
        _sfxGain = Mathf.Clamp01(volume);

        bool usesMixer = HasExposedMixerParam(_sfxVolParam);
        if (usesMixer)
        {
            _mainMixer.SetFloat(_sfxVolParam, LinearToDecibel(_sfxGain));
        }

        float sourceVol = usesMixer ? 1f : _sfxGain;
        if (_soundSource != null) _soundSource.volume = sourceVol;
        if (_loopSource != null) _loopSource.volume = sourceVol;
        if (_sfxPool != null)
        {
            for (int i = 0; i < _sfxPool.Length; i++)
            {
                if (_sfxPool[i] != null) _sfxPool[i].volume = sourceVol;
            }
        }

        if (DataManager.Instance != null && DataManager.Instance.GameData != null)
        {
            DataManager.Instance.GameData.SoundVolume = _sfxGain;
        }
    }

    public void PlayMusicIntro()
    {
        _duckMultiplier = 1f;
        _isExploreDucked = false;
        if (_musicIntro != null) PlayMusicGame(_musicIntro);
    }

    public void PlayMusicInMenu()
    {
        _duckMultiplier = 1f;
        _isExploreDucked = false;
        if (_musicInMenu != null) PlayMusicGame(_musicInMenu);
    }

    public void PlayMusicBattleNormal()
    {
        _duckMultiplier = 1f;
        _isExploreDucked = false;
        if (_musicBattleNormal != null) PlayMusicGame(_musicBattleNormal);
    }

    public void PlayMusicBattleBoss()
    {
        _duckMultiplier = 1f;
        _isExploreDucked = false;
        if (_musicBattleBoss != null) PlayMusicGame(_musicBattleBoss);
    }

    public void PlayMusicExplore()
    {
        _duckMultiplier = 1f;
        if (_musicExplore != null)
        {
            _isExploreDucked = false;
            PlayMusicGame(_musicExplore);
        }
        else if (_musicBattleNormal != null)
        {
            _isExploreDucked = true;
            PlayMusicGame(_musicBattleNormal, false);
        }
    }

    public void PlayMusicGame(AudioClip clip, bool fullVolume = true)
    {
        if (_musicSource == null || clip == null) return;

        _isExploreDucked = !fullVolume;

        if (_musicSource.isPlaying && _musicSource.clip == clip)
        {
            ApplyEffectiveMusicVolume(0.4f);
            return;
        }

        _musicSource.DOKill();
        _musicSource.loop = true;
        _musicSource.clip = clip;
        _musicSource.volume = 0f;
        _musicSource.Play();
        _musicSource.DOFade(GetTargetMusicSourceVolume(), 0.45f).SetUpdate(true);
    }

    public void StopMusic()
    {
        if (_musicSource != null && _musicSource.isPlaying)
        {
            _musicSource.DOKill();
            _musicSource.DOFade(0f, 0.45f).OnComplete(() =>
            {
                _musicSource.Stop();
            }).SetUpdate(true);
        }
    }

    private AudioSource GetNextSfxVoice()
    {
        if (_sfxPool == null || _sfxPool.Length == 0)
        {
            InitializeAudioChannels();
        }
        AudioSource voice = _sfxPool[_sfxPoolCursor];
        _sfxPoolCursor = (_sfxPoolCursor + 1) % _sfxPool.Length;
        return voice != null ? voice : _soundSource;
    }

    public void PlaySFX(AudioClip sound, bool repeat = false)
    {
        if (sound == null) return;

        if (repeat)
        {
            if (_loopSource == null) InitializeAudioChannels();
            if (_loopSource == null) return;

            _loopSource.loop = true;
            _loopSource.clip = sound;
            _loopSource.pitch = 1f;
            _loopSource.volume = HasExposedMixerParam(_sfxVolParam) ? 1f : _sfxGain;
            if (!_loopSource.isPlaying) _loopSource.Play();
        }
        else
        {
            AudioSource voice = GetNextSfxVoice();
            if (voice == null) return;

            voice.pitch = 1f;
            voice.PlayOneShot(sound, voice.volume);
        }
    }

    public void PlaySFXWithPitch(AudioClip sound, float minPitch = 0.92f, float maxPitch = 1.08f)
    {
        if (sound == null) return;

        AudioSource voice = GetNextSfxVoice();
        if (voice == null) return;

        voice.pitch = Random.Range(minPitch, maxPitch);
        voice.PlayOneShot(sound, voice.volume);
    }

    public void StopSFX()
    {
        if (_loopSource != null && _loopSource.isPlaying)
        {
            _loopSource.Stop();
            _loopSource.clip = null;
        }
    }

    #region Specific Game Play Methods

    public void PlaySoundButtonClick() => PlaySFX(_buttonClick);
    public void PlaySoundClick() => PlaySFX(_buttonClick);
    public void PlaySoundWin() => PlaySFX(_winSound);
    public void PlaySoundLose() => PlaySFX(_loseSound);
    public void PlayPopupOpen() => PlaySFX(_popupOpen);
    public void PlayPopupClose() => PlaySFX(_popupClose);

    public void PlayGemSelect() => PlaySFX(_gemSelect);
    public void PlayGemDrag() => PlaySFXWithPitch(_gemDrag, 0.92f, 1.08f);

    /// <summary>
    /// Phát âm thanh ăn ngọc trên voice độc lập với cao độ tăng dần theo kích thước cụm & nhịp cascade (không bị ResetPitch đè).
    /// </summary>
    public void PlayGemMatch(int matchCount, int cascadeStep = 1)
    {
        if (_gemMatchBase == null) return;

        AudioSource voice = GetNextSfxVoice();
        if (voice == null) return;

        float pitch = Mathf.Clamp(
            1.0f + Mathf.Max(0, matchCount - 3) * 0.08f + Mathf.Max(0, cascadeStep - 1) * 0.12f,
            1.0f,
            1.65f);
        voice.pitch = pitch;
        voice.PlayOneShot(_gemMatchBase, voice.volume);
    }

    public void PlayPlayerFootstep(bool play)
    {
        if (play) PlaySFX(_playerFootstep, true);
        else StopSFX();
    }

    public void PlayAttackSwing() => PlaySFXWithPitch(_attackSwing);
    public void PlayAttackCritSwing() => PlaySFXWithPitch(_attackCritSwing, 0.95f, 1.12f);
    public void PlayAttackHit() => PlaySFXWithPitch(_attackHit);
    public void PlayPlayerHurt() => PlaySFXWithPitch(_playerHurt);
    public void PlayEnemyHurt() => PlaySFXWithPitch(_enemyHurt);
    public void PlayPlayerBlock() => PlaySFXWithPitch(_playerBlock);
    public void PlayEnemyBlock() => PlaySFXWithPitch(_enemyBlock);
    public void PlayPlayerDie() => PlaySFX(_playerDie);
    public void PlayEnemyDie() => PlaySFX(_enemyDie);
    public void PlayRoundStart() => PlaySFX(_roundStart);

    public void PlayTargetSelected() => PlaySFX(_targetSelected);
    public void PlayWaveClear() => PlaySFX(_waveClear);

    #endregion
}
