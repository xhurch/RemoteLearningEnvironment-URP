using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MidiJack;

public class Instantiate : MonoBehaviour
{
    public GameObject objectToInstantiate;

    public int noteNumber;

    // Update is called once per frame
    void Update()
    {
        if(MidiMaster.GetKeyUp(noteNumber))
        {
            Debug.Log("Yo.");
            Instantiate(objectToInstantiate);
        }
        
        if(Input.GetKeyDown(KeyCode.K))
        {
            Debug.Log("Yo.");
            Instantiate(objectToInstantiate);
        }
    }
}
