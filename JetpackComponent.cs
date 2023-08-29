using UnityEngine;

public class JetpackComponent : MonoBehaviour {
    public enum JetpackState { Disabled, Enabled }
    public JetpackState currentState = JetpackState.Disabled;
    
    public float thrustForce = 10.0f;
    private Rigidbody rb;
    
    void Start() {
        rb = GetComponent<Rigidbody>();
    }
    
    void Update() {
        if (Input.GetKeyDown(KeyCode.J)) {
            currentState = (currentState == JetpackState.Enabled) ? JetpackState.Disabled : JetpackState.Enabled;
        }
        
        if (currentState == JetpackState.Enabled && Input.GetKey(KeyCode.Space)) {
            rb.AddForce(Vector3.up * thrustForce);
        }
    }
}