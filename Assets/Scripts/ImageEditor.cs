using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Controls the image editing panel:
/// - Drag image to reposition
/// - Scroll to zoom in / out
/// - Rotate 90 degrees left / right
/// - Interactive Crop Frame resizing/dragging handles
/// - Accurate screen-space capture on Apply without UI artifacts
/// </summary>
public class ImageEditor : MonoBehaviour, IPointerDownHandler, IDragHandler, IScrollHandler {

    [Header("UI References")]
    [SerializeField] private RawImage uploadedImage;
    [SerializeField] private RectTransform imageArea;
    [SerializeField] private RectTransform cropFrame;

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 0.1f;
    [SerializeField] private float minZoom = 0.2f;
    [SerializeField] private float maxZoom = 5f;

    [Header("Crop Frame Constraints")]
    [SerializeField] private float minCropSize = 100f;

    private RectTransform imageRect;
    private float currentZoom = 1f;
    private float currentRotation = 0f;

    // Interactive crop resize & drag state
    public enum CropDragMode {
        None,
        MoveCrop,
        TopLeft,
        TopRight,
        BottomLeft,
        BottomRight,
        EdgeLeft,
        EdgeRight,
        EdgeTop,
        EdgeBottom
    }

    private CropDragMode activeCropMode = CropDragMode.None;
    private const float CORNER_HIT_RADIUS = 30f;
    private const float EDGE_HIT_THICKNESS = 20f;

    private void Awake() {
        if (uploadedImage != null) {
            imageRect = uploadedImage.rectTransform;
        }
    }

    // ===============================================================
    // INITIALIZATION / SETUP
    // ===============================================================

    public void SetImage(Texture2D texture) {
        if (uploadedImage == null) return;

        uploadedImage.texture = texture;
        ResetImage();
        FitImage(texture);
        ResetCropFrameToDefault();
    }

    private void FitImage(Texture2D texture) {
        Canvas.ForceUpdateCanvases();

        float areaWidth = imageArea.rect.width > 0 ? imageArea.rect.width : 700f;
        float areaHeight = imageArea.rect.height > 0 ? imageArea.rect.height : 500f;

        float imgWidth = texture.width > 0 ? texture.width : 500f;
        float imgHeight = texture.height > 0 ? texture.height : 500f;

        // Fit image so it is fully visible or cleanly covering the editor area
        float scaleX = areaWidth / imgWidth;
        float scaleY = areaHeight / imgHeight;
        float scale = Mathf.Min(scaleX, scaleY);

        imageRect.sizeDelta = new Vector2(imgWidth * scale, imgHeight * scale);
        imageRect.anchoredPosition = Vector2.zero;
    }

    public void ResetCropFrameToDefault() {
        if (cropFrame == null) return;

        float areaWidth = imageArea.rect.width > 0 ? imageArea.rect.width : 700f;
        float areaHeight = imageArea.rect.height > 0 ? imageArea.rect.height : 500f;

        // Set default crop frame to a square centered in image area
        float defaultSize = Mathf.Min(areaWidth, areaHeight) * 0.75f;
        cropFrame.anchoredPosition = Vector2.zero;
        cropFrame.sizeDelta = new Vector2(defaultSize, defaultSize);
    }

    // ===============================================================
    // MOUSE POINTER / DRAG / RESIZE LOGIC
    // ===============================================================

    public void OnPointerDown(PointerEventData eventData) {
        activeCropMode = DetermineCropDragMode(eventData.position);
    }

    public void OnDrag(PointerEventData eventData) {
        if (cropFrame == null || activeCropMode == CropDragMode.None) {
            // Drag the image if not interacting with crop frame handles
            if (imageRect != null) {
                imageRect.anchoredPosition += eventData.delta;
            }
            return;
        }

        HandleCropResizeOrMove(eventData);
    }

    private CropDragMode DetermineCropDragMode(Vector2 screenPoint) {
        if (cropFrame == null) return CropDragMode.None;

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(cropFrame, screenPoint, cam, out Vector2 localPoint)) {
            return CropDragMode.None;
        }

        Vector2 size = cropFrame.rect.size;
        float halfW = size.x * 0.5f;
        float halfH = size.y * 0.5f;

        // Corners
        bool nearLeft = Mathf.Abs(localPoint.x - (-halfW)) <= CORNER_HIT_RADIUS;
        bool nearRight = Mathf.Abs(localPoint.x - halfW) <= CORNER_HIT_RADIUS;
        bool nearBottom = Mathf.Abs(localPoint.y - (-halfH)) <= CORNER_HIT_RADIUS;
        bool nearTop = Mathf.Abs(localPoint.y - halfH) <= CORNER_HIT_RADIUS;

        if (nearLeft && nearTop) return CropDragMode.TopLeft;
        if (nearRight && nearTop) return CropDragMode.TopRight;
        if (nearLeft && nearBottom) return CropDragMode.BottomLeft;
        if (nearRight && nearBottom) return CropDragMode.BottomRight;

        // Edges
        if (nearLeft && Mathf.Abs(localPoint.y) <= halfH) return CropDragMode.EdgeLeft;
        if (nearRight && Mathf.Abs(localPoint.y) <= halfH) return CropDragMode.EdgeRight;
        if (nearTop && Mathf.Abs(localPoint.x) <= halfW) return CropDragMode.EdgeTop;
        if (nearBottom && Mathf.Abs(localPoint.x) <= halfW) return CropDragMode.EdgeBottom;

        // Inside crop frame -> drag the whole crop box
        if (Mathf.Abs(localPoint.x) <= halfW && Mathf.Abs(localPoint.y) <= halfH) {
            return CropDragMode.MoveCrop;
        }

        return CropDragMode.None;
    }

    private void HandleCropResizeOrMove(PointerEventData eventData) {
        Canvas canvas = GetComponentInParent<Canvas>();
        float scaleFactor = canvas != null ? canvas.scaleFactor : 1f;
        Vector2 delta = eventData.delta / (scaleFactor > 0 ? scaleFactor : 1f);

        Vector2 size = cropFrame.sizeDelta;
        Vector2 pos = cropFrame.anchoredPosition;

        float maxW = imageArea.rect.width > 0 ? imageArea.rect.width : 1000f;
        float maxH = imageArea.rect.height > 0 ? imageArea.rect.height : 1000f;

        switch (activeCropMode) {
            case CropDragMode.MoveCrop:
                pos += delta;
                break;

            case CropDragMode.TopRight:
                size.x += delta.x;
                size.y += delta.y;
                pos.x += delta.x * 0.5f;
                pos.y += delta.y * 0.5f;
                break;

            case CropDragMode.TopLeft:
                size.x -= delta.x;
                size.y += delta.y;
                pos.x += delta.x * 0.5f;
                pos.y += delta.y * 0.5f;
                break;

            case CropDragMode.BottomRight:
                size.x += delta.x;
                size.y -= delta.y;
                pos.x += delta.x * 0.5f;
                pos.y += delta.y * 0.5f;
                break;

            case CropDragMode.BottomLeft:
                size.x -= delta.x;
                size.y -= delta.y;
                pos.x += delta.x * 0.5f;
                pos.y += delta.y * 0.5f;
                break;

            case CropDragMode.EdgeRight:
                size.x += delta.x;
                pos.x += delta.x * 0.5f;
                break;

            case CropDragMode.EdgeLeft:
                size.x -= delta.x;
                pos.x += delta.x * 0.5f;
                break;

            case CropDragMode.EdgeTop:
                size.y += delta.y;
                pos.y += delta.y * 0.5f;
                break;

            case CropDragMode.EdgeBottom:
                size.y -= delta.y;
                pos.y += delta.y * 0.5f;
                break;
        }

        // Clamp sizes
        size.x = Mathf.Clamp(size.x, minCropSize, maxW);
        size.y = Mathf.Clamp(size.y, minCropSize, maxH);

        // Clamp position within imageArea
        float limitX = (maxW - size.x) * 0.5f;
        float limitY = (maxH - size.y) * 0.5f;
        pos.x = Mathf.Clamp(pos.x, -limitX, limitX);
        pos.y = Mathf.Clamp(pos.y, -limitY, limitY);

        cropFrame.sizeDelta = size;
        cropFrame.anchoredPosition = pos;
    }

    // ===============================================================
    // ZOOM & ROTATE CONTROLS
    // ===============================================================

    public void OnScroll(PointerEventData eventData) {
        currentZoom += eventData.scrollDelta.y * zoomSpeed;
        currentZoom = Mathf.Clamp(currentZoom, minZoom, maxZoom);
        if (imageRect != null) {
            imageRect.localScale = Vector3.one * currentZoom;
        }
    }

    public void RotateLeft() {
        currentRotation += 90f;
        if (imageRect != null) {
            imageRect.localRotation = Quaternion.Euler(0f, 0f, currentRotation);
        }
    }

    public void RotateRight() {
        currentRotation -= 90f;
        if (imageRect != null) {
            imageRect.localRotation = Quaternion.Euler(0f, 0f, currentRotation);
        }
    }

    public void ResetImage() {
        currentZoom = 1f;
        currentRotation = 0f;

        if (imageRect != null) {
            imageRect.anchoredPosition = Vector2.zero;
            imageRect.localScale = Vector3.one;
            imageRect.localRotation = Quaternion.identity;
        }

        ResetCropFrameToDefault();
    }

    // ===============================================================
    // CAPTURE / CROP TO FINAL TEXTURE
    // ===============================================================

    /// <summary>
    /// Captures the cropped region defined by the cropFrame overlay without artifacts.
    /// </summary>
    public void RequestFinalTexture(Action<Texture2D> onDone) {
        StartCoroutine(CaptureRoutine(onDone));
    }

    private IEnumerator CaptureRoutine(Action<Texture2D> onDone) {
        // 1. Temporarily hide crop frame so the border isn't captured in the boss's face
        bool cropWasActive = cropFrame != null && cropFrame.gameObject.activeSelf;
        if (cropFrame != null) {
            cropFrame.gameObject.SetActive(false);
        }

        // 2. Wait until rendering for the current frame is complete
        yield return new WaitForEndOfFrame();

        // 3. Compute screen bounds of crop frame
        Vector3[] corners = new Vector3[4];
        cropFrame.GetWorldCorners(corners);

        Canvas canvas = GetComponentInParent<Canvas>();
        Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;

        Vector2 bottomLeft = RectTransformUtility.WorldToScreenPoint(cam, corners[0]);
        Vector2 topRight = RectTransformUtility.WorldToScreenPoint(cam, corners[2]);

        float x = Mathf.Clamp(bottomLeft.x, 0, Screen.width - 1);
        float y = Mathf.Clamp(bottomLeft.y, 0, Screen.height - 1);
        float width = Mathf.Clamp(topRight.x - bottomLeft.x, 1, Screen.width - x);
        float height = Mathf.Clamp(topRight.y - bottomLeft.y, 1, Screen.height - y);

        // 4. Capture the exact cropped pixels
        Texture2D result = new Texture2D(Mathf.RoundToInt(width), Mathf.RoundToInt(height), TextureFormat.RGBA32, false);
        result.ReadPixels(new Rect(x, y, width, height), 0, 0);
        result.Apply();

        // 5. Restore crop frame
        if (cropFrame != null) {
            cropFrame.gameObject.SetActive(cropWasActive);
        }

        Debug.Log($"[ImageEditor] Cropped and captured texture: {result.width}x{result.height}");
        onDone?.Invoke(result);
    }
}