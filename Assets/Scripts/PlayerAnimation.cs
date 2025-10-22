using UnityEngine;

[RequireComponent(typeof(Animator))]
public class PlayerAnimation : MonoBehaviour
{

    private Animator mAnimator;
    void Start()
    {
        mAnimator = GetComponent<Animator>();
    }

    void Update()
    {
        if (mAnimator != null)
        {
            bool isRunningForward = Input.GetKey(KeyCode.D);
            bool isRunningBackward = Input.GetKey(KeyCode.A);
            bool isAttacking = Input.GetKey(KeyCode.Space);

            mAnimator.SetBool("IsRunningForward", isRunningForward);
            mAnimator.SetBool("IsRunningBackward", isRunningBackward);
            mAnimator.SetBool("IsAttacking", isAttacking);

            // Optional: If you need to know direction, you can use a float
            // For example, in your Animator, use a float parameter "Direction"
            // and a 1D Blend Tree for Idle/Forward/Backward.
            // float moveDirection = Input.GetAxis("Horizontal"); // -1 for A, 1 for D, 0 for none
            // mAnimator.SetFloat("Direction", moveDirection);

        }
    }
}
