using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using UnityEngine.EventSystems;
using AnatomyAR.Data;
using AnatomyAR.Controllers;

namespace AnatomyAR.AR
{
    /// <summary>
    /// Режим работы AR-трекинга.
    /// </summary>
    public enum ARTrackingMode
    {
        PlaneDetection, // Безмаркерное размещение на столе/полу (SLAM)
        ImageTracking   // Привязка к печатной иллюстрации/учебнику (2D Image Tracking)
    }

    /// <summary>
    /// Контроллер пространственной привязки и гибридного трекинга в AR Foundation.
    /// Управляет размещением анатомических моделей на плоскостях, позиционированием по 2D-маркерам
    /// и переключением активного органа с сохранением пространственного якоря.
    /// </summary>
    public class ARPlacementAndTrackingManager : MonoBehaviour
    {
        [Header("AR Foundation Подсистемы")]
        [Tooltip("Менеджер AR-лучей для определения пересечений с плоскостями")]
        [SerializeField] private ARRaycastManager raycastManager;

        [Tooltip("Менеджер отслеживания поверхностей пола/стола")]
        [SerializeField] private ARPlaneManager planeManager;

        [Tooltip("Менеджер распознавания 2D-картинок (страниц учебника)")]
        [SerializeField] private ARTrackedImageManager trackedImageManager;

        [Tooltip("Менеджер пространственных якорей")]
        [SerializeField] private ARAnchorManager anchorManager;

        [Header("Данные и контент")]
        [Tooltip("Каталог доступных моделей анатомического атласа")]
        [SerializeField] private AnatomyDataSO anatomyDatabase;

        [Tooltip("Модель по умолчанию при первом запуске")]
        [SerializeField] private AnatomyModelSO defaultModel;

        [Header("Настройки спавна")]
        [Tooltip("Корневой объект, куда будут инстанцироваться 3D-модели")]
        [SerializeField] private Transform sessionOriginRoot;

        [Tooltip("Режим трекинга по умолчанию")]
        [SerializeField] private ARTrackingMode activeTrackingMode = ARTrackingMode.PlaneDetection;

        // События для уведомления UI и подсистем
        public event Action<OrganController, AnatomyModelSO> OnOrganPlacedOrSwitched;
        public event Action<bool> OnPlaneDetectionStateChanged;

        [Header("Сканирующий прицел плоскости (Reticle)")]
        [Tooltip("Визуальный маркер-прицел, следующий за поверхностью стола/пола")]
        [SerializeField] private GameObject placementReticle;

        // Текущее состояние
        private AnatomyModelSO currentModelData;
        private GameObject currentInstantiatedOrganGO;
        private OrganController currentOrganController;
        private GameObject currentAnchorGO;
        private bool isModelPlaced = false;
        private bool hasScannedPlane = false;
        private Pose lastScannedPose;

        // Список попаданий луча AR Raycast
        private static readonly List<ARRaycastHit> s_Hits = new List<ARRaycastHit>();

        public OrganController CurrentOrganController => currentOrganController;
        public AnatomyModelSO CurrentModelData => currentModelData;
        public bool IsModelPlaced => isModelPlaced;
        public ARTrackingMode ActiveTrackingMode => activeTrackingMode;
        public AnatomyDataSO AnatomyDatabase => anatomyDatabase;

        private void Awake()
        {
            // Автопоиск подсистем на XR Origin, если не назначены вручную в инспекторе
            if (raycastManager == null) raycastManager = FindObjectOfType<ARRaycastManager>();
            if (planeManager == null) planeManager = FindObjectOfType<ARPlaneManager>();
            if (trackedImageManager == null) trackedImageManager = FindObjectOfType<ARTrackedImageManager>();
            if (anchorManager == null) anchorManager = FindObjectOfType<ARAnchorManager>();
        }

        private void Start()
        {
            if (defaultModel != null)
            {
                currentModelData = defaultModel;
            }
            else if (anatomyDatabase != null && anatomyDatabase.AvailableModels.Count > 0)
            {
                currentModelData = anatomyDatabase.AvailableModels[0];
            }

            if (placementReticle == null)
            {
                placementReticle = CreateProceduralReticle();
            }
        }

        private void OnEnable()
        {
            if (trackedImageManager != null)
            {
                trackedImageManager.trackedImagesChanged += OnTrackedImagesChanged;
            }
        }

        private void OnDisable()
        {
            if (trackedImageManager != null)
            {
                trackedImageManager.trackedImagesChanged -= OnTrackedImagesChanged;
            }
        }

        private void Update()
        {
            if (activeTrackingMode == ARTrackingMode.PlaneDetection && !isModelPlaced)
            {
                UpdatePlaneScanning();
                CheckTouchForPlanePlacement();
            }

            #if UNITY_EDITOR
            if (Input.GetKeyDown(KeyCode.Space) && !isModelPlaced)
            {
                SimulateEditorPlacement(new Vector3(0, 0, 1.0f));
            }
            #endif
        }

        #region Безмаркерное размещение на плоскости (Plane Tracking)

        private void UpdatePlaneScanning()
        {
            Camera cam = Camera.main;
            Vector2 screenCenter = new Vector2(Screen.width * 0.5f, Screen.height * 0.45f);

            if (raycastManager != null && (raycastManager.Raycast(screenCenter, s_Hits, TrackableType.Planes) ||
                                           raycastManager.Raycast(new Vector2(Screen.width * 0.5f, Screen.height * 0.35f), s_Hits, TrackableType.Planes)))
            {
                hasScannedPlane = true;
                lastScannedPose = s_Hits[0].pose;

                if (cam != null)
                {
                    Vector3 camFwd = cam.transform.forward;
                    Vector3 proj = new Vector3(camFwd.x, 0, camFwd.z);
                    if (proj.sqrMagnitude > 0.001f)
                    {
                        lastScannedPose.rotation = Quaternion.LookRotation(proj.normalized, Vector3.up);
                    }
                }

                if (placementReticle != null)
                {
                    placementReticle.SetActive(true);
                    placementReticle.transform.SetPositionAndRotation(lastScannedPose.position, lastScannedPose.rotation);
                }
            }
            else
            {
                hasScannedPlane = false;
                if (cam != null)
                {
                    Vector3 forwardAimPos = cam.transform.position + cam.transform.forward * 0.70f + Vector3.down * 0.12f;
                    Quaternion forwardAimRot = Quaternion.Euler(0f, cam.transform.eulerAngles.y + 180f, 0f);
                    lastScannedPose = new Pose(forwardAimPos, forwardAimRot);

                    if (placementReticle != null)
                    {
                        placementReticle.SetActive(true);
                        placementReticle.transform.SetPositionAndRotation(forwardAimPos, Quaternion.Euler(30f, cam.transform.eulerAngles.y, 0f));
                    }
                }
            }
        }

        private void CheckTouchForPlanePlacement()
        {
            if (!AnatomyAR.Interaction.UniversalInputHelper.GetTapDown(out Vector2 touchPos))
            {
                return;
            }

            // Игнорируем нажатия ТОЛЬКО если палец попал по реальной кнопке/слайдеру
            if (AnatomyAR.Interaction.UniversalInputHelper.IsPointerOverInteractiveUI(touchPos))
            {
                return;
            }

            // Пускаем луч AR Raycast по горизонтальным поверхностям или ставим на маркер
            if (raycastManager != null && raycastManager.Raycast(touchPos, s_Hits, TrackableType.Planes))
            {
                PlaceModelAtPose(s_Hits[0].pose);
            }
            else
            {
                PlaceModelAtPose(lastScannedPose);
            }
        }

        /// <summary>
        /// Размещает модель принудительно (по кнопке в UI «УСТАНОВИТЬ МОДЕЛЬ СЮДА»).
        /// </summary>
        public void PlaceModelDirectly()
        {
            PlaceModelAtPose(lastScannedPose);
        }

        private void PlaceModelAtPose(Pose pose)
        {
            if (currentModelData == null || currentModelData.ArPrefab == null)
            {
                Debug.LogWarning("[ARPlacement] Не задан AnatomyModelSO или префаб органа!");
                return;
            }

            // Очищаем старые инстансы и якорь перед пересозданием
            if (currentInstantiatedOrganGO != null)
            {
                Destroy(currentInstantiatedOrganGO);
                currentInstantiatedOrganGO = null;
            }

            if (currentAnchorGO != null)
            {
                Destroy(currentAnchorGO);
                currentAnchorGO = null;
            }

            // Создаем пространственный якорь в реальных координатах мира
            if (anchorManager != null)
            {
                try
                {
                    ARAnchor anchor = anchorManager.AddAnchor(pose);
                    if (anchor != null)
                    {
                        currentAnchorGO = anchor.gameObject;
                    }
                }
                catch (System.Exception ex)
                {
                    Debug.LogWarning("[ARPlacement] AddAnchor: " + ex.Message);
                }
            }

            if (currentAnchorGO == null)
            {
                currentAnchorGO = new GameObject("AR_Organ_Anchor_Root");
                currentAnchorGO.transform.position = pose.position;
                currentAnchorGO.transform.rotation = pose.rotation;
            }

            currentInstantiatedOrganGO = Instantiate(currentModelData.ArPrefab, pose.position, pose.rotation, currentAnchorGO.transform);

            currentOrganController = currentInstantiatedOrganGO.GetComponent<OrganController>();
            if (currentOrganController != null)
            {
                currentOrganController.ResetOrientation(currentModelData.DefaultSpawnScale);
            }

            isModelPlaced = true;
            if (placementReticle != null) placementReticle.SetActive(false);

            // Отключаем отображение сетки плоскостей и скрываем подсказку
            SetPlanesActive(false);

            OnOrganPlacedOrSwitched?.Invoke(currentOrganController, currentModelData);
            OnPlaneDetectionStateChanged?.Invoke(false);
        }

        private GameObject CreateProceduralReticle()
        {
            GameObject reticleGO = new GameObject("AR_Placement_Reticle");
            MeshFilter mf = reticleGO.AddComponent<MeshFilter>();
            MeshRenderer mr = reticleGO.AddComponent<MeshRenderer>();

            Mesh mesh = new Mesh();
            mesh.name = "ReticleRing";
            int segments = 64;
            float innerRadius = 0.065f;
            float outerRadius = 0.085f;

            Vector3[] vertices = new Vector3[segments * 2];
            int[] triangles = new int[segments * 6];

            for (int i = 0; i < segments; i++)
            {
                float angle = i * Mathf.PI * 2f / segments;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                vertices[i * 2] = new Vector3(cos * innerRadius, 0.002f, sin * innerRadius);
                vertices[i * 2 + 1] = new Vector3(cos * outerRadius, 0.002f, sin * outerRadius);

                int next = (i + 1) % segments;
                triangles[i * 6 + 0] = i * 2;
                triangles[i * 6 + 1] = next * 2;
                triangles[i * 6 + 2] = i * 2 + 1;

                triangles[i * 6 + 3] = i * 2 + 1;
                triangles[i * 6 + 4] = next * 2;
                triangles[i * 6 + 5] = next * 2 + 1;
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mf.sharedMesh = mesh;

            Shader unlit = Shader.Find("Unlit/Transparent");
            if (unlit == null) unlit = Shader.Find("Mobile/Particles/Alpha Blended");
            if (unlit == null) unlit = Shader.Find("UI/Default");
            Material mat = new Material(unlit);
            Color whiteCircle = new Color(1f, 1f, 1f, 0.95f);
            mat.color = whiteCircle;
            mat.SetColor("_Color", whiteCircle);
            mat.renderQueue = 3500;
            mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
            mr.sharedMaterial = mat;

            reticleGO.SetActive(false);
            return reticleGO;
        }

        #endregion

        #region Маркерный трекинг по карточке/учебнику (2D Image Tracking)

        private void OnTrackedImagesChanged(ARTrackedImagesChangedEventArgs eventArgs)
        {
            if (activeTrackingMode != ARTrackingMode.ImageTracking) return;

            foreach (var trackedImage in eventArgs.added)
            {
                HandleTrackedImage(trackedImage);
            }

            foreach (var trackedImage in eventArgs.updated)
            {
                if (trackedImage.trackingState == TrackingState.Tracking && isModelPlaced && currentInstantiatedOrganGO != null)
                {
                    // Обновляем позицию и поворот вслед за маркером
                    currentInstantiatedOrganGO.transform.position = trackedImage.transform.position;
                    currentInstantiatedOrganGO.transform.rotation = trackedImage.transform.rotation;
                }
            }
        }

        private void HandleTrackedImage(ARTrackedImage trackedImage)
        {
            if (trackedImage.trackingState != TrackingState.Tracking) return;

            Pose pose = new Pose(trackedImage.transform.position, trackedImage.transform.rotation);
            PlaceModelAtPose(pose);
        }

        #endregion

        #region Управление состоянием и смена моделей

        /// <summary>
        /// Переключает активную анатомическую модель (например, с Сердца на Печень).
        /// Гарантированно спавнит модель на том же месте или перед камерой.
        /// </summary>
        public void SwitchActiveOrgan(AnatomyModelSO newModel)
        {
            if (newModel == null) return;

            currentModelData = newModel;

            if (currentInstantiatedOrganGO != null)
            {
                Vector3 currentPos = currentInstantiatedOrganGO.transform.position;
                Quaternion currentRot = currentInstantiatedOrganGO.transform.rotation;
                Transform parent = currentInstantiatedOrganGO.transform.parent;

                Destroy(currentInstantiatedOrganGO);

                if (newModel.ArPrefab != null)
                {
                    currentInstantiatedOrganGO = Instantiate(newModel.ArPrefab, currentPos, currentRot, parent);
                    currentOrganController = currentInstantiatedOrganGO.GetComponent<OrganController>();
                    if (currentOrganController != null)
                    {
                        currentOrganController.ResetOrientation(newModel.DefaultSpawnScale);
                    }
                }

                OnOrganPlacedOrSwitched?.Invoke(currentOrganController, currentModelData);
            }
            else
            {
                // Если модель еще не размещена, размещаем немедленно перед камерой
                Camera cam = Camera.main;
                if (cam != null && newModel.ArPrefab != null)
                {
                    Vector3 spawnPos = cam.transform.position + cam.transform.forward * 0.65f + Vector3.down * 0.1f;
                    Quaternion spawnRot = Quaternion.Euler(0f, cam.transform.eulerAngles.y + 180f, 0f);
                    PlaceModelAtPose(new Pose(spawnPos, spawnRot));
                }
            }
        }

        /// <summary>
        /// Позволяет пользователю заново выбрать плоскость и переместить орган по кнопке в UI.
        /// </summary>
        public void RequestReposition()
        {
            if (currentInstantiatedOrganGO != null)
            {
                Destroy(currentInstantiatedOrganGO);
                currentInstantiatedOrganGO = null;
                currentOrganController = null;
            }

            if (currentAnchorGO != null)
            {
                Destroy(currentAnchorGO);
                currentAnchorGO = null;
            }

            isModelPlaced = false;
            hasScannedPlane = false;
            if (placementReticle != null)
            {
                placementReticle.SetActive(false);
            }

            SetPlanesActive(true);
            OnPlaneDetectionStateChanged?.Invoke(true);
        }

        /// <summary>
        /// Переключает режим трекинга между плоскостями (SLAM) и маркерами (Image).
        /// </summary>
        public void SetTrackingMode(ARTrackingMode mode)
        {
            activeTrackingMode = mode;
            if (planeManager != null)
            {
                planeManager.enabled = (mode == ARTrackingMode.PlaneDetection);
            }
            if (trackedImageManager != null)
            {
                trackedImageManager.enabled = (mode == ARTrackingMode.ImageTracking);
            }
        }

        /// <summary>
        /// Включает/выключает визуализацию и обнаружение плоскостей.
        /// </summary>
        public void SetPlanesActive(bool active)
        {
            if (planeManager != null)
            {
                planeManager.enabled = active;
                foreach (var plane in planeManager.trackables)
                {
                    plane.gameObject.SetActive(active);
                }
            }
            OnPlaneDetectionStateChanged?.Invoke(active);
        }

        #if UNITY_EDITOR
        private void SimulateEditorPlacement(Vector3 spawnPosition)
        {
            Pose dummyPose = new Pose(spawnPosition, Quaternion.identity);
            PlaceModelAtPose(dummyPose);
            Debug.Log("[ARPlacement] Симуляция размещения модели в редакторе Unity успешна.");
        }
        #endif

        #endregion
    }
}
