using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Thanh HP / Shield gắn trực tiếp dưới chân nhân vật trong không gian 2D, hiển thị số máu ngay bên trong thanh.
/// Tự động khởi tạo nếu chưa gán sẵn trong Prefab để tương thích với mọi đối tượng từ Object Pool.
/// </summary>
public class CharacterWorldHUD : MonoBehaviour
{
    [Header("Layout")]
    [SerializeField] private Vector3 barLocalOffset = new Vector3(0f, -0.52f, 0f);
    [SerializeField] private string sortingLayerName = "Character";
    [SerializeField] private int sortingOrder = 20;

    private static Sprite cachedWhiteSprite;

    private GameObject hudRoot;
    private UnityEngine.UI.Image hpFillImage;
    private UnityEngine.UI.Image shieldFillImage;
    private TextMeshProUGUI hpValueText;

    private bool isInitialized = false;
    private bool isEnemyStyle = false;

    private static readonly Color PlayerHpColor = new Color(0.13f, 0.62f, 0.34f, 1f);
    private static readonly Color EnemyHpColor = new Color(0.76f, 0.20f, 0.18f, 1f);
    private static readonly Color ShieldBarColor = new Color(0.22f, 0.74f, 0.97f, 1f);
    private static readonly Color BarBgColor = new Color(0.08f, 0.10f, 0.15f, 0.94f);

    public static Sprite GetRuntimeWhiteSprite()
    {
        if (cachedWhiteSprite != null) return cachedWhiteSprite;

        Texture2D tex = new Texture2D(4, 4, TextureFormat.RGBA32, false);
        Color[] pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.white;
        tex.SetPixels(pixels);
        tex.Apply();

        cachedWhiteSprite = Sprite.Create(tex, new Rect(0, 0, 4, 4), new Vector2(0.5f, 0.5f), 100f);
        cachedWhiteSprite.name = "RuntimeWhiteSprite";
        return cachedWhiteSprite;
    }

    public void Initialize(bool isEnemy)
    {
        isEnemyStyle = isEnemy;

        if (!isInitialized)
        {
            BuildHUDHierarchy();
            isInitialized = true;
        }

        if (hpFillImage != null)
        {
            hpFillImage.color = isEnemyStyle ? EnemyHpColor : PlayerHpColor;
        }

        SetVisible(true);
    }

    public void SetVisible(bool visible)
    {
        if (hudRoot != null)
        {
            hudRoot.SetActive(visible);
        }
    }

    public void RefreshStats(float currentHp, float maxHp, float currentShield)
    {
        if (!isInitialized) return;

        float safeMaxHp = Mathf.Max(1f, maxHp);
        float hpRatio = Mathf.Clamp01(currentHp / safeMaxHp);
        float shieldRatio = Mathf.Clamp01(currentShield / (safeMaxHp * 0.5f));

        if (hpFillImage != null)
        {
            hpFillImage.DOKill();
            hpFillImage.DOFillAmount(hpRatio, 0.25f).SetEase(Ease.OutCubic);
        }

        if (shieldFillImage != null)
        {
            shieldFillImage.gameObject.SetActive(currentShield > 0.05f);
            shieldFillImage.DOKill();
            shieldFillImage.DOFillAmount(shieldRatio, 0.25f).SetEase(Ease.OutCubic);
        }

        if (hpValueText != null)
        {
            int displayHp = Mathf.CeilToInt(Mathf.Max(0f, currentHp));
            int displayShield = Mathf.RoundToInt(Mathf.Max(0f, currentShield));
            hpValueText.text = displayShield > 0
                ? $"{displayHp} <color=#7DD3FC>+{displayShield}</color>"
                : displayHp.ToString();
        }
    }

    public void UpdateIntent(bool isActingNext, int supportCooldown)
    {
        // Giữ hook để mở rộng icon đồ họa Intent riêng sau này (đã bỏ nhãn chữ NEXT/CD theo phản hồi UI).
    }

    private void BuildHUDHierarchy()
    {
        Sprite whiteSprite = GetRuntimeWhiteSprite();
        TMP_FontAsset fontAsset = ResolveFontAsset();

        hudRoot = new GameObject("WorldHUDCanvas");
        hudRoot.transform.SetParent(transform, false);
        hudRoot.transform.localPosition = Vector3.zero;
        hudRoot.transform.localRotation = Quaternion.identity;
        hudRoot.transform.localScale = Vector3.one * 0.01f;

        Canvas canvas = hudRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.overrideSorting = true;
        canvas.sortingLayerName = sortingLayerName;
        canvas.sortingOrder = sortingOrder;

        RectTransform canvasRect = hudRoot.GetComponent<RectTransform>();
        canvasRect.sizeDelta = new Vector2(100f, 40f);

        // Khung thanh HP & Shield dưới chân nhân vật
        GameObject barContainer = new GameObject("BarContainer");
        barContainer.transform.SetParent(hudRoot.transform, false);
        RectTransform barRect = barContainer.AddComponent<RectTransform>();
        barRect.anchoredPosition = new Vector2(barLocalOffset.x * 100f, barLocalOffset.y * 100f);
        barRect.sizeDelta = new Vector2(88f, 22f);

        UnityEngine.UI.Image bgImg = barContainer.AddComponent<UnityEngine.UI.Image>();
        bgImg.sprite = whiteSprite;
        bgImg.color = BarBgColor;
        bgImg.raycastTarget = false;

        // Thanh máu (HP Fill) chiếm 78% chiều cao phía trên
        GameObject hpFillObj = new GameObject("HPFill");
        hpFillObj.transform.SetParent(barContainer.transform, false);
        RectTransform hpFillRect = hpFillObj.AddComponent<RectTransform>();
        hpFillRect.anchorMin = new Vector2(0f, 0.22f);
        hpFillRect.anchorMax = new Vector2(1f, 1f);
        hpFillRect.offsetMin = new Vector2(2f, 0f);
        hpFillRect.offsetMax = new Vector2(-2f, -2f);

        hpFillImage = hpFillObj.AddComponent<UnityEngine.UI.Image>();
        hpFillImage.sprite = whiteSprite;
        hpFillImage.type = UnityEngine.UI.Image.Type.Filled;
        hpFillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        hpFillImage.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
        hpFillImage.fillAmount = 1f;
        hpFillImage.color = isEnemyStyle ? EnemyHpColor : PlayerHpColor;
        hpFillImage.raycastTarget = false;

        // Dải Khiên (Shield Fill strip) mỏng phía dưới trong cùng khung
        GameObject shieldFillObj = new GameObject("ShieldFill");
        shieldFillObj.transform.SetParent(barContainer.transform, false);
        RectTransform shieldFillRect = shieldFillObj.AddComponent<RectTransform>();
        shieldFillRect.anchorMin = new Vector2(0f, 0f);
        shieldFillRect.anchorMax = new Vector2(1f, 0.22f);
        shieldFillRect.offsetMin = new Vector2(2f, 1.5f);
        shieldFillRect.offsetMax = new Vector2(-2f, 0f);

        shieldFillImage = shieldFillObj.AddComponent<UnityEngine.UI.Image>();
        shieldFillImage.sprite = whiteSprite;
        shieldFillImage.type = UnityEngine.UI.Image.Type.Filled;
        shieldFillImage.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        shieldFillImage.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
        shieldFillImage.fillAmount = 0f;
        shieldFillImage.color = ShieldBarColor;
        shieldFillImage.raycastTarget = false;

        // Số HP + Shield đặt gọn gàng trực tiếp bên trong thanh HP
        GameObject hpTextObj = new GameObject("HPText");
        hpTextObj.transform.SetParent(barContainer.transform, false);
        RectTransform hpTextRect = hpTextObj.AddComponent<RectTransform>();
        hpTextRect.anchorMin = new Vector2(0f, 0.20f);
        hpTextRect.anchorMax = new Vector2(1f, 1f);
        hpTextRect.offsetMin = new Vector2(3f, 0f);
        hpTextRect.offsetMax = new Vector2(-3f, -1f);

        hpValueText = hpTextObj.AddComponent<TextMeshProUGUI>();
        if (fontAsset != null) hpValueText.font = fontAsset;
        hpValueText.enableAutoSizing = true;
        hpValueText.fontSizeMin = 8f;
        hpValueText.fontSizeMax = 13f;
        hpValueText.fontStyle = FontStyles.Bold;
        hpValueText.alignment = TextAlignmentOptions.Center;
        hpValueText.color = Color.white;
        hpValueText.raycastTarget = false;
        hpValueText.enableWordWrapping = false;
    }

    private TMP_FontAsset ResolveFontAsset()
    {
        if (BattleManager.Instance != null && BattleManager.Instance.GameplayUIManager != null)
        {
            TMP_FontAsset uiFont = BattleManager.Instance.GameplayUIManager.DefaultFont;
            if (uiFont != null) return uiFont;
        }

        return TMP_Settings.defaultFontAsset;
    }

    private void OnDisable()
    {
        if (hpFillImage != null) hpFillImage.DOKill();
        if (shieldFillImage != null) shieldFillImage.DOKill();
    }

    private void OnDestroy()
    {
        if (hpFillImage != null) hpFillImage.DOKill();
        if (shieldFillImage != null) shieldFillImage.DOKill();
    }
}
