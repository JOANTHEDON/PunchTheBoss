using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using SFB;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

[System.Serializable]
public class BossVoiceClip {
    public string clipName;
    public AudioClip audioClip;
    public string filePath;
}

/// <summary>
/// Manages boss voice/audio reactions and the Voice Menu panel.
/// BossFaceUploader.OnTextureReady() calls OpenVoiceMenu().
/// When the player clicks "Ready & Start!" OnStartGameplayClicked fires
/// the onVoiceSetupCompleted callback → BossFaceUploader shows weapons.
/// </summary>
public class BossVoiceManager : MonoBehaviour {

    public static BossVoiceManager Instance { get; private set; }

    // ── Inspector references ────────────────────────────────────────────────
    [Header("Audio")]
    [SerializeField] private AudioSource audioSource;

    [Header("Voice Clips")]
    [SerializeField] private List<BossVoiceClip> voiceClips = new List<BossVoiceClip>();

    [Header("Pitch Settings")]
    [SerializeField] private float minPitch = 0.88f;
    [SerializeField] private float maxPitch = 1.15f;

    // ── Runtime state ───────────────────────────────────────────────────────
    private GameObject   voiceMenuPanel;
    private Transform    voiceListContainer;
    private Button       addVoiceButton;
    private Button       startGameplayButton;
    private Action       onVoiceSetupCompleted;

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    private void Awake() {
        if (Instance != null && Instance != this) {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (audioSource == null) {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
                audioSource = gameObject.AddComponent<AudioSource>();
        }
    }

    private void Start() {
        if (voiceClips.Count == 0)
            CreateDefaultVoices();

        // If panel was injected via SetVoiceMenuPanel before Start, bind its buttons now
        if (voiceMenuPanel != null)
            BindPanelButtons();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PUBLIC API  (called by BossFaceUploader)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Injects the scene-wired BossVoiceMenuPanel so we never build a duplicate.
    /// Call before OpenVoiceMenu.
    /// </summary>
    public void SetVoiceMenuPanel(GameObject panel) {
        voiceMenuPanel = panel;
        if (voiceMenuPanel != null) {
            voiceMenuPanel.SetActive(false);   // always start hidden
            BindPanelButtons();
        }
    }

    /// <summary>
    /// Shows the voice menu. onCompleted fires when the player clicks "Ready &amp; Start!".
    /// </summary>
    public void OpenVoiceMenu(Action onCompleted) {
        onVoiceSetupCompleted = onCompleted;

        // If no panel was injected, search for it (including inactive objects)
        if (voiceMenuPanel == null)
            voiceMenuPanel = FindInactiveByName("BossVoiceMenuPanel");

        // Last resort: build one from scratch
        if (voiceMenuPanel == null)
            BuildVoiceMenuUI();

        // Bind buttons (idempotent — RemoveListener before AddListener)
        BindPanelButtons();

        // Show the panel
        if (voiceMenuPanel != null) {
            voiceMenuPanel.SetActive(true);
            voiceMenuPanel.transform.SetAsLastSibling();
        }

        RefreshVoiceListUI();
        Debug.Log("[BossVoiceManager] Voice menu opened.");
    }

    /// <summary>Hides the voice menu panel.</summary>
    public void HideVoiceMenu() {
        if (voiceMenuPanel != null)
            voiceMenuPanel.SetActive(false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BUTTON HANDLERS
    // ═══════════════════════════════════════════════════════════════════════

    public void OnAddVoiceClicked() {
        var ext = new[] { new ExtensionFilter("Audio Files", "wav", "mp3", "ogg") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Select Boss Hit Reaction Audio", "", ext, false);
        if (paths != null && paths.Length > 0 && !string.IsNullOrEmpty(paths[0]))
            StartCoroutine(LoadAudioRoutine(paths[0]));
    }

    public void OnStartGameplayClicked() {
        Debug.Log("[BossVoiceManager] Start clicked → hiding voice menu, invoking callback.");
        if (voiceMenuPanel != null)
            voiceMenuPanel.SetActive(false);

        onVoiceSetupCompleted?.Invoke();
        onVoiceSetupCompleted = null;   // clear so it can't fire twice
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AUDIO PLAYBACK
    // ═══════════════════════════════════════════════════════════════════════

    public void PlayHitReactionVoice(WeaponType weapon) {
        if (voiceClips.Count == 0 || audioSource == null) return;
        BossVoiceClip clip = voiceClips[UnityEngine.Random.Range(0, voiceClips.Count)];
        if (clip?.audioClip == null) return;
        audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
        audioSource.PlayOneShot(clip.audioClip, weapon == WeaponType.Super ? 1f : 0.8f);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private void BindPanelButtons() {
        if (voiceMenuPanel == null) return;

        // Find VoiceListContainer
        if (voiceListContainer == null)
            voiceListContainer = FindDeepChild(voiceMenuPanel.transform, "VoiceListContainer");

        // Find buttons by name (works for both scene-built and runtime-built panels)
        addVoiceButton      = null;
        startGameplayButton = null;
        foreach (var b in voiceMenuPanel.GetComponentsInChildren<Button>(true)) {
            if (b.name.Contains("Add"))   addVoiceButton      = b;
            if (b.name.Contains("Start")) startGameplayButton = b;
        }

        if (addVoiceButton != null) {
            addVoiceButton.onClick.RemoveAllListeners();
            addVoiceButton.onClick.AddListener(OnAddVoiceClicked);
        }
        if (startGameplayButton != null) {
            startGameplayButton.onClick.RemoveAllListeners();
            startGameplayButton.onClick.AddListener(OnStartGameplayClicked);
        }

        Debug.Log($"[BossVoiceManager] Panel buttons bound — " +
                  $"Add:{addVoiceButton != null}, Start:{startGameplayButton != null}");
    }

    private IEnumerator LoadAudioRoutine(string path) {
        if (!File.Exists(path)) yield break;
        string ext = Path.GetExtension(path).ToLower();
        AudioType type = ext == ".mp3" ? AudioType.MPEG :
                         ext == ".ogg" ? AudioType.OGGVORBIS : AudioType.WAV;

        using (var www = UnityWebRequestMultimedia.GetAudioClip("file://" + path, type)) {
            yield return www.SendWebRequest();
            if (www.result == UnityWebRequest.Result.Success) {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                clip.name = Path.GetFileNameWithoutExtension(path);
                voiceClips.Add(new BossVoiceClip { clipName = clip.name, audioClip = clip, filePath = path });
                Debug.Log($"[BossVoiceManager] Voice added: {clip.name}");
                RefreshVoiceListUI();
            } else {
                Debug.LogError($"[BossVoiceManager] Audio load failed: {www.error}");
            }
        }
    }

    private void RefreshVoiceListUI() {
        if (voiceListContainer == null) return;

        foreach (Transform child in voiceListContainer) Destroy(child.gameObject);

        for (int i = 0; i < voiceClips.Count; i++) {
            int idx  = i;
            var clip = voiceClips[i];

            var row = new GameObject($"VoiceItem_{i}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            row.transform.SetParent(voiceListContainer, false);
            row.GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.25f, 0.9f);
            var hlg = row.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f; hlg.childControlWidth = false; hlg.childControlHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;
            row.GetComponent<RectTransform>().sizeDelta = new Vector2(800f, 90f);

            // Label
            var label = MakeText(row.transform, $"🔊 {clip.clipName}", 30, new Vector2(450, 70));

            // Play button
            var playBtn = MakeButton(row.transform, "PlayBtn", "▶ Play", new Color(0.3f,0.7f,0.4f), new Vector2(140,65));
            playBtn.onClick.AddListener(() => {
                if (audioSource != null && clip.audioClip != null) {
                    audioSource.pitch = 1f;
                    audioSource.PlayOneShot(clip.audioClip);
                }
            });

            // Remove button (only if more than 1 clip)
            if (voiceClips.Count > 1) {
                var delBtn = MakeButton(row.transform, "DelBtn", "✖", new Color(0.8f,0.3f,0.3f), new Vector2(110,65));
                delBtn.onClick.AddListener(() => { voiceClips.RemoveAt(idx); RefreshVoiceListUI(); });
            }
        }
    }

    // ── Runtime UI builder (fallback only if no panel in scene) ────────────

    private void BuildVoiceMenuUI() {
        Canvas canvas = FindMainCanvas();
        if (canvas == null) {
            var cObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = cObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = cObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
        }
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            new GameObject("EventSystem",
                typeof(UnityEngine.EventSystems.EventSystem),
                typeof(UnityEngine.EventSystems.StandaloneInputModule));

        // Panel
        voiceMenuPanel = new GameObject("BossVoiceMenuPanel",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        voiceMenuPanel.transform.SetParent(canvas.transform, false);
        var panelRt = voiceMenuPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero; panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero; panelRt.offsetMax = Vector2.zero;
        voiceMenuPanel.GetComponent<Image>().color = new Color(0,0,0,0.88f);

        // Window box
        var box = new GameObject("VoiceWindow",
            typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        box.transform.SetParent(voiceMenuPanel.transform, false);
        box.GetComponent<Image>().color = new Color(0.12f,0.14f,0.18f,0.98f);
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = boxRt.anchorMax = boxRt.pivot = new Vector2(0.5f,0.5f);
        boxRt.sizeDelta = new Vector2(900f,1300f);
        var vlg = box.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(30,30,35,35); vlg.spacing = 25f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = false; vlg.childControlHeight = false;

        // Title
        MakeText(box.transform, "🎙️ Boss Voice Menu", 44, new Vector2(840,70));

        // Subtitle
        MakeText(box.transform,
            "Add reaction audio (.wav .mp3 .ogg) then hit Start!",
            26, new Vector2(840,55));

        // List container
        var listObj = new GameObject("VoiceListContainer",
            typeof(RectTransform), typeof(VerticalLayoutGroup));
        listObj.transform.SetParent(box.transform, false);
        voiceListContainer = listObj.transform;
        var listVlg = listObj.GetComponent<VerticalLayoutGroup>();
        listVlg.spacing = 15f; listVlg.childAlignment = TextAnchor.UpperCenter;
        listVlg.childControlWidth = false; listVlg.childControlHeight = false;
        listObj.GetComponent<RectTransform>().sizeDelta = new Vector2(840f,750f);

        // Buttons bar
        var bar = new GameObject("ButtonsBar",
            typeof(RectTransform), typeof(HorizontalLayoutGroup));
        bar.transform.SetParent(box.transform, false);
        var barHlg = bar.GetComponent<HorizontalLayoutGroup>();
        barHlg.spacing = 25f; barHlg.childAlignment = TextAnchor.MiddleCenter;
        barHlg.childControlWidth = false; barHlg.childControlHeight = false;
        bar.GetComponent<RectTransform>().sizeDelta = new Vector2(840f,100f);

        addVoiceButton = MakeButton(bar.transform, "AddVoiceBtn", "➕ Add Voice File",
            new Color(0.2f,0.6f,0.9f), new Vector2(380,90));
        addVoiceButton.onClick.AddListener(OnAddVoiceClicked);

        startGameplayButton = MakeButton(bar.transform, "StartGameBtn", "🎮 Ready & Start!",
            new Color(0.2f,0.8f,0.3f), new Vector2(400,90));
        startGameplayButton.onClick.AddListener(OnStartGameplayClicked);

        Debug.Log("[BossVoiceManager] Runtime voice menu built.");
    }

    // ── Small UI factories ──────────────────────────────────────────────────

    private static Text MakeText(Transform parent, string content, int size, Vector2 sizeDelta) {
        var go = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        go.transform.SetParent(parent, false);
        go.GetComponent<RectTransform>().sizeDelta = sizeDelta;
        var t = go.GetComponent<Text>();
        t.text = content;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.color = Color.white;
        t.alignment = TextAnchor.MiddleCenter;
        return t;
    }

    private static Button MakeButton(Transform parent, string name, string label,
                                     Color bg, Vector2 sizeDelta) {
        var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer),
                                typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        go.GetComponent<Image>().color = bg;
        go.GetComponent<RectTransform>().sizeDelta = sizeDelta;
        MakeText(go.transform, label, 26, sizeDelta);
        return go.GetComponent<Button>();
    }

    // ── Scene search helpers ────────────────────────────────────────────────

    private static Canvas FindMainCanvas() {
        foreach (var c in FindObjectsByType<Canvas>(FindObjectsSortMode.None))
            if (!c.name.Contains("Damage")) return c;
        return null;
    }

    private static GameObject FindInactiveByName(string name) {
        foreach (var rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            if (rt.name == name) return rt.gameObject;
        return null;
    }

    private static Transform FindDeepChild(Transform parent, string name) {
        foreach (Transform child in parent) {
            if (child.name == name) return child;
            var found = FindDeepChild(child, name);
            if (found != null) return found;
        }
        return null;
    }

    // ── Default procedural sounds ───────────────────────────────────────────

    private void CreateDefaultVoices() {
        voiceClips.Add(new BossVoiceClip { clipName = "Ouch!",  audioClip = Beep(440f, 0.15f) });
        voiceClips.Add(new BossVoiceClip { clipName = "Grunt!", audioClip = Beep(220f, 0.22f) });
        voiceClips.Add(new BossVoiceClip { clipName = "Yelp!",  audioClip = Beep(650f, 0.12f) });
    }

    private static AudioClip Beep(float freq, float dur) {
        int rate    = 44100;
        int samples = Mathf.RoundToInt(rate * dur);
        float[] data = new float[samples];
        for (int i = 0; i < samples; i++) {
            float t   = (float)i / rate;
            float env = Mathf.Exp(-t * 18f);
            float pd  = Mathf.Max(0.5f, 1f - (t / dur) * 0.6f);
            data[i]   = Mathf.Sin(2f * Mathf.PI * (freq * pd) * t) * env;
        }
        var clip = AudioClip.Create($"Voice_{freq}", samples, 1, rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
