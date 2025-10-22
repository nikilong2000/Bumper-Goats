using System.ComponentModel;
using UnityEngine;
using UnityEngine.InputSystem;


[RequireComponent(typeof(Rigidbody))]
public class AiTestingScript : MonoBehaviour
{

    [Header("Brace Settings")]
    [SerializeField] private float braceMassMultiplier; // how many times heavier when bracing

    [SerializeField] private bool isBraced;
    [SerializeField] private MeshRenderer appearance; // TODO: testing only
    [SerializeField] private Material someMaterial; // TODO: for visually testing if isBraced is true; testing onyl

    private Rigidbody rb;
    private float originalMass;



    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        originalMass = rb.mass;


        if (isBraced)
        {
            rb.mass = originalMass * braceMassMultiplier;
            appearance = GetComponent<MeshRenderer>();
            appearance.material = someMaterial; // Replace 'someMaterial' with your desired Material reference
        }
    }

    private void OnEnable()
    {
    }

    // OnDisable is called when the object becomes disabled or inactive
    private void OnDisable()
    {
    }

    private void Update()
    {
    }

    private void FixedUpdate()
    {
    }


}