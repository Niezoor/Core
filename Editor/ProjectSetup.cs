using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;
using Task = System.Threading.Tasks.Task;

namespace Core.Editor
{
    public static class ProjectSetup
    {
        [MenuItem("Core/Project Setup/All", false, 0)]
        public static void Initial()
        {
            CreateFolders();
            ImportAssets();
            InstallPackages();
        }

        [MenuItem("Core/Project Setup/Create Folders")]
        public static void CreateFolders()
        {
            Folders.Create("Assets/Animations");
            Folders.Create("Assets/Audio");
            Folders.Create("Assets/Materials");
            Folders.Create("Assets/Prefabs");
            Folders.Create("Assets/Scripts");
            Folders.Create("Assets/Scripts/Editor");
            Folders.Create("Assets/ScriptableObjects");
            Folders.Create("Assets/Textures");
            Folders.Delete("Assets/TutorialInfo");
            AssetDatabase.DeleteAsset("Assets/Readme.asset");
            AssetDatabase.Refresh();
        }

        [MenuItem("Core/Project Setup/Import Assets")]
        public static void ImportAssets()
        {
            // Check paths in C:\Users\%userprofile%\AppData\Roaming\Unity\Asset Store-5.x\
            Assets.ImportAsset("Sirenix/Editor ExtensionsSystem/Odin Inspector and Serializer.unitypackage");
            Assets.ImportAsset("Stompy Robot LTD/ScriptingGUI/SRDebugger - Console Tools On-Device.unitypackage");
            Assets.ImportAsset("yasirkula/Editor ExtensionsUtilities/Asset Usage Detector.unitypackage");
            Assets.ImportAsset("Demigiant/Editor ExtensionsAnimation/DOTween HOTween v2.unitypackage");
            Assets.ImportAsset("Carlos Wilkes/Editor ExtensionsSystem/Lean Pool.unitypackage");
        }

        [MenuItem("Core/Project Setup/Install Packages")]
        public static void InstallPackages()
        {
            Packages.InstallPackages(new[]
            {
                "com.unity.inputsystem" // If necessary, import new Input System last as it requires a Unity Editor restart
            });
        }

        private static class Assets
        {
            public static void ImportAsset(string assetPath)
            {
                var basePath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                var assetsFolder = Path.Combine(basePath, "Unity/Asset Store-5.x");
                assetPath = assetPath.EndsWith(".unitypackage") ? assetPath : assetPath + ".unitypackage";
                AssetDatabase.ImportPackage(Path.Combine(assetsFolder, assetPath), false);
            }
        }

        private static class Packages
        {
            static AddRequest request;
            static Queue<string> packagesToInstall = new Queue<string>();

            public static void InstallPackages(string[] packages)
            {
                foreach (var package in packages)
                {
                    packagesToInstall.Enqueue(package);
                }

                if (packagesToInstall.Count > 0)
                {
                    StartNextPackageInstallation();
                }
            }

            static async void StartNextPackageInstallation()
            {
                request = Client.Add(packagesToInstall.Dequeue());

                while (!request.IsCompleted) await Task.Delay(10);

                if (request.Status == StatusCode.Success) Debug.Log("Installed: " + request.Result.packageId);
                else if (request.Status >= StatusCode.Failure) Debug.LogError(request.Error.message);

                if (packagesToInstall.Count > 0)
                {
                    await Task.Delay(1000);
                    StartNextPackageInstallation();
                }
            }
        }

        private static class Folders
        {
            public static void Create(params string[] folders)
            {
                var projectDirectoryInfo = Directory.GetParent(Application.dataPath);
                if (projectDirectoryInfo == null) return;
                var projectPath = projectDirectoryInfo.FullName;
                foreach (var folder in folders)
                {
                    var path = Path.Combine(projectPath, folder);
                    if (!Directory.Exists(path))
                    {
                        Directory.CreateDirectory(path);
                    }
                }
            }

            public static void Move(string sourcePath, string destinationPath)
            {
                if (AssetDatabase.IsValidFolder(sourcePath))
                {
                    var error = AssetDatabase.MoveAsset(sourcePath, destinationPath);

                    if (!string.IsNullOrEmpty(error))
                    {
                        Debug.LogError($"Failed to move {sourcePath} to {destinationPath} ({error})");
                    }
                }
            }

            public static void Delete(string assetPath)
            {
                if (AssetDatabase.IsValidFolder(assetPath))
                {
                    AssetDatabase.DeleteAsset(assetPath);
                }
            }
        }
    }
}