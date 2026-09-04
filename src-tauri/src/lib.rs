use serde::{Deserialize, Serialize};
use std::io::{BufRead, BufReader, Write};
use std::path::PathBuf;
use std::process::{Child, Command, Stdio};
use std::sync::Mutex;
use tauri_plugin_dialog::DialogExt;

const PIPE_NAME: &str = r"\\.\pipe\Glide.Backend.v1";

#[derive(Default)]
struct BackendProcess(Mutex<Option<Child>>);

impl Drop for BackendProcess {
    fn drop(&mut self) {
        if let Ok(mut child) = self.0.lock() {
            if let Some(process) = child.as_mut() {
                let _ = process.kill();
                let _ = process.wait();
            }
        }
    }
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct BackendInfo {
    status: String,
    protocol_version: Option<String>,
    version: Option<String>,
}

fn backend_candidates() -> Vec<PathBuf> {
    let mut candidates = Vec::new();
    if let Ok(path) = std::env::var("GLIDE_BACKEND_PATH") {
        candidates.push(PathBuf::from(path));
    }
    candidates.push(PathBuf::from(
        "src/app/bin/Release/net10.0-windows/Glide.exe",
    ));
    candidates.push(
        PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../src/app/bin/Release/net10.0-windows/Glide.exe"),
    );
    candidates.push(PathBuf::from("src/app/bin/Debug/net10.0-windows/Glide.exe"));
    candidates.push(
        PathBuf::from(env!("CARGO_MANIFEST_DIR"))
            .join("../src/app/bin/Debug/net10.0-windows/Glide.exe"),
    );
    candidates
}

fn ensure_backend_running(state: &BackendProcess) -> Result<(), String> {
    let mut child = state.0.lock().map_err(|_| "backend lock poisoned")?;
    if let Some(process) = child.as_mut() {
        if process.try_wait().map_err(|e| e.to_string())?.is_none() {
            return Ok(());
        }
        *child = None;
    }

    let current_exe = std::env::current_exe().ok();
    let path = backend_candidates()
        .into_iter()
        .find(|candidate| {
            if !candidate.is_file() {
                return false;
            }
            match (&current_exe, std::fs::canonicalize(candidate)) {
                (Some(current), Ok(candidate)) => {
                    std::fs::canonicalize(current).map_or(true, |current| candidate != current)
                }
                _ => true,
            }
        })
        .ok_or_else(|| "C# backend executable not found".to_string())?;

    let mut command = Command::new(path);
    command
        .arg("--backend")
        .stdin(Stdio::null())
        .stdout(Stdio::null())
        .stderr(Stdio::null());
    #[cfg(windows)]
    {
        use std::os::windows::process::CommandExt;
        command.creation_flags(0x08000000);
    }
    *child = Some(
        command
            .spawn()
            .map_err(|e| format!("failed to start backend: {e}"))?,
    );
    Ok(())
}

#[cfg(windows)]
fn query_backend_pipe(request: &str) -> Result<String, String> {
    use std::fs::OpenOptions;

    let file = OpenOptions::new()
        .read(true)
        .write(true)
        .create(false)
        .open(PIPE_NAME)
        .map_err(|e| format!("backend unavailable: {e}"))?;
    let mut reader = BufReader::new(file.try_clone().map_err(|e| e.to_string())?);
    let mut writer = file;
    writer
        .write_all(format!("{request}\n").as_bytes())
        .map_err(|e| e.to_string())?;
    writer.flush().map_err(|e| e.to_string())?;
    let mut response = String::new();
    reader.read_line(&mut response).map_err(|e| e.to_string())?;
    Ok(response.trim().to_string())
}

#[cfg(not(windows))]
fn query_backend_pipe(_request: &str) -> Result<String, String> {
    Err("named-pipe transport is only available on Windows".to_string())
}

#[tauri::command]
fn get_backend_status(state: tauri::State<'_, BackendProcess>) -> BackendInfo {
    if let Err(error) = ensure_backend_running(&state) {
        return BackendInfo {
            status: "disconnected".into(),
            protocol_version: None,
            version: Some(error),
        };
    }

    let status_response = (0..50).find_map(|_| match query_backend_pipe("status") {
        Ok(response) => Some(response),
        Err(_) => {
            std::thread::sleep(std::time::Duration::from_millis(100));
            None
        }
    });

    match status_response
        .ok_or_else(|| "backend did not become ready within 5 seconds".to_string())
        .and_then(|response| {
            let mut info: BackendInfo = serde_json::from_str(&response)
                .map_err(|e| format!("invalid backend response: {e}"))?;
            info.status = "connected".into();
            Ok(info)
        }) {
        Ok(info) => info,
        Err(error) => BackendInfo {
            status: "disconnected".into(),
            protocol_version: None,
            version: Some(error),
        },
    }
}

#[tauri::command]
fn get_settings(state: tauri::State<'_, BackendProcess>) -> Result<serde_json::Value, String> {
    ensure_backend_running(&state)?;
    let response = query_backend_pipe("settings")?;
    serde_json::from_str(&response).map_err(|e| format!("invalid settings response: {e}"))
}

#[tauri::command]
fn update_setting(
    field: String,
    value: bool,
    state: tauri::State<'_, BackendProcess>,
) -> Result<serde_json::Value, String> {
    ensure_backend_running(&state)?;
    let request = serde_json::json!({ "field": field, "value": value });
    let response = query_backend_pipe(&format!("update {}", request))?;
    serde_json::from_str(&response).map_err(|e| format!("invalid settings response: {e}"))
}

#[tauri::command]
fn update_profile_setting(
    field: String,
    value: serde_json::Value,
    state: tauri::State<'_, BackendProcess>,
) -> Result<serde_json::Value, String> {
    ensure_backend_running(&state)?;
    let request = serde_json::json!({ "field": field, "value": value });
    let response = query_backend_pipe(&format!("update-profile {}", request))?;
    serde_json::from_str(&response).map_err(|e| format!("invalid settings response: {e}"))
}

#[tauri::command]
fn update_per_app(
    operation: String,
    app_name: Option<String>,
    filter_mode: Option<String>,
    state: tauri::State<'_, BackendProcess>,
) -> Result<serde_json::Value, String> {
    ensure_backend_running(&state)?;
    let request = serde_json::json!({
        "operation": operation,
        "appName": app_name,
        "filterMode": filter_mode
    });
    let response = query_backend_pipe(&format!("per-app {}", request))?;
    serde_json::from_str(&response).map_err(|e| format!("invalid settings response: {e}"))
}

#[tauri::command]
fn run_diagnostics_action(
    action: String,
    state: tauri::State<'_, BackendProcess>,
) -> Result<(), String> {
    ensure_backend_running(&state)?;
    let response = query_backend_pipe(&format!("diagnostics {action}"))?;
    let result: serde_json::Value = serde_json::from_str(&response)
        .map_err(|e| format!("invalid diagnostics response: {e}"))?;
    if result.get("ok") == Some(&serde_json::Value::Bool(true)) {
        Ok(())
    } else {
        Err("diagnostics action failed".into())
    }
}

#[derive(Deserialize, Serialize)]
#[serde(rename_all = "camelCase")]
struct DiagnosticsLogResponse {
    text: String,
    line_count: u32,
    truncated: bool,
}

#[tauri::command]
fn get_diagnostics_log(
    state: tauri::State<'_, BackendProcess>,
) -> Result<DiagnosticsLogResponse, String> {
    ensure_backend_running(&state)?;
    let response = query_backend_pipe("diagnostics-log")?;
    serde_json::from_str(&response).map_err(|e| format!("invalid diagnostics response: {e}"))
}

#[tauri::command]
fn clear_diagnostics_log(state: tauri::State<'_, BackendProcess>) -> Result<(), String> {
    ensure_backend_running(&state)?;
    let response = query_backend_pipe("clear-diagnostics-log")?;
    let result: serde_json::Value = serde_json::from_str(&response)
        .map_err(|e| format!("invalid diagnostics response: {e}"))?;
    if result.get("ok") == Some(&serde_json::Value::Bool(true)) {
        Ok(())
    } else {
        Err("clearing diagnostics log failed".into())
    }
}

#[tauri::command]
fn export_settings(
    app: tauri::AppHandle,
    state: tauri::State<'_, BackendProcess>,
) -> Result<bool, String> {
    ensure_backend_running(&state)?;
    let settings = query_backend_pipe("export-settings")?;
    let Some(path) = app
        .dialog()
        .file()
        .add_filter("JSON settings", &["json"])
        .set_file_name("Glide-settings.json")
        .blocking_save_file()
    else {
        return Ok(false);
    };
    std::fs::write(path.into_path().map_err(|e| e.to_string())?, settings)
        .map_err(|e| format!("could not export settings: {e}"))?;
    Ok(true)
}

#[tauri::command]
fn import_settings(
    app: tauri::AppHandle,
    state: tauri::State<'_, BackendProcess>,
) -> Result<Option<serde_json::Value>, String> {
    ensure_backend_running(&state)?;
    let Some(path) = app
        .dialog()
        .file()
        .add_filter("JSON settings", &["json"])
        .blocking_pick_file()
    else {
        return Ok(None);
    };
    let contents = std::fs::read_to_string(path.into_path().map_err(|e| e.to_string())?)
        .map_err(|e| format!("could not read settings: {e}"))?;
    let request = serde_json::json!({ "contents": contents });
    let response = query_backend_pipe(&format!("import-settings {}", request))?;
    serde_json::from_str(&response)
        .map(Some)
        .map_err(|e| format!("invalid imported settings response: {e}"))
}

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .manage(BackendProcess::default())
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![
            get_backend_status,
            get_settings,
            update_setting,
            update_profile_setting,
            update_per_app,
            run_diagnostics_action,
            get_diagnostics_log,
            clear_diagnostics_log,
            export_settings,
            import_settings
        ])
        .run(tauri::generate_context!())
        .expect("error while running Glide Tauri application");
}
