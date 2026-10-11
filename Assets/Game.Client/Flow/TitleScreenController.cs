using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Flow
{
    // First screen on launch: title, background and a start button that leads to the home hub.
    // If a painted background is assigned it replaces the code-drawn ink mountains.
    public class TitleScreenController : MonoBehaviour
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private Button startButton;
        [Tooltip("Optional painted background (e.g. Assets/Environment/title_bg.png). Leave empty to use the drawn ink mountains.")]
        [SerializeField] private Texture backgroundImage;
        [SerializeField] private RawImage backgroundImageView;
        [SerializeField] private GameObject drawnBackground;

        private void Awake()
        {
            bool useImage = backgroundImage != null;
            backgroundImageView.texture = backgroundImage;
            backgroundImageView.gameObject.SetActive(useImage);
            drawnBackground.SetActive(!useImage);
        }

        private void OnEnable() => startButton.onClick.AddListener(StartGame);

        private void OnDisable() => startButton.onClick.RemoveListener(StartGame);

        private void StartGame() => flow.GoTo(GameScreen.Home);
    }
}
