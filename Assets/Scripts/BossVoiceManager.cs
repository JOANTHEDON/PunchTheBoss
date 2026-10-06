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
/// Manages boss voice sound clips (Ouch, Grunt, Screams, Custom imported audio files)
/// and displays a UI menu right after face editing so players can add 3+ custom reaction sounds!
/// </summary>
public class BossVoiceManager : MonoBehaviour {

    public static BossVoiceManager Instance { get; private set; }

    [Header("Audio Source Target")]
    [SerializeField] private AudioSource audioSource;

    [Header("Voice Clips Collection")]
    [SerializeField] private List<BossVoiceClip> voiceClips = new List<BossVoiceClip>();

    [Header("UI Menu References")]
    [SerializeField] private GameObject voiceMenuPanel;
    [SerializeField] private Transform voiceListContainer;
    [SerializeField] private Button addVoiceButton;
    [SerializeField] private Button startGameplayButton;

    // Pitch variation settings for dynamic hit reactions
    [Header("Audio Reaction Dynamics")]
    [SerializeField] private float minPitch = 0.88f;
    [SerializeField] private float maxPitch = 1.15f;

    private System.Action onVoiceSetupCompleted;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        } else {
            Destroy(gameObject);
            return;
        }

        if (audioSource == null) {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null) {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }
    }

    private void Start() {
        // Generate initial default procedural reaction sounds if none loaded yet
        if (voiceClips.Count == 0) {
            CreateDefaultProceduralVoices();
        }

        if (voiceMenuPanel == null) {
            BuildVoiceMenuUI();
        }

        if (addVoiceButton != null) {
            addVoiceButton.onClick.AddListener(OnAddVoiceClicked);
        }
        if (startGameplayButton != null) {
            startGameplayButton.onClick.AddListener(OnStartGameplayClicked);
        }

        if (voiceMenuPanel != null) {
            voiceMenuPanel.SetActive(false);
        }
    }

    /// <summary>
    /// Opens the Voice Reaction Setup Menu (triggered right after image editing completes).
    /// </summary>
    public void OpenVoiceMenu(System.Action onCompleted) {
        onVoiceSetupCompleted = onCompleted;

        if (voiceMenuPanel == null) {
            BuildVoiceMenuUI();
        }
        
        if (voiceMenuPanel != null) {
            voiceMenuPanel.SetActive(true);
            voiceMenuPanel.transform.SetAsLastSibling(); // Ensure menu renders on top of all UI
        }

        RefreshVoiceListUI();
    }

    public void OnAddVoiceClicked() {
        var extensions = new[] {
            new ExtensionFilter("Audio Files", "wav", "mp3", "ogg")
        };

        string[] paths = StandaloneFileBrowser.OpenFilePanel(
            "Select Boss Hit Reaction Audio",
            "",
            extensions,
            false
        );

        if (paths.Length > 0 && !string.IsNullOrEmpty(paths[0])) {
            StartCoroutine(LoadAudioFileRoutine(paths[0]));
        }
    }

    private IEnumerator LoadAudioFileRoutine(string filePath) {
        if (!File.Exists(filePath)) yield break;

        string extension = Path.GetExtension(filePath).ToLower();
        AudioType audioType = AudioType.WAV;
        if (extension == ".mp3") audioType = AudioType.MPEG;
        else if (extension == ".ogg") audioType = AudioType.OGGVORBIS;

        string fileUri = "file://" + filePath;
        using (UnityWebRequest www = UnityWebRequestMultimedia.GetAudioClip(fileUri, audioType)) {
            yield return www.SendWebRequest();

            if (www.result == UnityWebRequest.Result.Success) {
                AudioClip clip = DownloadHandlerAudioClip.GetContent(www);
                string fileName = Path.GetFileNameWithoutExtension(filePath);
                clip.name = fileName;

                BossVoiceClip newVoice = new BossVoiceClip {
                    clipName = fileName,
                    audioClip = clip,
                    filePath = filePath
                };

                voiceClips.Add(newVoice);
                Debug.Log($"[BossVoiceManager] Added custom voice reaction: {fileName}");
                RefreshVoiceListUI();
            } else {
                Debug.LogError($"[BossVoiceManager] Failed to load audio file: {www.error}");
            }
        }
    }

    public void OnStartGameplayClicked() {
        if (voiceMenuPanel != null) {
            voiceMenuPanel.SetActive(false);
        }

        onVoiceSetupCompleted?.Invoke();
    }

    /// <summary>
    /// Plays a random boss voice clip when hit, varying pitch for variety!
    /// </summary>
    public void PlayHitReactionVoice(WeaponType weapon) {
        if (voiceClips.Count == 0 || audioSource == null) return;

        int index = UnityEngine.Random.Range(0, voiceClips.Count);
        BossVoiceClip clipToPlay = voiceClips[index];

        if (clipToPlay != null && clipToPlay.audioClip != null) {
            audioSource.pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            
            // Adjust volume according to hit intensity
            float volume = weapon == WeaponType.Super ? 1.0f : 0.8f;
            audioSource.PlayOneShot(clipToPlay.audioClip, volume);
        }
    }

    private void RefreshVoiceListUI() {
        if (voiceListContainer == null) return;

        foreach (Transform child in voiceListContainer) {
            Destroy(child.gameObject);
        }

        for (int i = 0; i < voiceClips.Count; i++) {
            int index = i;
            BossVoiceClip clip = voiceClips[i];

            GameObject itemObj = new GameObject($"VoiceItem_{i}", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
            itemObj.transform.SetParent(voiceListContainer, false);

            Image itemBg = itemObj.GetComponent<Image>();
            itemBg.color = new Color(0.2f, 0.2f, 0.25f, 0.9f);

            HorizontalLayoutGroup hlg = itemObj.GetComponent<HorizontalLayoutGroup>();
            hlg.spacing = 20f;
            hlg.childControlWidth = false;
            hlg.childControlHeight = false;
            hlg.childAlignment = TextAnchor.MiddleCenter;

            RectTransform itemRt = itemObj.GetComponent<RectTransform>();
            itemRt.sizeDelta = new Vector2(800f, 90f);

            // Label
            GameObject nameObj = new GameObject("Text", typeof(RectTransform), typeof(Text));
            nameObj.transform.SetParent(itemObj.transform, false);
            Text txt = nameObj.GetComponent<Text>();
            txt.text = $"🔊 {clip.clipName}";
            txt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            txt.fontSize = 30;
            txt.color = Color.white;
            txt.alignment = TextAnchor.MiddleLeft;
            nameObj.GetComponent<RectTransform>().sizeDelta = new Vector2(450f, 70f);

            // Play Preview Button
            GameObject playBtnObj = new GameObject("PlayBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            playBtnObj.transform.SetParent(itemObj.transform, false);
            playBtnObj.GetComponent<Image>().color = new Color(0.3f, 0.7f, 0.4f);
            Button playBtn = playBtnObj.GetComponent<Button>();
            playBtn.onClick.AddListener(() => {
                if (audioSource != null && clip.audioClip != null) {
                    audioSource.pitch = 1.0f;
                    audioSource.PlayOneShot(clip.audioClip);
                }
            });
            playBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 65f);

            GameObject playTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
            playTxtObj.transform.SetParent(playBtnObj.transform, false);
            Text playTxt = playTxtObj.GetComponent<Text>();
            playTxt.text = "▶ Play";
            playTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            playTxt.fontSize = 24;
            playTxt.color = Color.white;
            playTxt.alignment = TextAnchor.MiddleCenter;
            playTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(140f, 65f);

            // Remove Button
            if (voiceClips.Count > 1) {
                GameObject delBtnObj = new GameObject("DelBtn", typeof(RectTransform), typeof(Image), typeof(Button));
                delBtnObj.transform.SetParent(itemObj.transform, false);
                delBtnObj.GetComponent<Image>().color = new Color(0.8f, 0.3f, 0.3f);
                Button delBtn = delBtnObj.GetComponent<Button>();
                delBtn.onClick.AddListener(() => {
                    voiceClips.RemoveAt(index);
                    RefreshVoiceListUI();
                });
                delBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 65f);

                GameObject delTxtObj = new GameObject("Txt", typeof(RectTransform), typeof(Text));
                delTxtObj.transform.SetParent(delBtnObj.transform, false);
                Text delTxt = delTxtObj.GetComponent<Text>();
                delTxt.text = "✖";
                delTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                delTxt.fontSize = 26;
                delTxt.color = Color.white;
                delTxt.alignment = TextAnchor.MiddleCenter;
                delTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(110f, 65f);
            }
        }
    }

    /// <summary>
    /// Programmatically constructs the Voice Selection & Audio Upload UI on Canvas
    /// </summary>
    public void BuildVoiceMenuUI() {
        // Find main UI Canvas (ignore DamageCanvas)
        Canvas mainCanvas = null;
        Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
        foreach (var c in canvases) {
            if (c.name.Equals("Canvas")) {
                mainCanvas = c;
                break;
            }
        }

        if (mainCanvas == null && canvases.Length > 0) {
            foreach (var c in canvases) {
                if (!c.name.Contains("Damage")) {
                    mainCanvas = c;
                    break;
                }
            }
        }

        if (mainCanvas == null) {
            GameObject cObj = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            mainCanvas = cObj.GetComponent<Canvas>();
            mainCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            CanvasScaler scaler = cObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920); // 9:16 aspect ratio
        }

        // Ensure EventSystem exists for clicks to work
        if (FindFirstObjectByType<UnityEngine.EventSystems.EventSystem>() == null) {
            new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem), typeof(UnityEngine.EventSystems.StandaloneInputModule));
        }

        if (voiceMenuPanel != null) {
            Destroy(voiceMenuPanel);
        }

        // Overlay Panel under main Canvas
        voiceMenuPanel = new GameObject("BossVoiceMenuPanel", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        voiceMenuPanel.transform.SetParent(mainCanvas.transform, false);

        RectTransform panelRt = voiceMenuPanel.GetComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;
        voiceMenuPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.88f);

        // Window Box tuned for 9:16 Aspect Ratio
        GameObject box = new GameObject("VoiceWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(VerticalLayoutGroup));
        box.transform.SetParent(voiceMenuPanel.transform, false);
        box.GetComponent<Image>().color = new Color(0.12f, 0.14f, 0.18f, 0.98f);

        RectTransform boxRt = box.GetComponent<RectTransform>();
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.pivot = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(900f, 1300f); // Responsive 9:16 sizing

        VerticalLayoutGroup vlg = box.GetComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(30, 30, 35, 35);
        vlg.spacing = 25f;
        vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = false;
        vlg.childControlHeight = false;

        // Title Text
        GameObject titleObj = new GameObject("TitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        titleObj.transform.SetParent(box.transform, false);
        Text titleTxt = titleObj.GetComponent<Text>();
        titleTxt.text = "🎙️ Boss Hit Reactions & Voice Menu";
        titleTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        titleTxt.fontSize = 44;
        titleTxt.fontStyle = FontStyle.Bold;
        titleTxt.color = Color.gold;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        titleObj.GetComponent<RectTransform>().sizeDelta = new Vector2(840f, 70f);

        // Subtitle
        GameObject subObj = new GameObject("SubTitleText", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        subObj.transform.SetParent(box.transform, false);
        Text subTxt = subObj.GetComponent<Text>();
        subTxt.text = "Add custom reaction audio files (.wav, .mp3, .ogg) to play when boss is struck!";
        subTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        subTxt.fontSize = 28;
        subTxt.color = Color.lightGray;
        subTxt.alignment = TextAnchor.MiddleCenter;
        subObj.GetComponent<RectTransform>().sizeDelta = new Vector2(840f, 60f);

        // Voice List Container
        GameObject listObj = new GameObject("VoiceListContainer", typeof(RectTransform), typeof(VerticalLayoutGroup));
        listObj.transform.SetParent(box.transform, false);
        voiceListContainer = listObj.transform;

        VerticalLayoutGroup listVlg = listObj.GetComponent<VerticalLayoutGroup>();
        listVlg.spacing = 15f;
        listVlg.childAlignment = TextAnchor.UpperCenter;
        listVlg.childControlWidth = false;
        listVlg.childControlHeight = false;
        listObj.GetComponent<RectTransform>().sizeDelta = new Vector2(840f, 750f);

        // Buttons Bar Container
        GameObject btnsBar = new GameObject("ButtonsBar", typeof(RectTransform), typeof(HorizontalLayoutGroup));
        btnsBar.transform.SetParent(box.transform, false);
        HorizontalLayoutGroup hlg = btnsBar.GetComponent<HorizontalLayoutGroup>();
        hlg.spacing = 25f;
        hlg.childAlignment = TextAnchor.MiddleCenter;
        hlg.childControlWidth = false;
        hlg.childControlHeight = false;
        btnsBar.GetComponent<RectTransform>().sizeDelta = new Vector2(840f, 100f);

        // Add Voice Button
        GameObject addBtnObj = new GameObject("AddVoiceBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        addBtnObj.transform.SetParent(btnsBar.transform, false);
        addBtnObj.GetComponent<Image>().color = new Color(0.2f, 0.6f, 0.9f);
        addVoiceButton = addBtnObj.GetComponent<Button>();
        addVoiceButton.onClick.AddListener(OnAddVoiceClicked);
        addBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(380f, 90f);

        GameObject addTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        addTxtObj.transform.SetParent(addBtnObj.transform, false);
        Text addTxt = addTxtObj.GetComponent<Text>();
        addTxt.text = "➕ Add Voice File";
        addTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        addTxt.fontSize = 28;
        addTxt.color = Color.white;
        addTxt.alignment = TextAnchor.MiddleCenter;
        addTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(380f, 90f);

        // Start Gameplay Button
        GameObject startBtnObj = new GameObject("StartGameBtn", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Button));
        startBtnObj.transform.SetParent(btnsBar.transform, false);
        startBtnObj.GetComponent<Image>().color = new Color(0.2f, 0.8f, 0.3f);
        startGameplayButton = startBtnObj.GetComponent<Button>();
        startGameplayButton.onClick.AddListener(OnStartGameplayClicked);
        startBtnObj.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 90f);

        GameObject startTxtObj = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        startTxtObj.transform.SetParent(startBtnObj.transform, false);
        Text startTxt = startTxtObj.GetComponent<Text>();
        startTxt.text = "🎮 Ready & Start!";
        startTxt.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        startTxt.fontSize = 30;
        startTxt.fontStyle = FontStyle.Bold;
        startTxt.color = Color.white;
        startTxt.alignment = TextAnchor.MiddleCenter;
        startTxtObj.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 90f);
    }

    private void CreateDefaultProceduralVoices() {
        voiceClips.Add(new BossVoiceClip { clipName = "Default Reaction 1 (Ouch!)", audioClip = GenerateProceduralOuchClip(440f, 0.15f) });
        voiceClips.Add(new BossVoiceClip { clipName = "Default Reaction 2 (Grunt!)", audioClip = GenerateProceduralOuchClip(220f, 0.22f) });
        voiceClips.Add(new BossVoiceClip { clipName = "Default Reaction 3 (Yelp!)", audioClip = GenerateProceduralOuchClip(650f, 0.12f) });
    }

    private AudioClip GenerateProceduralOuchClip(float freq, float duration) {
        int sampleRate = 44100;
        int totalSamples = Mathf.RoundToInt(sampleRate * duration);
        float[] samples = new float[totalSamples];

        for (int i = 0; i < totalSamples; i++) {
            float t = (float)i / sampleRate;
            float envelope = Mathf.Exp(-t * 18f); // Fast decay
            float pitchDrop = Mathf.Max(0.5f, 1.0f - (t / duration) * 0.6f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * (freq * pitchDrop) * t) * envelope;
        }

        AudioClip clip = AudioClip.Create($"DefaultVoice_{freq}", totalSamples, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
