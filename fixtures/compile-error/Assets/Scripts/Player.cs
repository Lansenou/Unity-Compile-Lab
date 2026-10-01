using UnityEngine;

public class Player : MonoBehaviour
{
    [SerializeField] private float speed = 4f;

    private void Update()
    {
        transfrom.position += Vector3.forward * speed * Time.deltaTime;
    }
}
