using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwapTextures : MonoBehaviour
{
    public Texture[] textures;
    public int currentTexture;
    public MeshRenderer renderer;
    // Start is called before the first frame update
    void Start()
    {
        ;
    }

    // Update is called once per frame
    void Update()
    {
        if(Input.GetKeyDown(KeyCode.RightArrow))
        {
            currentTexture++;
            currentTexture %= textures.Length;
            GetComponent<Renderer>().material.mainTexture = textures[currentTexture];
        }

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            currentTexture--;
            currentTexture %= textures.Length;
            GetComponent<Renderer>().material.mainTexture = textures[currentTexture];
        }
    }
}
