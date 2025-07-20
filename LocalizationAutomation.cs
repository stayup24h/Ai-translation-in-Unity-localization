// Assets/Editor/LocalizationAutomation.cs
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using System.IO;
using System.Diagnostics; 
using System.Linq;
using System.Threading.Tasks; 
using UnityEngine.Localization.Settings; 
using UnityEditor.Localization.Plugins.CSV; // Csv.Export 및 Csv.ImportInto를 위해 필요

public class LocalizationAutomation : EditorWindow
{
    private static string s_googleCloudApiKey = "YOUR_GOOGLE_CLOUD_API_KEY_HERE"; 
    private static string s_googleCloudProjectId = "YOUR_GOOGLE_CLOUD_PROJECT_ID_HERE"; 

    private static string s_csvExportPath = "Assets/LocalizationData/ExportedStrings.csv";
    private static string s_pythonScriptPath = "Path/To/Your/translation_script.py";
    private static string s_translatedCsvPath = "Assets/LocalizationData/ExportedStrings_translated.csv";
    private static string s_pythonExePath = "python"; 

    [MenuItem("Localization/Automate Translate CSV")]
    public static void ShowWindow()
    {
        GetWindow<LocalizationAutomation>("Localization Automation");
    }

    private void OnGUI()
    {
        GUILayout.Label("Localization CSV Automation", EditorStyles.boldLabel);

        s_googleCloudApiKey = EditorGUILayout.TextField("Google Cloud API Key", s_googleCloudApiKey);
        s_googleCloudProjectId = EditorGUILayout.TextField("Google Cloud Project ID", s_googleCloudProjectId);
        GUILayout.Space(5);

        s_csvExportPath = EditorGUILayout.TextField("Export CSV Path", s_csvExportPath);
        s_pythonScriptPath = EditorGUILayout.TextField("Python Script Path", s_pythonScriptPath);
        s_translatedCsvPath = EditorGUILayout.TextField("Translated CSV Path", s_translatedCsvPath);
        s_pythonExePath = EditorGUILayout.TextField("Python Executable Path", s_pythonExePath);

        GUILayout.Space(10);

        if (GUILayout.Button("Automate All (Export -> Translate -> Import)"))
        {
            AutomateAll();
        }
    }

    private static Task ExportAllStringTablesToCsvAsync()
    {
        var tcs = new TaskCompletionSource<bool>();
        
        StringTableCollection stringTableCollection = null;

        string[] tableGuids = AssetDatabase.FindAssets("t:StringTable");
        if (tableGuids.Length > 0)
        {
            string tablePath = AssetDatabase.GUIDToAssetPath(tableGuids[0]);
            StringTable firstStringTable = AssetDatabase.LoadAssetAtPath<StringTable>(tablePath);

            if (firstStringTable != null)
            {
                stringTableCollection = (StringTableCollection)LocalizationEditorSettings.GetCollectionFromTable(firstStringTable);
            }
            else
            {
                UnityEngine.Debug.LogError("Failed to load the first StringTable found.");
            }

            if (tableGuids.Length > 1)
            {
                UnityEngine.Debug.LogWarning("Multiple StringTables found. Using the collection associated with the first one: " + tablePath + 
                                             "\nIf you need to specify a different collection, please modify the script.");
            }
        }

        if (stringTableCollection == null)
        {
            UnityEngine.Debug.LogError("No String Table Collection found (or could not be derived from a StringTable). Please ensure a String Table Collection exists and contains at least one String Table.");
            tcs.SetResult(false);
            return tcs.Task;
        }
        
        var stringTables = stringTableCollection.StringTables;

        if (stringTables == null || !stringTables.Any())
        {
            UnityEngine.Debug.LogError($"No String Tables found in the '{stringTableCollection.name}' String Table Collection. Please create or load some String Tables.");
            tcs.SetResult(false);
            return tcs.Task;
        }

        UnityEngine.Debug.Log($"Exporting all String Tables to CSV: {s_csvExportPath}");
        EditorUtility.DisplayProgressBar("Localization Automation", "Exporting String Tables...", 0.33f);

        // --- EXPORT LOGIC (using Csv.Export) ---
        try
        {
            string directory = Path.GetDirectoryName(s_csvExportPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using (var textWriter = new StreamWriter(s_csvExportPath, false, System.Text.Encoding.UTF8))
            {
                Csv.Export(textWriter, stringTableCollection); 
            }

            UnityEngine.Debug.Log($"Successfully exported String Tables to: {s_csvExportPath}");
            AssetDatabase.Refresh();
            tcs.SetResult(true);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to export String Tables: {e.Message}");
            tcs.SetResult(false);
        }
        // --- END EXPORT LOGIC ---

        return tcs.Task;
    }

    private static bool RunPythonScript() 
    {
        if (!File.Exists(s_pythonScriptPath))
        {
            UnityEngine.Debug.LogError($"Python script not found at: {s_pythonScriptPath}");
            return false;
        }
        if (string.IsNullOrEmpty(s_googleCloudApiKey) || s_googleCloudApiKey == "YOUR_GOOGLE_CLOUD_API_KEY_HERE")
        {
            UnityEngine.Debug.LogError("Please enter your Google Cloud API Key in the editor window.");
            return false;
        }
        if (string.IsNullOrEmpty(s_googleCloudProjectId) || s_googleCloudProjectId == "YOUR_GOOGLE_CLOUD_PROJECT_ID_HERE")
        {
            UnityEngine.Debug.LogError("Please enter your Google Cloud Project ID in the editor window.");
            return false;
        }

        UnityEngine.Debug.Log($"Running Python script: {s_pythonScriptPath}");
        EditorUtility.DisplayProgressBar("Localization Automation", "Running Python Translation Script...", 0.66f);

        ProcessStartInfo start = new ProcessStartInfo();
        start.FileName = s_pythonExePath;
        start.Arguments = $"{s_pythonScriptPath} \"{s_googleCloudApiKey}\" \"{s_googleCloudProjectId}\" \"{s_csvExportPath}\" \"{s_translatedCsvPath}\"";

        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;
        start.CreateNoWindow = true; 

        try
        {
            using (Process process = Process.Start(start))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(stdout)) UnityEngine.Debug.Log($"Python Script Output:\n{stdout}");
                if (!string.IsNullOrEmpty(stderr)) UnityEngine.Debug.LogError($"Python Script Error:\n{stderr}");

                if (process.ExitCode == 0)
                {
                    UnityEngine.Debug.Log("Python script executed successfully.");
                    AssetDatabase.Refresh();
                    return true;
                }
                else
                {
                    UnityEngine.Debug.LogError($"Python script exited with error code: {process.ExitCode}");
                    return false;
                }
            }
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Error running Python script: {e.Message}");
            return false;
        }
    }

    private static Task ImportTranslatedCsvToStringTablesAsync()
    {
        var tcs = new TaskCompletionSource<bool>();

        StringTableCollection currentTableCollection = null;
        
        string[] tableGuids = AssetDatabase.FindAssets("t:StringTable");
        if (tableGuids.Length > 0)
        {
            string tablePath = AssetDatabase.GUIDToAssetPath(tableGuids[0]);
            StringTable firstStringTable = AssetDatabase.LoadAssetAtPath<StringTable>(tablePath);
            if (firstStringTable != null)
            {
                currentTableCollection = (StringTableCollection)LocalizationEditorSettings.GetCollectionFromTable(firstStringTable);
            }
        }

        if (currentTableCollection == null)
        {
            UnityEngine.Debug.LogError("No String Table Collection found (or could not be derived from a StringTable) for import. Please ensure a String Table Collection exists and contains at least one String Table.");
            tcs.SetResult(false);
            return tcs.Task;
        }

        if (!File.Exists(s_translatedCsvPath))
        {
            UnityEngine.Debug.LogError($"Translated CSV file not found at: {s_translatedCsvPath}");
            tcs.SetResult(false);
            return tcs.Task;
        }

        UnityEngine.Debug.Log($"Importing translated CSV: {s_translatedCsvPath} into String Tables.");
        EditorUtility.DisplayProgressBar("Localization Automation", "Importing Translated CSV...", 1.0f);

        // --- IMPORT LOGIC (using Csv.ImportInto) ---
        try
        {
            using (var textReader = new StreamReader(s_translatedCsvPath, System.Text.Encoding.UTF8))
            {
                // Csv.Import 대신 Csv.ImportInto 사용
                Csv.ImportInto(textReader, currentTableCollection); 
            }

            UnityEngine.Debug.Log($"Successfully imported translated CSV to String Tables.");
            AssetDatabase.SaveAssets(); 
            AssetDatabase.Refresh();    
            tcs.SetResult(true);
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Failed to import translated CSV: {e.Message}");
            tcs.SetResult(false);
        }
        // --- END IMPORT LOGIC ---

        return tcs.Task;
    }

    private async static void AutomateAll()
    {
        EditorUtility.DisplayProgressBar("Localization Automation", "Starting Automation...", 0.0f);

        try
        {
            UnityEngine.Debug.Log("Automating all localization steps: Step 1/3 - Exporting String Tables...");
            await ExportAllStringTablesToCsvAsync();

            UnityEngine.Debug.Log("Automating all localization steps: Step 2/3 - Running Python Translation Script...");
            bool pythonSuccess = await Task.Run(() => RunPythonScript());

            if (!pythonSuccess)
            {
                UnityEngine.Debug.LogError("Python script failed. Aborting automation.");
                return;
            }

            UnityEngine.Debug.Log("Automating all localization steps: Step 3/3 - Importing Translated CSV...");
            await ImportTranslatedCsvToStringTablesAsync();

            UnityEngine.Debug.Log("Automation completed successfully!");
        }
        catch (System.Exception e)
        {
            UnityEngine.Debug.LogError($"Automation failed: {e.Message}");
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }
    }
}