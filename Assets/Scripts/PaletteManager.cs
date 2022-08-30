using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PaletteManager : MonoBehaviour
{
    //Classes, Structs & Enums:
    /// <summary>
    /// Contains stored information about a set of held items.
    /// </summary>
    public class Palette
    {
        public List<ItemController> items = new List<ItemController>(); //List of items contained within this palette

    }

    //Static Stuff:
    /// <summary>
    /// List of palettes currently saved to system.
    /// </summary>
    public static List<Palette> palettes = new List<Palette>();

    //Objects & Components:


    //Settings:


    //Runtime Vars:
}
