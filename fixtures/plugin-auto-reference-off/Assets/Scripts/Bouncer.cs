using UnityEngine;
using Vendor.Math;

public class Bouncer : MonoBehaviour
{
    private void Update()
    {
        transform.position = Vector3.up * Easing.InQuad(Time.time % 1f);
    }
}
