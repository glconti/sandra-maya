#!/usr/bin/env bash
set -Eeuo pipefail

app_home="${APP_HOME:-/app}"
data_root="${Storage__Root:-${APP_DATA_ROOT:-/data}}"
sqlite_path="${Storage__SqlitePath:-${APP_SQLITE_PATH:-${data_root}/sqlite/sandra-maya.db}}"
uploads_path="${Storage__UploadsPath:-${APP_UPLOADS_DIR:-${data_root}/files}}"
capabilities_path="${Storage__CapabilitiesPath:-${APP_CAPABILITIES_DIR:-${data_root}/capabilities}}"
generated_capabilities_path="${Storage__GeneratedCapabilitiesPath:-${APP_GENERATED_CAPABILITIES_DIR:-${capabilities_path}/generated}}"
work_path="${Storage__WorkPath:-${APP_WORK_DIR:-${data_root}/work}}"
temp_path="${Storage__TempPath:-${APP_TEMP_DIR:-${data_root}/tmp}}"

export Storage__Root="${data_root}"
export Storage__SqlitePath="${sqlite_path}"
export Storage__UploadsPath="${uploads_path}"
export Storage__CapabilitiesPath="${capabilities_path}"
export Storage__GeneratedCapabilitiesPath="${generated_capabilities_path}"
export Storage__WorkPath="${work_path}"
export Storage__TempPath="${temp_path}"
export TMPDIR="${TMPDIR:-${temp_path}}"

mkdir -p \
    "${data_root}" \
    "$(dirname "${sqlite_path}")" \
    "${uploads_path}" \
    "${capabilities_path}" \
    "${generated_capabilities_path}" \
    "${work_path}" \
    "${temp_path}"

touch "${sqlite_path}"

resolve_app_dll() {
    local configured_dll="${APP_DLL:-}"
    local configured_project="${APP_PROJECT_NAME:-}"

    if [[ -n "${configured_dll}" ]]; then
        if [[ -f "${configured_dll}" ]]; then
            printf '%s\n' "${configured_dll}"
            return 0
        fi

        if [[ -f "${app_home}/${configured_dll}" ]]; then
            printf '%s\n' "${app_home}/${configured_dll}"
            return 0
        fi

        printf 'Configured APP_DLL was not found: %s\n' "${configured_dll}" >&2
        return 1
    fi

    if [[ -n "${configured_project}" && -f "${app_home}/${configured_project}.runtimeconfig.json" ]]; then
        printf '%s\n' "${app_home}/${configured_project}.dll"
        return 0
    fi

    mapfile -t runtimeconfigs < <(find "${app_home}" -maxdepth 2 -type f -name '*.runtimeconfig.json' ! -name '*Test*.runtimeconfig.json' | sort)

    if [[ "${#runtimeconfigs[@]}" -eq 1 ]]; then
        printf '%s\n' "${runtimeconfigs[0]%.runtimeconfig.json}.dll"
        return 0
    fi

    if [[ "${#runtimeconfigs[@]}" -gt 1 ]]; then
        printf 'Multiple runnable .NET applications were found. Set APP_DLL or APP_PROJECT_NAME to one of:%s\n' "" >&2
        printf '  %s\n' "${runtimeconfigs[@]##*/}" >&2
        return 1
    fi

    printf '%s\n' ""
    return 0
}

app_dll="$(resolve_app_dll)"

if [[ -z "${app_dll}" ]]; then
    echo "Sandra Maya container foundation is ready, but no runnable .NET host was published to /app." >&2
    if [[ -f "${app_home}/BUILD_INFO.txt" ]]; then
        cat "${app_home}/BUILD_INFO.txt" >&2
    fi
    exit 1
fi

echo "Starting Sandra Maya with data root ${data_root}"
exec dotnet "${app_dll}"
