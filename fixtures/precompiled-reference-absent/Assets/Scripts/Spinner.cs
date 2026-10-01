using UnityEngine;

public class Spinner : MonoBehaviour
{
    public float degreesPerSecond = 90f;

    private void Start()
    {
        Debug.Log("spinning at " + degreesPerSecond);
    }
}
