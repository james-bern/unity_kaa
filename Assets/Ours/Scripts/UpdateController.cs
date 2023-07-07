using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UpdateController : MonoBehaviour
{

    public GameObject snake;

    void OnEnable()
    {
        snake.transform.GetComponent<snake>().isUpdating = true;
    }

    void OnDisable() {
        snake.transform.GetComponent<snake>().isUpdating = false;
    }
}
