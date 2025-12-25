using UnityEngine;

namespace MayaImporter.Animation
{
    /// <summary>
    /// Maya IK handle node.
    /// Stores IK chain definition.
    /// </summary>
    [DisallowMultipleComponent]
    public class IKHandleNode : MonoBehaviour
    {
        [Header("IK Chain")]
        public Transform startJoint;
        public Transform endJoint;

        [Header("Solver")]
        public string solverType; // ikRPsolver, ikSCsolver, etc.

        public void Initialize(
            Transform start,
            Transform end,
            string solver)
        {
            startJoint = start;
            endJoint = end;
            solverType = solver;
        }
    }
}
