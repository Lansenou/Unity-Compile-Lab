using UnityEngine;

[CreateAssetMenu(menuName = "Vendor Tools/Settings")]
public class VendorToolsSettings : ScriptableObject
{
    [SerializeField] private string apiKey = string.Empty;

    public string ApiKey => apiKey;
}
