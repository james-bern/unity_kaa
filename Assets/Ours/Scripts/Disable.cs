using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Disable : MonoBehaviour
{

    // Update is called once per frame
    void Update()
    {
         bool rightHandDelete, leftHandDelete;{
            bool value;
            rightHandDelete = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand).TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out value) && value;
            leftHandDelete  = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand ).TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out value) && value;
        }
        if(rightHandDelete || leftHandDelete){
            foreach(Transform child in transform) {
                if(child.name == "Lines") {
                    foreach(Transform child2 in child){
                        if(child2.GetComponent<LinePos>().head != null) child2.GetComponent<LinePos>().head.SetActive(false);
                    }
                }
            }
            gameObject.SetActive(false);
        }
    }
}
