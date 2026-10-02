// Fixture source for the ucl conformance corpus. Original code, Apache-2.0.

#if EXAMPLE_INPUT_ENABLE_UI
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Example.Input.UI
{
    public class InputModule : UnityEngine.MonoBehaviour
    {
        public Selectable firstSelected;

        public PointerEventData Pointer() => new PointerEventData();

        public bool HasEventSystem() => EventSystem.current != null;
    }
}
#endif
