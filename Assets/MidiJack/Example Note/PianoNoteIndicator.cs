using UnityEngine;
using MidiJack;

public class PianoNoteIndicator : MonoBehaviour
{
    public int noteNumber;

    void Update()
    {
        float g = MidiMaster.GetKey(noteNumber);

        var pianoKey = GetComponent<Renderer>();
		
        if(g > 0.001f)
        {
            Debug.Log("KeyDown");
            pianoKey.material.SetColor("_BaseColor", Color.green);
        }
        else
            pianoKey.material.SetColor("_BaseColor", Color.white);
    }
}
