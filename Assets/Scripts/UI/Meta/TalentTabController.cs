using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Quản lý Tab Talent — bố cục gọn trong khung viền MainContent (lề 84px), có ô IconSlot chờ thay asset.
/// </summary>
public class TalentTabController : MonoBehaviour
{
    private class TalentCardView
    {
        public int TalentIndex;
        public RectTransform CardRect;
        public TextMeshProUGUI LevelText;
        public TextMeshProUGUI CurrentBonusText;
        public UnityEngine.UI.Button UpgradeButton;
        public UnityEngine.UI.Image UpgradeButtonBg;
        public TextMeshProUGUI UpgradeButtonLabel;
    }

    private Transform mainContent;
    private TextMeshProUGUI heroSummaryText;
    private TalentCardView[] cards;
    private bool isBuilt = false;
    private Coroutine toastRoutine;

    private static readonly string[] TalentIconTags = { "ATK", "DEF", "GEM" };

    public void Initialize()
    {
        if (isBuilt)
        {
            Refresh();
            return;
        }

        mainContent = transform.Find("MainContent");
        if (mainContent == null) mainContent = transform;

        BuildUI();
        isBuilt = true;
        Refresh();
    }

    private void OnEnable()
    {
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnResourcesChanged, HandleExternalUpdate);
        ObserverManager<EventID>.AddRegisterEvent(EventID.OnMetaProgressionChanged, HandleExternalUpdate);
        if (isBuilt) Refresh();
    }

    private void OnDisable()
    {
        ObserverManager<EventID>.RemoveAddListener(EventID.OnResourcesChanged, HandleExternalUpdate);
        ObserverManager<EventID>.RemoveAddListener(EventID.OnMetaProgressionChanged, HandleExternalUpdate);
    }

    private void HandleExternalUpdate(object param)
    {
        if (isBuilt && gameObject.activeInHierarchy) Refresh();
    }

    private void BuildUI()
    {
        Transform existing = mainContent.Find("TalentRuntimeBody");
        if (existing != null) Destroy(existing.gameObject);

        GameObject body = new GameObject("TalentRuntimeBody");
        body.transform.SetParent(mainContent, false);
        RectTransform bodyRt = body.AddComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(84f, 64f);
        bodyRt.offsetMax = new Vector2(-84f, -152f);

        VerticalLayoutGroup vlg = body.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 24f;
        vlg.padding = new RectOffset(0, 0, 8, 8);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        // 1. Thẻ tổng quan chỉ số Anh Hùng (gọn 2 dòng)
        RectTransform summaryCard = MetaUIHelper.CreateFramedCard(body.transform, "HeroSummaryCard", MetaUIHelper.CardBgDark, MetaUIHelper.CardBorderGold, 3f);
        LayoutElement summaryLe = summaryCard.gameObject.AddComponent<LayoutElement>();
        summaryLe.preferredHeight = 148f;

        Transform summaryFill = summaryCard.Find("Fill");
        heroSummaryText = MetaUIHelper.CreateText(summaryFill, "SummaryText", "", 26f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        heroSummaryText.enableAutoSizing = true;
        heroSummaryText.fontSizeMin = 18f;
        heroSummaryText.fontSizeMax = 26f;
        heroSummaryText.rectTransform.offsetMin = new Vector2(20f, 12f);
        heroSummaryText.rectTransform.offsetMax = new Vector2(-20f, -12f);

        // 2. 3 thẻ nâng cấp nhánh Talent gọn gàng có sẵn ô Icon để đè asset sau
        cards = new TalentCardView[MetaProgressionSystem.Talents.Length];
        for (int i = 0; i < MetaProgressionSystem.Talents.Length; i++)
        {
            cards[i] = BuildTalentCard(body.transform, MetaProgressionSystem.Talents[i]);
        }
    }

    private TalentCardView BuildTalentCard(Transform parent, TalentDefinition def)
    {
        RectTransform cardRt = MetaUIHelper.CreateFramedCard(parent, $"TalentCard_{def.Index}", MetaUIHelper.CardBgPanel, MetaUIHelper.CardBorderGold, 3f);
        LayoutElement le = cardRt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 205f;

        Transform fill = cardRt.Find("Fill");

        // Cột trái: Ô IconSlot 116x116 chờ thay sprite asset
        UnityEngine.UI.Image iconSlot = MetaUIHelper.CreatePanel(fill, "IconSlot", MetaUIHelper.IconSlotBg, false);
        RectTransform iconRt = iconSlot.rectTransform;
        iconRt.anchorMin = new Vector2(0f, 0.5f);
        iconRt.anchorMax = new Vector2(0f, 0.5f);
        iconRt.pivot = new Vector2(0f, 0.5f);
        iconRt.sizeDelta = new Vector2(116f, 116f);
        iconRt.anchoredPosition = new Vector2(24f, 0f);

        UnityEngine.UI.Image iconAccent = MetaUIHelper.CreatePanel(iconSlot.transform, "IconAccent", def.AccentColor, false);
        iconAccent.rectTransform.anchorMin = new Vector2(0f, 0f);
        iconAccent.rectTransform.anchorMax = new Vector2(1f, 0f);
        iconAccent.rectTransform.pivot = new Vector2(0.5f, 0f);
        iconAccent.rectTransform.sizeDelta = new Vector2(0f, 10f);

        string iconTag = def.Index < TalentIconTags.Length ? TalentIconTags[def.Index] : "TAL";
        TextMeshProUGUI iconTxt = MetaUIHelper.CreateText(iconSlot.transform, "IconPlaceholder", iconTag, 32f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        iconTxt.rectTransform.offsetMin = new Vector2(6f, 10f);
        iconTxt.rectTransform.offsetMax = new Vector2(-6f, -4f);

        // Cột giữa: Tên + Cấp + Mô tả + Chỉ số hiện tại
        GameObject infoCol = new GameObject("InfoColumn");
        infoCol.transform.SetParent(fill, false);
        RectTransform infoRt = infoCol.AddComponent<RectTransform>();
        infoRt.anchorMin = new Vector2(0f, 0f);
        infoRt.anchorMax = new Vector2(1f, 1f);
        infoRt.offsetMin = new Vector2(160f, 18f);
        infoRt.offsetMax = new Vector2(-235f, -18f);

        TextMeshProUGUI titleTxt = MetaUIHelper.CreateText(infoCol.transform, "Title", def.Title, 32f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.66f);
        titleTxt.rectTransform.anchorMax = new Vector2(0.72f, 1f);
        titleTxt.enableAutoSizing = true;
        titleTxt.fontSizeMin = 20f;
        titleTxt.fontSizeMax = 32f;

        TextMeshProUGUI levelTxt = MetaUIHelper.CreateText(infoCol.transform, "Level", "Lv.0/10", 26f, MetaUIHelper.TextGold, TextAlignmentOptions.Right, FontStyles.Bold);
        levelTxt.rectTransform.anchorMin = new Vector2(0.72f, 0.66f);
        levelTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
        levelTxt.enableAutoSizing = true;
        levelTxt.fontSizeMin = 18f;
        levelTxt.fontSizeMax = 26f;

        TextMeshProUGUI descTxt = MetaUIHelper.CreateText(infoCol.transform, "Desc", def.ShortDesc, 22f, MetaUIHelper.TextMuted, TextAlignmentOptions.Left, FontStyles.Normal);
        descTxt.rectTransform.anchorMin = new Vector2(0f, 0.34f);
        descTxt.rectTransform.anchorMax = new Vector2(1f, 0.64f);
        descTxt.enableAutoSizing = true;
        descTxt.fontSizeMin = 16f;
        descTxt.fontSizeMax = 22f;

        TextMeshProUGUI bonusTxt = MetaUIHelper.CreateText(infoCol.transform, "CurrentBonus", "", 24f, MetaUIHelper.TextCyan, TextAlignmentOptions.Left, FontStyles.Bold);
        bonusTxt.rectTransform.anchorMin = new Vector2(0f, 0f);
        bonusTxt.rectTransform.anchorMax = new Vector2(1f, 0.34f);
        bonusTxt.enableAutoSizing = true;
        bonusTxt.fontSizeMin = 17f;
        bonusTxt.fontSizeMax = 24f;

        // Cột phải: Nút UPGRADE gọn gàng
        UnityEngine.UI.Button btn = MetaUIHelper.CreateButton(
            fill,
            "UpgradeBtn",
            "UPGRADE\n40G",
            25f,
            MetaUIHelper.BtnGoldAction,
            out TextMeshProUGUI btnLabel,
            out UnityEngine.UI.Image btnBg);

        RectTransform btnRt = btn.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1f, 0.5f);
        btnRt.anchorMax = new Vector2(1f, 0.5f);
        btnRt.pivot = new Vector2(1f, 0.5f);
        btnRt.sizeDelta = new Vector2(195f, 108f);
        btnRt.anchoredPosition = new Vector2(-22f, 0f);

        int capturedIndex = def.Index;
        btn.onClick.AddListener(() => OnUpgradeClicked(capturedIndex));

        return new TalentCardView
        {
            TalentIndex = def.Index,
            CardRect = cardRt,
            LevelText = levelTxt,
            CurrentBonusText = bonusTxt,
            UpgradeButton = btn,
            UpgradeButtonBg = btnBg,
            UpgradeButtonLabel = btnLabel
        };
    }

    private void OnUpgradeClicked(int talentIndex)
    {
        bool ok = MetaProgressionSystem.TryUpgradeTalent(talentIndex, out string feedback);
        if (ok)
        {
            AudioManager.Instance?.PlayTargetSelected();
            if (cards != null && talentIndex >= 0 && talentIndex < cards.Length && cards[talentIndex].CardRect != null)
            {
                cards[talentIndex].CardRect.DOKill();
                cards[talentIndex].CardRect.localScale = Vector3.one;
                cards[talentIndex].CardRect.DOPunchScale(Vector3.one * 0.03f, 0.20f, 6, 0.6f).SetUpdate(true);
            }
        }
        else
        {
            AudioManager.Instance?.PlaySoundButtonClick();
        }

        MetaUIHelper.ShowToast(this, transform, ref toastRoutine, feedback, ok);
        Refresh();
    }

    public void Refresh()
    {
        if (!isBuilt) return;

        SecureGameData data = DataManager.Instance?.GameData;
        PlayerTotalStatsPreview stats = MetaProgressionSystem.CalculateTotalPlayerStats(MetaUIHelper.GetPlayerBaseStatSO(), data);

        if (heroSummaryText != null)
        {
            int gemPct = Mathf.RoundToInt(stats.GemPowerBonusPercent * 100f);
            heroSummaryText.text =
                $"<color=#FAD15C>HERO LV.{stats.PlayerLevel}</color>   |   HP <color=#4ADE80>{stats.MaxHealth:0.#}</color>   |   SHD <color=#7DD3FC>{stats.Shield:0.#}</color>   |   DMG <color=#FCA5A5>{stats.Damage:0.#}</color>\n" +
                $"Crit {stats.CritRate:0.#}% ({stats.CritDamage:0.#}%)   |   Dodge {stats.Dodge:0.#}%   |   Gem <color=#D8B4FE>+{gemPct}%</color>";
        }

        if (cards == null || data == null) return;

        for (int i = 0; i < cards.Length; i++)
        {
            TalentCardView view = cards[i];
            TalentDefinition def = MetaProgressionSystem.Talents[i];
            int lv = data.GetTalentLevel(i);
            bool isMax = lv >= def.MaxLevel;
            int cost = MetaProgressionSystem.GetTalentUpgradeCost(i, lv);
            bool canAfford = !isMax && data.Gold >= cost;

            view.LevelText.text = $"Lv.{lv}/{def.MaxLevel}";

            if (i == 0) view.CurrentBonusText.text = $"Now: +{(lv * 1.5f):0.#} DMG, +{(lv * 0.5f):0.#}% Crit";
            else if (i == 1) view.CurrentBonusText.text = $"Now: +{lv * 14} HP, +{lv * 2} SHD";
            else view.CurrentBonusText.text = $"Now: +{lv * 5}% Gem Power";

            if (isMax)
            {
                view.UpgradeButtonLabel.text = "MAX";
                view.UpgradeButtonBg.color = MetaUIHelper.BtnConfirmGreen;
                view.UpgradeButton.interactable = false;
            }
            else
            {
                view.UpgradeButtonLabel.text = $"UPGRADE\n{cost} G";
                view.UpgradeButtonBg.color = canAfford ? MetaUIHelper.BtnGoldAction : MetaUIHelper.BtnDisabled;
                view.UpgradeButton.interactable = true;
            }
        }
    }
}
