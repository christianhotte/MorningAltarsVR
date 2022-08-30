using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Manages input separately between hands and organizes held items using various scripts on child objects. Gets all inspector settings from unifying PlayerController.
/// </summary>
public class HandController : MonoBehaviour
{
    //Objects & Components:
    internal PlayerController player;       //Controller script attached to player object
    internal GrabSystem grabSystem;         //System involved in hovering over and grabbing items
    internal ItemTargetSystem targetSystem; //System involved in organizing held items

    //Runtime Vars:
    internal bool holdingMove = false;                //True if player is currently holding move input
    internal bool placementReady = false;             //True if player is ready to place held items
    internal bool touchingGrab = false;               //True if player has their finger on the grab trigger
    internal bool touchingDrop = false;               //True if player is touching the drop button
    internal bool doingSecondaryManipulation = false; //True if this hand is currently holding trigger and manipulating objects on other hand
    internal float grabAmount;                        //Amount player is holding down the grab trigger
    private float grabZoneRadiusAdjust;               //Current value by which grab zone radius is being adjusted
    internal float arrayShift;                        //Current value by which array is being shifted in a direction (during Collection mode)
    private Vector3 holdTarget;                       //Target position of hand when holding move input
    private Vector2 itemRotAdjust;                    //Current value by which single item rotation is being continually adjusted
    private float arraySizeAdjust;                    //Current value by which array size is being continually adjusted
    private float arrayRotAdjust;                     //Current value by which array rotation is being continually adjusted
    private bool modeToggled = false;                 //Used to prevent double mode toggle input

    internal List<ItemController> heldItems = new List<ItemController>(); //List of items currently being held in this hand
    private Vector3 prevPosition;                                         //Position hand was at last update
    private List<Vector3> prevVelocity = new List<Vector3>();             //List of velocities throughout previous updates
    internal bool isSecondary = false;                                    //Indicates that this hand's interactions are affecting objects in the other hand
    internal bool isLeft = false;                                         //Indicates that this is the left hand

    //RUNTIME METHODS:
    private void Start()
    {
        //Get objects & components:
        player = GetComponentInParent<PlayerController>();                                                  //Get player controller component
        if (name.Contains("Left")) { player.leftHand = this; isLeft = true; } else player.rightHand = this; //Give player controller a copy of this script
    }
    private void Update()
    {
        //Update velocity:
        Vector3 currentPosition = transform.position;                              //Get current position
        prevVelocity.Add((currentPosition - prevPosition) / Time.fixedDeltaTime);  //Add current velocity to velocity memory
        if (prevVelocity.Count > player.handVelocityMem) prevVelocity.RemoveAt(0); //Remove oldest item from list if memory overflows
        prevPosition = currentPosition;                                            //Store previous position for next update

        //Continuous input checks:
        //MAP - UNIVERSAL:
        if (holdingMove) //Move input is currently being held
        {
            Vector3 posDelta = holdTarget - transform.position;   //Get difference between current and target positions of hand
            posDelta *= player.moveStrengthMultiplier;            //Apply multiplier to acceleration value
            player.rb.AddForce(posDelta, ForceMode.Acceleration); //Use rigidbody acceleration to pull player toward target
        }
        //MAP - COLLECTION:
        if (player.mode == PlayerController.InteractionMode.Collection) //System is in collection mode
        {
            if (!targetSystem.stowed) //System is not stowed
            {
                if (touchingGrab) //Grab input is currently being detected
                {
                    //Update grabzone:
                    if (grabZoneRadiusAdjust != 0) grabSystem.AdjustZoneRadius(grabZoneRadiusAdjust * Time.deltaTime); //Apply grab zone radius adjustment on a frame-by-frame basis
                    grabSystem.AdjustZoneSize(grabAmount);                                                             //Determine zone size based solely off of trigger squeeze amount

                    //Grab hovered items:
                    if (grabAmount > 0 && grabSystem.hoverItems.Count > 0) //Only do so if player is squeezing the trigger and at least one item is currently being hovered over
                    {
                        ItemController[] hoverItems = grabSystem.GrabAll();         //Save and clear all hovered items from grabSystem
                        foreach (ItemController item in hoverItems) HoldItem(item); //Setup each item as held
                    }
                }
                else //Grab input is not being detected
                {
                    targetSystem.ShiftArray(-arrayShift * Time.deltaTime); //Shift array based on manipulation input
                }
            }
        }
        //MAP - MANIPULATION:
        if (player.mode == PlayerController.InteractionMode.Manipulation) //System is in manipulation mode
        {
            if (!targetSystem.stowed || placementReady) //System is not stowed (or placement ghosts are active)
            {
                //Adjust basic array/item orientation:
                if (heldItems.Count == 1) //Player is currently holding only one item
                {
                    targetSystem.RotateItem(heldItems[0], itemRotAdjust * Time.deltaTime); //Apply item rotation adjustment value to single item
                }
                else if (heldItems.Count > 1) //Player is currently holding an array of items
                {
                    if (targetSystem.freshArray && arraySizeAdjust != 0) targetSystem.freshArray = false; //Indicate that current array has been modified
                    targetSystem.RotateArray(-arrayRotAdjust * Time.deltaTime);                           //Apply adjustment value to array rotation
                    targetSystem.AdjustArraySize(arraySizeAdjust * Time.deltaTime);                       //Apply adjustment value to array size
                }
            }
        }
        //MAP - PLACEMENT:
        if (player.mode == PlayerController.InteractionMode.Placement) //System is in placement mode
        {

        }
        //MAP - PALETTE:
        if (player.mode == PlayerController.InteractionMode.Palette) //System is in palette mode
        {

        }
    }

    //INPUT - UNIVERSAL:
    public void OnMoveInput(InputAction.CallbackContext context)
    {
        if (context.performed) holdingMove = true; else holdingMove = false; //Update holdingMove state
        if (context.started) holdTarget = transform.position;                //Get target when hold input is initiated
    }
    public void OnPaletteToggleInput(InputAction.CallbackContext context)
    {
        //Change mode depending on context:
        bool modeChanged = false; //Initialize secondary variable to determine whether or not mode was changed
        if (context.performed) //Button was held
        {
            if (player.mode != PlayerController.InteractionMode.Palette) //Player is not currently in palette mode
            {
                player.SwitchMode(PlayerController.InteractionMode.Palette); //Switch to palette mode
                modeToggled = true;                                          //Prevent input from double-registering
                modeChanged = true;                                          //Indicate that mode has changed
            }
        }
        if (context.canceled) //Button was released
        {
            if (modeToggled) //Make sure input doesn't double-register
            {
                modeToggled = false; //Reset marker
            }
            else //Normal mode toggle
            {
                if (player.mode == PlayerController.InteractionMode.Palette) player.SwitchMode(player.prevMode); //Switch back from palette to previous mode
                else player.IndexMode();                                                                         //Switch to next mode
                modeChanged = true;                                                                              //Indicate that mode has changed
            }
        }

        //Clear variables upon mode change:
        if (modeChanged) //Mode has been changed
        {
            touchingGrab = false;               //Reset value
            touchingDrop = false;               //Reset value
            placementReady = false;             //Reset value
            doingSecondaryManipulation = false; //Reset value
            grabZoneRadiusAdjust = 0;           //Reset value
            arrayShift = 0;                     //Reset value
            itemRotAdjust = Vector2.zero;       //Reset value
            arrayRotAdjust = 0;                 //Reset value
            arraySizeAdjust = 0;                //Reset value
        }
    }
    public void OnChangeModeInput(InputAction.CallbackContext context)
    {
        if (context.performed) //Change mode button has been pressed
        {
            switch (player.mode) //Decide behavior based on current interaction mode
            {
                case PlayerController.InteractionMode.Collection:
                    if (grabAmount > 0 || touchingGrab) //Player is currently pressing grab input
                    {
                        grabSystem.IndexZoneType(); //Switch grab system zone type
                    }
                    else //Player is in neutral collection mode
                    {
                        targetSystem.SwitchArrayType(); //Switch held item array type
                    }
                    break;
                case PlayerController.InteractionMode.Manipulation:
                    if (!OtherHand().isSecondary) //Secondary manipulation is not ocurring
                    {
                        targetSystem.SwitchArrayType(); //Switch held item array type
                    }
                    break;
                case PlayerController.InteractionMode.Placement:
                    break;
                case PlayerController.InteractionMode.Palette:
                    break;
            }
        }
    }
    public void OnManipulateInput(InputAction.CallbackContext context)
    {
        Vector2 value = context.ReadValue<Vector2>(); //Get value from context
        switch (player.mode) //Decide behavior based on current interaction mode
        {
            case PlayerController.InteractionMode.Collection:
                if (touchingGrab) //Player is currently touching the grab trigger
                {
                    grabZoneRadiusAdjust = value.x; //Use X axis to adjust grab zone radius
                }
                else //Player is not touching the grab trigger
                {
                    arrayShift = value.x; //Use X axis to adjust array position
                }
                break;
            case PlayerController.InteractionMode.Manipulation:
                //Rotate array:
                itemRotAdjust = value;                                                                                          //Use raw input to adjust rotation of single item
                if (Mathf.Abs(value.x) < player.rotAdjustDeadzone) arrayRotAdjust = 0;                                          //Filter out deadzone inputs
                else arrayRotAdjust = Mathf.InverseLerp(player.rotAdjustDeadzone, 1, Mathf.Abs(value.x)) * Mathf.Sign(value.x); //Remap value back onto full axis range and set as adjustment amount
                //Adjust array size:
                if (Mathf.Abs(value.y) < player.sizeAdjustDeadzone) arraySizeAdjust = 0;                                          //Filter out deadzone inputs
                else arraySizeAdjust = Mathf.InverseLerp(player.sizeAdjustDeadzone, 1, Mathf.Abs(value.y)) * Mathf.Sign(value.y); //Remap value back onto full axis range and set as adjustment amount
                break;
            case PlayerController.InteractionMode.Placement:
                break;
            case PlayerController.InteractionMode.Palette:
                break;
        }
    }
    
    //INPUT - COLLECTION:
    public void OnGrabInput(InputAction.CallbackContext context)
    {
        grabAmount = context.ReadValue<float>(); //Get value from context
    }
    public void OnTouchingGrabInput(InputAction.CallbackContext context)
    {
        if (context.started) //Player has touched the trigger
        {
            touchingGrab = true;               //Indicate that player has touched trigger
            arrayShift = 0;                    //Zero out adjustment value when entering radius adjustment mode
            grabSystem.CheckVisibilityState(); //Update grabSystem visibility state
        }
        if (context.canceled) //Player has fully released the trigger
        {
            touchingGrab = false;              //Indicate that player has released trigger
            grabZoneRadiusAdjust = 0;          //Zero out adjustment value when leaving radius adjustment mode
            grabSystem.CheckVisibilityState(); //Update grabSystem visibility state
            grabSystem.AdjustZoneSize(0);      //Reset zone size to minimum
        }
    }
    public void OnDropInput(InputAction.CallbackContext context)
    {
        if (context.started) //Drop button has been pressed (drop one item)
        {
            if (heldItems.Count == 0) return;    //Ignore if player is holding no items
            touchingDrop = true;                 //Make sure system knows that drop button is being touched
            targetSystem.UpdateSelectedItem();   //Make sure target system has a selected item
            DropItem(targetSystem.selectedItem); //Drop currently-selected item
        }
        if (context.performed) //Drop button has been held (drop all items)
        {
            if (heldItems.Count == 0) return;                    //Ignore if player is holding no items
            for (; heldItems.Count > 0;) DropItem(heldItems[0]); //Drop items until held items list is empty
        }
    }
    public void OnHighlightDropInput(InputAction.CallbackContext context)
    {
        if (context.started) //Player has just touched the drop button
        {
            touchingDrop = true; //Indicate that player is now touching drop button
        }
        if (context.canceled) //Player has just stopped touching the drop button
        {
            touchingDrop = false; //Indicate that player is no longer touching drop button
        }
    }
    
    //INPUT - MANIPULATION:
    public void OnSecondaryManipulateInput(InputAction.CallbackContext context)
    {
        if (context.started) //Player has just squeezed trigger
        {
            if (isSecondary) //Secondary manipulation mode is available
            {
                doingSecondaryManipulation = true; //Indicate that system is in secondary manipulation mode
            }
            else //Secondary manipulation mode is not available
            {
                targetSystem.SetupPlacement(); //Initialize item placement ghosts
                placementReady = true;         //Indicate that player is ready to place items
            }
        }
        if (context.canceled) //Player has just released trigger
        {
            doingSecondaryManipulation = false; //End manipulation
            placementReady = false;             //End placement
        }
    }
    public void OnPlaceInput(InputAction.CallbackContext context)
    {
        if (context.started) //Place button has just been pressed
        {
            if (placementReady) //System is ready to place items
            {
                PlaceItems(); //Place all currently-held items
            }
        }
    }

    //FUNCTIONALITY METHODS:
    private void HoldItem(ItemController item)
    {
        item.IsGrabbed();           //Indicate to item that it has been grabbed
        heldItems.Add(item);        //Add item to list of items being held
        targetSystem.AddSlot(item); //Add slot to item target system and attach item to it
    }
    private void DropItem(ItemController item)
    {
        //Validity checks:
        if (!heldItems.Contains(item)) return; //Ignore if target item is not actually currently held

        //Remove array slot:
        Transform emptySlot = item.transform.parent;     //Get slot item is childed to
        item.transform.SetParent(transform.root.parent); //Unchild item from slot
        targetSystem.RemoveSlot(emptySlot);              //Remove slot from target system
        item.transform.localScale = Vector3.one;         //Make sure item is properly scaled

        //Cleanup:
        if (heldItems.Count < 2) //Dropping item reduces item count below minimum array size
        {
            arrayRotAdjust = 0;                                     //Reset array rotation adjustment input
            arraySizeAdjust = 0;                                    //Reset array size adjustment input
            if (heldItems.Count == 0) itemRotAdjust = Vector2.zero; //Reset item rotation adjustment input if necessary
        }
        item.IsReleased(GetCurrentVelocity());             //Indicate to item that it has been released and apply current hand velocity
        if (grabAmount > 0) item.grabImmunityTime += 0.2f; //Give item grab immunity if player is holding grab while dropping it
        heldItems.Remove(item);                            //Remove dropped item from list of held items
    }
    private void PlaceItems()
    {
        //Validity checks:
        if (heldItems.Count == 0) return;                                   //Ignore if there are no items to place
        if (!placementReady) return;                                        //Ignore if system is not ready for placement
        if (targetSystem.placementGhosts.Length != heldItems.Count) return; //Ignore if there is a discrepancy in the number of extant placement ghosts

        //Trigger item placement:
        for (int i = 0; i < heldItems.Count; i++) //Iterate through list of held items
        {
            ItemController item = heldItems[i];              //Get controller of item being placed
            item.transform.SetParent(transform.root.parent); //Unchild item from slot
            item.IsPlaced(targetSystem.placementGhosts[i]);  //Place item with a reference to its corresponding ghost
        }

        //Clean up arrays:
        targetSystem.placementGhosts = new Transform[0]; //Get rid of references to placement ghosts so that they are not destroyed by slot removal
        targetSystem.RemoveAllSlots();                   //Remove all slots from targetSystem
        heldItems.Clear();                               //Clear held items list
    }
    public void LoadPalette(PaletteManager.Palette palette)
    {

    }

    //UTILITY METHODS:
    private Vector3 GetCurrentVelocity()
    {
        if (prevVelocity.Count == 0) return Vector3.zero;      //Return zero if there is no velocity memory
        Vector3 velTotal = Vector3.zero;                       //Initialize container to store total velocity
        foreach (Vector3 vel in prevVelocity) velTotal += vel; //Add each velocity in memory to total
        return velTotal / prevVelocity.Count;                  //Return average velocity throughout memory
    }
    /// <summary>
    /// Returns the hand which is not this hand.
    /// </summary>
    public HandController OtherHand()
    {
        if (isLeft) return player.rightHand; //Return right hand if this is the left hand
        else return player.leftHand;         //Return left hand if this is the right hand
    }
}
