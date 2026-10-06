using System.IO;
using SFB;
using UnityEngine;

public class BossFaceUploader : MonoBehaviour {

    [Header("Boss Face — Sitting Idle Character")]
    [Tooltip("The SkinnedMeshRenderer on the 'face' object of the Sitting Idle boss character.")]
    [SerializeField] private Renderer faceRenderer;

    [Header("Image Editor")]
    [SerializeField] private ImageEditor imageEditor;
    [SerializeField] private GameObject  imageEditorPanel;

    [Header("Attack / Weapon UI")]
    [SerializeField] private WeaponSelectionUI weaponSelectionUI;
    [SerializeField] private GameObject uploadButton;

    // Tracks the currently loaded / applied texture so we can destroy it when replaced
    private Texture2D currentFaceTexture;

    private void Start() {
        // Ensure WeaponManager exists in scene
        if (FindFirstObjectByType<WeaponManager>() == null) {
            GameObject wmObj = new GameObject("WeaponManager", typeof(WeaponManager));
        }

        // If weaponSelectionUI isn't assigned, find or create it on the Canvas
        if (weaponSelectionUI == null) {
            weaponSelectionUI = FindFirstObjectByType<WeaponSelectionUI>();
            if (weaponSelectionUI == null) {
                Canvas mainCanvas = FindFirstObjectByType<Canvas>();
                if (mainCanvas != null) {
                    GameObject uiObj = new GameObject("WeaponUI", typeof(WeaponSelectionUI));
                    uiObj.transform.SetParent(mainCanvas.transform, false);
                    weaponSelectionUI = uiObj.GetComponent<WeaponSelectionUI>();
                    weaponSelectionUI.BuildRuntimeUI(mainCanvas.transform);
                }
            }
        }
    }

    // ===============================================================
    // STEP 1 — Open file picker and load the image into the editor
    // ===============================================================

    public void SelectFaceImage() {
        var extensions = new[] {
            new ExtensionFilter("Image Files", "png", "jpg", "jpeg")
        };

        string[] paths = StandaloneFileBrowser.OpenFilePanel(
            "Select Boss Face",
            "",
            extensions,
            false
        );

        if (paths.Length == 0 || string.IsNullOrEmpty(paths[0])) {
            Debug.Log("[BossFaceUploader] No image selected.");
            return;
        }

        LoadFaceImage(paths[0]);
    }

    private void LoadFaceImage(string path) {
        if (!File.Exists(path)) {
            Debug.LogError($"[BossFaceUploader] File not found: {path}");
            return;
        }

        byte[] imageData = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!texture.LoadImage(imageData)) {
            Debug.LogError("[BossFaceUploader] Failed to decode image.");
            Destroy(texture);
            return;
        }

        texture.name = "UploadedBossFace";

        // Hide upload button while editing
        if (uploadButton != null) {
            uploadButton.SetActive(false);
        }

        // Hand the texture to the editor and show the panel
        if (imageEditor != null) {
            imageEditor.SetImage(texture);
        }

        if (imageEditorPanel != null) {
            imageEditorPanel.SetActive(true);
        }

        Debug.Log($"[BossFaceUploader] Image loaded: {path}");
    }

    // ===============================================================
    // STEP 2 — Apply button: capture cropped image & activate 5 weapons
    // ===============================================================

    public void ApplyImage() {
        if (imageEditor != null) {
            imageEditor.RequestFinalTexture(OnTextureReady);
        }
    }

    private void OnTextureReady(Texture2D editedTexture) {
        if (editedTexture == null) {
            Debug.LogError("[BossFaceUploader] Capture returned null texture.");
            return;
        }

        if (currentFaceTexture != null) {
            Destroy(currentFaceTexture);
        }

        currentFaceTexture = editedTexture;

        // Apply texture to the boss face
        if (faceRenderer != null) {
            faceRenderer.material.mainTexture = editedTexture;
            Debug.Log("[BossFaceUploader] Boss face texture applied successfully!");
        } else {
            Debug.LogError("[BossFaceUploader] faceRenderer is not assigned!");
        }

        // Hide editor panel
        if (imageEditorPanel != null) {
            imageEditorPanel.SetActive(false);
        }

        // Activate the 5 weapon buttons for gameplay
        if (weaponSelectionUI != null) {
            weaponSelectionUI.ShowWeaponPanel(true);
        }

        Debug.Log("[BossFaceUploader] Weapons unlocked: Glove, Slap, Coffee, Chappal, Super!");
    }
}