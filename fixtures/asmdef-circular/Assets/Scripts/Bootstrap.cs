using UnityEngine;

public class Bootstrap : MonoBehaviour
{
    private void Start()
    {
        Debug.Log($"Booting {Application.productName} {Application.version}");
    }
}
