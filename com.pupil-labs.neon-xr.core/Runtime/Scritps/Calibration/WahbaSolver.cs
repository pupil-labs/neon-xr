using UnityEngine;

public class WahbaSolver
{
    // Same internal layout as KabschSolver for minimal changes
    Vector3[] QuatBasis = new Vector3[3];
    Vector3[] DataCovariance = new Vector3[3];
    Quaternion OptimalRotation = Quaternion.identity;

    /// <summary>
    /// Rotation-only Wahba solver (directions -> directions derived from points).
    /// inDirs: input ray directions (will be normalized).
    /// refPoints: target 3D points; their directions from origin are used as targets. .w is a weight.
    /// squaredNormWeights: if true, multiply weight by |refDir|^2 (matches point-to-ray objective).
    /// Returns a pure rotation Matrix4x4.
    /// </summary>
    public Matrix4x4 SolveWahba(Vector3[] inDirs, Vector4[] refPoints, bool squaredNormWeights = true)
    {
        if (inDirs == null || refPoints == null || inDirs.Length != refPoints.Length || inDirs.Length == 0)
            return Matrix4x4.identity;

        // Build 3x3 correlation matrix B = sum_i w_i * (v_i * u_i^T),
        // where u_i = normalized(inDirs[i]), v_i = normalized(refPoints[i].xyz)
        TransposeMultWahba(inDirs, refPoints, squaredNormWeights, DataCovariance);

        // Reuse the same stable rotation extraction
        OptimalRotation = Quaternion.identity;
        extractRotation(DataCovariance, ref OptimalRotation);

        // Rotation about the origin
        return Matrix4x4.TRS(Vector3.zero, OptimalRotation, Vector3.one);
    }

    // Correlation builder for Wahba
    static Vector3[] TransposeMultWahba(Vector3[] inDirs, Vector4[] refPts, bool squaredNormWeights, Vector3[] covariance)
    {
        for (int i = 0; i < 3; i++) covariance[i] = Vector3.zero;

        for (int k = 0; k < inDirs.Length; k++)
        {
            // u: input ray direction (from sensor), normalized
            Vector3 u = inDirs[k];
            float um = u.magnitude;
            if (um <= 1e-12f) continue;
            u /= um;

            // v: target direction (from sensor toward the world point), normalized
            Vector3 p = new Vector3(refPts[k].x, refPts[k].y, refPts[k].z); //TODO origin
            float pm = p.magnitude;
            if (pm <= 1e-12f) continue;
            Vector3 v = p / pm;

            // weight (optionally scaled by |pm^2| to match point - to - ray distance weighting)
            float w = refPts[k].w;
            if (squaredNormWeights) w *= pm * pm;
            if (w == 0f) continue;

            // Accumulate w * v * u^T into covariance
            covariance[0][0] += w * u[0] * v[0];
            covariance[1][0] += w * u[1] * v[0];
            covariance[2][0] += w * u[2] * v[0];

            covariance[0][1] += w * u[0] * v[1];
            covariance[1][1] += w * u[1] * v[1];
            covariance[2][1] += w * u[2] * v[1];

            covariance[0][2] += w * u[0] * v[2];
            covariance[1][2] += w * u[1] * v[2];
            covariance[2][2] += w * u[2] * v[2];
        }

        return covariance;
    }

    //https://animation.rwth-aachen.de/media/papers/2016-MIG-StableRotation.pdf
    //Iteratively apply torque to the basis using Cross products (in place of SVD)
    void extractRotation(Vector3[] A, ref Quaternion q)
    {
        for (int iter = 0; iter < 9; iter++)
        {
            q.FillMatrixFromQuaternion(ref QuatBasis);
            Vector3 omega = (Vector3.Cross(QuatBasis[0], A[0]) +
                             Vector3.Cross(QuatBasis[1], A[1]) +
                             Vector3.Cross(QuatBasis[2], A[2])) *
             (1f / Mathf.Abs(Vector3.Dot(QuatBasis[0], A[0]) +
                             Vector3.Dot(QuatBasis[1], A[1]) +
                             Vector3.Dot(QuatBasis[2], A[2]) + 0.000000001f));

            float w = omega.magnitude;
            if (w < 0.000000001f)
                break;
            q = Quaternion.AngleAxis(w * Mathf.Rad2Deg, omega / w) * q;
            q = Quaternion.Lerp(q, q, 0f); //Normalizes the Quaternion; critical for error suppression
        }
    }
}
