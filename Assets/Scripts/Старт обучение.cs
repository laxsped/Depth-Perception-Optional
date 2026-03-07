using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class StartupMovementTutorial : MonoBehaviour
{
    private const string PausePrefsPrefix = "PauseSimple.";
    [Serializable]
    private struct KeyIconEntry
    {
        public KeyCode key;
        public Sprite sprite;
    }

    private enum TutorialStep
    {
        Move,
        Jump,
        Done
    }

    [Header("Visual")]
    [SerializeField] private Font tutorialFont;
    [SerializeField] private int fontSize = 28;
    [SerializeField] private Color panelColor = new Color(0f, 0f, 0f, 0.62f);
    [SerializeField] private Color textColor = Color.white;

    [Header("Flow")]
    [SerializeField] private float delayBeforeJumpPrompt = 10f;
    [SerializeField] private bool unlockRunOnTutorialComplete = false;
    [SerializeField] private bool keepRunLockedForNextScene = true;

    [Header("Key Icons")]
    [SerializeField] private List<KeyIconEntry> keyIcons = new List<KeyIconEntry>();

    private const string IconBasePath = "Assets/ICONS/Controls/keyboard-mouse-input-icons-251008/keyboard-input-icons/";

    private readonly Dictionary<KeyCode, Sprite> iconMap = new Dictionary<KeyCode, Sprite>();

    private Canvas canvas;
    private RectTransform panel;
    private Image firstIcon;
    private Image secondIcon;
    private Text label;

    private TutorialStep step = TutorialStep.Move;
    private float elapsed;
    private bool pressedMoveKey;
    private KeyCode cachedLeft;
    private KeyCode cachedRight;
    private KeyCode cachedJump;

    private void Awake()
    {
        GameInputBindings.EnsureLoaded();
        GameInputBindings.RunLocked = true;

        BuildIconMap();
        BuildUi();
        RefreshPrompt(true);
    }

    private void Update()
    {
        if (step == TutorialStep.Done)
        {
            return;
        }

        elapsed += Time.deltaTime;

        if (step == TutorialStep.Move)
        {
            if (!pressedMoveKey && (Input.GetKeyDown(GameInputBindings.LeftKey) || Input.GetKeyDown(GameInputBindings.RightKey)))
            {
                pressedMoveKey = true;
            }

            if (pressedMoveKey && elapsed >= delayBeforeJumpPrompt)
            {
                step = TutorialStep.Jump;
                RefreshPrompt(true);
            }
        }
        else if (step == TutorialStep.Jump)
        {
            if (Input.GetKeyDown(GameInputBindings.JumpKey))
            {
                step = TutorialStep.Done;
                if (unlockRunOnTutorialComplete && !keepRunLockedForNextScene)
                {
                    GameInputBindings.RunLocked = false;
                }
                if (canvas != null)
                {
                    canvas.enabled = false;
                }
            }
        }

        RefreshPrompt(false);
    }

    private void OnDestroy()
    {
        if (unlockRunOnTutorialComplete && !keepRunLockedForNextScene && step != TutorialStep.Done)
        {
            GameInputBindings.RunLocked = false;
        }
    }

    private void BuildUi()
    {
        Font font = ResolveFont();

        GameObject canvasGo = new GameObject("Tutorial Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1500;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject panelGo = new GameObject("Tutorial Panel", typeof(RectTransform), typeof(Image));
        panelGo.transform.SetParent(canvasGo.transform, false);
        panel = panelGo.GetComponent<RectTransform>();
        panel.anchorMin = new Vector2(0.5f, 0f);
        panel.anchorMax = new Vector2(0.5f, 0f);
        panel.pivot = new Vector2(0.5f, 0f);
        panel.anchoredPosition = new Vector2(0f, 56f);
        panel.sizeDelta = new Vector2(1120f, 148f);
        panelGo.GetComponent<Image>().color = panelColor;

        GameObject rowGo = new GameObject("Row", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        rowGo.transform.SetParent(panel, false);
        RectTransform rowRect = rowGo.GetComponent<RectTransform>();
        rowRect.anchorMin = Vector2.zero;
        rowRect.anchorMax = Vector2.one;
        rowRect.offsetMin = new Vector2(32f, 16f);
        rowRect.offsetMax = new Vector2(-32f, -16f);

        HorizontalLayoutGroup h = rowGo.GetComponent<HorizontalLayoutGroup>();
        h.childAlignment = TextAnchor.MiddleCenter;
        h.spacing = 20f;
        h.childControlWidth = false;
        h.childControlHeight = false;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = false;

        firstIcon = CreateIcon(rowRect, "Icon A");
        secondIcon = CreateIcon(rowRect, "Icon B");
        label = CreateLabel(rowRect, font);
    }

    private static Image CreateIcon(Transform parent, string name)
    {
        GameObject go = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        go.transform.SetParent(parent, false);
        RectTransform rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(80f, 80f);
        Image img = go.GetComponent<Image>();
        img.preserveAspect = true;

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 80f;
        le.preferredHeight = 80f;
        return img;
    }

    private Text CreateLabel(Transform parent, Font font)
    {
        GameObject go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
        go.transform.SetParent(parent, false);

        Text t = go.GetComponent<Text>();
        t.font = font;
        t.fontSize = Mathf.Max(8, fontSize);
        t.color = textColor;
        t.alignment = TextAnchor.MiddleLeft;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;

        LayoutElement le = go.GetComponent<LayoutElement>();
        le.preferredWidth = 600f;
        le.flexibleWidth = 1f;
        return t;
    }

    private Font ResolveFont()
    {
        if (tutorialFont != null)
        {
            return tutorialFont;
        }

#if UNITY_EDITOR
        tutorialFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
#endif
        return tutorialFont != null ? tutorialFont : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void BuildIconMap()
    {
        iconMap.Clear();
        for (int i = 0; i < keyIcons.Count; i++)
        {
            KeyIconEntry entry = keyIcons[i];
            if (entry.sprite != null)
            {
                iconMap[NormalizeKey(entry.key)] = entry.sprite;
            }
        }
    }

    private void RefreshPrompt(bool force)
    {
        KeyCode left = NormalizeKey(GameInputBindings.LeftKey);
        KeyCode right = NormalizeKey(GameInputBindings.RightKey);
        KeyCode jump = NormalizeKey(GameInputBindings.JumpKey);

        if (!force && left == cachedLeft && right == cachedRight && jump == cachedJump)
        {
            return;
        }

        cachedLeft = left;
        cachedRight = right;
        cachedJump = jump;
        bool isEnglish = PlayerPrefs.GetInt(PausePrefsPrefix + "language", 1) == 0;

        if (step == TutorialStep.Move)
        {
            ApplyIcon(firstIcon, left);
            ApplyIcon(secondIcon, right);
            secondIcon.gameObject.SetActive(true);
            label.text = isEnglish ? "move" : "ходьба";
            return;
        }

        if (step == TutorialStep.Jump)
        {
            ApplyIcon(firstIcon, jump);
            secondIcon.gameObject.SetActive(false);
            label.text = isEnglish ? "jump" : "прыжок";
        }
    }

    private void ApplyIcon(Image image, KeyCode key)
    {
        if (image == null)
        {
            return;
        }

        if (iconMap.TryGetValue(key, out Sprite sprite) && sprite != null)
        {
            image.enabled = true;
            image.sprite = sprite;
            return;
        }

#if UNITY_EDITOR
        string slug = KeyToSlug(key);
        if (!string.IsNullOrEmpty(slug))
        {
            Sprite loaded = AssetDatabase.LoadAssetAtPath<Sprite>(IconBasePath + "key-" + slug + ".png");
            if (loaded != null)
            {
                iconMap[key] = loaded;
                image.enabled = true;
                image.sprite = loaded;
                return;
            }
        }
#endif

        image.enabled = false;
        image.sprite = null;
    }

    private static KeyCode NormalizeKey(KeyCode key)
    {
        switch (key)
        {
            case KeyCode.RightShift: return KeyCode.LeftShift;
            case KeyCode.RightControl: return KeyCode.LeftControl;
            case KeyCode.RightAlt: return KeyCode.LeftAlt;
            case KeyCode.KeypadEnter: return KeyCode.Return;
            default: return key;
        }
    }

    private static string KeyToSlug(KeyCode key)
    {
        if (key >= KeyCode.A && key <= KeyCode.Z)
        {
            int offset = (int)key - (int)KeyCode.A;
            return ((char)('a' + offset)).ToString();
        }

        if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
        {
            return ((int)key - (int)KeyCode.Alpha0).ToString();
        }

        switch (key)
        {
            case KeyCode.Space: return "space";
            case KeyCode.LeftShift: return "shift";
            case KeyCode.LeftControl: return "ctrl";
            case KeyCode.LeftAlt: return "alt";
            case KeyCode.UpArrow: return "arrow-up";
            case KeyCode.DownArrow: return "arrow-down";
            case KeyCode.LeftArrow: return "arrow-left";
            case KeyCode.RightArrow: return "arrow-right";
            case KeyCode.Return: return "enter";
            default: return null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (tutorialFont == null)
        {
            tutorialFont = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
        }

        PopulateIconLibrary();
    }

    private void PopulateIconLibrary()
    {
        EnsureIconEntry(KeyCode.A);
        EnsureIconEntry(KeyCode.B);
        EnsureIconEntry(KeyCode.C);
        EnsureIconEntry(KeyCode.D);
        EnsureIconEntry(KeyCode.E);
        EnsureIconEntry(KeyCode.F);
        EnsureIconEntry(KeyCode.G);
        EnsureIconEntry(KeyCode.H);
        EnsureIconEntry(KeyCode.I);
        EnsureIconEntry(KeyCode.J);
        EnsureIconEntry(KeyCode.K);
        EnsureIconEntry(KeyCode.L);
        EnsureIconEntry(KeyCode.M);
        EnsureIconEntry(KeyCode.N);
        EnsureIconEntry(KeyCode.O);
        EnsureIconEntry(KeyCode.P);
        EnsureIconEntry(KeyCode.Q);
        EnsureIconEntry(KeyCode.R);
        EnsureIconEntry(KeyCode.S);
        EnsureIconEntry(KeyCode.T);
        EnsureIconEntry(KeyCode.U);
        EnsureIconEntry(KeyCode.V);
        EnsureIconEntry(KeyCode.W);
        EnsureIconEntry(KeyCode.X);
        EnsureIconEntry(KeyCode.Y);
        EnsureIconEntry(KeyCode.Z);
        EnsureIconEntry(KeyCode.Alpha0);
        EnsureIconEntry(KeyCode.Alpha1);
        EnsureIconEntry(KeyCode.Alpha2);
        EnsureIconEntry(KeyCode.Alpha3);
        EnsureIconEntry(KeyCode.Alpha4);
        EnsureIconEntry(KeyCode.Alpha5);
        EnsureIconEntry(KeyCode.Alpha6);
        EnsureIconEntry(KeyCode.Alpha7);
        EnsureIconEntry(KeyCode.Alpha8);
        EnsureIconEntry(KeyCode.Alpha9);
        EnsureIconEntry(KeyCode.Space);
        EnsureIconEntry(KeyCode.LeftShift);
        EnsureIconEntry(KeyCode.LeftControl);
        EnsureIconEntry(KeyCode.LeftAlt);
        EnsureIconEntry(KeyCode.UpArrow);
        EnsureIconEntry(KeyCode.DownArrow);
        EnsureIconEntry(KeyCode.LeftArrow);
        EnsureIconEntry(KeyCode.RightArrow);
        EnsureIconEntry(KeyCode.Return);
    }

    private void EnsureIconEntry(KeyCode key)
    {
        int index = keyIcons.FindIndex(x => x.key == key);
        KeyIconEntry entry = index >= 0 ? keyIcons[index] : new KeyIconEntry { key = key };
        if (entry.sprite == null)
        {
            string slug = KeyToSlug(key);
            if (!string.IsNullOrEmpty(slug))
            {
                entry.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(IconBasePath + "key-" + slug + ".png");
            }
        }

        if (index >= 0)
        {
            keyIcons[index] = entry;
        }
        else if (entry.sprite != null)
        {
            keyIcons.Add(entry);
        }
    }
#endif
}


