using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Tiện ích dựng UI gọn gàng theo bảng màu thủy mặc - vàng đồng của Hero's Adventure,
/// tách rõ khung nền/viền/icon để dễ dàng thay thế asset đồ họa về sau.
/// </summary>
public static class MetaUIHelper
{
    public static readonly Color CardBgDark = new Color(0.10f, 0.10f, 0.13f, 0.90f);
    public static readonly Color CardBgPanel = new Color(0.14f, 0.14f, 0.18f, 0.92f);
    public static readonly Color CardBorderGold = new Color(0.76f, 0.58f, 0.25f, 0.72f);
    public static readonly Color IconSlotBg = new Color(0.08f, 0.08f, 0.11f, 0.95f);

    public static readonly Color BtnConfirmGreen = new Color(0.18f, 0.56f, 0.32f, 1f);
    public static readonly Color BtnGoldAction = new Color(0.80f, 0.53f, 0.14f, 1f);
    public static readonly Color BtnCrimsonAction = new Color(0.68f, 0.22f, 0.20f, 1f);
    public static readonly Color BtnDisabled = new Color(0.26f, 0.27f, 0.31f, 0.92f);

    public static readonly Color TextMuted = new Color(0.78f, 0.76f, 0.72f, 1f);
    public static readonly Color TextGold = new Color(0.98f, 0.82f, 0.36f, 1f);
    public static readonly Color TextCyan = new Color(0.50f, 0.84f, 0.98f, 1f);

    private static CharacterInfoSO cachedPlayerBaseSO;

    public static TMP_FontAsset GetFont()
    {
        if (GameSceneManager.Instance != null && GameSceneManager.Instance.MenuDefaultFont != null)
        {
            return GameSceneManager.Instance.MenuDefaultFont;
        }
        return TMP_Settings.defaultFontAsset;
    }

    public static Sprite GetWhiteSprite()
    {
        return CharacterWorldHUD.GetRuntimeWhiteSprite();
    }

    public static CharacterInfoSO GetPlayerBaseStatSO()
    {
        if (cachedPlayerBaseSO == null)
        {
            cachedPlayerBaseSO = Resources.Load<CharacterInfoSO>("SO/CharacterInfo/Player1");
        }
        return cachedPlayerBaseSO;
    }

    public static TextMeshProUGUI CreateText(
        Transform parent,
        string name,
        string content,
        float fontSize,
        Color color,
        TextAlignmentOptions alignment = TextAlignmentOptions.Left,
        FontStyles style = FontStyles.Bold)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        RectTransform rt = obj.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        TextMeshProUGUI tmp = obj.AddComponent<TextMeshProUGUI>();
        TMP_FontAsset font = GetFont();
        if (font != null) tmp.font = font;
        tmp.text = content;
        tmp.fontSize = fontSize;
        tmp.fontStyle = style;
        tmp.color = color;
        tmp.alignment = alignment;
        tmp.raycastTarget = false;
        return tmp;
    }

    public static UnityEngine.UI.Image CreatePanel(Transform parent, string name, Color color, bool raycastTarget = false)
    {
        GameObject obj = new GameObject(name);
        obj.transform.SetParent(parent, false);
        obj.AddComponent<RectTransform>();

        UnityEngine.UI.Image img = obj.AddComponent<UnityEngine.UI.Image>();
        img.sprite = GetWhiteSprite();
        img.color = color;
        img.raycastTarget = raycastTarget;
        return img;
    }

    /// <summary>
    /// Tạo thẻ có viền mảnh (Border + InnerFill) sẵn sàng để sau này thay Sprite trực tiếp lên thẻ.
    /// </summary>
    public static RectTransform CreateFramedCard(Transform parent, string name, Color fillColor, Color borderColor, float borderThickness = 3f)
    {
        UnityEngine.UI.Image borderImg = CreatePanel(parent, name, borderColor, false);
        UnityEngine.UI.Image fillImg = CreatePanel(borderImg.transform, "Fill", fillColor, false);
        RectTransform fillRt = fillImg.rectTransform;
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = new Vector2(borderThickness, borderThickness);
        fillRt.offsetMax = new Vector2(-borderThickness, -borderThickness);
        return borderImg.rectTransform;
    }

    public static UnityEngine.UI.Button CreateButton(
        Transform parent,
        string name,
        string label,
        float fontSize,
        Color bgColor,
        out TextMeshProUGUI labelText,
        out UnityEngine.UI.Image bgImage)
    {
        bgImage = CreatePanel(parent, name, bgColor, true);
        UnityEngine.UI.Button btn = bgImage.gameObject.AddComponent<UnityEngine.UI.Button>();
        btn.targetGraphic = bgImage;

        labelText = CreateText(bgImage.transform, "Label", label, fontSize, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        labelText.enableAutoSizing = true;
        labelText.fontSizeMin = Mathf.Max(15f, fontSize * 0.55f);
        labelText.fontSizeMax = fontSize;
        labelText.rectTransform.offsetMin = new Vector2(8f, 4f);
        labelText.rectTransform.offsetMax = new Vector2(-8f, -4f);

        return btn;
    }

    public static void ShowToast(MonoBehaviour host, Transform toastParent, ref Coroutine activeRoutine, string message, bool isSuccess)
    {
        if (host == null || toastParent == null || string.IsNullOrEmpty(message)) return;

        Transform existing = toastParent.Find("MetaToastBanner");
        CanvasGroup cg;
        UnityEngine.UI.Image bg;
        TextMeshProUGUI txt;

        if (existing != null)
        {
            cg = existing.GetComponent<CanvasGroup>();
            bg = existing.GetComponent<UnityEngine.UI.Image>();
            txt = existing.GetComponentInChildren<TextMeshProUGUI>();
        }
        else
        {
            bg = CreatePanel(toastParent, "MetaToastBanner", CardBgDark, false);
            RectTransform rt = bg.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(680f, 68f);
            rt.anchoredPosition = new Vector2(0f, -215f);

            cg = bg.gameObject.AddComponent<CanvasGroup>();
            cg.interactable = false;
            cg.blocksRaycasts = false;

            txt = CreateText(bg.transform, "ToastText", "", 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            txt.enableAutoSizing = true;
            txt.fontSizeMin = 18f;
            txt.fontSizeMax = 26f;
            txt.rectTransform.offsetMin = new Vector2(18f, 6f);
            txt.rectTransform.offsetMax = new Vector2(-18f, -6f);
        }

        if (activeRoutine != null) host.StopCoroutine(activeRoutine);
        activeRoutine = host.StartCoroutine(ToastRoutine(cg, bg, txt, message, isSuccess));
    }

    public static void HideToast(Transform toastParent)
    {
        if (toastParent == null) return;
        Transform existing = toastParent.Find("MetaToastBanner");
        if (existing != null)
        {
            CanvasGroup cg = existing.GetComponent<CanvasGroup>();
            if (cg != null) cg.DOKill();
            existing.DOKill();
            existing.gameObject.SetActive(false);
        }
    }

    private static IEnumerator ToastRoutine(CanvasGroup cg, UnityEngine.UI.Image bg, TextMeshProUGUI txt, string msg, bool success)
    {
        cg.gameObject.SetActive(true);
        cg.transform.SetAsLastSibling();
        cg.DOKill();
        cg.transform.DOKill();

        bg.color = success ? new Color(0.14f, 0.52f, 0.30f, 0.96f) : new Color(0.70f, 0.20f, 0.18f, 0.96f);
        txt.text = msg;
        cg.alpha = 0f;
        cg.transform.localScale = Vector3.one * 0.92f;

        cg.DOFade(1f, 0.16f).SetUpdate(true);
        cg.transform.DOScale(1f, 0.20f).SetEase(Ease.OutBack).SetUpdate(true);

        yield return new WaitForSecondsRealtime(1.25f);

        yield return cg.DOFade(0f, 0.20f).SetUpdate(true).WaitForCompletion();
        cg.gameObject.SetActive(false);
    }
}
