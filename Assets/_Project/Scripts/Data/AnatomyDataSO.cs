using System.Collections.Generic;
using UnityEngine;

namespace AnatomyAR.Data
{
    /// <summary>
    /// Каталог всех доступных в атласе анатомических моделей.
    /// Позволяет централизованно управлять набором моделей для лекций, лабораторных и викторин.
    /// </summary>
    [CreateAssetMenu(fileName = "AnatomyDatabase", menuName = "Anatomy AR/Anatomy Database", order = 0)]
    public class AnatomyDataSO : ScriptableObject
    {
        [Tooltip("Список всех доступных для исследования 3D-моделей органов")]
        [SerializeField] private List<AnatomyModelSO> availableModels = new List<AnatomyModelSO>();

        public IReadOnlyList<AnatomyModelSO> AvailableModels => availableModels;
    }
}
