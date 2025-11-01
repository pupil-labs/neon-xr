using PupilLabs.Calibration;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs
{
    public class WahbaPoseSolver : PoseSolver
    {
        private WahbaSolver solver = null;

        public override bool RotationOnly => true;

        private void Start()
        {
            solver = new WahbaSolver();
        }

        protected override async Task<Matrix4x4> Solve(Vector3[] refPoints, Vector3[] obsDirections, CancellationToken token)
        {
            List<Vector4> refPoints4 = new List<Vector4>();

            for (int i = 0; i < obsDirections.Length; i++)
            {
                Vector4 refPoint = refPoints[i] - fallbackPos;
                refPoint.w = 1f; //weight 1
                refPoints4.Add(refPoint);
            }

            Matrix4x4 tm = await Task.Run(() => solver.SolveWahba(obsDirections, refPoints4.ToArray()));
            tm.SetColumn(3, new Vector4(fallbackPos.x, fallbackPos.y, fallbackPos.z, 1f));
            return tm;
        }
    }
}
