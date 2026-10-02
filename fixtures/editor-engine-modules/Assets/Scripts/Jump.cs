// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.
using UnityEngine;

public class Jump : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Debug.Log("jump");
        }
    }
}
