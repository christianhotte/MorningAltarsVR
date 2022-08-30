using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction;

public class PlayerController : MonoBehaviour
{
    //Classes, Structs & Enums:
    public enum Side { None, Left, Right}
    public enum InteractionMode { Collection, Manipulation, Placement, Palette }

    //Objects & Components:
    internal HandController leftHand;  //Controller script for left hand object
    internal HandController rightHand; //Controller script for right hand object
    internal Rigidbody rb;             //Player rigidbody component
    internal InputActionAsset actions; //Player input actions asset

    //Settings:
    [Header("Movement Settings:")]
    [Tooltip("Increases the amount of force exerted by hands when using move input")] public float moveStrengthMultiplier;
    [Tooltip("Determines number of fixed updates hand velocity is stored over")]      public int handVelocityMem;
    [Header("Input Settings:")]
    [Range(0, 1), Tooltip("Determines threshold at which array rotation adjustment input from joystick will be registered")] public float rotAdjustDeadzone;
    [Range(0, 1), Tooltip("Determines threshold at which array size adjustment input from joystick will be registered")]     public float sizeAdjustDeadzone;
    [Tooltip("Distance between hands at which one hand will become primary and the other will become secondary")]            public float proximityModeRadius;
    [Header("Visual Settings:")]
    [SerializeField, Tooltip("Material for player hands when in collection mode")]   private Material collectionModeMat;
    [SerializeField, Tooltip("Material for player hands when in manipulation mode")] private Material manipulationModeMat;
    [SerializeField, Tooltip("Material for player hands when in placement mode")]    private Material placementModeMat;
    [SerializeField, Tooltip("Material for player hands when in palette mode")]      private Material paletteModeMat;
    [Space()]
    [Tooltip("Color of highlight on objects which are being hovered over")]  public Color grabHighlightColor;
    [Tooltip("Color of highlight on objects which are about to be dropped")] public Color dropHighlightColor;
    [Tooltip("Color of highlight on objects which are being manipulated")]   public Color manipulateHighlightColor;

    //Runtime Vars:
    internal Side primaryHand = Side.None;                          //Hand which is currently performing primary actions when hands are in different modes
    internal InteractionMode mode = InteractionMode.Collection;     //Which mode of item interaction player system is currently in
    internal InteractionMode prevMode = InteractionMode.Collection; //Previous mode interaction system was in
    private bool proximityModeOn;                                   //Indicates that hands are close enough together to initiate dual interaction mode (if applicable). Does not necessarily mean hands are in this mode, just that they can be

    //RUNTIME METHODS:
    private void Start()
    {
        //Get objects & components:
        rb = GetComponent<Rigidbody>();                //Get player rigidbody component
        actions = GetComponent<PlayerInput>().actions; //Get player input actions

        //Disable actions:
        actions.FindActionMap("XRI LeftHand Manipulation").Disable();  //Disable manipulation actions on start
        actions.FindActionMap("XRI RightHand Manipulation").Disable(); //Disable manipulation actions on start
        actions.FindActionMap("XRI LeftHand Placement").Disable();     //Disable placement actions on start
        actions.FindActionMap("XRI RightHand Placement").Disable();    //Disable placement actions on start
        actions.FindActionMap("XRI LeftHand Palette").Disable();       //Disable palette actions on start
        actions.FindActionMap("XRI RightHand Palette").Disable();      //Disable palette actions on start
    }
    private void Update()
    {
        CheckProximityMode(); //Check hand proximity to determine if one hand is manipulating the other
    }

    //FUNCTIONALITY METHODS:
    /// <summary>
    /// Switches interaction system to target mode.
    /// </summary>
    public void SwitchMode(InteractionMode newMode)
    {
        //Initialize:
        prevMode = mode; //Store previous mode
        mode = newMode;  //Set new mode to current

        //Disable maps from previous mode:
        actions.FindActionMap("XRI LeftHand " + prevMode.ToString()).Disable();  //Disable left hand actions for previous mode
        actions.FindActionMap("XRI RightHand " + prevMode.ToString()).Disable(); //Disable right hand actions for previous mode

        //Enable maps for new mode:
        actions.FindActionMap("XRI LeftHand " + newMode.ToString()).Enable();  //Enable left hand actions for new mode
        actions.FindActionMap("XRI RightHand " + newMode.ToString()).Enable(); //Enable right hand actions for new mode

        //Mode change triggers:
        switch (newMode) //Determine specific triggers based on mode
        {
            case InteractionMode.Collection:
                leftHand.GetComponentInChildren<ParticleSystem>().GetComponent<Renderer>().material = collectionModeMat;  //Set material for left hand particles
                rightHand.GetComponentInChildren<ParticleSystem>().GetComponent<Renderer>().material = collectionModeMat; //Set material for right hand particles
                leftHand.grabSystem.CheckVisibilityState();                                                               //Update grabSystem visibility state
                rightHand.grabSystem.CheckVisibilityState();                                                              //Update grabSystem visibility state

                leftHand.targetSystem.ApplyItemBounds();  //Reset array size when entering collection mode
                rightHand.targetSystem.ApplyItemBounds(); //Reset array size when entering collection mode
                break;

            case InteractionMode.Manipulation:
                leftHand.GetComponentInChildren<ParticleSystem>().GetComponent<Renderer>().material = manipulationModeMat;  //Set material for left hand particles
                rightHand.GetComponentInChildren<ParticleSystem>().GetComponent<Renderer>().material = manipulationModeMat; //Set material for right hand particles
                leftHand.grabSystem.CheckVisibilityState();                                                                 //Update grabSystem visibility state
                rightHand.grabSystem.CheckVisibilityState();                                                                //Update grabSystem visibility state

                leftHand.targetSystem.RecallArrayState();  //Set array back to the state it was when previously in manipulation mode
                rightHand.targetSystem.RecallArrayState(); //Set array back to the state it was when previously in manipulation mode
                break;

            case InteractionMode.Placement: 
                leftHand.GetComponentInChildren<ParticleSystem>().GetComponent<Renderer>().material = placementModeMat;  //Set material for left hand particles
                rightHand.GetComponentInChildren<ParticleSystem>().GetComponent<Renderer>().material = placementModeMat; //Set material for right hand particles
                break;

            case InteractionMode.Palette:
                leftHand.GetComponentInChildren<ParticleSystem>().GetComponent<Renderer>().material = paletteModeMat;  //Set material for left hand particles
                rightHand.GetComponentInChildren<ParticleSystem>().GetComponent<Renderer>().material = paletteModeMat; //Set material for right hand particles
                leftHand.grabSystem.CheckVisibilityState();                                                            //Update grabSystem visibility state
                rightHand.grabSystem.CheckVisibilityState();                                                           //Update grabSystem visibility state
                break;
        }

        //Cleanup:
        CheckProximityMode(); //Immediately update proximity mode (validity may have changed)
    }
    /// <summary>
    /// Switches to next mode in triangular rotation.
    /// </summary>
    public void IndexMode()
    {
        switch (mode) //Determine which mode to switch to based on current mode
        {
            case InteractionMode.Collection: SwitchMode(InteractionMode.Manipulation); break; //Switch from Collection to Manipulation mode
            case InteractionMode.Manipulation: SwitchMode(InteractionMode.Collection); break;  //Switch from Manipulation to Placement mode (TEMP: go right back to collection mode)
            //case InteractionMode.Placement: SwitchMode(InteractionMode.Collection); break;    //Switch from Placement to Collection mode
            case InteractionMode.Palette: break;                                              //Do nothing if in Palette mode
        }
    }

    //UTILITY METHODS:
    /// <summary>
    /// Checks hand proximity and, if hands are close enough, determines which hand is dominant and which hand is secondary. Then changes additional variables as needed.
    /// </summary>
    private void CheckProximityMode()
    {
        //Initialize:
        if (mode == InteractionMode.Collection || //Proximity mode is not available for collection
            mode == InteractionMode.Palette)      //Proximity mode is not available for palette
        {
            if (proximityModeOn) //Proximity mode is currently on
            {
                proximityModeOn = false;       //Indicate that proximity mode is off
                leftHand.isSecondary = false;  //Make sure left hand is not secondary
                rightHand.isSecondary = false; //Make sure right hand is not secondary
            }
            return; //Do not perform further checks
        }
        proximityModeOn = false;                                       //Flip mode value so that it can be used as a marker
        float leftUpwardness = -leftHand.targetSystem.transform.up.y;  //Determine how upward-facing left hand is
        float rightUpwardness = rightHand.targetSystem.transform.up.y; //Determine how upward-facing right hand is

        //Determine hand proximity:
        if (leftHand.doingSecondaryManipulation && rightUpwardness > 0 || //Left hand is doing secondary manipulation and right hand is still facing upward
            rightHand.doingSecondaryManipulation && leftUpwardness > 0)   //Right hand is doing secondary manipulation and left hand is still facing upward
        {
            proximityModeOn = true; //Keep proximity mode on (ignoring orientation of non-dominant hand)
        }
        if (Vector3.Distance(leftHand.targetSystem.transform.position, rightHand.targetSystem.transform.position) <= proximityModeRadius) //Hands are close enough together to initiate proximity mode
        {
            //Determine which hand is dominant (if any):
            if (leftUpwardness > rightUpwardness && rightUpwardness < 0 && leftUpwardness > 0) //Left hand is more upwards than right hand
            {
                //Establish dominant hand:
                proximityModeOn = true;       //Indicate that system is now in proximity mode
                rightHand.isSecondary = true; //Make right hand secondary
                leftHand.isSecondary = false; //Make left hand dominant
            }
            else if (rightUpwardness > leftUpwardness && leftUpwardness < 0 && rightUpwardness > 0) //Right hand is more upwards than left hand
            {
                //Establish dominant hand:
                proximityModeOn = true;        //Indicate that system is now in proximity mode
                leftHand.isSecondary = true;   //Make left hand secondary
                rightHand.isSecondary = false; //Make right hand dominant
            }
        }

        //Cleanup:
        if (!proximityModeOn) //Proximity mode is not on
        {
            leftHand.isSecondary = false;                 //Make sure left hand is not secondary
            rightHand.isSecondary = false;                //Make sure right hand is not secondary
            leftHand.doingSecondaryManipulation = false;  //Make sure left hand is no longer doing secondary manipulation
            rightHand.doingSecondaryManipulation = false; //Make sure right hand is no longer doing secondary manipulation
        }
    }
}
