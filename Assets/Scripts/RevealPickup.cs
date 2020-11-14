using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RevealPickup : MonoBehaviour
{
    public GameObject pickup;
    public GameObject hammer;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
       if(Input.GetKeyDown(KeyCode.Keypad0)) 
       {
           pickup.SetActive(true);
           hammer.SetActive(true);
       }
    }
}
