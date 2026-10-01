using UnityEngine;

namespace Game.Camera
{
    public class CameraCollision : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float minDistance = 0.5f;

        public float ResolveDistance(float desired)
        {
#if GAME_CAMERA_PHYSICS
            var direction = transform.position - target.position;
            if (Physics.Raycast(target.position, direction, out RaycastHit hit, desired))
            {
                return Mathf.Max(minDistance, hit.distance);
            }
#endif
            return desired;
        }
    }
}
