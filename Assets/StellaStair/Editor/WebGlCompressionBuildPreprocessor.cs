using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

namespace StellaStair.Editor
{
    public sealed class WebGlCompressionBuildPreprocessor : IPreprocessBuildWithReport
    {
        public int callbackOrder => int.MinValue;

        public void OnPreprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.WebGL)
                return;

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
            PlayerSettings.WebGL.decompressionFallback = false;
        }
    }
}
