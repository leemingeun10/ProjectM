using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Flow
{
    // A button that just moves to another screen ("홈으로", "수련으로", ...).
    [RequireComponent(typeof(Button))]
    public class ScreenNavButton : MonoBehaviour
    {
        [SerializeField] private GameFlowController flow;
        [SerializeField] private GameScreen target = GameScreen.Home;

        private Button button;

        private void Awake() => button = GetComponent<Button>();

        private void OnEnable() => button.onClick.AddListener(Go);

        private void OnDisable() => button.onClick.RemoveListener(Go);

        private void Go() => flow.GoTo(target);
    }
}
