using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs.Calibration
{
    public class IterativeKabschSolver : PoseSolver
    {
        [SerializeField]
        protected Vector3Int minMaxStepX = new Vector3Int(0, 0, 1);
        [SerializeField]
        protected Vector3Int minMaxStepY = new Vector3Int(0, 30, 1);
        [SerializeField]
        protected Vector3Int minMaxStepZ = new Vector3Int(0, 40, 2);

        private Matrix4x4 SolveForPos(Vector3 sensorPos, Vector3[] refPoints, Vector3[] obsDirections)
        {
            List<Vector3> inPoints = new List<Vector3>();
            List<Vector4> refPoints4 = new List<Vector4>();

            for (int i = 0; i < obsDirections.Length; i++)
            {
                float distance = Vector3.Distance(refPoints[i], sensorPos);
                Vector3 observedPoint = obsDirections[i] * distance; //sensor space
                Vector4 refPoint = new Vector4(refPoints[i].x, refPoints[i].y, refPoints[i].z, 1f); //weight 1
                refPoints4.Add(refPoint);
                inPoints.Add(observedPoint);
            }
            KabschSolver solver = new KabschSolver();
            Matrix4x4 tm = solver.SolveKabsch(inPoints.ToArray(), refPoints4.ToArray());
            tm.SetColumn(3, new Vector4(sensorPos.x, sensorPos.y, sensorPos.z, 1f)); //use position passed by argument
            return tm;
        }

        protected override async Task<Matrix4x4> Solve(Vector3[] refPoints, Vector3[] obsDirections, CancellationToken token)
        {
            Matrix4x4 bestSolution = Matrix4x4.identity;
            float bestError = float.PositiveInfinity;

            for (int x = minMaxStepX.x; x <= minMaxStepX.y; x += minMaxStepX.z) //this is not really expected
            {
                for (int y = minMaxStepY.x; y <= minMaxStepY.y; y += minMaxStepY.z)
                {
                    for (int z = minMaxStepZ.x; z <= minMaxStepZ.y; z += minMaxStepZ.z) //this don't do that much
                    {
                        if (token.IsCancellationRequested)
                        {
                            return bestSolution;
                        }

                        Vector3 currentPos = new Vector3(x, y, z) * 0.001f;
                        Matrix4x4 tm = await Task.Run(() => SolveForPos(currentPos, refPoints, obsDirections));
                        float e = Evaluate(refPoints, obsDirections, tm, pointStash);
                        if (e < bestError)
                        {
                            bestError = e;
                            bestSolution = tm;
                        }
                    }
                }
            }
            return bestSolution;
        }
    }
}
