using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ImageEditor : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler {
    [Header("UI")]
    [SerializeField] private RawImage uploadedImage;
    [SerializeField] private RectTransform imageArea;
    [SerializeField] private RectTransform cropFrame;

    [Header("Capture")]
    [SerializeField] private RawImage captureImage;
    [SerializeField] private RenderTexture captureRenderTexture;
    [SerializeField] private Camera captureCamera;

    [Header("Settings")]
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float minZoom = 0.5f;
    [SerializeField] private float maxZoom = 3f;



    private RectTransform imageRect;

    private float currentZoom = 1f;
    private float currentRotation = 0f;

    private void Awake() {
        imageRect = uploadedImage.rectTransform;
    }

    public void SetImage(Texture2D texture) {
        uploadedImage.texture = texture;

        ResetImage();
        FitImage(texture);
    }

    private void FitImage(Texture2D texture) {
        float areaWidth = imageArea.rect.width;
        float areaHeight = imageArea.rect.height;

        float imageWidth = texture.width;
        float imageHeight = texture.height;

        float scaleX = areaWidth / imageWidth;
        float scaleY = areaHeight / imageHeight;

        // Cover the complete editor area
        float scale = Mathf.Max(scaleX, scaleY);

        imageRect.sizeDelta = new Vector2(
            imageWidth * scale,
            imageHeight * scale
        );
    }

    // =========================
    // DRAG
    // =========================

    public void OnPointerDown(PointerEventData eventData) {
        Debug.Log("Image editor pointer down");
    }

    public void OnDrag(PointerEventData eventData) {
        imageRect.anchoredPosition += eventData.delta;
    }

    // =========================
    // ZOOM
    // =========================

    public void OnScroll(PointerEventData eventData) {
        float amount =
            eventData.scrollDelta.y * zoomSpeed;

        currentZoom += amount;

        currentZoom = Mathf.Clamp(
            currentZoom,
            minZoom,
            maxZoom
        );

        imageRect.localScale =
            Vector3.one * currentZoom;
    }

    // =========================
    // ROTATION
    // =========================

    public void RotateLeft() {
        currentRotation -= 90f;

        imageRect.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                currentRotation
            );

        Debug.Log(
            "Rotation: " + currentRotation
        );
    }

    public void RotateRight() {
        currentRotation += 90f;

        imageRect.localRotation =
            Quaternion.Euler(
                0f,
                0f,
                currentRotation
            );

        Debug.Log(
            "Rotation: " + currentRotation
        );
    }

    // =========================
    // RESET
    // =========================

    public void ResetImage() {
        currentZoom = 1f;
        currentRotation = 0f;

        imageRect.anchoredPosition =
            Vector2.zero;

        imageRect.localScale =
            Vector3.one;

        imageRect.localRotation =
            Quaternion.identity;
    }

    // =========================
    // CREATE FINAL TEXTURE
    // =========================

    public Texture2D GenerateFinalTexture() {
        Canvas.ForceUpdateCanvases();

        bool cropWasActive = cropFrame.gameObject.activeSelf;
        cropFrame.gameObject.SetActive(false);

        Vector3[] corners = new Vector3[4];
        cropFrame.GetWorldCorners(corners);

        Canvas canvas = imageArea.GetComponentInParent<Canvas>();

        Camera cam = null;

        if (canvas.renderMode != RenderMode.ScreenSpaceOverlay) {
            cam = canvas.worldCamera;
        }

        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);

        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);


        float x = bottomLeft.x;
        float y = bottomLeft.y;

        float width = topRight.x - bottomLeft.x;
        float height = topRight.y - bottomLeft.y;

        x = Mathf.Clamp(x, 0, Screen.width - 1);
        y = Mathf.Clamp(y, 0, Screen.height - 1);

        width = Mathf.Clamp(width, 0, Screen.width - x);

        height = Mathf.Clamp(height, 1, Screen.height - y);

        Texture2D result = new Texture2D(Mathf.RoundToInt(width), Mathf.RoundToInt(height), TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(x, y, width, height), 0, 0);

        result.Apply();

        cropFrame.gameObject.SetActive(cropWasActive);

        Debug.Log("Captured: " + result.width + " x " + result.height);

        return result;

    }

    public void TestCaptureSetup() {
        if (captureImage == null) {
            Debug.LogError("Capture Image is not assigned!");
            return;
        }

        if (captureRenderTexture == null) {
            Debug.LogError("Capture Render Texture is not assigned!");
            return;
        }

        if (captureCamera == null) {
            Debug.LogError("Capture Camera is not assigned!");
            return;
        }

        // Use the same uploaded texture
        captureImage.texture = uploadedImage.texture;

        Debug.Log("Capture setup connected successfully!");
    }

    public void CopyEditorTransformToCapture() {
        if (uploadedImage == null) {
            Debug.LogError("UploadedImage is missing!");
            return;
        }

        if (captureImage == null) {
            Debug.Log("CaptureImage is missing");
            return;
        }

        captureImage.texture = uploadedImage.texture;

        captureImage.rectTransform.localRotation = uploadedImage.rectTransform.localRotation;
        captureImage.rectTransform.localScale = uploadedImage.rectTransform.localScale;
        captureImage.rectTransform.anchoredPosition = uploadedImage.rectTransform.anchoredPosition;

        Debug.Log("Editor transform copied to capture image");
    }
}