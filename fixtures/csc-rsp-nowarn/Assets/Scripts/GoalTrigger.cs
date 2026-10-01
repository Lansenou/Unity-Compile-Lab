using UnityEngine;

public class GoalTrigger : MonoBehaviour
{
    [SerializeField] private int points = 100;

    private void OnTriggerEnter(Collider other)
    {
        LegacyScore.Report(points);
    }
}
