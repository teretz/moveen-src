using System;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MechaConfiguration {
    public string armsType;
    public int numberOfArms;
    public string primaryWeapon;
    public string jetpackType;
    public string overdriveType;
    public string playDeadType;
    public string mechaFollowType;
    public string legsType;
    public int numberOfLegs;
    public string bodyType;
    public string headpieceType;
    public string[] aiChips;
    public string onboardSystem;
    public string[] userAccessEmails;
    public GameObject gunComponentPrefab;
    public GameObject cockpitComponentPrefab;
    public string ammoSlot;
    public AudioClip openSound;

    // Part sets
    public List<string> partSets;

    // Generate a unique ID for each configuration
    public string uniqueID = Guid.NewGuid().ToString();
}