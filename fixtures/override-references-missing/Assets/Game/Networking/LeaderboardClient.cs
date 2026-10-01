using Newtonsoft.Json;
using UnityEngine;

namespace Game.Networking
{
    public class LeaderboardClient : MonoBehaviour
    {
        public string Serialize(int score)
        {
            return JsonConvert.SerializeObject(new { score });
        }
    }
}
