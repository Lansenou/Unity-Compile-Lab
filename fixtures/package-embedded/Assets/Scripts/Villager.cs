using Example.Dialogue;
using UnityEngine;

[RequireComponent(typeof(DialogueTrigger))]
public class Villager : MonoBehaviour
{
    private void Start()
    {
        Debug.Log($"Villager speaks {GetComponent<DialogueTrigger>().ConversationId}");
    }
}
