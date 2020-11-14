using UnityEngine;
using MidiJack;

public class NoteIndicator : MonoBehaviour
{
    public int noteNumber;

    void Update()
    {
        transform.localScale = Vector3.one * (0.1f + MidiMaster.GetKey(noteNumber));

        var color = MidiMaster.GetKeyDown(noteNumber) ? Color.red : Color.green;
        GetComponent<Renderer>().material.color = color;
    }
}
