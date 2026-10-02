using UnityEngine;
using Vendor.Math;

public class Tween : MonoBehaviour
{
    [SerializeField] private float duration = 1f;

    private float elapsed;

    private void Update()
    {
        elapsed += Time.deltaTime;
        transform.localScale = Vector3.one * Easing.OutQuad(elapsed / duration);
    }
}
