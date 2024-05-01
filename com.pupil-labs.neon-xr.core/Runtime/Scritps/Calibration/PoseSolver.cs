using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace PupilLabs.Calibration
{
    public abstract class PoseSolver : MonoBehaviour
    {
        [SerializeField] protected int initialCapacity = 32;

        [Header("Visualization")]
        [SerializeField] protected Transform pointParent;
        [SerializeField] protected GameObject referencePointPrefab;
        [SerializeField] protected GameObject transformedPointPrefab;

        protected List<GameObject> pointStash = null;
        protected List<Vector3> observedDirections = null;
        protected List<Vector3> referencePoints = null;
        protected Matrix4x4 solution = Matrix4x4.identity;
        protected float error = 0.0f;

        public bool Ready { get; private set; } = false;
        public Matrix4x4 Solution { get { return solution; } }
        public float Error { get { return error; } }
        public int SampleCount { get { return referencePoints.Count; } }

        protected virtual void Awake()
        {
            observedDirections = new List<Vector3>(initialCapacity);
            referencePoints = new List<Vector3>(initialCapacity);

            if (pointParent != null)
            {
                pointStash = new List<GameObject>(initialCapacity);
                pointParent.gameObject.SetActive(false);
            }

            Ready = true;
        }

        public virtual void AddSample(Vector3 referencePoint, Vector3 observedDirection)
        {
            observedDirections.Add(observedDirection.normalized); //sensor space
            referencePoints.Add(referencePoint); //camera space

            if (pointStash != null)
            {
                if (referencePoints.Count * 2 > pointStash.Count)
                {
                    GameObject tmpGo = GameObject.Instantiate(referencePointPrefab, pointParent);
                    tmpGo.SetActive(false);
                    pointStash.Add(tmpGo);
                    tmpGo = GameObject.Instantiate(transformedPointPrefab, pointParent);
                    tmpGo.SetActive(false);
                    pointStash.Add(tmpGo);
                }
            }
        }

        public virtual void Clear()
        {
            observedDirections.Clear();
            referencePoints.Clear();
        }

        public async Task Solve()
        {
            await Solve(CancellationToken.None);
        }

        public virtual async Task Solve(CancellationToken token)
        {
            if (Ready == false)
            {
                Debug.LogError("[PoseSolver] Solver not ready.");
                return;
            }
            Ready = false;
            Vector3[] refPoints = referencePoints.ToArray();
            Vector3[] obsDirections = observedDirections.ToArray();
            solution = await Solve(refPoints, obsDirections, token);
            Evaluate(refPoints, obsDirections, pointStash);
            Ready = true;
        }

        protected abstract Task<Matrix4x4> Solve(Vector3[] refPoints, Vector3[] obsDirections, CancellationToken token);

        protected virtual float Evaluate(Vector3[] refPoints, Vector3[] obsDirections, List<GameObject> pStash = null)
        {
            return Evaluate(refPoints, obsDirections, solution, pStash);
        }

        protected static float Evaluate(Vector3[] refPoints, Vector3[] obsDirections, Matrix4x4 tm, List<GameObject> pStash = null)
        {
            float error = 0f;
            Vector3 solvedPos = tm.GetPosition();
            for (int i = 0; i < refPoints.Length; i++)
            {
                float distance = Vector3.Distance(solvedPos, refPoints[i]);
                var transformedPoint = tm.MultiplyPoint(obsDirections[i] * distance); //can be flatten to 2D
                error += Vector3.Angle(refPoints[i], transformedPoint);
                if (pStash != null)
                {
                    pStash[i * 2].transform.localPosition = refPoints[i];
                    pStash[i * 2 + 1].transform.localPosition = transformedPoint;
                }
            }
            if (pStash != null)
            {
                for (int i = 0; i < pStash.Count; i++)
                {
                    pStash[i].SetActive(i < refPoints.Length * 2);
                }
            }
            return error / obsDirections.Length;
        }

        public virtual void SetVisualizationActive(bool value)
        {
            if (pointParent == null)
            {
                Debug.LogError("[PoseControl] Visualization disabled, parent transform for points was not set.");
                return;
            }
            pointParent.gameObject.SetActive(value);
        }

        private void OnDestroy()
        {
            if (pointStash != null)
            {
                pointStash.Clear();
                Destroy(pointParent.gameObject);
            }
        }
    }
}
