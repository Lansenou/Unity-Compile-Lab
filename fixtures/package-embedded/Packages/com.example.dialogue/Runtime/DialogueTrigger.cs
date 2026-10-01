using UnityEngine;

namespace Example.Dialogue
{
    public class DialogueTrigger : MonoBehaviour
    {
        [SerializeField] private string conversationId = "intro";

        public string ConversationId => conversationId;

#if DIALOGUE_PHYSICS_TRIGGERS
        private void OnTriggerEnter(Collider other)
        {
            Debug.Log($"Starting conversation {conversationId} with {other.name}");
        }
#endif
    }
}
