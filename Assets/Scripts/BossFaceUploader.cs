using System.Collections;
using System.IO;
using SFB;
using UnityEngine;

public class BossFaceUploader : MonoBehaviour {
    [Header("Boss Face")]
    [SerializeField] private Renderer faceRenderer;

    [Header("Image Editor")]
    [SerializeField] private ImageEditor imageEditor;
    [SerializeField] private GameObject imageEditorPanel;


    private Texture2D currentFaceTexture;

    public void SelectFaceImage() {
        var extensions = new[] {
            new ExtensionFilter(
                    "Image Files",
                    "png",
                    "jpg",
                    "jpeg"
                )
        };

        string[] paths = StandaloneFileBrowser.OpenFilePanel(
        "Select Boss Face",
        "",
        extensions,
        false
        );

        //User cancelled
        if (paths.Length == 0) {
            Debug.Log("No Image Selected.");
            return;
        }

        LoadFaceImage(paths[0]);

    }

    private void LoadFaceImage(string path) {
        Debug.Log("Selected Image :" + path);

        if (!File.Exists(path)) {
            Debug.LogError("File does not exist :" + path);
            return;
        }

        byte[] imageData = File.ReadAllBytes(path);

        Texture2D texture = new Texture2D(
            2,
            2,
            TextureFormat.RGBA32,
            false
            );

        if (!texture.LoadImage(imageData)) {
            Debug.LogError("Failed to load image");
            Destroy(texture);
            return;
        }

        texture.name = "UploadedBossface";

        if (currentFaceTexture != null) {
            Destroy(currentFaceTexture);
        }

        currentFaceTexture = texture;

        imageEditor.SetImage(texture);

        imageEditorPanel.SetActive(true);
    }

    public void ApplyImage() {
        Texture2D editedTexture = imageEditor.GenerateFinalTexture();

        if (editedTexture == null) {
            Debug.LogError("Could not generate edited texture.");
            return;
        }

        if (currentFaceTexture != null) {
            Destroy(currentFaceTexture);
        }

        currentFaceTexture = editedTexture;

        faceRenderer.material.mainTexture = editedTexture;

        imageEditorPanel.SetActive(false);

        Debug.Log("Edited face successfully applied!");
    }

    private IEnumerator ApplyImageRoutine() {
        yield return new WaitForEndOfFrame();

        Texture2D editedTexture = imageEditor.GenerateFinalTexture();

        if (editedTexture == null) {
            Debug.LogError("Failed to capture edited image");
            yield break;
        }

        if (currentFaceTexture != null) {
            Destroy(currentFaceTexture);
        }

        currentFaceTexture = editedTexture;

        faceRenderer.material.mainTexture = editedTexture;
        imageEditorPanel.SetActive(false);
        Debug.Log("Eddited face successfully applied");
    }

}