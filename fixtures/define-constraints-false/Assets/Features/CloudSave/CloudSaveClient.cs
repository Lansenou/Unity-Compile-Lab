using UnityEngine;

public class CloudSaveClient : MonoBehaviour
{
    public void Upload(string slot)
    {
        Debug.Log($"Uploading save slot {slot}");
    }
}
