using System.IO;
using SFB;
using UnityEngine;

public class BossFaceUploader : MonoBehaviour {
    [Header("Boss Face")]
    [SerializeField] private Renderer faceRenderer;

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

        faceRenderer.material.mainTexture = texture;

        Debug.Log("Boss face updated successfully!");
    }



}