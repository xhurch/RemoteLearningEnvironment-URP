using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UMP;

public class MediaPlayerController : MonoBehaviour
{
    public UniversalMediaPlayer UMP;
    public GameObject vid1;
    public GameObject vid2;
    public GameObject vid3;
    // Start is called before the first frame update
    void Start()
    {
        UMP = GetComponent<UniversalMediaPlayer>();
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.P))
        {
            UMP.Play();
        }
        if(Input.GetKeyDown(KeyCode.U))
        {
            UMP.Pause();
        }
        if (Input.GetKeyDown(KeyCode.Delete))
        {
            vid2.SetActive(true);
            vid1.SetActive(false);

        }
        if (Input.GetKeyDown(KeyCode.End))
        {
            vid3.SetActive(true);
            vid2.SetActive(false);
        }
    }
}
