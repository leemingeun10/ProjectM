using UnityEngine;
using UnityEngine.UI;

namespace Game.Client.Battle
{
    // Head-top HP bar on a world-space canvas. Billboards to the main camera every frame so it
    // stays readable from any camera angle.
    public class WorldHpBar : MonoBehaviour
    {
        [SerializeField] private Image fill;
        [SerializeField] private Text label;

        public void SetHp(float hp, float maxHp)
        {
            fill.fillAmount = hp / maxHp;
            label.text = Mathf.RoundToInt(hp).ToString();
        }

        private void LateUpdate()
        {
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = cam.transform.rotation;
        }
    }
}
