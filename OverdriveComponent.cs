using UnityEngine;

public class OverdriveComponent : MonoBehaviour {
    public enum OverdriveState { Disabled, Enabled }
    public OverdriveState currentState = OverdriveState.Disabled;
    
    public float speedMultiplier = 2.0f;
    private float originalSpeed;

    void Start() {
        originalSpeed = /* Fetch your original speed variable */;
    }
    
    void Update() {
        if (Input.GetKeyDown(KeyCode.O)) {
            currentState = (currentState == OverdriveState.Enabled) ? OverdriveState.Disabled : OverdriveState.Enabled;
            originalSpeed = (currentState == OverdriveState.Enabled) ? originalSpeed * speedMultiplier : originalSpeed / speedMultiplier;
        }
    }
}