using UnityEngine;

public class SaveMenu : MonoBehaviour
{
    [SerializeField] private CloudSaveClient cloudSave;

    public void OnSaveClicked()
    {
        cloudSave.Upload("slot0");
    }
}
