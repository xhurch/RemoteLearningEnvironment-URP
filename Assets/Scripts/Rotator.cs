using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Rotator : MonoBehaviour
{
    public float speed;

    public float x;
    public float y;
    public float z;

    // Update is called once per frame
    void FixedUpdate()
    {
        transform.Rotate(new Vector3 (x, y, z) * speed);
    }
}