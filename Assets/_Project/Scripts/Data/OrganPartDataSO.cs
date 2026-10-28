using UnityEngine;

namespace AnatomyAR.Data
{
    /// <summary>
    /// Перечисление основных анатомических систем организма человека.
    /// Используется для послойной фильтрации и цветового кодирования органов.
    /// </summary>
    public enum OrganSystemType
    {
        Skeletal,        // Костная система (Скелет)
        Muscular,        // Мышечная система
        Cardiovascular,  // Сердечно-сосудистая (Кровеносная) система
        Nervous,         // Нервная система (Головной и спинной мозг, нервы)
        Respiratory,     // Дыхательная система (Легкие, трахея, бронхи)
        Digestive        // Пищеварительная система (Желудок, печень, кишечник)
    }

    /// <summary>
    /// ScriptableObject для хранения верифицированных медицинских данных об отделе органа.
    /// Архитектура Data-Driven: позволяет врачам/методистам наполнять атлас без изменения кода C#.
    /// </summary>
    [CreateAssetMenu(fileName = "OrganPartData_New", menuName = "Anatomy AR/Organ Part Data", order = 1)]
    public class OrganPartDataSO : ScriptableObject
    {
        [Header("Идентификация")]
        [Tooltip("Уникальный строковый идентификатор части органа (например: heart_left_ventricle)")]
        [SerializeField] private string partId;

        [Tooltip("Официальное название на русском языке")]
        [SerializeField] private string titleRu;

        [Tooltip("Международный медицинский анатомический термин на латыни (Nomina Anatomica)")]
        [SerializeField] private string titleLatin;

        [Tooltip("Принадлежность к функциональной системе организма")]
        [SerializeField] private OrganSystemType systemType;

        [Header("Медицинская справка")]
        [Tooltip("Подробное описание анатомического строения и физиологических функций")]
        [TextArea(4, 10)]
        [SerializeField] private string description;

        [Tooltip("Клинический факт, распространенные патологии или интересная статистика для студентов")]
        [TextArea(3, 8)]
        [SerializeField] private string clinicalFact;

        [Header("Мультимедиа")]
        [Tooltip("Аудиозапись дикторского озвучивания названия и описания структуры")]
        [SerializeField] private AudioClip voiceoverClip;

        // Публичные свойства доступа (геттеры)
        public string PartId => partId;
        public string TitleRu => titleRu;
        public string TitleLatin => titleLatin;
        public OrganSystemType SystemType => systemType;
        public string Description => description;
        public string ClinicalFact => clinicalFact;
        public AudioClip VoiceoverClip => voiceoverClip;

        /// <summary>
        /// Форматированное составное название для выносок и заголовков.
        /// </summary>
        public string FormattedTitle => $"{titleRu} ({titleLatin})";
    }
}
