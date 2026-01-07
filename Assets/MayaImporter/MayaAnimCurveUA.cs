// PATCH: ProductionImpl v6 (Unity-only, retention-first)
using UnityEngine;
using MayaImporter.Core;

namespace MayaImporter.Animation
{
    [DisallowMultipleComponent]
    [MayaNodeType("animCurveUA")]
    public sealed class MayaAnimCurveUA : MayaAnimCurveNodeComponent { }
}
