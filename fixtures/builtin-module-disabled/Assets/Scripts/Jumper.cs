using UnityEngine;

public class Jumper : MonoBehaviour
{
    [SerializeField] private float jumpForce = 5f;

    private Rigidbody body;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void Jump()
    {
        body.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
    }
}
