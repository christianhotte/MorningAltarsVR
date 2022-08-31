using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ItemTargetSystem : MonoBehaviour
{
    //Classes, Structs & Enums:
    public enum ItemArrayType { Linear, Circular }

    //Objects & Components:
    public Transform stowPosition; //Position where stowed objects are held
    private HandController hand;   //Hand script which this system works for

    //Settings:
    [Header("General Settings:")]
    [SerializeField, Range(0, 1), Tooltip("Determines how quickly held items snap to target positions")] private float itemSnapFactor;
    [SerializeField, Tooltip("Affects the rate wat which player can rotate array or individual item")]   private float rotationAdjustMultiplier;
    [Header("Placement Settings:")]
    [SerializeField, Tooltip("Maximum distance from which the player may place held items")]                                 public float maxPlacementDistance;
    [SerializeField, Tooltip("Layers which will be ignored when finding placement position")]                                public LayerMask placementIgnoreMask;
    [SerializeField, Tooltip("Height used to procedurally determine positions of placed items (maximum terrain roughness)")] public float placementHeightOffset;
    [Header("Stow Settings:")]
    [SerializeField, Tooltip("Amount of time taken for stow effects to fully activate or dissapate")]                          private float stowTime;
    [SerializeField, Tooltip("Curve describing transparency and scale of stowed objects as they travel toward stow location")] private AnimationCurve stowEffectCurve;
    [SerializeField, Tooltip("Minimum scale objects reach when being stowed")]                                                 private float minStowScale;
    [Header("Linear Array Settings:")]
    [SerializeField, Tooltip("Base distance between objects in linear array")]                private float defaultLinearSeparation;
    [SerializeField, Tooltip("Allowed range in distances between objects in linear array")]   private Vector2 linearSeparationRange;
    [SerializeField, Tooltip("Affects the speed at which linear separation can be adjusted")] private float linearAdjustMultiplier;
    [SerializeField, Tooltip("Multiplier applied to linear offset adjustment input")]         private float offsetAdjustMultiplier;
    [SerializeField, Tooltip("Maximum allowed velocity when linear array is sliding")]        private float maxLinearVelocity;
    [Header("Circular Array Settings:")]
    [SerializeField, Tooltip("Base radius of circular array")]                                                      private float defaultCircularRadius;
    [SerializeField, Tooltip("Allowed range in distances between objects in circular array")]                       private Vector2 circularRadiusRange;
    [SerializeField, Tooltip("Affects the speed at which circle radius can be adjusted")]                           private float radiusAdjustMultiplier;
    [SerializeField, Tooltip("Maximum allowed velocity (in degrees per second) when circular array is being spun")] private float maxRadialVelocity;

    //Runtime Vars:
    internal ItemArrayType currentArrayType = ItemArrayType.Circular; //Current shape of held item arrangement
    private List<Transform> slots = new List<Transform>();            //List of generated positions for held objects
    internal ItemController selectedItem;                             //Item (if any) currently selected by target system
    private Transform activeGhost;                                    //Ghost item actively being used for secondary manipulation
    internal Transform[] placementGhosts = new Transform[0];          //Array of item ghosts which indicate where items will appear during placement
    private int[] manipulatedSlots;                                   //Array of slot indexes for items which are being manipulated by active ghost

    internal bool freshArray = true;       //True while current array has not been modified in Manipulation mode
    internal bool stowed = false;          //Indicates whether or not items are actually stowed (does not set stow state)
    private float currentArrayRotation;    //Current angular rotation of entire array
    private float currentArrayOffset;      //Current linear offset of array (along X axis)
    private float currentLinearSeparation; //Current distance by which objects in linear array are separated
    private float currentCircularRadius;   //Current radius of circular array
    private float stowTimeTracker;         //Tracks time passed since last change in stow state
    private int ghostsHidden = 0;          //The number of placement ghosts which are currently hidden

    private float recordedLinearSeparation; //Last recorded linear separation during Manipulation mode
    private float recordedCircularRadius;   //Last recorded circular radius during Manipulation mode
    private float scaleCorrection;          //Multiply values by this to correct for scaling when converting measurements on childed transforms to those of non-childed transforms

    //RUNTIME METHODS:
    private void Awake()
    {
        //Set up runtime variables:
        currentLinearSeparation = defaultLinearSeparation; //Set linear separation value
        currentCircularRadius = defaultCircularRadius;     //Set circular radius value
        scaleCorrection = transform.parent.localScale.x;   //Get scale correction value
    }
    private void Start()
    {
        //Get objects & components:
        hand = GetComponentInParent<HandController>(); //Get hand controller from hierarchy
        hand.targetSystem = this;                      //Send hand a reference to this script

        //Event subscriptions:
        PlayerController.main.onModeChanged += OnInteractionModeChanged; //Subscribe to interactionmode change event
        hand.onDualManipulationBegin += OnDualManipulationBegin;         //Subscribe to dual manipulation begin event
        hand.onDualManipulationEnd += OnDualManipulationEnd;             //Subscribe to dual manipulation end event
    }
    private void OnDisable()
    {
        //Event unsubscriptions:
        PlayerController.main.onModeChanged -= OnInteractionModeChanged; //Unsubscribe from interactionmode change event
        hand.onDualManipulationBegin -= OnDualManipulationBegin;         //Unsubscribe from dual manipulation begin event
        hand.onDualManipulationEnd -= OnDualManipulationEnd;             //Unsubscribe from dual manipulation end event
    }
    private void FixedUpdate()
    {
        //Update stow state:
        stowTimeTracker = Mathf.Min(stowTimeTracker + Time.fixedDeltaTime, stowTime);   //Update stow time tracker
        bool prevStowed = stowed;                                                       //Save stow state before check
        if (hand.holdingMove || hand.isSecondary || hand.placementReady) stowed = true; //If conditions are met, items are stowed
        else stowed = false;                                                            //Otherwise, they are not
        if (stowed != prevStowed) //Stow state has changed
        {
            //State triggers:
            hand.grabSystem.CheckVisibilityState(); //Update grabSystem visibility state on any stow state change
            if (stowed) //System has just been stowed
            {
                
            }
            else //System has just been un-stowed
            {

            }

            //Cleanup:
            stowTimeTracker = 0; //Reset time tracker
        }
        float stowInterpolant = stowTimeTracker / stowTime; //Get linear interpolant value representing stow progress

        //Move slots:
        List<Vector3> targets = new List<Vector3>();                     //List of target local positions for slots to seek
        for (int i = 0; i < slots.Count; i++) targets.Add(Vector3.zero); //Populate targets list with vectors at zero
        if (stowed) //Items are currently stowed
        {
            stowInterpolant = stowEffectCurve.Evaluate(stowInterpolant); //Apply animation curve to interpolant value
            for (int i = 0; i < targets.Count; i++) //Iterate through list of targets
            {
                targets[i] = stowPosition.localPosition;                                          //Change all targets to stowed position
                slots[i].localScale = Mathf.Lerp(1, minStowScale, stowInterpolant) * Vector3.one; //Adjust scale of stowed objects
            }
        }
        else //Items are not stowed
        {
            //Undo stow effect:
            stowInterpolant = stowEffectCurve.Evaluate(1 - stowInterpolant); //Get interpolant for determining how stowed items should be
            for (int i = 0; i < slots.Count; i++) //Iterate through list of slots
            {
                slots[i].localScale = Mathf.Lerp(1, minStowScale, stowInterpolant) * Vector3.one; //Adjust scale of held objects
            }

            //Array arrangement:
            if (slots.Count > 1) //There are two or more slots to arrange
            {
                //Arrange items depending on array type:
                switch (currentArrayType) //Determine slot targets based on array type
                {
                    case ItemArrayType.Linear: //Objects are arrayed in a line with a given separation between each item
                        float setback = (currentLinearSeparation * (targets.Count - 1)) / 2; //Get value used to center array on hand
                        setback += currentArrayOffset;                                       //Offset entire array using setback
                        for (int i = 0; i < targets.Count; i++) //Iterate through list of target vectors
                        {
                            Vector3 targetVector = new Vector3((i * currentLinearSeparation) - setback, 0, 0); //Space each target out with linear separation and move back to center on hand
                            targets[i] = targetVector;                                                         //Insert computed target position into list
                        }
                        break;
                    case ItemArrayType.Circular: //Objects are arrayed around a circle with a given radius
                        for (int i = 0; i < targets.Count; i++) //Iterate through list of target vectors
                        {
                            Vector3 targetVector = new Vector3(currentCircularRadius, 0, 0);                  //Move each target away from center point by the same value
                            targetVector = Quaternion.Euler(0, i * (360f / targets.Count), 0) * targetVector; //Rotate target by angle determined by position in array
                            targets[i] = targetVector;                                                        //Insert computed target position into list
                        }
                        break;
                }

                //Rotate array uniformly:
                for (int i = 0; i < targets.Count; i++) //Iterate through each vector in target array
                {
                    targets[i] = Quaternion.Euler(0, currentArrayRotation, 0) * targets[i]; //Rotate each target around center point by given array rotation
                }
            }
        }
        for (int i = 0; i < targets.Count; i++) //Iterate through list of target positions
        {
            slots[i].localPosition = Vector3.Lerp(slots[i].localPosition, targets[i], itemSnapFactor); //Move slots toward targets at given interpolation rate
        }

        //Snap to selected item:
        UpdateSelectedItem();       //Update to determine which item is selected (if any)
        if (slots.Count > 1 &&      //Items are in an array
            selectedItem != null && //An item is selected
            hand.arrayShift == 0)   //Player is not currently manipulating selection
        {
            //Snap to selected object:
            int selectedIndex = slots.IndexOf(GetSlotFromItem(selectedItem)); //Get index of slot holding selected item
            switch (currentArrayType) //Determine array motion based on array type
            {
                case ItemArrayType.Linear:
                    float maxOffset = (slots.Count - 1) * currentLinearSeparation / 2;                                            //Get maximum possible offset in one direction
                    float targetOffset = Mathf.Lerp(-maxOffset, maxOffset, Mathf.InverseLerp(0, slots.Count - 1, selectedIndex)); //Use selected index to interpolate offset which positions slot in center
                    currentArrayOffset = Mathf.Lerp(currentArrayOffset, targetOffset, itemSnapFactor);                            //Lerp array offset to position item in center
                    break;
                case ItemArrayType.Circular:
                    float targetAngle = -selectedIndex * (360 / slots.Count) - 90;                             //Get angle which will position the item over the center of the array
                    currentArrayRotation = Mathf.LerpAngle(currentArrayRotation, targetAngle, itemSnapFactor); //Lerp array rotation to correct for relative angle of item in array
                    break;
            }
        }

        //Do secondary item manipulation:
        if (hand.player.mode == PlayerController.InteractionMode.Manipulation && currentArrayType == ItemArrayType.Circular && hand.OtherHand().isSecondary && slots.Count > 1 && selectedItem != null) //Ghost should be present
        {
            //Update ghost:
            if (activeGhost == null || activeGhost.name != selectedItem.ghost.name) //A new ghost needs to be made
            {
                //Instantiate ghost:
                if (activeGhost != null) Destroy(activeGhost.gameObject);                                             //Destroy previous ghost if applicable
                activeGhost = Instantiate(selectedItem.ghost).transform;                                              //Instantiate ghost from selected item
                activeGhost.parent = transform;                                                                       //Child ghost to this system
                activeGhost.SetPositionAndRotation(selectedItem.transform.position, selectedItem.transform.rotation); //Set position and rotation of ghost to match that of originator item
                activeGhost.name = selectedItem.ghost.name;                                                           //Make sure ghost is traceable via name back to its originator object

                //Get list of like items:
                List<int> ghostedSlotList = new List<int>(); //Initialize list to store found slots containing items matching ghosted item
                for (int i = 0; i < slots.Count; i++) //Iterate through list of all slots
                {
                    if (GetItemFromSlot(slots[i]).ghost.name == activeGhost.name) ghostedSlotList.Add(i); //Add items with matching ghosts (meaning they come from the same prefab) to slot index list
                }
                manipulatedSlots = ghostedSlotList.ToArray(); //Save list of indeces as array
            }
            else //Active ghost from last update was valid
            {
                //Update ghost orientation
                activeGhost.rotation = Quaternion.Slerp(activeGhost.rotation, hand.OtherHand().transform.rotation, itemSnapFactor); //Smoothly rotate ghost toward rotation target
                activeGhost.localPosition = Vector3.Lerp(activeGhost.localPosition, Vector3.zero, itemSnapFactor); //Move ghost toward center of system
            }

            //Modify orientation:
            if (hand.OtherHand().doingSecondaryManipulation) //Other hand is actively manipulating ghost
            {
                foreach (int index in manipulatedSlots) //Iterate through array of manipulated slots
                {
                    int adjustedIndex = index - slots.IndexOf(GetSlotFromItem(selectedItem)); if (adjustedIndex < 0) adjustedIndex += slots.Count; //Get adjusted index value which treats selected item as first item in list
                    Quaternion targetRotation = Quaternion.AngleAxis(adjustedIndex * (360f / slots.Count), transform.up) * activeGhost.rotation;   //Get target rotation which arrays items radially
                    slots[index].rotation = Quaternion.Slerp(slots[index].rotation, targetRotation, itemSnapFactor);                               //Rotate all similar items to match orientation of ghost
                }
            }
        }
        else if (activeGhost != null) //There should be no ghost, but there is
        {
            Destroy(activeGhost.gameObject); //Destroy ghost immediately
            activeGhost = null;              //Remove reference to destroyed ghost
        }
        if (hand.player.mode == PlayerController.InteractionMode.Manipulation && currentArrayType == ItemArrayType.Linear && hand.OtherHand().doingSecondaryManipulation && slots.Count > 1 && selectedItem != null) //Secondary manipulation of linear array
        {
            //Update selected item orientation:
            selectedItem.transform.parent.rotation = Quaternion.Slerp(selectedItem.transform.parent.rotation, hand.OtherHand().transform.rotation, itemSnapFactor); //Smoothly rotate item toward target

            //Update item orientation:
            foreach (Transform slot in slots) //Iterate through each item in slot list
            {
                if (GetItemFromSlot(slot).ghost == selectedItem.ghost) slot.rotation = Quaternion.Slerp(slot.rotation, selectedItem.transform.parent.rotation, itemSnapFactor); //Rotate all like items according to selected item
            }
        }
    }

    private void Update()
    {
        //Update placement ghost positions:
        if (placementGhosts.Length > 0) //There are placement ghosts to manage
        {
            if (hand.placementReady && Physics.Raycast(transform.position, transform.forward, out RaycastHit hitInfo, maxPlacementDistance, ~placementIgnoreMask)) //Hand is in placement mode and a valid placement point is within range
            {
                //Move ghosts to position:
                if (slots.Count == 1) //There is currently only one item in array
                {
                    if (!placementGhosts[0].gameObject.activeSelf) //Ghost was not active on previous update
                    {
                        placementGhosts[0].gameObject.SetActive(true); //Make ghost visible
                        ghostsHidden--;                                //Indicate that a ghost has been un-hidden
                        placementGhosts[0].position = hitInfo.point;   //Simply snap ghost to current target (because it has effectively just been spawned)
                    }
                    else //Ghost was active last update
                    {
                        placementGhosts[0].position = Vector3.Lerp(placementGhosts[0].position, hitInfo.point, itemSnapFactor); //Lerp ghost toward target
                    }
                    placementGhosts[0].rotation = slots[0].localRotation; //Orient ghost to slot rotation
                }
                else //Placement protocol for array
                {
                    //Get initial values:
                    List<Vector3> targetPoints = new List<Vector3>();                              //Initialize list of points which ghosts will home toward
                    Vector3 surfaceNormal = hitInfo.normal;                                        //Get normal of surface that was hit
                    Vector3 surfaceRight = transform.right;                                        //Initialize vector representing right direction relative to surface normal (currently relative to hand orientation
                    Vector3 surfaceUp = transform.up * (hand.isLeft ? -1 : 1);                     //Initialize vector representing up direction relative to surface normal (currently relative to hand orientation)
                    Vector3.OrthoNormalize(ref surfaceNormal, ref surfaceRight, ref surfaceUp);    //Orthogonalize hand directions onto surface plane so that they are useful references for converting from local system space to world space
                    Vector3 centerPoint = hitInfo.point + (surfaceNormal * placementHeightOffset); //Get center point of speculative array which items will be raycasting from

                    //Determine speculative item locations:
                    if (currentArrayType == ItemArrayType.Circular) //Simulate circular array target placement
                    {
                        Vector3 startPoint = surfaceRight * currentCircularRadius; //Initialize all points at world zero, offset in upward direction (relative to hit point normal) by circular radius (with scaling correction)
                        startPoint *= scaleCorrection;                             //Apply scaling correction
                        float indexAngle = 360f / slots.Count;                     //Get angular offset for each consecutive index
                        for (int i = 0; i < slots.Count; i++) //Iterate through each slot in system
                        {
                            float a = (i * indexAngle + currentArrayRotation) * (hand.isLeft ? -1 : 1); //Determine angle at which item should be offset, factoring in index position and current array rotation (flip if using left hand)
                            Vector3 newPoint = Quaternion.AngleAxis(a, surfaceNormal) * startPoint;     //Initialize new point as starting point rotated depending on slot position in array
                            targetPoints.Add(newPoint + centerPoint);                                   //Move point to position of speculative array and add to list
                        }
                    }
                    else if (currentArrayType == ItemArrayType.Linear) //Simulate linear array target placement
                    {
                        float setback = (currentLinearSeparation * (slots.Count - 1)) / 2; //Calculate setback based on math used to place normal linear array targets
                        float a = currentArrayRotation * (hand.isLeft ? -1 : 1);           //Get rotation angle to apply to each item (flip for left hand)
                        for (int i = 0; i < slots.Count; i++) //Iterate through each slot in system
                        {
                            Vector3 newPoint = surfaceRight * ((i * currentLinearSeparation) - setback); //Initialize point at world zero and offset plane-aligned x value by setback and linear separation (centering array)
                            newPoint *= scaleCorrection;                                                 //Apply scaling correction
                            newPoint = Quaternion.AngleAxis(a, surfaceNormal) * newPoint;                //Rotate point around world zero to match current array rotation
                            targetPoints.Add(newPoint + centerPoint);                                    //Move point to position of speculative array and add to list
                        }
                    }

                    //Move ghost toward target positions:
                    for (int i = 0; i < targetPoints.Count; i++) //Iterate through list of speculative points
                    {
                        if (Physics.Raycast(targetPoints[i], -surfaceNormal, out RaycastHit targetHit, placementHeightOffset * 2, ~placementIgnoreMask)) //This item has a valid location to be placed
                        {
                            //Get target values:
                            Vector3 currentTarget = targetHit.point + (surfaceNormal * GetItemFromSlot(slots[i]).placementOffset); //Get target at found surface, optionally offset by corresponding item size parameter

                            //Move ghost toward target:
                            if (!placementGhosts[i].gameObject.activeSelf) //Ghost was not active on previous update
                            {
                                placementGhosts[i].gameObject.SetActive(true); //Make ghost visible
                                ghostsHidden--;                                //Indicate that a ghost has been un-hidden
                                placementGhosts[i].position = hitInfo.point;   //Simply snap ghost to current target (because it has effectively just been spawned)
                            }
                            else //Ghost was active last update
                            {
                                placementGhosts[i].position = Vector3.Lerp(placementGhosts[i].position, currentTarget, itemSnapFactor); //Lerp ghost toward target
                            }
                            placementGhosts[i].rotation = Quaternion.LookRotation(surfaceUp, surfaceNormal * (hand.isLeft ? -1 : 1)) * slots[i].localRotation; //Apply rotation which maps local rotation of slots into local tangent space (flip for left hand)
                        }
                        else if (placementGhosts[i].gameObject.activeSelf) //Ghost cannot be placed but is currently active
                        {
                            placementGhosts[i].gameObject.SetActive(false); //Hide ghost
                            ghostsHidden++;                                 //Indicate that a ghost has been hidden
                        }
                    }
                }
            }
            else //Ghosts should be hidden
            {
                //Hide active ghosts:
                if (ghostsHidden < placementGhosts.Length) //At least one ghost is still visible
                {
                    foreach (Transform ghost in placementGhosts) ghost.gameObject.SetActive(false); //Make each ghost inactive
                    ghostsHidden = placementGhosts.Length;                                          //Indicate that all ghosts are hidden
                }
            }
        }
    }

    //EVENTS:
    private void OnInteractionModeChanged(PlayerController.InteractionMode newMode)
    {
        if (currentArrayType == ItemArrayType.Linear) //Mode change triggers for linear array
        {
            //Two-way mode switch triggers:
            currentArrayOffset = 0; //Zero out linear array offset when changing modes

            //One-way mode switch triggers:
            if (newMode == PlayerController.InteractionMode.Collection) //System is switching into collection mode
            {
                currentArrayRotation = 0; //Reset array rotation to zero (to ensure linear array is locked in sideways alignment)
            }
        }
    }
    private void OnDualManipulationBegin()
    {
        if (currentArrayType == ItemArrayType.Linear) //Beginning dual manipulation of a linear array
        {
            recordedLinearSeparation = currentLinearSeparation; //Record current linear separation
            currentArrayRotation = 0;                           //Reset array rotation
            ApplyItemBounds();                                  //Reset array size
        }
    }
    private void OnDualManipulationEnd()
    {
        if (currentArrayType == ItemArrayType.Linear) //Ending dual manipulation of a linear array
        {
            currentArrayOffset = 0; //Reset array offset
        }
    }

    //FUNCTIONALITY METHODS:
    /// <summary>
    /// Switches between circular and linear array types.
    /// </summary>
    public void SwitchArrayType()
    {
        //Toggle array type:
        switch (currentArrayType) //Determine switching behavior based on current array
        {
            case ItemArrayType.Circular: //Array is currently circular
                currentArrayType = ItemArrayType.Linear; //Switch to linear array
                break;
            case ItemArrayType.Linear: //Array is currently linear
                currentArrayType = ItemArrayType.Circular; //Switch to circular array
                break;
        }

        //Cleanup:
        currentArrayRotation = 0; //Reset array rotation
        currentArrayOffset = 0;   //Reset array offset
        ApplyItemBounds();        //Set array size based on adjacent item bounds
    }
    /// <summary>
    /// Adds array slot to target system (added item will be placed in selected position).
    /// </summary>
    public Transform AddSlot(ItemController item)
    {
        //Generate new slot:
        Transform newSlot = new GameObject().transform; //Generate new physical object
        newSlot.name = "Slot " + (slots.Count + 1);     //Name slot for tracking purposes
        
        //Position new slot:
        newSlot.parent = transform;       //Child slot to this object
        newSlot.localScale = Vector3.one; //Make sure scale is normalized       
        if (item != null) //Slot was added for specific item
        {
            newSlot.position = item.transform.position; //Match slot position to item world position
            newSlot.rotation = item.transform.rotation; //Match slot rotation to item world rotation
            item.transform.parent = newSlot;            //Child item to slot
        }
        else //Empty slot was added
        {
            if (slots.Count > 0) newSlot.localPosition = slots[slots.Count - 1].localPosition; //Spawn slot at position of latest slot in array
            else newSlot.localPosition = Vector3.zero;                                         //If this is the first slot in the array, just spawn it at local zero
        }

        //Cleanup:
        if (slots.Count > 1 && selectedItem != null) slots.Insert(slots.IndexOf(GetSlotFromItem(selectedItem)), newSlot); //Insert item at index of currently selected item if possible
        else slots.Add(newSlot);                                                                                          //Otherwise, simply add to list
        ResetSystemState();                                                                                               //Indicate that array is new
        return newSlot;                                                                                                   //Return generated position
    }
    /// <summary>
    /// Removes specific slot from target system array.
    /// </summary>
    public void RemoveSlot(Transform slot)
    {
        //Remove slot:
        if (!slots.Contains(slot)) return;           //Cancel if given transform is not a designated slot
        ItemController item = GetItemFromSlot(slot); //Retrieve item controller before destroying slot
        slots.Remove(slot);                          //Remove slot from list
        Destroy(slot.gameObject);                    //Destroy slot object

        //Slot quantity triggers:
        if (slots.Count < 2) //Removing this slot results in non-array
        {
            currentArrayRotation = 0; //Reset array rotation value
        }

        //Cleanup:
        if (item != null && item == selectedItem) //Removed item exists and was selected
        {
            selectedItem = null; //Clear reference to item
            item.UnSelect();     //Indicate to item that it is no longer selected
        }
        ResetSystemState(); //Indicate that array has been modified
    }
    /// <summary>
    /// Removes every slot in system.
    /// </summary>
    public void RemoveAllSlots()
    {
        while (slots.Count > 0) RemoveSlot(slots[0]); //Remove first slot until all slots are gone
    }
    /// <summary>
    /// Moves array contextually to change which item is currently focused.
    /// </summary>
    public void ShiftArray(float addedValue)
    {
        //Validity checks:
        if (slots.Count < 2) return; //Ignore if there is no item array
        if (addedValue == 0) return; //Ignore if 0 force was passed for some reason

        //Apply force:
        switch (currentArrayType) //Determine how to limit velocity based on current array type
        {
            case ItemArrayType.Linear:
                currentArrayOffset += addedValue * offsetAdjustMultiplier;                   //Add value to current offset
                float maxOffset = (currentLinearSeparation * (slots.Count - 1)) / 2;         //Get value for maximum allowed X offset
                currentArrayOffset = Mathf.Clamp(currentArrayOffset, -maxOffset, maxOffset); //Clamp offset value between calculated limits
                break;
            case ItemArrayType.Circular:
                RotateArray(addedValue); //Rotate array based on given value
                break; 
        }
    }
    /// <summary>
    /// Rotates slot containing given item by given value along two axes.
    /// </summary>
    public void RotateItem(ItemController item, Vector2 value)
    {
        //Initialize:
        Transform slot = GetSlotFromItem(item);                  //Get the slot this item is currently in
        if (slot == null) return;                                //Ignore if given item is not in a slot
        if (hand.OtherHand().doingSecondaryManipulation) return; //Ignore if item is being manipulated by other hand

        //Rotate item:
        value *= rotationAdjustMultiplier;                                  //Apply manipulation multiplier to value
        slot.RotateAround(transform.position, transform.forward, -value.x); //Use X axis to roll item left or right
        slot.RotateAround(transform.position, transform.right, value.y);    //Use Y axis to roll item forward or backward
    }
    /// <summary>
    /// Smoothly rotates entire array according to given value.
    /// </summary>
    public void RotateArray(float addedValue)
    {
        //Initialize:
        if (addedValue == 0) return;                                 //Ignore if given value is zero
        //if (activeGhost != null) return;                             //Ignore if there is an active ghost
        float adjustedValue = addedValue * rotationAdjustMultiplier; //Apply multiplier to given adjustment value

        //Modify overall array rotation:
        currentArrayRotation += adjustedValue;                        //Apply value directly to array rotation
        if (currentArrayRotation >= 360) currentArrayRotation -= 360; //Overflow angle if necessary
        if (currentArrayRotation < 0) currentArrayRotation += 360;    //Underflow angle if necessary

        //Correct rotation of individual slots:
        foreach (Transform slot in slots) //Iterate through list of slots
        {
            slot.RotateAround(slot.position, transform.up, adjustedValue); //Counter-rotate each array element to preserve relative rotation
        }
    }
    /// <summary>
    /// Increases or decreases size of item array (exact effect depends on array type) according to given value.
    /// </summary>
    public void AdjustArraySize(float value)
    {
        //Initialize:
        if (slots.Count < 2) return;     //Ignore if there is no array to adjust

        //Change array size:
        switch (currentArrayType) //Determine variable to change based on current array type
        {
            case ItemArrayType.Linear: AdjustLinearSeparation(value * linearAdjustMultiplier); break; //Adjust linear separation
            case ItemArrayType.Circular: AdjustCircularRadius(value * radiusAdjustMultiplier); break; //Adjust circular radius
        }
    }
    private void AdjustLinearSeparation(float addedValue)
    {
        currentLinearSeparation = Mathf.Clamp(currentLinearSeparation + addedValue, linearSeparationRange.x, linearSeparationRange.y); //Set clamped linear separation
        recordedLinearSeparation = currentLinearSeparation;                                                                            //Record new linear separation
    }
    private void AdjustCircularRadius(float addedValue)
    {
        currentCircularRadius = Mathf.Clamp(currentCircularRadius + addedValue, circularRadiusRange.x, circularRadiusRange.y); //Set clamped circular radius
        recordedCircularRadius = currentCircularRadius;                                                                        //Record new circular radius
    }
    /// <summary>
    /// Moves all items in array over given number of positions.
    /// </summary>
    /// <param name="places">Number of slots to move items by (negative values will make items move counterclockwise/left).</param>
    public void JogArray(int places)
    {
        //Validity checks:
        if (slots.Count == 0) return; //Ignore if there are no slots to nudge
        if (places == 0) return;      //Ignore if 0 was passed for some reason

        //Perform nudge:
        for (int i = 0; i < Mathf.Abs(places); i++) //Iterate for designated number of places
        {
            if (places > 0) //Jog is happening in clockwise direction
            {
                slots.Add(slots[0]); hand.heldItems.Add(hand.heldItems[0]); //Duplicate first item to end of list
                slots.RemoveAt(0); hand.heldItems.RemoveAt(0);              //Remove original from front of list
            }
            else //Jog is happening in counterclockwise direction
            {
                int x = slots.Count - 1;                                                //Get index of target slot
                slots.Insert(0, slots[x]); hand.heldItems.Insert(0, hand.heldItems[x]); //Duplicate last item to front of list
                slots.RemoveAt(x + 1); hand.heldItems.RemoveAt(x + 1);                  //Remove original from end of list
            }
        }
    }
    /// <summary>
    /// Prepares system for array placement.
    /// </summary>
    public void SetupPlacement()
    {
        //Initialization:
        for (int i = 0; i < placementGhosts.Length; i++) Destroy(placementGhosts[i].gameObject); //Destroy each object in existing ghost array

        //Generate new ghost array:
        List<Transform> newGhosts = new List<Transform>(); //Initialize list to store generated ghost transforms
        foreach (ItemController item in hand.heldItems)    //Iterate through each held item in hand
        {
            Transform newGhost = Instantiate(item.ghost.transform); //Instantiate new ghost
            newGhost.gameObject.SetActive(false);                   //Hide ghost by fully disabling it
            newGhosts.Add(newGhost);                                //Add new ghost to list
        }
        placementGhosts = newGhosts.ToArray(); //Store list of new ghosts as array
    }

    //UTILITY METHODS:
    /// <summary>
    /// Returns the slot this item is held in (if any)
    /// </summary>
    public Transform GetSlotFromItem(ItemController item)
    {
        if (slots.Count == 0) return null;                                       //Return null if slots are empty
        if (slots.Contains(item.transform.parent)) return item.transform.parent; //Return slot transform if item is in a slot
        return null;                                                             //Return null if item is not childed to a slot
    }
    /// <summary>
    /// Returns the item contained in given slot
    /// </summary>
    /// <param name="slot"></param>
    /// <returns></returns>
    public ItemController GetItemFromSlot(Transform slot)
    {
        return slot.GetComponentInChildren<ItemController>(); //Return item script childed to slot
    }
    /// <summary>
    /// Calculates and applies new array size based on bounding radii of adjacent items.
    /// </summary>
    public void ApplyItemBounds()
    {
        //Validity checks:
        if (slots.Count < 2 || hand.heldItems.Count != slots.Count) //System is in non-array state
        {
            currentLinearSeparation = defaultLinearSeparation; //Reset linear separation value
            currentCircularRadius = defaultCircularRadius;     //Reset circular radius value
            return;                                            //Do nothing else
        }

        //Determine item separation:
        float targetSeparation = 0; //Initialize container for max separation
        for (int i = 0; i < slots.Count; i++) //Iterate through each slot in system
        {
            float radius = GetItemFromSlot(slots[i]).boundingRadius;                 //Get bounding radius of current item
            int x = i - 1; if (x < 0) x = slots.Count - 1;                           //Get index of adjacent item
            float adjacentRadius = GetItemFromSlot(slots[x]).boundingRadius;         //Get bounding radius of adjacent item
            targetSeparation = Mathf.Max(radius + adjacentRadius, targetSeparation); //Check desired separation between objects with current target separation
        }

        //Determine and set array size:
        switch (currentArrayType) //Determine behavior based on array type
        {
            case ItemArrayType.Linear:
                currentLinearSeparation = targetSeparation; //Simply set linear separation to target
                break;
            case ItemArrayType.Circular:
                if (slots.Count == 2) currentCircularRadius = targetSeparation / 2; //Use much simpler linear separation when array only contains two items
                else //Array is oriented in true circle, so radius has to be adjusted based on trig
                {
                    float a1 = 360 / slots.Count;                                        //Get angle between items in current circular array
                    float a2 = (180 - a1) / 2;                                           //Value of other two angles in isosceles triangle
                    float angleRatio = Mathf.Sin(Mathf.Deg2Rad * a1) / targetSeparation; //Get ratio between constant separation angle and target separation distance
                    currentCircularRadius = Mathf.Sin(Mathf.Deg2Rad * a2) / angleRatio;  //Set radius in order to preserve separation between adjacent objects
                }
                break;
        }
    }
    /// <summary>
    /// Resets size and/or linear separation of array to recorded state.
    /// </summary>
    public void RecallArrayState()
    {
        if (freshArray) return;                             //Just keep current state if array is brand-new
        currentCircularRadius = recordedCircularRadius;     //Reset circular radius
        currentLinearSeparation = recordedLinearSeparation; //Reset linear separation
    }
    /// <summary>
    /// Updates the currently-selected item.
    /// </summary>
    public void UpdateSelectedItem()
    {
        ItemController prevSelectedItem = selectedItem; //Store previously-selected item
        if (hand.player.mode == PlayerController.InteractionMode.Collection && hand.touchingDrop && !stowed ||            //System fulfills Collection mode criteria for selecting an item
            hand.player.mode == PlayerController.InteractionMode.Manipulation && hand.OtherHand().isSecondary && !stowed) //System fulfills Manipulation mode criteria for selecting an item
        {
            selectedItem = GetCenteredItem(); //Get currently-centered item
        }
        else //Item cannot/will not be selected this frame
        {
            if (selectedItem != null) selectedItem = null; //Get rid of reference to selected item (if applicable)
        }
        if (selectedItem != prevSelectedItem) //Selected item has changed
        {
            if (selectedItem != null) //A new item has been selected
            {
                if (hand.player.mode == PlayerController.InteractionMode.Collection) selectedItem.Select(hand.player.dropHighlightColor);              //Select item to be dropped
                else if (hand.player.mode == PlayerController.InteractionMode.Manipulation) selectedItem.Select(hand.player.manipulateHighlightColor); //Select item to be manipulated
            }
            if (prevSelectedItem != null) //An item was previously selected
            {
                prevSelectedItem.UnSelect(); //Indicate to previous item that it is no longer selected (if applicable)
            }
        }
    }
    /// <summary>
    /// Returns the item which is closest to the center of the player's hand (and farthest forward, if array is in circle mode).
    /// </summary>
    public ItemController GetCenteredItem()
    {
        if (slots.Count == 0) return null;                   //Return nothing if no items are held
        else if (slots.Count == 1) return hand.heldItems[0]; //Return only available item if applicable
        else                                                 //There is more than one item to choose from
        {
            Vector3 selectionTarget = transform.position;                                                                 //Initialize target position at current position of target system
            if (currentArrayType == ItemArrayType.Circular) selectionTarget += transform.forward * currentCircularRadius; //Modify selection target if array type is circular
            float minDistance = Vector3.SqrMagnitude(selectionTarget - slots[0].position);                                //Initialize container for minimum found distance starting with first slot
            ItemController item = slots[0].GetComponentInChildren<ItemController>();                                      //Initialize container for item to return as first item in slot list
            for (int i = 1; i < slots.Count; i++) //Iterate through the remainder of the slots
            {
                float distance = Vector3.SqrMagnitude(selectionTarget - slots[i].position); //Get distance between target and current slot
                if (distance < minDistance) //Slot is closer to target than current selection
                {
                    item = slots[i].GetComponentInChildren<ItemController>(); //Make current slot new return item
                    minDistance = distance;                                   //Set new minimum distance to beat
                }
            }
            return item; //Return found item
        }
    }
    /// <summary>
    /// Call whenever the organization or number of items in an array has changed.
    /// </summary>
    private void ResetSystemState()
    {
        //Reset placement ghosts:
        if (placementGhosts.Length > 0) //There are extant placement ghosts
        {
            foreach (Transform ghost in placementGhosts) Destroy(ghost.gameObject); //Destroy any extant placement ghosts (now that array organization has been changed)
            placementGhosts = new Transform[0];                                     //Empty ghost array
            ghostsHidden = 0;                                                       //Indicate that no ghosts are hidden
        }
        freshArray = true; //Indicate that current array is unmodified
        ApplyItemBounds(); //Get new separation for items in array based on individual bounding radii
    }
}
