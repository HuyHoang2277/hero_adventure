using UnityEngine;
using System.Collections;
using UnityEngine.UI;
using DG.Tweening;

public enum MenuTab
{
    Shop,
    Equipment,
    Battle,
    Talent,
    Reward
}

[System.Serializable]
public class NavigationTabItem
{
    public MenuTab tabType;
    public Button tabButton;
    public GameObject tabPanel;
    public Color highlightColor = Color.white;
}

public class NavigationManager : MonoBehaviour
{
    [Header("Tabs Configuration")]
    [SerializeField] private NavigationTabItem[] tabs;

    [Header("Highlight UI")]
    [SerializeField] private RectTransform highlightRect;
    [SerializeField] private float highlightMoveDuration = 0.2f;
    [SerializeField] private Ease highlightEase = Ease.OutQuad;

    [Header("Coming Soon Popup")]
    [SerializeField] private GameObject inProgressPopupObj;
    [SerializeField] private RectTransform inProgresspopupPanel;
    [SerializeField] private Button popupOkBtn;

    private NavigationTabItem currentTabItem;
    private ShopTabController shopController;
    private EquipmentTabController equipmentController;
    private TalentTabController talentController;
    private RewardTabController rewardController;

    public void InitializeNavigation()
    {
        StartCoroutine(InitRoutine());
    }

    private IEnumerator InitRoutine()
    {
        if (popupOkBtn != null)
        {
            popupOkBtn.onClick.RemoveListener(HideComingSoonPopup);
            popupOkBtn.onClick.AddListener(HideComingSoonPopup);
        }
        if (inProgressPopupObj != null) inProgressPopupObj.SetActive(false);
        if (inProgresspopupPanel != null) inProgresspopupPanel.localScale = Vector3.zero;

        foreach (var tab in tabs)
        {
            if (tab.tabButton != null)
            {
                MenuTab type = tab.tabType;
                tab.tabButton.onClick.RemoveAllListeners();
                tab.tabButton.onClick.AddListener(() => SwitchTab(type));
            }
        }

        foreach (var tab in tabs)
        {
            try
            {
                SetupTabController(tab);
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[NavigationManager] Lỗi khởi tạo tab {tab.tabType}: {ex}");
            }
        }
        
        yield return new WaitForEndOfFrame();

        SwitchTab(MenuTab.Battle, false);
    }

    private void SetupTabController(NavigationTabItem tab)
    {
        if (tab == null || tab.tabPanel == null) return;

        switch (tab.tabType)
        {
            case MenuTab.Shop:
                shopController = tab.tabPanel.GetComponent<ShopTabController>() ?? tab.tabPanel.AddComponent<ShopTabController>();
                shopController.Initialize();
                break;
            case MenuTab.Equipment:
                equipmentController = tab.tabPanel.GetComponent<EquipmentTabController>() ?? tab.tabPanel.AddComponent<EquipmentTabController>();
                equipmentController.Initialize();
                break;
            case MenuTab.Talent:
                talentController = tab.tabPanel.GetComponent<TalentTabController>() ?? tab.tabPanel.AddComponent<TalentTabController>();
                talentController.Initialize();
                break;
            case MenuTab.Reward:
                rewardController = tab.tabPanel.GetComponent<RewardTabController>() ?? tab.tabPanel.AddComponent<RewardTabController>();
                rewardController.Initialize();
                break;
        }
    }

    private void RefreshTabContent(MenuTab targetTab)
    {
        GameSceneManager.Instance?.RefreshTopBar();
        switch (targetTab)
        {
            case MenuTab.Shop: shopController?.Refresh(); break;
            case MenuTab.Equipment: equipmentController?.Refresh(); break;
            case MenuTab.Talent: talentController?.Refresh(); break;
            case MenuTab.Reward: rewardController?.Refresh(); break;
        }
    }

    private void OnDestroy()
    {
        foreach (var tab in tabs)
        {
            if (tab.tabButton != null)
            {
                tab.tabButton.onClick.RemoveAllListeners();
            }
        }

        if (popupOkBtn != null) popupOkBtn.onClick.RemoveAllListeners();
    }

    public void SwitchTab(MenuTab targetTab, bool animateHighlight = true)
    {
        if (animateHighlight)
        {
            AudioManager.Instance?.PlaySoundClick();
        }

        if (currentTabItem != null && currentTabItem.tabPanel != null)
        {
            MetaUIHelper.HideToast(currentTabItem.tabPanel.transform);
            currentTabItem.tabPanel.SetActive(false);
        }

        NavigationTabItem newTabItem = null;
        foreach (var tab in tabs)
        {
            if (tab.tabType == targetTab)
            {
                newTabItem = tab;
                break;
            }
        }

        if (newTabItem != null)
        {
            currentTabItem = newTabItem;

            if (currentTabItem.tabPanel != null)
            {
                currentTabItem.tabPanel.SetActive(true);
            }

            if (highlightRect != null && currentTabItem.tabButton != null)
            {
                RectTransform btnRect = currentTabItem.tabButton.GetComponent<RectTransform>();
                Image highlightImg = highlightRect.GetComponent<Image>();
                
                if (animateHighlight)
                {
                    highlightRect.DOMoveX(btnRect.position.x, highlightMoveDuration).SetEase(highlightEase);
                    if (highlightImg != null)
                    {
                        highlightImg.DOColor(currentTabItem.highlightColor, highlightMoveDuration);
                    }
                }
                else
                {
                    Vector3 newPos = highlightRect.position;
                    newPos.x = btnRect.position.x;
                    highlightRect.position = newPos;
                    if (highlightImg != null)
                    {
                        highlightImg.color = currentTabItem.highlightColor;
                    }
                }
            }
        }

        RefreshTabContent(targetTab);
    }

    private void ShowComingSoonPopup()
    {
        if (inProgressPopupObj == null || inProgresspopupPanel == null) return;
        
        AudioManager.Instance?.PlayPopupOpen();
        inProgressPopupObj.SetActive(true);
        inProgresspopupPanel.localScale = Vector3.zero;
        inProgresspopupPanel.DOScale(Vector3.one, 0.3f).SetEase(Ease.OutBack).SetUpdate(true);
    }

    private void HideComingSoonPopup()
    {
        if (inProgressPopupObj == null || inProgresspopupPanel == null) return;
        
        AudioManager.Instance?.PlaySoundClick();
        AudioManager.Instance?.PlayPopupClose();

        inProgresspopupPanel.DOScale(Vector3.zero, 0.2f).SetEase(Ease.InBack).SetUpdate(true).OnComplete(() =>
        {
            inProgressPopupObj.SetActive(false);
        });
    }
}
