using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

/// <summary>
/// A custom ScrollRect that disables mouse/touch dragging 
/// but preserves scroll wheel and scrollbar functionality.
/// </summary>
public class NoDragScrollRect : ScrollRect
{
    // We override these methods with empty bodies to "silence" drag inputs.
    
    public override void OnBeginDrag(PointerEventData eventData) 
    {
        // Do nothing - prevents the start of a drag gesture
    }

    public override void OnDrag(PointerEventData eventData) 
    {
        // Do nothing - prevents movement during a drag gesture
    }

    public override void OnEndDrag(PointerEventData eventData) 
    {
        // Do nothing - prevents the release logic of a drag gesture
    }
}