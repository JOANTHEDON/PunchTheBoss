using System.IO;
using SFB;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Controls the full UI onboarding flow:
///   1. Upload Button  → click → Image Editor Panel opens
///   2. Apply Button   → click → Boss Voice Menu Panel opens
///   3. Start Button   → click → Weapon Action Panel opens (gameplay begins)
/// </summary>
public class BossFaceUploader : MonoBehaviour {

    // ── Inspector references ────────────────────────────────────────────────
    [Header("Boss Face")]
    [SerializeField] private Renderer faceRenderer;

    [Header("Step 1 – Upload")]
    [SerializeField] private GameObject uploadButton;

    [Header("Step 2 – Image Editor")]
    [SerializeField] private GameObject imageEditorPanel;
    [SerializeField] private ImageEditor imageEditor;

    [Header("Step 3 – Voice Menu")]
    [SerializeField] private GameObject voiceMenuPanel;

    [Header("Step 4 – Weapons")]
    [SerializeField] private GameObject weaponActionPanel;
    [SerializeField] private WeaponSelectionUI weaponSelectionUI;

    // ── Private state ───────────────────────────────────────────────────────
    private Texture2D currentFaceTexture;

    // ═══════════════════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════════════════

    private void Awake() {
        // Enforce correct initial states immediately — before any Start() fires
        SetInitialState();
    }

    private void Start() {
        // Auto-resolve any missing scene references
        AutoResolveReferences();
        // Wire button listeners
        WireButtons();
        // Re-enforce after resolution (in case something was null in Awake)
        SetInitialState();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // INITIAL STATE  —  Only Upload Button is visible
    // ═══════════════════════════════════════════════════════════════════════

    private void SetInitialState() {
        SetActive(uploadButton,      true);
        SetActive(imageEditorPanel,  false);
        SetActive(voiceMenuPanel,    false);
        SetActive(weaponActionPanel, false);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AUTO-RESOLVE REFERENCES  (fallback if not wired in Inspector)
    // ═══════════════════════════════════════════════════════════════════════

    private void AutoResolveReferences() {
        // Face renderer
        if (faceRenderer == null) {
            GameObject faceGO = GameObject.Find("face");
            if (faceGO != null) faceRenderer = faceGO.GetComponent<Renderer>();
            if (faceRenderer == null) faceRenderer = FindFirstObjectByType<SkinnedMeshRenderer>();
        }

        // Upload button
        if (uploadButton == null) {
            Button[] all = FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            foreach (var b in all) {
                string n = b.name.ToLower();
                if (n.Contains("upload") || n.Contains("select")) { uploadButton = b.gameObject; break; }
            }
        }

        // Image editor
        if (imageEditor == null)
            imageEditor = FindFirstObjectByType<ImageEditor>(FindObjectsInactive.Include);
        if (imageEditorPanel == null && imageEditor != null)
            imageEditorPanel = imageEditor.gameObject;
        if (imageEditorPanel == null)
            imageEditorPanel = FindInactiveByName("ImageEditorPanel");

        // Voice menu — search inactive objects (GameObject.Find misses inactive)
        if (voiceMenuPanel == null)
            voiceMenuPanel = FindInactiveByName("BossVoiceMenuPanel");

        // Weapon panel
        if (weaponActionPanel == null)
            weaponActionPanel = FindInactiveByName("WeaponActionPanel");
        if (weaponSelectionUI == null)
            weaponSelectionUI = FindFirstObjectByType<WeaponSelectionUI>(FindObjectsInactive.Include);

        // Ensure WeaponManager exists
        if (WeaponManager.Instance == null)
            new GameObject("WeaponManager", typeof(WeaponManager));

        // Ensure BossVoiceManager exists as its own active scene object (NOT on the panel)
        if (BossVoiceManager.Instance == null) {
            GameObject vmHost = new GameObject("BossVoiceManager", typeof(BossVoiceManager));
            // Give it the panel reference right away
            vmHost.GetComponent<BossVoiceManager>().SetVoiceMenuPanel(voiceMenuPanel);
        } else if (voiceMenuPanel != null) {
            BossVoiceManager.Instance.SetVoiceMenuPanel(voiceMenuPanel);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // BUTTON WIRING
    // ═══════════════════════════════════════════════════════════════════════

    private void WireButtons() {
        // Upload button
        if (uploadButton != null) {
            Button btn = uploadButton.GetComponent<Button>();
            if (btn != null) {
                btn.onClick.RemoveListener(OnUploadClicked);
                btn.onClick.AddListener(OnUploadClicked);
            }
        }

        // Apply / Save button inside the image editor panel
        if (imageEditorPanel != null) {
            BindBtn(imageEditorPanel, new[] { "ApplyButton","ApplyBtn","Btn_Apply","Apply","Save" }, OnApplyClicked);
            if (imageEditor != null) {
                BindBtn(imageEditorPanel, new[] { "RotateLeftButton","RotateLeft","Btn_RotateLeft" },  imageEditor.RotateLeft);
                BindBtn(imageEditorPanel, new[] { "RotateRightButton","RotateRight","Btn_RotateRight" }, imageEditor.RotateRight);
                BindBtn(imageEditorPanel, new[] { "ResetButton","ResetBtn","Btn_Reset" },               imageEditor.ResetImage);
            }
        }

        // Voice menu buttons are wired by BossVoiceManager itself
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STEP 1  —  Upload button clicked
    // ═══════════════════════════════════════════════════════════════════════

    private void OnUploadClicked() {
        var extensions = new[] { new ExtensionFilter("Image Files", "png", "jpg", "jpeg") };
        string[] paths = StandaloneFileBrowser.OpenFilePanel("Select Boss Face", "", extensions, false);

        if (paths == null || paths.Length == 0 || string.IsNullOrEmpty(paths[0])) {
            Debug.Log("[BossFaceUploader] No image selected.");
            return;
        }

        LoadAndShowEditor(paths[0]);
    }

    private void LoadAndShowEditor(string path) {
        if (!File.Exists(path)) {
            Debug.LogError($"[BossFaceUploader] File not found: {path}");
            return;
        }

        byte[] data = File.ReadAllBytes(path);
        Texture2D tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        if (!tex.LoadImage(data)) {
            Debug.LogError("[BossFaceUploader] Failed to decode image.");
            Destroy(tex);
            return;
        }
        tex.name = "UploadedBossFace";

        // Transition: Upload → Editor
        SetActive(uploadButton,      false);
        SetActive(imageEditorPanel,  true);
        SetActive(voiceMenuPanel,    false);
        SetActive(weaponActionPanel, false);

        if (imageEditor != null)
            imageEditor.SetImage(tex);

        Debug.Log($"[BossFaceUploader] Image loaded → Image Editor shown: {path}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STEP 2  —  Apply button clicked inside Image Editor
    // ═══════════════════════════════════════════════════════════════════════

    private void OnApplyClicked() {
        if (imageEditor != null)
            imageEditor.RequestFinalTexture(OnTextureReady);
    }

    private void OnTextureReady(Texture2D editedTexture) {
        if (editedTexture == null) {
            Debug.LogError("[BossFaceUploader] Capture returned null.");
            return;
        }

        // Update boss face texture
        if (currentFaceTexture != null) Destroy(currentFaceTexture);
        currentFaceTexture = editedTexture;

        if (faceRenderer != null)
            faceRenderer.material.mainTexture = editedTexture;

        // Transition: Editor → Voice Menu
        SetActive(uploadButton,      false);
        SetActive(imageEditorPanel,  false);
        SetActive(voiceMenuPanel,    false);   // BossVoiceManager.OpenVoiceMenu will show it
        SetActive(weaponActionPanel, false);

        Debug.Log("[BossFaceUploader] Texture applied → opening Voice Menu");

        // Open voice menu; the callback fires when the player clicks "Ready & Start!"
        BossVoiceManager vm = BossVoiceManager.Instance;
        if (vm == null) {
            Debug.LogError("[BossFaceUploader] BossVoiceManager.Instance is null! Creating one now.");
            GameObject host = new GameObject("BossVoiceManager", typeof(BossVoiceManager));
            vm = host.GetComponent<BossVoiceManager>();
            vm.SetVoiceMenuPanel(voiceMenuPanel);
        }

        vm.OpenVoiceMenu(OnVoiceMenuComplete);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // STEP 3  —  "Ready & Start!" clicked inside Voice Menu
    // ═══════════════════════════════════════════════════════════════════════

    private void OnVoiceMenuComplete() {
        // Transition: Voice Menu → Weapon Action Panel
        SetActive(uploadButton,      false);
        SetActive(imageEditorPanel,  false);
        SetActive(voiceMenuPanel,    false);
        SetActive(weaponActionPanel, true);

        if (weaponSelectionUI != null)
            weaponSelectionUI.ShowWeaponPanel(true);

        if (WeaponManager.Instance != null)
            WeaponManager.Instance.SelectWeapon(WeaponType.Glove);

        Debug.Log("[BossFaceUploader] Voice setup done → Weapons unlocked!");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // HELPERS
    // ═══════════════════════════════════════════════════════════════════════

    private static void SetActive(GameObject go, bool active) {
        if (go != null) go.SetActive(active);
    }

    /// <summary>Finds a GameObject by name even if it is inactive.</summary>
    private static GameObject FindInactiveByName(string name) {
        foreach (var rt in FindObjectsByType<RectTransform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (rt.name == name) return rt.gameObject;
        }
        // Also search non-UI objects
        foreach (var t in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)) {
            if (t.name == name) return t.gameObject;
        }
        return null;
    }

    /// <summary>Finds a Button in panel by name and binds an action to it.</summary>
    private static void BindBtn(GameObject panel, string[] names, UnityEngine.Events.UnityAction action) {
        Button[] buttons = panel.GetComponentsInChildren<Button>(true);
        foreach (var b in buttons) {
            foreach (string n in names) {
                if (b.name.Equals(n, System.StringComparison.OrdinalIgnoreCase) || b.name.Contains(n)) {
                    b.onClick.RemoveListener(action);
                    b.onClick.AddListener(action);
                    return;
                }
            }
        }
    }
}