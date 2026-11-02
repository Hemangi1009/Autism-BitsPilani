using System;
using System.IO;
using System.Xml;
using UnityEditor.Android;

namespace BitsPilani.Module1.Playground.Editor {
    // Merge the declaration into Unity's generated manifest when the user builds.
    // Leaves package manifests, activities, storage permissions and Player Settings intact.
    public sealed class PlaygroundMicrophoneManifest : IPostGenerateGradleAndroidProject {
        const string Android = "http://schemas.android.com/apk/res/android";
        public int callbackOrder => 100;
        public void OnPostGenerateGradleAndroidProject(string path) {
            EnsureMicrophonePermission(Path.Combine(path, "src", "main", "AndroidManifest.xml"));
        }
        public static void EnsureMicrophonePermission(string manifestPath) {
            var document = new XmlDocument { PreserveWhitespace = true };
            document.Load(manifestPath);
            var manifest = document.DocumentElement;
            if (manifest == null || manifest.Name != "manifest") throw new InvalidOperationException("Android manifest root is missing: " + manifestPath);
            foreach (XmlNode node in manifest.ChildNodes) {
                if (node is XmlElement element && element.Name == "uses-permission" && element.GetAttribute("name", Android) == "android.permission.RECORD_AUDIO") return;
            }
            var permission = document.CreateElement("uses-permission");
            var name = document.CreateAttribute("android", "name", Android); name.Value = "android.permission.RECORD_AUDIO";
            permission.Attributes.Append(name); manifest.AppendChild(permission);
            document.Save(manifestPath);
        }
    }
}
