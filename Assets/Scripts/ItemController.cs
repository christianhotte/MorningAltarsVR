using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Controls objects which the player is able to pick up and interact with.
/// </summary>
public class ItemController : MonoBehaviour
{
    //Objects & Components:
    private Renderer[] renderers;          //Array of all render components on or childed to this object
    private Renderer[] highlightRenderers; //Array of all render components used to highlight this object
    internal Rigidbody rb;                 //Rigidbody component for this object

    [Tooltip("Reference to prefab object which this item came from")]                                  public GameObject prefab;
    [Tooltip("Prefab for semi-transparent clone of this object, used for placement and manipulation")] public GameObject ghost;

    internal GameObject spawnedGhost; //Ghost object given to this script when item is being placed (will be destroyed upon final placement

    //Settings:
    [Header("Settings:")]
    [Tooltip("Invisible sphere used to artificially increase array separation during collection mode")]           public float boundingRadius = 1;
    [Tooltip("Amount by which to offset this object from surfaces when doing placement")]                         public float placementOffset;
    [SerializeField, Tooltip("The amount of time it takes for this object to reach target position when placed")] private float placementTime = 0.5f;
    [SerializeField, Tooltip("Curve describing object motion when being placed")]                                 private AnimationCurve placementCurve;

    //Runtime Vars:
    internal bool isHeld;            //Whether or not this item is currently being held by player
    internal bool selected;          //Whether or not this item is currently selected (selection is a contextual state which determines some system behavior)
    internal bool isPlaced;          //Indicates that item has been glued to a surface by player placement
    internal float grabImmunityTime; //Timer which can be assigned to dropped items to prevent them from being immediately picked up again

    //COROUTINES:
    /// <summary>
    /// Moves this item to designated position and rotation within given time frame
    /// </summary>
    IEnumerator Place(Vector3 position, Quaternion rotation)
    {
        //Initialization:
        Vector3 origPosition = transform.position;    //Store position at beginning of placement
        Quaternion origRotation = transform.rotation; //Store rotation at beginning of placement
        Vector3 origScale = transform.localScale;     //Store scale at beginning of placement

        //Move to target over time:
        float currentTime = 0;              //Initialize time tracker
        while (currentTime < placementTime) //Iterate for given amount of time
        {
            //Initialize:
            currentTime += Time.fixedDeltaTime;                               //Increment time tracker
            float t = placementCurve.Evaluate(currentTime / placementTime);   //Get interpolant value for current time (according to placement curve)

            //Move item:
            transform.position = Vector3.Lerp(origPosition, position, t);     //Lerp to new position
            transform.rotation = Quaternion.Slerp(origRotation, rotation, t); //Slerp to new rotation
            transform.localScale = Vector3.Lerp(origScale, Vector3.one, t);   //Lerp to new scale
            //Cleanup:
            if (spawnedGhost != null) spawnedGhost.transform.localScale = Vector3.Lerp(Vector3.one, Vector3.zero, t); //Scale placement ghost down if possible
            yield return new WaitForFixedUpdate();                                                                    //Wait for next FixedUpdate
        }

        //Final checks:
        transform.SetPositionAndRotation(position, rotation); //Set final transform values
        transform.localScale = Vector3.one;                   //Make sure scale is correct (should be unnecessary)
        if (spawnedGhost != null) Destroy(spawnedGhost);      //Destroy ghost if applicablep
    }

    //RUNTIME METHODS:
    private void Awake()
    {
        //Get objects & components:
        highlightRenderers = transform.Find("Highlight").GetComponentsInChildren<Renderer>();                    //Get renderers used for highlights
        List<Renderer> renderComponents = new List<Renderer>(GetComponents<Renderer>());                         //Get renderer components on object
        renderComponents.AddRange(GetComponentsInChildren<Renderer>());                                          //Get renderer components from children
        foreach (Renderer r in highlightRenderers) if (renderComponents.Contains(r)) renderComponents.Remove(r); //Remove highlight renderers from list of normal renderers
        renderers = renderComponents.ToArray();                                                                  //Get list of renderers as array
        rb = GetComponent<Rigidbody>();                                                                          //Get rigidbody component

        //Initialize:
        UnSelect(); //Hide highlight renderers
    }
    private void Update()
    {
        //Update timers:
        if (grabImmunityTime > 0) { grabImmunityTime = Mathf.Max(grabImmunityTime - Time.deltaTime, 0); } //Update hover immunity timer
    }

    //FUNCTIONALITY METHODS:
    /// <summary>
    /// Call this method whenever this item is grabbed.
    /// </summary>
    public void IsGrabbed()
    {
        //Set physics:
        rb.isKinematic = true;                          //Make rigidbody kinematic
        rb.interpolation = RigidbodyInterpolation.None; //Turn off interpolation

        //Cleanup:
        isHeld = true;    //Indicate that item is now being held
        isPlaced = false; //Indicate that item is not currently placed
    }
    /// <summary>
    /// Call this method whenever this item is released.
    /// </summary>
    /// <param name="velocity">Velocity at which to release item.</param>
    public void IsReleased(Vector3 velocity)
    {
        //Set physics:
        rb.isKinematic = false;                                //Make rigidbody dynamic
        rb.interpolation = RigidbodyInterpolation.Interpolate; //Turn on interpolation
        rb.AddForce(velocity, ForceMode.Impulse);              //Set item velocity

        //Cleanup:
        isHeld = false; //Indicate that item is no longer being held
    }
    /// <summary>
    /// Call this method whenever this item is placed in a specific spot.
    /// </summary>
    /// <param name="placementGhost">The ghost transform being used as a placement reference</param>
    public void IsPlaced(Transform placementGhost)
    {
        //Initialize:
        spawnedGhost = placementGhost.gameObject;                                //Keep reference to placement ghost
        StartCoroutine(Place(placementGhost.position, placementGhost.rotation)); //Begin item placement sequence

        //Cleanup:
        isHeld = false; //Indicate that item is no longer being held
        isPlaced = true; //Indicate that item has been placed
    }
    /// <summary>
    /// Call this when this item is selected.
    /// </summary>
    public void Select(Color highlightColor)
    {
        //Enable highlight:
        foreach (Renderer hr in highlightRenderers) //Iterate through highlight renderers
        {
            hr.material.color = highlightColor; //Set highlight color
            hr.enabled = true;                  //Make highlight visible
        }

        //Cleanup:
        selected = true; //Indicate that item is now selected
    }
    /// <summary>
    /// Cancels item selection state.
    /// </summary>
    public void UnSelect()
    {
        //Disable highlight:
        foreach (Renderer hr in highlightRenderers) //Iterate through highlight renderers
        {
            hr.enabled = false; //Make highlight invisible
        }

        //Cleanup:
        selected = false; //Indicate that item is no longer selected
    }
}
