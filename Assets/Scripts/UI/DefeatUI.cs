using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

public class DefeatUI : MonoBehaviour
{
    [Header("Popup Root")]
    [Tooltip("Transform của DefeatPopup (con trực tiếp của panel, sẽ scale-in khi mở).")]
    [SerializeField] private Transform defeatPopup;
    [SerializeField] private Image dime;

    [Header("Animated Elements")]
    [Tooltip("Image chữ DEFEAT ở đầu popup.")]
    [SerializeField] private CanvasGroup defeatImg;

    [Tooltip("Image thanh gươm gãy.")]
    [SerializeField] private Transform swordBrokenImg;

    [Header("Rewards")]
    [Tooltip("Prefab của RewardItem để khởi tạo động.")]
    [SerializeField] private RewardItem rewardPrefab;
    [Tooltip("Container chứa các RewardItem.")]
    [SerializeField] private Transform rewardContainer;
    
    [Header("Reward Sprites")]
    [SerializeField] private Sprite goldSprite;
    [SerializeField] private Sprite diamondSprite;
    [SerializeField] private Sprite expSprite;

    [Header("Buttons")]
    [Tooltip("Danh sách Button hiện sau khi xong rewards (theo thứ tự hiện dần).")]
    [SerializeField] private List<Button> buttons;

    [Header("Button Actions")]
    [Tooltip("Button Retry — reload lại trậy.")]
    [SerializeField] private Button retryButton;
    [Tooltip("Button Return to Menu.")]
    [SerializeField] private Button mainMenuButton;

    [Header("Timing (seconds)")]
    [SerializeField] private float popupScaleDuration = 0.45f;
    [SerializeField] private float defeatImgDuration = 0.5f;
    [SerializeField] private float swordImgDuration = 0.4f;
    [SerializeField] private float rewardStagger = 0.25f;
    [SerializeField] private float buttonStagger = 0.15f;
    [SerializeField] private float gapAfterTitle = 0.2f;
    [SerializeField] private float gapAfterSword = 0.3f;
    [SerializeField] private float gapAfterRewards = 0.2f;

    [Header("Endless Base Rewards")]
    [SerializeField] private int baseGoldEndless = 30;
    [SerializeField] private int baseDiamondEndless = 0;
    [SerializeField] private int baseExpEndless = 20;

    private Coroutine _showCoroutine;
    private bool _isAnimating = false;
    private bool _skipRequested = false;
    private float _sequenceStartRealtime = 0f;


    private void Awake()
    {
        if (retryButton != null) retryButton.onClick.AddListener(OnRetryClicked);
        if (mainMenuButton != null) mainMenuButton.onClick.AddListener(OnMainMenuClicked);
    }

    private void Update()
    {
        if (_isAnimating && !_skipRequested && Time.realtimeSinceStartup - _sequenceStartRealtime > 0.25f)
        {
            if (InputHelper.GetPointerDown())
            {
                _skipRequested = true;
            }
        }
    }

    private IEnumerator WaitTweenOrSkip(Tween tween)
    {
        if (tween == null) yield break;
        if (_skipRequested)
        {
            tween.Complete(true);
            yield break;
        }
        while (tween.IsActive() && !tween.IsComplete())
        {
            if (_skipRequested)
            {
                tween.Complete(true);
                yield break;
            }
            yield return null;
        }
    }

    private IEnumerator WaitRealtimeOrSkip(float duration)
    {
        if (_skipRequested || duration <= 0f) yield break;
        float end = Time.realtimeSinceStartup + duration;
        while (Time.realtimeSinceStartup < end)
        {
            if (_skipRequested) yield break;
            yield return null;
        }
    }

    private void OnDestroy()
    {
        if (retryButton != null) retryButton.onClick.RemoveAllListeners();
        if (mainMenuButton != null) mainMenuButton.onClick.RemoveAllListeners();
    }

    public void Show(List<int> rewardOverrides = null)
    {
        if (AudioManager.Instance != null) AudioManager.Instance.PlaySoundLose();
        _skipRequested = false;
        _isAnimating = true;
        _sequenceStartRealtime = Time.realtimeSinceStartup;
        dime.gameObject.SetActive(true);
        PrepareInitialState();

        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        _showCoroutine = StartCoroutine(ShowSequence(rewardOverrides));
    }

    public void Hide()
    {
        _isAnimating = false;
        _skipRequested = false;
        if (_showCoroutine != null) StopCoroutine(_showCoroutine);
        dime.gameObject.SetActive(false);
        if (defeatPopup != null) defeatPopup.gameObject.SetActive(false);
    }

    private void PrepareInitialState()
    {
        if (defeatPopup != null)
        {
            defeatPopup.gameObject.SetActive(true);
            defeatPopup.localScale = Vector3.zero;
        }

        if (defeatImg != null) { defeatImg.alpha = 0f; defeatImg.gameObject.SetActive(false); }

        if (swordBrokenImg != null) { swordBrokenImg.localScale = Vector3.zero; swordBrokenImg.gameObject.SetActive(false); }

        if (rewardContainer != null)
        {
            foreach (Transform child in rewardContainer)
            {
                Destroy(child.gameObject);
            }
        }

        if (buttons != null)
        {
            foreach (var btn in buttons)
            {
                if (btn == null) continue;
                CanvasGroup cg = GetOrAddCanvasGroup(btn.gameObject);
                cg.alpha = 0f;
                cg.interactable = false;
                cg.blocksRaycasts = false;
                btn.gameObject.SetActive(true);
            }
        }
    }

    private IEnumerator ShowSequence(List<int> rewardOverrides)
    {
        if (defeatPopup != null)
        {
            Tween t = defeatPopup
                .DOScale(1f, popupScaleDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            yield return WaitTweenOrSkip(t);
        }

        yield return WaitRealtimeOrSkip(gapAfterTitle * 0.5f);

        if (defeatImg != null)
        {
            defeatImg.gameObject.SetActive(true);
            defeatImg.transform.localScale = Vector3.one * 1.3f;

            Sequence seq = DOTween.Sequence()
                .Append(defeatImg.DOFade(1f, defeatImgDuration * 0.5f))
                .Join(defeatImg.transform.DOScale(1f, defeatImgDuration).SetEase(Ease.OutElastic))
                .SetUpdate(true);
            yield return WaitTweenOrSkip(seq);
        }

        yield return WaitRealtimeOrSkip(gapAfterTitle);

        if (swordBrokenImg != null && !_skipRequested)
        {
            swordBrokenImg.gameObject.SetActive(true);
            Tween t = swordBrokenImg
                .DOScale(1f, swordImgDuration)
                .SetEase(Ease.OutBack)
                .SetUpdate(true);
            yield return WaitTweenOrSkip(t);
        }

        yield return WaitRealtimeOrSkip(gapAfterSword);
        
        if (swordBrokenImg != null) swordBrokenImg.gameObject.SetActive(false);

        List<int> amounts = rewardOverrides ?? BuildRewardAmounts();
        
        if (DataManager.Instance != null && amounts.Count >= 3)
        {
            DataManager.Instance.GameData.AddResources(amounts[0], amounts[1], amounts[2]);
            DataManager.Instance.GameData.Save();
        }

        if (rewardPrefab != null && rewardContainer != null && amounts != null)
        {
            Sprite[] sprites = { goldSprite, diamondSprite, expSprite };

            for (int i = 0; i < amounts.Count; i++)
            {
                int amount = amounts[i];
                if (amount <= 0) continue;

                RewardItem newReward = Instantiate(rewardPrefab, rewardContainer);
                Sprite icon = (i < sprites.Length) ? sprites[i] : null;
                newReward.Init(icon, amount);
                newReward.PlayShowAnimation(_skipRequested ? 0.05f : 0.35f);

                yield return WaitRealtimeOrSkip(rewardStagger);
            }
        }

        yield return WaitRealtimeOrSkip(gapAfterRewards);

        if (buttons != null)
        {
            foreach (var btn in buttons)
            {
                if (btn == null) continue;

                CanvasGroup cg = GetOrAddCanvasGroup(btn.gameObject);
                Tween t = cg.DOFade(1f, 0.25f)
                    .SetUpdate(true)
                    .OnComplete(() =>
                    {
                        cg.interactable = true;
                        cg.blocksRaycasts = true;
                    });
                yield return WaitTweenOrSkip(t);
                cg.alpha = 1f;
                cg.interactable = true;
                cg.blocksRaycasts = true;

                yield return WaitRealtimeOrSkip(buttonStagger);
            }
        }

        _isAnimating = false;
    }

    private List<int> BuildRewardAmounts()
    {
        bool isLevel = GameModeManager.Instance != null &&
                       GameModeManager.Instance.CurrentMode == GameModeType.Level;

        IGameModeStrategy strategy = BattleManager.Instance?.CurrentStrategy;
        int completedWaves = strategy != null ? strategy.GetCompletedWaves() : 0;

        if (isLevel)
        {
            // Thua ở Wave 1 nhận 0 thưởng (chặn exploit tự sát cày tài nguyên) và không rơi Kim Cương khi thua
            if (completedWaves <= 0)
            {
                return new List<int> { 0, 0, 0 };
            }

            LevelConfig cfg = GameModeManager.Instance?.CurrentLevelConfig;
            int totalWaves = strategy != null ? strategy.GetTotalWaves() : Mathf.Max(1, cfg?.MaxWaves ?? 3);
            float progressRatio = Mathf.Clamp01((float)completedWaves / totalWaves) * 0.30f;

            return new List<int>
            {
                Mathf.RoundToInt((cfg?.GoldReward ?? 0) * progressRatio),
                0,
                Mathf.RoundToInt((cfg?.ExpReward ?? 0) * progressRatio)
            };
        }
        else
        {
            int clearedRounds = Mathf.Max(0, GetEndlessCurrentRound() - 1);
            if (DataManager.Instance != null && DataManager.Instance.GameData != null)
            {
                DataManager.Instance.GameData.UpdateBestEndlessRound(clearedRounds);
            }

            // Thua ở Round 1 (chưa vượt qua Round nào) nhận 0 thưởng; thưởng lũy tiến nhẹ + 1 Kim Cương mỗi 5 Round
            if (clearedRounds <= 0)
            {
                return new List<int> { 0, 0, 0 };
            }

            int gold = Mathf.RoundToInt(Mathf.Max(22, baseGoldEndless * 0.75f) * clearedRounds * (1f + clearedRounds * 0.08f));
            int diamondPer5Rounds = Mathf.Max(1, baseDiamondEndless);
            int diamond = (clearedRounds / 5) * diamondPer5Rounds;
            int exp = Mathf.RoundToInt(Mathf.Max(18, baseExpEndless * 0.9f) * clearedRounds * (1f + clearedRounds * 0.06f));

            return new List<int> { gold, diamond, exp };
        }
    }

    private int GetEndlessCurrentRound()
    {
        var strategy = BattleManager.Instance?.CurrentStrategy as EndlessModeStrategy;
        return strategy?.CurrentRound ?? 1;
    }

    private static CanvasGroup GetOrAddCanvasGroup(GameObject go)
    {
        var cg = go.GetComponent<CanvasGroup>();
        if (cg == null) cg = go.AddComponent<CanvasGroup>();
        return cg;
    }

    private void OnRetryClicked()
    {
        AudioManager.Instance?.PlaySoundButtonClick();
        if (BattleManager.Instance != null) BattleManager.Instance.ApplyCurrentTimeScale();
        else Time.timeScale = 1f;
        Hide();
        BattleManager.Instance?.InitBattle();
    }

    private void OnMainMenuClicked()
    {
        AudioManager.Instance?.PlaySoundButtonClick();
        Time.timeScale = 1f;
        Hide();
        GameSceneManager.Instance?.ShowMainMenu();
    }
}
