using Game.Core;
using UnityEngine;

namespace Game.Chat
{
    public class BadgeIcon : MonoBehaviour
    {
        public bool CanShowTitle(string title) => BadWordFilter.IsClean(title);
    }
}
