using System.Collections;
using UnityEngine;

namespace Game.Client.Battle
{
    // Short white flash at a hit point: the sprite grows while fading out, facing the camera.
    [RequireComponent(typeof(SpriteRenderer))]
    public class ImpactFlash : MonoBehaviour
    {
        [SerializeField] private float duration = 0.2f;
        [SerializeField] private float maxScale = 0.8f;

        private SpriteRenderer sprite;
        private Coroutine flashRoutine;

        private void Awake()
        {
            sprite = GetComponent<SpriteRenderer>();
            sprite.enabled = false;
        }

        public void Play(Vector3 position)
        {
            transform.position = position;
            if (flashRoutine != null)
                StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(Flash());
        }

        private IEnumerator Flash()
        {
            sprite.enabled = true;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                float t = elapsed / duration;
                transform.localScale = Vector3.one * Mathf.Lerp(0.1f, maxScale, t);
                sprite.color = new Color(1f, 1f, 1f, 1f - t);
                if (Camera.main != null)
                    transform.rotation = Camera.main.transform.rotation;

                yield return null;
                elapsed += Time.deltaTime;
            }

            sprite.enabled = false;
            flashRoutine = null;
        }
    }
}
