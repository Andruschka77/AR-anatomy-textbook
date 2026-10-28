using System.Collections.Generic;
using UnityEngine;
using AnatomyAR.Components;
using AnatomyAR.Data;

namespace AnatomyAR.Controllers
{
    /// <summary>
    /// Главный контроллер 3D-модели анатомического органа (навешивается на корень префаба).
    /// Управляет:
    /// 1. Режимом «Взрыв-схема» (Exploded View) по значению слайдера.
    /// 2. Послойной фильтрацией систем органов (Layer filtering).
    /// 3. Режимом рентгена (X-Ray / полупрозрачность окружающих тканей).
    /// 4. Процедурной анимацией биения сердца с регулировкой BPM (ЧСС).
    /// 5. Жестами манипуляции: вращение одним пальцем и масштабирование двумя пальцами (Pinch-to-zoom).
    /// </summary>
    public class OrganController : MonoBehaviour
    {
        [Header("Настройки Exploded View (Взрыв-схема)")]
        [Tooltip("Максимальная дистанция разлета частей органа от центра (в метрах)")]
        [SerializeField] [Range(0.05f, 1.5f)] private float maxExplodeDistance = 0.35f;

        [Tooltip("Скорость сглаживания перемещения при разлете (Lerp speed)")]
        [SerializeField] private float explodeSmoothSpeed = 10f;

        [Header("Материалы рентгена (X-Ray)")]
        [Tooltip("Материал с URP Fresnel / X-Ray шейдером для эффекта просвечивания тканей")]
        [SerializeField] private Material xrayMaterial;

        [Header("Процедурная пульсация (Сердцебиение)")]
        [Tooltip("Включить процедурное биение для данного органа")]
        [SerializeField] private bool enablePulsation = true;

        [Tooltip("Частота сердечных сокращений (BPM / ударов в минуту)")]
        [SerializeField] [Range(40f, 180f)] private float heartRateBPM = 75f;

        [Tooltip("Амплитуда расширения при систоле/диастоле (относительный масштаб)")]
        [SerializeField] [Range(0.01f, 0.25f)] private float pulsationAmplitude = 0.08f;

        [Header("Жесты манипуляции (Вращение и масштаб)")]
        [Tooltip("Чувствительность вращения одним пальцем")]
        [SerializeField] private float rotationSpeed = 0.35f;

        [Tooltip("Минимально допустимый масштаб органа в AR")]
        [SerializeField] private float minScale = 0.2f;

        [Tooltip("Максимально допустимый масштаб органа в AR")]
        [SerializeField] private float maxScale = 5.0f;

        // Список всех управляемых дочерних интерактивных деталей
        private List<OrganPartInteractable> parts = new List<OrganPartInteractable>();

        // Переменные состояния Exploded View
        private float targetExplodeFactor = 0f;
        private float currentExplodeFactor = 0f;

        // Переменные состояния анимации пульсации
        private float pulsationTimer = 0f;
        private Vector3 baseScale;

        // Переменные для обработки жестов пальцами
        private Vector2 lastSingleTouchPos;
        private bool isSingleTouching = false;
        private float initialPinchDistance = 0f;
        private Vector3 initialScaleOnPinch;

        public IReadOnlyList<OrganPartInteractable> Parts => parts;
        public float HeartRateBPM => heartRateBPM;
        public float CurrentExplodeFactor => currentExplodeFactor;

        private void Awake()
        {
            baseScale = transform.localScale;
            CollectAllChildParts();
        }

        private void Update()
        {
            UpdateExplodedView();
            UpdatePulsation();
            HandleTouchGestures();
        }

        /// <summary>
        /// Сканирует всю иерархию объекта и регистрирует дочерние интерактивные сегменты.
        /// </summary>
        public void CollectAllChildParts()
        {
            parts.Clear();
            OrganPartInteractable[] foundParts = GetComponentsInChildren<OrganPartInteractable>(true);
            parts.AddRange(foundParts);
        }

        #region Взрыв-схема (Exploded View)

        /// <summary>
        /// Устанавливает целевой фактор разлета деталей органа (от 0.0 - сжато до 1.0 - полный разлет).
        /// Вызывается напрямую из события UI Slider.OnValueChanged.
        /// </summary>
        public void SetExplodeFactor(float factor)
        {
            targetExplodeFactor = Mathf.Clamp01(factor);
        }

        private void UpdateExplodedView()
        {
            if (Mathf.Abs(currentExplodeFactor - targetExplodeFactor) > 0.0005f)
            {
                currentExplodeFactor = Mathf.Lerp(currentExplodeFactor, targetExplodeFactor, Time.deltaTime * explodeSmoothSpeed);

                float currentDistance = currentExplodeFactor * maxExplodeDistance;
                for (int i = 0; i < parts.Count; i++)
                {
                    if (parts[i] != null)
                    {
                        parts[i].SetExplodeOffset(currentDistance);
                    }
                }
            }
        }

        #endregion

        #region Фильтрация анатомических слоев

        /// <summary>
        /// Включает или отключает видимость целой анатомической системы (Скелет, Дыхание, Кровообращение и т.д.).
        /// </summary>
        public void ToggleSystemLayer(OrganSystemType system, bool isVisible)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null && parts[i].PartData != null && parts[i].PartData.SystemType == system)
                {
                    parts[i].SetVisibility(isVisible);
                }
            }
        }

        #endregion

        #region Режим Рентгена (X-Ray)

        /// <summary>
        /// Переключает модель в режим рентгена. Если указан конкретный выбранный отдел,
        /// то он остается в полном цвете/PBR, а все окружающие ткани становятся полупрозрачными.
        /// </summary>
        public void SetXRayMode(bool isXRay, OrganPartInteractable isolatedPart = null)
        {
            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] == null) continue;

                if (isXRay)
                {
                    if (isolatedPart != null && parts[i] == isolatedPart)
                    {
                        // Выбранный орган отображается непрозрачным PBR
                        parts[i].SetXRayMode(false, null);
                    }
                    else
                    {
                        // Окружающие ткани просвечивают
                        parts[i].SetXRayMode(true, xrayMaterial);
                    }
                }
                else
                {
                    // Возврат всех деталей к стандартному виду
                    parts[i].SetXRayMode(false, null);
                }
            }
        }

        #endregion

        #region Процедурная пульсация сердца

        /// <summary>
        /// Задает новую частоту пульсации в ударах в минуту (BPM).
        /// </summary>
        public void SetHeartRateBPM(float bpm)
        {
            heartRateBPM = Mathf.Clamp(bpm, 40f, 180f);
        }

        /// <summary>
        /// Включает/выключает биение сердца.
        /// </summary>
        public void SetPulsationActive(bool active)
        {
            enablePulsation = active;
            if (!active)
            {
                transform.localScale = baseScale;
            }
        }

        private void UpdatePulsation()
        {
            if (!enablePulsation || heartRateBPM <= 0f) return;

            // Вычисляем частоту в герцах: f = BPM / 60
            float frequencyHz = heartRateBPM / 60f;
            pulsationTimer += Time.deltaTime * frequencyHz * Mathf.PI * 2f;

            // Двухфазная волна сердечного цикла: быстрый систолический толчок + диастолическое расслабление
            float wave = Mathf.Sin(pulsationTimer);
            float pulseWave = Mathf.Pow(Mathf.Clamp01(wave), 3f) * pulsationAmplitude;

            Vector3 pulsedScale = baseScale * (1f + pulseWave);
            transform.localScale = pulsedScale;
        }

        #endregion

        #region Обработка жестов (Вращение Drag и Масштабирование Pinch)

        private void HandleTouchGestures()
        {
            // Масштабирование двумя пальцами (Pinch-to-Zoom)
            if (AnatomyAR.Interaction.UniversalInputHelper.GetPinchScaleFactor(out float scaleFactor))
            {
                Vector3 newScale = transform.localScale * scaleFactor;
                float clampedUniform = Mathf.Clamp(newScale.x, minScale, maxScale);
                transform.localScale = Vector3.one * clampedUniform;
                baseScale = transform.localScale;
            }
            // Вращение пальцем во ВСЕХ плоскостях (Yaw и Pitch относительно ориентации камеры)
            else if (AnatomyAR.Interaction.UniversalInputHelper.GetOneFingerDragDelta(out Vector2 delta))
            {
                float rotY = -delta.x * rotationSpeed;
                float rotX = delta.y * rotationSpeed;

                Transform camTrans = Camera.main != null ? Camera.main.transform : null;
                Vector3 yawAxis = camTrans != null ? camTrans.up : Vector3.up;
                Vector3 pitchAxis = camTrans != null ? camTrans.right : Vector3.right;

                transform.Rotate(yawAxis, rotY, Space.World);
                transform.Rotate(pitchAxis, rotX, Space.World);
            }
        }

        private void HandleMouseDebugGestures()
        {
            // Вращение правой кнопкой мыши
            if (Input.GetMouseButton(1))
            {
                float rotX = Input.GetAxis("Mouse X") * rotationSpeed * 10f;
                float rotY = Input.GetAxis("Mouse Y") * rotationSpeed * 10f;

                transform.Rotate(Vector3.up, -rotX, Space.World);
                transform.Rotate(Camera.main != null ? Camera.main.transform.right : Vector3.right, rotY, Space.World);
            }

            // Масштабирование колесом мыши
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (Mathf.Abs(scroll) > 0.001f)
            {
                float newScaleFactor = 1f + scroll * 0.5f;
                Vector3 newScale = transform.localScale * newScaleFactor;
                float clamped = Mathf.Clamp(newScale.x, minScale, maxScale);
                transform.localScale = Vector3.one * clamped;
                baseScale = transform.localScale;
            }
        }

        #endregion

        /// <summary>
        /// Задает базовый масштаб и сбрасывает поворот при смене модели.
        /// </summary>
        public void ResetOrientation(float defaultScale)
        {
            transform.localRotation = Quaternion.identity;
            baseScale = Vector3.one * defaultScale;
            transform.localScale = baseScale;
            targetExplodeFactor = 0f;
            currentExplodeFactor = 0f;

            if (parts.Count == 0)
            {
                CollectAllChildParts();
            }

            for (int i = 0; i < parts.Count; i++)
            {
                if (parts[i] != null)
                {
                    parts[i].ResetPosition();
                }
            }
        }
    }
}
