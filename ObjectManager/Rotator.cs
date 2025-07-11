using UnityEngine;

namespace ObjectManager
{
    public class Rotator : MonoBehaviour
    {
        public Vector3 rotationSpeed;
        public float duration = -1f;
        private float timer = 0f;

        void Update()
        {
            transform.Rotate(rotationSpeed * Time.deltaTime);

            if (duration > 0f && (timer += Time.deltaTime) >= duration)
            {
                Debug.Log($"[Rotator] Rotation finished after {duration} sec");
                Destroy(this);
            }
        }
    }
}
