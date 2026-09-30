using System.Collections;
using UnityEngine;

namespace Game.Client.Battle
{
    // Plays a sequence of action clips (Slash/Thrust/Guard) on one character, then returns to Idle.
    // A new request mid-sequence restarts from the new sequence. The sword trail emits only while
    // an action sequence is playing.
    //
    // Wait time is accumulated as deltaTime * animator.speed, so PlaybackSpeed (including 0 for
    // pause) scales both the clip and the wait before returning to Idle.
    [RequireComponent(typeof(Animator))]
    public class CharacterMotionPlayer : MonoBehaviour
    {
        public const string IdleState = "Idle";
        public const string SlashState = "Slash";
        public const string ThrustState = "Thrust";
        public const string GuardState = "Guard";

        [SerializeField] private TrailRenderer swordTrail;

        // Code-driven hit reaction (no hit clip): lean back, then return.
        private const float HitLeanDegrees = 5f;
        private const float HitLeanDuration = 0.15f;

        private Animator animator;
        private Coroutine playRoutine;
        private Coroutine hitRoutine;
        private Quaternion baseRotation;

        public float PlaybackSpeed
        {
            get => animator.speed;
            set => animator.speed = value;
        }

        public Vector3 SwordTipPosition => swordTrail != null ? swordTrail.transform.position : transform.position + Vector3.up;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            baseRotation = transform.localRotation;
            SetTrail(false);
        }

        private void OnDisable()
        {
            playRoutine = null;
            hitRoutine = null;
            transform.localRotation = baseRotation;
            SetTrail(false);
        }

        // Tilts the whole character back around its own right axis (pivot at the feet). Relies on
        // Apply Root Motion being off so the Animator never writes the root transform.
        public void PlayHitReaction()
        {
            if (hitRoutine != null)
                StopCoroutine(hitRoutine);
            hitRoutine = StartCoroutine(HitLean());
        }

        private IEnumerator HitLean()
        {
            Quaternion leaned = baseRotation * Quaternion.Euler(-HitLeanDegrees, 0f, 0f);
            yield return RotateOver(baseRotation, leaned);
            yield return RotateOver(leaned, baseRotation);
            hitRoutine = null;
        }

        private IEnumerator RotateOver(Quaternion from, Quaternion to)
        {
            float elapsed = 0f;
            while (elapsed < HitLeanDuration)
            {
                transform.localRotation = Quaternion.Slerp(from, to, elapsed / HitLeanDuration);
                yield return null;
                elapsed += Time.deltaTime * animator.speed;
            }

            transform.localRotation = to;
        }

        public void Play(params string[] states)
        {
            if (playRoutine != null)
                StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayThenIdle(states));
        }

        public void StopToIdle()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }

            animator.Play(IdleState, 0, 0f);
            SetTrail(false);

            if (hitRoutine != null)
            {
                StopCoroutine(hitRoutine);
                hitRoutine = null;
            }

            transform.localRotation = baseRotation;
        }

        private IEnumerator PlayThenIdle(string[] states)
        {
            SetTrail(true);
            foreach (string state in states)
            {
                animator.Play(state, 0, 0f);
                float length = GetClipLength(state);
                float elapsed = 0f;
                while (elapsed < length)
                {
                    yield return null;
                    elapsed += Time.deltaTime * animator.speed;
                }
            }

            animator.Play(IdleState, 0, 0f);
            SetTrail(false);
            playRoutine = null;
        }

        private void SetTrail(bool on)
        {
            if (swordTrail == null)
                return;

            // Clear on start so the new swing doesn't connect to where the blade was last time.
            if (on && !swordTrail.emitting)
                swordTrail.Clear();
            swordTrail.emitting = on;
        }

        private float GetClipLength(string clipName)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == clipName)
                    return clip.length;
            }

            Debug.LogWarning($"[CharacterMotionPlayer] Clip '{clipName}' not found on {animator.name}'s controller.");
            return 0f;
        }
    }
}
