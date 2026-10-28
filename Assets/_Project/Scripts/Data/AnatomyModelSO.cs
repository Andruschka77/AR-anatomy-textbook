using System.Collections.Generic;
using UnityEngine;

namespace AnatomyAR.Data
{
    /// <summary>
    /// ScriptableObject верхнего уровня для описания цельной 3D-модели органа или системы (например: Сердце, Мозг, Торс).
    /// Содержит ссылку на AR-префаб, перечень всех входящих в него анатомических частей и настройки по умолчанию.
    /// </summary>
    [CreateAssetMenu(fileName = "AnatomyModel_New", menuName = "Anatomy AR/Anatomy Model Data", order = 2)]
    public class AnatomyModelSO : ScriptableObject
    {
        [Header("Основные сведения")]
        [Tooltip("Уникальный идентификатор модели (например: model_human_heart)")]
        [SerializeField] private string modelId;

        [Tooltip("Название органа для отображения в главном меню")]
        [SerializeField] private string titleRu;

        [Tooltip("Иконка органа для отображения на кнопках выбора")]
        [SerializeField] private Sprite icon;

        [Tooltip("Ссылка на подготовленный префаб органа с компонентом OrganController")]
        [SerializeField] private GameObject arPrefab;

        [Header("Анатомическая структура")]
        [Tooltip("Список всех интерактивных отделов/долей, входящих в данную модель")]
        [SerializeField] private List<OrganPartDataSO> partsList = new List<OrganPartDataSO>();

        [Header("Параметры масштаба и анимации")]
        [Tooltip("Рекомендуемый базовый масштаб модели при первичном спавне в AR (например: 0.15 для настольного режима)")]
        [SerializeField] [Range(0.01f, 2.0f)] private float defaultSpawnScale = 0.2f;

        [Tooltip("Поддерживает ли данная модель процедурную пульсацию (биение)")]
        [SerializeField] private bool supportsPulsation = false;

        [Tooltip("Базовая частота пульсации по умолчанию (BPM / уд. в минуту)")]
        [SerializeField] [Range(40f, 180f)] private float defaultBPM = 75f;

        // Публичные свойства доступа
        public string ModelId => modelId;
        public string TitleRu => titleRu;
        public Sprite Icon => icon;
        public GameObject ArPrefab => arPrefab;
        public IReadOnlyList<OrganPartDataSO> PartsList => partsList;
        public float DefaultSpawnScale => defaultSpawnScale;
        public bool SupportsPulsation => supportsPulsation;
        public float DefaultBPM => defaultBPM;
    }
}
