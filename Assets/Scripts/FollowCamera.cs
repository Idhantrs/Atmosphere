using UnityEngine;

public class FollowCamera : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 1.7f, -8f);
    public float positionLag = 6f;
    public float rotationLag = 6f;

    void LateUpdate()
    {
        if (target == null) return;
        var atm = Atmosphere.Instance;
        Vector3 up = atm != null ? atm.Up(target.position) : Vector3.up;

        Vector3 desired = target.TransformPoint(offset);
        transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-positionLag * Time.deltaTime));
        if ((desired - transform.position).sqrMagnitude > 2500f)
        {
            transform.position = desired;
        }
        Quaternion look = Quaternion.LookRotation(target.position - transform.position, up);
        transform.rotation = Quaternion.Slerp(transform.rotation, look, 1f - Mathf.Exp(-rotationLag * Time.deltaTime));
    }
}