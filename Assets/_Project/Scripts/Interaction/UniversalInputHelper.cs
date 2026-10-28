using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;
#endif

namespace AnatomyAR.Interaction
{
    /// <summary>
    /// Универсальный обработчик ввода, гарантирующий корректную работу тач-скрина и мыши
    /// как в New Input System (Input System Package), так и в Legacy Input Manager.
    /// </summary>
    public static class UniversalInputHelper
    {
        static UniversalInputHelper()
        {
#if ENABLE_INPUT_SYSTEM
            if (!EnhancedTouchSupport.enabled)
            {
                EnhancedTouchSupport.Enable();
            }
#endif
        }

        /// <summary>
        /// Возвращает true в кадре, когда палец коснулся экрана или была нажата левая кнопка мыши.
        /// </summary>
        public static bool GetTapDown(out Vector2 screenPosition)
        {
            screenPosition = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            // 1. Проверка касаний тач-скрина через EnhancedTouch
            if (Touch.activeTouches.Count > 0)
            {
                var t = Touch.activeTouches[0];
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Began)
                {
                    screenPosition = t.screenPosition;
                    return true;
                }
            }
            // 2. Fallback через Touchscreen.current
            else if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                screenPosition = Touchscreen.current.primaryTouch.position.ReadValue();
                return true;
            }
            // 3. Проверка мыши (для тестирования на ПК / в редакторе)
            else if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
            {
                screenPosition = Mouse.current.position.ReadValue();
                return true;
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount > 0 && Input.GetTouch(0).phase == UnityEngine.TouchPhase.Began)
            {
                screenPosition = Input.GetTouch(0).position;
                return true;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                screenPosition = (Vector2)Input.mousePosition;
                return true;
            }
#endif

            return false;
        }

        /// <summary>
        /// Проверяет, перекрыта ли точка экрана реальным интерактивным элементом UI (кнопкой, слайдером, дропдауном).
        /// Обычные фоновые панели и прозрачные тексты игнорируются, чтобы не блокировать размещение модели.
        /// </summary>
        public static bool IsPointerOverInteractiveUI(Vector2 screenPosition)
        {
            if (EventSystem.current == null) return false;

            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = screenPosition
            };

            List<RaycastResult> results = new List<RaycastResult>();
            EventSystem.current.RaycastAll(eventData, results);

            for (int i = 0; i < results.Count; i++)
            {
                GameObject hitGO = results[i].gameObject;
                if (hitGO == null) continue;

                // Если попали по кнопке, слайдеру, тогглу или дропдауну — блокируем клик в 3D
                if (hitGO.GetComponentInParent<Button>() != null ||
                    hitGO.GetComponentInParent<Slider>() != null ||
                    hitGO.GetComponentInParent<Toggle>() != null ||
                    hitGO.GetComponentInParent<TMP_Dropdown>() != null ||
                    hitGO.GetComponentInParent<ScrollRect>() != null)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Возвращает дельту смещения при вращении одним пальцем / мышью.
        /// </summary>
        public static bool GetOneFingerDragDelta(out Vector2 delta)
        {
            delta = Vector2.zero;

#if ENABLE_INPUT_SYSTEM
            if (Touch.activeTouches.Count == 1)
            {
                var t = Touch.activeTouches[0];
                if (IsPointerOverInteractiveUI(t.screenPosition))
                {
                    return false;
                }
                if (t.phase == UnityEngine.InputSystem.TouchPhase.Moved)
                {
                    delta = t.delta;
                    return true;
                }
            }
            else if (Mouse.current != null)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                bool isRight = Mouse.current.rightButton.isPressed;
                bool isLeft = Mouse.current.leftButton.isPressed && !IsPointerOverInteractiveUI(mousePos);
                if (isRight || isLeft)
                {
                    delta = Mouse.current.delta.ReadValue();
                    return delta.sqrMagnitude > 0.001f;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount == 1)
            {
                var touch = Input.GetTouch(0);
                if (!IsPointerOverInteractiveUI(touch.position) && touch.phase == UnityEngine.TouchPhase.Moved)
                {
                    delta = touch.deltaPosition;
                    return true;
                }
            }
            else
            {
                bool isRight = Input.GetMouseButton(1);
                bool isLeft = Input.GetMouseButton(0) && !IsPointerOverInteractiveUI(Input.mousePosition);
                if (isRight || isLeft)
                {
                    delta = new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y")) * 10f;
                    return delta.sqrMagnitude > 0.001f;
                }
            }
#endif

            return false;
        }

        /// <summary>
        /// Возвращает относительный фактор изменения масштаба при щипке двумя пальцами.
        /// </summary>
        public static bool GetPinchScaleFactor(out float scaleFactor)
        {
            scaleFactor = 1.0f;

#if ENABLE_INPUT_SYSTEM
            if (Touch.activeTouches.Count >= 2)
            {
                var t0 = Touch.activeTouches[0];
                var t1 = Touch.activeTouches[1];

                Vector2 prevPos0 = t0.screenPosition - t0.delta;
                Vector2 prevPos1 = t1.screenPosition - t1.delta;

                float prevDist = Vector2.Distance(prevPos0, prevPos1);
                float curDist = Vector2.Distance(t0.screenPosition, t1.screenPosition);

                if (prevDist > 0.01f)
                {
                    scaleFactor = curDist / prevDist;
                    return true;
                }
            }
            else if (Mouse.current != null)
            {
                float scroll = Mouse.current.scroll.ReadValue().y;
                if (Mathf.Abs(scroll) > 0.01f)
                {
                    scaleFactor = 1.0f + (scroll > 0 ? 0.08f : -0.08f);
                    return true;
                }
            }
#endif

#if ENABLE_LEGACY_INPUT_MANAGER
            if (Input.touchCount >= 2)
            {
                UnityEngine.Touch t0 = Input.GetTouch(0);
                UnityEngine.Touch t1 = Input.GetTouch(1);

                Vector2 prev0 = t0.position - t0.deltaPosition;
                Vector2 prev1 = t1.position - t1.deltaPosition;

                float prevDist = Vector2.Distance(prev0, prev1);
                float curDist = Vector2.Distance(t0.position, t1.position);

                if (prevDist > 0.01f)
                {
                    scaleFactor = curDist / prevDist;
                    return true;
                }
            }
            else if (Mathf.Abs(Input.GetAxis("Mouse ScrollWheel")) > 0.001f)
            {
                scaleFactor = 1.0f + Input.GetAxis("Mouse ScrollWheel") * 0.5f;
                return true;
            }
#endif

            return false;
        }
    }
}
