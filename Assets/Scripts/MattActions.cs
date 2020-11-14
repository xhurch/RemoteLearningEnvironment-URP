using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Valve.VR;
using UMP;

public class MattActions : MonoBehaviour
{

    public SteamVR_ActionSet actionSet;

    public SteamVR_Action_Boolean flipWhiteboard;
    public SteamVR_Action_Boolean nextSlide;
    public SteamVR_Action_Boolean previousSlide;
    public SteamVR_Action_Boolean nextVid;
    public SteamVR_Action_Boolean previousVid;
    public SteamVR_Action_Boolean shatterCube;
    public SteamVR_Action_Boolean showHideMenu;
    public SteamVR_Action_Boolean playVid;

    public SteamVR_Input_Sources handType;

    public Animator whiteboard;

    public Canvas canvas;
    public GameObject cube;
    public GameObject hammer;

    public LineRenderer myPointer;
    public Texture[] textures;
    public int currentTexture;

    public string[] DataSources;
    public int currentDataSource;

    public UniversalMediaPlayer UMP;
    public UniversalMediaPlayer vid1;
    // public int currentVid;
    // public UniversalMediaPlayer vid2;
    // public UniversalMediaPlayer vid3;

    // Start is called before the first frame update
    void Start()
    {
        flipWhiteboard.AddOnStateDownListener(FlipWhiteboard, handType);
        nextSlide.AddOnStateDownListener(NextSlide, handType);
        previousSlide.AddOnStateDownListener(PreviousSlide, handType);
        nextVid.AddOnStateDownListener(NextVid, handType);
        previousVid.AddOnStateDownListener(PreviousVid, handType);
        showHideMenu.AddOnStateDownListener(ShowHideCanvas, handType);
        shatterCube.AddOnStateDownListener(ShatterCube, handType);
        playVid.AddOnStateDownListener(PlayVid, handType);

        actionSet.Activate(SteamVR_Input_Sources.Any, 0, true);

        GameObject vids = GameObject.FindWithTag("Vids");

    }

    // Update is called once per frame
    void Update()
    {
        /* if (nextSlide.GetStateDown(SteamVR_Input_Sources.Any))
        {
            //NextSlide(nextSlide, handType);
            Debug.Log("Next Slide");
            currentTexture++;
            currentTexture %= textures.Length;
            GetComponent<Renderer>().material.mainTexture = textures[currentTexture];
        }*/

        if(Input.GetKeyDown(KeyCode.P))
        {
            UMP.Play();
        }
        if(Input.GetKeyDown(KeyCode.U))
        {
            UMP.Pause();
        }
    }

    public void FlipWhiteboard(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.Log("Button is up");
        whiteboard.GetComponent<Animator>().SetBool("Flip", true);
        vid1.Play();
        
    }

    public void NextSlide(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.Log("Next Slide");
        currentTexture++;
        currentTexture %= textures.Length;
        GetComponent<Renderer>().material.mainTexture = textures[currentTexture];
        currentDataSource++;
        currentDataSource %= DataSources.Length;
        UMP.GetComponent<UniversalMediaPlayer>().Path = DataSources[currentDataSource];
    }
    public void PreviousSlide(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        Debug.Log("Previous Slide");
        currentTexture--;
        currentTexture %= textures.Length;
        GetComponent<Renderer>().material.mainTexture = textures[currentTexture];
        currentDataSource--;
        currentDataSource %= DataSources.Length;
        UMP.GetComponent<UniversalMediaPlayer>().Path = DataSources[currentDataSource];
    }

    public void NextVid(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
            Debug.Log("Next Vid");
        
           
            // currentVid++;
            // currentVid %= vids.Length;
            // vids.Play();
            // vid2.SetActive(true);
            // vid1.SetActive(false);
    }

    public void PreviousVid(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
            Debug.Log("Previous Slide");
       
           
            // currentVid++;
            // currentVid %= vids.Length;
            // vids.Play();
            // vid2.SetActive(true);
            // vid1.SetActive(false);
    }

    public void ShowHideCanvas(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        canvas.enabled = !canvas.enabled;
        myPointer.enabled = !myPointer.enabled;
    }

    public void ShatterCube(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        cube.SetActive(true);
        hammer.SetActive(true);
    }

    public void PlayVid(SteamVR_Action_Boolean fromAction, SteamVR_Input_Sources fromSource)
    {
        UMP.Play();
        Debug.Log("Is Playing");
    }
}
