using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class spinCamera : MonoBehaviour {

	public float rotateSpeed = .5f;

	void Update () {
		this.transform.RotateAround (Vector3.zero, Vector3.up, rotateSpeed);
		this.transform.LookAt (Vector3.zero);
	}
}
