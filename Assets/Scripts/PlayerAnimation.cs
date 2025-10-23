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
            bool isBracing = Input.GetKey(KeyCode.S);

            bool doAttack = Input.GetKeyDown(KeyCode.Space);
            bool doJump = Input.GetKeyDown(KeyCode.W);
            bool doSpinLeft = Input.GetKeyDown(KeyCode.Q);

            // mAnimator.SetBool("IsJumping", isJumping)   ;
            mAnimator.SetBool("IsRunningForward", isRunningForward);
            mAnimator.SetBool("IsRunningBackward", isRunningBackward);
            mAnimator.SetBool("IsBracing", isBracing);
            // mAnimator.SetBool("IsAttacking", isAttacking);


            if (doJump)
            {
                mAnimator.SetTrigger("DoJump");
            }

            if (doAttack)
            {
                mAnimator.SetTrigger("DoAttack");
            }

            if (doSpinLeft)
            {
                mAnimator.SetTrigger("DoSpinLeft");
            }

            // Optional: If you need to know direction, you can use a float
            // For example, in your Animator, use a float parameter "Direction"
            // and a 1D Blend Tree for Idle/Forward/Backward.
            // float moveDirection = Input.GetAxis("Horizontal"); // -1 for A, 1 for D, 0 for none
            // mAnimator.SetFloat("Direction", moveDirection);

        }
    }
}
