using Game.EditorTools;
using UnityEngine;

namespace Game.Runtime
{
    public class LevelState : MonoBehaviour
    {
        public bool CanSave() => !LevelValidator.IsBusy();
    }
}
