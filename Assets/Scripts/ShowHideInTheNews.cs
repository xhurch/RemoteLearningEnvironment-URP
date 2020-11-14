using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ShowHideInTheNews : MonoBehaviour
{
    public GameObject inTheNews;
    public MeshRenderer globe;
    public MeshRenderer news;

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.N))
        {
            inTheNews.SetActive(true);
        }
        
        if(Input.GetKeyDown(KeyCode.H))
        {
            globe.enabled = !globe.enabled;
            news.enabled = !news.enabled;
        }
    }
}
