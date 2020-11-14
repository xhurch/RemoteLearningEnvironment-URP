using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class WhiteBoardAnimator : MonoBehaviour
{
    public Animator whiteboardAnim;
    // Start is called before the first frame update
    void Start()
    {
        whiteboardAnim = GetComponent<Animator>();
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            whiteboardAnim.enabled = true;
            whiteboardAnim.SetBool("Flip", true);
        }
        // if (Input.GetKeyDown(KeyCode.Tab))
        // {
        //     whiteboardAnim.enabled = true;
        // }

        
    }

    void pauseAnimation()
    {
        whiteboardAnim.SetBool("Flip", false);
        whiteboardAnim.enabled = false;
    }
}
