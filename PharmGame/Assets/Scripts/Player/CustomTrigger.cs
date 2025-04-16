using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class CustomTrigger : MonoBehaviour
{
    public event System.Action<Collider2D> EnteredTrigger;
    public event System.Action<Collider2D> ExitedTrigger;
    
    void OnTriggerEnter2D(Collider2D collider)
    {
        EnteredTrigger?.Invoke(collider);

    }
    void OnTriggerExit2D(Collider2D collider)
    {
        ExitedTrigger?.Invoke(collider);
    }
}
