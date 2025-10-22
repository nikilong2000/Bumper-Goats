// using UnityEngine;
// using Unity.MLAgents;
// using Unity.MLAgents.Actuators;
// using Unity.MLAgents.Sensors;
// using Unity.VisualScripting;

// public class AiGoatScript : Agent
// {
//     [SerializeField] private Transform playerTransform;
//     [SerializeField] private Transform playerEdgeTransform;
//     [SerializeField] private Transform ownEdgeTransform;
//     // [SerializeField] private Material winMaterial;
//     // [SerializeField] private Material looseMaterial;
//     // [SerializeField] private MeshRenderer floorMeshRenderer;

//     public override void OnEpisodeBegin()
//     {
//         // transform.localPosition = new Vector3(Random.Range(0f, 3.7f), 1, Random.Range(4f, -4f));

//         // targetTransform.localPosition = new Vector3(Random.Range(-1f, -4f), 1, Random.Range(4f, -4f));

//     }


//     public override void OnActionReceived(ActionBuffers actions)
//     {
//         float moveX = actions.ContinuousActions[0];
//         float moveZ = actions.ContinuousActions[1];

//         float moveSpeed = 4f;

//         transform.localPosition += moveSpeed * Time.deltaTime * new Vector3(moveX, 0, moveZ);
//     }

//     public override void CollectObservations(VectorSensor sensor)
//     {
//         sensor.AddObservation(transform.position);
//         sensor.AddObservation(playerTransform.position);
//         sensor.AddObservation(playerEdgeTransform.position);
//         sensor.AddObservation(ownEdgeTransform.position);
//     }

//     // public override void Heuristic(in ActionBuffers actionsOut)
//     // {
//     //     ActionSegment<float> continuosActions = actionsOut.ContinuousActions;
//     //     continuosActions[0] = Input.GetAxisRaw("Horizontal");
//     //     continuosActions[1] = Input.GetAxisRaw("Vertical");
//     // }

//     private void OnTriggerEnter(Collider other)
//     {
//         // if (other.gameObject.TryGetComponent<Goal>(out Goal goal))
//         // {
//         //     floorMeshRenderer.material = winMaterial;
//         //     SetReward(+1f);
//         //     EndEpisode();
//         // }

//         // if (other.gameObject.TryGetComponent<Wall>(out Wall wall))
//         // {
//         //     floorMeshRenderer.material = looseMaterial;
//         //     SetReward(-1f);
//         //     EndEpisode();
//         // }
//     }
// }
