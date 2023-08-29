using UnityEngine;

public class PlayDeadComponent : MonoBehaviour {
    public enum PlayDeadState { Alive, Dead }
    public PlayDeadState currentState = PlayDeadState.Alive;
    
    void Update() {
        if (Input.GetKeyDown(KeyCode.P)) {
            currentState = (currentState == PlayDeadState.Alive) ? PlayDeadState.Dead : PlayDeadState.Alive;
            // Add logic to disable movement, attacks, etc.
        }
    }
}