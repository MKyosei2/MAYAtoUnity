// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    [DisallowMultipleComponent]
    [MayaNodeType("animCurveTU")]
    public sealed class MayaAnimCurveTU : MayaAnimCurveNodeComponent { }
}
