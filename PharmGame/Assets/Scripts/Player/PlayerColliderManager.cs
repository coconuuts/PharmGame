using UnityEngine;
using System.Collections.Generic;
using System.Collections;

public class PlayerColliderManager : MonoBehaviour
{
    public static PlayerColliderManager Instance;
    public CustomTrigger stairTrigger;
    
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        stairTrigger.EnteredTrigger += OnStairTriggerEntered;
        stairTrigger.ExitedTrigger += OnStairTriggerExited;

    }

    void OnStairTriggerEntered(Collider2D collider)
    {
        if (collider.CompareTag("Stair"))
        {

        }
    }

    void OnStairTriggerExited(Collider2D collider)
    {
        if (collider.CompareTag("Stair"))
        {
            
        }
    }
}
