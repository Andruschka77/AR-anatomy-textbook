using System;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using AnatomyAR.Components;
using AnatomyAR.Controllers;
using AnatomyAR.Data;

namespace AnatomyAR.Interaction
{
    /// <summary>
    /// Система выбора (Raycast Selection), подсветки и трехмерных выносок (World Space Annotations).
    /// Пускает луч с экрана, определяет задетый sub-mesh, подсвечивает его через MaterialPropertyBlock,
    /// активирует трехмерную выноску с LineRenderer и передает данные в интерфейс.
    /// </summary>
    public class SelectionAndAnnotationManager : MonoBehaviour
    {
        [Header("Настройки луча")]
        [Tooltip("Максимальная дистанция луча Raycast в метрах")]
        [SerializeField] private float maxRayDistance = 20f;

        [Tooltip("Слои коллайдеров для рейкаста (по умолчанию Default)")]
        [SerializeField] private LayerMask organLayerMask = ~0;

        [Header("Подсветка")]
        [Tooltip("Цвет контурного свечения выбранного органа")]
        [SerializeField] private Color selectionHighlightColor = new Color(0.1f, 0.85f, 1.0f, 1.0f);

        [Tooltip("Автоматически включать рентген для остальных органов при выборе одного (по умолчанию выключено, чтобы модель не белела)")]
        [SerializeField] private bool dimOtherPartsOnSelection = false;

        [Header("3D-выноска (World Space Label)")]
        [Tooltip("Корневой объект выноски с BillboardUI и Canvas")]
        [SerializeField] private GameObject annotationRoot;

        [Tooltip("Линия соединения точки органа и плашки с надписью")]
        [SerializeField] private LineRenderer calloutLine;

        [Tooltip("Текстовое поле заголовка на русском (TMP)")]
        [SerializeField] private TextMeshProUGUI titleRuText;

        [Tooltip("Текстовое поле заголовка на латыни (TMP)")]
        [SerializeField] private TextMeshProUGUI titleLatinText;

        [Tooltip("Смещение плашки с текстом относительно точки привязки органа")]
        [SerializeField] private Vector3 annotationOffset = new Vector3(0.12f, 0.15f, -0.05f);

        // Событие выбора для UI (передает выбранную часть и данные)
        public event Action<OrganPartInteractable, OrganPartDataSO> OnPartSelected;
        public event Action OnSelectionCleared;

        private OrganPartInteractable currentlySelectedPart = null;
        private OrganController activeOrganController = null;
        private Camera arCamera;

        public OrganPartInteractable CurrentlySelectedPart => currentlySelectedPart;

        private void Awake()
        {
            arCamera = Camera.main;
            if (annotationRoot != null)
            {
                annotationRoot.SetActive(false);
            }
        }

        private void Update()
        {
            HandleSelectionInput();
            UpdateAnnotationLine();
        }

        /// <summary>
        /// Привязывает контроллер текущего активного органа при спавне/смене.
        /// </summary>
        public void BindActiveOrganController(OrganController controller)
        {
            ClearSelection();
            activeOrganController = controller;
        }

        private void HandleSelectionInput()
        {
            if (!UniversalInputHelper.GetTapDown(out Vector2 inputPos))
            {
                return;
            }

            // Игнорируем нажатия ТОЛЬКО если палец попал по интерактивной кнопке/слайдеру
            if (UniversalInputHelper.IsPointerOverInteractiveUI(inputPos))
            {
                return;
            }

            if (arCamera == null)
            {
                arCamera = Camera.main;
                if (arCamera == null) return;
            }

            Ray ray = arCamera.ScreenPointToRay(inputPos);
            if (Physics.Raycast(ray, out RaycastHit hit, maxRayDistance, organLayerMask))
            {
                OrganPartInteractable part = hit.collider.GetComponentInParent<OrganPartInteractable>();
                if (part != null)
                {
                    SelectPart(part);
                    return;
                }
            }

            // Тап в пустое пространство снимает выделение
            ClearSelection();
        }

        /// <summary>
        /// Выделяет указанную часть органа и разворачивает 3D-выноску.
        /// </summary>
        public void SelectPart(OrganPartInteractable part)
        {
            if (part == null) return;

            // Если кликнули на уже выбранную часть - не пересоздаем выноску
            if (currentlySelectedPart == part)
            {
                return;
            }

            // Снимаем подсветку с предыдущего органа
            if (currentlySelectedPart != null)
            {
                currentlySelectedPart.SetHighlight(false, Color.black);
            }

            currentlySelectedPart = part;
            currentlySelectedPart.SetHighlight(true, selectionHighlightColor);

            // Опционально включаем режим рентгена для остальных частей
            if (dimOtherPartsOnSelection && activeOrganController != null)
            {
                activeOrganController.SetXRayMode(true, currentlySelectedPart);
            }

            // Настраиваем 3D-выноску над органом
            ShowAnnotation(part);

            OnPartSelected?.Invoke(part, part.PartData);
        }

        /// <summary>
        /// Снимает выделение со всех частей органа и скрывает 3D-выноску.
        /// </summary>
        public void ClearSelection()
        {
            if (currentlySelectedPart != null)
            {
                currentlySelectedPart.SetHighlight(false, Color.black);
                currentlySelectedPart = null;
            }

            if (activeOrganController != null)
            {
                activeOrganController.SetXRayMode(false, null);
            }

            if (annotationRoot != null)
            {
                annotationRoot.SetActive(false);
            }

            OnSelectionCleared?.Invoke();
        }

        private void ShowAnnotation(OrganPartInteractable part)
        {
            if (annotationRoot == null) return;

            annotationRoot.SetActive(true);

            Vector3 originPos = part.AnnotationWorldPosition;
            Vector3 labelPos = originPos + annotationOffset;
            annotationRoot.transform.position = labelPos;

            if (part.PartData != null)
            {
                if (titleRuText != null) titleRuText.text = part.PartData.TitleRu;
                if (titleLatinText != null) titleLatinText.text = $"<i>{part.PartData.TitleLatin}</i>";
            }
            else
            {
                if (titleRuText != null) titleRuText.text = part.gameObject.name;
                if (titleLatinText != null) titleLatinText.text = "";
            }

            UpdateAnnotationLine();
        }

        private void UpdateAnnotationLine()
        {
            if (currentlySelectedPart == null || calloutLine == null || annotationRoot == null || !annotationRoot.activeSelf)
            {
                return;
            }

            Vector3 startPos = currentlySelectedPart.AnnotationWorldPosition;
            Vector3 endPos = annotationRoot.transform.position;

            calloutLine.positionCount = 2;
            calloutLine.SetPosition(0, startPos);
            calloutLine.SetPosition(1, endPos);
        }
    }
}
