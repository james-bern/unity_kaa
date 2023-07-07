using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// TODO (Austin): Rename this class Node

public class scale_with_distance : MonoBehaviour

{
    public Camera mainCam;
    public float dist;
    public float distScale;

    void Update() {

        { // Clamp position and zero velocity
            GetComponent<Rigidbody>().velocity = new Vector3(0.0f, 0.0f, 0.0f);
            Vector3 new_pos = new Vector3(
                Mathf.Clamp(transform.position.x, -2.0f, 2.0f),
                Mathf.Clamp(transform.position.y, -2.0f, 2.0f), 
                Mathf.Clamp(transform.position.z, -2.0f, 2.0f)
                );

            transform.position = new_pos;
        }
    }
}