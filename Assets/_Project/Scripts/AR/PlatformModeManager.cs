using UnityEngine;
using UnityEngine.XR.ARFoundation;
using AnatomyAR.Controllers;
using AnatomyAR.Data;

namespace AnatomyAR.AR
{
    /// <summary>
    /// Менеджер режима платформы: определяет окружение и переключает
    /// между нативным AR (Android/iOS) и fallback 3D-режимом (WebGL/Editor/ПК).
    /// Решает проблему чёрного экрана при запуске Unity WebGL билда в браузере.
    /// </summary>
    public class PlatformModeManager : MonoBehaviour
    {
        [Header("AR компоненты (нативный мобильный билд)")]
        [Tooltip("Корневой AR Origin объект — будет отключён в WebGL")]
        [SerializeField] private GameObject arSessionObject;
        [SerializeField] private GameObject arOriginObject;
        [SerializeField] private ARPlaneManager arPlaneManager;
        [SerializeField] private ARPlacementAndTrackingManager arPlacementManager;

        [Header("Fallback 3D-режим (WebGL / Editor / ПК)")]
        [Tooltip("Обычная камера для отображения 3D-сцены без AR")]
        [SerializeField] private Camera fallbackCamera;
        [Tooltip("Объект с дефолтной 3D-моделью, которая появляется сразу без тапа на поверхность")]
        [SerializeField] private GameObject fallbackOrganRoot;

        [Header("Настройки")]
        [SerializeField] private AnatomyModelSO defaultModelForWebGL;
        [Tooltip("Фоновый цвет сцены в WebGL режиме (цвет неба / студия)")]
        [SerializeField] private Color webGLBackgroundColor = new Color(0.07f, 0.10f, 0.16f, 1.0f);

        private bool isARPlatform = false;

        private void Awake()
        {
            isARPlatform = DetectARCapability();
            ApplyPlatformMode();
        }

        /// <summary>
        /// Определяет, поддерживает ли текущая платформа нативный AR.
        /// WebGL-билд и редактор не поддерживают.
        /// </summary>
        private bool DetectARCapability()
        {
#if UNITY_WEBGL
            // WebGL никогда не поддерживает AR Foundation нативно
            return false;
#elif UNITY_EDITOR
            // В редакторе Unity AR тоже недоступен (нет нативного устройства)
            return false;
#elif UNITY_ANDROID || UNITY_IOS
            // На Android и iOS — нативный AR через ARCore/ARKit
            return true;
#else
            return false;
#endif
        }

        private void ApplyPlatformMode()
        {
            if (isARPlatform)
            {
                EnableARMode();
            }
            else
            {
                EnableFallback3DMode();
            }
        }

        private void EnableARMode()
        {
            Debug.Log("[PlatformMode] Запуск в режиме нативного AR (Android/iOS ARCore/ARKit)");

            // Активируем AR-компоненты
            if (arSessionObject != null) arSessionObject.SetActive(true);
            if (arOriginObject != null) arOriginObject.SetActive(true);

            // Отключаем fallback объекты
            if (fallbackCamera != null) fallbackCamera.gameObject.SetActive(false);
            if (fallbackOrganRoot != null) fallbackOrganRoot.SetActive(false);
        }

        private void EnableFallback3DMode()
        {
            Debug.Log("[PlatformMode] Запуск в режиме WebGL / Editor — AR отключен, активирована 3D-студия");

            // Полностью отключаем AR-объекты (они вызывают чёрный экран)
            if (arSessionObject != null) arSessionObject.SetActive(false);
            if (arOriginObject != null) arOriginObject.SetActive(false);
            if (arPlaneManager != null) arPlaneManager.enabled = false;
            if (arPlacementManager != null) arPlacementManager.enabled = false;

            // Включаем обычную 3D-камеру (fallback)
            if (fallbackCamera != null)
            {
                fallbackCamera.gameObject.SetActive(true);
                fallbackCamera.backgroundColor = webGLBackgroundColor;
                fallbackCamera.clearFlags = CameraClearFlags.SolidColor;
                fallbackCamera.tag = "MainCamera";
            }

            // Отображаем модель органа сразу без тапа на поверхность
            if (fallbackOrganRoot != null)
            {
                fallbackOrganRoot.SetActive(true);

                // Располагаем орган перед камерой на удобном расстоянии
                if (fallbackCamera != null)
                {
                    fallbackOrganRoot.transform.position = fallbackCamera.transform.position
                                                           + fallbackCamera.transform.forward * 1.8f;
                    fallbackOrganRoot.transform.rotation = Quaternion.identity;
                    fallbackOrganRoot.transform.localScale = Vector3.one * 0.3f;
                }

                // Запускаем OrganController (пульсация, жесты мышью в браузере)
                OrganController organCtrl = fallbackOrganRoot.GetComponent<OrganController>();
                if (organCtrl != null && defaultModelForWebGL != null)
                {
                    organCtrl.ResetOrientation(defaultModelForWebGL.DefaultSpawnScale);
                }
            }

            // Скрываем AR-специфичный UI (подсказка "найдите плоскость")
            HideAROnlyUI();
        }

        private void HideAROnlyUI()
        {
            // Ищем и скрываем AR-подсказку по имени объекта
            GameObject planePrompt = GameObject.Find("PlaneDetection_Prompt");
            if (planePrompt != null) planePrompt.SetActive(false);

            GameObject repositionBtn = GameObject.Find("RepositionButton");
            if (repositionBtn != null) repositionBtn.SetActive(false);
        }

        public bool IsARMode => isARPlatform;
    }
}
