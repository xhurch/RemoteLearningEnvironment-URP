using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Shatter : MonoBehaviour
{

	public GameObject target;
	public AudioSource shatterSound;
	public GameObject shatter;


	void OnTriggerEnter(Collider other)
	{
		if (other.gameObject.tag == "Untagged")
		{
			Debug.Log("hit");
			target.gameObject.SetActive(false);
			shatter.gameObject.SetActive(true);
			shatterSound.Play();
		}
	}
}
