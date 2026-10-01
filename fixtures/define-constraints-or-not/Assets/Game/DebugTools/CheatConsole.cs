using UnityEngine;

namespace Game.DebugTools
{
    public class CheatConsole : MonoBehaviour
    {
        [SerializeField] private bool visible;

        public void Toggle()
        {
            visible = !visible;
            Debug.Log(visible ? "Cheat console opened" : "Cheat console closed");
        }
    }
}
