using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class ДверьИнтеракт : MonoBehaviour
{
    private const string ЯзыкКлюч = "PauseSimple.language";
    private const string ИконкиПуть = "Assets/ICONS/Controls/keyboard-mouse-input-icons-251008/keyboard-input-icons/";
    private const string ЗвукПоУмолчанию = "Assets/Door, Cabinet and Locker Sound Pack (Free)/FREE VERSION/Locked Door Turn Doorknob 3.wav";
    private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
    private static readonly int MainTexId = Shader.PropertyToID("_MainTex");
    private static readonly int UnlitColorMapId = Shader.PropertyToID("_UnlitColorMap");
    private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");

    [Header("Поиск двери")]
    [SerializeField] private string тегДвери = "Door";
    [SerializeField] private KeyCode клавишаДействия = KeyCode.E;

    [Header("Точка UI")]
    [SerializeField] private Transform точкаПривязки;
    [SerializeField] private Vector3 смещениеМира = new Vector3(0f, 1.1f, 0f);

    [Header("Звук")]
    [SerializeField] private AudioSource источникЗвука;
    [SerializeField] private AudioClip звукЗаперто;
    [SerializeField] [Range(0f, 1f)] private float громкостьOneShot = 1f;

    [Header("Текстура интеракта")]
    [SerializeField] private Renderer рендерерАнимации;
    [SerializeField] private Texture2D[] кадрыИнтеракт;
    [SerializeField] private float fpsАнимации = 12f;
    [SerializeField] private string папкаКадров = "Assets/sprite/16x32/frames_16x32/Interact";

    [Header("UI стиль")]
    [SerializeField] private Font шрифт;
    [SerializeField] private int размерШрифта = 28;
    [SerializeField] private Color цветТекста = Color.white;
    [SerializeField] private Color фонТекста = new Color(0f, 0f, 0f, 0.55f);
    [SerializeField] private float длительностьНадписи = 1.3f;
    [SerializeField] private Vector2 размерИконки = new Vector2(90f, 90f);

    private Canvas canvas;
    private Image иконкаКлавиши;
    private Text текстКлавиши;
    private RectTransform низПлашка;
    private Text низТекст;
    private Sprite иконкаСпрайт;
    private Material runtimeMat;
    private Texture originalTexture;
    private bool дверьРядом;
    private Collider текущаяДверь;
    private readonly HashSet<Collider> двериВТриггере = new HashSet<Collider>();
    private readonly Dictionary<Collider, int> счетчикНажатийПоДвери = new Dictionary<Collider, int>();
    private int нажатий;
    private Coroutine корутинаНадписи;
    private Coroutine корутинаАнимации;

    private void Awake()
    {
        if (точкаПривязки == null)
        {
            точкаПривязки = transform;
        }

        if (источникЗвука == null)
        {
            источникЗвука = GetComponent<AudioSource>();
        }

        if (рендерерАнимации != null)
        {
            runtimeMat = рендерерАнимации.material;
            originalTexture = ReadCurrentTexture(runtimeMat);
        }

        BuildUi();
        LoadKeyIcon();
    }

    private void Update()
    {
        UpdatePromptPosition();

        if (!дверьРядом || текущаяДверь == null)
        {
            return;
        }

        if (Input.GetKeyDown(клавишаДействия))
        {
            if (!счетчикНажатийПоДвери.TryGetValue(текущаяДверь, out нажатий))
            {
                нажатий = 0;
            }

            нажатий++;
            счетчикНажатийПоДвери[текущаяДверь] = нажатий;
            TryResolveDoorOverrides(текущаяДверь);

            if (источникЗвука != null && звукЗаперто != null)
            {
                источникЗвука.PlayOneShot(звукЗаперто, громкостьOneShot);
            }

            if (корутинаАнимации != null)
            {
                StopCoroutine(корутинаАнимации);
            }
            корутинаАнимации = StartCoroutine(ПроигратьКадрыИнтеракт());

            if (нажатий >= 2)
            {
                if (корутинаНадписи != null)
                {
                    StopCoroutine(корутинаНадписи);
                }
                корутинаНадписи = StartCoroutine(ПоказатьНадписьСнизу());
            }
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag(тегДвери))
        {
            return;
        }

        двериВТриггере.Add(other);
        текущаяДверь = ChooseNearestDoor();
        дверьРядом = текущаяДверь != null;
        TryResolveDoorOverrides(текущаяДверь);
        SetPromptVisible(true);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!other.CompareTag(тегДвери))
        {
            return;
        }

        двериВТриггере.Remove(other);
        if (текущаяДверь == other)
        {
            текущаяДверь = null;
        }

        текущаяДверь = ChooseNearestDoor();
        дверьРядом = текущаяДверь != null;
        if (!дверьРядом)
        {
            SetPromptVisible(false);
        }
    }

    private IEnumerator ПоказатьНадписьСнизу()
    {
        bool en = PlayerPrefs.GetInt(ЯзыкКлюч, 1) == 0;
        низТекст.text = en ? "locked" : "закрыто";
        низПлашка.gameObject.SetActive(true);

        yield return new WaitForSecondsRealtime(длительностьНадписи);

        низПлашка.gameObject.SetActive(false);
    }

    private IEnumerator ПроигратьКадрыИнтеракт()
    {
        if (runtimeMat == null || кадрыИнтеракт == null || кадрыИнтеракт.Length == 0)
        {
            yield break;
        }

        float frameDur = 1f / Mathf.Max(1f, fpsАнимации);
        for (int i = 0; i < кадрыИнтеракт.Length; i++)
        {
            SetTexture(runtimeMat, кадрыИнтеракт[i]);
            yield return new WaitForSeconds(frameDur);
        }

        if (originalTexture != null)
        {
            SetTexture(runtimeMat, originalTexture);
        }
    }

    private void BuildUi()
    {
        Font f = ResolveFont();

        GameObject canvasGo = new GameObject("DoorInteract Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1600;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject iconGo = new GameObject("ActionIcon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(canvasGo.transform, false);
        RectTransform iconRect = iconGo.GetComponent<RectTransform>();
        iconRect.sizeDelta = размерИконки;
        иконкаКлавиши = iconGo.GetComponent<Image>();
        иконкаКлавиши.preserveAspect = true;

        GameObject keyTextGo = new GameObject("ActionText", typeof(RectTransform), typeof(Text));
        keyTextGo.transform.SetParent(iconGo.transform, false);
        RectTransform keyTextRect = keyTextGo.GetComponent<RectTransform>();
        keyTextRect.anchorMin = Vector2.zero;
        keyTextRect.anchorMax = Vector2.one;
        keyTextRect.offsetMin = Vector2.zero;
        keyTextRect.offsetMax = Vector2.zero;

        текстКлавиши = keyTextGo.GetComponent<Text>();
        текстКлавиши.font = f;
        текстКлавиши.fontSize = Mathf.Max(8, размерШрифта);
        текстКлавиши.color = цветТекста;
        текстКлавиши.alignment = TextAnchor.MiddleCenter;
        текстКлавиши.text = клавишаДействия.ToString();

        GameObject bottomGo = new GameObject("LockedPanel", typeof(RectTransform), typeof(Image));
        bottomGo.transform.SetParent(canvasGo.transform, false);
        низПлашка = bottomGo.GetComponent<RectTransform>();
        низПлашка.anchorMin = new Vector2(0.5f, 0f);
        низПлашка.anchorMax = new Vector2(0.5f, 0f);
        низПлашка.pivot = new Vector2(0.5f, 0f);
        низПлашка.anchoredPosition = new Vector2(0f, 42f);
        низПлашка.sizeDelta = new Vector2(360f, 64f);

        Image bg = bottomGo.GetComponent<Image>();
        bg.color = фонТекста;

        GameObject bottomTextGo = new GameObject("LockedText", typeof(RectTransform), typeof(Text));
        bottomTextGo.transform.SetParent(bottomGo.transform, false);
        RectTransform btr = bottomTextGo.GetComponent<RectTransform>();
        btr.anchorMin = Vector2.zero;
        btr.anchorMax = Vector2.one;
        btr.offsetMin = new Vector2(8f, 6f);
        btr.offsetMax = new Vector2(-8f, -6f);

        низТекст = bottomTextGo.GetComponent<Text>();
        низТекст.font = f;
        низТекст.fontSize = Mathf.Max(8, размерШрифта - 4);
        низТекст.color = цветТекста;
        низТекст.alignment = TextAnchor.MiddleCenter;
        низТекст.text = "закрыто";

        низПлашка.gameObject.SetActive(false);
        SetPromptVisible(false);
    }

    private void UpdatePromptPosition()
    {
        if (canvas == null || иконкаКлавиши == null || !дверьРядом || текущаяДверь == null)
        {
            return;
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            return;
        }

        Transform anchor = точкаПривязки != null ? точкаПривязки : текущаяДверь.transform;
        Vector3 world = anchor.position + смещениеМира;
        Vector3 screen = cam.WorldToScreenPoint(world);
        bool visible = screen.z > 0.05f;

        иконкаКлавиши.enabled = visible && иконкаСпрайт != null;
        текстКлавиши.enabled = visible && иконкаСпрайт == null;

        RectTransform rt = иконкаКлавиши.rectTransform;
        rt.position = screen;
    }

    private void SetPromptVisible(bool value)
    {
        if (иконкаКлавиши != null)
        {
            иконкаКлавиши.gameObject.SetActive(value);
        }
    }

    private Font ResolveFont()
    {
        if (шрифт != null)
        {
            return шрифт;
        }

#if UNITY_EDITOR
        шрифт = AssetDatabase.LoadAssetAtPath<Font>("Assets/Fonts/Press_Start_2P/PressStart2P-Regular.ttf");
#endif
        return шрифт != null ? шрифт : Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
    }

    private void LoadKeyIcon()
    {
        Sprite sp = null;
#if UNITY_EDITOR
        string slug = KeyToSlug(клавишаДействия);
        if (!string.IsNullOrEmpty(slug))
        {
            sp = AssetDatabase.LoadAssetAtPath<Sprite>(ИконкиПуть + "key-" + slug + ".png");
        }
#endif
        иконкаСпрайт = sp;
        if (иконкаКлавиши != null)
        {
            иконкаКлавиши.sprite = sp;
            иконкаКлавиши.enabled = sp != null;
        }
        if (текстКлавиши != null)
        {
            текстКлавиши.text = клавишаДействия.ToString();
            текстКлавиши.enabled = sp == null;
        }
    }

    private Collider ChooseNearestDoor()
    {
        Collider best = null;
        float bestSqr = float.MaxValue;
        Vector3 p = transform.position;

        foreach (Collider c in двериВТриггере)
        {
            if (c == null)
            {
                continue;
            }

            float sqr = (c.ClosestPoint(p) - p).sqrMagnitude;
            if (sqr < bestSqr)
            {
                bestSqr = sqr;
                best = c;
            }
        }

        return best;
    }

    private void TryResolveDoorOverrides(Collider door)
    {
        if (door == null)
        {
            return;
        }

        if (точкаПривязки == null)
        {
            точкаПривязки = door.transform;
        }

        if (рендерерАнимации == null)
        {
            рендерерАнимации = door.GetComponentInChildren<Renderer>();
            if (рендерерАнимации != null)
            {
                runtimeMat = рендерерАнимации.material;
                originalTexture = ReadCurrentTexture(runtimeMat);
            }
        }

        if (источникЗвука == null)
        {
            источникЗвука = door.GetComponent<AudioSource>();
        }
    }

    private static Texture ReadCurrentTexture(Material mat)
    {
        if (mat == null)
        {
            return null;
        }
        if (mat.HasProperty(BaseMapId)) return mat.GetTexture(BaseMapId);
        if (mat.HasProperty(MainTexId)) return mat.GetTexture(MainTexId);
        if (mat.HasProperty(UnlitColorMapId)) return mat.GetTexture(UnlitColorMapId);
        if (mat.HasProperty(BaseColorMapId)) return mat.GetTexture(BaseColorMapId);
        return null;
    }

    private static void SetTexture(Material mat, Texture tex)
    {
        if (mat == null)
        {
            return;
        }
        if (mat.HasProperty(BaseMapId)) mat.SetTexture(BaseMapId, tex);
        if (mat.HasProperty(MainTexId)) mat.SetTexture(MainTexId, tex);
        if (mat.HasProperty(UnlitColorMapId)) mat.SetTexture(UnlitColorMapId, tex);
        if (mat.HasProperty(BaseColorMapId)) mat.SetTexture(BaseColorMapId, tex);
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
            case KeyCode.RightShift: return "shift";
            case KeyCode.LeftControl: return "ctrl";
            case KeyCode.RightControl: return "ctrl";
            case KeyCode.LeftAlt: return "alt";
            case KeyCode.RightAlt: return "alt";
            case KeyCode.Return:
            case KeyCode.KeypadEnter: return "enter";
            case KeyCode.UpArrow: return "arrow-up";
            case KeyCode.DownArrow: return "arrow-down";
            case KeyCode.LeftArrow: return "arrow-left";
            case KeyCode.RightArrow: return "arrow-right";
            default: return null;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        fpsАнимации = Mathf.Clamp(fpsАнимации, 1f, 30f);
        длительностьНадписи = Mathf.Clamp(длительностьНадписи, 0.3f, 5f);

        if (звукЗаперто == null)
        {
            звукЗаперто = AssetDatabase.LoadAssetAtPath<AudioClip>(ЗвукПоУмолчанию);
        }

        if (кадрыИнтеракт == null || кадрыИнтеракт.Length == 0)
        {
            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { папкаКадров });
            List<string> paths = new List<string>(guids.Length);
            for (int i = 0; i < guids.Length; i++)
            {
                string p = AssetDatabase.GUIDToAssetPath(guids[i]);
                if (!string.IsNullOrEmpty(p))
                {
                    paths.Add(p);
                }
            }
            paths.Sort(System.StringComparer.OrdinalIgnoreCase);

            List<Texture2D> textures = new List<Texture2D>(paths.Count);
            for (int i = 0; i < paths.Count; i++)
            {
                Texture2D t = AssetDatabase.LoadAssetAtPath<Texture2D>(paths[i]);
                if (t != null)
                {
                    textures.Add(t);
                }
            }

            if (textures.Count > 0)
            {
                кадрыИнтеракт = textures.ToArray();
            }
        }

        if (string.IsNullOrWhiteSpace(тегДвери))
        {
            тегДвери = "Door";
        }
    }
#endif
}
