#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Callbacks;
using UnityEditor.iOS.Xcode;

namespace MutagenEditor
{
    /// <summary>
    /// Expose the app's Documents folder (Application.persistentDataPath) in the iOS Files app,
    /// so Balance Lab CSVs written on-device can be opened and shared directly from the phone
    /// ("Files → On My iPhone → Morphage"). Runs automatically on every iOS build.
    /// </summary>
    public static class IosPostBuild
    {
        [PostProcessBuild]
        public static void OnPostProcessBuild(BuildTarget target, string buildPath)
        {
            if (target != BuildTarget.iOS) return;
            string plistPath = Path.Combine(buildPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetBoolean("UIFileSharingEnabled", true);          // folder visible in the Files app
            plist.root.SetBoolean("LSSupportsOpeningDocumentsInPlace", true); // open files where they live
            plist.WriteToFile(plistPath);
        }
    }
}
#endif
