using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Picks up OnTriggerEnter calls for hoverable items and passes information to unifying GrabSystem. Collider dimensions are flexible.
/// </summary>
public class GrabZone : MonoBehaviour
{
    //Objects & Components:
    internal GrabSystem system; //System which this zone is sending information to
    private Renderer rd;        //Renderer component on this object

    //Runtime Vars:
    internal List<ItemController> hoverItems = new List<ItemController>(); //Items this specific zone is currently hovering over
    internal bool active = false;                                          //While true, this zone will actively report on Trigger calls (inactive zones still keep track of local hoverItems)

    //RUNTIME METHODS:
    private void Start()
    {
        //Get objects & components:
        rd = GetComponent<Renderer>(); //Get zone renderer

        //Setup:
        rd.enabled = false; //Deactivate renderer on start
    }
    private void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Item")) //Check if object is on Item layer
        {
            //Get item controller:
            ItemController hoveredItem = other.GetComponentInParent<ItemController>();                             //Get item controller (should be parent of collider)
            if (hoveredItem == null) hoveredItem = other.GetComponent<ItemController>();                           //Retry if collider is on item level
            if (hoveredItem == null) { Debug.LogError("Could not find ItemController on " + other.name); return; } //Abort if item controller could not be found

            //Check hover validity:
            if (hoveredItem.isHeld) return;               //Ignore if item is currently being held
            if (hoverItems.Contains(hoveredItem)) return; //Ignore if item is already being hovered over by this zone
            if (hoveredItem.grabImmunityTime > 0) return; //Ignore if item is temporarily immune to being hovered (deprecated)

            //Add item:
            hoverItems.Add(hoveredItem);                //Add item to local list
            if (active) system.TryAddItem(hoveredItem); //Add hovered item to system list if active
        }
    }
    private void OnTriggerExit(Collider other)
    {
        if (other.gameObject.layer == LayerMask.NameToLayer("Item")) //Check if object is on item layer
        {
            //Get item controller:
            ItemController hoveredItem = other.GetComponentInParent<ItemController>();                             //Get item controller (should be parent of collider)
            if (hoveredItem == null) hoveredItem = other.GetComponent<ItemController>();                           //Retry if collider is on item level
            if (hoveredItem == null) { Debug.LogError("Could not find ItemController on " + other.name); return; } //Abort if item controller could not be found

            //Remove item:
            if (hoverItems.Contains(hoveredItem)) //Only remove item if it was previously detected by this zone
            {
                hoverItems.Remove(hoveredItem);                //Remove item from local list
                if (active) system.TryRemoveItem(hoveredItem); //Remove hovered item from system list if active
            }
        }
    }

    //FUNCTIONALITY METHODS:
    /// <summary>
    /// Activates or deactivates zone item reporting.
    /// </summary>
    public void SetActive(bool yes)
    {
        //Initialize:
        if (active == yes) return;  //Ignore if activation state is not changing
        active = yes;               //Set new activation state
        if (system == null) return; //Ignore rest if zone is not yet connected with system (may happen at start of runtime)

        //State change triggers:
        if (active) //Zone has just become active
        {
            rd.enabled = system.isVisible;                                       //Set renderer enablement to system visibility
            foreach (ItemController item in hoverItems) system.TryAddItem(item); //Try to immediately add all locally-hovered items to system list
        }
        else //Zone has just become inactive
        {
            rd.enabled = false;                                                     //Deactivate renderer
            foreach (ItemController item in hoverItems) system.TryRemoveItem(item); //Try to immediately remove all locally-hovered items from system list
        }
    }
    /// <summary>
    /// Hides or unhides zone renderer (does not unhide if zone is inactive).
    /// </summary>
    public void SetVisible(bool yes)
    {
        if (yes && active) rd.enabled = true; //Unhide if active
        if (!yes) rd.enabled = false;         //Hide
    }
}
