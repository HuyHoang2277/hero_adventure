using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Quản lý Tab Reward — bố cục gọn trong khung viền MainContent (lề 84px), mỗi dòng có IconSlot chờ thay asset.
/// </summary>
public class RewardTabController : MonoBehaviour
{
    private class MilestoneRowView
    {
        public int Index;
        public RectTransform RowRect;
        public UnityEngine.UI.Image ProgressFill;
        public TextMeshProUGUI ProgressText;
        public UnityEngine.UI.Button ClaimButton;
        public UnityEngine.UI.Image ClaimButtonBg;
        public TextMeshProUGUI ClaimButtonLabel;
    }

    private TextMeshProUGUI headerSummaryText;
    private readonly List<MilestoneRowView> rows = new List<MilestoneRowView>();
    private bool isBuilt = false;
    private Coroutine toastRoutine;

    public void Initialize()
    {
        if (isBuilt)
        {
            Refresh();
            return;
        }

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
        Transform mainContent = transform.Find("MainContent");
        if (mainContent == null) mainContent = transform;

        Transform existing = mainContent.Find("RewardRuntimeBody");
        if (existing != null) Destroy(existing.gameObject);

        GameObject body = new GameObject("RewardRuntimeBody");
        body.transform.SetParent(mainContent, false);
        RectTransform bodyRt = body.AddComponent<RectTransform>();
        bodyRt.anchorMin = Vector2.zero;
        bodyRt.anchorMax = Vector2.one;
        bodyRt.offsetMin = new Vector2(84f, 64f);
        bodyRt.offsetMax = new Vector2(-84f, -152f);

        // 1. Thanh tổng hợp thành tích ở trên cùng
        RectTransform headerCard = MetaUIHelper.CreateFramedCard(body.transform, "MilestoneHeader", MetaUIHelper.CardBgDark, MetaUIHelper.CardBorderGold, 3f);
        headerCard.anchorMin = new Vector2(0f, 1f);
        headerCard.anchorMax = new Vector2(1f, 1f);
        headerCard.pivot = new Vector2(0.5f, 1f);
        headerCard.sizeDelta = new Vector2(0f, 76f);
        headerCard.anchoredPosition = Vector2.zero;

        headerSummaryText = MetaUIHelper.CreateText(headerCard.Find("Fill"), "SummaryTxt", "", 27f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        headerSummaryText.enableAutoSizing = true;
        headerSummaryText.fontSizeMin = 18f;
        headerSummaryText.fontSizeMax = 27f;

        // 2. ScrollView chứa danh sách 8 Mốc thưởng
        GameObject scrollObj = new GameObject("MilestoneScrollView");
        scrollObj.transform.SetParent(body.transform, false);
        RectTransform scrollRt = scrollObj.AddComponent<RectTransform>();
        scrollRt.anchorMin = Vector2.zero;
        scrollRt.anchorMax = Vector2.one;
        scrollRt.offsetMin = Vector2.zero;
        scrollRt.offsetMax = new Vector2(0f, -92f);

        ScrollRect scrollRect = scrollObj.AddComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Elastic;
        scrollRect.scrollSensitivity = 35f;

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        RectTransform vpRt = viewport.AddComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = Vector2.zero;
        vpRt.offsetMax = Vector2.zero;
        viewport.AddComponent<RectMask2D>();

        UnityEngine.UI.Image vpBg = viewport.AddComponent<UnityEngine.UI.Image>();
        vpBg.sprite = MetaUIHelper.GetWhiteSprite();
        vpBg.color = new Color(0f, 0f, 0f, 0.01f);

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        RectTransform contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0f, 1f);
        contentRt.anchorMax = new Vector2(1f, 1f);
        contentRt.pivot = new Vector2(0.5f, 1f);
        contentRt.anchoredPosition = Vector2.zero;
        contentRt.sizeDelta = Vector2.zero;

        VerticalLayoutGroup vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 16f;
        vlg.padding = new RectOffset(0, 0, 6, 16);
        vlg.childControlWidth = true;
        vlg.childControlHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = false;

        ContentSizeFitter csf = content.AddComponent<ContentSizeFitter>();
        csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scrollRect.viewport = vpRt;
        scrollRect.content = contentRt;

        rows.Clear();
        for (int i = 0; i < MetaProgressionSystem.Milestones.Length; i++)
        {
            rows.Add(BuildMilestoneRow(content.transform, MetaProgressionSystem.Milestones[i]));
        }
    }

    private MilestoneRowView BuildMilestoneRow(Transform parent, RewardMilestoneDefinition m)
    {
        RectTransform rowRt = MetaUIHelper.CreateFramedCard(parent, $"Milestone_{m.Index}", MetaUIHelper.CardBgPanel, MetaUIHelper.CardBorderGold, 2.5f);
        LayoutElement le = rowRt.gameObject.AddComponent<LayoutElement>();
        le.preferredHeight = 148f;

        Transform fill = rowRt.Find("Fill");
        Color tagColor = m.IsEndlessMilestone ? new Color(0.58f, 0.30f, 0.78f, 1f) : new Color(0.84f, 0.56f, 0.14f, 1f);

        // Cột trái: Ô IconSlot 84x84 chờ thay asset
        UnityEngine.UI.Image iconSlot = MetaUIHelper.CreatePanel(fill, "IconSlot", MetaUIHelper.IconSlotBg, false);
        iconSlot.rectTransform.anchorMin = new Vector2(0f, 0.5f);
        iconSlot.rectTransform.anchorMax = new Vector2(0f, 0.5f);
        iconSlot.rectTransform.pivot = new Vector2(0f, 0.5f);
        iconSlot.rectTransform.sizeDelta = new Vector2(84f, 84f);
        iconSlot.rectTransform.anchoredPosition = new Vector2(18f, 0f);

        string badgeTag = m.IsEndlessMilestone ? $"R{m.TargetValue}" : $"{m.TargetValue}*";
        MetaUIHelper.CreateText(iconSlot.transform, "IconPlaceholder", badgeTag, 26f, tagColor, TextAlignmentOptions.Center, FontStyles.Bold);

        // Cột giữa: Tiêu đề + Phần thưởng + Thanh tiến độ
        GameObject infoCol = new GameObject("InfoColumn");
        infoCol.transform.SetParent(fill, false);
        RectTransform infoRt = infoCol.AddComponent<RectTransform>();
        infoRt.anchorMin = Vector2.zero;
        infoRt.anchorMax = Vector2.one;
        infoRt.offsetMin = new Vector2(118f, 14f);
        infoRt.offsetMax = new Vector2(-196f, -14f);

        TextMeshProUGUI titleTxt = MetaUIHelper.CreateText(infoCol.transform, "Title", m.ConditionText, 25f, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
        titleTxt.rectTransform.anchorMin = new Vector2(0f, 0.66f);
        titleTxt.rectTransform.anchorMax = new Vector2(1f, 1f);
        titleTxt.enableAutoSizing = true;
        titleTxt.fontSizeMin = 17f;
        titleTxt.fontSizeMax = 25f;

        string rewardLine = $"+{m.GoldReward}G  +{m.DiamondReward}D";
        if (m.BonusEquipmentId >= 0 && m.BonusEquipmentId < MetaProgressionSystem.Equipments.Length)
        {
            rewardLine += $"  +{MetaProgressionSystem.Equipments[m.BonusEquipmentId].Name}";
        }
        TextMeshProUGUI rewardTxt = MetaUIHelper.CreateText(infoCol.transform, "RewardLine", rewardLine, 21f, MetaUIHelper.TextGold, TextAlignmentOptions.Left, FontStyles.Bold);
        rewardTxt.rectTransform.anchorMin = new Vector2(0f, 0.34f);
        rewardTxt.rectTransform.anchorMax = new Vector2(1f, 0.64f);
        rewardTxt.enableAutoSizing = true;
        rewardTxt.fontSizeMin = 15f;
        rewardTxt.fontSizeMax = 21f;

        UnityEngine.UI.Image progBg = MetaUIHelper.CreatePanel(infoCol.transform, "ProgBg", MetaUIHelper.IconSlotBg, false);
        progBg.rectTransform.anchorMin = new Vector2(0f, 0f);
        progBg.rectTransform.anchorMax = new Vector2(1f, 0.28f);
        progBg.rectTransform.offsetMin = Vector2.zero;
        progBg.rectTransform.offsetMax = Vector2.zero;

        UnityEngine.UI.Image progFill = MetaUIHelper.CreatePanel(progBg.transform, "ProgFill", tagColor, false);
        progFill.type = UnityEngine.UI.Image.Type.Filled;
        progFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
        progFill.fillOrigin = (int)UnityEngine.UI.Image.OriginHorizontal.Left;
        progFill.fillAmount = 0f;
        progFill.rectTransform.anchorMin = Vector2.zero;
        progFill.rectTransform.anchorMax = Vector2.one;
        progFill.rectTransform.offsetMin = new Vector2(2f, 2f);
        progFill.rectTransform.offsetMax = new Vector2(-2f, -2f);

        TextMeshProUGUI progTxt = MetaUIHelper.CreateText(progBg.transform, "ProgTxt", "0/3", 17f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);

        // Cột phải: Nút CLAIM
        UnityEngine.UI.Button claimBtn = MetaUIHelper.CreateButton(fill, "ClaimBtn", "0/3", 23f, MetaUIHelper.BtnDisabled, out TextMeshProUGUI claimLabel, out UnityEngine.UI.Image claimBg);
        RectTransform btnRt = claimBtn.GetComponent<RectTransform>();
        btnRt.anchorMin = new Vector2(1f, 0.5f);
        btnRt.anchorMax = new Vector2(1f, 0.5f);
        btnRt.pivot = new Vector2(1f, 0.5f);
        btnRt.sizeDelta = new Vector2(162f, 76f);
        btnRt.anchoredPosition = new Vector2(-18f, 0f);

        int capturedIdx = m.Index;
        claimBtn.onClick.AddListener(() => OnClaimClicked(capturedIdx));

        return new MilestoneRowView
        {
            Index = m.Index,
            RowRect = rowRt,
            ProgressFill = progFill,
            ProgressText = progTxt,
            ClaimButton = claimBtn,
            ClaimButtonBg = claimBg,
            ClaimButtonLabel = claimLabel
        };
    }

    private void OnClaimClicked(int index)
    {
        bool ok = MetaProgressionSystem.TryClaimMilestone(index, out string feedback);
        if (ok)
        {
            AudioManager.Instance?.PlaySoundWin();
            if (index >= 0 && index < rows.Count && rows[index].RowRect != null)
            {
                rows[index].RowRect.DOKill();
                rows[index].RowRect.localScale = Vector3.one;
                rows[index].RowRect.DOPunchScale(Vector3.one * 0.03f, 0.20f, 6, 0.6f).SetUpdate(true);
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
        if (data == null) return;

        int totalStars = data.GetTotalStars();
        int bestEndless = data.BestEndlessRound;

        if (headerSummaryText != null)
        {
            headerSummaryText.text = $"<color=#FAD15C>STARS: {totalStars} / 15</color>     |     <color=#D8B4FE>ENDLESS BEST: R{bestEndless}</color>";
        }

        for (int i = 0; i < rows.Count; i++)
        {
            MilestoneRowView view = rows[i];
            RewardMilestoneDefinition m = MetaProgressionSystem.Milestones[i];
            int current = MetaProgressionSystem.GetMilestoneCurrentProgress(m, data);
            bool claimed = data.IsRewardClaimed(i);
            bool ready = !claimed && current >= m.TargetValue;

            view.ProgressFill.fillAmount = Mathf.Clamp01((float)current / Mathf.Max(1, m.TargetValue));
            view.ProgressText.text = $"{Mathf.Min(current, m.TargetValue)} / {m.TargetValue}";

            if (claimed)
            {
                view.ClaimButtonLabel.text = "DONE";
                view.ClaimButtonBg.color = MetaUIHelper.BtnConfirmGreen;
                view.ClaimButton.interactable = false;
            }
            else if (ready)
            {
                view.ClaimButtonLabel.text = "CLAIM";
                view.ClaimButtonBg.color = MetaUIHelper.BtnGoldAction;
                view.ClaimButton.interactable = true;
            }
            else
            {
                view.ClaimButtonLabel.text = $"{current}/{m.TargetValue}";
                view.ClaimButtonBg.color = MetaUIHelper.BtnDisabled;
                view.ClaimButton.interactable = false;
            }
        }
    }
}
