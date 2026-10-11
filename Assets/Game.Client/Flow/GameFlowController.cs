using System;
using System.Collections;
using Game.Client.Stroke;
using UnityEngine;

namespace Game.Client.Flow
{
    // Single hub for screen transitions. Each screen is a root GameObject under the UI canvas;
    // GoTo fades to ink, swaps which root is active, raises ScreenExited/ScreenEntered and fades
    // back. Controllers react to those events for their own enter/exit work instead of toggling
    // each other's panels. The app always starts on the title screen.
    public class GameFlowController : MonoBehaviour
    {
        [SerializeField] private GameObject titleScreen;
        [SerializeField] private GameObject homeScreen;
        [SerializeField] private GameObject workshopScreen;
        [SerializeField] private GameObject trainingScreen;
        [SerializeField] private GameObject sparringScreen;
        [SerializeField] private GameObject battleScreen;
        // Overlays shown on the menu-like screens (e.g. the PvP record HUD); hidden on the title
        // and during battle, where the HP bars take the top of the screen.
        [SerializeField] private GameObject hudRoot;
        // Full-screen ink overlay used for the fade; blocks input while a transition runs.
        [SerializeField] private CanvasGroup fadeOverlay;
        [SerializeField] private float fadeDuration = 0.22f;
        // Drawing only happens on the workshop screen; elsewhere the input is switched off.
        [SerializeField] private StrokeInputController strokeInput;

        private Coroutine transition;

        public GameScreen Current { get; private set; } = GameScreen.Title;
        public bool IsTransitioning => transition != null;

        public event Action<GameScreen> ScreenExited;
        public event Action<GameScreen> ScreenEntered;

        private void Start()
        {
            ShowOnly(GameScreen.Title);
            ScreenEntered?.Invoke(GameScreen.Title);
            // Open on ink and fade in, as if the title is being revealed.
            transition = StartCoroutine(Reveal());
        }

        private IEnumerator Reveal()
        {
            yield return Fade(1f, 0f);
            transition = null;
        }

        // onEntered runs after the new screen is active and its ScreenEntered handlers have run,
        // while the fade-in is still under way (e.g. to start a replay on the battle screen).
        public void GoTo(GameScreen screen, Action onEntered = null)
        {
            if (transition != null)
                StopCoroutine(transition);
            transition = StartCoroutine(Transition(screen, onEntered));
        }

        private IEnumerator Transition(GameScreen screen, Action onEntered)
        {
            yield return Fade(fadeOverlay.alpha, 1f);

            GameScreen previous = Current;
            if (previous != screen)
                ScreenExited?.Invoke(previous);
            Current = screen;
            ShowOnly(screen);
            if (previous != screen)
                ScreenEntered?.Invoke(screen);
            onEntered?.Invoke();

            yield return Fade(1f, 0f);
            transition = null;
        }

        private IEnumerator Fade(float from, float to)
        {
            fadeOverlay.blocksRaycasts = true;
            float t = 0f;
            while (t < fadeDuration)
            {
                t += Time.unscaledDeltaTime;
                fadeOverlay.alpha = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t / fadeDuration));
                yield return null;
            }
            fadeOverlay.alpha = to;
            fadeOverlay.blocksRaycasts = to > 0f;
        }

        private void ShowOnly(GameScreen screen)
        {
            titleScreen.SetActive(screen == GameScreen.Title);
            homeScreen.SetActive(screen == GameScreen.Home);
            workshopScreen.SetActive(screen == GameScreen.Workshop);
            trainingScreen.SetActive(screen == GameScreen.Training);
            sparringScreen.SetActive(screen == GameScreen.Sparring);
            battleScreen.SetActive(screen == GameScreen.Battle);
            hudRoot.SetActive(screen != GameScreen.Title && screen != GameScreen.Battle);
            strokeInput.enabled = screen == GameScreen.Workshop;
        }
    }
}
