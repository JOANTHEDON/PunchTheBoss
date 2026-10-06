using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Weapon types that the player can select to hit the boss.
/// </summary>
public enum WeaponType {
    None,
    Glove,
    Slap,
    Coffee,
    Chappal,
    Super
}

/// <summary>
/// Handles selecting weapons, following mouse pointer / appearing,
/// clicking on the boss to hit, triggering animations / hit reactions / audio / effects,
/// and delegating localized progressive facial redness / bruising.
/// </summary>
public class WeaponManager : MonoBehaviour {

    public static WeaponManager Instance { get; private set; }

    [Header("Current Selection")]
    [SerializeField] private WeaponType currentWeapon = WeaponType.Glove;

    [Header("Boss Target References")]
    [Tooltip("Target animator on the Sitting Idle boss character")]
    [SerializeField] private Animator bossAnimator;
    [Tooltip("Head transform or face object of the boss")]
    [SerializeField] private Transform bossHeadTransform;
    [Tooltip("Main camera for raycasting")]
    [SerializeField] private Camera mainCamera;

    [Header("Visual Floating Icons / Reticles (Optional 2D/3D indicators)")]
    [SerializeField] private RectTransform weaponCursorUI;
    [SerializeField] private Canvas canvas;

    private BossRagdollHead bossRagdoll;
    private BossFaceDamage bossFaceDamage;

    // Events
    public event Action<WeaponType> OnWeaponChanged;
    public event Action<WeaponType, Vector3> OnBossHit;

    private void Awake() {
        if (Instance == null) {
            Instance = this;
        } else {
            Destroy(gameObject);
            return;
        }

        if (mainCamera == null) {
            mainCamera = Camera.main;
        }

        AutoFindBossReferences();
    }

    private void Start() {
        AutoFindBossReferences();
        EnsureBossComponents();
    }

    private void Update() {
        // Update custom cursor position if weapon active
        if (weaponCursorUI != null && currentWeapon != WeaponType.None) {
            UpdateCursorPosition();
        }

        // Left click to strike
        if (Input.GetMouseButtonDown(0)) {
            // Avoid triggering attack if clicking on UI buttons
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject()) {
                return;
            }

            if (currentWeapon != WeaponType.None) {
                TryStrikeBoss(Input.mousePosition);
            }
        }
    }

    public void SelectWeapon(WeaponType weapon) {
        currentWeapon = weapon;
        Debug.Log($"[WeaponManager] Weapon selected: {weapon}");

        if (weaponCursorUI != null) {
            weaponCursorUI.gameObject.SetActive(weapon != WeaponType.None);
        }

        OnWeaponChanged?.Invoke(weapon);
    }

    public WeaponType GetCurrentWeapon() => currentWeapon;

    private void UpdateCursorPosition() {
        if (weaponCursorUI == null) return;

        Vector2 mousePos = Input.mousePosition;
        if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceOverlay) {
            weaponCursorUI.position = mousePos;
        } else {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvas.transform as RectTransform,
                mousePos,
                mainCamera,
                out Vector2 localPos
            );
            weaponCursorUI.localPosition = localPos;
        }
    }

    /// <summary>
    /// Performs raycasting against boss colliders to execute a localized hit.
    /// </summary>
    private void TryStrikeBoss(Vector3 screenPos) {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        Ray ray = mainCamera.ScreenPointToRay(screenPos);
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, 100f)) {
            Vector3 hitDir = ray.direction;
            ExecuteHit(hit.point, hit.normal, hitDir, hit.collider.gameObject);
            return;
        }

        // Screen-space proximity fallback if no physics collider hit directly
        if (bossHeadTransform != null) {
            Vector3 headScreenPos = mainCamera.WorldToScreenPoint(bossHeadTransform.position);
            if (headScreenPos.z > 0 && Vector2.Distance(screenPos, (Vector2)headScreenPos) < 250f) {
                Vector3 hitDir = (bossHeadTransform.position - mainCamera.transform.position).normalized;
                ExecuteHit(bossHeadTransform.position, -hitDir, hitDir, bossHeadTransform.gameObject);
            }
        }
    }

    private void ExecuteHit(Vector3 hitPoint, Vector3 hitNormal, Vector3 hitDirection, GameObject hitObject) {
        Debug.Log($"[WeaponManager] Hit boss with {currentWeapon} at {hitPoint} on {hitObject.name}!");

        // 1. Trigger Kick-the-Buddy physics impulse
        if (bossRagdoll == null) {
            AutoFindBossReferences();
        }

        if (bossRagdoll != null) {
            bossRagdoll.ApplyHitImpulse(currentWeapon, hitPoint, hitDirection);
        } else {
            BossHitReceiver receiver = hitObject.GetComponentInParent<BossHitReceiver>();
            if (receiver != null) {
                receiver.ReceiveHit(currentWeapon, hitPoint, hitDirection);
            } else if (bossAnimator != null) {
                bossAnimator.Play("sittingDodge", 0, 0f);
            }
        }

        // 2. Register localized progressive face damage & redness (eyes, mouth, nose, forehead)
        if (bossFaceDamage == null) {
            bossFaceDamage = FindFirstObjectByType<BossFaceDamage>();
        }
        if (bossFaceDamage != null) {
            bossFaceDamage.RegisterDamageAtPoint(hitPoint, currentWeapon);
        }

        // 3. Spawn punch/hit visual impact effect
        SpawnImpactEffect(currentWeapon, hitPoint, hitNormal);

        // 4. Play boss hit voice reaction clip with pitch modulation
        if (BossVoiceManager.Instance != null) {
            BossVoiceManager.Instance.PlayHitReactionVoice(currentWeapon);
        }

        OnBossHit?.Invoke(currentWeapon, hitPoint);
    }

    private void SpawnImpactEffect(WeaponType weapon, Vector3 pos, Vector3 normal) {
        GameObject effectObj = new GameObject($"Impact_{weapon}");
        effectObj.transform.position = pos;
        effectObj.transform.rotation = Quaternion.LookRotation(normal);

        StartCoroutine(AnimateHitPunch(effectObj, weapon));
    }

    private IEnumerator AnimateHitPunch(GameObject fxObj, WeaponType weapon) {
        float elapsed = 0f;
        float duration = 0.35f;

        GameObject textObj = new GameObject("HitPopup");
        textObj.transform.position = fxObj.transform.position + Vector3.up * 0.25f;
        textObj.transform.forward = mainCamera != null ? mainCamera.transform.forward : Vector3.forward;

        TextMesh tm = textObj.AddComponent<TextMesh>();
        tm.fontSize = 42;
        tm.characterSize = 0.05f;
        tm.anchor = TextAnchor.MiddleCenter;
        tm.alignment = TextAlignment.Center;

        switch (weapon) {
            case WeaponType.Glove:
                tm.text = "🥊 POW!";
                tm.color = Color.red;
                break;
            case WeaponType.Slap:
                tm.text = "👋 SLAP!";
                tm.color = Color.yellow;
                break;
            case WeaponType.Coffee:
                tm.text = "☕ SPLASH!";
                tm.color = new Color(0.6f, 0.3f, 0.1f);
                break;
            case WeaponType.Chappal:
                tm.text = "🩴 WHACK!";
                tm.color = Color.magenta;
                break;
            case WeaponType.Super:
                tm.text = "💥 K.O.!";
                tm.color = Color.cyan;
                break;
        }

        Vector3 startPos = textObj.transform.position;
        while (elapsed < duration) {
            elapsed += Time.deltaTime;
            float t = elapsed / duration;
            if (textObj != null) {
                textObj.transform.position = startPos + Vector3.up * (t * 0.45f);
                textObj.transform.localScale = Vector3.one * (1f + Mathf.Sin(t * Mathf.PI) * 0.6f);
            }
            yield return null;
        }

        Destroy(textObj);
        Destroy(fxObj);
    }

    public void AutoFindBossReferences() {
        GameObject bossObj = GameObject.Find("Sitting Idle");
        if (bossObj != null) {
            if (bossAnimator == null) {
                bossAnimator = bossObj.GetComponentInChildren<Animator>();
            }

            if (bossRagdoll == null) {
                bossRagdoll = bossObj.GetComponentInChildren<BossRagdollHead>();
                if (bossRagdoll == null) {
                    bossRagdoll = bossObj.AddComponent<BossRagdollHead>();
                }
            }

            if (bossFaceDamage == null) {
                bossFaceDamage = bossObj.GetComponentInChildren<BossFaceDamage>();
                if (bossFaceDamage == null) {
                    bossFaceDamage = bossObj.AddComponent<BossFaceDamage>();
                }
            }
        }

        if (bossHeadTransform == null) {
            GameObject faceObj = GameObject.Find("face");
            if (faceObj != null) {
                bossHeadTransform = faceObj.transform;
            } else if (bossObj != null) {
                Transform[] allChildren = bossObj.GetComponentsInChildren<Transform>();
                foreach (var c in allChildren) {
                    if (c.name.ToLower().Contains("head") || c.name.ToLower().Contains("face")) {
                        bossHeadTransform = c;
                        break;
                    }
                }
            }
        }
    }

    private void EnsureBossComponents() {
        GameObject bossObj = GameObject.Find("Sitting Idle");
        if (bossObj != null) {
            if (bossObj.GetComponent<BossRagdollHead>() == null) {
                bossRagdoll = bossObj.AddComponent<BossRagdollHead>();
            } else {
                bossRagdoll = bossObj.GetComponent<BossRagdollHead>();
            }

            if (bossObj.GetComponent<BossHitReceiver>() == null) {
                bossObj.AddComponent<BossHitReceiver>();
            }

            if (bossObj.GetComponent<BossFaceDamage>() == null) {
                bossFaceDamage = bossObj.AddComponent<BossFaceDamage>();
            }
        }

        if (bossHeadTransform != null && bossHeadTransform.GetComponent<Collider>() == null) {
            SphereCollider sc = bossHeadTransform.gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.28f;
            sc.center = Vector3.zero;
        }
    }
}
