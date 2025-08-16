@echo off
echo 正在复制模组文件...

REM 设置源文件路径
set SOURCE_MIXIN=D:\Projects\ShrinkFrameworkGodotSharp\TheMixinMod\.godot\mono\temp\bin\Debug\TheMixinMod.dll
set SOURCE_BEMIXINED=D:\Projects\ShrinkFrameworkGodotSharp\TheModBeMixined\.godot\mono\temp\bin\Debug\TheModBeMixined.dll

REM 设置目标文件夹路径
set TARGET_MIXIN=C:\Users\ASUS\AppData\Roaming\Godot\app_userdata\ShrinkFrameworkGodotSharp\mods\TheMixinMod\
set TARGET_BEMIXINED=C:\Users\ASUS\AppData\Roaming\Godot\app_userdata\ShrinkFrameworkGodotSharp\mods\TheModBeMixined\

REM 创建目标文件夹（如果不存在）
if not exist "%TARGET_MIXIN%" mkdir "%TARGET_MIXIN%"
if not exist "%TARGET_BEMIXINED%" mkdir "%TARGET_BEMIXINED%"

REM 检查源文件是否存在并复制
if exist "%SOURCE_MIXIN%" (
    copy "%SOURCE_MIXIN%" "%TARGET_MIXIN%"
    echo ? TheMixinMod.dll 复制成功
) else (
    echo ? 源文件不存在: %SOURCE_MIXIN%
)

if exist "%SOURCE_BEMIXINED%" (
    copy "%SOURCE_BEMIXINED%" "%TARGET_BEMIXINED%"
    echo ? TheModBeMixined.dll 复制成功
) else (
    echo ? 源文件不存在: %SOURCE_BEMIXINED%
)

echo.
echo 复制操作完成！
pause