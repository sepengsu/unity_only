using UnityEngine;

namespace ObjectManager
{
    public class Mover : MonoBehaviour
    {
        public Vector3 moveSpeed;
        public float duration = -1f;
        private float timer;

        void Update()
        {
            transform.position += moveSpeed * Time.deltaTime;

            if (duration > 0f && (timer += Time.deltaTime) >= duration)
            {
                Debug.Log($"[Mover] Movement finished after {duration} sec");
                Destroy(this);
            }
        }
    }
}
