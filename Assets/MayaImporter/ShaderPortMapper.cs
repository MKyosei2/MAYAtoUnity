using System;
using System.Collections.Generic;
using UnityEngine;

namespace MayaImporter.Shader
{
    /// <summary>
    /// Maya Hypershade の plug / attribute 名を
    /// Unity 側で扱いやすい「入力ポート名 / 出力ポート名」に正規化する。
    ///
    /// 例:
    ///   Maya: file1.outColor      → outColor
    ///   Maya: lambert1.color      → color
    ///   Maya: place2dTexture1.outUV → outUV
    /// </summary>
    public static class ShaderPortMapper
    {
        private static readonly Dictionary<string, string> _inputPortMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static readonly Dictionary<string, string> _outputPortMap =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        private static bool _initialized;

        /// <summary>
        /// 初期化（1回のみ）
        /// </summary>
        public static void Initialize()
        {
            if (_initialized)
                return;

            _initialized = true;
            _inputPortMap.Clear();
            _outputPortMap.Clear();

            RegisterDefaultPorts();
        }

        #region Public API

        /// <summary>
        /// Maya plug 名から「入力ポート名」を取得
        /// </summary>
        public static string GetInputPortName(string mayaAttributeName)
        {
            if (string.IsNullOrEmpty(mayaAttributeName))
                return string.Empty;

            Initialize();

            if (_inputPortMap.TryGetValue(mayaAttributeName, out var port))
                return port;

            return NormalizeAttributeName(mayaAttributeName);
        }

        /// <summary>
        /// Maya plug 名から「出力ポート名」を取得
        /// </summary>
        public static string GetOutputPortName(string mayaAttributeName)
        {
            if (string.IsNullOrEmpty(mayaAttributeName))
                return string.Empty;

            Initialize();

            if (_outputPortMap.TryGetValue(mayaAttributeName, out var port))
                return port;

            return NormalizeAttributeName(mayaAttributeName);
        }

        #endregion


        #region Registration

        private static void RegisterDefaultPorts()
        {
            // ===== 共通 =====
            RegisterOutput("outColor");
            RegisterOutput("outAlpha");
            RegisterOutput("outValue");
            RegisterOutput("outUV");

            // ===== File Texture =====
            RegisterInput("fileTextureName", "filePath");
            RegisterInput("colorGain", "colorGain");
            RegisterInput("colorOffset", "colorOffset");
            RegisterInput("alphaGain", "alphaGain");
            RegisterInput("alphaOffset", "alphaOffset");

            // ===== Place2DTexture =====
            RegisterOutput("outUV", "uv");
            RegisterOutput("outUvFilterSize", "uvFilterSize");

            RegisterInput("coverage");
            RegisterInput("translateFrame");
            RegisterInput("rotateFrame");
            RegisterInput("mirrorU");
            RegisterInput("mirrorV");
            RegisterInput("wrapU");
            RegisterInput("wrapV");
            RegisterInput("repeatUV");
            RegisterInput("offset");
            RegisterInput("rotateUV");
            RegisterInput("noiseUV");

            // ===== Lambert / Phong / Blinn =====
            RegisterInput("color", "albedo");
            RegisterInput("incandescence", "emission");
            RegisterInput("transparency", "opacity");

            RegisterOutput("outColor", "color");

            // ===== Utility =====
            RegisterInput("input1");
            RegisterInput("input2");
            RegisterOutput("output");
        }

        private static void RegisterInput(string mayaAttr, string portName = null)
        {
            if (string.IsNullOrEmpty(mayaAttr))
                return;

            _inputPortMap[mayaAttr] = portName ?? mayaAttr;
        }

        private static void RegisterOutput(string mayaAttr, string portName = null)
        {
            if (string.IsNullOrEmpty(mayaAttr))
                return;

            _outputPortMap[mayaAttr] = portName ?? mayaAttr;
        }

        #endregion


        #region Utilities

        /// <summary>
        /// Maya attribute 名を Unity 用に正規化
        /// </summary>
        private static string NormalizeAttributeName(string mayaAttributeName)
        {
            var attr = mayaAttributeName;

            // compound attribute 対応（colorR → color）
            if (attr.EndsWith("R", StringComparison.OrdinalIgnoreCase) ||
                attr.EndsWith("G", StringComparison.OrdinalIgnoreCase) ||
                attr.EndsWith("B", StringComparison.OrdinalIgnoreCase) ||
                attr.EndsWith("A", StringComparison.OrdinalIgnoreCase))
            {
                attr = attr.Substring(0, attr.Length - 1);
            }

            return attr;
        }

        #endregion
    }
}
