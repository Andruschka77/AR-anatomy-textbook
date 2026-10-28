using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using AnatomyAR.Components;
using AnatomyAR.Controllers;
using AnatomyAR.Data;
using AnatomyAR.Interaction;

namespace AnatomyAR.Quiz
{
    /// <summary>
    /// Контроллер интерактивного режима тестирования знаний («AR Викторина / Экзамен»).
    /// Случайным образом генерирует вопросы студенту (например: «Найдите на модели: Аорта (Aorta)»),
    /// регистрирует клики по 3D-мешам, подсвечивает правильные/ошибочные ответы зеленым/красным
    /// и ведет учет набранных баллов и прогресса.
    /// </summary>
    public class AnatomyQuizManager : MonoBehaviour
    {
        [Header("Ссылки на взаимодействие")]
        [SerializeField] private SelectionAndAnnotationManager selectionManager;

        [Header("UI Элементы викторины")]
        [Tooltip("Контейнер всего интерфейса викторины")]
        [SerializeField] private GameObject quizUIPanel;

        [Tooltip("Текст вопроса с русским и латинским названием целевого органа")]
        [SerializeField] private TextMeshProUGUI targetQuestionText;

        [Tooltip("Текст счетчика очков (например: 'Очки: 350')")]
        [SerializeField] private TextMeshProUGUI scoreText;

        [Tooltip("Текст текущего раунда (например: 'Вопрос 3 из 10')")]
        [SerializeField] private TextMeshProUGUI roundProgressText;

        [Tooltip("Текст обратной связи ('Верно!' / 'Ошибка! Попробуйте еще раз')")]
        [SerializeField] private TextMeshProUGUI feedbackBannerText;

        [Tooltip("Индикатор прогресса (Slider)")]
        [SerializeField] private Slider progressBar;

        [Tooltip("Кнопка досрочного завершения викторины")]
        [SerializeField] private Button exitQuizButton;

        [Header("Настройки подсветки и звука")]
        [SerializeField] private Color correctColor = new Color(0.1f, 1f, 0.2f, 1f);
        [SerializeField] private Color wrongColor = new Color(1f, 0.15f, 0.15f, 1f);
        [SerializeField] private int pointsPerCorrectAnswer = 100;
        [SerializeField] private float feedbackDuration = 1.6f;

        [Header("Аудиоэффекты")]
        [SerializeField] private AudioClip correctSfx;
        [SerializeField] private AudioClip wrongSfx;
        private AudioSource audioSource;

        private AnatomyModelSO activeModelData;
        private OrganController activeOrganController;
        private List<OrganPartDataSO> remainingQuizParts = new List<OrganPartDataSO>();
        private OrganPartDataSO currentTargetPart;

        private bool isQuizActive = false;
        private int currentScore = 0;
        private int totalQuestionsInSession = 0;
        private int currentQuestionIndex = 0;
        private bool isWaitingForInput = false;

        public bool IsQuizActive => isQuizActive;

        private void Awake()
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();

            if (quizUIPanel != null) quizUIPanel.SetActive(false);
            if (feedbackBannerText != null) feedbackBannerText.gameObject.SetActive(false);
            if (exitQuizButton != null) exitQuizButton.onClick.AddListener(ExitQuiz);
        }

        /// <summary>
        /// Привязывает текущую анатомическую модель для формирования пула вопросов.
        /// </summary>
        public void SetCurrentModel(AnatomyModelSO model, OrganController controller)
        {
            activeModelData = model;
            activeOrganController = controller;

            // Если викторина уже была открыта, перезапускаем с новыми вопросами
            if (isQuizActive)
            {
                StartQuiz();
            }
        }

        /// <summary>
        /// Переключает состояние викторины (Вкл/Выкл).
        /// </summary>
        public void ToggleQuizMode()
        {
            if (isQuizActive)
            {
                ExitQuiz();
            }
            else
            {
                StartQuiz();
            }
        }

        /// <summary>
        /// Запускает новую экзаменационную сессию.
        /// </summary>
        public void StartQuiz()
        {
            if (activeModelData == null || activeModelData.PartsList == null || activeModelData.PartsList.Count == 0)
            {
                Debug.LogWarning("[AnatomyQuiz] Нет данных об органах в текущей модели для викторины!");
                return;
            }

            isQuizActive = true;
            currentScore = 0;
            currentQuestionIndex = 0;

            remainingQuizParts.Clear();
            remainingQuizParts.AddRange(activeModelData.PartsList);
            totalQuestionsInSession = remainingQuizParts.Count;

            if (quizUIPanel != null) quizUIPanel.SetActive(true);

            // Подписываемся на события клика по мешам
            if (selectionManager != null)
            {
                selectionManager.OnPartSelected += HandleQuizPartSelected;
            }

            UpdateUIState();
            NextQuestion();
        }

        /// <summary>
        /// Завершает викторину и возвращает приложение в режим свободного осмотра.
        /// </summary>
        public void ExitQuiz()
        {
            isQuizActive = false;
            isWaitingForInput = false;

            if (selectionManager != null)
            {
                selectionManager.OnPartSelected -= HandleQuizPartSelected;
                selectionManager.ClearSelection();
            }

            if (quizUIPanel != null) quizUIPanel.SetActive(false);
            if (feedbackBannerText != null) feedbackBannerText.gameObject.SetActive(false);
        }

        private void NextQuestion()
        {
            if (remainingQuizParts.Count == 0)
            {
                FinishQuiz();
                return;
            }

            currentQuestionIndex++;
            int randomIndex = Random.Range(0, remainingQuizParts.Count);
            currentTargetPart = remainingQuizParts[randomIndex];
            remainingQuizParts.RemoveAt(randomIndex);

            if (targetQuestionText != null)
            {
                targetQuestionText.text = $"Укажите на модели:\n<b><color=#00E5FF>{currentTargetPart.TitleRu}</color></b>\n<i>({currentTargetPart.TitleLatin})</i>";
            }

            if (feedbackBannerText != null)
            {
                feedbackBannerText.gameObject.SetActive(false);
            }

            UpdateUIState();
            isWaitingForInput = true;
        }

        private void HandleQuizPartSelected(OrganPartInteractable part, OrganPartDataSO selectedData)
        {
            if (!isQuizActive || !isWaitingForInput || currentTargetPart == null) return;

            isWaitingForInput = false;
            bool isCorrect = (selectedData != null && selectedData.PartId == currentTargetPart.PartId);

            StartCoroutine(ProcessAnswerFeedback(part, isCorrect));
        }

        private IEnumerator ProcessAnswerFeedback(OrganPartInteractable part, bool isCorrect)
        {
            if (isCorrect)
            {
                currentScore += pointsPerCorrectAnswer;
                if (correctSfx != null && audioSource != null) audioSource.PlayOneShot(correctSfx);

                if (feedbackBannerText != null)
                {
                    feedbackBannerText.gameObject.SetActive(true);
                    feedbackBannerText.text = "<color=#2ECC71>ВЕРНО! Отличные знания анатомии!</color>";
                }

                if (part != null)
                {
                    part.SetHighlight(true, correctColor);
                }

                yield return new WaitForSeconds(feedbackDuration);

                if (part != null)
                {
                    part.SetHighlight(false, Color.black);
                }

                NextQuestion();
            }
            else
            {
                if (wrongSfx != null && audioSource != null) audioSource.PlayOneShot(wrongSfx);

                if (feedbackBannerText != null)
                {
                    feedbackBannerText.gameObject.SetActive(true);
                    string selectedName = selectedDataTitle(part);
                    feedbackBannerText.text = $"<color=#E74C3C>НЕВЕРНО! Это {selectedName}. Попробуйте снова!</color>";
                }

                if (part != null)
                {
                    part.SetHighlight(true, wrongColor);
                }

                yield return new WaitForSeconds(feedbackDuration);

                if (part != null)
                {
                    part.SetHighlight(false, Color.black);
                }

                // Даем повторить попытку
                isWaitingForInput = true;
            }

            UpdateUIState();
        }

        private string selectedDataTitle(OrganPartInteractable part)
        {
            if (part != null && part.PartData != null)
            {
                return part.PartData.TitleRu;
            }
            return "другой отдел";
        }

        private void UpdateUIState()
        {
            if (scoreText != null)
            {
                scoreText.text = $"Баллы: <b>{currentScore}</b>";
            }

            if (roundProgressText != null)
            {
                roundProgressText.text = $"Вопрос {currentQuestionIndex} из {totalQuestionsInSession}";
            }

            if (progressBar != null && totalQuestionsInSession > 0)
            {
                progressBar.value = (float)currentQuestionIndex / totalQuestionsInSession;
            }
        }

        private void FinishQuiz()
        {
            isWaitingForInput = false;
            if (targetQuestionText != null)
            {
                targetQuestionText.text = $"<b>ТЕСТИРОВАНИЕ ЗАВЕРШЕНО!</b>\nИтоговый результат: <color=#00E5FF>{currentScore} баллов</color>";
            }

            if (feedbackBannerText != null)
            {
                feedbackBannerText.gameObject.SetActive(true);
                feedbackBannerText.text = "<color=#F1C40F>Экзамен сдан успешно! Отличная подготовка к защите курсовой.</color>";
            }
        }
    }
}
