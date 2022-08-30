using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Collects and manages array of items which player is currently able to grab using multiple GrabZone controllers on subobjects (one system per hand).
/// </summary>
public class GrabSystem : MonoBehaviour
{
    //Classes, Enums & Structs:
    public enum GrabZoneType { Laser, Bubble }

    //Objects & Components:
    private HandController hand; //Hand script which this system sends information to
    private GrabZone[] grabZones;                    //Array of all grab zone controllers currently feeding item information into this system

    [SerializeField] private GrabZone laserBeam;     //Cylindrical part of Laser grab zone
    [SerializeField] private GrabZone laserEndClose; //Spherical part of Laser grab zone
    [SerializeField] private GrabZone laserEndFar;   //Spherical part of Laser grab zone
    [SerializeField] private GrabZone bubble;        //Bubble grab zone

    //Settings:
    [Header("Settings:")]
    [SerializeField, Tooltip("Multiplier applied to grab zone radius adjustment rate")] private float adjustmentMultiplier;
    [Header("Laser Settings:")]
    [SerializeField, Tooltip("Describes how analog input value maps to laser length")] private AnimationCurve laserLengthCurve;
    [SerializeField, Tooltip("Radius range of Laser grab zone")]                       private Vector2 laserRadiusRange;
    [SerializeField, Tooltip("Maxiumum length of Laser grab zone")]                    private float maxLaserLength;
    [Header("Bubble Settings:")]
    [SerializeField, Tooltip("Describes how analog input value maps to bubble distance and size")] private AnimationCurve bubbleCurve;
    [SerializeField, Tooltip("Radius range of Bubble grab zone")]                                  private Vector2 bubbleRadiusRange;
    [SerializeField, Tooltip("Max distance bubble is able to travel away from hand")]              private float maxBubbleDistance;

    //Runtime Vars:
    internal List<ItemController> hoverItems = new List<ItemController>(); //Unheld items which this hand is currently hovering over
    private GrabZoneType zoneType = GrabZoneType.Laser;                    //Determines which type of zone grab system is currently using (on this hand)
    private float currentZoneLength;                                       //Current length of grab zone (if applicable)
    private float currentZoneRadius;                                       //Current radius of grab zone (if applicable)

    private float minLaserLength;      //Minimum/default length of laser grab zone
    private float defaultLaserRadius;  //Default radius or Laser grab zone
    private float defaultBubbleRadius; //Default radius of Bubble grab zone

    internal bool isVisible = false; //Indicates whether grab visualizers are visible or not

    //RUNTIME METHODS:
    private void Start()
    {
        //Get objects & components:
        hand = GetComponentInParent<HandController>();           //Get hand controller from hierarchy
        hand.grabSystem = this;                                  //Send hand a reference to this script
        grabZones = GetComponentsInChildren<GrabZone>();         //Get all grab zone controllers under this system
        foreach (GrabZone zone in grabZones) zone.system = this; //Set this to system for all zones

        //Activate default grab zone:
        laserBeam.SetActive(true);     //Activate Laser grabZone
        laserEndClose.SetActive(true); //Activate Laser grabZone
        laserEndFar.SetActive(true);   //Activate Laser grabZone

        //Record default zone values:
        minLaserLength = laserEndFar.transform.localPosition.y;        //Get minimum/default length of Laser grab zone
        defaultLaserRadius = laserEndClose.transform.localScale.x / 2; //Get default radius of Laser grab zone
        defaultBubbleRadius = bubble.transform.localScale.x / 2;       //Get default radius of Bubble grab zone

        //Setup starting zone properties:
        currentZoneLength = minLaserLength;     //Set current length of laser to default setting
        currentZoneRadius = defaultLaserRadius; //Set current radius of laser to default setting
        ApplyZoneDimensions();                  //Set up zone dimensions
    }

    //MANAGEMENT METHODS:
    /// <summary>
    /// Called every time a GrabZone tries to add an item to the hoverItems list, adds it if not already in system list.
    /// </summary>
    public void TryAddItem(ItemController item)
    {
        if (item.grabImmunityTime > 0) return;                //Ignore item if it is currently immune to being grabbed
        if (!hoverItems.Contains(item)) hoverItems.Add(item); //Add item to hover list if it hasn't already been added by another grabZone
    }
    /// <summary>
    /// Called every time a GrabZone tries to remove an item from the hoverItems list.
    /// </summary>
    public void TryRemoveItem(ItemController item)
    {
        foreach (GrabZone zone in grabZones) //Iterate through each grabZone in system
        {
            if (!zone.active) continue;                 //Ignore inactive zones
            if (zone.hoverItems.Contains(item)) return; //Do not remove item if it is still present in another active zone
        }
        hoverItems.Remove(item); //Only remove item once it has been confirmed that it is no longer present in any active grabZones
    }
    /// <summary>
    /// Called when a hovered item is grabbed. Removes hovered item at given index from system list and local lists of all GrabZones (active and inactive).
    /// </summary>
    public void GrabItem(ItemController item)
    {
        //Initialize:
        if (!hoverItems.Contains(item)) return; //Ignore if given item is not currently hovered

        //Remove item from all lists:
        foreach (GrabZone zone in grabZones) if (zone.hoverItems.Contains(item)) zone.hoverItems.Remove(item); //Search through every zone in system and remove item from local list if found
        hoverItems.Remove(item);                                                                               //Remove item from system list
    }
    /// <summary>
    /// Returns an array of all hovered items, then clears them from all hover lists.
    /// </summary>
    public ItemController[] GrabAll()
    {
        ItemController[] items = hoverItems.ToArray();                //Save array of currently-hovered items
        foreach (GrabZone zone in grabZones) zone.hoverItems.Clear(); //Clear item list from every zone
        hoverItems.Clear();                                           //Clear system item list
        return items;                                                 //Return list of all hovered items
    }
    /// <summary>
    /// Should be called whenever something that might affect zone visibility is changed. Enables or disables zone visualizers depending on factors.
    /// </summary>
    public void CheckVisibilityState()
    {
        if (hand.player.mode == PlayerController.InteractionMode.Collection && //Interactions must be in collection mode
            hand.touchingGrab &&                                               //Player must be touching grab trigger
            !hand.targetSystem.stowed                                          //Target system must not be stowed
            ) ToggleVisibility(true); //Make system visible if it fulfills the given criteria
        else ToggleVisibility(false); //If criteria are not fulfilled, make system invisible
    }

    //FUNCTIONALITY METHODS:
    /// <summary>
    /// Increases or decreases grab zone radius by given amount, clamped by set range.
    /// </summary>
    public void AdjustZoneRadius(float amount)
    {
        //Initialize
        amount *= adjustmentMultiplier; //Apply multiplier to adjustment amount
        
        //Modify radius setting:
        switch (zoneType)
        {
            case GrabZoneType.Laser: //LASER: radius changes affect overall radius of beam
                currentZoneRadius = Mathf.Clamp(currentZoneRadius + amount, laserRadiusRange.x, laserRadiusRange.y); //Add amount to current radius and clamp based on size limits
                break;
            case GrabZoneType.Bubble: //BUBBLE: no effect
                break;
        }

        //Cleanup:
        ApplyZoneDimensions(); //Apply change to zone dimensions
    }
    /// <summary>
    /// Adjusts grab zone length to percentage of max length based on interpolant value.
    /// </summary>
    /// <param name="interpolant">Value between 0 and 1 representing percentage of max length.</param>
    public void AdjustZoneSize(float interpolant)
    {
        //Modify size setting:
        switch (zoneType) //Determine behavior based on current zone type
        {
            case GrabZoneType.Laser: //LASER: zone size changes affect length
                currentZoneLength = Mathf.Lerp(minLaserLength, maxLaserLength, laserLengthCurve.Evaluate(interpolant)); //Interpolate zone length setting according to Laser size limits (apply set curve for flavor)
                break;
            case GrabZoneType.Bubble: //BUBBLE: interpolated size change controls radius
                float bubbleInterpolant = bubbleCurve.Evaluate(interpolant);                                 //Get interpolant as evaluated by bubbleCurve
                currentZoneRadius = Mathf.Lerp(bubbleRadiusRange.x, bubbleRadiusRange.y, bubbleInterpolant); //Interpolate between min and max radii
                currentZoneLength = Mathf.Lerp(0, maxBubbleDistance, bubbleInterpolant);                     //Interpolate between min and max distance
                break;
        }

        //Cleanup:
        ApplyZoneDimensions(); //Apply change to zone dimensions
    }
    /// <summary>
    /// Changes grab zone to given type.
    /// </summary>
    public void SetZoneType(GrabZoneType newType)
    {
        //Initialization:
        if (newType == zoneType) return; //Ignore if new type is not changing

        //Deactivate old zone:
        switch (zoneType) //Determine which zone to deactivate
        {
            case GrabZoneType.Laser:
                //Deactivate zones:
                laserBeam.SetActive(false);     //Deactivate Laser grabZone
                laserEndClose.SetActive(false); //Deactivate Laser grabZone
                laserEndFar.SetActive(false);   //Deactivate Laser grabZone
                break;
            case GrabZoneType.Bubble:
                //Deactivate zones:
                bubble.SetActive(false); //Deactivate Bubble grabZone
                break;
        }

        //New zone initialization:
        switch (newType) //Determine initialization steps depending on new zone type
        {
            case GrabZoneType.Laser:
                //Activate zones:
                laserBeam.SetActive(true);     //Activate Laser grabZone
                laserEndClose.SetActive(true); //Activate Laser grabZone
                laserEndFar.SetActive(true);   //Activate Laser grabZone
                //Apply default values:
                currentZoneLength = minLaserLength;     //Set current zone length to laser default
                currentZoneRadius = defaultLaserRadius; //Set current zone radius to laser default
                break;
            case GrabZoneType.Bubble:
                //Activate zones:
                bubble.SetActive(true); //Activate Bubble grabZone
                //Apply default values:
                currentZoneLength = 0;                   //Set current zone length to zero
                currentZoneRadius = defaultBubbleRadius; //Set current zone radius to bubble default
                break;
        }

        //Cleanup:
        ApplyZoneDimensions(); //Apply dimensions for outgoing zone
        zoneType = newType;    //Set new type as current
        ApplyZoneDimensions(); //Apply dimensions for ingoing zone
    }
    /// <summary>
    /// Cycles system to next grab zone type.
    /// </summary>
    public void IndexZoneType()
    {
        int newTypeIndex = (int)zoneType + 1;    //Get incremented index from current zone
        if (newTypeIndex > 1) newTypeIndex = 0;  //Overflow to zero if index is greater than that of last GrabZone type (NOTE: update this number when adding new types)
        SetZoneType((GrabZoneType)newTypeIndex); //Set zoneType of incremented index as new type
    }
    /// <summary>
    /// Can be used to turn all grabZone renderers on or off.
    /// </summary>
    public void ToggleVisibility(bool visible)
    {
        if (visible == isVisible) return;                              //Ignore if new setting is redundant
        foreach (GrabZone zone in grabZones) zone.SetVisible(visible); //Set each zone visible or invisble (inactive zones will not be made visible)
        isVisible = visible;                                           //Record current visibility state of system
    }

    //UTILITY METHODS:
    private void ApplyZoneDimensions()
    {
        switch (zoneType) //Determine how to adjust dimensions based on zone type
        {
            case GrabZoneType.Laser: //LASER: zone is a long cylinder with spheres at either end, projecting out roughly from index finger
                //Apply new radii:
                Vector3 newScale = Vector3.one * (currentZoneRadius * 2); //Get target scale for spherical laser ends
                laserEndClose.transform.localScale = newScale;            //Scale end sphere
                laserEndFar.transform.localScale = newScale;              //Scale end sphere
                newScale.y = currentZoneLength / 2;                       //Change Y value to zone length for cylindrical beam
                laserBeam.transform.localScale = newScale;                //Scale beam

                //Apply new positions:
                float zOffset = -defaultLaserRadius + currentZoneRadius; //Get Z offset (which will preserve position of beam relative to fingers)
                Vector3 newPosition = new Vector3(0, 0, zOffset);        //Get position for closest sphere (only moves along Z axis within container)
                laserEndClose.transform.localPosition = newPosition;     //Move end sphere
                newPosition.y = currentZoneLength;                       //Get position for farthest sphere (moves along Y axis within container)
                laserEndFar.transform.localPosition = newPosition;       //Move end sphere
                newPosition.y /= 2;                                      //Divide Y position by 2 so that cylinder is positioned exactly between end spheres
                laserBeam.transform.localPosition = newPosition;         //Move beam
                break;
            case GrabZoneType.Bubble: //BUBBLE: zone is a single sphere touching the center of the palm, may be moved up from palm
                //Apply new radius:
                bubble.transform.localScale = Vector3.one * (currentZoneRadius * 2); //Scale bubble
                //Apply new position:
                float offset = defaultBubbleRadius - currentZoneRadius;     //Get initial Y offset (which will preserve position of bubble relative to palm)
                offset -= currentZoneLength;                                //Apply zone length to offset so that it pushes bubble away from hand
                bubble.transform.localPosition = new Vector3(0, offset, 0); //Move bubble
                break;
        }
    }
}
