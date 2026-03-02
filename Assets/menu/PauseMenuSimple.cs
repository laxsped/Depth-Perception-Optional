using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

[DefaultExecutionOrder(200)]
public class PauseMenuSimple : MonoBehaviour
{
    [Header("Input")]
    [SerializeField] private KeyCode toggleKey = KeyCode.Escape;

    [Header("Style")]
    [SerializeField] private Color overlayColor = new Color(0f, 0f, 0f, 0.42f);
    [SerializeField] private int fontSize = 42;
    [SerializeField] private float buttonSpacing = 70f;

    [Header("Optional Font")]
    [SerializeField] private Font customFont;

    private Canvas canvas;
    private Image overlay;
    private Button resumeButton;
    private Button quitButton;
    private Text resumeLabel;
    private Text quitLabel;
    private bool isOpen;

    private void Awake()
    {
        EnsureEventSystem();
        BuildUi();
        SetOpen(false, true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            SetOpen(!isOpen);
        }
    }

    private void SetOpen(bool open, bool force = false)
    {
        if (!force && isOpen == open)
        {
            return;
        }

        isOpen = open;
        if (canvas != null)
        {
            canvas.enabled = open;
        }

        Time.timeScale = open ? 0f : 1f;
        AudioListener.pause = open;
        Cursor.visible = open;
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
    }

    private void Resume()
    {
        SetOpen(false);
    }

    private void Quit()
    {
        Time.timeScale = 1f;
        AudioListener.pause = false;
#if UNITY_EDITOR
        EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void BuildUi()
    {
        GameObject canvasGo = new GameObject("Pause Menu Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);

        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 2000;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        Font font = customFont != null ? customFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject overlayGo = new GameObject("Overlay", typeof(RectTransform), typeof(Image));
        overlayGo.transform.SetParent(canvasGo.transform, false);
        overlay = overlayGo.GetComponent<Image>();
        overlay.color = overlayColor;
        RectTransform overlayRect = overlayGo.GetComponent<RectTransform>();
        overlayRect.anchorMin = Vector2.zero;
        overlayRect.anchorMax = Vector2.one;
        overlayRect.offsetMin = Vector2.zero;
        overlayRect.offsetMax = Vector2.zero;

        resumeButton = CreateTextButton("Resume Button", "вернуться", font, new Vector2(0f, buttonSpacing * 0.5f), out resumeLabel);
        quitButton = CreateTextButton("Quit Button", "уйти", font, new Vector2(0f, -buttonSpacing * 0.5f), out quitLabel);

        resumeButton.onClick.AddListener(Resume);
        quitButton.onClick.AddListener(Quit);

        AddHoverQuestionMark(resumeButton.gameObject, resumeLabel, "вернуться");
        AddHoverQuestionMark(quitButton.gameObject, quitLabel, "уйти");
    }

    private Button CreateTextButton(string objectName, string text, Font font, Vector2 anchoredPosition, out Text label)
    {
        GameObject buttonGo = new GameObject(objectName, typeof(RectTransform), typeof(Button), typeof(Image));
        buttonGo.transform.SetParent(overlay.transform, false);

        RectTransform rect = buttonGo.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(500f, 56f);
        rect.anchoredPosition = anchoredPosition;

        Image bg = buttonGo.GetComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0f);

        Button btn = buttonGo.GetComponent<Button>();
        btn.transition = Selectable.Transition.None;

        GameObject textGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(buttonGo.transform, false);
        RectTransform tr = textGo.GetComponent<RectTransform>();
        tr.anchorMin = Vector2.zero;
        tr.anchorMax = Vector2.one;
        tr.offsetMin = Vector2.zero;
        tr.offsetMax = Vector2.zero;

        label = textGo.GetComponent<Text>();
        label.text = text;
        label.font = font;
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.raycastTarget = false;

        return btn;
    }

    private static void AddHoverQuestionMark(GameObject go, Text targetLabel, string baseText)
    {
        HoverQuestionSuffix hover = go.AddComponent<HoverQuestionSuffix>();
        hover.Initialize(targetLabel, baseText);
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }
}

public class HoverQuestionSuffix : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    private Text label;
    private string baseText;

    public void Initialize(Text targetLabel, string text)
    {
        label = targetLabel;
        baseText = text;
        if (label != null)
        {
            label.text = baseText;
        }
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (label != null)
        {
            label.text = baseText + "?";
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (label != null)
        {
            label.text = baseText;
        }
    }
}
