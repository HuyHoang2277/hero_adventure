using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class InputHelper
{
    private static readonly List<RaycastResult> uiRaycastResults = new List<RaycastResult>();

    /// <summary>
    /// Chỉ trả về true khi con trỏ nằm trên một UI tương tác thực sự (Button, Slider, Toggle...),
    /// tránh bị chặn bởi các ảnh nền Canvas có bật RaycastTarget phía sau lưới ngọc/quái.
    /// </summary>
    public static bool IsPointerOverUI()
    {
        if (EventSystem.current == null) return false;

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = GetPointerScreenPosition()
        };

        uiRaycastResults.Clear();
        EventSystem.current.RaycastAll(eventData, uiRaycastResults);

        for (int i = 0; i < uiRaycastResults.Count; i++)
        {
            GameObject hitObj = uiRaycastResults[i].gameObject;
            if (hitObj == null) continue;

            Selectable selectable = hitObj.GetComponentInParent<Selectable>();
            if (selectable != null && selectable.IsActive() && selectable.IsInteractable())
            {
                return true;
            }
        }

        return false;
    }

    public static bool GetPointerDown()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        return Input.GetMouseButtonDown(0);
#else
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began;
#endif
    }

    public static bool GetPointerHeld()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        return Input.GetMouseButton(0);
#else
        return Input.touchCount > 0 &&
               (Input.GetTouch(0).phase == TouchPhase.Moved ||
                Input.GetTouch(0).phase == TouchPhase.Stationary);
#endif
    }

    public static bool GetPointerUp()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        return Input.GetMouseButtonUp(0);
#else
        return Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Ended;
#endif
    }

    public static Vector2 GetPointerScreenPosition()
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        return Input.mousePosition;
#else
        if (Input.touchCount > 0)
            return Input.GetTouch(0).position;
        return Vector2.zero;
#endif
    }

    public static Vector3 GetPointerWorldPosition()
    {
        Vector3 worldPos = Camera.main.ScreenToWorldPoint(GetPointerScreenPosition());
        worldPos.z = 0f;
        return worldPos;
    }
}
