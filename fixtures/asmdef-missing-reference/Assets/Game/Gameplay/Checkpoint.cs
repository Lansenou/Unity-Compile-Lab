using UnityEngine;

namespace Game.Gameplay
{
    public class Checkpoint : MonoBehaviour
    {
        [SerializeField] private string checkpointId = "start";

        public void Activate(SaveSystem saveSystem)
        {
            saveSystem.Save(checkpointId);
            Debug.Log($"Checkpoint {checkpointId} saved");
        }
    }
}
