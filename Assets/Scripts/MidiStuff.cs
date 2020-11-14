using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using MidiJack;

public class MidiStuff : MonoBehaviour
{

    public GameObject objectToHide;
    public int noteNumber;
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        if (MidiMaster.GetKeyDown(noteNumber))
        {
            Debug.Log("Pressed 30");
            // objectToHide.SetActive(false);
        }
    }
}
