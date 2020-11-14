using UnityEngine;
using MidiJack;

public class PianoNoteIndicatorBlackKeys : MonoBehaviour
{
    public int noteNumber;

    void Update()
    {
        float g = MidiMaster.GetKey(noteNumber);

        var pianoKey = GetComponent<Renderer>();
		
        if(g > 0.001f)
        {
            pianoKey.material.SetColor("_BaseColor", Color.green);
        }
        else
            pianoKey.material.SetColor("_BaseColor", Color.black);
    }
}
