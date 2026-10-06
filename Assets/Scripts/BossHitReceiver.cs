using UnityEngine;

/// <summary>
/// Attached to the boss GameObject to receive hits and delegate
/// immediately to BossRagdollHead for dynamic Kick-the-Buddy physics.
/// </summary>
public class BossHitReceiver : MonoBehaviour {

    [SerializeField] private Animator animator;
    [SerializeField] private BossRagdollHead ragdollHead;

    private void Awake() {
        if (animator == null) {
            animator = GetComponentInParent<Animator>();
        }
        if (ragdollHead == null) {
            ragdollHead = GetComponentInParent<BossRagdollHead>();
            if (ragdollHead == null) {
                ragdollHead = gameObject.AddComponent<BossRagdollHead>();
            }
        }
    }

    public void ReceiveHit(WeaponType weapon, Vector3 worldHitPoint, Vector3 hitDirection) {
        if (ragdollHead != null) {
            ragdollHead.ApplyHitImpulse(weapon, worldHitPoint, hitDirection);
        } else if (animator != null) {
            animator.Play("sittingDodge", 0, 0f);
        }
    }
}
