using UnityEngine;
using UnityEngine.InputSystem;

namespace Game.Client.Temp
{
    // TEMPORARY: visual check for the Mixamo clips. Keys 1-4 jump straight to Idle/Slash/Thrust/Guard.
    // Delete once real animation driving is in place.
    [RequireComponent(typeof(Animator))]
    public class AnimationPreviewTester : MonoBehaviour
    {
        private static readonly string[] States = { "Idle", "Slash", "Thrust", "Guard" };

        private Animator animator;

        private void Awake()
        {
            animator = GetComponent<Animator>();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null) return;

            if (keyboard.digit1Key.wasPressedThisFrame) PlayState(0);
            else if (keyboard.digit2Key.wasPressedThisFrame) PlayState(1);
            else if (keyboard.digit3Key.wasPressedThisFrame) PlayState(2);
            else if (keyboard.digit4Key.wasPressedThisFrame) PlayState(3);
        }

        private void PlayState(int index)
        {
            // normalizedTime 0 restarts the clip even if it is already the current state.
            animator.Play(States[index], 0, 0f);
        }
    }
}
