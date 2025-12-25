// Assets/MayaImporter/MayaFloatValueExtensions_Nodes.cs
// MayaImporter.Nodes 名前空間に存在する MayaFloatValue（AddDoubleLinearNode.cs 内で定義されている型）向けの互換拡張。
// UnitConversionNode / DecomposeMatrixNode の outVal.Set(...) を解決する。

namespace MayaImporter.Nodes
{
    public static class MayaFloatValueExtensions_Nodes
    {
        /// <summary>
        /// 互換: Set(float) - MayaImporter.Nodes.MayaFloatValue 用
        /// </summary>
        public static void Set(this MayaFloatValue v, float value)
        {
            if (v == null) return;
            v.valid = true;
            v.value = value;
        }

        /// <summary>
        /// 互換: Set(maya, unity) - MayaImporter.Nodes.MayaFloatValue 用
        /// （Nodes版は単一 value しか持たないので unity を優先して格納）
        /// </summary>
        public static void Set(this MayaFloatValue v, float maya, float unity)
        {
            Set(v, unity);
        }
    }
}
