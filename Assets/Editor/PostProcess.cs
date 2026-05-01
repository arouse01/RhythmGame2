using System.IO;
using UnityEditor.Build.Reporting;
using UnityEditor.Build;
using UnityEngine;


public class PostProcess : IPostprocessBuildWithReport
{
    // Specify the callback order if needed; lower numbers run first
    public int callbackOrder => 0;

    public void OnPostprocessBuild(BuildReport report)
    {
        // Path to the folder where the build is created
        string buildFolder = Path.GetDirectoryName(report.summary.outputPath);

        // Define your source files
        string parameterFile = Path.Combine(Directory.GetCurrentDirectory(), "parameters.txt");  // The ".." makes us go one folder above the data folder, which is where the application exe is
        string trialFolder = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "PhaseParams");
        //Path.Combine(Application.dataPath, "Parameters", "file1.txt");
        // Define the target destination (build folder)
        string paramFileDest = Path.Combine(buildFolder, "parameters.txt");
        string trialFolderDest = Path.Combine(buildFolder, "PhaseParams");

        // Copy files if they exist
        try
        {
            if (File.Exists(parameterFile))
                File.Copy(parameterFile, paramFileDest, overwrite: true);
            else
                Debug.LogWarning($"Source file not found: {parameterFile}");

            if (Directory.Exists(trialFolder))
            {
                CopyDirectory(trialFolder, trialFolderDest);
                Debug.Log("Folder copied successfully.");
            }
            else
                Debug.LogWarning($"Source file not found: {trialFolder}");

            Debug.Log("Parameter files copied successfully to the build folder.");
        }
        catch (System.Exception ex)
        {
            Debug.LogError($"Error copying parameter files: {ex.Message}");
        }
    }

    private void CopyDirectory(string sourceDir, string destinationDir)
    {
        // Create the destination directory if it doesn't exist
        Directory.CreateDirectory(destinationDir);

        // Copy all files
        foreach (string file in Directory.GetFiles(sourceDir, "*.txt"))
        {
            string fileName = Path.GetFileName(file);
            string destFile = Path.Combine(destinationDir, fileName);
            File.Copy(file, destFile, overwrite: true);
        }

        //// Copy all subdirectories
        //foreach (string subDir in Directory.GetDirectories(sourceDir))
        //{
        //    string dirName = Path.GetFileName(subDir);
        //    string destSubDir = Path.Combine(destinationDir, dirName);
        //    CopyDirectory(subDir, destSubDir); // Recursive call
        //}
    }
}
