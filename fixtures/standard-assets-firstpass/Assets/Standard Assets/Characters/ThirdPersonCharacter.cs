using UnityEngine;

namespace UnityStandardAssets.Characters.ThirdPerson
{
    [RequireComponent(typeof(Rigidbody))]
    public class ThirdPersonCharacter : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 4f;

        public void Move(Vector3 direction)
        {
            transform.position += direction * (moveSpeed * Time.deltaTime);
        }
    }
}
