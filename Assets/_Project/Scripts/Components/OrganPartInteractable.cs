using UnityEngine;
using AnatomyAR.Data;

namespace AnatomyAR.Components
{
    /// <summary>
    /// Компонент, навешиваемый на каждый отдельный дочерний меш (sub-mesh) органа.
    /// Отвечает за локальную трансформацию при разлете (Exploded View),
    /// выделение через MaterialPropertyBlock, режим X-Ray и точку привязки 3D-аннотации.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class OrganPartInteractable : MonoBehaviour
    {
        [Header("Данные")]
        [Tooltip("Ссылка на медицинскую карточку ScriptableObject для данного отдела органа")]
        [SerializeField] private OrganPartDataSO partData;

        [Header("Аннотации и визуал")]
        [Tooltip("Пользовательская точка привязки линии выноски. Если не назначена, вычисляется автоматически по центру меша")]
        [SerializeField] private Transform annotationAnchor;

        // Исходные координаты в локальном пространстве родительского органа
        public Vector3 InitialLocalPosition { get; private set; }
        public Quaternion InitialLocalRotation { get; private set; }
        public Vector3 InitialLocalScale { get; private set; }

        // Направление вектора разлета относительно центра родительского объекта
        public Vector3 ExplodeDirection { get; private set; }

        // Кэшированные компоненты рендеринга
        private Renderer cachedRenderer;
        private Material[] originalMaterials;
        private MaterialPropertyBlock propertyBlock;

        // Идентификаторы свойств шейдера (Standard и URP) для быстрой передачи без аллокаций строк
        private static readonly int EmissionColorProp = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorProp = Shader.PropertyToID("_BaseColor");
        private static readonly int MainColorProp = Shader.PropertyToID("_Color");

        private bool isCurrentHighlighted = false;
        private bool isCurrentXRay = false;

        public OrganPartDataSO PartData => partData;
        public Renderer CachedRenderer => cachedRenderer;
        public bool IsHighlighted => isCurrentHighlighted;
        public bool IsXRay => isCurrentXRay;

        /// <summary>
        /// Возвращает мировую точку привязки выноски (LineRenderer / Billboard label).
        /// </summary>
        public Vector3 AnnotationWorldPosition
        {
            get
            {
                if (annotationAnchor != null)
                    return annotationAnchor.position;

                if (cachedRenderer != null)
                    return cachedRenderer.bounds.center;

                return transform.position;
            }
        }

        private void Awake()
        {
            cachedRenderer = GetComponent<Renderer>();
            originalMaterials = cachedRenderer.sharedMaterials;
            propertyBlock = new MaterialPropertyBlock();

            // Запоминаем дефолтную геометрию в локальных координатах
            InitialLocalPosition = transform.localPosition;
            InitialLocalRotation = transform.localRotation;
            InitialLocalScale = transform.localScale;

            // Расчет вектора направления разлета («Взрыв-схема»):
            Vector3 dir = InitialLocalPosition;
            if (dir.sqrMagnitude < 0.0001f && cachedRenderer != null)
            {
                // Для сегментированных органов берем центр масс меша относительно родителя
                dir = transform.parent != null 
                    ? transform.parent.InverseTransformPoint(cachedRenderer.bounds.center) 
                    : cachedRenderer.bounds.center;
            }
            if (dir.sqrMagnitude < 0.0001f)
            {
                dir = transform.up;
            }
            ExplodeDirection = dir.normalized;

            // Гарантируем наличие коллайдера для работы Physics.Raycast с экрана AR
            EnsureColliderExists();
        }

        /// <summary>
        /// Проверяет наличие коллайдера. При отсутствии автоматически добавляет MeshCollider (convex) или BoxCollider.
        /// </summary>
        private void EnsureColliderExists()
        {
            Collider col = GetComponent<Collider>();
            if (col == null)
            {
                MeshFilter meshFilter = GetComponent<MeshFilter>();
                if (meshFilter != null && meshFilter.sharedMesh != null)
                {
                    MeshCollider meshCol = gameObject.AddComponent<MeshCollider>();
                    meshCol.convex = true;
                }
                else
                {
                    gameObject.AddComponent<BoxCollider>();
                }
            }
        }

        /// <summary>
        /// Управляет подсветкой органа при наведении или выборе через MaterialPropertyBlock.
        /// Не создает дубликатов материалов в оперативной памяти мобильного устройства.
        /// </summary>
        /// <param name="isHighlighted">Включить/выключить подсветку</param>
        /// <param name="highlightColor">Цвет свечения (Emission)</param>
        public void SetHighlight(bool isHighlighted, Color highlightColor)
        {
            if (cachedRenderer == null) return;

            isCurrentHighlighted = isHighlighted;
            cachedRenderer.GetPropertyBlock(propertyBlock);

            if (isHighlighted)
            {
                // Мягкое свечение контура без искажения основного цвета органа
                propertyBlock.SetColor(EmissionColorProp, highlightColor * 0.45f);
            }
            else
            {
                propertyBlock.SetColor(EmissionColorProp, Color.black);
            }

            cachedRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Переключает меш между оригинальными PBR-материалами и полупрозрачным рентген-материалом (X-Ray).
        /// </summary>
        /// <param name="isXRay">Режим рентгена</param>
        /// <param name="xrayMaterial">Материал с рентген-шейдером URP (Fresnel)</param>
        public void SetXRayMode(bool isXRay, Material xrayMaterial)
        {
            if (cachedRenderer == null) return;

            isCurrentXRay = isXRay;

            if (isXRay && xrayMaterial != null)
            {
                Material[] xrayMats = new Material[originalMaterials.Length];
                for (int i = 0; i < xrayMats.Length; i++)
                {
                    xrayMats[i] = xrayMaterial;
                }
                cachedRenderer.materials = xrayMats;
            }
            else
            {
                cachedRenderer.sharedMaterials = originalMaterials;
            }

            // Восстанавливаем состояние подсветки после смены материала
            SetHighlight(isCurrentHighlighted, isCurrentHighlighted ? Color.cyan : Color.black);
        }

        /// <summary>
        /// Смещает положение части органа в локальном пространстве вдоль вектора разлета.
        /// </summary>
        /// <param name="distance">Дистанция смещения (в метрах)</param>
        public void SetExplodeOffset(float distance)
        {
            transform.localPosition = InitialLocalPosition + ExplodeDirection * distance;
        }

        /// <summary>
        /// Сбрасывает локальную позицию к исходной анатомической точке.
        /// </summary>
        public void ResetPosition()
        {
            transform.localPosition = InitialLocalPosition;
            transform.localRotation = InitialLocalRotation;
            transform.localScale = InitialLocalScale;
        }

        /// <summary>
        /// Включает/выключает видимость данного сегмента (при фильтрации анатомических систем).
        /// </summary>
        public void SetVisibility(bool isVisible)
        {
            gameObject.SetActive(isVisible);
        }

        /// <summary>
        /// Программная инициализация данных (полезна для редактора и генерации префабов).
        /// </summary>
        public void InitializeData(OrganPartDataSO data, Transform anchor = null)
        {
            partData = data;
            if (anchor != null)
            {
                annotationAnchor = anchor;
            }
        }
    }
}
