using System.Collections;
using UnityEngine;

/// <summary>
/// "Kick the Buddy" head recoil & wobble physics:
/// - Finds the actual skeletal head bone (mixamorig:Head) and neck bone.
/// - In LateUpdate() (after Animator runs), applies physics-based rotation recoil and spring damping.
/// - DOES NOT mutate scale or position, completely preventing the mesh from bursting or swelling.
/// </summary>
public class BossRagdollHead : MonoBehaviour {

    [Header("Bones")]
    [SerializeField] private Transform headBone;
    [SerializeField] private Transform neckBone;

    [Header("Spring Physics Parameters")]
    [Tooltip("Stiffness of the spring pulling head back to animated pose")]
    [SerializeField] private float springStiffness = 140f;
    [Tooltip("Damping ratio to smoothly settle head recoil without jitter")]
    [SerializeField] private float springDamping = 14f;
    [Tooltip("Maximum tilt angle in degrees to prevent unnatural broken-neck poses")]
    [SerializeField] private float maxTiltAngle = 40f;

    // Angular wobble state
    private Vector3 currentEulerOffset = Vector3.zero;
    private Vector3 rotVelocity = Vector3.zero;

    private Animator animator;

    private void Awake() {
        animator = GetComponentInChildren<Animator>();
        if (animator == null) {
            animator = GetComponentInParent<Animator>();
        }
        FindBones();
    }

    private void Start() {
        if (headBone == null) {
            FindBones();
        }
        EnsureHeadCollider();
    }

    private void FindBones() {
        // Search thoroughly through all children for the true skeletal head bone
        Transform[] allTransforms = GetComponentsInChildren<Transform>(true);
        foreach (var t in allTransforms) {
            string lower = t.name.ToLower();
            if (lower.Contains("head") && !lower.Contains("top") && !lower.Contains("end")) {
                // Prefer mixamorig:Head or actual bone
                if (headBone == null || t.name.Contains(":")) {
                    headBone = t;
                }
            } else if (lower.Contains("neck")) {
                if (neckBone == null || t.name.Contains(":")) {
                    neckBone = t;
                }
            }
        }

        // If bone not found in self hierarchy, search the boss character root
        if (headBone == null) {
            GameObject bossObj = GameObject.Find("Sitting Idle");
            if (bossObj != null) {
                Transform[] bossChildren = bossObj.GetComponentsInChildren<Transform>(true);
                foreach (var t in bossChildren) {
                    string lower = t.name.ToLower();
                    if (lower.Contains("head") && !lower.Contains("top") && !lower.Contains("end")) {
                        headBone = t;
                        break;
                    }
                }
            }
        }

        Debug.Log($"[BossRagdollHead] Head bone bound to: {(headBone != null ? headBone.name : "NULL")}");
    }

    private void EnsureHeadCollider() {
        Transform target = headBone;
        if (target == null) {
            GameObject faceObj = GameObject.Find("face");
            if (faceObj != null) target = faceObj.transform;
        }

        if (target != null && target.GetComponent<Collider>() == null) {
            SphereCollider sc = target.gameObject.AddComponent<SphereCollider>();
            sc.radius = 0.22f;
            sc.center = Vector3.zero;
        }
    }

    /// <summary>
    /// Applies a physics impulse to the head rotation upon being struck.
    /// </summary>
    public void ApplyHitImpulse(WeaponType weapon, Vector3 hitPoint, Vector3 hitDirection) {
        float rotForce = 90f;

        switch (weapon) {
            case WeaponType.Glove:
                rotForce = 130f;
                break;
            case WeaponType.Slap:
                rotForce = 160f; // Fast snappy horizontal slap
                break;
            case WeaponType.Coffee:
                rotForce = 75f;
                break;
            case WeaponType.Chappal:
                rotForce = 140f;
                break;
            case WeaponType.Super:
                rotForce = 220f;
                break;
        }

        // Determine recoil rotation axis from hit direction
        Vector3 headPos = headBone != null ? headBone.position : transform.position;
        Vector3 hitVector = (hitPoint - headPos);

        // Cross product gives the natural rotational axis around which the head spins
        Vector3 torqueAxis = Vector3.Cross(hitVector.normalized, hitDirection.normalized);
        if (torqueAxis.sqrMagnitude < 0.05f) {
            // Default backwards tilt if hit straight-on
            torqueAxis = Vector3.right;
        }

        // Convert world torque axis to head's local space so it rotates cleanly
        if (headBone != null) {
            torqueAxis = headBone.InverseTransformDirection(torqueAxis);
        }

        rotVelocity += torqueAxis.normalized * rotForce;

        // Trigger dodge / flinch animation
        if (animator != null) {
            animator.Play("sittingDodge", 0, 0f);
        }
    }

    /// <summary>
    /// LateUpdate runs AFTER the Animator evaluates.
    /// Modifies ONLY localRotation, leaving scale and position untouched.
    /// </summary>
    private void LateUpdate() {
        if (headBone == null) return;

        float dt = Time.deltaTime;
        if (dt <= 0f || dt > 0.1f) dt = 0.016f;

        // Damped harmonic spring for rotational recoil
        Vector3 springTorque = -springStiffness * currentEulerOffset - springDamping * rotVelocity;
        rotVelocity += springTorque * dt;
        currentEulerOffset += rotVelocity * dt;

        // Clamp to prevent breaking the neck
        currentEulerOffset.x = Mathf.Clamp(currentEulerOffset.x, -maxTiltAngle, maxTiltAngle);
        currentEulerOffset.y = Mathf.Clamp(currentEulerOffset.y, -maxTiltAngle, maxTiltAngle);
        currentEulerOffset.z = Mathf.Clamp(currentEulerOffset.z, -maxTiltAngle, maxTiltAngle);

        // Apply secondary flex to neck if present
        if (neckBone != null) {
            neckBone.localRotation *= Quaternion.Euler(currentEulerOffset * 0.3f);
        }

        // Apply recoil tilt to head bone
        headBone.localRotation *= Quaternion.Euler(currentEulerOffset);
    }
}
