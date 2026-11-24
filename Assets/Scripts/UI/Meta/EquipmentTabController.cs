using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Quản lý Tab Equipment — bố cục tách biệt sạch sẽ để dễ đè asset:
/// - 6 ô EquippedItem (Left/Right) chỉ gắn nhãn nhỏ gọn ở đáy, chừa nguyên khoảng icon.
/// - Giữa EquippedItem đặt thẻ Chi tiết & nút Equip/Upgrade rộng rãi.
/// - Bảng CharacterStat chỉ hiển thị 2 hàng chỉ số Anh Hùng sạch thoáng.
/// </summary>
public class EquipmentTabController : MonoBehaviour
{
    private class SlotVisual
    {
        public Transform Root;
        public UnityEngine.UI.Button Button;
        public UnityEngine.UI.Image Background;
        public UnityEngine.UI.Image IconImage;
        public GameObject BadgeBg;
        public TextMeshProUGUI BadgeText;
    }

    private readonly SlotVisual[] equippedSlots = new SlotVisual[SecureGameData.EQUIPMENT_SLOT_COUNT];
    private readonly List<SlotVisual> storageSlots = new List<SlotVisual>();
    private readonly List<int> ownedItemIdsCache = new List<int>();

    private TextMeshProUGUI statsRow1Text;
    private TextMeshProUGUI statsRow2Text;

    private TextMeshProUGUI detailNameText;
    private TextMeshProUGUI detailRarityText;
    private TextMeshProUGUI detailStatsText;
    private UnityEngine.UI.Button equipToggleBtn;
    private UnityEngine.UI.Image equipToggleBg;
    private TextMeshProUGUI equipToggleLabel;
    private UnityEngine.UI.Button upgradeGearBtn;
    private UnityEngine.UI.Image upgradeGearBg;
    private TextMeshProUGUI upgradeGearLabel;

    private int selectedItemId = -1;
    private bool isBuilt = false;
    private Coroutine toastRoutine;

    public void Initialize()
    {
        if (isBuilt)
        {
            Refresh();
            return;
        }

        BindExistingHierarchy();
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

    private void BindExistingHierarchy()
    {
        Transform mainContent = transform.Find("MainContent");
        if (mainContent == null) mainContent = transform;

        // 1. 6 ô trang bị đang mặc (3 Left + 3 Right) + thẻ Chi tiết nằm gọn giữa khoảng trống trung tâm
        Transform equippedRoot = mainContent.Find("EquippedItem");
        if (equippedRoot != null)
        {
            Transform left = equippedRoot.Find("Left");
            Transform right = equippedRoot.Find("Right");

            int slotIdx = 0;
            if (left != null)
            {
                for (int i = 0; i < left.childCount && slotIdx < 3; i++)
                {
                    int capturedSlot = slotIdx;
                    equippedSlots[slotIdx] = BindSlotVisual(left.GetChild(i), () => OnEquippedSlotClicked(capturedSlot));
                    slotIdx++;
                }
            }
            if (right != null)
            {
                for (int i = 0; i < right.childCount && slotIdx < 6; i++)
                {
                    int capturedSlot = slotIdx;
                    equippedSlots[slotIdx] = BindSlotVisual(right.GetChild(i), () => OnEquippedSlotClicked(capturedSlot));
                    slotIdx++;
                }
            }

            BuildCenterGearDetailCard(equippedRoot);
        }

        // 2. Bảng CharacterStat chỉ hiển thị tổng chỉ số gọn 2 dòng (không nhồi nút bấm)
        Transform statContainer = mainContent.Find("CharacterStat");
        if (statContainer != null)
        {
            BuildCleanCharacterStatPanel(statContainer);
        }

        // 3. Kho 13 ô trang bị trong ItemStorage/ListItem
        Transform listItem = mainContent.Find("ItemStorage/ListItem");
        if (listItem != null)
        {
            for (int i = 0; i < listItem.childCount; i++)
            {
                int storageIdx = i;
                storageSlots.Add(BindSlotVisual(listItem.GetChild(i), () => OnStorageSlotClicked(storageIdx)));
            }
        }
    }

    private SlotVisual BindSlotVisual(Transform root, UnityEngine.Events.UnityAction onClick)
    {
        UnityEngine.UI.Image bg = root.Find("Background")?.GetComponent<UnityEngine.UI.Image>();
        UnityEngine.UI.Image icon = root.Find("IconItem")?.GetComponent<UnityEngine.UI.Image>();

        // Dọn dẹp nhãn cũ nếu tồn tại
        Transform oldTop = root.Find("TopTagBg");
        if (oldTop != null) Destroy(oldTop.gameObject);
        Transform oldBottom = root.Find("BottomTagBg");
        if (oldBottom != null) Destroy(oldBottom.gameObject);

        UnityEngine.UI.Button btn = root.GetComponent<UnityEngine.UI.Button>();
        if (btn == null) btn = root.gameObject.AddComponent<UnityEngine.UI.Button>();
        if (bg != null) btn.targetGraphic = bg;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(onClick);

        // Chỉ giữ 1 dải nhãn mỏng gọn (24% chiều cao) sát đáy ô để chừa trọn khu vực Icon cho asset sau này
        Transform badgeTf = root.Find("SlotBadgeBg");
        GameObject badgeBgObj;
        TextMeshProUGUI badgeTxt;

        if (badgeTf != null)
        {
            badgeBgObj = badgeTf.gameObject;
            badgeTxt = badgeTf.GetComponentInChildren<TextMeshProUGUI>();
        }
        else
        {
            UnityEngine.UI.Image badgeImg = MetaUIHelper.CreatePanel(root, "SlotBadgeBg", new Color(0.06f, 0.07f, 0.10f, 0.84f), false);
            badgeImg.rectTransform.anchorMin = new Vector2(0.06f, 0.04f);
            badgeImg.rectTransform.anchorMax = new Vector2(0.94f, 0.28f);
            badgeImg.rectTransform.offsetMin = Vector2.zero;
            badgeImg.rectTransform.offsetMax = Vector2.zero;

            badgeBgObj = badgeImg.gameObject;
            badgeTxt = MetaUIHelper.CreateText(badgeImg.transform, "BadgeTxt", "", 18f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            badgeTxt.enableAutoSizing = true;
            badgeTxt.fontSizeMin = 12f;
            badgeTxt.fontSizeMax = 19f;
        }

        return new SlotVisual
        {
            Root = root,
            Button = btn,
            Background = bg,
            IconImage = icon,
            BadgeBg = badgeBgObj,
            BadgeText = badgeTxt
        };
    }

    private void BuildCenterGearDetailCard(Transform equippedRoot)
    {
        Transform existing = equippedRoot.Find("CenterGearDetail");
        if (existing != null) Destroy(existing.gameObject);

        // Nằm giữa cột Left (rộng 200) và Right (rộng 200), chừa lề thoáng 24px mỗi bên
        RectTransform card = MetaUIHelper.CreateFramedCard(equippedRoot, "CenterGearDetail", MetaUIHelper.CardBgDark, MetaUIHelper.CardBorderGold, 3f);
        card.anchorMin = new Vector2(0f, 0.06f);
        card.anchorMax = new Vector2(1f, 0.94f);
        card.offsetMin = new Vector2(220f, 0f);
        card.offsetMax = new Vector2(-220f, 0f);

        Transform fill = card.Find("Fill");

        detailNameText = MetaUIHelper.CreateText(fill, "ItemName", "Iron Sword", 30f, MetaUIHelper.TextGold, TextAlignmentOptions.Center, FontStyles.Bold);
        detailNameText.rectTransform.anchorMin = new Vector2(0f, 0.74f);
        detailNameText.rectTransform.anchorMax = new Vector2(1f, 0.96f);
        detailNameText.rectTransform.offsetMin = new Vector2(16f, 0f);
        detailNameText.rectTransform.offsetMax = new Vector2(-16f, 0f);
        detailNameText.enableAutoSizing = true;
        detailNameText.fontSizeMin = 20f;
        detailNameText.fontSizeMax = 30f;

        detailRarityText = MetaUIHelper.CreateText(fill, "ItemMeta", "Weapon • Common • Lv.1", 22f, MetaUIHelper.TextMuted, TextAlignmentOptions.Center, FontStyles.Normal);
        detailRarityText.rectTransform.anchorMin = new Vector2(0f, 0.58f);
        detailRarityText.rectTransform.anchorMax = new Vector2(1f, 0.74f);
        detailRarityText.rectTransform.offsetMin = new Vector2(16f, 0f);
        detailRarityText.rectTransform.offsetMax = new Vector2(-16f, 0f);
        detailRarityText.enableAutoSizing = true;
        detailRarityText.fontSizeMin = 16f;
        detailRarityText.fontSizeMax = 22f;

        detailStatsText = MetaUIHelper.CreateText(fill, "ItemStats", "+3 DMG | +2% Crit", 25f, MetaUIHelper.TextCyan, TextAlignmentOptions.Center, FontStyles.Bold);
        detailStatsText.rectTransform.anchorMin = new Vector2(0f, 0.30f);
        detailStatsText.rectTransform.anchorMax = new Vector2(1f, 0.56f);
        detailStatsText.rectTransform.offsetMin = new Vector2(18f, 0f);
        detailStatsText.rectTransform.offsetMax = new Vector2(-18f, 0f);
        detailStatsText.enableAutoSizing = true;
        detailStatsText.fontSizeMin = 17f;
        detailStatsText.fontSizeMax = 25f;

        // Hàng 2 nút rộng rãi phía dưới thẻ
        equipToggleBtn = MetaUIHelper.CreateButton(fill, "EquipBtn", "EQUIP", 24f, MetaUIHelper.BtnConfirmGreen, out equipToggleLabel, out equipToggleBg);
        RectTransform eqRt = equipToggleBtn.GetComponent<RectTransform>();
        eqRt.anchorMin = new Vector2(0.05f, 0.06f);
        eqRt.anchorMax = new Vector2(0.48f, 0.25f);
        eqRt.offsetMin = Vector2.zero;
        eqRt.offsetMax = Vector2.zero;
        equipToggleBtn.onClick.AddListener(OnEquipButtonClicked);

        upgradeGearBtn = MetaUIHelper.CreateButton(fill, "UpgradeBtn", "UP 50G", 24f, MetaUIHelper.BtnGoldAction, out upgradeGearLabel, out upgradeGearBg);
        RectTransform upRt = upgradeGearBtn.GetComponent<RectTransform>();
        upRt.anchorMin = new Vector2(0.52f, 0.06f);
        upRt.anchorMax = new Vector2(0.95f, 0.25f);
        upRt.offsetMin = Vector2.zero;
        upRt.offsetMax = Vector2.zero;
        upgradeGearBtn.onClick.AddListener(OnUpgradeButtonClicked);
    }

    private void BuildCleanCharacterStatPanel(Transform statContainer)
    {
        Transform oldOverlay = statContainer.Find("EquipmentInspectorOverlay");
        if (oldOverlay != null) Destroy(oldOverlay.gameObject);

        Transform existing = statContainer.Find("CleanHeroStats");
        if (existing != null) Destroy(existing.gameObject);

        GameObject panel = new GameObject("CleanHeroStats");
        panel.transform.SetParent(statContainer, false);
        RectTransform rt = panel.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(36f, 22f);
        rt.offsetMax = new Vector2(-36f, -22f);

        statsRow1Text = MetaUIHelper.CreateText(panel.transform, "StatsRow1", "", 28f, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
        statsRow1Text.rectTransform.anchorMin = new Vector2(0f, 0.52f);
        statsRow1Text.rectTransform.anchorMax = new Vector2(1f, 1f);
        statsRow1Text.enableAutoSizing = true;
        statsRow1Text.fontSizeMin = 20f;
        statsRow1Text.fontSizeMax = 28f;

        statsRow2Text = MetaUIHelper.CreateText(panel.transform, "StatsRow2", "", 26f, MetaUIHelper.TextMuted, TextAlignmentOptions.Center, FontStyles.Bold);
        statsRow2Text.rectTransform.anchorMin = new Vector2(0f, 0f);
        statsRow2Text.rectTransform.anchorMax = new Vector2(1f, 0.48f);
        statsRow2Text.enableAutoSizing = true;
        statsRow2Text.fontSizeMin = 18f;
        statsRow2Text.fontSizeMax = 26f;
    }

    private void OnEquippedSlotClicked(int slotIdx)
    {
        SecureGameData data = DataManager.Instance?.GameData;
        if (data == null) return;

        int itemId = data.GetEquippedItemId(slotIdx);
        if (itemId >= 0)
        {
            AudioManager.Instance?.PlaySoundButtonClick();
            selectedItemId = itemId;
            Refresh();
        }
    }

    private void OnStorageSlotClicked(int storageIdx)
    {
        if (storageIdx < 0 || storageIdx >= ownedItemIdsCache.Count) return;

        AudioManager.Instance?.PlaySoundButtonClick();
        selectedItemId = ownedItemIdsCache[storageIdx];
        Refresh();
    }

    private void OnEquipButtonClicked()
    {
        if (selectedItemId < 0) return;

        bool ok = MetaProgressionSystem.TryToggleEquip(selectedItemId, out string feedback);
        if (ok) AudioManager.Instance?.PlayTargetSelected();
        else AudioManager.Instance?.PlaySoundButtonClick();

        MetaUIHelper.ShowToast(this, transform, ref toastRoutine, feedback, ok);
        Refresh();
    }

    private void OnUpgradeButtonClicked()
    {
        if (selectedItemId < 0) return;

        bool ok = MetaProgressionSystem.TryUpgradeEquipment(selectedItemId, out string feedback);
        if (ok) AudioManager.Instance?.PlayTargetSelected();
        else AudioManager.Instance?.PlaySoundButtonClick();

        MetaUIHelper.ShowToast(this, transform, ref toastRoutine, feedback, ok);
        Refresh();
    }

    public void Refresh()
    {
        if (!isBuilt) return;

        SecureGameData data = DataManager.Instance?.GameData;
        if (data == null) return;

        ownedItemIdsCache.Clear();
        for (int id = 0; id < MetaProgressionSystem.Equipments.Length; id++)
        {
            if (data.GetEquipmentLevel(id) > 0) ownedItemIdsCache.Add(id);
        }

        if (selectedItemId < 0 || data.GetEquipmentLevel(selectedItemId) <= 0)
        {
            selectedItemId = ownedItemIdsCache.Count > 0 ? ownedItemIdsCache[0] : -1;
        }

        // 1. 6 ô trang bị đang mặc
        for (int slot = 0; slot < equippedSlots.Length; slot++)
        {
            SlotVisual view = equippedSlots[slot];
            if (view == null) continue;

            int equippedId = data.GetEquippedItemId(slot);
            if (equippedId >= 0 && equippedId < MetaProgressionSystem.Equipments.Length && data.GetEquipmentLevel(equippedId) > 0)
            {
                EquipmentDefinition eq = MetaProgressionSystem.Equipments[equippedId];
                int lv = data.GetEquipmentLevel(equippedId);
                if (view.Background != null) view.Background.color = MetaProgressionSystem.GetRarityColor(eq.Rarity);
                if (view.IconImage != null) view.IconImage.color = Color.white;
                if (view.BadgeBg != null) view.BadgeBg.SetActive(true);
                if (view.BadgeText != null)
                {
                    view.BadgeText.text = $"{eq.ShortTag} Lv.{lv}";
                    view.BadgeText.color = MetaUIHelper.TextGold;
                }
            }
            else
            {
                if (view.Background != null) view.Background.color = new Color(0.25f, 0.26f, 0.30f, 0.85f);
                if (view.IconImage != null) view.IconImage.color = new Color(1f, 1f, 1f, 0.18f);
                if (view.BadgeBg != null) view.BadgeBg.SetActive(true);
                if (view.BadgeText != null)
                {
                    view.BadgeText.text = MetaProgressionSystem.GetSlotShortName((EquipmentSlotType)slot);
                    view.BadgeText.color = MetaUIHelper.TextMuted;
                }
            }
        }

        // 2. 13 ô kho đồ ItemStorage
        for (int i = 0; i < storageSlots.Count; i++)
        {
            SlotVisual view = storageSlots[i];
            if (i < ownedItemIdsCache.Count)
            {
                int id = ownedItemIdsCache[i];
                EquipmentDefinition eq = MetaProgressionSystem.Equipments[id];
                int lv = data.GetEquipmentLevel(id);
                bool isEquipped = data.GetEquippedItemId((int)eq.Slot) == id;
                bool isSelected = (id == selectedItemId);

                Color rarityCol = MetaProgressionSystem.GetRarityColor(eq.Rarity);
                if (view.Background != null)
                {
                    view.Background.color = isSelected ? Color.Lerp(rarityCol, Color.white, 0.35f) : rarityCol;
                }
                if (view.IconImage != null) view.IconImage.color = Color.white;
                if (view.BadgeBg != null) view.BadgeBg.SetActive(true);
                if (view.BadgeText != null)
                {
                    view.BadgeText.text = isEquipped ? $"EQ Lv.{lv}" : $"{eq.ShortTag} {lv}";
                    view.BadgeText.color = isEquipped ? MetaUIHelper.TextGold : Color.white;
                }
                view.Button.interactable = true;
            }
            else
            {
                if (view.Background != null) view.Background.color = new Color(0.18f, 0.19f, 0.22f, 0.50f);
                if (view.IconImage != null) view.IconImage.color = new Color(1f, 1f, 1f, 0.06f);
                if (view.BadgeBg != null) view.BadgeBg.SetActive(false);
                view.Button.interactable = false;
            }
        }

        // 3. Bảng CharacterStat 2 dòng sạch sẽ
        PlayerTotalStatsPreview total = MetaProgressionSystem.CalculateTotalPlayerStats(MetaUIHelper.GetPlayerBaseStatSO(), data);
        if (statsRow1Text != null)
        {
            statsRow1Text.text = $"LV.{total.PlayerLevel}    HP <color=#4ADE80>{total.MaxHealth:0.#}</color>    SHD <color=#7DD3FC>{total.Shield:0.#}</color>    DMG <color=#FCA5A5>{total.Damage:0.#}</color>";
        }
        if (statsRow2Text != null)
        {
            int gemPct = Mathf.RoundToInt(total.GemPowerBonusPercent * 100f);
            statsRow2Text.text = $"Crit {total.CritRate:0.#}%    CDmg {total.CritDamage:0.#}%    Dodge {total.Dodge:0.#}%    Gem +{gemPct}%";
        }

        // 4. Thẻ chi tiết trang bị ở giữa
        if (selectedItemId >= 0 && selectedItemId < MetaProgressionSystem.Equipments.Length)
        {
            EquipmentDefinition eq = MetaProgressionSystem.Equipments[selectedItemId];
            int lv = data.GetEquipmentLevel(selectedItemId);
            bool isEquipped = data.GetEquippedItemId((int)eq.Slot) == selectedItemId;
            bool isMax = lv >= MetaProgressionSystem.MAX_EQUIPMENT_LEVEL;
            int upCost = MetaProgressionSystem.GetEquipmentUpgradeCost(lv);

            detailNameText.text = eq.Name;
            detailRarityText.text = $"{eq.Slot}  •  {eq.Rarity}  •  Lv.{lv}/{MetaProgressionSystem.MAX_EQUIPMENT_LEVEL}";
            detailStatsText.text = MetaProgressionSystem.BuildEquipmentStatSummary(eq, lv);

            equipToggleBtn.gameObject.SetActive(true);
            equipToggleLabel.text = isEquipped ? "UNEQUIP" : "EQUIP";
            equipToggleBg.color = isEquipped ? MetaUIHelper.BtnCrimsonAction : MetaUIHelper.BtnConfirmGreen;

            upgradeGearBtn.gameObject.SetActive(true);
            if (isMax)
            {
                upgradeGearLabel.text = "MAX";
                upgradeGearBg.color = MetaUIHelper.BtnDisabled;
                upgradeGearBtn.interactable = false;
            }
            else
            {
                upgradeGearLabel.text = $"UP {upCost}G";
                upgradeGearBg.color = data.Gold >= upCost ? MetaUIHelper.BtnGoldAction : MetaUIHelper.BtnDisabled;
                upgradeGearBtn.interactable = true;
            }
        }
        else if (detailNameText != null)
        {
            detailNameText.text = "No Gear Selected";
            detailRarityText.text = "Buy in Shop or Claim Rewards";
            detailStatsText.text = "";
            if (equipToggleBtn != null) equipToggleBtn.gameObject.SetActive(false);
            if (upgradeGearBtn != null) upgradeGearBtn.gameObject.SetActive(false);
        }
    }
}
