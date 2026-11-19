using UnityEngine;

public class RemoteView : MonoBehaviour
{
    public Transform head;                       // CenterEyeAnchor
    public PassthroughFrameReceiver receiver;    // for pose (step 3)
    public float distance = 10f;                 // arbitrary; only angular size matters
    public Vector2 sensorRes = new Vector2(1728, 1728);
    public Vector2 focal = new Vector2(1173.17f, 1173.17f);
    public Vector2 sentRes = new Vector2(1280, 1280);
    public bool worldLocked = false;             // false = pure head-locked
    public float fillScale = 1.4f;
    public float pitchDownDegrees = 5f;   // positive = lower

    Quaternion yawOffset = Quaternion.identity;
    bool aligned;

    void LateUpdate()
    {
        float s = sentRes.x / sensorRes.x;
        float w = distance * sentRes.x / (focal.x * s);   // ? 1.473 * d
        float h = distance * sentRes.y / (focal.y * s);   // ? 1.105 * d
        w *= fillScale;
        h *= fillScale;
        transform.localScale = new Vector3(w, h, 1f);

        Quaternion rot = head.rotation;
        //if (worldLocked && receiver.HasPose)
        //{
        //    if (!aligned)   // match yaw once (or call on a button press to recenter)
        //    {
        //        float dy = head.eulerAngles.y - receiver.LatestPose.eulerAngles.y;
        //        yawOffset = Quaternion.Euler(0, dy, 0);
        //        aligned = true;
        //    }
        //    rot = yawOffset * receiver.LatestPose;
        //}

        //transform.SetPositionAndRotation(head.position + rot * Vector3.forward * distance, rot);
        rot = rot * Quaternion.Euler(pitchDownDegrees, 0f, 0f);
        transform.SetPositionAndRotation(head.position + rot * Vector3.forward * distance, rot);
    }
}