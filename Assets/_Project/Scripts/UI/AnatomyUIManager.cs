using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AnatomyAR.AR;
using AnatomyAR.Components;
using AnatomyAR.Controllers;
using AnatomyAR.Data;
using AnatomyAR.Interaction;
using AnatomyAR.Quiz;

namespace AnatomyAR.UI
{
    /// <summary>
    /// Главный менеджер мобильного интерфейса (Screen Space Overlay / Camera uGUI).
    /// Выполнен в современном полупрозрачном стиле Glassmorphism.
    /// Связывает слайдеры, выпадающие списки, переключатели слоев, аудиогид и нижнюю карточку описания (Bottom Sheet).
    /// </summary>
    public class AnatomyUIManager : MonoBehaviour
    {
        [Header("Ссылки на ключевые менеджеры")]
        [SerializeField] private ARPlacementAndTrackingManager arPlacementManager;
        [SerializeField] private SelectionAndAnnotationManager selectionManager;
        [SerializeField] private AnatomyQuizManager quizManager;
        [SerializeField] private AnatomyDataSO anatomyDatabase;

        [Header("Верхняя панель управления")]
        [Tooltip("Выпадающий список выбора 3D-модели органа")]
        [SerializeField] private TMP_Dropdown modelSelectorDropdown;

        [Tooltip("Кнопка переразмещения модели в пространстве")]
        [SerializeField] private Button repositionButton;

        [Tooltip("Кнопка переключения режима Рентгена (X-Ray)")]
        [SerializeField] private Button toggleXRayButton;

        [Tooltip("Кнопка запуска/выхода из режима Викторины")]
        [SerializeField] private Button toggleQuizModeButton;

        [Header("Меню выбора органов (Гамбургер и список)")]
        [SerializeField] private Button openMenuButton;
        [SerializeField] private Button bottomMenuButton;
        [SerializeField] private GameObject organMenuPanel;
        [SerializeField] private Button selectHeartButton;
        [SerializeField] private Button selectBrainButton;
        [SerializeField] private Button closeMenuButton;
        [SerializeField] private TextMeshProUGUI currentOrganBadgeText;

        [Header("Панель манипуляций (Слайдеры)")]
        [Tooltip("Контейнер слайдеров")]
        [SerializeField] private GameObject manipulationPanel;

        [Tooltip("Слайдер режима 'Взрыв-схема' (Exploded View)")]
        [SerializeField] private Slider explodeSlider;
        [SerializeField] private TextMeshProUGUI explodeValueText;

        [Tooltip("Слайдер частоты сердечных сокращений (BPM)")]
        [SerializeField] private Slider bpmSlider;
        [SerializeField] private TextMeshProUGUI bpmValueText;
        [SerializeField] private GameObject bpmContainer;

        [Header("Панель фильтрации слоев систем")]
        [Tooltip("Тогглы для включения/выключения анатомических систем")]
        [SerializeField] private Toggle toggleSkeletal;
        [SerializeField] private Toggle toggleCardiovascular;
        [SerializeField] private Toggle toggleNervous;
        [SerializeField] private Toggle toggleRespiratory;
        [SerializeField] private Toggle toggleDigestive;

        [Header("Выдвижная карточка информации (Bottom Sheet)")]
        [SerializeField] private GameObject bottomSheetPanel;
        [SerializeField] private TextMeshProUGUI sheetTitleRuText;
        [SerializeField] private TextMeshProUGUI sheetTitleLatinText;
        [SerializeField] private TextMeshProUGUI sheetSystemBadgeText;
        [SerializeField] private TextMeshProUGUI sheetDescriptionText;
        [SerializeField] private TextMeshProUGUI sheetClinicalFactText;
        [SerializeField] private Button playAudioButton;
        [SerializeField] private Button closeSheetButton;
        [SerializeField] private AudioSource voiceoverAudioSource;

        [Header("Визуальное руководство по наведению на плоскость")]
        [SerializeField] private GameObject planeDetectionPrompt;
        [SerializeField] private Button directPlaceButton;

        private OrganController activeOrganController;
        private bool isXRayActive = false;

        private void Awake()
        {
            if (arPlacementManager == null) arPlacementManager = FindObjectOfType<ARPlacementAndTrackingManager>();
            if (selectionManager == null) selectionManager = FindObjectOfType<SelectionAndAnnotationManager>();
            if (quizManager == null) quizManager = FindObjectOfType<AnatomyQuizManager>();
            if (anatomyDatabase == null && arPlacementManager != null) anatomyDatabase = arPlacementManager.AnatomyDatabase;

            if (organMenuPanel == null)
            {
                Transform t = transform.Find("OrganMenu_Modal");
                if (t != null) organMenuPanel = t.gameObject;
            }

            if (bottomSheetPanel == null)
            {
                Transform t = transform.Find("InfoCard_Panel");
                if (t != null)
                {
                    bottomSheetPanel = t.gameObject;
                    if (sheetTitleRuText == null) sheetTitleRuText = t.Find("TitleRu")?.GetComponent<TextMeshProUGUI>();
                    if (sheetTitleLatinText == null) sheetTitleLatinText = t.Find("TitleLat")?.GetComponent<TextMeshProUGUI>();
                    if (sheetSystemBadgeText == null) sheetSystemBadgeText = t.Find("SystemBadge")?.GetComponent<TextMeshProUGUI>();
                    if (sheetDescriptionText == null) sheetDescriptionText = t.Find("Description")?.GetComponent<TextMeshProUGUI>();
                    if (sheetClinicalFactText == null) sheetClinicalFactText = t.Find("ClinicalFact")?.GetComponent<TextMeshProUGUI>();
                    if (closeSheetButton == null) closeSheetButton = t.Find("CloseCardButton")?.GetComponent<Button>();
                }
            }

            if (planeDetectionPrompt == null)
            {
                Transform t = transform.Find("PlaneDetection_Prompt");
                if (t != null) planeDetectionPrompt = t.gameObject;
            }

            if (directPlaceButton == null && planeDetectionPrompt != null)
            {
                directPlaceButton = planeDetectionPrompt.GetComponentInChildren<Button>(true);
            }

            if (bottomMenuButton == null)
            {
                Transform t = transform.Find("Bottom_Dock_Bar/BottomMenuButton");
                if (t != null) bottomMenuButton = t.GetComponent<Button>();
            }

            if (repositionButton == null)
            {
                Transform t = transform.Find("Bottom_Dock_Bar/RepositionButton");
                if (t != null) repositionButton = t.GetComponent<Button>();
            }

            if (closeMenuButton == null && organMenuPanel != null)
            {
                Transform t = organMenuPanel.transform.Find("CloseMenuButton");
                if (t != null) closeMenuButton = t.GetComponent<Button>();
            }

            if (currentOrganBadgeText == null)
            {
                Transform t = transform.Find("TopBar_Panel/BadgeText");
                if (t != null) currentOrganBadgeText = t.GetComponent<TextMeshProUGUI>();
            }

            if (bottomSheetPanel != null) bottomSheetPanel.SetActive(false);
            if (organMenuPanel != null) organMenuPanel.SetActive(false);
            if (voiceoverAudioSource == null) voiceoverAudioSource = gameObject.AddComponent<AudioSource>();
        }

        private void Start()
        {
            InitializeDropdown();
            SubscribeEvents();
        }

        private void OnDestroy()
        {
            UnsubscribeEvents();
        }

        private void InitializeDropdown()
        {
            if (modelSelectorDropdown == null || anatomyDatabase == null) return;

            modelSelectorDropdown.ClearOptions();
            List<string> options = new List<string>();

            for (int i = 0; i < anatomyDatabase.AvailableModels.Count; i++)
            {
                options.Add(anatomyDatabase.AvailableModels[i].TitleRu);
            }

            modelSelectorDropdown.AddOptions(options);
            modelSelectorDropdown.onValueChanged.AddListener(OnModelDropdownChanged);
        }

        private void SubscribeEvents()
        {
            // Подписка на AR события
            if (arPlacementManager != null)
            {
                arPlacementManager.OnOrganPlacedOrSwitched += HandleOrganPlacedOrSwitched;
                arPlacementManager.OnPlaneDetectionStateChanged += HandlePlaneStateChanged;
            }

            // Подписка на выбор деталей
            if (selectionManager != null)
            {
                selectionManager.OnPartSelected += HandlePartSelected;
                selectionManager.OnSelectionCleared += HandleSelectionCleared;
            }

            // Настройка кнопок
            if (directPlaceButton != null)
            {
                directPlaceButton.onClick.RemoveAllListeners();
                directPlaceButton.onClick.AddListener(() =>
                {
                    if (arPlacementManager != null)
                    {
                        arPlacementManager.PlaceModelDirectly();
                    }
                });
            }

            if (repositionButton != null)
            {
                repositionButton.onClick.RemoveAllListeners();
                repositionButton.onClick.AddListener(OnRepositionClicked);
            }

            if (closeSheetButton != null)
            {
                closeSheetButton.onClick.RemoveAllListeners();
                closeSheetButton.onClick.AddListener(OnCloseSheetClicked);
            }

            // Настройка меню выбора органов
            if (openMenuButton != null)
            {
                openMenuButton.onClick.RemoveAllListeners();
                openMenuButton.onClick.AddListener(ToggleOrganMenu);
            }

            if (bottomMenuButton != null)
            {
                bottomMenuButton.onClick.RemoveAllListeners();
                bottomMenuButton.onClick.AddListener(ToggleOrganMenu);
            }

            if (closeMenuButton != null)
            {
                closeMenuButton.onClick.RemoveAllListeners();
                closeMenuButton.onClick.AddListener(() => { if (organMenuPanel != null) organMenuPanel.SetActive(false); });
            }

            // Динамически привязываем все 13 кнопок меню органов
            BindOrganMenuButtons();
        }

        private void BindOrganMenuButtons()
        {
            if (organMenuPanel == null || anatomyDatabase == null) return;

            Button[] buttons = organMenuPanel.GetComponentsInChildren<Button>(true);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button btn = buttons[i];
                if (btn.name.StartsWith("Btn_"))
                {
                    string modelId = btn.name.Substring("Btn_".Length);
                    int targetIdx = -1;
                    for (int m = 0; m < anatomyDatabase.AvailableModels.Count; m++)
                    {
                        if (anatomyDatabase.AvailableModels[m].ModelId == modelId)
                        {
                            targetIdx = m;
                            break;
                        }
                    }
                    if (targetIdx >= 0)
                    {
                        int capturedIndex = targetIdx;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => SelectOrganByIndex(capturedIndex));
                    }
                }
            }
        }

        private void UnsubscribeEvents()
        {
            if (arPlacementManager != null)
            {
                arPlacementManager.OnOrganPlacedOrSwitched -= HandleOrganPlacedOrSwitched;
                arPlacementManager.OnPlaneDetectionStateChanged -= HandlePlaneStateChanged;
            }

            if (selectionManager != null)
            {
                selectionManager.OnPartSelected -= HandlePartSelected;
                selectionManager.OnSelectionCleared -= HandleSelectionCleared;
            }
        }

        #region Обработка событий модели

        private void HandleOrganPlacedOrSwitched(OrganController controller, AnatomyModelSO modelData)
        {
            activeOrganController = controller;

            if (selectionManager != null)
            {
                selectionManager.BindActiveOrganController(controller);
            }

            if (manipulationPanel != null)
            {
                manipulationPanel.SetActive(true);
            }

            if (planeDetectionPrompt != null)
            {
                planeDetectionPrompt.SetActive(false);
            }

            // Настройка доступности слайдера пульсации
            if (bpmContainer != null)
            {
                bpmContainer.SetActive(modelData.SupportsPulsation);
                if (modelData.SupportsPulsation && bpmSlider != null)
                {
                    bpmSlider.value = modelData.DefaultBPM;
                    if (bpmValueText != null) bpmValueText.text = $"{modelData.DefaultBPM:F0} BPM";
                }
            }

            // Сброс слайдера разлета
            if (explodeSlider != null)
            {
                explodeSlider.value = 0f;
                if (explodeValueText != null) explodeValueText.text = "0%";
            }

            if (currentOrganBadgeText != null && modelData != null)
            {
                currentOrganBadgeText.text = modelData.TitleRu;
            }

            // Синхронизация режима викторины
            if (quizManager != null)
            {
                quizManager.SetCurrentModel(modelData, controller);
            }
        }

        private void HandlePlaneStateChanged(bool planesActive)
        {
            if (planeDetectionPrompt != null)
            {
                planeDetectionPrompt.SetActive(planesActive);
            }
        }

        #endregion

        #region Слайдеры и переключатели

        private void OnExplodeSliderChanged(float value)
        {
            if (activeOrganController != null)
            {
                activeOrganController.SetExplodeFactor(value);
            }
            if (explodeValueText != null)
            {
                explodeValueText.text = $"{(value * 100f):F0}%";
            }
        }

        private void OnBpmSliderChanged(float value)
        {
            if (activeOrganController != null)
            {
                activeOrganController.SetHeartRateBPM(value);
            }
            if (bpmValueText != null)
            {
                bpmValueText.text = $"{value:F0} BPM";
            }
        }

        private void SetLayer(OrganSystemType system, bool isVisible)
        {
            if (activeOrganController != null)
            {
                activeOrganController.ToggleSystemLayer(system, isVisible);
            }
        }

        private void OnModelDropdownChanged(int index)
        {
            if (anatomyDatabase != null && index >= 0 && index < anatomyDatabase.AvailableModels.Count)
            {
                AnatomyModelSO selectedModel = anatomyDatabase.AvailableModels[index];
                if (arPlacementManager != null)
                {
                    arPlacementManager.SwitchActiveOrgan(selectedModel);
                }
            }
        }

        private void OnRepositionClicked()
        {
            if (arPlacementManager != null)
            {
                arPlacementManager.RequestReposition();
            }
            if (manipulationPanel != null)
            {
                manipulationPanel.SetActive(false);
            }
            if (bottomSheetPanel != null)
            {
                bottomSheetPanel.SetActive(false);
            }
            if (planeDetectionPrompt != null)
            {
                planeDetectionPrompt.SetActive(true);
            }
        }

        private void OnToggleXRayClicked()
        {
            isXRayActive = !isXRayActive;
            if (activeOrganController != null)
            {
                activeOrganController.SetXRayMode(isXRayActive, selectionManager != null ? selectionManager.CurrentlySelectedPart : null);
            }
        }

        private void OnToggleQuizModeClicked()
        {
            if (quizManager != null)
            {
                quizManager.ToggleQuizMode();
            }
        }

        public void ToggleOrganMenu()
        {
            if (organMenuPanel != null)
            {
                organMenuPanel.SetActive(!organMenuPanel.activeSelf);
            }
        }

        public void SelectOrganByIndex(int index)
        {
            if (anatomyDatabase != null && index >= 0 && index < anatomyDatabase.AvailableModels.Count)
            {
                AnatomyModelSO selectedModel = anatomyDatabase.AvailableModels[index];
                if (arPlacementManager != null)
                {
                    arPlacementManager.SwitchActiveOrgan(selectedModel);
                }
                if (currentOrganBadgeText != null)
                {
                    currentOrganBadgeText.text = selectedModel.TitleRu;
                }
            }
            if (organMenuPanel != null)
            {
                organMenuPanel.SetActive(false);
            }
        }

        #endregion

        #region Нижняя карточка (Bottom Sheet)

        private void HandlePartSelected(OrganPartInteractable part, OrganPartDataSO data)
        {
            if (data == null || bottomSheetPanel == null) return;

            bottomSheetPanel.SetActive(true);

            if (sheetTitleRuText != null) sheetTitleRuText.text = data.TitleRu;
            if (sheetTitleLatinText != null) sheetTitleLatinText.text = $"<i>{data.TitleLatin}</i>";
            if (sheetSystemBadgeText != null) sheetSystemBadgeText.text = FormatSystemRu(data.SystemType);
            if (sheetDescriptionText != null)
            {
                string factFormatted = string.IsNullOrEmpty(data.ClinicalFact) ? "" : $"\n\n<color=#FFD672><b>Клиническое значение:</b> {data.ClinicalFact}</color>";
                sheetDescriptionText.text = $"{data.Description}{factFormatted}";
            }
            if (sheetClinicalFactText != null)
            {
                sheetClinicalFactText.gameObject.SetActive(false);
            }

            Canvas.ForceUpdateCanvases();
            RectTransform cardRT = bottomSheetPanel.GetComponent<RectTransform>();
            if (cardRT != null)
            {
                UnityEngine.UI.LayoutRebuilder.ForceRebuildLayoutImmediate(cardRT);
            }

            if (playAudioButton != null)
            {
                playAudioButton.gameObject.SetActive(data.VoiceoverClip != null);
                playAudioButton.onClick.RemoveAllListeners();
                playAudioButton.onClick.AddListener(() =>
                {
                    if (voiceoverAudioSource != null && data.VoiceoverClip != null)
                    {
                        voiceoverAudioSource.clip = data.VoiceoverClip;
                        voiceoverAudioSource.Play();
                    }
                });
            }
        }

        private void HandleSelectionCleared()
        {
            if (bottomSheetPanel != null)
            {
                bottomSheetPanel.SetActive(false);
            }
            if (voiceoverAudioSource != null && voiceoverAudioSource.isPlaying)
            {
                voiceoverAudioSource.Stop();
            }
        }

        private void OnCloseSheetClicked()
        {
            if (selectionManager != null)
            {
                selectionManager.ClearSelection();
            }
            HandleSelectionCleared();
        }

        private string FormatSystemRu(OrganSystemType type)
        {
            switch (type)
            {
                case OrganSystemType.Cardiovascular: return "Сердечно-сосудистая система";
                case OrganSystemType.Nervous: return "Нервная система";
                case OrganSystemType.Respiratory: return "Дыхательная система";
                case OrganSystemType.Digestive: return "Пищеварительная / Мочеполовая система";
                case OrganSystemType.Skeletal: return "Опорно-двигательная система";
                case OrganSystemType.Muscular: return "Мышечная система";
                default: return "Анатомическая система";
            }
        }

        #endregion
    }
}
