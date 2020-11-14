using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using VRTK;

public class ReturnToPositionOntology : MonoBehaviour
{
    //This controls how fast the object returns to it's position.
    public float moveSpeed = 2f;
    private float movePercent;
    private float rotatePercent;

    private bool audioPlayedThisMove;
    public float percentMoveToPlayAudio = 0.9f;
    public AudioSource audioSource;
    public AudioClip playWhenMoveIsFinished;

    private float startTime;
    private float journeyLength;
    private float rotationLength;

    private Rigidbody objectRigidbody;
    public Transform objectTransform
    {
        get; private set;
    }

    //Variables that controls where the object returns to, set to the position it starts at.
    public Vector3 sceneStartPos
    {
        get; private set;
    }
    public Quaternion sceneStartRotation
    {
        get; private set;
    }
    //Variables that stand for where the object is at the start of the Move.
    private Vector3 moveStartPos;
    private Quaternion moveStartRotation;

    //Reference to the other object that will return to it's starting position when this object returns to it's starting position.
    public List<ReturnToPositionOntology> linkedObjects;
    // private Pickup pickupScript;
    public bool isMoving = false;
    private bool isRotating = false;
    private bool linkedMotion = false;
    public bool noLinkedObjectsHeld = true;
    
    //Layer that has no collisions applied so the objects return to their position without collisions.
    public int noCollisionLayer = 8;
    private bool objectUsesGravity;
    private int objectLayer;


    //If you need to have an object move based off of a trigger as opposed to a conditional(return to position when a button is pressed), you can just have that function access this script and run the function StartLerpMove().

    // Use this for initialization
    void Start()
    {
        // pickupScript = GetComponent<Pickup>();
        objectRigidbody = GetComponent<Rigidbody>();
        objectTransform = GetComponent<Transform>();
        sceneStartPos = objectTransform.position;
        sceneStartRotation = objectTransform.rotation;
        objectLayer = gameObject.layer;
        objectUsesGravity = objectRigidbody.useGravity;
    }

    // Update is called once per frame
    void Update()
    {
        noLinkedObjectsHeld = true;
        // foreach (ReturnToPositionOntology linkedObject in linkedObjects)
        // {
        //     if (linkedObject.pickupScript.isAttached)
        //     {
        //         noLinkedObjectsHeld = false;
        //     }
        // }

        // Conditional to check if this object or the linked object is currently held.
        // if (pickupScript.isAttached | !noLinkedObjectsHeld)
        // {
        //     objectRigidbody.useGravity = objectUsesGravity;
        //     isMoving = false;
        //     isRotating = false;
        //     SetLayerOnAll(gameObject, objectLayer);
        // }
        //Execute LerpMove and LerpRotate if object should be moving.
        if (isMoving)
        {
            LerpMove();
        }
        if (isRotating)
        {
            LerpRotate();
        }
        //Resets movements when the linked objects are finished moving.
        if (linkedMotion & !isMoving & !isRotating)
        {
            bool objectsStillMoving = false;
            foreach (ReturnToPositionOntology linkedObject in linkedObjects)
            {
                if(linkedObject.isMoving | linkedObject.isRotating)
                {
                    objectsStillMoving = true;
                }
            }
            if (objectsStillMoving)
            {
                objectRigidbody.velocity = Vector3.zero;
                objectRigidbody.angularVelocity = Vector3.zero;
                linkedMotion = false;
                SetLayerOnAll(gameObject, objectLayer);
                objectRigidbody.useGravity = objectUsesGravity;
                Debug.Log("Linked move done");
            }
        }
    }

    private void LerpMove()
    {
        ////Move object at fixed speed
        //float distCovered = (Time.time - startTime) * moveSpeed;
        //float fracJourney = distCovered / journeyLength;
        //objectRigidbody.MovePosition(Vector3.Lerp(moveStartPos, sceneStartPos, fracJourney));
        ////transform.position = Vector3.Lerp(moveStartPos, sceneStartPos, fracJourney);

        //Move object over fixed amount of time.
        movePercent += (moveSpeed * 0.005f);
        float fracJourney = movePercent;
        objectRigidbody.MovePosition(Vector3.Lerp(moveStartPos, sceneStartPos, fracJourney));

        //Plays audio at specified point in move.
        if(fracJourney > percentMoveToPlayAudio & !audioPlayedThisMove)
        {
            audioPlayedThisMove = true;
            if (playWhenMoveIsFinished != null)
            {
                audioSource.PlayOneShot(playWhenMoveIsFinished);
            }
        }
        if (fracJourney >= 1)
        {
            //Debug.Log("Move done at " + Time.time);
            isMoving = false;

            //Plays audio when move is entirely finished.
            //if(playWhenMoveIsFinished != null)
            //{
            //    audioSource.PlayOneShot(playWhenMoveIsFinished);
            //}

            if (!isMoving & !isRotating & !linkedMotion)
            {
                objectRigidbody.velocity = Vector3.zero;
                SetLayerOnAll(gameObject, objectLayer);
                GetComponent<Rigidbody>().useGravity = objectUsesGravity;
            }
        }
    }

    private void LerpRotate()
    {
        ////Rotate object at fixedRate
        //float distCovered = (Time.time - startTime) * moveSpeed;
        //float fracRotate = distCovered / journeyLength;
        //objectRigidbody.MoveRotation(Quaternion.Lerp(moveStartRotation, sceneStartRotation, fracRotate));
        //transform.rotation = Quaternion.Lerp(objectTransform.rotation, startRotation, fracRotate);
        rotatePercent += (moveSpeed * 0.005f);
        float fracRotate = rotatePercent;
        objectRigidbody.MoveRotation(Quaternion.Lerp(moveStartRotation, sceneStartRotation, fracRotate));
        //Rotate object over fixed time
        if (fracRotate >= 1)
        {
            //Debug.Log("Rotate done at " + Time.time);
            isRotating = false;
            if (!isMoving & !isRotating & !linkedMotion)
            {
                objectRigidbody.angularVelocity = Vector3.zero;
                SetLayerOnAll(gameObject, objectLayer);
                objectRigidbody.useGravity = objectUsesGravity;
            }
        }
    }

    public void StartLerpMove()
    {
        movePercent = 0;
        rotatePercent = 0;
        audioPlayedThisMove = false;
        objectRigidbody.velocity = Vector3.zero;
        objectRigidbody.angularVelocity = Vector3.zero;
        Debug.Log("Start Lerp Move " + gameObject.name);
        SetLayerOnAll(gameObject, noCollisionLayer);
        GetComponent<Rigidbody>().useGravity = false;
        startTime = Time.time;
        moveStartPos = objectTransform.position;
        moveStartRotation = objectTransform.rotation;
        journeyLength = Vector3.Distance(moveStartPos, sceneStartPos);
        rotationLength = Vector3.Distance(moveStartRotation.eulerAngles, sceneStartRotation.eulerAngles);
        //Debug.Log(journeyLength);
        isMoving = true;
        isRotating = true;
        if (linkedObjects.Count > 0)
        {
            foreach(ReturnToPositionOntology linkedObject in linkedObjects)
            {
                Debug.Log("Start linked Lerp Move " + linkedObject.name);
                if (linkedObject.isMoving == false)
                {
                    linkedObject.StartLerpMove();
                }
                linkedMotion = true;
            }
           
        }
    }
    public void StopLerpMove()
    {
        isMoving = false;
        isRotating = false;
        SetLayerOnAll(gameObject, objectLayer);
        objectRigidbody.useGravity = objectUsesGravity;
        objectRigidbody.velocity = Vector3.zero;
        objectRigidbody.angularVelocity = Vector3.zero;
    }

    public static void SetLayerOnAll(GameObject go, int layerNumber)
    {
        if (go == null) return;
        foreach (Transform trans in go.GetComponentsInChildren<Transform>(true))
        {
            trans.gameObject.layer = layerNumber;
        }
    }
}
