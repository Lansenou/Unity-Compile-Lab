using UnityEngine;
using UnityStandardAssets.Characters.ThirdPerson;

[RequireComponent(typeof(ThirdPersonCharacter))]
public class PlayerController : MonoBehaviour
{
    private ThirdPersonCharacter character;

    private void Awake()
    {
        character = GetComponent<ThirdPersonCharacter>();
    }

    private void Update()
    {
        character.Move(new Vector3(Input.GetAxis("Horizontal"), 0f, Input.GetAxis("Vertical")));
    }
}
