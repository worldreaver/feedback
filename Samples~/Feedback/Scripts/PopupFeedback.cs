using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Pancake.Threading.Tasks;
using TMPro;
using UnityEngine.UI;

namespace Pancake.Feedback
{
    using UnityEngine;
    
    public sealed class PopupFeedback : MonoBehaviour
    {
        [SerializeField] private Button btnFeedbackType;
        [SerializeField] private GameObject arrowFeedbackType;
        [SerializeField] private TextMeshProUGUI txtFeedbackType;
        [SerializeField] private Button btnPriorityLevel;
        [SerializeField] private GameObject arrowPriorityLevel;
        [SerializeField] private TextMeshProUGUI txtPriorityLevel;
        [SerializeField] private TMP_InputField inputFieldSummary;
        [SerializeField] private TMP_InputField inputFieldEmail;
        [SerializeField] private TMP_InputField inputFieldDetail;
        [SerializeField] private RectTransform selectFeedbackType;
        [SerializeField] private RectTransform selectFeedbackContainer;
        [SerializeField] private RectTransform selectPriorityLevel;
        [SerializeField] private RectTransform selectPriorityContainer;
        [SerializeField] private Button btnCancel;
        [SerializeField] private Button btnSubmit;
        [SerializeField] private GameObject popup;
        [SerializeField] private GameObject bgDimed;
        [SerializeField] private GameObject block;
        public Report report;

        private DropdownElement[] _feedbackTypeElements;
        private DropdownElement[] _priorityLevelElements;
        private int _indexFeedbackType;
        private int _indexPriorityLevel;
        private Trello _trello;
        private Coroutine _handle;
        private bool _isInitCompleted;
        private string _screenshotPath;

        public event Action CollectDataAction;
        public event Action SubmitFeedbackCompletedAction;

        private void Start()
        {
            btnFeedbackType.onClick.RemoveAllListeners();
            btnPriorityLevel.onClick.RemoveAllListeners();
            btnCancel.onClick.RemoveAllListeners();
            btnSubmit.onClick.RemoveAllListeners();
            btnFeedbackType.onClick.AddListener(OnButtonFeedbackTypeClicked);
            btnPriorityLevel.onClick.AddListener(OnButtonPriorityLevelClicked);
            btnCancel.onClick.AddListener(OnButtonCancelClicked);
            btnSubmit.onClick.AddListener(OnButtonSubmitClicked);

            _feedbackTypeElements = selectFeedbackContainer.GetComponentsInChildren<DropdownElement>(true);
            _priorityLevelElements = selectPriorityContainer.GetComponentsInChildren<DropdownElement>(true);

            txtFeedbackType.text = _feedbackTypeElements[_indexFeedbackType].Label.text;
            txtPriorityLevel.text = _priorityLevelElements[_indexPriorityLevel].Label.text;

            foreach (var feedbackTypeElement in _feedbackTypeElements)
            {
                feedbackTypeElement.Init(OnFeedbackTypeElementClicked, OnValidateSelectedFeedback);
            }

            foreach (var priorityLevelElement in _priorityLevelElements)
            {
                priorityLevelElement.Init(OnPriorityLevelElementClicked, OnValidateSelectedPriority);
            }

            Init();
            _isInitCompleted = true;
        }


        private void Init()
        {
            _trello = new Trello(FeedbackSettings.Instance.token);
            report = new Report();
        }

        public async void Show()
        {
            block.SetActive(false);
            await UniTask.WaitUntil(() => _isInitCompleted).Timeout(TimeSpan.FromSeconds(10));

            _handle = StartCoroutine(ScreenshotAndOpenForm());
        }

        public IEnumerator ScreenshotAndOpenForm()
        {
            if (FeedbackSettings.Instance.includeScreenshot)
            {
                var filename = "debug-" + DateTime.Now.ToString("ddMMyyyy-HHmmss") + ".png";
                _screenshotPath = Path.Combine(Application.persistentDataPath, filename);
#if !UNITY_EDITOR && (UNITY_ANDROID || UNITY_IOS)
                ScreenCapture.CaptureScreenshot(filename);
#else
                ScreenCapture.CaptureScreenshot(_screenshotPath);
#endif

                while (!File.Exists(_screenshotPath)) yield return 0;

                const int readAttempts = 5;
                const float timeout = 0.1f;
                Exception exception = null;
                for (var i = 0; i < readAttempts; i++)
                {
                    try
                    {
                        report.Screenshot = File.ReadAllBytes(_screenshotPath);
                        break;
                    }
                    catch (IOException e)
                    {
                        Debug.LogErrorFormat("[Feedback] IOException on screenshot read attempt {0}", i + 1);
                        Debug.LogException(e);
                    }
                    catch (Exception e)
                    {
                        Debug.LogErrorFormat("[Feedback] Unexpected error on screenshot read attempt {0}", i + 1);
                        Debug.LogException(e);
                        exception = e;
                        break;
                    }

                    yield return new WaitForSeconds(timeout);
                }

                if (report.Screenshot == null && exception != null) Debug.LogError("[Feedback]: Failed to capture screenshot.");
            }

            popup.SetActive(true);
            bgDimed.SetActive(true);
        }

        private void OnButtonSubmitClicked() { StartCoroutine(SubmitAsync()); }

        private void OnButtonCancelClicked() { Hide(); }

        private void Hide()
        {
            bgDimed.SetActive(false);
            popup.SetActive(false);
            gameObject.SetActive(false);

            inputFieldSummary.text = "";
            inputFieldEmail.text = "";
            inputFieldDetail.text = "";

            if (FeedbackSettings.Instance.includeScreenshot && File.Exists(_screenshotPath))
            {
                StopCoroutine(_handle);

                File.Delete(_screenshotPath);
            }

            _screenshotPath = "";
        }

        private void OnDisable()
        {
            if (File.Exists(_screenshotPath)) File.Delete(_screenshotPath);
        }

        private IEnumerator SubmitAsync()
        {
            report.List.id = FeedbackSettings.Instance.board.categoryIds[_indexFeedbackType];
            report.List.name = FeedbackSettings.Instance.board.categoryNames[_indexFeedbackType];
            report.Title = inputFieldSummary.text;
            report.AddLabel(FeedbackSettings.Instance.board.labels[_indexPriorityLevel]);

            if (!report.HasSection("Summary")) report.AddSection("Summary", 0);

            report["Summary"].SetText(inputFieldSummary.text);
            report["Summary"].SortOrder = 0;         
            
            if (!report.HasSection("Email")) report.AddSection("Email", 0);

            report["Email"].SetText(inputFieldEmail.text);
            report["Email"].SortOrder = 1;
            
            if (!report.HasSection("Detail")) report.AddSection("Detail", 0);

            report["Detail"].SetText(inputFieldDetail.text);
            report["Detail"].SortOrder = 2;
            
            block.SetActive(true);
            OnCollectDataAction();

            // add card to board
            yield return _trello.AddCard(report.Title ?? "[no summary]",
                report.ToString() ?? "[no detail]",
                report.Labels,
                report.List.id,
                report.Screenshot);

            // send up attachments 
            if (_trello.lastAddCardResponse != null && !_trello.uploadError) yield return AttachFilesAsync(_trello.lastAddCardResponse.id);

            if (_trello.uploadError)
            {
                // notify failure
                Debug.LogError("Trello upload failed.\n" + "Reason: " + _trello.errorMessage);

                if (_trello.uploadException != null) Debug.LogException(_trello.uploadException);
                else Debug.LogError(_trello.errorMessage);
            }
            else
            {
                // report success
                // you can show popup notification in here
                OnSubmitFeedbackCompletedAction();
            }
            Hide();
            report = new Report();
        }

        /// <summary>
        /// Attaches files on current report to card
        /// </summary>
        /// <param name="cardID"></param>
        /// <returns></returns>
        private IEnumerator AttachFilesAsync(string cardID)
        {
            for (var i = 0; i < report.Attachments.Count; i++)
            {
                var attachment = report.Attachments[i];
                yield return _trello.AddAttachmentAsync(cardID, attachment.Data, null, attachment.Name);

                if (_trello.uploadError) // failed to add attachment
                    Debug.LogError("Failed to attach file to report.\n" + "Reason: " + _trello.errorMessage);
            }
        }

        private bool OnValidateSelectedFeedback(int arg) => arg == _indexFeedbackType;
        private bool OnValidateSelectedPriority(int arg) => arg == _indexPriorityLevel;

        private void OnFeedbackTypeElementClicked(DropdownElement element)
        {
            _indexFeedbackType = element.Index;
            foreach (var feedbackTypeElement in _feedbackTypeElements)
            {
                feedbackTypeElement.RefreshView();
            }

            txtFeedbackType.text = _feedbackTypeElements[_indexFeedbackType].Label.text;
            OnButtonFeedbackTypeClicked();
        }

        private void OnPriorityLevelElementClicked(DropdownElement element)
        {
            _indexPriorityLevel = element.Index;
            foreach (var priorityLevelElement in _priorityLevelElements)
            {
                priorityLevelElement.RefreshView();
            }

            txtPriorityLevel.text = _priorityLevelElements[_indexPriorityLevel].Label.text;
            OnButtonPriorityLevelClicked();
        }

        private void OnButtonPriorityLevelClicked()
        {
            
            selectPriorityContainer.gameObject.SetActive(!selectPriorityContainer.gameObject.activeInHierarchy);
        }

        private void OnButtonFeedbackTypeClicked()
        {
            selectFeedbackContainer.gameObject.SetActive(!selectFeedbackContainer.gameObject.activeInHierarchy);
        }

        private void OnCollectDataAction() { CollectDataAction?.Invoke(); }
        private void OnSubmitFeedbackCompletedAction() { SubmitFeedbackCompletedAction?.Invoke(); }
    }
}