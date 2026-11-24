using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;

/// <summary>
/// Quản lý Tab Shop — đấu nối đúng đường dẫn prefab ItemShop (Icon/NumOfItem và Button/Text (TMP))
/// với nhãn ngắn 1 dòng gọn gàng, giữ nguyên khung/icon của prefab để dễ thay asset về sau.
/// </summary>
public class ShopTabController : MonoBehaviour
{
    private class ShopCardBinding
    {
        public ShopOfferDefinition Offer;
        public Transform CardRoot;
        public UnityEngine.UI.Button BuyButton;
        public TextMeshProUGUI PriceText;
        public TextMeshProUGUI ItemAmountText;
        public TextMeshProUGUI ItemNameHeader;
    }

    private readonly List<ShopCardBinding> bindings = new List<ShopCardBinding>();
    private bool isBuilt = false;
    private Coroutine toastRoutine;

    public void Initialize()
    {
        if (isBuilt)
        {
            Refresh();
            return;
        }

        BindExistingShopCards();
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

    private void BindExistingShopCards()
    {
        Transform mainContent = transform.Find("MainContent");
        if (mainContent == null) mainContent = transform;

        BindSection(mainContent.Find("CoinPurchase/ListItem"), ShopOfferSection.Coin);
        BindSection(mainContent.Find("DiamondPurchase/ListItem"), ShopOfferSection.Diamond);
        BindSection(mainContent.Find("OtherPurchase/ListItem"), ShopOfferSection.Other);
    }

    private void BindSection(Transform listRoot, ShopOfferSection section)
    {
        if (listRoot == null) return;

        List<ShopOfferDefinition> sectionOffers = new List<ShopOfferDefinition>();
        for (int i = 0; i < MetaProgressionSystem.ShopOffers.Length; i++)
        {
            if (MetaProgressionSystem.ShopOffers[i].Section == section)
            {
                sectionOffers.Add(MetaProgressionSystem.ShopOffers[i]);
            }
        }

        for (int i = 0; i < listRoot.childCount && i < sectionOffers.Count; i++)
        {
            Transform card = listRoot.GetChild(i);
            ShopOfferDefinition offer = sectionOffers[i];

            UnityEngine.UI.Button buyBtn = card.Find("Button")?.GetComponent<UnityEngine.UI.Button>();
            TextMeshProUGUI priceTxt = card.Find("Button/Text (TMP)")?.GetComponent<TextMeshProUGUI>();
            // Đúng đường dẫn trong ItemShop.prefab: Icon/NumOfItem
            Transform iconRoot = card.Find("Icon");
            TextMeshProUGUI numTxt = iconRoot != null ? iconRoot.Find("NumOfItem")?.GetComponent<TextMeshProUGUI>() : null;

            if (buyBtn == null) continue;

            if (numTxt != null)
            {
                numTxt.enableAutoSizing = true;
                numTxt.fontSizeMin = 15f;
                numTxt.fontSizeMax = 24f;
                numTxt.alignment = TextAlignmentOptions.Center;
                numTxt.enableWordWrapping = false;
            }

            if (priceTxt != null)
            {
                priceTxt.enableAutoSizing = true;
                priceTxt.fontSizeMin = 18f;
                priceTxt.fontSizeMax = 28f;
            }

            // Nhãn tên ngắn 1 dòng đặt gọn ở mép trên khung Icon (không che giữa Icon)
            TextMeshProUGUI nameHeader = null;
            if (iconRoot != null)
            {
                Transform existingHeader = iconRoot.Find("OfferNameBg");
                if (existingHeader != null)
                {
                    nameHeader = existingHeader.GetComponentInChildren<TextMeshProUGUI>();
                }
                else
                {
                    UnityEngine.UI.Image hdrBg = MetaUIHelper.CreatePanel(iconRoot, "OfferNameBg", new Color(0.06f, 0.07f, 0.10f, 0.80f), false);
                    hdrBg.rectTransform.anchorMin = new Vector2(0.06f, 0.78f);
                    hdrBg.rectTransform.anchorMax = new Vector2(0.94f, 0.96f);
                    hdrBg.rectTransform.offsetMin = Vector2.zero;
                    hdrBg.rectTransform.offsetMax = Vector2.zero;

                    nameHeader = MetaUIHelper.CreateText(hdrBg.transform, "OfferNameTxt", offer.Label, 19f, MetaUIHelper.TextGold, TextAlignmentOptions.Center, FontStyles.Bold);
                    nameHeader.enableAutoSizing = true;
                    nameHeader.fontSizeMin = 13f;
                    nameHeader.fontSizeMax = 19f;
                    nameHeader.enableWordWrapping = false;
                }
            }

            ShopOfferDefinition capturedOffer = offer;
            Transform capturedCard = card;
            buyBtn.onClick.RemoveAllListeners();
            buyBtn.onClick.AddListener(() => OnBuyClicked(capturedOffer, capturedCard));

            bindings.Add(new ShopCardBinding
            {
                Offer = offer,
                CardRoot = card,
                BuyButton = buyBtn,
                PriceText = priceTxt,
                ItemAmountText = numTxt,
                ItemNameHeader = nameHeader
            });
        }
    }

    private void OnBuyClicked(ShopOfferDefinition offer, Transform cardRoot)
    {
        bool ok = MetaProgressionSystem.TryPurchaseShopOffer(offer, out string feedback);
        if (ok)
        {
            AudioManager.Instance?.PlayWaveClear();
            if (cardRoot != null)
            {
                cardRoot.DOKill();
                cardRoot.localScale = Vector3.one;
                cardRoot.DOPunchScale(Vector3.one * 0.06f, 0.20f, 6, 0.6f).SetUpdate(true);
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

        for (int i = 0; i < bindings.Count; i++)
        {
            ShopCardBinding b = bindings[i];
            ShopOfferDefinition offer = b.Offer;

            bool isMaxOwned = offer.RewardType == ShopRewardType.SpecificEquipment &&
                              data.GetEquipmentLevel(offer.RewardParam) >= MetaProgressionSystem.MAX_EQUIPMENT_LEVEL;

            if (b.ItemNameHeader != null)
            {
                b.ItemNameHeader.text = offer.Label;
            }

            if (b.ItemAmountText != null)
            {
                if (offer.RewardType == ShopRewardType.SpecificEquipment)
                {
                    int curLv = data.GetEquipmentLevel(offer.RewardParam);
                    b.ItemAmountText.text = curLv > 0 ? $"Lv.{curLv}" : offer.AmountLabel;
                }
                else
                {
                    b.ItemAmountText.text = offer.AmountLabel;
                }
            }

            if (b.PriceText != null)
            {
                b.PriceText.text = isMaxOwned
                    ? "MAX"
                    : (offer.UsesDiamond ? $"{offer.Price} D" : $"{offer.Price} G");
            }

            if (b.BuyButton != null)
            {
                b.BuyButton.interactable = !isMaxOwned;
            }
        }
    }
}
