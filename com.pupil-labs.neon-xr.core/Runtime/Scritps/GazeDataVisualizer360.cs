using UnityEngine;

namespace PupilLabs
{
    public class GazeDataVisualizer360 : GazeDataVisualizer
    {
        [SerializeField]
        protected bool occlude = true;

        protected override float UpdateRaycast(Vector3 worldOrigin, Vector3 worldDirection)
        {
            if (raycastPointer != null)
            {
                raycastPointer.SetActive(false);
            }
            if (doRaycast)
            {
                var ray = new Ray(worldOrigin, worldDirection);
                RaycastHit hit;
                if (Physics.Raycast(ray.GetPoint(raycastDistance), -worldDirection, out hit, raycastDistance, raycastMask)) //backward raycast to hit the sphere
                {
                    float distance = raycastDistance - hit.distance;
                    if (occlude)
                    {
                        RaycastHit hit2;
                        if (Physics.Raycast(ray, out hit2, hit.distance, raycastMask))
                        {
                            distance = hit2.distance;
                            hit = hit2;
                        }
                    }
                    onHit.Invoke(hit);
                    if (raycastPointerVisible)
                    {
                        raycastPointer.transform.position = hit.point;
                        raycastPointer.SetActive(true);
                    }
                    return distance;
                }
            }
            return -1f;
        }
    }
}
