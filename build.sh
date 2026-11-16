dotnet publish bindings/NativeRKNNC -c Release -r $(python - <<'PY'
import platform, sys
sysname=platform.system(); arch=platform.machine().lower()
print({"Windows":"win-x64","Linux":"linux-x64","Darwin":"osx-arm64" if arch in ("arm64","aarch64") else "osx-x64"}[sysname])
PY
)