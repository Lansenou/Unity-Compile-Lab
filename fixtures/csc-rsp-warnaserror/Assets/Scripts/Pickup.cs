using UnityEngine;

public class Pickup : MonoBehaviour
{
    [SerializeField] private string itemId = "coin";

    private void OnTriggerEnter(Collider other)
    {
        InventoryService.AddItem(itemId);
        Destroy(gameObject);
    }
}
