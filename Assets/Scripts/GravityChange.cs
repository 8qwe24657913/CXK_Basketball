using UnityEngine;

public class GravityChange : MonoBehaviour
{
    void LateUpdate() {
        var angles = transform.rotation * new Vector3(0, 0, 1.0F);
        Physics.gravity = angles;
    }
}
