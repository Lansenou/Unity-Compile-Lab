using Example.Tools;
using UnityEngine;

public class DashAbility : MonoBehaviour
{
    private readonly Cooldown cooldown = new Cooldown(1.5f);

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.LeftShift) && cooldown.TryUse())
        {
            transform.position += transform.forward * 3f;
        }
    }
}
