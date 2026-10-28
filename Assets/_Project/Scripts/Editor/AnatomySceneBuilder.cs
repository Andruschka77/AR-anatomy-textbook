#if UNITY_EDITOR
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.XR.ARFoundation;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.XR;
using TMPro;
using AnatomyAR.Data;
using AnatomyAR.Components;
using AnatomyAR.Controllers;
using AnatomyAR.AR;
using AnatomyAR.Interaction;
using AnatomyAR.UI;
using AnatomyAR.Quiz;

namespace AnatomyAR.EditorScripts
{
    /// <summary>
    /// Редакторский генератор полной AR-сцены интерактивного атласа анатомии человека.
    /// Поддерживает:
    /// - 13 ключевых органов человека (Сердце, Мозг, Легкие с трахеей, Печень, Почки, Желудок, Кишечник,
    ///   Поджелудочная железа, Селезенка, Мочевой пузырь, Желчный пузырь, Щитовидная железа, Матка).
    /// - 100% реалистичные текстурированные PBR-материалы (Standard).
    /// - Полное отсутствие эмодзи (чистый русский текст без квадратов).
    /// - Сверху: только название органа.
    /// - Снизу: удобный док-бар из 2 кнопок [ МЕНЮ ОРГАНОВ ] и [ СБРОС ].
    /// - Жест горизонтального вращения на месте и масштабирование pinch.
    /// </summary>
    public static class AnatomySceneBuilder
    {
        private const string RootFolder = "Assets/_Project";
        private const string SoFolder = RootFolder + "/ScriptableObjects";
        private const string PartsFolder = SoFolder + "/Parts";
        private const string ModelsFolder = SoFolder + "/Models";
        private const string PrefabsFolder = RootFolder + "/Prefabs/Models";
        private const string ScenesFolder = RootFolder + "/Scenes";
        private const string MaterialsFolder = RootFolder + "/Materials";
        private const string ModelsAssetFolder = RootFolder + "/Models";
        private const string FontsFolder = RootFolder + "/Fonts";

        private static TMP_FontAsset s_CyrillicFont;

        [MenuItem("Anatomy AR/Setup Complete Project & Scene", false, 1)]
        public static void BuildCompleteProjectAndScene()
        {
            EnsureDirectories();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

            // 1. Кириллический шрифт
            s_CyrillicFont = GetOrCreateArialFontAsset();

            // 2. Безопасные текстурированные PBR-материалы
            Material xrayMat = CreateOrGetXRayMaterial();
            Material heartMat = CreateTexturedPbrMaterial("Mat_Heart_Realistic", "heart_texture.png", Color.white);
            Material lungTracheaMat = CreateTexturedPbrMaterial("Mat_Lung_Trachea", "lung_trachea_tex.png", Color.white);
            Material lungLobesMat = CreateTexturedPbrMaterial("Mat_Lung_Lobes", "lung_lobes_tex.png", Color.white);

            Material liverMat = CreateTexturedPbrMaterial("Mat_Liver", "liver_liver_001_Material_011_0.png", Color.white);
            Material liverGbMat = CreateTexturedPbrMaterial("Mat_Liver_GB", "liver_gallbladder_002_Material_013_0.png", Color.white);
            Material liverVesselMat = CreateTexturedPbrMaterial("Mat_Liver_Vessels", "liver_inferior_vena_cava_001_Material_012_0.png", Color.white);

            Material kidneyCortexMat = CreateTexturedPbrMaterial("Mat_Kidney_Cortex", "tex_kidney_cortex.png", Color.white);
            Material kidneyPyramidsMat = CreateTexturedPbrMaterial("Mat_Kidney_Pyramids", "tex_kidney_pyramids.png", Color.white);
            Material kidneyPelvisMat = CreateTexturedPbrMaterial("Mat_Kidney_Pelvis", "tex_kidney_pelvis.png", Color.white);
            Material kidneyVesselsMat = CreateTexturedPbrMaterial("Mat_Kidney_Vessels", "tex_kidney_pelvis.png", new Color(0.4f, 0.5f, 0.85f));

            Material stomachMat = CreateTexturedPbrMaterial("Mat_Stomach", "stomach_diffuse.png", Color.white);

            Material smallIntestineMat = CreateTexturedPbrMaterial("Mat_Intestine_Small", "tex_int_small.png", Color.white);
            Material largeIntestineMat = CreateTexturedPbrMaterial("Mat_Intestine_Large", "tex_int_large.png", Color.white);

            Material pancreasMat = CreateTexturedPbrMaterial("Mat_Pancreas", "tex_pancreas.png", Color.white);
            Material spleenMat = CreateTexturedPbrMaterial("Mat_Spleen", "tex_spleen.png", Color.white);
            Material bladderMat = CreateTexturedPbrMaterial("Mat_Bladder", "tex_bladder.png", Color.white);
            Material gallbladderMat = CreateTexturedPbrMaterial("Mat_Gallbladder", "tex_gallbladder.png", new Color(0.24f, 0.38f, 0.16f));
            Material thyroidMat = CreateTexturedPbrMaterial("Mat_Thyroid", "tex_thyroid.png", Color.white);
            Material parathyroidMat = CreateTexturedPbrMaterial("Mat_Parathyroid", "tex_parathyroid.png", Color.white);

            Material uterusMat = CreateTexturedPbrMaterial("Mat_Uterus", "tex_uterus.png", Color.white);

            // Доли мозга
            Material frontalMat = CreateTexturedPbrMaterial("Mat_Lobe_Frontal", "tex_brain_frontal.png", Color.white);
            Material parietalMat = CreateTexturedPbrMaterial("Mat_Lobe_Parietal", "tex_brain_parietal.png", Color.white);
            Material occipitalMat = CreateTexturedPbrMaterial("Mat_Lobe_Occipital", "tex_brain_occipital.png", Color.white);
            Material temporalMat = CreateTexturedPbrMaterial("Mat_Lobe_Temporal", "tex_brain_temporal.png", Color.white);
            Material cerebellumMat = CreateTexturedPbrMaterial("Mat_Lobe_Cerebellum", "tex_brain_cerebellum.png", Color.white);
            Material stemMat = CreateTexturedPbrMaterial("Mat_Lobe_Stem", "tex_brain_stem.png", Color.white);

            Material planeMat = CreatePlaneMaterial();

            // 3. Создание ScriptableObjects и 3D-префабов для всех 13 органов
            AnatomyDataSO database = CreateAll13OrganPrefabs(
                heartMat, frontalMat, parietalMat, occipitalMat, temporalMat, cerebellumMat, stemMat,
                lungTracheaMat, lungLobesMat,
                liverMat, liverGbMat, liverVesselMat,
                kidneyCortexMat, kidneyPyramidsMat, kidneyPelvisMat, kidneyVesselsMat,
                stomachMat, smallIntestineMat, largeIntestineMat, uterusMat,
                pancreasMat, spleenMat, bladderMat, gallbladderMat, thyroidMat, parathyroidMat, xrayMat);

            // 4. Создание сцены
            var newScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // 5. Освещение
            GameObject lightGO = new GameObject("Directional Light");
            Light dirLight = lightGO.AddComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(1f, 0.98f, 0.95f);
            dirLight.intensity = 1.35f;
            lightGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);

            // 6. EventSystem
            GameObject eventSystemGO = new GameObject("EventSystem");
            eventSystemGO.AddComponent<EventSystem>();
#if ENABLE_INPUT_SYSTEM
            eventSystemGO.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            eventSystemGO.AddComponent<StandaloneInputModule>();
#endif

            // 7. XR Origin
            GameObject xrOriginGO = new GameObject("XR Origin");
            Unity.XR.CoreUtils.XROrigin xrOrigin = xrOriginGO.AddComponent<Unity.XR.CoreUtils.XROrigin>();
            ARRaycastManager raycastMgr = xrOriginGO.AddComponent<ARRaycastManager>();
            ARPlaneManager planeMgr = xrOriginGO.AddComponent<ARPlaneManager>();
            ARAnchorManager anchorMgr = xrOriginGO.AddComponent<ARAnchorManager>();

            // Убираем огромное белое пятно полигона плоскости: прицел наведения теперь компактный белый кружок
            planeMgr.planePrefab = null;

            // Camera Offset (стандартная иерархия XROrigin)
            GameObject cameraOffsetGO = new GameObject("Camera Offset");
            cameraOffsetGO.transform.SetParent(xrOriginGO.transform, false);

            // 8. AR Camera
            GameObject cameraGO = new GameObject("AR Camera");
            cameraGO.transform.SetParent(cameraOffsetGO.transform, false);
            cameraGO.tag = "MainCamera";
            Camera arCam = cameraGO.AddComponent<Camera>();
            arCam.clearFlags = CameraClearFlags.SolidColor;
            arCam.backgroundColor = Color.black;
            arCam.nearClipPlane = 0.05f;
            arCam.farClipPlane = 30f;
            cameraGO.AddComponent<AudioListener>();
            cameraGO.AddComponent<ARCameraManager>();
            cameraGO.AddComponent<ARCameraBackground>();
            TrackedPoseDriver trackedPoseDriver = cameraGO.AddComponent<TrackedPoseDriver>();

            var positionAction = new InputAction("Position", binding: "<XRHMD>/centerEyePosition", expectedControlType: "Vector3");
            positionAction.AddBinding("<HandheldARInputDevice>/devicePosition");
            var rotationAction = new InputAction("Rotation", binding: "<XRHMD>/centerEyeRotation", expectedControlType: "Quaternion");
            rotationAction.AddBinding("<HandheldARInputDevice>/deviceRotation");
            trackedPoseDriver.positionInput = new InputActionProperty(positionAction);
            trackedPoseDriver.rotationInput = new InputActionProperty(rotationAction);

            xrOrigin.Camera = arCam;
            xrOrigin.Origin = xrOriginGO;
            xrOrigin.CameraFloorOffsetObject = cameraOffsetGO;

            // 9. AR Session
            GameObject arSessionGO = new GameObject("AR Session");
            arSessionGO.AddComponent<ARSession>();
            arSessionGO.AddComponent<ARInputManager>();

            // 10. Вспомогательный менеджер привязки и взаимодействия
            GameObject managersGO = new GameObject("AppManagers");
            ARPlacementAndTrackingManager placementMgr = managersGO.AddComponent<ARPlacementAndTrackingManager>();
            SelectionAndAnnotationManager selMgr = managersGO.AddComponent<SelectionAndAnnotationManager>();
            AnatomyQuizManager quizMgr = managersGO.AddComponent<AnatomyQuizManager>();

            // 11. Построение UI (Хедер сверху с названием, 2 кнопки снизу [МЕНЮ] и [СБРОС])
            GameObject canvasGO = new GameObject("Canvas_MainUI", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            Canvas canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.matchWidthOrHeight = 0.5f;

            AnatomyUIManager uiMgr = canvasGO.AddComponent<AnatomyUIManager>();
            BuildCleanUI(canvasGO, uiMgr, quizMgr, placementMgr, database);

            // 12. 3D Выноска над деталями (Billboard Callout)
            GameObject calloutRoot = new GameObject("WorldSpace_Callout_Anchor");
            calloutRoot.transform.SetParent(managersGO.transform, false);
            GameObject calloutCanvasGO = new GameObject("CalloutCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            calloutCanvasGO.transform.SetParent(calloutRoot.transform, false);
            Canvas cCanvas = calloutCanvasGO.GetComponent<Canvas>();
            cCanvas.renderMode = RenderMode.WorldSpace;
            calloutCanvasGO.AddComponent<BillboardUI>();
            RectTransform ccRT = calloutCanvasGO.GetComponent<RectTransform>();
            ccRT.sizeDelta = new Vector2(280, 100);
            calloutCanvasGO.transform.localScale = Vector3.one * 0.001f;

            GameObject calloutPlate = CreateUIPanel(calloutCanvasGO.transform, "Plate", Vector2.zero, Vector2.one, new Color(0.08f, 0.12f, 0.20f, 0.94f));
            TextMeshProUGUI cRu = CreateUIText(calloutPlate.transform, "TitleRu", "Название детали", new Vector2(0.06f, 0.50f), new Vector2(0.94f, 0.95f), 18, TextAlignmentOptions.Center, Color.white);
            TextMeshProUGUI cLat = CreateUIText(calloutPlate.transform, "TitleLat", "Nomina", new Vector2(0.06f, 0.05f), new Vector2(0.94f, 0.50f), 15, TextAlignmentOptions.Center, new Color(0f, 0.82f, 0.76f, 1f));
            cLat.fontStyle = FontStyles.Italic;

            LineRenderer line = calloutRoot.AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.startWidth = line.endWidth = 0.0025f;
            line.positionCount = 2;
            line.material = xrayMat;

            SetPrivateField(selMgr, "annotationRoot", calloutRoot);
            SetPrivateField(selMgr, "calloutLine", line);
            SetPrivateField(selMgr, "titleRuText", cRu);
            SetPrivateField(selMgr, "titleLatinText", cLat);

            // Связывание компонентов
            SetPrivateField(placementMgr, "raycastManager", raycastMgr);
            SetPrivateField(placementMgr, "planeManager", planeMgr);
            SetPrivateField(placementMgr, "anchorManager", anchorMgr);
            SetPrivateField(placementMgr, "anatomyDatabase", database);
            SetPrivateField(placementMgr, "defaultModel", database.AvailableModels[0]);
            SetPrivateField(placementMgr, "sessionOriginRoot", xrOriginGO.transform);

            SetPrivateField(uiMgr, "arPlacementManager", placementMgr);
            SetPrivateField(uiMgr, "selectionManager", selMgr);
            SetPrivateField(uiMgr, "quizManager", quizMgr);
            SetPrivateField(uiMgr, "anatomyDatabase", database);
            SetPrivateField(quizMgr, "selectionManager", selMgr);

            // 13. Сохранение сцены
            string scenePath = ScenesFolder + "/AR_Anatomy_MainScene.unity";
            EditorSceneManager.SaveScene(newScene, scenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("[Anatomy AR] Complete 13 Textured Organs & Clean UI successfully generated and saved to: " + scenePath);
        }

        #region Построение UI (Только название сверху, 2 кнопки снизу)

        private static void BuildCleanUI(GameObject canvasGO, AnatomyUIManager uiMgr, AnatomyQuizManager quizMgr, ARPlacementAndTrackingManager placementMgr, AnatomyDataSO database)
        {
            Transform root = canvasGO.transform;

            Color darkGlass = new Color(0.08f, 0.11f, 0.17f, 0.94f);
            Color buttonBaseColor = new Color(0.12f, 0.18f, 0.28f, 0.96f);
            Color accentCyan = new Color(0f, 0.85f, 0.78f, 1f);

            // ==========================================
            // 1. ВЕРХНИЙ ХЕДЕР (Только название органа)
            // ==========================================
            GameObject topBarGO = CreateUIPanel(root, "TopBar_Panel", new Vector2(0.06f, 0.92f), new Vector2(0.94f, 0.985f), darkGlass);
            TextMeshProUGUI badgeTMP = CreateUIText(topBarGO.transform, "BadgeText", "СЕРДЦЕ ЧЕЛОВЕКА",
                new Vector2(0.02f, 0f), new Vector2(0.98f, 1f), 28, TextAlignmentOptions.Center, accentCyan);
            badgeTMP.fontStyle = FontStyles.Bold;
            SetPrivateField(uiMgr, "currentOrganBadgeText", badgeTMP);

            // ==========================================
            // 2. МОДАЛЬНОЕ МЕНЮ ВЫБОРА ОРГАНА (13 органов в скролл-списке)
            // ==========================================
            GameObject organModalGO = CreateUIPanel(root, "OrganMenu_Modal", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.89f), new Color(0.06f, 0.09f, 0.15f, 0.98f));
            organModalGO.SetActive(false);
            SetPrivateField(uiMgr, "organMenuPanel", organModalGO);

            CreateUIText(organModalGO.transform, "ModalTitle", "ВЫБЕРИТЕ ОРГАН",
                new Vector2(0.05f, 0.93f), new Vector2(0.95f, 0.98f), 28, TextAlignmentOptions.Center, Color.white);

            GameObject scrollGO = new GameObject("ScrollView", typeof(RectTransform), typeof(ScrollRect), typeof(Image));
            scrollGO.transform.SetParent(organModalGO.transform, false);
            RectTransform scrollRT = scrollGO.GetComponent<RectTransform>();
            scrollRT.anchorMin = new Vector2(0.04f, 0.10f);
            scrollRT.anchorMax = new Vector2(0.96f, 0.91f);
            scrollRT.offsetMin = scrollRT.offsetMax = Vector2.zero;
            scrollGO.GetComponent<Image>().color = new Color(0.04f, 0.07f, 0.12f, 0.5f);

            GameObject viewportGO = new GameObject("Viewport", typeof(RectTransform), typeof(Mask), typeof(Image));
            viewportGO.transform.SetParent(scrollGO.transform, false);
            RectTransform viewRT = viewportGO.GetComponent<RectTransform>();
            viewRT.anchorMin = Vector2.zero;
            viewRT.anchorMax = Vector2.one;
            viewRT.offsetMin = viewRT.offsetMax = Vector2.zero;
            viewportGO.GetComponent<Image>().color = Color.white;
            viewportGO.GetComponent<Mask>().showMaskGraphic = false;

            GameObject contentGO = new GameObject("Content", typeof(RectTransform), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            contentGO.transform.SetParent(viewportGO.transform, false);
            RectTransform contentRT = contentGO.GetComponent<RectTransform>();
            contentRT.anchorMin = new Vector2(0f, 1f);
            contentRT.anchorMax = new Vector2(1f, 1f);
            contentRT.pivot = new Vector2(0.5f, 1f);

            VerticalLayoutGroup vlg = contentGO.GetComponent<VerticalLayoutGroup>();
            vlg.padding = new RectOffset(10, 10, 10, 10);
            vlg.spacing = 8f;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            ContentSizeFitter csf = contentGO.GetComponent<ContentSizeFitter>();
            csf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            ScrollRect sr = scrollGO.GetComponent<ScrollRect>();
            sr.content = contentRT;
            sr.viewport = viewRT;
            sr.horizontal = false;
            sr.vertical = true;

            for (int i = 0; i < database.AvailableModels.Count; i++)
            {
                int organIndex = i;
                AnatomyModelSO organSO = database.AvailableModels[i];

                GameObject btnGO = new GameObject("Btn_" + organSO.ModelId, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
                btnGO.transform.SetParent(contentGO.transform, false);
                btnGO.GetComponent<Image>().color = buttonBaseColor;
                LayoutElement le = btnGO.GetComponent<LayoutElement>();
                le.minHeight = 88f;
                le.preferredHeight = 88f;

                // 1. Фиксированный номерной бэйдж слева (01, 02, ... 13) для идеального ровного выравнивания
                GameObject badgeGO = CreateUIPanel(btnGO.transform, "IndexBadge", new Vector2(0.02f, 0.14f), new Vector2(0.13f, 0.86f), new Color(0.06f, 0.10f, 0.18f, 0.95f));
                string indexStr = (i + 1).ToString("D2");
                TextMeshProUGUI numTMP = CreateUIText(badgeGO.transform, "Num", indexStr, Vector2.zero, Vector2.one, 22, TextAlignmentOptions.Center, accentCyan);
                numTMP.fontStyle = FontStyles.Bold;

                // 2. Название органа справа со строго одинаковым отступом для всех строк
                CreateUIText(btnGO.transform, "Title", organSO.TitleRu, new Vector2(0.16f, 0f), new Vector2(0.96f, 1f), 24, TextAlignmentOptions.Left, Color.white);

                Button b = btnGO.GetComponent<Button>();
                b.onClick.AddListener(() => uiMgr.SelectOrganByIndex(organIndex));

                if (i == 0) SetPrivateField(uiMgr, "selectHeartButton", b);
                if (i == 1) SetPrivateField(uiMgr, "selectBrainButton", b);
            }

            Button btnCloseModal = CreateUIButton(organModalGO.transform, "CloseMenuButton", "Закрыть",
                new Vector2(0.25f, 0.015f), new Vector2(0.75f, 0.085f), new Color(0.28f, 0.16f, 0.18f, 0.95f), 22);
            SetPrivateField(uiMgr, "closeMenuButton", btnCloseModal);

            // ==========================================
            // 3. НИЖНИЙ ДОК-БАР (Ровно 2 кнопки: [МЕНЮ ОРГАНОВ] и [СБРОС])
            // ==========================================
            GameObject dockBarGO = CreateUIPanel(root, "Bottom_Dock_Bar", new Vector2(0.04f, 0.02f), new Vector2(0.96f, 0.10f), darkGlass);

            // Кнопка 1: [ МЕНЮ ОРГАНОВ ]
            Button btnMenu = CreateUIButton(dockBarGO.transform, "BottomMenuButton", "МЕНЮ ОРГАНОВ",
                new Vector2(0.02f, 0.10f), new Vector2(0.56f, 0.90f), buttonBaseColor, 24);
            btnMenu.onClick.AddListener(() => uiMgr.ToggleOrganMenu());
            SetPrivateField(uiMgr, "bottomMenuButton", btnMenu);

            // Кнопка 2: [ СБРОС ]
            Button btnRepo = CreateUIButton(dockBarGO.transform, "RepositionButton", "СБРОС",
                new Vector2(0.60f, 0.10f), new Vector2(0.98f, 0.90f), buttonBaseColor, 24);
            btnRepo.onClick.AddListener(() => placementMgr.RequestReposition());
            SetPrivateField(uiMgr, "repositionButton", btnRepo);

            // ==========================================
            // 4. ИНФОРМАЦИОННЫЙ БЛОК ВВЕРХУ (Под названием органа, авто-высота без пустоты снизу)
            // ==========================================
            GameObject infoCardGO = new GameObject("InfoCard_Panel", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup), typeof(ContentSizeFitter));
            infoCardGO.transform.SetParent(root, false);
            RectTransform cardRT = infoCardGO.GetComponent<RectTransform>();
            cardRT.anchorMin = new Vector2(0.04f, 0.90f);
            cardRT.anchorMax = new Vector2(0.96f, 0.90f);
            cardRT.pivot = new Vector2(0.5f, 1f);
            cardRT.anchoredPosition = new Vector2(0f, -15f);
            cardRT.sizeDelta = new Vector2(0f, 0f);

            Image cardImg = infoCardGO.GetComponent<Image>();
            cardImg.color = darkGlass;

            VerticalLayoutGroup cardVlg = infoCardGO.GetComponent<VerticalLayoutGroup>();
            cardVlg.padding = new RectOffset(18, 18, 14, 14);
            cardVlg.spacing = 6f;
            cardVlg.childControlWidth = true;
            cardVlg.childControlHeight = true;
            cardVlg.childForceExpandWidth = true;
            cardVlg.childForceExpandHeight = false;

            ContentSizeFitter cardCsf = infoCardGO.GetComponent<ContentSizeFitter>();
            cardCsf.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
            cardCsf.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            infoCardGO.SetActive(false);
            SetPrivateField(uiMgr, "bottomSheetPanel", infoCardGO);

            // Верхняя строка: Заголовок детали на русском + кнопка закрытия [X]
            GameObject headerRowGO = new GameObject("HeaderRow", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            headerRowGO.transform.SetParent(infoCardGO.transform, false);
            LayoutElement headerLE = headerRowGO.GetComponent<LayoutElement>();
            headerLE.preferredHeight = 36f;
            headerLE.minHeight = 36f;

            HorizontalLayoutGroup hlg = headerRowGO.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 8f;
            hlg.childControlWidth = true;
            hlg.childControlHeight = true;
            hlg.childForceExpandWidth = false;
            hlg.childForceExpandHeight = false;

            // Заголовок детали на русском (укрупненный шрифт 24pt)
            GameObject titleGO = new GameObject("TitleRu", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            titleGO.transform.SetParent(headerRowGO.transform, false);
            LayoutElement titleLE = titleGO.GetComponent<LayoutElement>();
            titleLE.flexibleWidth = 1f;
            TextMeshProUGUI titleRu = titleGO.GetComponent<TextMeshProUGUI>();
            titleRu.text = "Название анатомической части";
            titleRu.fontSize = 24;
            titleRu.alignment = TextAlignmentOptions.Left;
            titleRu.color = Color.white;
            titleRu.fontStyle = FontStyles.Bold;
            titleRu.enableWordWrapping = true;
            SetCyrillicFont(titleRu);

            // Кнопка закрытия [X] (аккуратный квадрат 34x34)
            GameObject closeBtnGO = new GameObject("CloseCardButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            closeBtnGO.transform.SetParent(headerRowGO.transform, false);
            closeBtnGO.GetComponent<Image>().color = new Color(0.42f, 0.14f, 0.18f, 0.95f);
            LayoutElement closeLE = closeBtnGO.GetComponent<LayoutElement>();
            closeLE.preferredWidth = 34f;
            closeLE.preferredHeight = 34f;
            closeLE.minWidth = 34f;
            closeLE.minHeight = 34f;
            closeLE.flexibleWidth = 0f;
            closeLE.flexibleHeight = 0f;
            CreateUIText(closeBtnGO.transform, "Text", "X", Vector2.zero, Vector2.one, 20, TextAlignmentOptions.Center, Color.white);
            Button btnCloseCard = closeBtnGO.GetComponent<Button>();
            SetPrivateField(uiMgr, "closeSheetButton", btnCloseCard);

            // Латинское название (увеличенный шрифт 18pt)
            GameObject latGO = new GameObject("TitleLat", typeof(RectTransform), typeof(TextMeshProUGUI));
            latGO.transform.SetParent(infoCardGO.transform, false);
            TextMeshProUGUI titleLat = latGO.GetComponent<TextMeshProUGUI>();
            titleLat.text = "Nomina Anatomica";
            titleLat.fontSize = 18;
            titleLat.alignment = TextAlignmentOptions.Left;
            titleLat.color = accentCyan;
            titleLat.fontStyle = FontStyles.Italic;
            titleLat.enableWordWrapping = true;
            SetCyrillicFont(titleLat);

            // Бейдж анатомической системы (увеличенный шрифт 15pt)
            GameObject sysGO = new GameObject("SystemBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            sysGO.transform.SetParent(infoCardGO.transform, false);
            TextMeshProUGUI sysBadge = sysGO.GetComponent<TextMeshProUGUI>();
            sysBadge.text = "Анатомическая система";
            sysBadge.fontSize = 15;
            sysBadge.alignment = TextAlignmentOptions.Left;
            sysBadge.color = new Color(1f, 0.85f, 0.35f);
            sysBadge.fontStyle = FontStyles.Bold;
            sysBadge.enableWordWrapping = true;
            SetCyrillicFont(sysBadge);

            // Подробное анатомическое описание и клинический факт (укрупненный шрифт 18pt)
            GameObject descGO = new GameObject("Description", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGO.transform.SetParent(infoCardGO.transform, false);
            TextMeshProUGUI descTMP = descGO.GetComponent<TextMeshProUGUI>();
            descTMP.text = "Описание...";
            descTMP.fontSize = 18;
            descTMP.lineSpacing = 4f;
            descTMP.alignment = TextAlignmentOptions.TopLeft;
            descTMP.color = new Color(0.92f, 0.94f, 0.97f);
            descTMP.enableWordWrapping = true;
            SetCyrillicFont(descTMP);

            // Запасной элемент (скрыт)
            GameObject factGO = new GameObject("ClinicalFact", typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
            factGO.transform.SetParent(infoCardGO.transform, false);
            LayoutElement factLE = factGO.GetComponent<LayoutElement>();
            factLE.ignoreLayout = true;
            TextMeshProUGUI factTMP = factGO.GetComponent<TextMeshProUGUI>();
            factTMP.text = "";
            factTMP.fontSize = 14;
            factTMP.gameObject.SetActive(false);
            SetCyrillicFont(factTMP);

            SetPrivateField(uiMgr, "sheetTitleRuText", titleRu);
            SetPrivateField(uiMgr, "sheetTitleLatinText", titleLat);
            SetPrivateField(uiMgr, "sheetSystemBadgeText", sysBadge);
            SetPrivateField(uiMgr, "sheetDescriptionText", descTMP);
            SetPrivateField(uiMgr, "sheetClinicalFactText", factTMP);

            // ==========================================
            // 5. ПОДСКАЗКА ПО РАЗМЕЩЕНИЮ (Prompt)
            // ==========================================
            GameObject promptGO = CreateUIPanel(root, "PlaneDetection_Prompt", new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.34f), new Color(0.06f, 0.10f, 0.16f, 0.94f));
            promptGO.GetComponent<Image>().raycastTarget = false;

            TextMeshProUGUI promptTMP = CreateUIText(promptGO.transform, "PromptText",
                "Наведите камеру на стол или пол.\nПоявится сканирующий прицел:",
                new Vector2(0.04f, 0.52f), new Vector2(0.96f, 0.95f), 24, TextAlignmentOptions.Center, Color.white);
            promptTMP.raycastTarget = false;

            Button btnDirectPlace = CreateUIButton(promptGO.transform, "DirectPlaceButton", "УСТАНОВИТЬ МОДЕЛЬ СЮДА",
                new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.48f), buttonBaseColor, 26);
            btnDirectPlace.onClick.AddListener(() => placementMgr.PlaceModelDirectly());

            SetPrivateField(uiMgr, "directPlaceButton", btnDirectPlace);
            SetPrivateField(uiMgr, "planeDetectionPrompt", promptGO);
        }

        private static GameObject CreateUIPanel(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, Color bgColor)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            Image img = go.GetComponent<Image>();
            img.color = bgColor;
            return go;
        }

        private static Button CreateUIButton(Transform parent, string name, string label, Vector2 anchorMin, Vector2 anchorMax, Color btnColor, float fontSize = 24)
        {
            GameObject btnGO = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGO.transform.SetParent(parent, false);
            RectTransform rt = btnGO.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            btnGO.GetComponent<Image>().color = btnColor;

            CreateUIText(btnGO.transform, "Text", label, Vector2.zero, Vector2.one, fontSize, TextAlignmentOptions.Center, Color.white);

            return btnGO.GetComponent<Button>();
        }

        private static TextMeshProUGUI CreateUIText(Transform parent, string name, string text, Vector2 anchorMin, Vector2 anchorMax, float fontSize, TextAlignmentOptions alignment, Color color)
        {
            GameObject go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            TextMeshProUGUI tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = color;
            tmp.enableWordWrapping = true;
            SetCyrillicFont(tmp);
            return tmp;
        }

        private static Slider CreateUISlider(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            GameObject sliderGO = new GameObject(name, typeof(RectTransform), typeof(Slider));
            sliderGO.transform.SetParent(parent, false);
            RectTransform rt = sliderGO.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = rt.offsetMax = Vector2.zero;

            GameObject bgGO = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bgGO.transform.SetParent(sliderGO.transform, false);
            RectTransform bgRT = bgGO.GetComponent<RectTransform>();
            bgRT.anchorMin = new Vector2(0, 0.25f);
            bgRT.anchorMax = new Vector2(1, 0.75f);
            bgRT.offsetMin = bgRT.offsetMax = Vector2.zero;
            bgGO.GetComponent<Image>().color = new Color(0.18f, 0.24f, 0.35f, 0.85f);

            GameObject fillAreaGO = new GameObject("Fill Area", typeof(RectTransform));
            fillAreaGO.transform.SetParent(sliderGO.transform, false);
            RectTransform faRT = fillAreaGO.GetComponent<RectTransform>();
            faRT.anchorMin = new Vector2(0, 0.25f);
            faRT.anchorMax = new Vector2(1, 0.75f);
            faRT.offsetMin = faRT.offsetMax = Vector2.zero;

            GameObject fillGO = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fillGO.transform.SetParent(fillAreaGO.transform, false);
            RectTransform fillRT = fillGO.GetComponent<RectTransform>();
            fillRT.sizeDelta = Vector2.zero;
            fillGO.GetComponent<Image>().color = new Color(0f, 0.82f, 0.76f, 1f);

            Slider slider = sliderGO.GetComponent<Slider>();
            slider.fillRect = fillRT;
            slider.targetGraphic = bgGO.GetComponent<Image>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0f;

            return slider;
        }

        #endregion

        #region Шрифты

        private static void SetCyrillicFont(TextMeshProUGUI tmp)
        {
            if (tmp == null || s_CyrillicFont == null) return;
            try
            {
                tmp.font = s_CyrillicFont;
                if (s_CyrillicFont.material != null)
                {
                    tmp.fontSharedMaterial = s_CyrillicFont.material;
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[Anatomy AR] Skipping font set: " + ex.Message);
            }
        }

        private static TMP_FontAsset GetOrCreateArialFontAsset()
        {
            string[] possiblePaths = new string[] {
                FontsFolder + "/Arial SDF.asset",
                FontsFolder + "/Arial_SDF.asset"
            };

            foreach (var path in possiblePaths)
            {
                TMP_FontAsset existing = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(path);
                if (existing != null && existing.material != null && existing.atlasTextures != null && existing.atlasTextures.Length > 0 && existing.atlasTextures[0] != null)
                {
                    return existing;
                }
            }

            Font arialFont = AssetDatabase.LoadAssetAtPath<Font>(FontsFolder + "/Arial.ttf");
            if (arialFont != null)
            {
                try
                {
                    Selection.activeObject = arialFont;
                    Selection.objects = new Object[] { arialFont };
                    bool executed = EditorApplication.ExecuteMenuItem("Assets/Create/TextMeshPro/Font Asset");
                    AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

                    string generatedPath = FontsFolder + "/Arial SDF.asset";
                    TMP_FontAsset created = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>(generatedPath);
                    if (created != null && created.atlasTextures != null && created.atlasTextures.Length > 0 && created.atlasTextures[0] != null)
                    {
                        return created;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[Anatomy AR] Font creation fallback: " + ex.Message);
                }
            }

            TMP_FontAsset defaultFont = Resources.Load<TMP_FontAsset>("Fonts & Materials/LiberationSans SDF");
            if (defaultFont == null) defaultFont = TMP_Settings.defaultFontAsset;
            return defaultFont;
        }

        #endregion

        #region Безопасные PBR-материалы

        private static Shader GetSafeLitShader()
        {
            Shader s = Shader.Find("Standard");
            if (s == null) s = Shader.Find("Mobile/Diffuse");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Lit");
            if (s == null) s = Shader.Find("Diffuse");
            return s;
        }

        private static Shader GetSafeTransparentShader()
        {
            Shader s = Shader.Find("Unlit/Transparent");
            if (s == null) s = Shader.Find("Mobile/Particles/Alpha Blended");
            if (s == null) s = Shader.Find("Universal Render Pipeline/Unlit");
            return s;
        }

        private static Material CreateTexturedPbrMaterial(string name, string textureFileName, Color tintColor)
        {
            string path = $"{MaterialsFolder}/{name}.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader litShader = GetSafeLitShader();
            if (mat == null)
            {
                mat = new Material(litShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = litShader;
            }

            mat.color = tintColor;
            mat.SetColor("_Color", tintColor);
            mat.SetColor("_BaseColor", tintColor);
            mat.SetFloat("_Glossiness", 0.40f);
            mat.SetFloat("_Smoothness", 0.40f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);

            if (!string.IsNullOrEmpty(textureFileName))
            {
                Texture2D tex = AssetDatabase.LoadAssetAtPath<Texture2D>($"{ModelsAssetFolder}/{textureFileName}");
                if (tex != null)
                {
                    mat.mainTexture = tex;
                    mat.SetTexture("_MainTex", tex);
                    mat.SetTexture("_BaseMap", tex);
                }
            }
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrGetXRayMaterial()
        {
            string path = MaterialsFolder + "/Mat_XRay_Fresnel.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader xrayShader = GetSafeTransparentShader();
            if (mat == null)
            {
                mat = new Material(xrayShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = xrayShader;
            }

            Color xrayColor = new Color(0.15f, 0.75f, 1f, 0.35f);
            mat.color = xrayColor;
            mat.SetColor("_Color", xrayColor);
            mat.SetColor("_BaseColor", xrayColor);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreatePlaneMaterial()
        {
            string path = MaterialsFolder + "/Mat_AR_Plane.mat";
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader transparentShader = GetSafeTransparentShader();
            if (mat == null)
            {
                mat = new Material(transparentShader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = transparentShader;
            }

            Color planeColor = new Color(0f, 0.82f, 0.76f, 0.32f);
            mat.color = planeColor;
            mat.SetColor("_Color", planeColor);
            mat.SetColor("_BaseColor", planeColor);
            mat.renderQueue = 3000;
            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static GameObject CreateOrGetPlanePrefab(Material planeMat)
        {
            string path = $"{PrefabsFolder}/AR_Plane_Visualizer.prefab";
            GameObject saved = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (saved != null) return saved;

            GameObject planeGO = new GameObject("AR_Plane_Visualizer");
            planeGO.AddComponent<ARPlane>();
            planeGO.AddComponent<ARPlaneMeshVisualizer>();
            planeGO.AddComponent<MeshFilter>();
            MeshRenderer mr = planeGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = planeMat;
            planeGO.AddComponent<MeshCollider>();

            saved = PrefabUtility.SaveAsPrefabAsset(planeGO, path);
            GameObject.DestroyImmediate(planeGO);
            return saved;
        }

        #endregion

        #region Генерация 13 Органов Человека

        private static Mesh LoadMeshFromObj(string fileName)
        {
            string path = $"{ModelsAssetFolder}/{fileName}";

            Mesh directMesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (directMesh != null) return directMesh;

            var subAssets = AssetDatabase.LoadAllAssetsAtPath(path);
            if (subAssets != null)
            {
                for (int i = 0; i < subAssets.Length; i++)
                {
                    if (subAssets[i] is Mesh m) return m;
                }
            }

            GameObject objPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (objPrefab != null)
            {
                MeshFilter mf = objPrefab.GetComponentInChildren<MeshFilter>(true);
                if (mf != null && mf.sharedMesh != null)
                {
                    return mf.sharedMesh;
                }
            }
            return null;
        }

        private static AnatomyDataSO CreateAll13OrganPrefabs(
            Material heartMat, Material frontalMat, Material parietalMat, Material occipitalMat,
            Material temporalMat, Material cerebellumMat, Material stemMat,
            Material lungTracheaMat, Material lungLobesMat,
            Material liverMat, Material liverGbMat, Material liverVesselMat,
            Material kidneyCortexMat, Material kidneyPyramidsMat, Material kidneyPelvisMat, Material kidneyVesselsMat,
            Material stomachMat, Material smallIntestineMat, Material largeIntestineMat, Material uterusMat,
            Material pancreasMat, Material spleenMat, Material bladderMat, Material gallbladderMat,
            Material thyroidMat, Material parathyroidMat, Material xrayMat)
        {
            List<AnatomyModelSO> modelsList = new List<AnatomyModelSO>();

            // 1. СЕРДЦЕ ЧЕЛОВЕКА (7 сегментов)
            modelsList.Add(CreateHeartOrgan(heartMat, xrayMat));

            // 2. ГОЛОВНОЙ МОЗГ (6 анатомических долей)
            modelsList.Add(CreateBrainOrgan(frontalMat, parietalMat, occipitalMat, temporalMat, cerebellumMat, stemMat, xrayMat));

            // 3. ЛЕГКИЕ И ТРАХЕЯ (3 анатомических структуры: трахея с бронхами, правое и левое легкое)
            modelsList.Add(CreateLungsOrgan(lungTracheaMat, lungLobesMat, xrayMat));

            // 4. ПЕЧЕНЬ (Развернута на 90 градусов по часовой стрелке)
            modelsList.Add(CreateLiverOrgan(liverMat, liverGbMat, liverVesselMat, xrayMat));

            // 5. ПОЧКИ (Две соединенные почки с центральной аортой и полой веной)
            modelsList.Add(CreateKidneyOrgan(kidneyCortexMat, kidneyPyramidsMat, kidneyPelvisMat, kidneyVesselsMat, xrayMat));

            // 6. ЖКТ (Желудочно-кишечный тракт)
            modelsList.Add(CreateGastrointestinalOrgan(stomachMat, xrayMat));

            // 7. ПОДЖЕЛУДОЧНАЯ ЖЕЛЕЗА (3 отдела: головка, тело, хвост)
            modelsList.Add(CreatePancreasOrgan(pancreasMat, xrayMat));

            // 8. СЕЛЕЗЕНКА (3 поверхности: диафрагмальная, висцеральная, ворота)
            modelsList.Add(CreateSpleenOrgan(spleenMat, liverVesselMat, xrayMat));

            // 9. МОЧЕВОЙ ПУЗЫРЬ (3 отдела: верхушка, тело, дно и шейка)
            modelsList.Add(CreateBladderOrgan(bladderMat, xrayMat));

            // 10. ЖЕЛЧНЫЙ ПУЗЫРЬ (Болотного цвета с разветвленными протоками)
            modelsList.Add(CreateGallbladderOrgan(gallbladderMat, liverVesselMat, xrayMat));

            // 11. ЩИТОВИДНАЯ ЖЕЛЕЗА (4 структуры: правая доля, левая доля, перешеек, паращитовидные железы)
            modelsList.Add(CreateThyroidOrgan(thyroidMat, parathyroidMat, xrayMat));

            // 12. МАТКА (Развернута так, чтобы цервикальный канал смотрел строго вниз)
            modelsList.Add(CreateUterusOrgan(uterusMat, xrayMat));

            // Сохранение единой базы данных органов
            AnatomyDataSO db = ScriptableObject.CreateInstance<AnatomyDataSO>();
            var dbModels = (List<AnatomyModelSO>)GetPrivateField(db, "availableModels");
            dbModels.AddRange(modelsList);
            AssetDatabase.CreateAsset(db, SoFolder + "/AnatomyDatabase.asset");

            return db;
        }

        private static void CreateMultiPartChild(Transform parent, string objFileName, OrganPartDataSO partData, Material mat, float scale = 1f, Vector3 localOffset = default, Vector3 localRotEuler = default)
        {
            GameObject partGO = new GameObject(partData.PartId);
            partGO.transform.SetParent(parent, false);
            partGO.transform.localPosition = localOffset;
            partGO.transform.localEulerAngles = localRotEuler;
            partGO.transform.localScale = Vector3.one * scale;

            Mesh mesh = LoadMeshFromObj(objFileName);
            MeshFilter mf = partGO.AddComponent<MeshFilter>();
            if (mesh != null) mf.sharedMesh = mesh;
            MeshRenderer mr = partGO.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;

            MeshCollider mc = partGO.AddComponent<MeshCollider>();
            if (mesh != null)
            {
                mc.sharedMesh = mesh;
                mc.convex = false;
            }

            OrganPartInteractable interactable = partGO.AddComponent<OrganPartInteractable>();
            interactable.InitializeData(partData);
        }

        // 1. СЕРДЦЕ ЧЕЛОВЕКА
        private static AnatomyModelSO CreateHeartOrgan(Material heartMat, Material xrayMat)
        {
            var lv = CreatePartSO("heart_lv", "Левый желудочек", "Ventriculus sinister", OrganSystemType.Cardiovascular,
                "Главная насосная камера большого круга кровообращения с толщиной миокарда 12-15 мм. Нагнетает кровь в аорту под давлением 120 мм рт. ст.",
                "При длительной артериальной гипертонии левый желудочек компенсаторно гипертрофируется.");

            var rv = CreatePartSO("heart_rv", "Правый желудочек", "Ventriculus dexter", OrganSystemType.Cardiovascular,
                "Камера малого круга кровообращения. Выталкивает венозную кровь в легочный ствол для насыщения кислородом.",
                "Массивная тромбоэмболия легочной артерии вызывает острую перегрузку правого желудочка (легочное сердце).");

            var aorta = CreatePartSO("heart_aorta", "Дуга аорты", "Arcus aortae", OrganSystemType.Cardiovascular,
                "Крупнейший артериальный коллектор организма, дающий начало брахиоцефальному стволу, сонной и подключичной артериям.",
                "Эластичность дуги аорты (эффект Виндкесселя) сглаживает скачки давления между систолой и диастолой.");

            var la = CreatePartSO("heart_la", "Левое предсердие", "Atrium sinistrum", OrganSystemType.Cardiovascular,
                "Принимает артериальную кровь из четырех легочных вен и перекачивает ее через митральный клапан в левый желудочек.",
                "Мерцательная аритмия чаще всего инициируется патологическими импульсами из мышечных муфт легочных вен.");

            var ra = CreatePartSO("heart_ra", "Правое предсердие", "Atrium dextrum", OrganSystemType.Cardiovascular,
                "Принимает всю венозную кровь из верхней и нижней полых вен и коронарного синуса сердца.",
                "В стенке правого предсердия залегает синусно-предсердный узел — главный водитель ритма сердца.");

            var pulmonary = CreatePartSO("heart_pulmonary", "Легочный ствол", "Truncus pulmonalis", OrganSystemType.Cardiovascular,
                "Широкий артериальный ствол малого круга, начинающийся от правого желудочка и делящийся на легочные артерии.",
                "В легочном стволе течет венозная кровь, несмотря на артериальный тип строения сосудистой стенки.");

            var svc = CreatePartSO("heart_svc", "Верхняя полая вена", "Vena cava superior", OrganSystemType.Cardiovascular,
                "Коллектор венозной крови от головы, шеи, верхних конечностей и органов средостения.",
                "Сдавление верхней полой вены опухолями средостения приводит к отеку шеи и лица (синдром верхней полой вены).");

            GameObject root = new GameObject("Prefab_Human_Heart");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", true);
            SetPrivateField(ctrl, "heartRateBPM", 75f);
            SetPrivateField(ctrl, "pulsationAmplitude", 0.05f);

            float scale = 0.062f;
            CreateMultiPartChild(root.transform, "heart_lv.obj", lv, heartMat, scale);
            CreateMultiPartChild(root.transform, "heart_rv.obj", rv, heartMat, scale);
            CreateMultiPartChild(root.transform, "heart_la.obj", la, heartMat, scale);
            CreateMultiPartChild(root.transform, "heart_ra.obj", ra, heartMat, scale);
            CreateMultiPartChild(root.transform, "heart_aorta.obj", aorta, heartMat, scale);
            CreateMultiPartChild(root.transform, "heart_pulmonary.obj", pulmonary, heartMat, scale);
            CreateMultiPartChild(root.transform, "heart_svc.obj", svc, heartMat, scale);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_Heart.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "heart_full");
            SetPrivateField(so, "titleRu", "Сердце человека");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", true);
            SetPrivateField(so, "defaultBPM", 75f);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { lv, rv, aorta, la, ra, pulmonary, svc });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_Heart.asset");
            return so;
        }

        // 2. ГОЛОВНОЙ МОЗГ
        private static AnatomyModelSO CreateBrainOrgan(
            Material frontalMat, Material parietalMat, Material occipitalMat, Material temporalMat,
            Material cerebellumMat, Material stemMat, Material xrayMat)
        {
            var frontal = CreatePartSO("brain_frontal", "Лобная доля", "Lobus frontalis", OrganSystemType.Nervous,
                "Отвечает за произвольные движения, моторную речь (зона Брока), осознанное планирование и принятие решений.",
                "Поражение лобной коры ведет к апатии, растормаживанию влечений и утрате критики.");

            var parietal = CreatePartSO("brain_parietal", "Теменная доля", "Lobus parietalis", OrganSystemType.Nervous,
                "Центр обработки соматосенсорной информации: тактильной, болевой и температурной чувствительности.",
                "Поражение теменной доли нарушает пространственное восприятие и узнавание предметов на ощупь.");

            var occipital = CreatePartSO("brain_occipital", "Затылочная доля", "Lobus occipitalis", OrganSystemType.Nervous,
                "Первичная зрительная кора. Обрабатывает зрительные сигналы сетчатки, цветовое зрение и формы объектов.",
                "Повреждение зрительной коры вызывает гемианопсию — слепоту в половине полей зрения обоих глаз.");

            var temporal = CreatePartSO("brain_temporal", "Височная доля", "Lobus temporalis", OrganSystemType.Nervous,
                "Слуховой центр, зона понимания речи (Вернике) и гиппокамп, отвечающий за долговременную память.",
                "Височная эпилепсия часто протекает с яркими обонятельными галлюцинациями и переживанием дежавю.");

            var cerebellum = CreatePartSO("brain_cerebellum", "Мозжечок", "Cerebellum", OrganSystemType.Nervous,
                "Координирует мышечный тонус, равновесие, плавность движений и моторное обучение.",
                "При мозжечковой атаксии нарушается походка и появляется интенционный тремор.");

            var stem = CreatePartSO("brain_stem", "Ствол мозга", "Truncus encephali", OrganSystemType.Nervous,
                "Содержит жизненно важные центры дыхания, сосудистого тонуса, глотания и ядра черепных нервов.",
                "Сдавление ствола мозга при дислокации смертельно опасно из-за мгновенной остановки дыхания.");

            GameObject root = new GameObject("Prefab_Human_Brain");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);
            SetPrivateField(ctrl, "maxExplodeDistance", 0.12f);

            float scale = 0.018f;
            CreateMultiPartChild(root.transform, "brain_Frontal_0.obj", frontal, frontalMat, scale);
            CreateMultiPartChild(root.transform, "brain_Parietal_0.obj", parietal, parietalMat, scale);
            CreateMultiPartChild(root.transform, "brain_Occipital_0.obj", occipital, occipitalMat, scale);
            CreateMultiPartChild(root.transform, "brain_Temporal_0.obj", temporal, temporalMat, scale);
            CreateMultiPartChild(root.transform, "brain_cerebellum_0.obj", cerebellum, cerebellumMat, scale);
            CreateMultiPartChild(root.transform, "brain_lambert1_0.obj", stem, stemMat, scale);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_Brain.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "brain_full");
            SetPrivateField(so, "titleRu", "Головной мозг");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { frontal, parietal, occipital, temporal, cerebellum, stem });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_Brain.asset");
            return so;
        }

        // 3. ЛЕГКИЕ И ТРАХЕЯ (НОВЫЙ ОРГАН)
        private static AnatomyModelSO CreateLungsOrgan(Material tracheaMat, Material lungMat, Material xrayMat)
        {
            var trachea = CreatePartSO("lung_trachea", "Трахея и главные бронхи", "Trachea et bronchi", OrganSystemType.Respiratory,
                "Дыхательная трубка длиной 10-12 см из 16-20 гиалиновых хрящевых полуколец, делящаяся на правый и левый главные бронхи.",
                "Слизистая оболочка покрыта мерцательным эпителием, реснички которого непрерывно эвакуируют пылевые частицы вверх.");

            var rightLung = CreatePartSO("lung_right", "Правое легкое (3 доли)", "Pulmo dexter", OrganSystemType.Respiratory,
                "Разделено косой и горизонтальной щелями на 3 анатомические доли: верхнюю, среднюю и нижнюю.",
                "Правое легкое примерно на 10% объемнее левого, так как левая половина груди частично занята сердцем.");

            var leftLung = CreatePartSO("lung_left", "Левое легкое (2 доли)", "Pulmo sinister", OrganSystemType.Respiratory,
                "Разделено косой щелью на 2 доли (верхнюю и нижнюю) и имеет глубокую сердечную вырезку (incisura cardiaca).",
                "Общая площадь альвеолярной дыхательной поверхности обоих легких на вдохе превышает 100-120 квадратных метров.");

            GameObject root = new GameObject("Prefab_Human_lungs");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 0.008f;
            CreateMultiPartChild(root.transform, "lung_trachea.obj", trachea, tracheaMat, scale);
            CreateMultiPartChild(root.transform, "lung_right.obj", rightLung, lungMat, scale);
            CreateMultiPartChild(root.transform, "lung_left.obj", leftLung, lungMat, scale);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_lungs.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "lungs");
            SetPrivateField(so, "titleRu", "Легкие и трахея");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { trachea, rightLung, leftLung });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_lungs.asset");
            return so;
        }

        // 4. ПЕЧЕНЬ
        private static AnatomyModelSO CreateLiverOrgan(Material liverMat, Material gbMat, Material vesselMat, Material xrayMat)
        {
            var leftLobe = CreatePartSO("liver_left_lobe", "Левая доля печени", "Lobus sinister hepatis", OrganSystemType.Digestive,
                "Меньшая анатомическая доля печени, разделенная серповидной связкой. Включает II и III сегменты по Куино.",
                "Клетки левой доли обладают исключительной способностью к стремительной регенерации и гипертрофии.");

            var rightLobe = CreatePartSO("liver_right_lobe", "Правая доля печени", "Lobus dexter hepatis", OrganSystemType.Digestive,
                "Массивная доля (около 60% объема печени), включающая V, VI, VII и VIII функциональные сегменты.",
                "В правой доле депонируется до 15% гликогена всего организма и синтезируются ключевые белки свертывания.");

            var gallbladder = CreatePartSO("liver_gallbladder", "Желчный пузырь", "Vesica biliaris", OrganSystemType.Digestive,
                "Резервуар на висцеральной поверхности печени, аккумулирующий и концентрирующий вырабатываемую желчь.",
                "За сутки гепатоциты печени секретируют от 800 до 1200 мл желчи, необходимой для эмульгирования жиров.");

            var vessels = CreatePartSO("liver_vessels", "Сосуды печени", "Vena portae et vena cava", OrganSystemType.Digestive,
                "Воротная вена, печеночная артерия и нижняя полая вена, обеспечивающие двойное кровоснабжение органа.",
                "Около 75% притока крови к печени поступает через воротную вену от органов ЖКТ для фильтрации токсинов.");

            GameObject root = new GameObject("Prefab_Human_liver");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 0.085f;
            Vector3 liverRot = new Vector3(-90f, 90f, 90f); // Печень вертикально стоит, сосуды направлены строго вниз
            CreateMultiPartChild(root.transform, "liver_left_lobe.obj", leftLobe, liverMat, scale, Vector3.zero, liverRot);
            CreateMultiPartChild(root.transform, "liver_right_lobe.obj", rightLobe, liverMat, scale, Vector3.zero, liverRot);
            CreateMultiPartChild(root.transform, "liver_gallbladder.obj", gallbladder, gbMat, scale, Vector3.zero, liverRot);
            CreateMultiPartChild(root.transform, "liver_vessels.obj", vessels, vesselMat, scale, Vector3.zero, liverRot);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_liver.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "liver");
            SetPrivateField(so, "titleRu", "Печень");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { leftLobe, rightLobe, gallbladder, vessels });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_liver.asset");
            return so;
        }

        // 5. ПОЧКИ (Две соединенные почки с центральной аортой и полой веной)
        private static AnatomyModelSO CreateKidneyOrgan(Material cortexMat, Material pyramidsMat, Material pelvisMat, Material vesselMat, Material xrayMat)
        {
            var rCortex = CreatePartSO("kidney_r_cortex", "Кора правой почки", "Cortex renalis dexter", OrganSystemType.Digestive,
                "Наружный слой правой почки толщиной до 1 см, содержащий более 1 миллиона почечных клубочков (нефронов).",
                "Здесь ежесуточно фильтруется около 150-180 литров первичной мочи из плазмы крови.");

            var rPyramids = CreatePartSO("kidney_r_pyramids", "Пирамиды правой почки", "Pyramides renales dexter", OrganSystemType.Digestive,
                "Мозговое вещество в форме конусообразных пирамид с петлями Генле и собирательными трубками.",
                "Осуществляет обратное всасывание воды и электролитов, концентрируя вторичную мочу.");

            var rPelvis = CreatePartSO("kidney_r_pelvis", "Лоханка правой почки", "Pelvis renalis dexter", OrganSystemType.Digestive,
                "Воронкообразный резервуар, собирающий мочу из больших почечных чашечек перед оттоком в мочеточник.",
                "Перистальтические волны стенок лоханки проталкивают порции мочи в мочеточник каждые 10-30 секунд.");

            var lCortex = CreatePartSO("kidney_l_cortex", "Кора левой почки", "Cortex renalis sinister", OrganSystemType.Digestive,
                "Корковое вещество левой почки, расположенной чуть выше правой из-за давления печени справа.",
                "Выполняет фильтрацию метаболических шлаков (мочевины, креатинина) и регулирует водно-солевой гомеостаз.");

            var lPyramids = CreatePartSO("kidney_l_pyramids", "Пирамиды левой почки", "Pyramides renales sinister", OrganSystemType.Digestive,
                "Мозговые пирамиды левой почки, обеспечивающие концентрирование урины в противоточной множительной системе.",
                "Кровоток в почечных капиллярах поддерживается механизмом ауторегуляции даже при колебаниях артериального давления.");

            var lPelvis = CreatePartSO("kidney_l_pelvis", "Лоханка левой почки", "Pelvis renalis sinister", OrganSystemType.Digestive,
                "Лоханочный резервуар левой почки, направляющий вторичную мочу в левый мочеточник.",
                "Воспаление почечной лоханки (пиелонефрит) является одним из наиболее частых нефрологических заболеваний.");

            var vessels = CreatePartSO("kidney_vessels", "Аорта, полая вена и сосуды", "Aorta abdominalis et v. cava cum vasis renalibus", OrganSystemType.Cardiovascular,
                "Брюшная аорта и нижняя полая вена с отходящими парными почечными артериями и венами, соединяющими обе почки.",
                "Почки получают более 20-25% от всего сердечного выброса крови для непрерывного очищения организма.");

            GameObject root = new GameObject("Prefab_Human_kidney");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 0.038f;
            // Правая почка (+X)
            Vector3 rOffset = new Vector3(0.048f, 0, 0);
            CreateMultiPartChild(root.transform, "kidney_cortex.obj", rCortex, cortexMat, scale, rOffset);
            CreateMultiPartChild(root.transform, "kidney_pyramids.obj", rPyramids, pyramidsMat, scale, rOffset);
            CreateMultiPartChild(root.transform, "kidney_pelvis.obj", rPelvis, pelvisMat, scale, rOffset);

            // Левая почка (-X, зеркально развернута к центру)
            Vector3 lOffset = new Vector3(-0.048f, 0, 0);
            Vector3 lRot = new Vector3(0, 180f, 0);
            CreateMultiPartChild(root.transform, "kidney_cortex.obj", lCortex, cortexMat, scale, lOffset, lRot);
            CreateMultiPartChild(root.transform, "kidney_pyramids.obj", lPyramids, pyramidsMat, scale, lOffset, lRot);
            CreateMultiPartChild(root.transform, "kidney_pelvis.obj", lPelvis, pelvisMat, scale, lOffset, lRot);

            // Соединяющие магистральные сосуды (Аорта + Нижняя полая вена)
            CreateMultiPartChild(root.transform, "kidney_vessels_connected.obj", vessels, vesselMat, scale, Vector3.zero);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_kidney.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "kidney");
            SetPrivateField(so, "titleRu", "Почки");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { rCortex, rPyramids, rPelvis, lCortex, lPyramids, lPelvis, vessels });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_kidney.asset");
            return so;
        }

        // 6. ЖКТ (Желудочно-кишечный тракт на базе модели желудка)
        private static AnatomyModelSO CreateGastrointestinalOrgan(Material stomachMat, Material xrayMat)
        {
            var fundus = CreatePartSO("gastro_fundus", "Дно и кардия желудка", "Fundus et pars cardiaca", OrganSystemType.Digestive,
                "Куполообразный свод желудка, расположенный под диафрагмой. Служит ловушкой для газового пузыря.",
                "Стенка дна обладает свойством релаксационной аккомодации, вмещая пищу без скачков давления.");

            var body = CreatePartSO("gastro_body", "Тело желудка", "Corpus gastris", OrganSystemType.Digestive,
                "Основная часть ЖКТ, содержащая железы с париетальными клетками, секретирующими HCl и фактор Касла.",
                "Фактор Касла связывает витамин B12, предотвращая развитие тяжелой пернициозной анемии.");

            var pylorus = CreatePartSO("gastro_pylorus", "Пилорический отдел и сфинктер", "Pars pylorica et pylorus", OrganSystemType.Digestive,
                "Привратниковая пещера и мощный мышечный сфинктер, дозирующий эвакуацию химуса в тонкую кишку.",
                "Сфинктер рефлекторно открывается лишь тогда, когда кислотность в луковице двенадцатиперстной кишки нейтрализуется.");

            GameObject root = new GameObject("Prefab_Human_gastrointestinal");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 0.0042f;
            CreateMultiPartChild(root.transform, "stomach_fundus.obj", fundus, stomachMat, scale);
            CreateMultiPartChild(root.transform, "stomach_body.obj", body, stomachMat, scale);
            CreateMultiPartChild(root.transform, "stomach_pylorus.obj", pylorus, stomachMat, scale);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_gastrointestinal.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "gastrointestinal");
            SetPrivateField(so, "titleRu", "ЖКТ");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.35f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { fundus, body, pylorus });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_gastrointestinal.asset");
            return so;
        }

        // 8. ПОДЖЕЛУДОЧНАЯ ЖЕЛЕЗА
        private static AnatomyModelSO CreatePancreasOrgan(Material pancreasMat, Material xrayMat)
        {
            var head = CreatePartSO("pancreas_head", "Головка поджелудочной железы", "Caput pancreatis", OrganSystemType.Digestive,
                "Утолщенный правый отдел железы, охваченный петлей двенадцатиперстной кишки, с крючковидным отростком.",
                "Опухоли головки железы могут сдавливать общий желчный проток, вызывая механическую желтуху.");

            var body = CreatePartSO("pancreas_body", "Тело поджелудочной железы", "Corpus pancreatis", OrganSystemType.Digestive,
                "Трехгранная средняя часть органа, пересекающая аорту и нижнюю полую вену. Содержит островки Лангерганса.",
                "Бета-клетки островков Лангерганса непрерывно секретируют гормон инсулин в капиллярное русло.");

            var tail = CreatePartSO("pancreas_tail", "Хвост поджелудочной железы", "Cauda pancreatis", OrganSystemType.Digestive,
                "Суженный левый конец органа, направленный косо вверх к висцеральной поверхности ворот селезенки.",
                "Плотность эндокринных островков в хвосте железы значительно выше, чем в ее головке.");

            GameObject root = new GameObject("Prefab_Human_pancreas");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 1.69f;
            CreateMultiPartChild(root.transform, "pancreas_head.obj", head, pancreasMat, scale);
            CreateMultiPartChild(root.transform, "pancreas_body.obj", body, pancreasMat, scale);
            CreateMultiPartChild(root.transform, "pancreas_tail.obj", tail, pancreasMat, scale);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_pancreas.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "pancreas");
            SetPrivateField(so, "titleRu", "Поджелудочная железа");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { head, body, tail });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_pancreas.asset");
            return so;
        }

        // 9. СЕЛЕЗЕНКА
        private static AnatomyModelSO CreateSpleenOrgan(Material spleenMat, Material vesselMat, Material xrayMat)
        {
            var diaph = CreatePartSO("spleen_diaph", "Диафрагмальная поверхность", "Facies diaphragmatica", OrganSystemType.Cardiovascular,
                "Гладкая выпуклая латеральная поверхность, прилежащая к куполу диафрагмы на уровне IX-XI ребер слева.",
                "Фиброзно-мышечная капсула селезенки позволяет депонировать до 300-400 мл резервной крови.");

            var visc = CreatePartSO("spleen_visceral", "Висцеральная поверхность", "Facies visceralis", OrganSystemType.Cardiovascular,
                "Медиальная поверхность с анатомическими вдавлениями от желудка, левой почки и ободочной кишки.",
                "Белая пульпа висцеральной зоны состоит из лимфоидных фолликулов, синтезирующих иммуноглобулины.");

            var hilum = CreatePartSO("spleen_hilum", "Ворота селезенки и сосуды", "Hilum splenicum", OrganSystemType.Cardiovascular,
                "Продольная щель, через которую проходят извитая селезеночная артерия, вена и нервные стволы.",
                "Селезенка является главным фильтром крови, утилизирующим сотни миллиардов состарившихся эритроцитов.");

            GameObject root = new GameObject("Prefab_Human_spleen");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 2.10f;
            CreateMultiPartChild(root.transform, "spleen_diaphragmatic.obj", diaph, spleenMat, scale);
            CreateMultiPartChild(root.transform, "spleen_visceral.obj", visc, spleenMat, scale);
            CreateMultiPartChild(root.transform, "spleen_hilum.obj", hilum, vesselMat, scale);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_spleen.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "spleen");
            SetPrivateField(so, "titleRu", "Селезенка");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { diaph, visc, hilum });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_spleen.asset");
            return so;
        }

        // 10. МОЧЕВОЙ ПУЗЫРЬ
        private static AnatomyModelSO CreateBladderOrgan(Material bladderMat, Material xrayMat)
        {
            var apex = CreatePartSO("bladder_apex", "Верхушка мочевого пузыря", "Apex vesicae", OrganSystemType.Digestive,
                "Заостренный передне-верхний отдел, соединенный срединной пупочной связкой (урахусом) с пупком.",
                "Урахус — эмбриональный ход, связывавший мочевой пузырь плода с сосудами пуповины.");

            var body = CreatePartSO("bladder_body", "Тело мочевого пузыря", "Corpus vesicae", OrganSystemType.Digestive,
                "Средняя расширяемая часть органа, стенка которой образована трехслойной гладкой мышцей-детрузором.",
                "Детрузор способен растягиваться в несколько раз при наполнении до 500-700 мл без резкого роста давления.");

            var fundus = CreatePartSO("bladder_fundus", "Дно и шейка мочевого пузыря", "Fundus et cervix vesicae", OrganSystemType.Digestive,
                "Нижний отдел с мочепузырным треугольником Льето, переходящий в шейку с внутренним сфинктером уретры.",
                "Слизистая оболочка треугольника Льето не имеет подслизистого слоя и складок, обеспечивая беспрепятственный отток мочи.");

            GameObject root = new GameObject("Prefab_Human_bladder");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 2.40f;
            CreateMultiPartChild(root.transform, "bladder_apex.obj", apex, bladderMat, scale);
            CreateMultiPartChild(root.transform, "bladder_body.obj", body, bladderMat, scale);
            CreateMultiPartChild(root.transform, "bladder_fundus.obj", fundus, bladderMat, scale);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_bladder.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "bladder");
            SetPrivateField(so, "titleRu", "Мочевой пузырь");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { apex, body, fundus });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_bladder.asset");
            return so;
        }

        // 11. ЖЕЛЧНЫЙ ПУЗЫРЬ
        private static AnatomyModelSO CreateGallbladderOrgan(Material gbMat, Material vesselMat, Material xrayMat)
        {
            var fundus = CreatePartSO("gb_fundus", "Дно желчного пузыря", "Fundus vesicae biliaris", OrganSystemType.Digestive,
                "Широкий передний закругленный конец, слегка выступающий из-под нижнего края печени.",
                "Болезненность при нажатии на область дна на вдохе (симптом Мерфи) — патогномоничный признак холецистита.");

            var body = CreatePartSO("gb_body", "Тело желчного пузыря", "Corpus vesicae biliaris", OrganSystemType.Digestive,
                "Цилиндрический резервуар в ямке висцеральной поверхности печени, концентрирующий печеночную желчь.",
                "Слизистая пузыря реабсорбирует до 90% воды, делая пузырную желчь в 10 раз более концентрированной.");

            var neck = CreatePartSO("gb_neck", "Шейка желчного пузыря", "Collum vesicae biliaris", OrganSystemType.Digestive,
                "Суженный задний отдел, переходящий в пузырный проток со спиральной складкой Гейстера.",
                "Спиральная складка Гейстера действует как заслонка, поддерживая тонус и ток желчи при скачках давления.");

            var ducts = CreatePartSO("gb_ducts", "Желчные протоки и холедох", "Ductus biliares et ductus choledochus", OrganSystemType.Digestive,
                "Разветвленная система протоков: пузырный проток сливается с общим печеночным, формируя холедох (общий желчный проток), впадающий в двенадцатиперстную кишку.",
                "Обтурация холедоха желчным камнем приводит к острой билиарной колике и механической желтухе.");

            GameObject root = new GameObject("Prefab_Human_gallbladder");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 0.28f;
            CreateMultiPartChild(root.transform, "gallbladder_fundus.obj", fundus, gbMat, scale);
            CreateMultiPartChild(root.transform, "gallbladder_body.obj", body, gbMat, scale);
            CreateMultiPartChild(root.transform, "gallbladder_neck.obj", neck, gbMat, scale);
            CreateMultiPartChild(root.transform, "gallbladder_ducts.obj", ducts, vesselMat, scale);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_gallbladder.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "gallbladder");
            SetPrivateField(so, "titleRu", "Желчный пузырь");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { fundus, body, neck, ducts });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_gallbladder.asset");
            return so;
        }

        // 12. ЩИТОВИДНАЯ ЖЕЛЕЗА
        private static AnatomyModelSO CreateThyroidOrgan(Material thyroidMat, Material paraMat, Material xrayMat)
        {
            var rightLobe = CreatePartSO("thyroid_right_lobe", "Правая доля щитовидной железы", "Lobus dexter glandulae thyroideae", OrganSystemType.Digestive,
                "Правая часть железы, прилежащая к гортани и трахее, секретирующая гормоны тироксин (Т4) и трийодтиронин (Т3).",
                "У большинства людей правая доля анатомически крупнее левой и интенсивнее кровоснабжается.");

            var leftLobe = CreatePartSO("thyroid_left_lobe", "Левая доля щитовидной железы", "Lobus sinister glandulae thyroideae", OrganSystemType.Digestive,
                "Левая часть железы, регулирующая основной энергетический обмен, потребление кислорода и температуру тела.",
                "Иод — ключевой микроэлемент: без него синтез молекул Т3 и Т4 полностью прекращается.");

            var isthmus = CreatePartSO("thyroid_isthmus", "Перешеек щитовидной железы", "Isthmus glandulae thyroideae", OrganSystemType.Digestive,
                "Узкая перемычка ткани, соединяющая правую и левую доли на уровне 2-3 хрящевых колец трахеи.",
                "У 10-15% людей перешеек может врожденно отсутствовать без каких-либо нарушений функции.");

            var parathyroid = CreatePartSO("thyroid_parathyroid", "Околощитовидные железы", "Glandulae parathyroideae", OrganSystemType.Digestive,
                "Четыре крошечные железы на задней поверхности долей, продуцирующие паратгормон.",
                "Паратгормон поддерживает уровень кальция в крови; его дефицит вызывает судорожный синдром (тетанию).");

            GameObject root = new GameObject("Prefab_Human_thyroid");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 3.30f;
            CreateMultiPartChild(root.transform, "thyroid_right_lobe.obj", rightLobe, thyroidMat, scale);
            CreateMultiPartChild(root.transform, "thyroid_left_lobe.obj", leftLobe, thyroidMat, scale);
            CreateMultiPartChild(root.transform, "thyroid_isthmus.obj", isthmus, thyroidMat, scale);
            CreateMultiPartChild(root.transform, "thyroid_parathyroid.obj", parathyroid, paraMat, scale);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_thyroid.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "thyroid");
            SetPrivateField(so, "titleRu", "Щитовидная железа");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { rightLobe, leftLobe, isthmus, parathyroid });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_thyroid.asset");
            return so;
        }

        // 13. МАТКА
        private static AnatomyModelSO CreateUterusOrgan(Material uterusMat, Material xrayMat)
        {
            var fundus = CreatePartSO("uterus_fundus", "Дно матки", "Fundus uteri", OrganSystemType.Digestive,
                "Верхняя куполообразная часть матки, возвышающаяся над плоскостью впадения маточных труб.",
                "Высота стояния дна матки над лонным сочленением является главным клиническим маркером гестационного возраста.");

            var body = CreatePartSO("uterus_body", "Тело матки и миометрий", "Corpus uteri", OrganSystemType.Digestive,
                "Основная мышечная часть с мощным трехслойным миометрием и функциональным циклическим эндометрием.",
                "Мышечные волокна миометрия в период беременности гипертрофируются более чем в 10 раз по объему.");

            var cervix = CreatePartSO("uterus_cervix", "Шейка матки", "Cervix uteri", OrganSystemType.Digestive,
                "Нижний суженный сегмент, выступающий в свод влагалища, с цервикальным каналом и защитной слизистой пробкой.",
                "Цервикальная слизь в овуляторный период становится прозрачной и тягучей, облегчая миграцию сперматозоидов.");

            var tubes = CreatePartSO("uterus_tubes", "Маточные трубы (Фаллопиевы)", "Tubae uterinae", OrganSystemType.Digestive,
                "Парные трубчатые органы длиной 10-12 см с ампулой и воронкой с бахромками, обращенными к яичникам.",
                "Оплодотворение яйцеклетки происходит именно в просвете ампулы маточной трубы.");

            GameObject root = new GameObject("Prefab_Human_uterus");
            OrganController ctrl = root.AddComponent<OrganController>();
            SetPrivateField(ctrl, "xrayMaterial", xrayMat);
            SetPrivateField(ctrl, "enablePulsation", false);

            float scale = 0.083f;
            Vector3 uterusRot = new Vector3(-90f, 0, 0); // Разворот на 90 градусов так, чтобы канал смотрел строго вниз
            CreateMultiPartChild(root.transform, "uterus_fundus.obj", fundus, uterusMat, scale, Vector3.zero, uterusRot);
            CreateMultiPartChild(root.transform, "uterus_body.obj", body, uterusMat, scale, Vector3.zero, uterusRot);
            CreateMultiPartChild(root.transform, "uterus_cervix.obj", cervix, uterusMat, scale, Vector3.zero, uterusRot);
            CreateMultiPartChild(root.transform, "uterus_tubes.obj", tubes, uterusMat, scale, Vector3.zero, uterusRot);

            string prefabPath = $"{PrefabsFolder}/Prefab_Human_uterus.prefab";
            GameObject savedPrefab = PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            GameObject.DestroyImmediate(root);

            AnatomyModelSO so = ScriptableObject.CreateInstance<AnatomyModelSO>();
            SetPrivateField(so, "modelId", "uterus");
            SetPrivateField(so, "titleRu", "Матка");
            SetPrivateField(so, "arPrefab", savedPrefab);
            SetPrivateField(so, "supportsPulsation", false);
            SetPrivateField(so, "defaultSpawnScale", 1.0f);
            var partsList = (List<OrganPartDataSO>)GetPrivateField(so, "partsList");
            partsList.AddRange(new[] { fundus, body, cervix, tubes });
            AssetDatabase.CreateAsset(so, ModelsFolder + "/Model_uterus.asset");
            return so;
        }

        #endregion

        private static OrganPartDataSO CreatePartSO(string id, string ru, string latin, OrganSystemType sys, string desc, string fact)
        {
            string path = $"{PartsFolder}/{id}.asset";
            OrganPartDataSO so = AssetDatabase.LoadAssetAtPath<OrganPartDataSO>(path);
            if (so == null)
            {
                so = ScriptableObject.CreateInstance<OrganPartDataSO>();
                SetPrivateField(so, "partId", id);
                SetPrivateField(so, "titleRu", ru);
                SetPrivateField(so, "titleLatin", latin);
                SetPrivateField(so, "systemType", sys);
                SetPrivateField(so, "description", desc);
                SetPrivateField(so, "clinicalFact", fact);
                AssetDatabase.CreateAsset(so, path);
            }
            return so;
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PartsFolder)) Directory.CreateDirectory(PartsFolder);
            if (!Directory.Exists(ModelsFolder)) Directory.CreateDirectory(ModelsFolder);
            if (!Directory.Exists(PrefabsFolder)) Directory.CreateDirectory(PrefabsFolder);
            if (!Directory.Exists(ScenesFolder)) Directory.CreateDirectory(ScenesFolder);
            if (!Directory.Exists(MaterialsFolder)) Directory.CreateDirectory(MaterialsFolder);
            if (!Directory.Exists(FontsFolder)) Directory.CreateDirectory(FontsFolder);
        }

        private static void SetPrivateField(object target, string memberName, object value)
        {
            if (target == null) return;
            var type = target.GetType();
            var field = type.GetField(memberName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (field != null)
            {
                field.SetValue(target, value);
                if (target is UnityEngine.Object unityObj)
                {
                    EditorUtility.SetDirty(unityObj);
                }
                return;
            }
            var prop = type.GetProperty(memberName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(target, value);
                if (target is UnityEngine.Object unityObj)
                {
                    EditorUtility.SetDirty(unityObj);
                }
            }
        }

        private static object GetPrivateField(object target, string fieldName)
        {
            var field = target.GetType().GetField(fieldName, System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            return field?.GetValue(target);
        }
    }
}
#endif
