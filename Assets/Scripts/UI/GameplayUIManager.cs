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
    }

    private void OnDisable()
    {
        ObserverManager<EventID>.RemoveAddListener(EventID.OnUpdatePlayerStats, UpdatePlayerStats);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnUpdateRoundCount, UpdateRoundCount);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnShowDamagePopup, HandleShowDamagePopup);
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
        EnsureNotificationBanner();

        if (speedToggleButton != null)
        {
            speedToggleButton.onClick.RemoveListener(OnSpeedToggleClicked);
            speedToggleButton.onClick.AddListener(OnSpeedToggleClicked);
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
        if (notificationBannerGroup != null) notificationBannerGroup.DOKill();
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
            speedToggleButton = existing.GetComponent<UnityEngine.UI.Button>();
            speedButtonBg = existing.GetComponent<UnityEngine.UI.Image>();
            speedButtonText = existing.GetComponentInChildren<TextMeshProUGUI>();
            return;
        }

        RectTransform pauseRect = pauseButton.GetComponent<RectTransform>();
        float pauseWidth = pauseRect != null && pauseRect.sizeDelta.x > 0f ? pauseRect.sizeDelta.x : 75f;

        GameObject btnObj = new GameObject("SpeedToggleButton");
        btnObj.transform.SetParent(topBarParent, false);

        RectTransform btnRect = btnObj.AddComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0f);
        btnRect.anchorMax = new Vector2(1f, 1f);
        btnRect.pivot = new Vector2(1f, 0.5f);
        btnRect.sizeDelta = new Vector2(96f, 0f);
        btnRect.anchoredPosition = new Vector2(-(pauseWidth + 14f), 0f);

        speedButtonBg = btnObj.AddComponent<UnityEngine.UI.Image>();
        speedButtonBg.sprite = CharacterWorldHUD.GetRuntimeWhiteSprite();
        speedButtonBg.color = SpeedNormalBgColor;

        speedToggleButton = btnObj.AddComponent<UnityEngine.UI.Button>();
        speedToggleButton.targetGraphic = speedButtonBg;

        GameObject labelObj = new GameObject("Label");
        labelObj.transform.SetParent(btnObj.transform, false);
        RectTransform labelRect = labelObj.AddComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = Vector2.zero;
        labelRect.offsetMax = Vector2.zero;

        speedButtonText = labelObj.AddComponent<TextMeshProUGUI>();
        if (DefaultFont != null) speedButtonText.font = DefaultFont;
        speedButtonText.text = "1x";
        speedButtonText.fontSize = 34f;
        speedButtonText.fontStyle = FontStyles.Bold;
        speedButtonText.alignment = TextAlignmentOptions.Center;
        speedButtonText.color = Color.white;
        speedButtonText.raycastTarget = false;
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
        if (turnIcons == null) return;
        for (int i = 0; i < turnIcons.Length; i++)
        {
            if (turnIcons[i] != null) turnIcons[i].SetActive(i < currentTurns);
        }
    }

    public void ShowEnemyInfo(Enemy enemy)
    {
        if (enemy == null) return;
        currentShownEnemy = enemy;
        if (enemyInfoPanel != null) enemyInfoPanel.SetActive(true);
        if (enemySprite != null) enemySprite.sprite = enemy.CharacterSprite;
        RefreshEnemyHP(enemy);
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
            if (hpText != null) hpText.text = $"{player.CurrentHealth}/{player.CurrentMaxHealth}";
            if (shieldText != null) shieldText.text = $"{player.CurrentShield}";
            if (damageText != null) damageText.text = $"{player.CurrentDamage}";
            if (critRateText != null) critRateText.text = $"{player.CurrentCritRate}%";
            if (critDamageText != null) critDamageText.text = $"{player.CurrentCritDamage}%";
            if (blockRateText != null) blockRateText.text = $"{player.CurrentBlockRate}%";

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
        if (roundText != null) roundText.text = round.ToString();
    }

    public void RefreshEnemyHP(Enemy enemy)
    {
        if (enemy == null) return;
        if (currentShownEnemy != null && enemy != currentShownEnemy) return;

        if (enemyHpText != null)
            enemyHpText.text = $"{enemy.CurrentHealth}/{enemy.CurrentMaxHealth}";
        if (enemyHpFill != null)
        {
            float fillAmount = enemy.CurrentHealth / Mathf.Max(1f, enemy.CurrentMaxHealth);
            enemyHpFill.DOFillAmount(fillAmount, 0.3f).SetEase(Ease.OutQuad);
        }
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