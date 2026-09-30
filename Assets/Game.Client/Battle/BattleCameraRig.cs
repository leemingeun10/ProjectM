using UnityEngine;

namespace Game.Client.Battle
{
    // Side-on third-person framing of both fighters. While a turn is focused the camera eases a
    // little closer to the pair, then eases back to the base framing when focus ends.
    public class BattleCameraRig : MonoBehaviour
    {
        private const float MoveDuration = 0.3f;

        [SerializeField] private Transform characterA;
        [SerializeField] private Transform characterB;
        [Tooltip("Camera position relative to the midpoint between the fighters.")]
        [SerializeField] private Vector3 baseOffset = new Vector3(5f, 1.3f, 0f);
        [SerializeField] private float lookHeight = 1f;
        [Tooltip("Fraction of the distance to the look point covered when focused on a turn.")]
        [Range(0f, 0.9f)]
        [SerializeField] private float zoomFraction = 0.25f;

        private bool focused;
        private Vector3 moveFrom;
        private float moveT = 1f;

        public void SetFocused(bool value)
        {
            if (focused == value)
                return;

            focused = value;
            moveFrom = transform.position;
            moveT = 0f;
        }

        private void LateUpdate()
        {
            Vector3 lookPoint = (characterA.position + characterB.position) * 0.5f + Vector3.up * lookHeight;
            Vector3 basePosition = lookPoint - Vector3.up * lookHeight + baseOffset;
            Vector3 target = focused ? Vector3.Lerp(basePosition, lookPoint, zoomFraction) : basePosition;

            if (moveT < 1f)
            {
                moveT = Mathf.Min(1f, moveT + Time.deltaTime / MoveDuration);
                transform.position = Vector3.Lerp(moveFrom, target, Mathf.SmoothStep(0f, 1f, moveT));
            }
            else
            {
                transform.position = target;
            }

            transform.LookAt(lookPoint);
        }
    }
}
