using UnityEngine;

namespace AnatomyAR.Components
{
    /// <summary>
    /// Вспомогательный компонент для элементов интерфейса в мировом пространстве (World Space Canvas, 3D Labels).
    /// Поворачивает объект "лицом" к активной AR-камере в LateUpdate, обеспечивая читаемость текста с любого ракурса.
    /// </summary>
    public class BillboardUI : MonoBehaviour
    {
        [Header("Настройки")]
        [Tooltip("Фиксировать поворот только вокруг вертикальной оси Y (предотвращает наклон вверх/вниз)")]
        [SerializeField] private bool lockYOnly = false;

        [Tooltip("Инвертировать направление взгляда (если Canvas отрендерен задом наперед)")]
        [SerializeField] private bool reverseFace = false;

        private Transform targetCameraTransform;

        private void Start()
        {
            UpdateCameraReference();
        }

        private void UpdateCameraReference()
        {
            if (Camera.main != null)
            {
                targetCameraTransform = Camera.main.transform;
            }
        }

        private void LateUpdate()
        {
            if (targetCameraTransform == null)
            {
                UpdateCameraReference();
                if (targetCameraTransform == null) return;
            }

            Vector3 lookDir = targetCameraTransform.position - transform.position;

            if (reverseFace)
            {
                lookDir = -lookDir;
            }

            if (lockYOnly)
            {
                lookDir.y = 0;
            }

            if (lookDir.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(-lookDir);
            }
        }
    }
}
