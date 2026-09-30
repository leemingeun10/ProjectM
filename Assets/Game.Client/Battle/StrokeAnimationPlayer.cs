using System.Collections;
using System.Collections.Generic;
using Game.Client.Stroke;
using Game.Sim.Stroke;
using UnityEngine;

namespace Game.Client.Battle
{
    // Plays the Slash/Thrust clip matching a completed stroke, then returns to Idle once the
    // clip has finished. A new stroke mid-swing restarts playback from the new clip.
    public class StrokeAnimationPlayer : MonoBehaviour
    {
        private const string IdleState = "Idle";
        private const string SlashState = "Slash";
        private const string ThrustState = "Thrust";

        [SerializeField] private StrokeInputController strokeInput;
        [SerializeField] private Animator animator;

        private Coroutine playRoutine;

        private void OnEnable()
        {
            strokeInput.StrokeCompleted += OnStrokeCompleted;
        }

        private void OnDisable()
        {
            strokeInput.StrokeCompleted -= OnStrokeCompleted;
        }

        private void OnStrokeCompleted(StrokeResult result, IReadOnlyList<StrokePoint> points)
        {
            string state = result.Type == StrokeType.Thrust ? ThrustState : SlashState;

            if (playRoutine != null)
                StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayThenIdle(state));
        }

        private IEnumerator PlayThenIdle(string state)
        {
            animator.Play(state, 0, 0f);
            yield return new WaitForSeconds(GetClipLength(state));
            animator.Play(IdleState, 0, 0f);
            playRoutine = null;
        }

        private float GetClipLength(string clipName)
        {
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name == clipName)
                    return clip.length;
            }

            Debug.LogWarning($"[StrokeAnimationPlayer] Clip '{clipName}' not found on {animator.name}'s controller.");
            return 0f;
        }
    }
}
