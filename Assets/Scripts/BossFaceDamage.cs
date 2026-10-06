using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Facial region definitions for localized damage, bruising, and custom damage images.
/// </summary>
public enum FaceDamageZone {
    GeneralHead,
    LeftEye,
    RightEye,
    Mouth,
    Nose,
    Cheek
}

[System.Serializable]
public class FacialDamageSlot {
    [Tooltip("Label for this facial zone")]
    public string zoneName;
    public FaceDamageZone zone;

    [Tooltip("The Transform positioned where this facial feature is on the face")]
    public Transform targetTransform;

    [Tooltip("Optional UI Image component if using World Space / Overlay Canvas UI elements")]
    public Image targetImage;

    [Tooltip("Drop your custom damage image / sprite here! (e.g. black eye sprite, swollen lip, bloody nose, cut, etc.)")]
    public Sprite customDamageSprite;

    [Tooltip("Size/scale of the damage decal in world space")]
    public float decalScale = 0.08f;

    [HideInInspector] public SpriteRenderer activeRenderer;
    [HideInInspector] public int hitCount = 0;
}

/// <summary>
/// Handles progressive facial damage with customizable damage sprites/images:
/// - In the Unity Inspector, you can drop any Sprite/Image directly into:
///     • Left Eye Sprite (e.g. black eye, swollen eye)
///     • Right Eye Sprite
///     • Mouth Sprite (e.g. bleeding lip, bruised mouth)
///     • Nose Sprite (e.g. broken nose, blood)
///     • Forehead Sprite (e.g. bump, bruise, band-aid)
/// - If no image is assigned, it automatically falls back to procedural soft redness/bruising!
/// - You can drag each GameObject/Transform around in the Scene view to position it exactly on the face.
/// - Only reveals after several hits (e.g. 3-4 hits).
/// </summary>
public class BossFaceDamage : MonoBehaviour {

    public static BossFaceDamage Instance { get; private set; }

    [Header("Face Renderer Target")]
    [Tooltip("The SkinnedMeshRenderer on the face object")]
    [SerializeField] private Renderer faceRenderer;

    [Header("Custom Facial Damage Slots (Drop Sprites & Position Transforms here!)")]
    public FacialDamageSlot leftEye = new FacialDamageSlot {
        zoneName = "Left Eye",
        zone = FaceDamageZone.LeftEye,
        decalScale = 0.07f
    };

    public FacialDamageSlot rightEye = new FacialDamageSlot {
        zoneName = "Right Eye",
        zone = FaceDamageZone.RightEye,
        decalScale = 0.07f
    };

    public FacialDamageSlot mouth = new FacialDamageSlot {
        zoneName = "Mouth",
        zone = FaceDamageZone.Mouth,
        decalScale = 0.09f
    };

    public FacialDamageSlot leftCheek = new FacialDamageSlot {
        zoneName = "Left Cheek",
        zone = FaceDamageZone.Cheek,
        decalScale = 0.08f
    };

    public FacialDamageSlot rightCheek = new FacialDamageSlot {
        zoneName = "Right Cheek",
        zone = FaceDamageZone.Cheek,
        decalScale = 0.08f
    };

    public FacialDamageSlot nose = new FacialDamageSlot {
        zoneName = "Nose",
        zone = FaceDamageZone.Nose,
        decalScale = 0.06f
    };

    public FacialDamageSlot forehead = new FacialDamageSlot {
        zoneName = "Forehead / Head",
        zone = FaceDamageZone.GeneralHead,
        decalScale = 0.12f
    };

    [Header("Damage Settings")]
    [Tooltip("Number of hits on this part before the damage image/bruise starts appearing")]
    [SerializeField] private int hitsToStartShowing = 3;
    [Tooltip("Number of hits for full 100% opacity")]
    [SerializeField] private int hitsForMaxVisibility = 8;

    private List<FacialDamageSlot> allSlots = new List<FacialDamageSlot>();
    private Sprite defaultProceduralSprite;

    private Material faceMaterialInstance;
    private Color originalMaterialColor = Color.white;
    private Coroutine flashCoroutine;
    private int totalHeadHits = 0;

    private void Awake() {
        if (Instance == null) Instance = this;

        allSlots = new List<FacialDamageSlot> { leftEye, rightEye, mouth, leftCheek, rightCheek, nose, forehead };

        if (faceRenderer == null) {
            faceRenderer = GetComponentInChildren<SkinnedMeshRenderer>();
        }
    }

    private void Start() {
        if (faceRenderer == null) {
            GameObject faceObj = GameObject.Find("face");
            if (faceObj != null) {
                faceRenderer = faceObj.GetComponent<Renderer>();
            }
        }

        if (faceRenderer != null && faceRenderer.material != null) {
            faceMaterialInstance = faceRenderer.material;
            if (faceMaterialInstance.HasProperty("_BaseColor")) {
                originalMaterialColor = faceMaterialInstance.GetColor("_BaseColor");
            } else if (faceMaterialInstance.HasProperty("_Color")) {
                originalMaterialColor = faceMaterialInstance.GetColor("_Color");
            }
        }

        // Procedural soft gradient fallback sprite if no custom image was provided
        Texture2D defaultTex = GenerateSoftBruiseTexture();
        defaultProceduralSprite = Sprite.Create(defaultTex, new Rect(0, 0, defaultTex.width, defaultTex.height), new Vector2(0.5f, 0.5f), 100f);

        BindDamageCanvasUIElements();
        SetupAllSlotTransformsAndDecals();
    }

    /// <summary>
    /// Searches for DamageCanvas and binds UI Images (LeftEye, RightEye, LeftChick, RightChick, Mouth)
    /// </summary>
    private void BindDamageCanvasUIElements() {
        GameObject damageCanvas = GameObject.Find("DamageCanvas");
        if (damageCanvas == null) {
            Canvas[] canvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            foreach (var c in canvases) {
                if (c.name.Contains("DamageCanvas")) {
                    damageCanvas = c.gameObject;
                    break;
                }
            }
        }

        if (damageCanvas != null) {
            BindSlotUI(leftEye, damageCanvas, "LeftEye");
            BindSlotUI(rightEye, damageCanvas, "RightEye");
            BindSlotUI(leftCheek, damageCanvas, "LeftChick", "LeftCheek");
            BindSlotUI(rightCheek, damageCanvas, "RightChick", "RightCheek");
            BindSlotUI(mouth, damageCanvas, "Mouth");
        }
    }

    private void BindSlotUI(FacialDamageSlot slot, GameObject canvasObj, params string[] possibleNames) {
        if (slot.targetImage != null) {
            InitializeUIElement(slot.targetImage);
            return;
        }

        foreach (string name in possibleNames) {
            Transform t = canvasObj.transform.Find(name);
            if (t != null) {
                Image img = t.GetComponent<Image>();
                if (img != null) {
                    slot.targetImage = img;
                    if (slot.targetTransform == null) {
                        slot.targetTransform = t;
                    }
                    InitializeUIElement(img);
                    break;
                }
            }
        }
    }

    private void InitializeUIElement(Image img) {
        if (img == null) return;
        img.gameObject.SetActive(true);
        Color c = img.color;
        c.a = 0f; // Start hidden, fade in on hit
        img.color = c;
    }

    /// <summary>
    /// Creates or attaches visible GameObject anchors for each facial feature
    /// so you can easily move them or see them in the Unity Hierarchy / Scene view!
    /// </summary>
    public void SetupAllSlotTransformsAndDecals() {
        Transform headAnchor = transform;
        if (faceRenderer != null) {
            headAnchor = faceRenderer.transform;
        }

        SetupSlot(leftEye, headAnchor, "DamageTarget_LeftEye", new Vector3(-0.045f, 0.05f, 0.08f));
        SetupSlot(rightEye, headAnchor, "DamageTarget_RightEye", new Vector3(0.045f, 0.05f, 0.08f));
        SetupSlot(leftCheek, headAnchor, "DamageTarget_LeftCheek", new Vector3(-0.05f, 0.0f, 0.08f));
        SetupSlot(rightCheek, headAnchor, "DamageTarget_RightCheek", new Vector3(0.05f, 0.0f, 0.08f));
        SetupSlot(nose, headAnchor, "DamageTarget_Nose", new Vector3(0f, 0.015f, 0.09f));
        SetupSlot(mouth, headAnchor, "DamageTarget_Mouth", new Vector3(0f, -0.045f, 0.08f));
        SetupSlot(forehead, headAnchor, "DamageTarget_Forehead", new Vector3(0f, 0.11f, 0.07f));
    }

    private void SetupSlot(FacialDamageSlot slot, Transform parent, string anchorName, Vector3 defaultOffset) {
        if (slot.targetTransform == null) {
            Transform found = parent.Find(anchorName);
            if (found == null) {
                GameObject anchorObj = new GameObject(anchorName);
                anchorObj.transform.SetParent(parent, false);
                anchorObj.transform.localPosition = defaultOffset;
                anchorObj.transform.localRotation = Quaternion.identity;
                found = anchorObj.transform;
            }
            slot.targetTransform = found;
        }

        if (slot.targetImage != null) return; // UI Image handles rendering for this slot

        // Create the damage decal child object
        string decalName = "DecalVisual_" + slot.zone;
        Transform decalChild = slot.targetTransform.Find(decalName);
        GameObject decalObj;

        if (decalChild == null) {
            decalObj = new GameObject(decalName);
            decalObj.transform.SetParent(slot.targetTransform, false);
            decalObj.transform.localPosition = Vector3.forward * 0.015f; // slightly proud of face
            decalObj.transform.localRotation = Quaternion.identity;
            decalObj.transform.localScale = Vector3.one * slot.decalScale;
        } else {
            decalObj = decalChild.gameObject;
        }

        SpriteRenderer sr = decalObj.GetComponent<SpriteRenderer>();
        if (sr == null) {
            sr = decalObj.AddComponent<SpriteRenderer>();
        }

        // Use custom sprite if assigned by user, otherwise procedural bruise
        sr.sprite = slot.customDamageSprite != null ? slot.customDamageSprite : defaultProceduralSprite;
        sr.color = slot.customDamageSprite != null
            ? new Color(1f, 1f, 1f, 0f) // Custom sprite starts transparent
            : new Color(0.85f, 0.1f, 0.15f, 0f); // Default redness starts transparent
        sr.sortingOrder = 5;

        slot.activeRenderer = sr;
    }

    /// <summary>
    /// Registers a hit at world position, finding the closest facial feature
    /// and progressively revealing the damage image/bruise after several hits!
    /// </summary>
    public void RegisterDamageAtPoint(Vector3 worldHitPoint, WeaponType weapon) {
        totalHeadHits++;

        FacialDamageSlot closestSlot = GetClosestSlot(worldHitPoint);
        if (closestSlot == null) return;

        closestSlot.hitCount++;
        Debug.Log($"[BossFaceDamage] Hit {closestSlot.zoneName}! Count on part: {closestSlot.hitCount}");

        // Reveal damage after 'hitsToStartShowing'
        if (closestSlot.hitCount >= hitsToStartShowing) {
            float progress = Mathf.InverseLerp(hitsToStartShowing, hitsForMaxVisibility, closestSlot.hitCount);

            Color targetColor;
            if (closestSlot.targetImage != null || closestSlot.customDamageSprite != null) {
                // Fade in alpha cleanly
                float alpha = Mathf.Lerp(0.4f, 1f, progress);
                targetColor = new Color(1f, 1f, 1f, alpha);
            } else {
                // If using default procedural redness: intensify red/purple tone
                float alpha = Mathf.Lerp(0.35f, 0.85f, progress);
                targetColor = Color.Lerp(new Color(0.9f, 0.2f, 0.2f, alpha), new Color(0.7f, 0.05f, 0.15f, alpha), progress);
            }

            if (closestSlot.targetImage != null) {
                StartCoroutine(FadeUIImageColor(closestSlot.targetImage, targetColor));
            } else if (closestSlot.activeRenderer != null) {
                StartCoroutine(FadeDecalColor(closestSlot.activeRenderer, targetColor));
            }
        }

        // Brief impact flash for juice
        if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(HitFlashRoutine());
    }

    private FacialDamageSlot GetClosestSlot(Vector3 hitPoint) {
        FacialDamageSlot best = null;
        float minDst = float.MaxValue;

        foreach (var slot in allSlots) {
            if (slot.targetTransform == null && slot.targetImage == null) continue;
            Vector3 slotPos = slot.targetTransform != null ? slot.targetTransform.position : transform.position;
            float d = Vector3.Distance(slotPos, hitPoint);
            if (d < minDst) {
                minDst = d;
                best = slot;
            }
        }

        return best ?? forehead;
    }

    private IEnumerator FadeUIImageColor(Image img, Color target) {
        if (img == null) yield break;
        Color start = img.color;
        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            if (img != null) {
                img.color = Color.Lerp(start, target, elapsed / duration);
            }
            yield return null;
        }

        if (img != null) img.color = target;
    }

    private IEnumerator FadeDecalColor(SpriteRenderer sr, Color target) {
        if (sr == null) yield break;
        Color start = sr.color;
        float elapsed = 0f;
        float duration = 0.35f;

        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            if (sr != null) {
                sr.color = Color.Lerp(start, target, elapsed / duration);
            }
            yield return null;
        }

        if (sr != null) sr.color = target;
    }

    private IEnumerator HitFlashRoutine() {
        if (faceMaterialInstance == null) yield break;

        Color currentBase = faceMaterialInstance.HasProperty("_BaseColor")
            ? faceMaterialInstance.GetColor("_BaseColor")
            : (faceMaterialInstance.HasProperty("_Color") ? faceMaterialInstance.GetColor("_Color") : Color.white);

        Color flashColor = new Color(1f, 0.45f, 0.45f, 1f);
        SetMaterialColor(flashColor);

        yield return new WaitForSeconds(0.08f);

        SetMaterialColor(currentBase);
    }

    private void SetMaterialColor(Color c) {
        if (faceMaterialInstance == null) return;
        if (faceMaterialInstance.HasProperty("_BaseColor")) {
            faceMaterialInstance.SetColor("_BaseColor", c);
        } else if (faceMaterialInstance.HasProperty("_Color")) {
            faceMaterialInstance.SetColor("_Color", c);
        }
    }

    private Texture2D GenerateSoftBruiseTexture() {
        int res = 64;
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGBA32, false);
        Vector2 center = new Vector2(res * 0.5f, res * 0.5f);
        float radius = res * 0.5f;

        for (int y = 0; y < res; y++) {
            for (int x = 0; x < res; x++) {
                float dist = Vector2.Distance(new Vector2(x, y), center);
                float norm = Mathf.Clamp01(dist / radius);
                float alpha = Mathf.Pow(1f - norm, 2f);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }
        tex.Apply();
        return tex;
    }
}
