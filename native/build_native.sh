#!/bin/bash
# 编译 taskbar_transparency.dll(需在 MSYS2 环境中执行;首次构建会自动生成 C++/WinRT 投影头)
set -e
export PATH="/mingw64/bin:$PATH"
cd "$(dirname "$0")"

INCLUDE=include
if [ ! -f "$INCLUDE/winrt/Windows.UI.Xaml.h" ]; then
    echo "Generating C++/WinRT projections from C:/Windows/System32/WinMetadata ..."
    mkdir -p "$INCLUDE"
    cppwinrt -in C:/Windows/System32/WinMetadata -out "$INCLUDE"
fi

g++ -std=c++20 -O2 -shared -static -static-libgcc -static-libstdc++ \
    -I "$INCLUDE" \
    taskbar_transparency.cpp \
    -o taskbar_transparency.dll \
    -lole32 -loleaut32 -lwindowsapp -luuid
echo "OK: $(pwd)/taskbar_transparency.dll"
ls -la taskbar_transparency.dll
