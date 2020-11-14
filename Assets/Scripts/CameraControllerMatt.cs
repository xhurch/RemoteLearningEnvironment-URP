using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraControllerMatt : MonoBehaviour {

	public Transform[] views;
	public float transitionSpeed;
	Transform currentView;
	public Camera mainCamera;
	public Camera cameraViews;

	// Use this for initialization
	void Start () {
		mainCamera.enabled = true;
		cameraViews.enabled = false;
	}

	void Update(){

		if(Input.GetKeyDown(KeyCode.Alpha1)){
			currentView = views [0];
			cameraViews.enabled = true;
			mainCamera.enabled = false;
			

		}
		if (Input.GetKeyDown (KeyCode.Alpha2)){
			currentView = views [1];
		}
		if(Input.GetKeyDown(KeyCode.Alpha3)){
			currentView = views [2];

		}
		if (Input.GetKeyDown (KeyCode.Alpha4)){
			currentView = views [3];
		}
		if(Input.GetKeyDown(KeyCode.Alpha5)){
			currentView = views [4];

		}
		if (Input.GetKeyDown (KeyCode.Alpha6)){
			currentView = views [5];
		}
		if (Input.GetKeyDown (KeyCode.Alpha7)){
			currentView = views [6];
		}
		if(Input.GetKeyDown(KeyCode.Alpha8)){
			currentView = views [7];

		}
		if (Input.GetKeyDown (KeyCode.Alpha9)){
			currentView = views [8];
		}
		if (Input.GetKeyDown (KeyCode.Alpha0)){
			currentView = views [9];
		}

		if (Input.GetKeyDown(KeyCode.Tab))
		{
			mainCamera.enabled = !mainCamera.enabled;
			cameraViews.enabled = !cameraViews.enabled;
		}
	}
	
	// Update is called once per frame
	void LateUpdate () {
		transform.position = Vector3.Lerp(transform.position, currentView.position, Time.deltaTime * transitionSpeed);

		Vector3 currentAngle = new Vector3 (
			Mathf.LerpAngle(transform.rotation.eulerAngles.x, currentView.transform.rotation.eulerAngles.x, Time.deltaTime * transitionSpeed),
			Mathf.LerpAngle(transform.rotation.eulerAngles.y, currentView.transform.rotation.eulerAngles.y, Time.deltaTime * transitionSpeed),
			Mathf.LerpAngle(transform.rotation.eulerAngles.z, currentView.transform.rotation.eulerAngles.z, Time.deltaTime * transitionSpeed)
		);

		transform.eulerAngles = currentAngle;


		
	}
}
