using Example.LooseUtils;
using UnityEngine;

public class Announcer : MonoBehaviour
{
    private void Start()
    {
        Debug.Log(StringUtils.Shout("ready"));
    }
}
