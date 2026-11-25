using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Pool;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using DG.Tweening;

public enum FloatingPopupType
{
    NormalDamage = 0,
    CriticalDamage = 1,
    Heal = 2,
    Shield = 3,
    Dodge = 4,
    Blocked = 5,
    Buff = 6
}

public struct DamagePopupData
{
    public Vector3 Position;
    public int Damage;
    public bool IsCritical;
    public FloatingPopupType PopupType;
    public string CustomText;
}

public class GameplayUIManager : MonoBehaviour
{
    [Header("Player Stats UI")]
    [SerializeField] private TextMeshProUGUI hpText;
    [SerializeField] private TextMeshProUGUI shieldText;
    [SerializeField] private TextMeshProUGUI damageText;
    [SerializeField] private TextMeshProUGUI critRateText;
    [SerializeField] private TextMeshProUGUI critDamageText;
    [SerializeField] private TextMeshProUGUI blockRateText;

    [Header("Player Bars (Optional)")]
    [SerializeField] private UnityEngine.UI.Image playerHpFill;
    [SerializeField] private UnityEngine.UI.Image playerShieldFill;

    [Header("Speed & Event Banner (Optional)")]
    [SerializeField] private UnityEngine.UI.Button speedToggleButton;
    [SerializeField] private TextMeshProUGUI speedButtonText;
    [SerializeField] private UnityEngine.UI.Image speedButtonBg;
    [SerializeField] private CanvasGroup notificationBannerGroup;
    [SerializeField] private UnityEngine.UI.Image notificationBannerBg;
    [SerializeField] private TextMeshProUGUI notificationBannerText;

    public TMP_FontAsset DefaultFont => hpText != null ? hpText.font : TMP_Settings.defaultFontAsset;

    private Enemy currentShownEnemy;
    private Coroutine notificationBannerCoroutine;
    private ObjectPool<GameObject> popupPool;
    private readonly List<GameObject> activePopups = new List<GameObject>();
    private int popupStaggerCounter = 0;

    // Combat Clarity & Quick Guide runtime UI
    private TextMeshProUGUI turnPhaseTitleText;
    private TextMeshProUGUI turnPhaseStateText;
    private CanvasGroup targetPromptGroup;
    private Tween targetPromptPulseTween;
    private UnityEngine.UI.Button quickGuideButton;
    private GameObject quickGuideModalObj;

    private static readonly Color SpeedNormalBgColor = new Color(0.18f, 0.22f, 0.30f, 0.94f);
    private static readonly Color SpeedFastBgColor = new Color(0.92f, 0.56f, 0.08f, 0.96f);


    [Header("Turn & Round UI")]
    [SerializeField] private TextMeshProUGUI roundText;
    [SerializeField] private GameObject[] turnIcons;

    [Header("Enemy Info Panel")]
    [SerializeField] private GameObject enemyInfoPanel;
    [SerializeField] private Image enemySprite;
    [SerializeField] private Image enemyHpFill;
    [SerializeField] private TextMeshProUGUI enemyHpText;

    [Header("Damage Popups")]
    [SerializeField] private GameObject damagePopupObj;
    [SerializeField] private TextMeshProUGUI damagePopupText;

    [Header("Pause Popup")]
    [SerializeField] private PausePopup pausePopup;
    [SerializeField] private Button pauseButton;

    [Header("Victory Panel")]
    [SerializeField] private VictoryUI victoryUI;

    [Header("Game Over Panel")]
    [SerializeField] private DefeatUI defeatUI;

    private void OnEnable()
    {
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnUpdatePlayerStats, UpdatePlayerStats);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnUpdateRoundCount, UpdateRoundCount);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnShowDamagePopup, HandleShowDamagePopup);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnPlayerTurnStart, HandlePlayerTurnStart);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnEnemyTurnStart, HandleEnemyTurnStart);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnEnemyTargetSelectionRequired, HandleTargetSelectionRequired);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnEnemyTargetSelected, HandleTargetSelected);
    }

    private void OnDisable()
    {
        ObserverManager<EventID>.RemoveAddListener(EventID.OnUpdatePlayerStats, UpdatePlayerStats);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnUpdateRoundCount, UpdateRoundCount);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnShowDamagePopup, HandleShowDamagePopup);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnPlayerTurnStart, HandlePlayerTurnStart);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnEnemyTurnStart, HandleEnemyTurnStart);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnEnemyTargetSelectionRequired, HandleTargetSelectionRequired);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnEnemyTargetSelected, HandleTargetSelected);
    }

    public void Init()
    {
        currentShownEnemy = null;
        if (enemyInfoPanel != null) enemyInfoPanel.SetActive(false);
        if (damagePopupObj != null) damagePopupObj.SetActive(false);
        if (victoryUI != null) victoryUI.Hide();
        if (defeatUI != null) defeatUI.Hide();

        if (pausePopup != null) pausePopup.Init();
        
        if (pauseButton != null)
        {
            pauseButton.onClick.RemoveListener(OnPauseClicked);
            pauseButton.onClick.AddListener(OnPauseClicked);
        }

        EnsureSpeedToggleButton();
        EnsureQuickGuideButton();
        EnsureNotificationBanner();
        EnsureTurnPhaseStatusWidget();
        EnsureTargetSelectionPrompt();
        HideTargetSelectionPrompt();
        CloseQuickGuideModal();

        if (speedToggleButton != null)
        {
            speedToggleButton.onClick.RemoveListener(OnSpeedToggleClicked);
            speedToggleButton.onClick.AddListener(OnSpeedToggleClicked);
        }

        if (quickGuideButton != null)
        {
            quickGuideButton.onClick.RemoveListener(ToggleQuickGuideModal);
            quickGuideButton.onClick.AddListener(ToggleQuickGuideModal);
        }

        float speed = BattleManager.Instance != null ? BattleManager.Instance.CurrentBattleSpeed : 1f;
        UpdateSpeedButtonUI(speed);

        if (notificationBannerGroup != null)
        {
            if (notificationBannerCoroutine != null)
            {
                StopCoroutine(notificationBannerCoroutine);
                notificationBannerCoroutine = null;
            }
            notificationBannerGroup.DOKill();
            notificationBannerGroup.alpha = 0f;
            notificationBannerGroup.gameObject.SetActive(false);
        }
    }

    public void CleanupUI()
    {
        currentShownEnemy = null;

        if (_gameOverCoroutine != null)
        {
            StopCoroutine(_gameOverCoroutine);
            _gameOverCoroutine = null;
        }

        if (notificationBannerCoroutine != null)
        {
            StopCoroutine(notificationBannerCoroutine);
            notificationBannerCoroutine = null;
        }

        if (notificationBannerGroup != null)
        {
            notificationBannerGroup.DOKill();
            notificationBannerGroup.alpha = 0f;
            notificationBannerGroup.gameObject.SetActive(false);
        }

        HideEnemyInfo();
        HideTargetSelectionPrompt();
        CloseQuickGuideModal();
        ReleaseAllActivePopups();
        if (damagePopupObj != null)
        {
            damagePopupObj.transform.DOKill();
            damagePopupObj.SetActive(false);
        }
        if (victoryUI != null) victoryUI.Hide();
        if (defeatUI != null) defeatUI.Hide();
    }

    private void OnDestroy()
    {
        if (pauseButton != null) pauseButton.onClick.RemoveAllListeners();
        if (speedToggleButton != null) speedToggleButton.onClick.RemoveAllListeners();
        if (quickGuideButton != null) quickGuideButton.onClick.RemoveAllListeners();
        if (notificationBannerGroup != null) notificationBannerGroup.DOKill();
        if (targetPromptPulseTween != null) targetPromptPulseTween.Kill();
        if (playerHpFill != null) playerHpFill.DOKill();
        if (playerShieldFill != null) playerShieldFill.DOKill();
    }

    private void OnPauseClicked()
    {
        AudioManager.Instance?.PlaySoundButtonClick();
        ObserverManager<EventID>.PostEvent(EventID.OnPause);
    }

    private void OnSpeedToggleClicked()
    {
        AudioManager.Instance?.PlaySoundButtonClick();
        BattleManager.Instance?.ToggleBattleSpeed();
    }

    public void UpdateSpeedButtonUI(float speed)
    {
        bool isFast = speed > 1.5f;
        if (speedButtonText != null)
        {
            speedButtonText.text = isFast ? "2x" : "1x";
        }
        if (speedButtonBg != null)
        {
            speedButtonBg.color = isFast ? SpeedFastBgColor : SpeedNormalBgColor;
        }
    }

    public void ShowNotificationBanner(string message, Color backgroundColor, float holdSeconds = 1.4f)
    {
        EnsureNotificationBanner();
        if (notificationBannerGroup == null || notificationBannerText == null) return;

        if (notificationBannerCoroutine != null)
        {
            StopCoroutine(notificationBannerCoroutine);
        }
        notificationBannerCoroutine = StartCoroutine(ShowNotificationBannerRoutine(message, backgroundColor, holdSeconds));
    }

    private IEnumerator ShowNotificationBannerRoutine(string message, Color backgroundColor, float holdSeconds)
    {
        notificationBannerGroup.gameObject.SetActive(true);
        notificationBannerGroup.DOKill();
        notificationBannerGroup.transform.DOKill();

        if (notificationBannerBg != null) notificationBannerBg.color = backgroundColor;
        notificationBannerText.text = message;

        notificationBannerGroup.alpha = 0f;
        notificationBannerGroup.transform.localScale = Vector3.one * 0.85f;

        notificationBannerGroup.DOFade(1f, 0.22f).SetUpdate(true);
        notificationBannerGroup.transform.DOScale(1f, 0.25f).SetEase(Ease.OutBack).SetUpdate(true);

        yield return new WaitForSecondsRealtime(holdSeconds);

        yield return notificationBannerGroup.DOFade(0f, 0.25f).SetUpdate(true).WaitForCompletion();
        notificationBannerGroup.gameObject.SetActive(false);
        notificationBannerCoroutine = null;
    }

    private void EnsureSpeedToggleButton()
    {
        if (speedToggleButton != null || pauseButton == null || pauseButton.transform.parent == null) return;

        Transform topBarParent = pauseButton.transform.parent;
        Transform existing = topBarParent.Find("SpeedToggleButton");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        RectTransform pauseRect = pauseButton.GetComponent<RectTransform>();
        float pauseWidth = pauseRect != null && pauseRect.rect.width > 10f ? pauseRect.rect.width : 76f;
        float pauseHeight = pauseRect != null && pauseRect.rect.height > 10f ? pauseRect.rect.height : 74f;
        float rightEdgePadding = pauseRect != null ? Mathf.Abs(pauseRect.anchoredPosition.x) : 18f;
        if (rightEdgePadding < 10f) rightEdgePadding = 18f;

        // Framed antique-gold button matching PauseBtn height & vertical padding
        RectTransform cardRt = MetaUIHelper.CreateFramedCard(
            topBarParent,
            "SpeedToggleButton",
            SpeedNormalBgColor,
            new Color(0.84f, 0.66f, 0.30f, 0.96f),
            3f);
        cardRt.anchorMin = new Vector2(1f, 0.5f);
        cardRt.anchorMax = new Vector2(1f, 0.5f);
        cardRt.pivot = new Vector2(1f, 0.5f);
        cardRt.sizeDelta = new Vector2(84f, pauseHeight);
        cardRt.anchoredPosition = new Vector2(-(rightEdgePadding + pauseWidth + 12f), 0f);

        UnityEngine.UI.Image borderImg = cardRt.GetComponent<UnityEngine.UI.Image>();
        if (borderImg != null) borderImg.raycastTarget = true;

        Transform fill = cardRt.Find("Fill");
        speedButtonBg = fill != null ? fill.GetComponent<UnityEngine.UI.Image>() : borderImg;

        speedToggleButton = cardRt.gameObject.AddComponent<UnityEngine.UI.Button>();
        speedToggleButton.targetGraphic = speedButtonBg;

        speedButtonText = MetaUIHelper.CreateText(
            fill != null ? fill : cardRt,
            "Label",
            "1x",
            30f,
            new Color(0.98f, 0.94f, 0.84f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        speedButtonText.enableAutoSizing = true;
        speedButtonText.fontSizeMin = 20f;
        speedButtonText.fontSizeMax = 30f;
    }

    private void EnsureNotificationBanner()
    {
        if (notificationBannerGroup != null) return;

        Transform existing = transform.Find("ExploreNotificationBanner");
        if (existing != null)
        {
            notificationBannerGroup = existing.GetComponent<CanvasGroup>();
            notificationBannerBg = existing.GetComponent<UnityEngine.UI.Image>();
            notificationBannerText = existing.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        GameObject bannerObj = new GameObject("ExploreNotificationBanner");
        bannerObj.transform.SetParent(transform, false);

        RectTransform bannerRect = bannerObj.AddComponent<RectTransform>();
        bannerRect.anchorMin = new Vector2(0.5f, 1f);
        bannerRect.anchorMax = new Vector2(0.5f, 1f);
        bannerRect.pivot = new Vector2(0.5f, 0.5f);
        bannerRect.sizeDelta = new Vector2(620f, 76f);
        bannerRect.anchoredPosition = new Vector2(0f, -185f);

        notificationBannerBg = bannerObj.AddComponent<UnityEngine.UI.Image>();
        notificationBannerBg.sprite = CharacterWorldHUD.GetRuntimeWhiteSprite();
        notificationBannerBg.color = new Color(0.15f, 0.20f, 0.28f, 0.94f);
        notificationBannerBg.raycastTarget = false;

        notificationBannerGroup = bannerObj.AddComponent<CanvasGroup>();
        notificationBannerGroup.alpha = 0f;
        notificationBannerGroup.interactable = false;
        notificationBannerGroup.blocksRaycasts = false;

        GameObject textObj = new GameObject("BannerText");
        textObj.transform.SetParent(bannerObj.transform, false);
        RectTransform textRect = textObj.AddComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 6f);
        textRect.offsetMax = new Vector2(-18f, -6f);

        notificationBannerText = textObj.AddComponent<TextMeshProUGUI>();
        if (DefaultFont != null) notificationBannerText.font = DefaultFont;
        notificationBannerText.fontSize = 34f;
        notificationBannerText.fontStyle = FontStyles.Bold;
        notificationBannerText.alignment = TextAlignmentOptions.Center;
        notificationBannerText.color = Color.white;
        notificationBannerText.raycastTarget = false;

        bannerObj.SetActive(false);
    }

    public void ShowVictory(Player player = null)
    {
        
        if (victoryUI != null)
            victoryUI.Show(player);
    }

    private Coroutine _gameOverCoroutine;

    public void ShowGameOver(Player player = null)
    {
        
        if (_gameOverCoroutine != null) StopCoroutine(_gameOverCoroutine);
        _gameOverCoroutine = StartCoroutine(ShowGameOverDelayed());
    }

    private IEnumerator ShowGameOverDelayed()
    {
        yield return new WaitForSecondsRealtime(1.5f);
        if (defeatUI != null) defeatUI.Show();
        _gameOverCoroutine = null;
    }

    public void UpdateTurnCount(int currentTurns)
    {
        EnsureTurnPhaseStatusWidget();

        // Hide legacy 5-dot AP icons cleanly since Turn Phase widget replaces them
        if (turnIcons != null)
        {
            for (int i = 0; i < turnIcons.Length; i++)
            {
                if (turnIcons[i] != null) turnIcons[i].SetActive(false);
            }
        }
    }

    private void HandlePlayerTurnStart(object param)
    {
        HideTargetSelectionPrompt();
        SetTurnPhaseStatus("YOUR TURN", new Color(0.32f, 0.92f, 0.54f, 1f));
    }

    private void HandleEnemyTurnStart(object param)
    {
        HideTargetSelectionPrompt();
        SetTurnPhaseStatus("ENEMY TURN", new Color(0.98f, 0.44f, 0.38f, 1f));
    }

    private void HandleTargetSelectionRequired(object param)
    {
        SetTurnPhaseStatus("SELECT TARGET", new Color(0.98f, 0.80f, 0.24f, 1f));
        ShowTargetSelectionPrompt();
    }

    private void HandleTargetSelected(object param)
    {
        HideTargetSelectionPrompt();
    }

    private void EnsureTurnPhaseStatusWidget()
    {
        if (turnPhaseStateText != null) return;
        if (turnIcons == null || turnIcons.Length == 0 || turnIcons[0] == null) return;

        // turnIcons[0] sits inside TurnContainer inside CharacterTurn
        Transform turnListTf = turnIcons[0].transform.parent;
        Transform characterTurnRoot = turnListTf != null && turnListTf.parent != null ? turnListTf.parent : turnListTf;
        if (characterTurnRoot == null) return;

        // Reuse or tidy up static "Action Point" header inside CharacterTurn
        TextMeshProUGUI[] existingTexts = characterTurnRoot.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (var t in existingTexts)
        {
            if (t != null && t.name != "TurnPhaseStateText")
            {
                turnPhaseTitleText = t;
                turnPhaseTitleText.text = "TURN PHASE";
                turnPhaseTitleText.color = new Color(0.84f, 0.78f, 0.66f, 0.92f);
                turnPhaseTitleText.enableAutoSizing = true;
                turnPhaseTitleText.fontSizeMin = 14f;
                turnPhaseTitleText.fontSizeMax = 22f;
                break;
            }
        }

        if (turnListTf != null && turnListTf != characterTurnRoot)
        {
            turnListTf.gameObject.SetActive(false);
        }

        Transform existingState = characterTurnRoot.Find("TurnPhaseStateText");
        if (existingState != null)
        {
            turnPhaseStateText = existingState.GetComponent<TextMeshProUGUI>();
            return;
        }

        GameObject stateObj = new GameObject("TurnPhaseStateText");
        stateObj.transform.SetParent(characterTurnRoot, false);
        RectTransform rt = stateObj.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0f, 0.05f);
        rt.anchorMax = new Vector2(1f, 0.62f);
        rt.offsetMin = new Vector2(6f, 0f);
        rt.offsetMax = new Vector2(-6f, 0f);

        turnPhaseStateText = stateObj.AddComponent<TextMeshProUGUI>();
        if (DefaultFont != null) turnPhaseStateText.font = DefaultFont;
        turnPhaseStateText.text = "READY";
        turnPhaseStateText.alignment = TextAlignmentOptions.Center;
        turnPhaseStateText.fontStyle = FontStyles.Bold;
        turnPhaseStateText.enableAutoSizing = true;
        turnPhaseStateText.fontSizeMin = 15f;
        turnPhaseStateText.fontSizeMax = 26f;
        turnPhaseStateText.color = new Color(0.96f, 0.84f, 0.42f, 1f);
        turnPhaseStateText.raycastTarget = false;
    }

    private void SetTurnPhaseStatus(string label, Color color)
    {
        EnsureTurnPhaseStatusWidget();
        if (turnPhaseStateText == null) return;

        turnPhaseStateText.text = label;
        turnPhaseStateText.color = color;
        turnPhaseStateText.transform.DOKill();
        turnPhaseStateText.transform.localScale = Vector3.one * 0.88f;
        turnPhaseStateText.transform.DOScale(1f, 0.20f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void EnsureTargetSelectionPrompt()
    {
        if (targetPromptGroup != null) return;

        Transform existing = transform.Find("TargetSelectionPromptBanner");
        if (existing != null)
        {
            targetPromptGroup = existing.GetComponent<CanvasGroup>();
            return;
        }

        RectTransform cardRt = MetaUIHelper.CreateFramedCard(
            transform,
            "TargetSelectionPromptBanner",
            new Color(0.12f, 0.08f, 0.09f, 0.94f),
            new Color(0.95f, 0.72f, 0.24f, 0.96f),
            3f);
        cardRt.anchorMin = new Vector2(0.5f, 1f);
        cardRt.anchorMax = new Vector2(0.5f, 1f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(640f, 68f);
        cardRt.anchoredPosition = new Vector2(0f, -210f);

        targetPromptGroup = cardRt.gameObject.AddComponent<CanvasGroup>();
        targetPromptGroup.alpha = 0f;
        targetPromptGroup.interactable = false;
        targetPromptGroup.blocksRaycasts = false;

        Transform fill = cardRt.Find("Fill");
        TextMeshProUGUI txt = MetaUIHelper.CreateText(
            fill != null ? fill : cardRt,
            "PromptText",
            "TAP AN ENEMY TO ATTACK",
            28f,
            new Color(1f, 0.88f, 0.42f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        txt.enableAutoSizing = true;
        txt.fontSizeMin = 18f;
        txt.fontSizeMax = 28f;

        cardRt.gameObject.SetActive(false);
    }

    private void ShowTargetSelectionPrompt()
    {
        // Only show banner if >= 2 alive enemies (1 enemy auto-selects in 0.3s)
        EnsureTargetSelectionPrompt();
        if (targetPromptGroup == null) return;

        targetPromptGroup.gameObject.SetActive(true);
        targetPromptGroup.DOKill();
        if (targetPromptPulseTween != null) targetPromptPulseTween.Kill();

        targetPromptGroup.alpha = 0f;
        targetPromptGroup.transform.localScale = Vector3.one * 0.92f;
        targetPromptGroup.DOFade(1f, 0.16f).SetUpdate(true);
        targetPromptPulseTween = targetPromptGroup.transform
            .DOScale(1.04f, 0.45f)
            .SetEase(Ease.InOutSine)
            .SetLoops(-1, LoopType.Yoyo)
            .SetUpdate(true);
    }

    private void HideTargetSelectionPrompt()
    {
        if (targetPromptPulseTween != null)
        {
            targetPromptPulseTween.Kill();
            targetPromptPulseTween = null;
        }
        if (targetPromptGroup != null)
        {
            targetPromptGroup.DOKill();
            targetPromptGroup.alpha = 0f;
            targetPromptGroup.gameObject.SetActive(false);
        }
    }

    private void EnsureQuickGuideButton()
    {
        if (quickGuideButton != null || pauseButton == null || pauseButton.transform.parent == null) return;

        Transform topBarParent = pauseButton.transform.parent;
        Transform existing = topBarParent.Find("QuickGuideButton");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        RectTransform pauseRect = pauseButton.GetComponent<RectTransform>();
        float pauseWidth = pauseRect != null && pauseRect.rect.width > 10f ? pauseRect.rect.width : 76f;
        float pauseHeight = pauseRect != null && pauseRect.rect.height > 10f ? pauseRect.rect.height : 74f;
        float rightEdgePadding = pauseRect != null ? Mathf.Abs(pauseRect.anchoredPosition.x) : 18f;
        if (rightEdgePadding < 10f) rightEdgePadding = 18f;

        float rightOffset = rightEdgePadding + pauseWidth + 12f + 84f + 10f; // Cleanly left of SpeedToggleButton

        RectTransform cardRt = MetaUIHelper.CreateFramedCard(
            topBarParent,
            "QuickGuideButton",
            new Color(0.58f, 0.16f, 0.15f, 0.96f),
            new Color(0.84f, 0.66f, 0.30f, 0.96f),
            3f);
        cardRt.anchorMin = new Vector2(1f, 0.5f);
        cardRt.anchorMax = new Vector2(1f, 0.5f);
        cardRt.pivot = new Vector2(1f, 0.5f);
        cardRt.sizeDelta = new Vector2(74f, pauseHeight);
        cardRt.anchoredPosition = new Vector2(-rightOffset, 0f);

        UnityEngine.UI.Image borderImg = cardRt.GetComponent<UnityEngine.UI.Image>();
        if (borderImg != null) borderImg.raycastTarget = true;

        Transform fill = cardRt.Find("Fill");
        UnityEngine.UI.Image fillImg = fill != null ? fill.GetComponent<UnityEngine.UI.Image>() : borderImg;

        UnityEngine.UI.Button btn = cardRt.gameObject.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = fillImg;

        TextMeshProUGUI qTxt = MetaUIHelper.CreateText(
            fill != null ? fill : cardRt,
            "Label",
            "?",
            32f,
            new Color(1f, 0.92f, 0.62f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        qTxt.enableAutoSizing = true;
        qTxt.fontSizeMin = 22f;
        qTxt.fontSizeMax = 32f;

        quickGuideButton = btn;
    }

    private void ToggleQuickGuideModal()
    {
        AudioManager.Instance?.PlaySoundButtonClick();
        EnsureQuickGuideModal();
        if (quickGuideModalObj == null) return;

        bool nextActive = !quickGuideModalObj.activeSelf;
        quickGuideModalObj.SetActive(nextActive);
        if (nextActive)
        {
            quickGuideModalObj.transform.SetAsLastSibling();
            Transform card = quickGuideModalObj.transform.Find("GuideCard");
            if (card != null)
            {
                card.DOKill();
                card.localScale = Vector3.one * 0.86f;
                card.DOScale(1f, 0.22f).SetEase(Ease.OutBack).SetUpdate(true);
            }
        }
    }

    private void CloseQuickGuideModal()
    {
        if (quickGuideModalObj != null)
        {
            quickGuideModalObj.SetActive(false);
        }
    }

    private Dictionary<GemType, Sprite> LoadGemSpritesMap()
    {
        var map = new Dictionary<GemType, Sprite>();
        GemData[] allGems = Resources.LoadAll<GemData>("SO/GemInfo");
        if (allGems != null)
        {
            foreach (var gd in allGems)
            {
                if (gd != null && gd.gemSprite != null)
                {
                    map[gd.gemType] = gd.gemSprite;
                }
            }
        }
        return map;
    }

    private Sprite FindScrollHeaderSprite()
    {
        // Reuse the ornate red-gold scroll sprite from BackUI > StatBG if present in scene
        GameObject statBg = GameObject.Find("StatBG");
        if (statBg != null)
        {
            UnityEngine.UI.Image img = statBg.GetComponent<UnityEngine.UI.Image>();
            if (img != null && img.sprite != null) return img.sprite;
        }
        return null;
    }

    private void EnsureQuickGuideModal()
    {
        if (quickGuideModalObj != null) return;

        Transform existing = transform.Find("QuickGuideModal");
        if (existing != null)
        {
            Destroy(existing.gameObject);
        }

        Dictionary<GemType, Sprite> gemSprites = LoadGemSpritesMap();
        Sprite headerScrollSprite = FindScrollHeaderSprite();

        UnityEngine.UI.Image backdrop = MetaUIHelper.CreatePanel(transform, "QuickGuideModal", new Color(0.03f, 0.03f, 0.05f, 0.86f), true);
        RectTransform bgRt = backdrop.rectTransform;
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        UnityEngine.UI.Button bgCloseBtn = backdrop.gameObject.AddComponent<UnityEngine.UI.Button>();
        bgCloseBtn.transition = Selectable.Transition.None;
        bgCloseBtn.onClick.AddListener(CloseQuickGuideModal);

        // Main outer board frame (Crimson-Gold Dark Ink)
        RectTransform cardRt = MetaUIHelper.CreateFramedCard(
            backdrop.transform,
            "GuideCard",
            new Color(0.09f, 0.09f, 0.12f, 0.98f),
            new Color(0.78f, 0.58f, 0.24f, 0.96f),
            4f);
        cardRt.anchorMin = new Vector2(0.5f, 0.5f);
        cardRt.anchorMax = new Vector2(0.5f, 0.5f);
        cardRt.pivot = new Vector2(0.5f, 0.5f);
        cardRt.sizeDelta = new Vector2(940f, 1020f);
        cardRt.anchoredPosition = new Vector2(0f, -10f);

        // Block click-through on card body so tapping inside doesn't close accidentally
        UnityEngine.UI.Image cardBlocker = cardRt.GetComponent<UnityEngine.UI.Image>();
        if (cardBlocker != null) cardBlocker.raycastTarget = true;

        // Inner crimson accent border
        RectTransform innerTrim = MetaUIHelper.CreateFramedCard(
            cardRt,
            "CrimsonInnerTrim",
            new Color(0.10f, 0.10f, 0.13f, 1f),
            new Color(0.68f, 0.18f, 0.16f, 0.85f),
            2f);
        innerTrim.anchorMin = Vector2.zero;
        innerTrim.anchorMax = Vector2.one;
        innerTrim.offsetMin = new Vector2(10f, 10f);
        innerTrim.offsetMax = new Vector2(-10f, -10f);
        Transform contentRoot = innerTrim.Find("Fill") ?? innerTrim;

        // Ornate Top Scroll Ribbon Header (Pokiwar-style banner!)
        GameObject headerObj = new GameObject("HeaderRibbon");
        headerObj.transform.SetParent(cardRt, false);
        RectTransform headerRt = headerObj.AddComponent<RectTransform>();
        headerRt.anchorMin = new Vector2(0.5f, 1f);
        headerRt.anchorMax = new Vector2(0.5f, 1f);
        headerRt.pivot = new Vector2(0.5f, 0.5f);
        headerRt.sizeDelta = new Vector2(720f, 108f);
        headerRt.anchoredPosition = new Vector2(0f, -12f);

        UnityEngine.UI.Image headerImg = headerObj.AddComponent<UnityEngine.UI.Image>();
        if (headerScrollSprite != null)
        {
            headerImg.sprite = headerScrollSprite;
            headerImg.type = UnityEngine.UI.Image.Type.Sliced;
            headerImg.color = Color.white;
        }
        else
        {
            headerImg.sprite = CharacterWorldHUD.GetRuntimeWhiteSprite();
            headerImg.color = new Color(0.58f, 0.16f, 0.15f, 1f);
        }
        headerImg.raycastTarget = false;

        TextMeshProUGUI title = MetaUIHelper.CreateText(
            headerObj.transform,
            "Title",
            "GEM & COMBO GUIDE",
            32f,
            new Color(1f, 0.90f, 0.50f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        title.rectTransform.offsetMin = new Vector2(70f, 8f);
        title.rectTransform.offsetMax = new Vector2(-70f, -8f);
        title.enableAutoSizing = true;
        title.fontSizeMin = 22f;
        title.fontSizeMax = 32f;

        // Top-right corner 'X' close button
        UnityEngine.UI.Button closeXBtn = MetaUIHelper.CreateButton(
            cardRt,
            "CloseXBtn",
            "X",
            28f,
            new Color(0.72f, 0.18f, 0.16f, 1f),
            out _,
            out _);
        RectTransform closeXRt = closeXBtn.GetComponent<RectTransform>();
        closeXRt.anchorMin = new Vector2(1f, 1f);
        closeXRt.anchorMax = new Vector2(1f, 1f);
        closeXRt.pivot = new Vector2(1f, 1f);
        closeXRt.sizeDelta = new Vector2(64f, 64f);
        closeXRt.anchoredPosition = new Vector2(-16f, -16f);
        closeXBtn.onClick.AddListener(CloseQuickGuideModal);

        // SECTION 1 TITLE: ELEMENTAL GEMS (2x3 visual icon cards like Pokiwar!)
        TextMeshProUGUI sec1Title = MetaUIHelper.CreateText(
            contentRoot,
            "Sec1Header",
            "RUNIC GEMS (MATCH 3+ TO ACTIVATE)",
            22f,
            new Color(0.95f, 0.78f, 0.38f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        sec1Title.rectTransform.anchorMin = new Vector2(0.04f, 0.865f);
        sec1Title.rectTransform.anchorMax = new Vector2(0.96f, 0.915f);

        GameObject gemGridObj = new GameObject("GemCardsGrid");
        gemGridObj.transform.SetParent(contentRoot, false);
        RectTransform gemGridRt = gemGridObj.AddComponent<RectTransform>();
        gemGridRt.anchorMin = new Vector2(0.035f, 0.435f);
        gemGridRt.anchorMax = new Vector2(0.965f, 0.86f);
        gemGridRt.offsetMin = Vector2.zero;
        gemGridRt.offsetMax = Vector2.zero;

        GridLayoutGroup glg = gemGridObj.AddComponent<GridLayoutGroup>();
        glg.cellSize = new Vector2(412f, 124f);
        glg.spacing = new Vector2(18f, 14f);
        glg.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        glg.constraintCount = 2;
        glg.childAlignment = TextAnchor.MiddleCenter;

        BuildVisualGemCard(gemGridObj.transform, gemSprites, GemType.Damage, "SWORD GEM", new Color(1f, 0.45f, 0.45f, 1f), "Strike enemy for direct DMG.\n2+ groups trigger Cleave!");
        BuildVisualGemCard(gemGridObj.transform, gemSprites, GemType.Health, "HEART GEM", new Color(0.35f, 0.92f, 0.55f, 1f), "Restore HP instantly\n(scales with Max HP).");
        BuildVisualGemCard(gemGridObj.transform, gemSprites, GemType.Shield, "SHIELD GEM", new Color(0.45f, 0.82f, 1f, 1f), "Gain Shield armor to\nblock incoming hits.");
        BuildVisualGemCard(gemGridObj.transform, gemSprites, GemType.CritRate, "STAR GEM", new Color(1f, 0.86f, 0.32f, 1f), "+Crit Rate for 4 turns.\nOverflow -> Bonus DMG!");
        BuildVisualGemCard(gemGridObj.transform, gemSprites, GemType.CritDamage, "BOLT GEM", new Color(1f, 0.58f, 0.24f, 1f), "+Crit DMG for 4 turns.\nOverflow -> Bonus DMG!");
        BuildVisualGemCard(gemGridObj.transform, gemSprites, GemType.Dodge, "WING GEM", new Color(0.84f, 0.58f, 1f, 1f), "+Dodge chance (4 turns).\nOverflow -> Bonus Shield!");

        // SECTION 2 TITLE: VISUAL COMBO FORMATIONS (Pokiwar 4-in-row / 5-cross diagrams!)
        TextMeshProUGUI sec2Title = MetaUIHelper.CreateText(
            contentRoot,
            "Sec2Header",
            "SPECIAL COMBOS & RIVAL DUEL RULES",
            22f,
            new Color(0.95f, 0.78f, 0.38f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        sec2Title.rectTransform.anchorMin = new Vector2(0.04f, 0.375f);
        sec2Title.rectTransform.anchorMax = new Vector2(0.96f, 0.425f);

        GameObject comboRowObj = new GameObject("ComboCardsRow");
        comboRowObj.transform.SetParent(contentRoot, false);
        RectTransform comboRowRt = comboRowObj.AddComponent<RectTransform>();
        comboRowRt.anchorMin = new Vector2(0.035f, 0.125f);
        comboRowRt.anchorMax = new Vector2(0.965f, 0.37f);
        comboRowRt.offsetMin = Vector2.zero;
        comboRowRt.offsetMax = Vector2.zero;

        HorizontalLayoutGroup hlg = comboRowObj.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14f;
        hlg.childControlWidth = true;
        hlg.childControlHeight = true;
        hlg.childForceExpandWidth = true;
        hlg.childForceExpandHeight = true;

        Sprite swordSp = gemSprites.ContainsKey(GemType.Damage) ? gemSprites[GemType.Damage] : null;
        Sprite boltSp = gemSprites.ContainsKey(GemType.CritDamage) ? gemSprites[GemType.CritDamage] : swordSp;
        Sprite heartSp = gemSprites.ContainsKey(GemType.Health) ? gemSprites[GemType.Health] : swordSp;
        Sprite shieldSp = gemSprites.ContainsKey(GemType.Shield) ? gemSprites[GemType.Shield] : swordSp;

        BuildVisualComboCard(
            comboRowObj.transform,
            "MATCH 4 LINE",
            new Color(1f, 0.78f, 0.28f, 1f),
            new[] { swordSp, swordSp, swordSp, swordSp },
            "LINE BLAST!\nExplodes full Row or Column.");

        BuildVisualComboCard(
            comboRowObj.transform,
            "MATCH 5+ / T / L",
            new Color(1f, 0.46f, 0.32f, 1f),
            new[] { boltSp, boltSp, boltSp, boltSp, boltSp },
            "CROSS BLAST!\nClears Row + Col & Cleaves foes.");

        BuildVisualComboCard(
            comboRowObj.transform,
            "SHARED BOARD AI",
            new Color(0.48f, 0.86f, 0.98f, 1f),
            new[] { swordSp, heartSp, shieldSp },
            "RIVAL SWAPS!\nFoes steal Gems too (Amber ring).");

        // Bottom CTA button
        UnityEngine.UI.Button gotItBtn = MetaUIHelper.CreateButton(
            contentRoot,
            "CloseGuideBtn",
            "BACK TO BATTLE",
            26f,
            MetaUIHelper.BtnCrimsonAction,
            out _,
            out _);
        RectTransform closeRt = gotItBtn.GetComponent<RectTransform>();
        closeRt.anchorMin = new Vector2(0.5f, 0.025f);
        closeRt.anchorMax = new Vector2(0.5f, 0.025f);
        closeRt.pivot = new Vector2(0.5f, 0f);
        closeRt.sizeDelta = new Vector2(320f, 68f);
        closeRt.anchoredPosition = Vector2.zero;
        gotItBtn.onClick.AddListener(CloseQuickGuideModal);

        quickGuideModalObj = backdrop.gameObject;
        quickGuideModalObj.SetActive(false);
    }

    private void BuildVisualGemCard(
        Transform parent,
        Dictionary<GemType, Sprite> sprites,
        GemType type,
        string gemTitle,
        Color titleColor,
        string effectDesc)
    {
        RectTransform card = MetaUIHelper.CreateFramedCard(
            parent,
            $"GemInfo_{type}",
            new Color(0.14f, 0.14f, 0.18f, 0.96f),
            new Color(titleColor.r * 0.75f, titleColor.g * 0.75f, titleColor.b * 0.75f, 0.65f),
            2f);
        Transform fill = card.Find("Fill") ?? card;

        // Real Gem Sprite icon on the left!
        GameObject iconSlot = new GameObject("GemSpriteSlot");
        iconSlot.transform.SetParent(fill, false);
        RectTransform iconRt = iconSlot.AddComponent<RectTransform>();
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.sizeDelta = new Vector2(92f, 92f);
        iconRt.anchoredPosition = new Vector2(14f, 0f);

        UnityEngine.UI.Image gemImg = iconSlot.AddComponent<UnityEngine.UI.Image>();
        if (sprites != null && sprites.TryGetValue(type, out Sprite sp) && sp != null)
        {
            gemImg.sprite = sp;
            gemImg.preserveAspect = true;
            gemImg.color = Color.white;
        }
        else
        {
            gemImg.sprite = CharacterWorldHUD.GetRuntimeWhiteSprite();
            gemImg.color = titleColor;
        }
        gemImg.raycastTarget = false;

        TextMeshProUGUI nameTxt = MetaUIHelper.CreateText(
            fill,
            "GemName",
            gemTitle,
            23f,
            titleColor,
            TextAlignmentOptions.BottomLeft,
            FontStyles.Bold);
        nameTxt.rectTransform.anchorMin = new Vector2(0f, 0.56f);
        nameTxt.rectTransform.anchorMax = new Vector2(1f, 0.94f);
        nameTxt.rectTransform.offsetMin = new Vector2(118f, 0f);
        nameTxt.rectTransform.offsetMax = new Vector2(-12f, 0f);
        nameTxt.enableAutoSizing = true;
        nameTxt.fontSizeMin = 16f;
        nameTxt.fontSizeMax = 23f;

        TextMeshProUGUI descTxt = MetaUIHelper.CreateText(
            fill,
            "GemDesc",
            effectDesc,
            18f,
            new Color(0.88f, 0.86f, 0.82f, 1f),
            TextAlignmentOptions.TopLeft,
            FontStyles.Normal);
        descTxt.enableWordWrapping = true;
        descTxt.enableAutoSizing = true;
        descTxt.fontSizeMin = 13f;
        descTxt.fontSizeMax = 18f;
        descTxt.rectTransform.anchorMin = new Vector2(0f, 0.06f);
        descTxt.rectTransform.anchorMax = new Vector2(1f, 0.56f);
        descTxt.rectTransform.offsetMin = new Vector2(118f, 0f);
        descTxt.rectTransform.offsetMax = new Vector2(-10f, 0f);
    }

    private void BuildVisualComboCard(
        Transform parent,
        string header,
        Color accentColor,
        Sprite[] miniSprites,
        string desc)
    {
        RectTransform card = MetaUIHelper.CreateFramedCard(
            parent,
            $"Combo_{header}",
            new Color(0.13f, 0.13f, 0.17f, 0.96f),
            new Color(accentColor.r * 0.8f, accentColor.g * 0.8f, accentColor.b * 0.8f, 0.7f),
            2f);
        Transform fill = card.Find("Fill") ?? card;

        TextMeshProUGUI headTxt = MetaUIHelper.CreateText(
            fill,
            "Header",
            header,
            20f,
            accentColor,
            TextAlignmentOptions.Center,
            FontStyles.Bold);
        headTxt.rectTransform.anchorMin = new Vector2(0.04f, 0.74f);
        headTxt.rectTransform.anchorMax = new Vector2(0.96f, 0.96f);
        headTxt.enableAutoSizing = true;
        headTxt.fontSizeMin = 14f;
        headTxt.fontSizeMax = 20f;

        // Visual mini-gem row in the middle!
        GameObject rowObj = new GameObject("MiniGemRow");
        rowObj.transform.SetParent(fill, false);
        RectTransform rowRt = rowObj.AddComponent<RectTransform>();
        rowRt.anchorMin = new Vector2(0.05f, 0.40f);
        rowRt.anchorMax = new Vector2(0.95f, 0.72f);
        rowRt.offsetMin = Vector2.zero;
        rowRt.offsetMax = Vector2.zero;

        HorizontalLayoutGroup rowHlg = rowObj.AddComponent<HorizontalLayoutGroup>();
        rowHlg.spacing = 4f;
        rowHlg.childAlignment = TextAnchor.MiddleCenter;
        rowHlg.childControlWidth = false;
        rowHlg.childControlHeight = false;
        rowHlg.childForceExpandWidth = false;
        rowHlg.childForceExpandHeight = false;

        if (miniSprites != null)
        {
            float iconSize = miniSprites.Length >= 5 ? 42f : 48f;
            for (int i = 0; i < miniSprites.Length; i++)
            {
                if (miniSprites[i] == null) continue;
                GameObject g = new GameObject($"MiniGem_{i}");
                g.transform.SetParent(rowObj.transform, false);
                RectTransform grt = g.AddComponent<RectTransform>();
                grt.sizeDelta = new Vector2(iconSize, iconSize);
                UnityEngine.UI.Image gi = g.AddComponent<UnityEngine.UI.Image>();
                gi.sprite = miniSprites[i];
                gi.preserveAspect = true;
                gi.raycastTarget = false;
            }
        }

        TextMeshProUGUI descTxt = MetaUIHelper.CreateText(
            fill,
            "Desc",
            desc,
            17f,
            new Color(0.88f, 0.86f, 0.82f, 1f),
            TextAlignmentOptions.Center,
            FontStyles.Normal);
        descTxt.enableWordWrapping = true;
        descTxt.enableAutoSizing = true;
        descTxt.fontSizeMin = 12f;
        descTxt.fontSizeMax = 17f;
        descTxt.rectTransform.anchorMin = new Vector2(0.05f, 0.05f);
        descTxt.rectTransform.anchorMax = new Vector2(0.95f, 0.38f);
    }

    public void ShowEnemyInfo(Enemy enemy)
    {
        // Đã có thanh máu & giáp trực tiếp dưới chân từng quái (CharacterWorldHUD),
        // tắt hẳn panel máu quái cũ trên TopBar để không che khu vực Round & Pause.
        currentShownEnemy = null;
        if (enemyInfoPanel != null) enemyInfoPanel.SetActive(false);
    }

    public void HideEnemyInfo()
    {
        currentShownEnemy = null;
        if (enemyInfoPanel != null) enemyInfoPanel.SetActive(false);
    }

    private void UpdatePlayerStats(object param)
    {
        if (param is Player player)
        {
            int curHp = player.CurrentHealth > 0f ? Mathf.CeilToInt(player.CurrentHealth) : 0;
            int maxHp = Mathf.RoundToInt(player.CurrentMaxHealth);
            int shd = Mathf.RoundToInt(player.CurrentShield);
            int dmg = Mathf.RoundToInt(player.CurrentDamage);
            int crit = Mathf.RoundToInt(player.CurrentCritRate);
            int cDmg = Mathf.RoundToInt(player.CurrentCritDamage);
            int dodge = Mathf.RoundToInt(player.CurrentBlockRate);

            if (hpText != null) hpText.text = $"{curHp}/{maxHp}";
            if (shieldText != null) shieldText.text = $"{shd}";
            if (damageText != null) damageText.text = $"{dmg}";
            if (critRateText != null) critRateText.text = $"{crit}%";
            if (critDamageText != null) critDamageText.text = $"{cDmg}%";
            if (blockRateText != null) blockRateText.text = $"{dodge}%";

            float safeMaxHp = Mathf.Max(1f, player.CurrentMaxHealth);
            if (playerHpFill != null)
            {
                playerHpFill.DOKill();
                playerHpFill.DOFillAmount(Mathf.Clamp01(player.CurrentHealth / safeMaxHp), 0.25f).SetEase(Ease.OutCubic);
            }
            if (playerShieldFill != null)
            {
                playerShieldFill.DOKill();
                playerShieldFill.DOFillAmount(Mathf.Clamp01(player.CurrentShield / (safeMaxHp * 0.5f)), 0.25f).SetEase(Ease.OutCubic);
            }
        }
    }

    private void UpdateRoundCount(object param)
    {
        int round = (int)param;
        if (roundText == null) return;

        // Hide legacy static "ROUND" label sibling and give roundText full wide TopBar room (680px) so font stays large!
        if (roundText.transform.parent != null)
        {
            Transform staticRoundLbl = roundText.transform.parent.Find("RoundTxt");
            if (staticRoundLbl != null && staticRoundLbl.gameObject.activeSelf)
            {
                staticRoundLbl.gameObject.SetActive(false);
            }
        }

        RectTransform rt = roundText.rectTransform;
        rt.anchorMin = new Vector2(0f, 0.5f);
        rt.anchorMax = new Vector2(0f, 0.5f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(26f, 0f);
        rt.sizeDelta = new Vector2(680f, 78f);
        roundText.alignment = TextAlignmentOptions.Left;
        roundText.enableWordWrapping = false;
        roundText.enableAutoSizing = true;
        roundText.fontSizeMin = 28f;
        roundText.fontSizeMax = 42f;
        roundText.color = new Color(0.98f, 0.94f, 0.84f, 1f);

        bool isLevel = GameModeManager.Instance != null && GameModeManager.Instance.CurrentMode == GameModeType.Level;
        if (isLevel)
        {
            LevelConfig cfg = GameModeManager.Instance.CurrentLevelConfig;
            IGameModeStrategy strategy = BattleManager.Instance?.CurrentStrategy;
            int totalWaves = strategy != null ? strategy.GetTotalWaves() : Mathf.Max(1, cfg?.MaxWaves ?? 3);
            int lvId = cfg != null ? cfg.LevelID : 1;
            bool isBoss = strategy != null && strategy.IsBossWave();
            float threeStarHp = cfg != null ? cfg.threeStarHPThreshold : 70f;

            string bossTag = isBoss ? " <color=#FBBF24>[BOSS]</color>" : "";
            roundText.text = $"LV.{lvId} - WAVE {round}/{totalWaves}{bossTag}  <size=72%><color=#FAD15C>(3-STAR >{threeStarHp:0}% HP)</color></size>";
        }
        else
        {
            roundText.text = $"ABYSSAL RIFT - ROUND {round}";
        }
    }

    public void RefreshEnemyHP(Enemy enemy)
    {
        // Deprecated top enemy bar disabled in favor of CharacterWorldHUD
    }

    private void EnsurePopupPool()
    {
        if (popupPool != null || damagePopupObj == null) return;

        popupPool = new ObjectPool<GameObject>(
            createFunc: () =>
            {
                GameObject clone = Instantiate(damagePopupObj, damagePopupObj.transform.parent);
                clone.SetActive(false);
                return clone;
            },
            actionOnGet: (go) =>
            {
                if (go != null)
                {
                    go.SetActive(true);
                    go.transform.SetAsLastSibling();
                }
            },
            actionOnRelease: (go) =>
            {
                if (go != null)
                {
                    go.transform.DOKill();
                    TextMeshProUGUI tmp = go.GetComponentInChildren<TextMeshProUGUI>();
                    if (tmp != null) tmp.DOKill();
                    go.SetActive(false);
                }
            },
            actionOnDestroy: (go) =>
            {
                if (go != null) Destroy(go);
            },
            collectionCheck: false,
            defaultCapacity: 8,
            maxSize: 24);
    }

    private void ReleaseAllActivePopups()
    {
        if (popupPool == null || activePopups.Count == 0) return;

        for (int i = activePopups.Count - 1; i >= 0; i--)
        {
            GameObject go = activePopups[i];
            activePopups.RemoveAt(i);
            if (go != null) popupPool.Release(go);
        }
    }

    private void HandleShowDamagePopup(object param)
    {
        if (param is DamagePopupData data)
        {
            ShowFloatingPopup(data);
        }
    }

    public void ShowDamagePopup(Vector3 position, int damageAmount, bool isCritical)
    {
        ShowFloatingPopup(new DamagePopupData
        {
            Position = position,
            Damage = damageAmount,
            IsCritical = isCritical,
            PopupType = isCritical ? FloatingPopupType.CriticalDamage : FloatingPopupType.NormalDamage
        });
    }

    public void ShowFloatingPopup(DamagePopupData data)
    {
        if (damagePopupObj == null) return;
        EnsurePopupPool();
        if (popupPool == null) return;

        GameObject popup = popupPool.Get();
        if (popup == null) return;

        TextMeshProUGUI tmp = popup.GetComponentInChildren<TextMeshProUGUI>();
        if (tmp == null)
        {
            popupPool.Release(popup);
            return;
        }

        FloatingPopupType type = data.PopupType;
        if (type == FloatingPopupType.NormalDamage && data.IsCritical)
        {
            type = FloatingPopupType.CriticalDamage;
        }

        string text;
        Color color;
        float startScale;

        switch (type)
        {
            case FloatingPopupType.CriticalDamage:
                text = string.IsNullOrEmpty(data.CustomText) ? $"{data.Damage}!" : data.CustomText;
                color = new Color(1.00f, 0.28f, 0.22f, 1f);
                startScale = 1.45f;
                break;
            case FloatingPopupType.Heal:
                text = string.IsNullOrEmpty(data.CustomText) ? $"+{data.Damage} HP" : data.CustomText;
                color = new Color(0.28f, 0.88f, 0.48f, 1f);
                startScale = 1.05f;
                break;
            case FloatingPopupType.Shield:
                text = string.IsNullOrEmpty(data.CustomText) ? $"+{data.Damage} SHD" : data.CustomText;
                color = new Color(0.32f, 0.78f, 0.99f, 1f);
                startScale = 1.05f;
                break;
            case FloatingPopupType.Dodge:
                text = string.IsNullOrEmpty(data.CustomText) ? "DODGE!" : data.CustomText;
                color = new Color(0.68f, 0.94f, 0.28f, 1f);
                startScale = 1.15f;
                break;
            case FloatingPopupType.Blocked:
                text = string.IsNullOrEmpty(data.CustomText) ? "BLOCKED" : data.CustomText;
                color = new Color(0.62f, 0.84f, 1.00f, 1f);
                startScale = 1.00f;
                break;
            case FloatingPopupType.Buff:
                text = string.IsNullOrEmpty(data.CustomText) ? "+BUFF" : data.CustomText;
                color = new Color(0.98f, 0.80f, 0.26f, 1f);
                startScale = 0.95f;
                break;
            default:
                text = string.IsNullOrEmpty(data.CustomText) ? data.Damage.ToString() : data.CustomText;
                color = new Color(1.00f, 0.90f, 0.34f, 1f);
                startScale = 1.00f;
                break;
        }

        // Độ lệch nhẹ để nhiều số nhảy liên tiếp (Cleave / Heal + Shield + Buff) không đè lên nhau
        float staggerY = (popupStaggerCounter % 3) * 0.16f;
        popupStaggerCounter++;
        Vector3 spawnPos = data.Position + new Vector3(Random.Range(-0.22f, 0.22f), staggerY, 0f);

        popup.transform.DOKill();
        tmp.DOKill();

        popup.transform.position = spawnPos;
        popup.transform.localScale = Vector3.one * (startScale * 0.65f);

        tmp.text = text;
        tmp.color = color;
        tmp.alpha = 1f;

        activePopups.Add(popup);

        popup.transform.DOScale(Vector3.one * startScale, 0.18f).SetEase(Ease.OutBack);
        popup.transform.DOMoveY(spawnPos.y + 0.95f, 0.72f).SetEase(Ease.OutCubic);
        tmp.DOFade(0f, 0.36f).SetDelay(0.36f).OnComplete(() =>
        {
            activePopups.Remove(popup);
            if (popupPool != null && popup != null)
            {
                popupPool.Release(popup);
            }
        });
    }
}