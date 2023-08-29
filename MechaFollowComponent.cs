using UnityEngine;

public class MechaFollowComponent : MonoBehaviour {
    public GameObject target;
    public float speed = 5.0f;
    
    void Update() {
        if (target != null) {
            float step = speed * Time.deltaTime;
            transform.position = Vector3.MoveTowards(transform.position, target.transform.position, step);
        }
    }
}