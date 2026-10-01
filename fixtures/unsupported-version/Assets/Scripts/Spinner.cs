using UnityEngine;

public class Spinner : MonoBehaviour
{
    [SerializeField] private float speed = 90f;

    private void Update()
    {
        transform.Rotate(new Vector3(0f, speed * Time.deltaTime, 0f));
    }
}
