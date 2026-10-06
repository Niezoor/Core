using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.ProjectWindowCallback;
using UnityEngine;

namespace Core.UISystem.Editor
{
    /// <summary>Assets › Create › Core › UI Screen: the screen class, its UXML and USS, paired after compilation.</summary>
    internal static class CreateScreenMenu
    {
        [MenuItem("Assets/Create/Core/UI Screen", priority = 81)]
        private static void Create()
        {
            var icon = EditorGUIUtility.IconContent("cs Script Icon").image as Texture2D;
            ProjectWindowUtil.StartNameEditingIfProjectWindowExists(0,
                ScriptableObject.CreateInstance<CreateScreenAction>(), "NewScreen.cs", icon, null);
        }

        internal static string[] Write(string directory, string className)
        {
            var kebab = Regex.Replace(className, "(?<!^)([A-Z])", "-$1").ToLowerInvariant();
            var files = new[]
            {
                Path.Combine(directory, className + ".cs"),
                Path.Combine(directory, className + ".uxml"),
                Path.Combine(directory, className + ".uss"),
            };

            WriteNew(files[0], $@"using Core.UISystem;
using UnityEngine.UIElements;

public sealed class {className} : UIScreen
{{
    protected override void OnCreate()
    {{
        Q<Button>(""close"").clicked += Close;
    }}
}}
");
            WriteNew(files[1], $@"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:core=""Core.UISystem"">
    <Style src=""{className}.uss"" />
    <core:SafeArea class=""{kebab}"">
        <ui:Label text=""{className}"" class=""{kebab}__title"" />
        <ui:Button name=""close"" text=""Close"" />
    </core:SafeArea>
</ui:UXML>
");
            WriteNew(files[2], $@".{kebab} {{
    align-items: center;
    justify-content: center;
}}

.{kebab}__title {{
    font-size: 32px;
}}
");
            return files;
        }

        private static void WriteNew(string path, string content)
        {
            if (File.Exists(path)) return;
            File.WriteAllText(path, content.Replace("\r\n", "\n"), new UTF8Encoding(false));
        }
    }

    internal sealed class CreateScreenAction : EndNameEditAction
    {
        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            var directory = Path.GetDirectoryName(pathName) ?? "Assets";
            var className = Regex.Replace(Path.GetFileNameWithoutExtension(pathName), "[^A-Za-z0-9_]", string.Empty);
            if (className.Length == 0 || char.IsDigit(className[0])) className = "New" + className;

            var files = CreateScreenMenu.Write(directory, className);
            // The class exists only after compilation; the registry sync then pairs it and creates the settings.
            SessionState.SetBool(ScreenRegistry.CreateSettingsKey, true);
            foreach (var file in files) AssetDatabase.ImportAsset(file.Replace('\\', '/'));
            ProjectWindowUtil.ShowCreatedAsset(AssetDatabase.LoadAssetAtPath<Object>(files[0].Replace('\\', '/')));
        }
    }
}
