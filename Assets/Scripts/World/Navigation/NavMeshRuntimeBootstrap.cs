using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;

[DefaultExecutionOrder(-500)]
[DisallowMultipleComponent]
[RequireComponent(typeof(NavMeshSurface))]
public class NavMeshRuntimeBootstrap : MonoBehaviour
{
    [SerializeField] private bool rebuildOnStart = true;
    [SerializeField] private float agentSnapRadius = 12f;

    private NavMeshSurface surface;

    private void Awake()
    {
        surface = GetComponent<NavMeshSurface>();
    }

    private void Start()
    {
        if (surface == null)
        {
            return;
        }

        if (rebuildOnStart)
        {
            surface.BuildNavMesh();
        }

        SnapAllAgentsToNavMesh();
    }

    private void SnapAllAgentsToNavMesh()
    {
        NavMeshAgent[] agents = FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        for (int i = 0; i < agents.Length; i++)
        {
            NavMeshAgent agent = agents[i];
            if (agent == null || !agent.enabled || !agent.isActiveAndEnabled)
            {
                continue;
            }

            if (agent.isOnNavMesh)
            {
                continue;
            }

            Vector3 probeCenter = agent.transform.position;
            if (NavMesh.SamplePosition(probeCenter, out NavMeshHit hit, agentSnapRadius, agent.areaMask))
            {
                agent.Warp(hit.position);
            }
        }
    }
}
