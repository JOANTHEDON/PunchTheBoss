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

    // Tracks the currently loaded / applied texture so we can destroy it when replaced
    private Texture2D currentFaceTexture;

    // ===============================================================
    // STEP 1 — Open file picker and load the image into the editor
    // ===============================================================

    /// <summary>
    /// Called by the "Upload Boss Face" button.
    /// Opens a native OS file picker, loads the chosen image, and shows the editor panel.
    /// </summary>
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

        // User cancelled the dialog
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

        byte[]    imageData = File.ReadAllBytes(path);
        Texture2D texture   = new Texture2D(2, 2, TextureFormat.RGBA32, false);

        if (!texture.LoadImage(imageData)) {
            Debug.LogError("[BossFaceUploader] Failed to decode image.");
            Destroy(texture);
            return;
        }

        texture.name = "UploadedBossFace";

        // Hand the texture to the editor and show the panel — player now edits
        imageEditor.SetImage(texture);
        imageEditorPanel.SetActive(true);

        Debug.Log($"[BossFaceUploader] Image loaded: {path}");
    }

    // ===============================================================
    // STEP 2 — Apply button: capture the edited/cropped image and
    //           set it as the texture on the boss's face renderer
    // ===============================================================

    /// <summary>
    /// Called by the "Apply" button in the editor panel.
    /// Captures the cropped image (waiting one frame for a clean render)
    /// then applies it to the boss face renderer on the Sitting Idle character.
    /// </summary>
    public void ApplyImage() {
        // Ask the editor to capture the crop. It waits for end-of-frame
        // internally so ReadPixels sees a frame with no overlay UI.
        imageEditor.RequestFinalTexture(OnTextureReady);
    }

    private void OnTextureReady(Texture2D editedTexture) {
        if (editedTexture == null) {
            Debug.LogError("[BossFaceUploader] Capture returned null texture.");
            return;
        }

        // Destroy the previous texture to avoid memory leaks
        if (currentFaceTexture != null) {
            Destroy(currentFaceTexture);
        }

        currentFaceTexture = editedTexture;

        // Apply to the face mesh on the Sitting Idle boss character
        if (faceRenderer != null) {
            faceRenderer.material.mainTexture = editedTexture;
            Debug.Log("[BossFaceUploader] Boss face texture applied successfully!");
        } else {
            Debug.LogError("[BossFaceUploader] faceRenderer is not assigned! " +
                           "Please assign the face SkinnedMeshRenderer from the Sitting Idle character.");
        }

        // Hide the editor panel — player is back in the main scene
        imageEditorPanel.SetActive(false);
    }
}