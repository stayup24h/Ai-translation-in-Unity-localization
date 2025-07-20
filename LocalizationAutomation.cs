// Assets/Editor/LocalizationAutomation.cs
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;
using System.IO;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks; // Task를 사용하기 위해 필요

public class LocalizationAutomation : EditorWindow
{
    private static string s_googleCloudApiKey = "YOUR_GOOGLE_CLOUD_API_KEY_HERE"; // Google Cloud Translation API 키
    private static string s_googleCloudProjectId = "YOUR_GOOGLE_CLOUD_PROJECT_ID_HERE"; // Google Cloud 프로젝트 ID

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

    private static Task ExportAllStringTablesToCsvAsync() // Task 반환 타입으로 변경
    {
        var tcs = new TaskCompletionSource<bool>(); // Task 완료를 제어할 TaskCompletionSource

        if (LocalizationEditorSettings.ActiveTableCollection == null)
        {
            Debug.LogError("No active String Table Collection found. Please open the Localization Tables window.");
            tcs.SetResult(false); // Task를 실패로 완료
            return tcs.Task;
        }

        Debug.Log($"Exporting all String Tables to CSV: {s_csvExportPath}");
        EditorUtility.DisplayProgressBar("Localization Automation", "Exporting String Tables...", 0.33f); // 1/3 진행

        var exportOperation = LocalizationEditorSettings.ActiveTableCollection.ExportToFile(
            LocalizationEditorSettings.ActiveTableCollection.StringTables.ToArray(),
            s_csvExportPath,
            CsvColumns.AllTableEntries);

        exportOperation.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"Successfully exported String Tables to: {s_csvExportPath}");
                AssetDatabase.Refresh();
                tcs.SetResult(true); // Task를 성공으로 완료
            }
            else
            {
                Debug.LogError($"Failed to export String Tables: {op.OperationException}");
                tcs.SetResult(false); // Task를 실패로 완료
            }
        };

        return tcs.Task;
    }

    private static bool RunPythonScript() // bool 반환 타입으로 변경 (성공 여부)
    {
        if (!File.Exists(s_pythonScriptPath))
        {
            Debug.LogError($"Python script not found at: {s_pythonScriptPath}");
            return false;
        }
        if (string.IsNullOrEmpty(s_googleCloudApiKey) || s_googleCloudApiKey == "YOUR_GOOGLE_CLOUD_API_KEY_HERE")
        {
            Debug.LogError("Please enter your Google Cloud API Key in the editor window.");
            return false;
        }
        if (string.IsNullOrEmpty(s_googleCloudProjectId) || s_googleCloudProjectId == "YOUR_GOOGLE_CLOUD_PROJECT_ID_HERE")
        {
            Debug.LogError("Please enter your Google Cloud Project ID in the editor window.");
            return false;
        }

        Debug.Log($"Running Python script: {s_pythonScriptPath}");
        EditorUtility.DisplayProgressBar("Localization Automation", "Running Python Translation Script...", 0.66f); // 2/3 진행

        ProcessStartInfo start = new ProcessStartInfo();
        start.FileName = s_pythonExePath;
        // API 키와 프로젝트 ID를 인자로 추가 (Python 스크립트 수정 필요: 인자 4개 받도록)
        // 현재 Python 스크립트의 인자 개수 3개를 유지하려면, Python 스크립트 내에서 프로젝트 ID를 하드코딩해야 합니다.
        // 하지만 여기서는 Unity에서 모두 전달하는 방식으로 가이드합니다.
        // Python 스크립트의 main 함수 부분을 다음과 같이 수정해야 합니다:
        // if len(sys.argv) != 5:
        //     print("Python 오류: 올바른 사용법: python translation_script.py <api_key> <project_id> <input_csv_path> <output_csv_path>")
        //     sys.exit(1)
        // API_KEY = sys.argv[1]
        // GOOGLE_CLOUD_PROJECT_ID = sys.argv[2]
        // input_csv_file = sys.argv[3]
        // output_csv_file = sys.argv[4]
        start.Arguments = $"{s_pythonScriptPath} \"{s_googleCloudApiKey}\" \"{s_googleCloudProjectId}\" \"{s_csvExportPath}\" \"{s_translatedCsvPath}\"";

        start.UseShellExecute = false;
        start.RedirectStandardOutput = true;
        start.RedirectStandardError = true;

        try
        {
            using (Process process = Process.Start(start))
            {
                string stdout = process.StandardOutput.ReadToEnd();
                string stderr = process.StandardError.ReadToEnd();
                process.WaitForExit();

                if (!string.IsNullOrEmpty(stdout)) Debug.Log($"Python Script Output:\n{stdout}");
                if (!string.IsNullOrEmpty(stderr)) Debug.LogError($"Python Script Error:\n{stderr}");

                if (process.ExitCode == 0)
                {
                    Debug.Log("Python script executed successfully.");
                    AssetDatabase.Refresh();
                    return true;
                }
                else
                {
                    Debug.LogError($"Python script exited with error code: {process.ExitCode}");
                    return false;
                }
            }
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Error running Python script: {e.Message}");
            return false;
        }
    }

    private static Task ImportTranslatedCsvToStringTablesAsync() // Task 반환 타입으로 변경
    {
        var tcs = new TaskCompletionSource<bool>(); // Task 완료를 제어할 TaskCompletionSource

        if (LocalizationEditorSettings.ActiveTableCollection == null)
        {
            Debug.LogError("No active String Table Collection found. Please open the Localization Tables window.");
            tcs.SetResult(false);
            return tcs.Task;
        }

        if (!File.Exists(s_translatedCsvPath))
        {
            Debug.LogError($"Translated CSV file not found at: {s_translatedCsvPath}");
            tcs.SetResult(false);
            return tcs.Task;
        }

        Debug.Log($"Importing translated CSV: {s_translatedCsvPath} into String Tables.");
        EditorUtility.DisplayProgressBar("Localization Automation", "Importing Translated CSV...", 1.0f); // 마지막 단계

        var importOperation = LocalizationEditorSettings.ActiveTableCollection.ImportFromPath(s_translatedCsvPath);

        importOperation.Completed += (op) =>
        {
            if (op.Status == AsyncOperationStatus.Succeeded)
            {
                Debug.Log($"Successfully imported translated CSV to String Tables.");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                tcs.SetResult(true);
            }
            else
            {
                Debug.LogError($"Failed to import translated CSV: {op.OperationException}");
                tcs.SetResult(false);
            }
        };

        return tcs.Task;
    }

    private async static void AutomateAll() // async로 변경하여 await 사용 가능
    {
        EditorUtility.DisplayProgressBar("Localization Automation", "Starting Automation...", 0.0f); // 시작

        try
        {
            // 1. Export
            Debug.Log("Automating all localization steps: Step 1/3 - Exporting String Tables...");
            await ExportAllStringTablesToCsvAsync();

            // 2. Translate (Python 스크립트는 동기적으로 실행되므로 Task.Run으로 감싸서 비동기처럼 처리)
            Debug.Log("Automating all localization steps: Step 2/3 - Running Python Translation Script...");
            bool pythonSuccess = await Task.Run(() => RunPythonScript()); // UI 스레드를 막지 않도록 Task.Run 사용

            if (!pythonSuccess)
            {
                Debug.LogError("Python script failed. Aborting automation.");
                return; // Python 스크립트 실패 시 중단
            }

            // 3. Import
            Debug.Log("Automating all localization steps: Step 3/3 - Importing Translated CSV...");
            await ImportTranslatedCsvToStringTablesAsync();

            Debug.Log("Automation completed successfully!");
        }
        catch (System.Exception e)
        {
            Debug.LogError($"Automation failed: {e.Message}");
        }
        finally
        {
            EditorUtility.ClearProgressBar(); // 작업 완료 또는 오류 시 프로그레스 바 숨기기
        }
    }
}