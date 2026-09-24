using System;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Forwards pointer enter/exit on this UI element as C# events, so a parent
/// component can react to hover on a child (e.g. a "stats" icon) without
/// that child needing its own bespoke script. Needs a raycast-target Graphic
/// on the same object.
/// </summary>
public class PointerHoverRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public event Action Entered;
    public event Action Exited;

    public void OnPointerEnter(PointerEventData eventData) => Entered?.Invoke();
    public void OnPointerExit(PointerEventData eventData) => Exited?.Invoke();

    // Pointer-exit never fires if the element is hidden while hovered.
    private void OnDisable() => Exited?.Invoke();
}
